//
// Copyright (c) 2010-2021 Antmicro
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

namespace Antmicro.Renode.Peripherals.DMA
{
    public sealed class STM32F0_DMA: IDMA, IDoubleWordPeripheral, IKnownSize
    {
        public STM32F0_DMA(int numberOfChannels, Machine machine)
        {
            this.numberOfChannels = numberOfChannels;
            this.machine = machine;

            Irq1   = new GPIO();
            Irq2_3 = new GPIO();
            Irq4_7 = new GPIO();

            channels = new Channel[numberOfChannels];

            for(var i = 0; i < numberOfChannels; i++)
            {
                channels[i] = new Channel(this, i + 1);
            }

            registers = CreateRegisters();
            Reset();
        }

        public void Reset()
        {
            registers.Reset();
            foreach(var channel in channels)
            {
                channel.Reset();
            }
        }

        // Public method to be used in connected peripherals,
        // Channels here are not zero indexed!
        public void RequestTransfer(int channel)
        {
            if(channel < 1 || channel > numberOfChannels)
            {
                this.Log(LogLevel.Error, "This peripheral implements {0} channels with indexes starting at 1. Channel number '{1}' passed in `RequestTransfer` is invalid", numberOfChannels, channel);
                return;
            }

            channels[channel -1].Transfer();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public GPIO Irq1   { get; private set; }
        public GPIO Irq2_3 { get; private set; }
        public GPIO Irq4_7 { get; private set; }

        public int numberOfChannels { get; private set; }

        public long Size => 0x400;

        private void UpdateInterrupts()
        {
            foreach(var channel in channels)
            {
                bool irqState;
                if(!channel.globalInterruptFlag.Value)
                {
                    // No flag raised
                    irqState = false;
                }
                else
                {
                    var transferCompleteInterrupt = channel.transferCompleteFlag.Value && channel.transferCompleteInterruptEnable.Value;
                    var halfTransferInterrupt = channel.halfTransferFlag.Value && channel.halfTransferInterruptEnable.Value;
                    var transferErrorInterrupt = channel.transferErrorFlag.Value && channel.transferErrorFlag.Value;

                    irqState = transferCompleteInterrupt || halfTransferInterrupt || transferErrorInterrupt;
                }

                switch(channel.channelNumber)
                {
                    case 1:
                        Irq1.Set(irqState);
                        break;
                    case 2:
                    case 3:
                        Irq2_3.Set(irqState);
                        break;
                    case 4:
                    case 5:
                    case 6:
                    case 7:
                        Irq4_7.Set(irqState);
                        break;
                    default:
                        throw new Exception("This should never have happend!");
                }
            }
        }

        private DoubleWordRegisterCollection CreateRegisters()
        {
            int bitsUsed = numberOfChannels * 4;

            var interruptStatus = new DoubleWordRegister(this);
            var interruptFlagClear = new DoubleWordRegister(this);

            interruptStatus.Reserved(bitsUsed, 32 - bitsUsed);
            interruptFlagClear.Reserved(bitsUsed, 32 - bitsUsed);
            interruptFlagClear.DefineWriteCallback((_, __) => UpdateInterrupts());

            for(var i = 0; i < numberOfChannels; i++)
            {
                var j = i;
                // Interrupt flag clear
                interruptFlagClear.DefineFlagField(4 * j,       FieldMode.Write, writeCallback: (_, val) => { if(val) channels[j].globalInterruptFlag.Value = false; },  name: $"CGIF{j + 1}");
                interruptFlagClear.DefineFlagField(1 + (4 * j), FieldMode.Write, writeCallback: (_, val) => { if(val) channels[j].transferCompleteFlag.Value = false; }, name: $"CTCIF{j + 1}");
                interruptFlagClear.DefineFlagField(2 + (4 * j), FieldMode.Write, writeCallback: (_, val) => { if(val) channels[j].halfTransferFlag.Value = false; },     name: $"CHTIF{j + 1}");
                interruptFlagClear.DefineFlagField(3 + (4 * j), FieldMode.Write, writeCallback: (_, val) => { if(val) channels[j].transferErrorFlag.Value = false; },    name: $"CTEIF{j + 1}");
                // Interrupt status register
                channels[i].globalInterruptFlag  = interruptStatus.DefineFlagField((4 * j),     FieldMode.Read, name: $"GIF{j + 1}");
                channels[i].transferCompleteFlag = interruptStatus.DefineFlagField(1 + (4 * j), FieldMode.Read, name: $"TCIF{j + 1}");
                channels[i].halfTransferFlag     = interruptStatus.DefineFlagField(2 + (4 * j), FieldMode.Read, name: $"HTIF{j + 1}");
                channels[i].transferErrorFlag    = interruptStatus.DefineFlagField(3 + (4 * j), FieldMode.Read, name: $"TEIF{j + 1}");
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptStatus, interruptStatus},
                {(long)Registers.InterruptFlagClear, interruptFlagClear}
            };

            for(var i = 0; i < numberOfChannels; i++)
            {
                var j = i;
                registersMap.Add((long)(Registers.Channel1Configuration + (i * 20)), new DoubleWordRegister(this)
                    .WithFlag(0, out channels[j].enabled,
                        writeCallback: (_, val) =>
                            {
                                this.Log(LogLevel.Debug, "Channel {0}: Enabled -> {1}", j+1, val);
                                if(channels[j].memoryToMemory.Value && val)
                                {
                                    channels[j].Transfer();
                                }
                                channels[j].previousNumberOfData = channels[j].numberOfData.Value;
                                channels[j].currentMemoryAddress = channels[j].memoryAddress.Value;
                                channels[j].currentPeripheralAddress = channels[j].peripheralAddress.Value;
                            }, name: "EN")
                    .WithFlag(1, out channels[j].transferCompleteInterruptEnable, name: "TCIE")
                    .WithFlag(2, out channels[j].halfTransferInterruptEnable, name: "HTIE")
                    .WithFlag(3, out channels[j].transferErrorInterruptEnable, name: "TEIE")
                    .WithFlag(4, out channels[j].directionFromMemory, name: "DIR")
                    .WithFlag(5, out channels[j].circularMode, name: "CIRC")
                    .WithFlag(6, out channels[j].peripheralIncrement, name: "PINC")
                    .WithFlag(7, out channels[j].memoryIncrement, name: "MINC")
                    .WithValueField(8, 2, out channels[j].peripheralSize,
                        writeCallback: (_, val) =>
                            {
                                if(val == 0b11)
                                {
                                    this.Log(LogLevel.Warning, "Channel {0}: DMA_CCR 'Peripheral Size' field set to reserved value. Setting to default.", j);
                                    channels[j].peripheralSize.Value = 0b00;
                                }
                            },name:"PSIZE")
                    .WithValueField(10, 2, out channels[j].memorySize,
                        writeCallback: (_, val) =>
                            {
                                if(val == 0b11)
                                {
                                    this.Log(LogLevel.Warning, "Channel {0}: DMA_CCR 'Memory Size' field set to reserved value. Setting to default.", j);
                                    channels[j].memorySize.Value = 0b00;
                                }
                            },name:"MSIZE")
                    .WithTag("PL", 12, 2)
                    .WithFlag(14, out channels[j].memoryToMemory, name: "MEM2MEM")
                    .WithReservedBits(15, 17)
                    .WithWriteCallback((_,__) => {
                        UpdateInterrupts();
                        this.Log(LogLevel.Debug, "Channel {0}: Configured with: 'DIR': {1}, 'CIRC': {2}, 'PINC': {3}, 'MINC': {4}, 'PSIZE': {5}, 'MSIZE': {6}, 'MEM2MEM': {7}",
                            j+1, channels[j].directionFromMemory.Value, channels[j].circularMode.Value, channels[j].peripheralIncrement.Value, channels[j].memoryIncrement.Value,
                            channels[j].peripheralSize.Value, channels[j].memorySize.Value, channels[j].memoryToMemory.Value);
                    }));

                registersMap.Add((long)(Registers.Channel1NumberOfData + (i * 20)), new DoubleWordRegister(this)
                    .WithValueField(0, 16, out channels[j].numberOfData,
                        writeCallback: (_, val) => {
                            if(channels[j].enabled.Value)
                            {
                                this.Log(LogLevel.Warning, "Number of data (DMA_CNDTR{0}) register written when the channel is enabled. This is forbidden in manual.");
                            }
                            this.Log(LogLevel.Debug, "Channel {0}: Number of data set to {1}", j, val);
                        }, name: "NDT")
                    .WithReservedBits(16, 16));

                registersMap.Add((long)(Registers.Channel1PeripheralAddress + (i * 20)), new DoubleWordRegister(this)
                    .WithValueField(0, 32, out channels[j].peripheralAddress, writeCallback: (_, val) =>
                        {
                            this.Log(LogLevel.Debug, "Channel {0}: Peripheral address set to {1:X}", j+1, val);
                        }, name: "PA"));

                registersMap.Add((long)(Registers.Channel1MemoryAddress + (i * 20)), new DoubleWordRegister(this)
                    .WithValueField(0, 32, out channels[j].memoryAddress, writeCallback: (_, val) =>
                        {
                            this.Log(LogLevel.Debug, "Channel {0}: Memory address set to {1:X}", j+1, val);
                        }, name: "MA"));
            }
            return new DoubleWordRegisterCollection(this, registersMap);
        }

        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registers;
        private readonly Channel[] channels;

