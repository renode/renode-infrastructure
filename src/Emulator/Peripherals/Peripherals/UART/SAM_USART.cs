//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class SAM_USART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public SAM_USART(Machine machine) : base(machine)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(2, out var resetReceiver, FieldMode.Write, name: "RSTRX")
                    .WithFlag(3, out var resetTransmitter, FieldMode.Write, name: "RSTTX")

                    .WithFlag(4, out var enableReceiver, FieldMode.Write, name: "RXEN")
                    .WithFlag(5, out var disableReceiver, FieldMode.Write, name: "RXDIS")

                    .WithFlag(6, out var enableTransmitter, FieldMode.Write, name: "TXEN")
                    .WithFlag(7, out var disableTransmitter, FieldMode.Write, name: "TXDIS")

                    .WithWriteCallback((_, __) =>
                    {
                        if(resetReceiver.Value)
                        {
                            /* Clear FIFO */
                            ClearBuffer();
                            receiverEnabled = false;
                        }

                        /* Determine what to do with the Receiver */
                        if(disableReceiver.Value)
                        {
                            receiverEnabled = false;
                        }
                        else if(enableReceiver.Value)
                        {
                            receiverEnabled = true;
                        }

                        /* Determine what to do with the Transmitter */
                        if(disableTransmitter.Value || (resetTransmitter.Value && !enableTransmitter.Value))
                        {
                            transmitterEnabled = false;
                        }
                        else if(enableTransmitter.Value)
                        {
                            transmitterEnabled = true;
                        }
                    })
                },

                {(long)Registers.Mode, new DoubleWordRegister(this)
                    .WithValueField(0, 4, valueProviderCallback: _ => 0, writeCallback: (_, value) =>
                    {
                        if(value != 0)
                        {
                            this.Log(LogLevel.Warning, "Trying to configure the device to an unsupported mode!");
                        }
                    }, name: "USART_MODE")
                    .WithTag("USCLKS", 4, 2)
                    .WithTag("CHRL", 6, 2)
                    .WithTaggedFlag("SYNC", 8)

                    .WithEnumField(9, 3, out parityType, name: "PAR")
                    .WithEnumField(12, 2, out numberOfStopBits, name: "NBSTOP")

                    .WithTag("CHMODE", 14, 2)
                    .WithTaggedFlag("MSBF", 16)
                    .WithTaggedFlag("MODE9", 17)
                    .WithTaggedFlag("CLKO", 18)
                    .WithTaggedFlag("OVER", 19)
                    .WithTaggedFlag("INACK", 20)
                    .WithTaggedFlag("DSNACK", 21)
                    .WithTaggedFlag("VAR_SYNC", 22)
                    .WithTaggedFlag("INVDATA", 23)
                    .WithTag("MAX_ITERATION", 24, 3)
                    .WithReservedBits(27, 1)
                    .WithTaggedFlag("FILTER", 28)
                    .WithTaggedFlag("MAN", 29)
                    .WithTaggedFlag("MODSYNC", 30)
                    .WithTaggedFlag("ONEBIT", 31)
                },

                {(long)Registers.ChannelStatus, new DoubleWordRegister(this)
                    .WithFlag(0, out receiverReady, FieldMode.Read, name: "RXRDY")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return transmitterEnabled;
                    }, name: "TXRDY")
                },

                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            receiverReadyIrqEnabled.Value = true;
                        }
                    }, name: "IER_RXRDY")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            transmitterReadyIrqEnabled.Value = true;
                        }
                    }, name: "IER_TXRDY")
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },

                {(long)Registers.InterruptDisable, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            receiverReadyIrqEnabled.Value = false;
                        }
                    }, name: "IDR_RXRDY")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            transmitterReadyIrqEnabled.Value = false;
                        }
                    }, name: "IDR_TXRDY")
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },

                {(long)Registers.InterruptMask, new DoubleWordRegister(this)
                    .WithFlag(0, out receiverReadyIrqEnabled, FieldMode.Read, name: "IMR_RXRDY")
                    .WithFlag(1, out transmitterReadyIrqEnabled, FieldMode.Read, name: "IMR_TXRDY")
                },

                {(long)Registers.ReceiveHolding, new DoubleWordRegister(this)
                    .WithValueField(0, 9, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(!receiverEnabled)
                        {
                            return 0;
                        }

                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                        }
                        if(Count == 0)
                        {
                            receiverReady.Value = false;
                        }
                        UpdateInterrupts();
                        return character;
                    }, name: "RXCHR")
                    .WithTaggedFlag("RXSYNH", 15)
                },

                {(long)Registers.TransmitHolding, new DoubleWordRegister(this)
                    .WithValueField(0, 9, FieldMode.Write, writeCallback: (_, b) =>
                    {
                        if(!transmitterEnabled)
                        {
                            return;
                        }

                        this.TransmitCharacter((byte)b);
                        UpdateInterrupts();
                    }, name: "TXCHR")
                    .WithTaggedFlag("TXSYNH", 15)
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);

            IRQ = new GPIO();
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(innerLock)
            {
                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(innerLock)
            {
                registers.Write(offset, value);
            }
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; private set; }

        public override uint BaudRate => 115200;

        public override Bits StopBits
        {
            get
            {
                switch(numberOfStopBits.Value)
                {
                case NumberOfStopBitsValues.One:
                    return Bits.One;
                case NumberOfStopBitsValues.Half:
                    return Bits.Half;
                case NumberOfStopBitsValues.Two:
                    return Bits.Two;
                case NumberOfStopBitsValues.OneAndAHalf:
                    return Bits.OneAndAHalf;
                default:
                    throw new ArgumentException("Invalid number of stop bits");
                }
            }
        }

        public override Parity ParityBit
        {
            get
            {
                switch(parityType.Value)
                {
                case ParityTypeValues.Even:
                    return Parity.Even;
                case ParityTypeValues.Odd:
                    return Parity.Odd;
                case ParityTypeValues.Space:
                    return Parity.Forced0;
                case ParityTypeValues.Mark:
                    return Parity.Forced1;
                case ParityTypeValues.No:
                    return Parity.None;
                case ParityTypeValues.Multidrop:
                case ParityTypeValues.AlsoMultidrop:
                    return Parity.Multidrop;
                default:
                    throw new ArgumentException("Invalid parity type");
                }
            }
        }

        protected override void CharWritten()
        {
            receiverReady.Value = true;
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            IRQ.Set((receiverEnabled && receiverReadyIrqEnabled.Value && receiverReady.Value) || (transmitterEnabled && transmitterReadyIrqEnabled.Value));
        }

        private readonly IFlagRegisterField receiverReady;

        private readonly IFlagRegisterField receiverReadyIrqEnabled;
        private readonly IFlagRegisterField transmitterReadyIrqEnabled;

        private readonly DoubleWordRegisterCollection registers;

        private IEnumRegisterField<ParityTypeValues> parityType;
        private IEnumRegisterField<NumberOfStopBitsValues> numberOfStopBits;

        private bool receiverEnabled;
        private bool transmitterEnabled;

        private enum ParityTypeValues
        {
            Even = 0,
            Odd = 1,
            Space = 2,
            Mark = 3,
            No = 4,
            Multidrop = 7,
            AlsoMultidrop = 6
        }

        private enum NumberOfStopBitsValues
        {
            One = 0,
            Half = 1,
            Two = 2,
            OneAndAHalf = 3
        }

        private enum Registers
        {
            Control = 0x0,
            Mode = 0x04,
            InterruptEnable = 0x08,
            InterruptDisable = 0x0C,
            InterruptMask = 0x10,
            ChannelStatus = 0x14,
            ReceiveHolding = 0x18,
            TransmitHolding = 0x1C,
            BaudRateGenerator = 0x20,
            ReceiveTimeout = 0x24,
            TransmitterTimeguard = 0x28,
            FIDIRatio = 0x40,
            NumberOfErrors = 0x44,
            IrDAFilter = 0x4C,
            ManchesterConfiguration = 0x50,
            LINMode = 0x54,
            LINIdentifier = 0x58,
            LINBaudRate = 0x5C,
            LONMode = 0x60,
            LONPreamble = 0x64,
            LONDataLength = 0x68,
            LONL2HDR = 0x6C,
            LONBacklog = 0x70,
            LONBeta1TX = 0x74,
            LONBeta1RX = 0x78,
            LONPriority = 0x7C,
            LONIntermediateTimeAfterTransmission = 0x80,
            LONIntermediateTimeAfterReception = 0x84,
            ICDifferentiator = 0x88,
            WriteProtectionMode = 0xE4,
            WriteProtectionStatus = 0xE8
        }
    }
}
