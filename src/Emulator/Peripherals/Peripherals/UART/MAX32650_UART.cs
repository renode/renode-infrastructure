//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.UART
{
    public class MAX32650_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public MAX32650_UART(IMachine machine, MAX32650_GCR gcr) : base(machine)
        {
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            IRQ = new GPIO();
            GCR = gcr;
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void WriteChar(byte value)
        {
            if(Count < FIFOBufferSize)
            {
                base.WriteChar(value);
            }
            else
            {
                interruptRxOverrunPending.Value |= true;
            }
            interruptRxFIFOLevelPending.Value |= Count >= (int)rxFIFOLevel.Value;
            UpdateInterrupts();
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            IRQ.Unset();
        }

        public override Bits StopBits => stopBits;

        public override Parity ParityBit => parityEnabled.Value ? parityBit : Parity.None;

        public override uint BaudRate
        {
            get
            {
                var clockFreq = clockSelect.Value ? InternalBitRateClockFrequency : GCR.SysClk / 2;
                var divider = (float)baudDividerInteger.Value + ((float)baudDividerDecimal.Value / 128.0);
                return (uint)((float)clockFreq / (divider * (1 << (int)baudClockDivider.Value)));
            }
        }

        public GPIO IRQ { get; }
        public long Size => 0x1000;

        protected override void CharWritten()
        {
            // intentionally left empty
        }

        protected override void QueueEmptied()
        {
            // intentionally left empty
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control0, new DoubleWordRegister(this, 0x00)
                    .WithFlag(0, out isEnabled, name: "CTRL1.ENABLE")
                    .WithFlag(1, out parityEnabled, name: "CTRL1.PARITY_EN")
                    .WithEnumField<DoubleWordRegister, ParityMode>(2, 2, name: "CTRL1.PARITY_MODE",
                        writeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case ParityMode.Even:
                                    parityBit = Parity.Even;
                                    break;
                                case ParityMode.Odd:
                                    parityBit = Parity.Odd;
                                    break;
                                default:
                                    this.Log(LogLevel.Warning, "Unsupported parity has been set");
                                    break;
                            }
                        })
                    .WithTaggedFlag("CTRL1.PARITY_LVL", 4)
                    .WithFlag(5, name: "CTRL1.TX_FLUSH", valueProviderCallback: _ => false)
                    .WithFlag(6, name: "CTRL1.RX_FLUSH",
                        valueProviderCallback: _ => false,
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                ClearBuffer();
                            }
                        })
                    .WithTaggedFlag("CTRL1.BITACC", 7)
                    .WithEnumField<DoubleWordRegister, CharacterSize>(8, 2, out characterSize, name: "CTRL1.SIZE",
                        writeCallback: (_, value) =>
                        {
                            if(value != CharacterSize.EightBits)
                            {
                                this.Log(LogLevel.Warning, "Character size set to {0}, but only {1} characters are supported", value, CharacterSize.EightBits);
                            }
                        })
                    .WithFlag(10, name: "CTRL1.STOP",
                        writeCallback: (_, value) =>
                        {
                            if(!value)
                            {
                                stopBits = Bits.One;
                            }
                            else if(value && characterSize.Value == CharacterSize.FiveBits)
                            {
                                stopBits = Bits.OneAndAHalf;
                            }
                            else
                            {
                                stopBits = Bits.Two;
                            }
                        })
                    .WithTaggedFlag("CTRL1.FLOW", 11)
                    .WithTaggedFlag("CTRL1.FLOWPOL", 12)
                    .WithTaggedFlag("CTRL1.NULLMOD", 13)
                    .WithTaggedFlag("CTRL1.BREAK", 14)
                    .WithFlag(15, out clockSelect, name: "CTRL1.CLK_SEL")
                    .WithTag("CTRL1.TIMEOUT_CNT", 16, 8)
                    .WithReservedBits(25, 7)
                },
                {(long)Registers.Control1, new DoubleWordRegister(this, 0x00)
                    .WithValueField(0, 6, out rxFIFOLevel, name: "CTRL2.RX_FIFO_LVL")
                    .WithReservedBits(6, 2)
                    .WithTag("CTRL2.TX_FIFO_LVL", 8, 6)
                    .WithReservedBits(14, 2)
                    .WithTag("CTRL2.RTS_FIFO_LVL", 16, 6)
                    .WithReservedBits(22, 10)
                },
                {(long)Registers.Status, new DoubleWordRegister(this, 0x50)
                    .WithFlag(0, FieldMode.Read, name: "STAT.TX_BUSY", valueProviderCallback: _ => false)
                    .WithFlag(1, FieldMode.Read, name: "STAT.RX_BUSY", valueProviderCallback: _ => false)
                    .WithTaggedFlag("STAT.PARITY", 2)
                    .WithTaggedFlag("STAT.BREAK", 3)
                    .WithFlag(4, FieldMode.Read, name: "STAT.RX_EMPTY", valueProviderCallback: _ => Count == 0)
                    .WithFlag(5, FieldMode.Read, name: "STAT.RX_FULL", valueProviderCallback: _ => Count > FIFOBufferSize)
                    .WithFlag(6, FieldMode.Read, name: "STAT.TX_EMPTY", valueProviderCallback: _ => true)
                    .WithFlag(7, FieldMode.Read, name: "STAT.TX_FULL", valueProviderCallback: _ => false)
                    .WithValueField(8, 6, FieldMode.Read, name: "STAT.RX_NUM", valueProviderCallback: _ => (uint)Count)
                    .WithReservedBits(14, 2)
                    .WithValueField(16, 6, FieldMode.Read, name: "STAT.TX_NUM", valueProviderCallback: _ => 0)
                    .WithReservedBits(22, 2)
                    .WithTaggedFlag("STAT.RX_TIMEOUT", 24)
                    .WithReservedBits(25, 7)
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this, 0x00)
                    // marked as a flag to limit the amount of log messages
                    .WithFlag(0, name: "INT_EN.RX_FRAME_ERROR")
                    // marked as a flag to limit the amount of log messages
                    .WithFlag(1, name: "INT_EN.RX_PARITY_ERROR")
                    .WithTaggedFlag("INT_EN.CTS", 2)
                    .WithFlag(3, out interruptRxOverrunEnabled, name: "INT_EN.RX_OVERRUN")
                    .WithFlag(4, out interruptRxFIFOLevelEnabled, name: "INT_EN.RX_FIFO_LVL")
                    // marked as a flag to limit the amount of log messages
                    .WithFlag(5, name: "INT_EN.TX_FIFO_AE")
                    // marked as a flag to limit the amount of log messages
                    .WithFlag(6, name: "INT_EN.TX_FIFO_LVL")
                    .WithTaggedFlag("INT_EN.BREAK", 7)
                    .WithTaggedFlag("INT_EN.RX_TIMEOUT", 8)
                    .WithTaggedFlag("INT_EN.LASTBREAK", 9)
                    .WithReservedBits(10, 22)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptFlags, new DoubleWordRegister(this, 0x00)
                    .WithTaggedFlag("INT_FL.FRAME", 0)
                    .WithTaggedFlag("INT_FL.PARITY", 1)
                    .WithTaggedFlag("INT_FL.CTS", 2)
                    .WithFlag(3, out interruptRxOverrunPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.RX_OVERRUN")
                    .WithFlag(4, out interruptRxFIFOLevelPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.RX_FIFO_LVL")
                    .WithTaggedFlag("INT_FL.TX_FIFO_AE", 5)
                    .WithTaggedFlag("INT_FL.TX_FIFO_LVL", 6)
                    .WithTaggedFlag("INT_FL.BREAK", 7)
                    .WithTaggedFlag("INT_FL.RX_TIMEOUT", 8)
                    .WithTaggedFlag("INT_FL.LASTBREAK", 9)
                    .WithReservedBits(10, 22)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.BaudInteger, new DoubleWordRegister(this, 0x00)
                    .WithValueField(0, 12, out baudDividerInteger, name: "BAUD0.IBAUD")
                    .WithReservedBits(12, 4)
                    .WithValueField(16, 3, out baudClockDivider, name: "BAUD0.CLKDIV")
                    .WithReservedBits(19, 13)
                },
                {(long)Registers.BaudDecimal, new DoubleWordRegister(this, 0x00)
                    .WithValueField(0, 7, out baudDividerDecimal, name: "BAUD1.DBAUD")
                    .WithReservedBits(7, 25)
                },
                {(long)Registers.FIFO, new DoubleWordRegister(this, 0x00)
                    .WithValueField(0, 8, name: "FIFO.FIFO",
                        valueProviderCallback: _ =>
                        {
                            if(!TryGetCharacter(out var character))
                            {
                                this.Log(LogLevel.Warning, "Trying to read from empty buffer");
                            }
                            return character;
                        },
                        writeCallback: (_, value) =>
                        {
                            if(isEnabled.Value)
                            {
                                TransmitCharacter((byte)value);
                            }
                        })
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.DMA, new DoubleWordRegister(this, 0x00)
                    .WithTaggedFlag("DMA.TXDMA_EN", 0)
                    .WithTaggedFlag("DMA.RXDMA_EN", 1)
                    .WithReservedBits(2, 6)
                    .WithTag("DMA.TXDMA_LVL", 8, 6)
                    .WithReservedBits(14, 2)
                    .WithTag("DMA.RXDMA_LVL", 16, 6)
                    .WithReservedBits(22, 10)
                },
                {(long)Registers.TxFIFO, new DoubleWordRegister(this, 0x00)
                    .WithValueField(0, 8, FieldMode.Read, name: "TXFIFO.DATA",
                        valueProviderCallback: _ => (byte)0x00)
                    .WithReservedBits(8, 24)
                },
            };

            return registersMap;
        }

        private void UpdateInterrupts()
        {
            var interruptPending = false;

            interruptPending |= interruptRxOverrunEnabled.Value && interruptRxOverrunPending.Value;
            interruptPending |= interruptRxFIFOLevelEnabled.Value && interruptRxFIFOLevelPending.Value;

            IRQ.Set(interruptPending);
        }

        private Bits stopBits;
        private Parity parityBit;

        private IFlagRegisterField isEnabled;
        private IFlagRegisterField parityEnabled;
        private IFlagRegisterField clockSelect;
        private IEnumRegisterField<CharacterSize> characterSize;

        private IValueRegisterField rxFIFOLevel;

        private IValueRegisterField baudClockDivider;
        private IValueRegisterField baudDividerInteger;
        private IValueRegisterField baudDividerDecimal;

        private IFlagRegisterField interruptRxOverrunEnabled;
        private IFlagRegisterField interruptRxFIFOLevelEnabled;

        private IFlagRegisterField interruptRxOverrunPending;
        private IFlagRegisterField interruptRxFIFOLevelPending;

        private const long FIFOBufferSize = 32;
        private const long InternalBitRateClockFrequency = 7372800; // Only used to calculate baudrate

        private readonly DoubleWordRegisterCollection registers;
        private readonly MAX32650_GCR GCR;

        private enum ParityMode : byte
        {
            Even,
            Odd,
            Mark,
            Space
        }

        private enum CharacterSize : byte
        {
            FiveBits,
            SixBits,
            SevenBits,
            EightBits
        }

        private enum Registers : long
        {
            Control0        = 0x00,
            Control1        = 0x04,
            Status          = 0x08,
            InterruptEnable = 0x0C,
            InterruptFlags  = 0x10,
            BaudInteger     = 0x14,
            BaudDecimal     = 0x18,
            FIFO            = 0x1C,
            DMA             = 0x20,
            TxFIFO          = 0x24
        }
    }
}