        private class Channel
        {
            public Channel(STM32F0_DMA parent, int number)
            {
                this.parent = parent;
                this.channelNumber = number;
                Reset();
            }

            public void Reset()
            {
                currentPeripheralAddress = 0;
                currentMemoryAddress = 0;
                previousNumberOfData = null;
            }

            public void Transfer()
            {
                parent.Log(LogLevel.Debug, "Channel {0}: Transfer stared.", channelNumber);
                // Setup numberOfData
                if(circularMode.Value && (numberOfData.Value == 0) && previousNumberOfData.HasValue)
                {
                    numberOfData.Value = previousNumberOfData ?? 0;
                    currentMemoryAddress = memoryAddress.Value;
                    currentPeripheralAddress = peripheralAddress.Value;
                }

                if((!enabled.Value) || (numberOfData.Value == 0))
                {
                    parent.Log(LogLevel.Warning, "Channel {0}: To perform transfer channel must be enabled and 'DMA_CNDTR{0}' register must be set to nonzero value. Transfer aborted." +
                               "Current Settings: DMA_CCR{0}.EN = {1}, DMA_CNDTR{0} = {2}", channelNumber, enabled.Value, numberOfData.Value);
                    return;
                }

                if(memoryToMemory.Value && circularMode.Value)
                {
                    parent.Log(LogLevel.Warning, "Channel {0}: Memory mode may not be used at the same time as Circular mode. Aborting transfer", channelNumber);
                    return;
                }

                var transferMemoryBits     = 8 << (int)memorySize.Value;
                var transferPeripheralBits = 8 << (int)peripheralSize.Value;

                // Transfer
                ulong sourceAddress = directionFromMemory.Value ? currentMemoryAddress : currentPeripheralAddress;
                ulong targetAddress = directionFromMemory.Value ? currentPeripheralAddress : currentMemoryAddress;
                var sourceBitsSize = directionFromMemory.Value ? transferMemoryBits : transferPeripheralBits;
                var targetBitsSize = directionFromMemory.Value ? transferPeripheralBits : transferMemoryBits;

                uint data = parent.machine.SystemBus.ReadDoubleWord(sourceAddress) >> (32-sourceBitsSize);
                SetFlag(ref halfTransferFlag);

                switch(targetBitsSize)
                {
                    case 8:
                        parent.machine.SystemBus.WriteByte(targetAddress, (byte)data);
                        break;
                    case 16:
                        parent.machine.SystemBus.WriteWord(targetAddress, (ushort)data);
                        break;
                    case 32:
                        parent.machine.SystemBus.WriteDoubleWord(targetAddress, data);
                        break;
                    default:
                        throw new Exception("This should never have happend!");
                }

                SetFlag(ref transferCompleteFlag);

                // Post transfer actions
                if(memoryIncrement.Value && (currentMemoryAddress > 0))
                {
                    currentMemoryAddress += (ulong)transferMemoryBits / 8;
                }
                if(peripheralIncrement.Value && (currentPeripheralAddress > 0))
                {
                    currentPeripheralAddress += (ulong)transferPeripheralBits / 8;
                }
                numberOfData.Value--;
                parent.Log(LogLevel.Debug, "Channel {0}: Finished transfer of {1} bits from addres 0x{2:X}, to {3} bits at adress 0x{4:X}.",
                           channelNumber, sourceBitsSize, sourceAddress, targetBitsSize, targetAddress);
            }

