//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class RenesasDA_SPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public RenesasDA_SPI(IMachine machine, int fifoDepth = DefaultFifoDepth) : base(machine)
        {
            IRQ = new GPIO();
            receiveQueue = new Queue<byte>();
            if(fifoDepth < 1)
            {
                throw new ConstructionException($"{nameof(fifoDepth)} value cannot be less than 1, got {fifoDepth}");
            }
            this.fifoDepth = fifoDepth;

            RegistersCollection = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            Reset();
        }

        public override void Reset()
        {
            receiveQueue.Clear();
            IRQ.Unset();
            RegistersCollection.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; } 
        public GPIO IRQ { get; }
        public long Size => 0x100;
        public bool ChipSelectEnableValue { get; set; }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, out spiEnabled, name: "SPI_EN")
                    .WithFlag(1, out transmitEnabled, name: "SPI_TX_EN")
                    .WithFlag(2, out receiveEnabled, name: "SPI_RX_EN")
                    .WithTaggedFlag("SPI_DMA_TX_EN", 3)
                    .WithTaggedFlag("SPI_DMA_RX_EN", 4)
                    .WithFlag(5, FieldMode.Read | FieldMode.WriteOneToClear, name: "SPI_FIFO_RESET",
                        changeCallback: (_, value) =>
                        {
                            if(!value)
                            {
                                return;
                            }

                            this.DebugLog("Resetting FIFO");
                            receiveQueue.Clear();
                        })
                    .WithTaggedFlag("SPI_CAPTURE_AT_NEXT_EDGE", 6)
                    .WithTaggedFlag("SPI_SWAP_BYTES", 7)
                    .WithReservedBits(8, 24)
                },

                {(long)Registers.Clock, new DoubleWordRegister(this)
                    .WithTag("SPI_CLK_DIV", 0, 7)
                    .WithReservedBits(7, 25)
                },

                {(long)Registers.Configuration, new DoubleWordRegister(this)
                    .WithTag("SPI_MODE", 0, 2)
                    .WithValueField(2, 5, out wordBits, name: "SPI_WORD_LENGTH",
                        changeCallback: (_, value) =>
                        {
                            var length = value + WordBitsOffset;
                            if(length != 8)
                            {
                                this.ErrorLog("Word length of {0} bits is currently not supported. Reverting to word length of 8 bits");
                                wordBits.Value = length - WordBitsOffset;
                            }
                        })
                    .WithTaggedFlag("SPI_SLAVE_EN", 7)
                    .WithReservedBits(8, 24)
                },

                {(long)Registers.FifoConfiguration, new DoubleWordRegister(this)
                    .WithTag("SPI_TX_TL", 0, 4)
                    .WithValueField(4, 4, out receiveFIFOThresholdLevel, name: "SPI_RX_TL", changeCallback: (_, __) => UpdateInterrupts())
                    .WithReservedBits(8, 24)
                },

                {(long)Registers.InterruptMask, new DoubleWordRegister(this)
                    .WithFlag(0, out fifoTransmitEmptyInterruptEnabled, name: "SPI_IRQ_MASK_TX_EMPTY")
                    .WithFlag(1, out fifoReceiveFullInterruptEnabled, name: "SPI_IRQ_MASK_RX_FULL")
                    .WithReservedBits(2, 30)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },

                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, name: "SPI_STATUS_TX_EMPTY", valueProviderCallback: _ => true)
                    .WithFlag(1, FieldMode.Read, name: "SPI_STATUS_RX_FULL", valueProviderCallback: _ => IsReceiveFIFOFull)
                    .WithReservedBits(2, 30)
                },

                {(long)Registers.FifoStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 6, FieldMode.Read, name: "SPI_RX_FIFO_LEVEL", valueProviderCallback: _ => (ulong)Math.Min(fifoDepth, receiveQueue.Count))
                    .WithValueField(6, 6, FieldMode.Read, name: "SPI_TX_FIFO_LEVEL", valueProviderCallback: _ => 0)
                    .WithFlag(12, FieldMode.Read, name: "SPI_STATUS_RX_EMPTY", valueProviderCallback: _ => receiveQueue.Count == 0)
                    .WithFlag(13, FieldMode.Read, name: "SPI_STATUS_TX_FULL", valueProviderCallback: _ => false)
                    .WithFlag(14, FieldMode.Read, name: "SPI_RX_FIFO_OVFL", valueProviderCallback: _ => false)
                    .WithFlag(15, FieldMode.Read, name: "SPI_TRANSACTION_ACTIVE", valueProviderCallback: _ => false)
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.FifoRead, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "SPI_FIFO_READ",
                        valueProviderCallback: _ =>
                        {
                            if(!spiEnabled.Value)
                            {
                                this.WarningLog("SPI Module is disabled, returning 0");
                                return 0;
                            }

                            if(!receiveEnabled.Value)
                            {
                                this.WarningLog("Reception is disabled, returning 0");
                                return 0;
                            }

                            if(!receiveQueue.TryDequeue(out byte result))
                            {
                                this.WarningLog("Reading from a empty receive FIFO, returning 0");
                                return 0x0;
                            }
                            UpdateInterrupts();
                            return result;
                        })
                },

                {(long)Registers.FifoWrite, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, name: "SPI_FIFO_WRITE",
                        writeCallback: (_, value) =>
                        {
                            if(!spiEnabled.Value)
                            {
                                this.WarningLog("SPI Module is disabled, returning");
                                return;
                            }

                            if(!transmitEnabled.Value)
                            {
                                this.WarningLog("Transmission is disabled, returning");
                                return;
                            }

                            if(selectedPeripheral == null)
                            {
                                this.WarningLog("No peripheral is selected, returning");
                                return;
                            }

                            receiveQueue.Enqueue(selectedPeripheral.Transmit((byte)value));
                            UpdateInterrupts();
                        })
                },

                {(long)Registers.CsConfiguration, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, ChipSelect>(0, 3, name: "SPI_CS_SELECT",
                        changeCallback: (_, value) => SwitchActivePeripheral(value))
                    .WithReservedBits(3, 29)
                },

                {(long)Registers.TXBufferForce, new DoubleWordRegister(this)
                    .WithTag("SPI_TXBUFFER_FORCE", 0, 32)
                }
            };

            return registerMap;
        }

        private void UpdateInterrupts()
        {
            var state = fifoTransmitEmptyInterruptEnabled.Value || (fifoReceiveFullInterruptEnabled.Value && IsReceiveFIFOFull);
            this.DebugLog("IRQ {0}", state ? "set" : "unset");
            IRQ.Set(state);
        }

        private void SwitchActivePeripheral(ChipSelect value)
        {
            switch(value)
            {
                case ChipSelect.None:
                {
                    if(selectedPeripheral is IGPIOReceiver receiver)
                    {
                        receiver.OnGPIO(0, !ChipSelectEnableValue);
                    }
                    else
                    {
                        selectedPeripheral?.FinishTransmission();
                    }
                    selectedPeripheral = null;
                    return;
                }
                case ChipSelect.SPIEnable:
                case ChipSelect.SPIEnable2:
                case ChipSelect.GPIO:
                {
                    if(selectedPeripheral is IGPIOReceiver oldReceiver)
                    {
                        oldReceiver.OnGPIO(0, !ChipSelectEnableValue);
                    }
                    else
                    {
                        selectedPeripheral?.FinishTransmission();
                    }
                    
                    var address = ChipSelectToAddress(value);
                    if(!TryGetByAddress(address, out var peripheral))
                    {
                        this.WarningLog("No peripheral with address {0} exists (Chip select: {1})", address, value);
                        selectedPeripheral = null;
                        return;
                    }

                    if(peripheral is IGPIOReceiver newReceiver)
                    {
                        newReceiver.OnGPIO(0, ChipSelectEnableValue);
                    }

                    selectedPeripheral = peripheral;
                    return;
                }
                default:
                    this.ErrorLog("Invalid chip select value 0x{0:X}", value);
                    return;
            }
        }

        private int ChipSelectToAddress(ChipSelect chipSelect)
        {
            switch(chipSelect)
            {
                case ChipSelect.SPIEnable:
                    return 1;
                case ChipSelect.SPIEnable2:
                    return 2;
                case ChipSelect.GPIO:
                    return 3;
                default:
                    return -1;
            }
        }

        private bool IsReceiveFIFOFull => (ulong)receiveQueue.Count >= (receiveFIFOThresholdLevel.Value + 1);

        private readonly Queue<byte> receiveQueue;
        private readonly int fifoDepth;

        private ISPIPeripheral selectedPeripheral;

        private IFlagRegisterField spiEnabled;
        private IFlagRegisterField transmitEnabled;
        private IFlagRegisterField receiveEnabled;
        private IFlagRegisterField fifoTransmitEmptyInterruptEnabled;
        private IFlagRegisterField fifoReceiveFullInterruptEnabled;
        private IValueRegisterField receiveFIFOThresholdLevel;
        private IValueRegisterField wordBits;

        private const int DefaultFifoDepth = 4;
        private const int WordBitsOffset = 1;

        public enum Registers
        {
            Control           = 0x0,  // SPI_CTRL_REG
            Configuration     = 0x4,  // SPI_CONFIG_REG
            Clock             = 0x8,  // SPI_CLOCK_REG
            FifoConfiguration = 0xC,  // SPI_FIFO_CONFIG_REG
            InterruptMask     = 0x10, // SPI_IRQ_MASK_REG
            Status            = 0x14, // SPI_STATUS_REG
            FifoStatus        = 0x18, // SPI_FIFO_STATUS_REG
            FifoRead          = 0x1C, // SPI_FIFO_READ_REG
            FifoWrite         = 0x20, // SPI_FIFO_WRITE_REG
            CsConfiguration   = 0x24, // SPI_CS_CONFIG_REG
            TXBufferForce     = 0x2C, // SPI_TXBUFFER_FORCE_REG
        }

        private enum ChipSelect
        {
            None        = 0,
            SPIEnable   = 1,
            SPIEnable2  = 2,
            GPIO        = 4,
        }
    }
}
