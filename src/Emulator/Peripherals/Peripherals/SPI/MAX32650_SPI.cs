//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class MAX32650_SPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize
    {
        public MAX32650_SPI(IMachine machine, int numberOfSlaves, bool hushTxFifoLevelWarnings = false) : base(machine)
        {
            if(numberOfSlaves < 0 || numberOfSlaves > MaximumNumberOfSlaves)
            {
                throw new ConstructionException($"numberOfSlaves should be between 0 and {MaximumNumberOfSlaves - 1}");
            }

            IRQ = new GPIO();
            NumberOfSlaves = numberOfSlaves;

            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            rxQueue = new Queue<byte>();
            txQueue = new Queue<byte>();
            shouldDeassert = new bool[numberOfSlaves];

            this.hushTxFifoLevelWarnings = hushTxFifoLevelWarnings;
        }

        public override void Reset()
        {
            IRQ.Unset();
            registers.Reset();

            rxQueue.Clear();
            txQueue.Clear();

            charactersToTransmit = 0;
            transactionInProgress = false;

            for(var i = 0; i < NumberOfSlaves; ++i)
            {
                shouldDeassert[i] = false;
            }
        }

        public byte ReadByte(long address)
        {
            if(address >= (long)Registers.FIFOData + FIFODataWidth)
            {
                this.Log(LogLevel.Warning, "Tried to perform byte read from different register than FIFO; ignoring");
                return 0x00;
            }
            return RxDequeue();
        }

        public void WriteByte(long address, byte value)
        {
            if(address >= (long)Registers.FIFOData + FIFODataWidth)
            {
                this.Log(LogLevel.Warning, "Tried to perform byte write to different register than FIFO; ignoring");
                return;
            }
            TxEnqueue(value);
        }

        public ushort ReadWord(long address)
        {
            if(address >= (long)Registers.FIFOData + FIFODataWidth)
            {
                this.Log(LogLevel.Warning, "Tried to perform word read from different register than FIFO; ignoring");
                return 0x00;
            }

            var value1 = RxDequeue();
            var value2 = (ushort)RxDequeue() << 8;
            return (ushort)(value1 | value2);
        }

        public void WriteWord(long address, ushort value)
        {
            if(address >= (long)Registers.FIFOData + FIFODataWidth)
            {
                this.Log(LogLevel.Warning, "Tried to perform word write to different register than FIFO; ignoring");
                return;
            }
            TxEnqueue((byte)value);
            TxEnqueue((byte)(value >> 8));
        }

        public uint ReadDoubleWord(long address)
        {
            return registers.Read(address);
        }

        public void WriteDoubleWord(long address, uint value)
        {
            registers.Write(address, value);
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        public int NumberOfSlaves { get; }

        private void UpdateInterrupts()
        {
            interruptRxLevelPending.Value = rxQueue.Count >= (int)rxFIFOThreshold.Value;
            interruptTxLevelPending.Value = txQueue.Count >= (int)txFIFOThreshold.Value;

            var pending = false;
            pending |= interruptTxLevelEnabled.Value && interruptTxLevelPending.Value;
            pending |= interruptTxEmptyEnabled.Value && interruptTxEmptyPending.Value;
            pending |= interruptRxLevelEnabled.Value && interruptRxLevelPending.Value;
            pending |= interruptRxFullEnabled.Value && interruptRxFullPending.Value;
            pending |= interruptTransactionFinishedEnabled.Value && interruptTransactionFinishedPending.Value;
            pending |= interruptRxOverrunEnabled.Value && interruptRxOverrunPending.Value;
            pending |= interruptRxUnderrunEnabled.Value && interruptRxUnderrunPending.Value;
            IRQ.Set(pending);
        }

        private void DeassertCS(Func<int, bool> predicate)
        {
            foreach(var indexPeripheral in ActivePeripherals)
            {
                var index = indexPeripheral.Item1;
                var peripheral = indexPeripheral.Item2;

                if(predicate(index))
                {
                    peripheral.FinishTransmission();
                    shouldDeassert[index] = false;
                }
            }
        }

        private void StartTransaction()
        {
            // deassert CS of active peripherals that are not enabled in the slave select register anymore
            DeassertCS(x => !BitHelper.IsBitSet((uint)slaveSelect.Value, (byte)x));

            foreach(var value in txQueue)
            {
                Transmit(value);
            }

            txQueue.Clear();

            transactionInProgress = true;
            interruptTxEmptyPending.Value = true;

            UpdateInterrupts();
            TryFinishTransaction();
        }

        private void TryFinishTransaction()
        {
            if(charactersToTransmit > 0)
            {
                return;
            }

            transactionInProgress = false;
            interruptTransactionFinishedPending.Value = true;

            // deassert CS of active peripherals marked in the should deassert array
            DeassertCS(x => shouldDeassert[x]);
            UpdateInterrupts();
        }

        private void Transmit(byte value)
        {
            var numberOfPeripherals = ActivePeripherals.Count();
            foreach(var indexPeripheral in ActivePeripherals)
            {
                var peripheral = indexPeripheral.Item2;
                var output = peripheral.Transmit(value);
                // In case multiple SS lines are chosen, we are deliberately
                // ignoring output from all of them. Therefore, this configuration
                // can only be used to send data to multiple receivers at once.
                if(numberOfPeripherals == 1)
                {
                    RxEnqueue(output);
                }
            }

            if(numberOfPeripherals == 0)
            {
                // If there is no target device we still need to populate the RX queue
                // with dummy bytes
                RxEnqueue(DummyResponseByte);
            }

            charactersToTransmit -= 1;
            TryFinishTransaction();
        }

        private void TryTransmit()
        {
            if(!transactionInProgress || rxQueue.Count == FIFOLength || txQueue.Count == 0)
            {
                return;
            }

            var bytesToTransmit = Math.Min(FIFOLength - rxQueue.Count, txQueue.Count);
            for(var i = 0; i < bytesToTransmit; ++i)
            {
                Transmit(txQueue.Dequeue());
            }
        }

        private void RxEnqueue(byte value)
        {
            if(!rxFIFOEnabled.Value)
            {
                return;
            }

            if(rxQueue.Count == FIFOLength)
            {
                interruptRxOverrunPending.Value = true;
                UpdateInterrupts();
                return;
            }
            rxQueue.Enqueue(value);
            if(rxQueue.Count == FIFOLength)
            {
                interruptRxFullPending.Value = true;
                UpdateInterrupts();
            }
        }

        private byte RxDequeue()
        {
            if(!rxFIFOEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Tried to read from RX FIFO while it's disabled");
                return 0x00;
            }

            if(!rxQueue.TryDequeue(out var result))
            {
                interruptRxUnderrunPending.Value |= true;
            }
            else
            {
                TryTransmit();
            }

            TryFinishTransaction();
            UpdateInterrupts();

            return result;
        }

        private void TxEnqueue(byte value)
        {
            if(transactionInProgress && rxQueue.Count < FIFOLength)
            {
                // If we have active transaction and we have room to receive data,
                // send/receive it immediately
                Transmit(value);
            }
            else
            {
                // Otherwise, we either generate TX overrun interrupt if internal
                // TX buffer is full, or enqueue new data to it. This data will be
                // send either after START condition, or when there is room in RX
                // buffer when transaction is active
                if(txQueue.Count == FIFOLength)
                {
                    interruptTxOverrunPending.Value = true;
                }
                else
                {
                    txQueue.Enqueue(value);
                }
            }
            UpdateInterrupts();
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>()
            {
                {(long)Registers.FIFOData, new DoubleWordRegister(this)
                    .WithValueFields(0, 8, FIFODataWidth, name: "DATA.data",
                        valueProviderCallback: (_, __) => RxDequeue(),
                        writeCallback: (_, __, value) => TxEnqueue((byte)value))
                },
                {(long)Registers.MasterSignalsControl, new DoubleWordRegister(this)
                    .WithFlag(0, out var spiEnabled, name: "CTRL0.spi_en",
                        // deassert all CS lines when disabling the controller
                        writeCallback: (_, value) => { if(!value) DeassertCS(x => true); })
                    .WithFlag(1, name: "CTRL0.mm_en",
                        changeCallback: (_, value) =>
                        {
                            if(!value && spiEnabled.Value)
                            {
                                this.Log(LogLevel.Warning, "CTRL0.mm_en has been unset, but only Master mode is supported");
                            }
                        })
                    .WithReservedBits(2, 2)
                    .WithTaggedFlag("CTRL0.ss_io", 4)
                    .WithFlag(5, FieldMode.WriteOneToClear, name: "CTRL0.start",
                        writeCallback: (_, value) => { if(value) StartTransaction(); })
                    .WithReservedBits(6, 2)
                    .WithFlag(8, name: "CTRL0.ss_ctrl",
                        changeCallback: (_, value) =>
                        {
                            foreach(var indexPeripheral in ActivePeripherals)
                            {
                                shouldDeassert[indexPeripheral.Item1] |= !value;
                            }
                        })
                    .WithReservedBits(9, 7)
                    .WithValueField(16, 4, out slaveSelect, name: "CTRL0.ss_sel",
                        changeCallback: (_, value) =>
                        {
                            for(var i = 0; i < NumberOfSlaves; ++i)
                            {
                                if(BitHelper.IsBitSet(value, (byte)i) && !TryGetByAddress(i, out var __))
                                {
                                    this.Log(LogLevel.Warning, "Tried to select SS{0}, but it's not connected to anything; ignoring", i);
                                    BitHelper.SetBit(ref value, (byte)i, false);
                                }
                            }
                            slaveSelect.Value = value;
                        })
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.TrasmitPacketSize, new DoubleWordRegister(this)
                    .WithValueField(0, 16, name: "CTRL1.tx_num_char",
                        writeCallback: (_, value) => charactersToTransmit = (uint)value)
                    .WithTag("CTRL1.rx_num_char", 16, 16)
                },
                {(long)Registers.StaticConfiguration, new DoubleWordRegister(this)
                    .WithTaggedFlag("CTRL2.clk_pha", 0)
                    .WithTaggedFlag("CTRL2.clk_pol", 1)
                    .WithReservedBits(2, 6)
                    .WithValueField(8, 4, name: "CTRL2.num_bits",
                        writeCallback: (_, value) =>
                        {
                            if(value >= 1 && value != 8)
                            {
                                this.Log(LogLevel.Warning, "Only 8-bit characters are supported, but tried to change to {0}-bit characters; ignored", value);
                            }
                        })
                    .WithTag("CTRL2.bus_width", 12, 2)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("CTRL2.three_wire", 15)
                    .WithTag("CTRL2.ss_pol", 16, 4)
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.InterruptStatusFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out interruptTxLevelPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.tx_level")
                    .WithFlag(1, out interruptTxEmptyPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.tx_empty")
                    .WithFlag(2, out interruptRxLevelPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.rx_level")
                    .WithFlag(3, out interruptRxFullPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.rx_full")
                    .WithTaggedFlag("INT_FL.ssa", 4)
                    .WithTaggedFlag("INT_FL.ssd", 5)
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("INT_FL.fault", 8)
                    .WithTaggedFlag("INT_FL.abort", 9)
                    .WithReservedBits(10, 1)
                    .WithFlag(11, out interruptTransactionFinishedPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.m_done")
                    .WithFlag(12, out interruptTxOverrunPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.tx_ovr")
                    .WithTaggedFlag("INT_FL.tx_und", 13)
                    .WithFlag(14, out interruptRxOverrunPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.rx_ovr")
                    .WithFlag(15, out interruptRxUnderrunPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_EN.rx_und")
                    .WithReservedBits(16, 16)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out interruptTxLevelEnabled, name: "INT_EN.tx_level")
                    .WithFlag(1, out interruptTxEmptyEnabled, name: "INT_EN.tx_empty")
                    .WithFlag(2, out interruptRxLevelEnabled, name: "INT_EN.rx_level")
                    .WithFlag(3, out interruptRxFullEnabled, name: "INT_EN.rx_full")
                    .WithTaggedFlag("INT_EN.ssa", 4)
                    .WithTaggedFlag("INT_EN.ssd", 5)
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("INT_EN.fault", 8)
                    .WithTaggedFlag("INT_EN.abort", 9)
                    .WithReservedBits(10, 1)
                    .WithFlag(11, out interruptTransactionFinishedEnabled, name: "INT_EN.m_done")
                    .WithFlag(12, out interruptTxOverrunEnabled, name: "INT_EN.tx_ovr")
                    .WithTaggedFlag("INT_EN.tx_und", 13)
                    .WithFlag(14, out interruptRxOverrunEnabled, name: "INT_EN.rx_ovr")
                    .WithFlag(15, out interruptRxUnderrunEnabled, name: "INT_EN.rx_und")
                    .WithReservedBits(16, 16)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.ActiveStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("STAT.busy", 0)
                    .WithReservedBits(1, 31)
                }
            };

            {
                var constructedRegister = new DoubleWordRegister(this)
                    .WithValueField(0, 5, out txFIFOThreshold, name: "DMA.tx_fifo_level")
                    // NOTE: 5th bit covered in if statement
                    .WithFlag(6, out txFIFOEnabled, name: "DMA.tx_fifo_en")
                    .WithFlag(7, FieldMode.WriteOneToClear, name: "DMA.tx_fifo_clear",
                        writeCallback: (_, value) => { if(value) txQueue.Clear(); })
                    .WithValueField(8, 6, FieldMode.Read, name: "DMA.tx_fifo_cnt",
                        valueProviderCallback: _ => (uint)txQueue.Count)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("DMA.tx_dma_en", 15)
                    .WithValueField(16, 5, out rxFIFOThreshold, name: "DMA.rx_fifo_level")
                    .WithReservedBits(21, 1)
                    .WithFlag(22, out rxFIFOEnabled, name: "DMA.rx_fifo_en")
                    .WithFlag(23, FieldMode.WriteOneToClear, name: "DMA.rx_fifo_clear",
                        writeCallback: (_, value) => { if(value) rxQueue.Clear(); })
                    .WithValueField(24, 6, FieldMode.Read, name: "DMA.rx_fifo_cnt",
                        valueProviderCallback: _ => (uint)rxQueue.Count)
                    .WithReservedBits(30, 1)
                    .WithTag("DMA.rx_dma_en", 31, 1)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                ;
                // Depending on the peripheral constructor argument, treat writes to reserved field as error or don't.
                if(hushTxFifoLevelWarnings)
                {
                    constructedRegister.WithFlag(5, name: "RESERVED");
                }
                else
                {
                    constructedRegister.WithReservedBits(5, 1);
                }
                registerMap.Add((long)Registers.DMAControl, constructedRegister);
            }

            return registerMap;
        }

        private IEnumerable<Tuple<int, ISPIPeripheral>> ActivePeripherals
        {
            get
            {
                return Enumerable
                    .Range(0, NumberOfSlaves)
                    .Select(index =>
                    {
                        if(!BitHelper.IsBitSet(slaveSelect.Value, (byte)index))
                        {
                            return null;
                        }
                        if(!TryGetByAddress(index, out var peripheral))
                        {
                            return null;
                        }
                        return Tuple.Create(index, peripheral);
                    })
                    .Where(tuple => tuple != null);
            }
        }

        private bool[] shouldDeassert;
        private bool transactionInProgress;
        private bool hushTxFifoLevelWarnings;
        private uint charactersToTransmit;

        private IValueRegisterField slaveSelect;

        private IFlagRegisterField rxFIFOEnabled;
        private IFlagRegisterField txFIFOEnabled;

        private IValueRegisterField rxFIFOThreshold;
        private IValueRegisterField txFIFOThreshold;

        private IFlagRegisterField interruptTxLevelPending;
        private IFlagRegisterField interruptTxEmptyPending;
        private IFlagRegisterField interruptRxLevelPending;
        private IFlagRegisterField interruptRxFullPending;
        private IFlagRegisterField interruptTransactionFinishedPending;
        private IFlagRegisterField interruptTxOverrunPending;
        private IFlagRegisterField interruptRxOverrunPending;
        private IFlagRegisterField interruptRxUnderrunPending;

        private IFlagRegisterField interruptTxLevelEnabled;
        private IFlagRegisterField interruptTxEmptyEnabled;
        private IFlagRegisterField interruptRxLevelEnabled;
        private IFlagRegisterField interruptRxFullEnabled;
        private IFlagRegisterField interruptTransactionFinishedEnabled;
        private IFlagRegisterField interruptTxOverrunEnabled;
        private IFlagRegisterField interruptRxOverrunEnabled;
        private IFlagRegisterField interruptRxUnderrunEnabled;

        private const int FIFODataWidth = 0x04;
        private const int FIFOLength = 32;
        private const int MaximumNumberOfSlaves = 4;

        private readonly Queue<byte> rxQueue;
        private readonly Queue<byte> txQueue;
        private readonly DoubleWordRegisterCollection registers;

        private const byte DummyResponseByte = 0xFF;

        private enum Registers : long
        {
            FIFOData = 0x00,
            MasterSignalsControl = 0x04,
            TrasmitPacketSize = 0x08,
            StaticConfiguration = 0x0C,
            SlaveSelectTiming = 0x10,
            MasterClockConfiguration = 0x14,
            DMAControl = 0x1C,
            InterruptStatusFlags = 0x20,
            InterruptEnable = 0x24,
            WakeupStatusFlags = 0x28,
            WakeupEnable = 0x2C,
            ActiveStatus = 0x30,
        }
    }
}