            public readonly int channelNumber;
            public uint? previousNumberOfData;
            public ulong currentPeripheralAddress;
            public ulong currentMemoryAddress;

            public IFlagRegisterField enabled;
            public IFlagRegisterField globalInterruptFlag;
            public IFlagRegisterField halfTransferFlag;
            public IFlagRegisterField transferCompleteFlag;
            public IFlagRegisterField transferErrorFlag;

            public IFlagRegisterField halfTransferInterruptEnable;
            public IFlagRegisterField transferCompleteInterruptEnable;
            public IFlagRegisterField transferErrorInterruptEnable;

            public IFlagRegisterField memoryToMemory;
            public IFlagRegisterField memoryIncrement;
            public IFlagRegisterField peripheralIncrement;
            public IFlagRegisterField circularMode;
            public IFlagRegisterField directionFromMemory;  // 0 - read from peripheral, write to memory;  1 - read from memory, write to peripheral
            public IValueRegisterField memorySize;
            public IValueRegisterField peripheralSize;

            public IValueRegisterField peripheralAddress;
            public IValueRegisterField memoryAddress;
            public IValueRegisterField numberOfData;

            private void SetFlag(ref IFlagRegisterField flag)
            {
                flag.Value = true;
                globalInterruptFlag.Value = true;
                parent.UpdateInterrupts();
            }

