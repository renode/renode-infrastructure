//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    public class NPCX_UART : UARTBase, IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IKnownSize
    {
        public NPCX_UART(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
            DMAReceive = new GPIO();

            RegistersCollection = new ByteRegisterCollection(this, BuildRegisterMap());

            Reset();
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public override void Reset()
        {
            stopBits = false;
            parityEnable = false;
            parityMode = ParityMode.Odd;
            divisor = 1;
            rxFullLevelSelect = 1;

            base.Reset();
            RegistersCollection.Reset();
            IRQ.Unset();
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x2D;
        public GPIO IRQ { get; }
        public GPIO DMAReceive { get; }

        public ByteRegisterCollection RegistersCollection { get; }

        public override Bits StopBits => stopBits ? Bits.Two : Bits.One;

        public override Parity ParityBit
        {
            get
            {
                if(!parityEnable)
                {
                    return Parity.None;
                }

                switch(parityMode)
                {
                    case ParityMode.Odd:
                        return Parity.Odd;
                    case ParityMode.Even:
                        return Parity.Even;
                    case ParityMode.Mark:
                        return Parity.Forced1;
                    case ParityMode.Space:
                        return Parity.Forced0;
                }

                throw new Exception("Unreachable");
            }
        }

        // It's supposed to be APB4_CLK / (16 * DIV * P), where:
        //     APB4_CLK is the APB4 clock frequency
        //     DIV = UDIV10-0 + 1
        //     P is the "Prescaler Factor" selected by UPSC field
        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
            UpdateInterrupts();
            if(receiveDMAEnabled.Value)
            {
                // This blink is used to signal the DMA that it should perform the peripheral -> memory transaction now.
                // Without this signal DMA will never move data from the receive FIFO to memory.
                // See NPCX_MDMA:OnGPIO
                DMAReceive.Blink();
            }
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private void UpdateInterrupts()
        {
            var rxNonEmpty = rxNonEmptyInterruptEnable.Value && Count != 0;
            var rxFullLevel = rxFullLevelInterruptEnable.Value && RxFullLevelStatus;

            var status = rxNonEmpty || rxFullLevel || transmitFifoEmptyInterruptEnable.Value || noTransmitInProgressInterruptEnable.Value;
            this.Log(LogLevel.Noisy, "IRQ set to {0}", status);
            IRQ.Set(status);
        }

        private Dictionary<long, ByteRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.TransmitBuffer, new ByteRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, name: "UTBUF",
                        writeCallback: (_, value) => this.TransmitCharacter((byte)value)
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.ReceiveBuffer, new ByteRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "URBUF",
                        valueProviderCallback: _ =>
                        {
                            if(!TryGetCharacter(out var character))
                            {
                                return 0xFF;
                            }
                            return character;
                        }
                    )
                    .WithReadCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Status, new ByteRegister(this)
                    .WithReservedBits(5, 3)
                    .WithTaggedFlag("BKD (Break Detect)", 4)
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("DOE (Data Overrun Error)", 2)
                    .WithTaggedFlag("FE (Framing Error)", 1)
                    .WithTaggedFlag("PE (Parity Error)", 0)
                },
                {(long)Registers.FrameSelect, new ByteRegister(this)
                    .WithReservedBits(7, 1)
                    .WithFlag(6, name: "PEN (Parity Enable)",
                        valueProviderCallback: _ => parityEnable,
                        writeCallback: (_, val) => parityEnable = val
                    )
                    .WithEnumField<ByteRegister, ParityMode>(4, 2, name: "PSEL (Parity Select)",
                        valueProviderCallback: _ => parityMode,
                        writeCallback: (_, val) => parityMode = val
                    )
                    .WithReservedBits(3, 1)
                    .WithFlag(2, name: "STP (Stop Bits)",
                        valueProviderCallback: _ => stopBits,
                        writeCallback: (_, val) => stopBits = val
                    )
                    .WithReservedBits(0, 2)
                },
                {(long)Registers.ModeSelect, new ByteRegister(this)
                    .WithReservedBits(6, 2)
                    .WithFlag(5, out receiveDMAEnabled, name: "ERD (Enable Receive DMA)")
                    .WithTaggedFlag("ETD (Enable Transmit DMA)", 4)
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("BRK (Break Transmit)", 2)
                    .WithReservedBits(0, 2)
                },
                {(long)Registers.BaudRateDivisor, new ByteRegister(this)
                    .WithValueField(0, 8, name: "UDIV7-0 (UART Divisor Bits 7-0)",
                        valueProviderCallback: _ => divisor,
                        writeCallback: (_, val) => divisor = (divisor & 0x700) | (uint)val
                    )
                },
                {(long)Registers.BaudRatePrescaler, new ByteRegister(this)
                    .WithTag("UPSC (Prescaler Select)", 3, 5)
                    .WithValueField(0, 3, name: "UDIV10-8 (UART Divisor Bits 10-8)",
                        valueProviderCallback: _ => divisor >> 8,
                        writeCallback: (_, val) => divisor = (divisor & 0xff) | ((uint)val << 8)
                    )
                },
                {(long)Registers.TransmitStatus, new ByteRegister(this)
                    .WithFlag(7, FieldMode.Read, name: "XMIP (No Transmit in Progress)",
                        // 0: CR_UART is transmitting.
                        // 1: CR_UART is not transmitting (default).
                        valueProviderCallback: _ => true
                    )
                    .WithFlag(6, FieldMode.Read, name: "TFIFO_EMPTY_STS (Transmit FIFO Empty Status)",
                        valueProviderCallback: _ => true
                    )
                    .WithTaggedFlag("TEMPTY_LEVEL_STS (Transmit FIFO Empty Level Status)", 5)
                    .WithValueField(0, 5, FieldMode.Read, name: "TEMPTY_LEVEL (Transmit FIFO Empty Level)",
                        valueProviderCallback: _ => (ulong)MaxQueueCount
                    )
                },
                {(long)Registers.ReceiveStatus, new ByteRegister(this)
                    .WithTaggedFlag("ERR (Receive Error)", 7)
                    .WithFlag(6, FieldMode.Read, name: "RFIFO_NEMPTY_STS (Receive FIFO Not Empty Status)",
                        valueProviderCallback: _ => Count != 0
                    )
                    .WithFlag(5, FieldMode.Read, name: "RFULL_LEVEL_STS (Receive FIFO Full Level Status)",
                        valueProviderCallback: _ => RxFullLevelStatus
                    )
                    .WithValueField(0, 5, FieldMode.Read, name: "RFULL_LEVEL (Receive FIFO Full Level)",
                        valueProviderCallback: _ => (ulong)(Count >= MaxQueueCount ? MaxQueueCount : Count)
                    )
                },
                {(long)Registers.TransmitControl, new ByteRegister(this)
                    .WithFlag(7, out noTransmitInProgressInterruptEnable, name: "XMIP_EN (No Transmit in Progress Interrupt Enable)")
                    .WithFlag(6, out transmitFifoEmptyInterruptEnable, name: "TFIFO_EMPTY_EN (Transmit FIFO Empty Interrupt Enable)")
                    .WithTaggedFlag("TEMPTY_LEVEL_EN (Transmit FIFO Empty Level Interrupt Enable)", 5)
                    .WithTag("TEMPTY_LEVEL_SEL (Transmit FIFO Empty Level Select)", 0, 5)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.ReceiveControl, new ByteRegister(this)
                    .WithTaggedFlag("ERR_EN (Receive Error Interrupt Enable)", 7)
                    .WithFlag(6, out rxNonEmptyInterruptEnable, name: "RFIFO_NEMPTY_EN (Receive FIFO Not Empty Status Interrupt Enable)")
                    .WithFlag(5, out rxFullLevelInterruptEnable, name: "RFULL_LEVEL_EN (Receive FIFO Full Level Status Interrupt Enable)")
                    .WithValueField(0, 5, name: "RFULL_LEVEL_SEL (Receive FIFO Full Level Select)",
                        valueProviderCallback: _ => (ulong)rxFullLevelSelect,
                        writeCallback: (_, val) => rxFullLevelSelect = (int)val
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Control, new ByteRegister(this)
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("COM_FDBK_EN (Common Mode Feedback Enable)", 6)
                    .WithTaggedFlag("CR_SOUT_PP (CR_SOUT Common Push-Pull Select)", 5)
                    .WithTaggedFlag("CR_SOUT_COM (CR_SOUT Common Mode Select)", 4)
                    .WithTaggedFlag("CR_SIN_PP (CR_SIN Common Push-Pull Select)", 3)
                    .WithTaggedFlag("CR_SIN_COM (CR_SIN Common Mode Select)", 2)
                    .WithTaggedFlag("CR_SOUT_INV (CR_SOUT Signal Invert)", 1)
                    .WithTaggedFlag("CR_SIN_INV (CR_SIN Signal Invert)", 0)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
            };

            return registerMap;
        }

        private bool RxFullLevelStatus => Count >= rxFullLevelSelect;

        private IFlagRegisterField receiveDMAEnabled;

        private bool stopBits;
        private bool parityEnable;
        private ParityMode parityMode;
        private uint divisor;
        private int rxFullLevelSelect;

        private IFlagRegisterField rxNonEmptyInterruptEnable;
        private IFlagRegisterField rxFullLevelInterruptEnable;
        private IFlagRegisterField noTransmitInProgressInterruptEnable;
        private IFlagRegisterField transmitFifoEmptyInterruptEnable;

        private const int MaxQueueCount = 16;

        private enum ParityMode
        {
            Odd   = 0b00,
            Even  = 0b01,
            Mark  = 0b10,
            Space = 0b11,
        }

        private enum Registers : long
        {
            TransmitBuffer    = 0x00, // UTBUF
            ReceiveBuffer     = 0x02, // URBUF
            Status            = 0x06, // USTAT
            FrameSelect       = 0x08, // UFRS
            ModeSelect        = 0x0A, // UMDSL
            BaudRateDivisor   = 0x0C, // UBAUD
            BaudRatePrescaler = 0x0E, // UPSR
            TransmitStatus    = 0x20, // UFTSTS
            ReceiveStatus     = 0x22, // UFRSTS
            TransmitControl   = 0x24, // UFTCTL
            ReceiveControl    = 0x26, // UFRCTL
            Control           = 0x2C, // UCNTL
        }
    }
}
