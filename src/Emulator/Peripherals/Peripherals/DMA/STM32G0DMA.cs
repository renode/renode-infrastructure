//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class STM32G0DMA : IDoubleWordPeripheral, IKnownSize, IGPIOReceiver, INumberedGPIOOutput
    {
        public STM32G0DMA(IMachine machine, int numberOfChannels)
        {
            this.machine = machine;
            engine = new DmaEngine(machine.GetSystemBus(this));
            this.numberOfChannels = numberOfChannels;
            channels = new Channel[numberOfChannels];
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfChannels; ++i)
            {
                channels[i] = new Channel(this, i);
                innerConnections[i] = new GPIO();
            }

            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            var interruptStatus = new DoubleWordRegister(this);
            var interruptFlagClear = new DoubleWordRegister(this)
                    .WithWriteCallback((_, __) => Update());

            for(var i = 0; i < numberOfChannels; ++i)
            {
                var j = i;
                interruptStatus.DefineFlagField(j * 4 + 0, FieldMode.Read,
                    valueProviderCallback: _ => channels[j].GlobalInterrupt,
                    name: $"Global interrupt flag for channel {j} (GIF{j})");
                interruptStatus.DefineFlagField(j * 4 + 1, FieldMode.Read,
                    valueProviderCallback: _ => channels[j].TransferComplete,
                    name: $"Transfer complete flag for channel {j} (TCIF{j})");
                interruptStatus.DefineFlagField(j * 4 + 2, FieldMode.Read,
                    valueProviderCallback: _ => channels[j].HalfTransfer,
                    name: $"Half transfer flag for channel {j} (HTIF{j})");
                interruptStatus.Tag($"Transfer error flag for channel {j} (TEIF{j})", j * 4 + 3, 1);

                interruptFlagClear.DefineFlagField(j * 4 + 0, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        channels[j].GlobalInterrupt = false;
                        channels[j].TransferComplete = false;
                        channels[j].HalfTransfer = false;
                    },
                    name: $"Global interrupt flag clear for channel {j} (CGIF{j})");
                interruptFlagClear.DefineFlagField(j * 4 + 1, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        channels[j].TransferComplete = false;
                        channels[j].GlobalInterrupt = false;
                    },
                    name: $"Transfer complete flag clear for channel {j} (CTEIF{j})");
                interruptFlagClear.DefineFlagField(j * 4 + 2, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        channels[j].HalfTransfer = false;
                        channels[j].GlobalInterrupt = false;
                    },
                    name: $"Half transfer flag clear for channel {j} (CHTIF{j})");
                interruptFlagClear.Tag($"Transfer error flag clear for channel {j} (CTEIF{j})", j * 4 + 3, 1);
            }

            var channelSelection = new DoubleWordRegister(this)
                .WithTag("DMA channel 1 selection (C1S)", 0, 4)
                .WithTag("DMA channel 2 selection (C2S)", 4, 4)
                .WithTag("DMA channel 3 selection (C3S)", 8, 4)
                .WithTag("DMA channel 4 selection (C4S)", 12, 4)
                .WithTag("DMA channel 5 selection (C5S)", 16, 4)
                .WithTag("DMA channel 6 selection (C6S)", 20, 4)
                .WithTag("DMA channel 7 selection (C7S)", 24, 4)
                .WithReservedBits(28, 4);

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptStatus, interruptStatus },
                {(long)Registers.InterruptFlagClear, interruptFlagClear },
                {(long)Registers.ChannelSelection, channelSelection },
            };

            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            if(registers.TryRead(offset, out var result))
            {
                return result;
            }
            if(TryGetChannelNumberBasedOnOffset(offset, out var channelNo))
            {
                return channels[channelNo].ReadDoubleWord(offset);
            }
            this.Log(LogLevel.Error, "Could not read from offset 0x{0:X} nor read from channel {1}, the channel has to be in range 0-{2}", offset, channelNo, numberOfChannels);
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(registers.TryWrite(offset, value))
            {
                return;
            }
            if(TryGetChannelNumberBasedOnOffset(offset, out var channelNo))
            {
                channels[channelNo].WriteDoubleWord(offset, value);
                return;
            }
            this.Log(LogLevel.Error, "Could not write to offset 0x{0:X} nor write to channel {1}, the channel has to be in range 0-{2}", offset, channelNo, numberOfChannels);
        }

        public void OnGPIO(int number, bool value)
        {
            if(number == 0 || number > channels.Length)
            {
                this.Log(LogLevel.Error, "Channel number {0} is out of range, must be in [1; {1}]", number, channels.Length);
                return;
            }

            if(!value)
            {
                return;
            }

            this.Log(LogLevel.Debug, "DMA peripheral request on channel {0}", number);
            if(!channels[number - 1].TryTriggerTransfer())
            {
                this.Log(LogLevel.Warning, "DMA peripheral request on channel {0} ignored - channel is disabled "
                    + "or has data count set to 0", number);
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x100;

        private bool TryGetChannelNumberBasedOnOffset(long offset, out int channel)
        {
            var shifted = offset - (long)Registers.Channel1Configuration;
            channel = (int)(shifted / ShiftBetweenChannels);
            if(channel < 0 || channel > numberOfChannels)
            {
                return false;
            }
            return true;
        }

        private void Update()
        {
            for(var i = 0; i < numberOfChannels; ++i)
            {
                Connections[i].Set((channels[i].TransferComplete && channels[i].TransferCompleteInterruptEnable)
                        || (channels[i].HalfTransfer && channels[i].HalfTransferInterruptEnable));
            }
        }

        private readonly IMachine machine;
        private readonly DmaEngine engine;
        private readonly DoubleWordRegisterCollection registers;
        private readonly Channel[] channels;
        private readonly int numberOfChannels;

        private const int ShiftBetweenChannels = (int)((long)Registers.Channel2Configuration - (long)Registers.Channel1Configuration);

        private class Channel
        {
            public Channel(STM32G0DMA parent, int number)
            {
                this.parent = parent;
                channelNumber = number;

                var registersMap = new Dictionary<long, DoubleWordRegister>();
                registersMap.Add((long)ChannelRegisters.ChannelConfiguration + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithFlag(0, out channelEnable,
                        writeCallback: (_, val) =>
                        {
                            if(!val)
                            {
                                return;
                            }
                            if(memoryToMemory.Value || transferDirection.Value == TransferDirection.MemoryToPeripheral)
                            {
                                DoTransfer();
                            }
                        },
                        valueProviderCallback: _ => false, name: "Channel enable (EN)")
                    .WithFlag(1, out transferCompleteInterruptEnable, name: "Transfer complete interrupt enable (TCIE)")
                    .WithFlag(2, out halfTransferInterruptEnable, name: "Half transfer interrupt enable (HTIE)")
                    .WithTag("Transfer error interrupt enable (TEIE)", 3, 1)
                    .WithEnumField(4, 1, out transferDirection, name: "Data transfer direction (DIR)")
                    .WithFlag(5, out circularMode, name: "Circular mode (CIRC)")
                    .WithFlag(6, out peripheralIncrementMode, name: "Peripheral increment mode (PINC)")
                    .WithFlag(7, out memoryIncrementMode, name: "Memory increment mode (MINC)")
                    .WithEnumField(8, 2, out peripheralTransferType, name: "Peripheral size (PSIZE)")
                    .WithEnumField(10, 2, out memoryTransferType, name: "Memory size (MSIZE)")
                    .WithTag("Priority level (PL)", 12, 2)
                    .WithFlag(14, out memoryToMemory, name: "Memory-to-memory mode (MEM2MEM)")
                    .WithReservedBits(15, 17)
                    .WithWriteCallback(
                            (_, __) => parent.Update()));

                // The ChannelDataCount register stores the currently remaining number of data units to copy.
                // It is decremented on each transfer. The originalDataCount field stores the original value
                // written by software in order to implement the half transfer interrupt.
                registersMap.Add((long)ChannelRegisters.ChannelDataCount + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithValueField(0, 16, out dataCount, name: "Number of data to transfer (NDT)",
                        writeCallback: (_, val) => originalDataCount = val)
                    .WithReservedBits(16, 16));

                // The ChannelPeripheralAddress and ChannelMemoryAddress registers retain the value written
                // by software. This value is read by the model during circular mode wraparound.
                // The incremented address is stored in the currentAddress fields.
                registersMap.Add((long)ChannelRegisters.ChannelPeripheralAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out peripheralAddress,
                        writeCallback: (_, val) => currentPeripheralAddress = val, name: "Peripheral address (PA)"));

                registersMap.Add((long)ChannelRegisters.ChannelMemoryAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out memoryAddress,
                        writeCallback: (_, val) => currentMemoryAddress = val, name: "Memory address (MA)"));

                registers = new DoubleWordRegisterCollection(parent, registersMap);
            }

            public uint ReadDoubleWord(long offset)
            {
                return registers.Read(offset);
            }

            public void WriteDoubleWord(long offset, uint value)
            {
                registers.Write(offset, value);
            }

            public void Reset()
            {
                registers.Reset();
                TransferComplete = false;
                HalfTransfer = false;
            }

            public bool TryTriggerTransfer()
            {
                if(!Enabled || dataCount.Value == 0)
                {
                    return false;
                }

                DoTransfer();
                parent.Update();
                return true;
            }

            public bool GlobalInterrupt
            {
                get
                {
                    return HalfTransfer || TransferComplete;
                }
                set
                {
                    if(value)
                    {
                        return;
                    }
                    HalfTransfer = false;
                    TransferComplete = false;
                }
            }

            public bool Enabled => channelEnable.Value;

            public bool HalfTransfer { get; set; }

            public bool TransferComplete { get; set; }

            public bool HalfTransferInterruptEnable => halfTransferInterruptEnable.Value;

            public bool TransferCompleteInterruptEnable => transferCompleteInterruptEnable.Value;

            private void DoTransfer()
            {
                // This value is still valid in memory-to-memory mode, "peripheral" means
                // "the address specified by the peripheralAddress field" and not necessarily
                // a peripheral.
                if(transferDirection.Value == TransferDirection.PeripheralToMemory)
                {
                    var toCopy = (uint)dataCount.Value;
                    // In peripheral-to-memory mode, only copy one data unit. Otherwise, do the whole block.
                    if(!memoryToMemory.Value)
                    {
                        toCopy = Math.Max((uint)SizeToType(memoryTransferType.Value),
                            (uint)SizeToType(peripheralTransferType.Value));
                        dataCount.Value -= 1;
                    }
                    else
                    {
                        dataCount.Value = 0;
                    }
                    var response = IssueCopy(currentPeripheralAddress, currentMemoryAddress, toCopy,
                        peripheralIncrementMode.Value, memoryIncrementMode.Value, peripheralTransferType.Value,
                        memoryTransferType.Value);
                    currentPeripheralAddress = response.ReadAddress.Value;
                    currentMemoryAddress = response.WriteAddress.Value;
                    HalfTransfer = dataCount.Value <= originalDataCount / 2;
                    TransferComplete = dataCount.Value == 0;
                }
                else // 1-bit field, so we handle both possible values
                {
                    IssueCopy(memoryAddress.Value, peripheralAddress.Value, (uint)dataCount.Value,
                        memoryIncrementMode.Value, peripheralIncrementMode.Value, memoryTransferType.Value,
                        peripheralTransferType.Value);
                    dataCount.Value = 0;
                    HalfTransfer = true;
                    TransferComplete = true;
                }

                // Loop around if circular mode is enabled
                if(circularMode.Value && !memoryToMemory.Value && dataCount.Value == 0)
                {
                    dataCount.Value = originalDataCount;
                    currentPeripheralAddress = peripheralAddress.Value;
                    currentMemoryAddress = memoryAddress.Value;
                }
                // No parent.Update - this is called by the register write and TryTriggerTransfer
                // to avoid calling it twice in the former case
            }

            private Response IssueCopy(ulong sourceAddress, ulong destinationAddress, uint size,
                bool incrementReadAddress, bool incrementWriteAddress, TransferSize sourceTransferType,
                TransferSize destinationTransferType)
            {
                var request = new Request(
                    sourceAddress,
                    destinationAddress,
                    (int)size,
                    SizeToType(sourceTransferType),
                    SizeToType(destinationTransferType),
                    incrementReadAddress,
                    incrementWriteAddress
                );
                return parent.engine.IssueCopy(request);
            }

            private TransferType SizeToType(TransferSize size)
            {
                switch(size)
                {
                    case TransferSize.Bits32:
                        return TransferType.DoubleWord;
                    case TransferSize.Bits16:
                        return TransferType.Word;
                    case TransferSize.Bits8:
                    default:
                        return TransferType.Byte;
                }
            }


            private IEnumRegisterField<TransferDirection> transferDirection;
            private IFlagRegisterField circularMode;
            private IFlagRegisterField peripheralIncrementMode;
            private IFlagRegisterField memoryIncrementMode;
            private IFlagRegisterField memoryToMemory;
            private IValueRegisterField dataCount;
            private IValueRegisterField memoryAddress;
            private IValueRegisterField peripheralAddress;
            private IFlagRegisterField channelEnable;
            private IFlagRegisterField transferCompleteInterruptEnable;
            private IFlagRegisterField halfTransferInterruptEnable;
            private IEnumRegisterField<TransferSize> memoryTransferType;
            private IEnumRegisterField<TransferSize> peripheralTransferType;
            private ulong currentPeripheralAddress;
            private ulong currentMemoryAddress;
            private ulong originalDataCount;

            private readonly DoubleWordRegisterCollection registers;
            private readonly STM32G0DMA parent;
            private readonly int channelNumber;

            private enum TransferSize
            {
                Bits8 = 0,
                Bits16 = 1,
                Bits32 = 2,
                Reserved = 3
            }

            private enum TransferDirection
            {
                PeripheralToMemory = 0,
                MemoryToPeripheral = 1,
            }

            private enum ChannelRegisters
            {
                ChannelConfiguration = 0x8,
                ChannelDataCount = 0xC,
                ChannelPeripheralAddress = 0x10,
                ChannelMemoryAddress = 0x14,
            }
        }

        private enum Registers : long
        {
            InterruptStatus = 0x0,
            InterruptFlagClear = 0x4,

            Channel1Configuration = 0x8,
            Channel1DataCount = 0xC,
            Channel1PeripheralAddress = 0x10,
            Channel1MemoryAddress = 0x14,

            Channel2Configuration = 0x1C,
            Channel2DataCount = 0x20,
            Channel2PeripheralAddress = 0x24,
            Channel2MemoryAddress = 0x28,

            Channel3Configuration = 0x30,
            Channel3DataCount = 0x34,
            Channel3PeripheralAddress = 0x38,
            Channel3MemoryAddress = 0x3c,

            Channel4Configuration = 0x44,
            Channel4DataCount = 0x48,
            Channel4PeripheralAddress = 0x4c,
            Channel4MemoryAddress = 0x50,

            Channel5Configuration = 0x58,
            Channel5DataCount = 0x5c,
            Channel5PeripheralAddress = 0x60,
            Channel5MemoryAddress = 0x64,

            Channel6Configuration = 0x6C,
            Channel6DataCount = 0x70,
            Channel6PeripheralAddress = 0x74,
            Channel6MemoryAddress = 0x78,

            Channel7Configuration = 0x80,
            Channel7DataCount = 0x84,
            Channel7PeripheralAddress = 0x88,
            Channel7MemoryAddress = 0x8c,
            // 0x90 to 0xA4 - Reserved
            ChannelSelection = 0xA8
        }
    }
}