            private STM32F0_DMA parent;
        }

        private enum Registers: long
        {
            InterruptStatus           = 0x0,  // DMA_ISR
            InterruptFlagClear        = 0x4,  // DMA_IFCR
            Channel1Configuration     = 0x8,  // DMA_CCR1
            Channel1NumberOfData      = 0xC,  // DMA_CNDTR1
            Channel1PeripheralAddress = 0x10, // DMA_CPAR1
            Channel1MemoryAddress     = 0x14, // DMA_CMAR1
            // Below registers names are used only in logs
            Channel2Configuration     = 0x1C, // DMA_CCR2
            Channel2NumberOfData      = 0x20, // DMA_CNDTR2
            Channel2PeripheralAddress = 0x24, // DMA_CPAR2
            Channel2MemoryAddress     = 0x28, // DMA_CMAR2
            Channel3Configuration     = 0x30, // DMA_CCR3
            Channel3NumberOfData      = 0x34, // DMA_CNDTR3
            Channel3PeripheralAddress = 0x38, // DMA_CPAR3
            Channel3MemoryAddress     = 0x3C, // DMA_CMAR3
            Channel4Configuration     = 0x44, // DMA_CCR4
            Channel4NumberOfData      = 0x48, // DMA_CNDTR4
            Channel4PeripheralAddress = 0x4C, // DMA_CPAR4
            Channel4MemoryAddress     = 0x50, // DMA_CMAR4
            Channel5Configuration     = 0x58, // DMA_CCR5
            Channel5NumberOfData      = 0x5C, // DMA_CNDTR5
            Channel5PeripheralAddress = 0x60, // DMA_CPAR5
            Channel5MemoryAddress     = 0x64, // DMA_CMAR5
            Channel6Configuration     = 0x6C, // DMA_CCR6
            Channel6NumberOfData      = 0x70, // DMA_CNDTR6
            Channel6PeripheralAddress = 0x74, // DMA_CPAR6
            Channel6MemoryAddress     = 0x78, // DMA_CMAR6
            Channel7Configuration     = 0x80, // DMA_CCR7
            Channel7NumberOfData      = 0x84, // DMA_CNDTR7
            Channel7PeripheralAddress = 0x88, // DMA_CPAR7
            Channel7MemoryAddress     = 0x8C, // DMA_CMAR7
        }
    }
}
