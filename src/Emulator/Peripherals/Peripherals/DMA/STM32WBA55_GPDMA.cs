using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class STM32WBA55_GPDMA : IDoubleWordPeripheral, IKnownSize, IGPIOReceiver, INumberedGPIOOutput
    {
        public STM32WBA55_GPDMA(IMachine machine, int numberOfChannels)
        {
            this.machine = machine;
            engine = new DmaEngine(machine.GetSystemBus(this));
            this.numberOfChannels = numberOfChannels;
            channels = new Channel[numberOfChannels];
            var innerConnections = new Dictionary<int, IGPIO>();
            for (var i = 0; i < numberOfChannels; ++i)
            {
                channels[i] = new Channel(this, i);
                innerConnections[i] = new GPIO();
            }

            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            var secureConfiguration = new DoubleWordRegister(this);
            var privilegedConfiguration = new DoubleWordRegister(this);
            var configurationLock = new DoubleWordRegister(this);
            var nonsecureMaskedInteruptStatus = new DoubleWordRegister(this);
            var secureMaskedInteruptStatus = new DoubleWordRegister(this);


            //TODO: rework/adapt to WBA55
            var interruptStatus = new DoubleWordRegister(this);
            var interruptFlagClear = new DoubleWordRegister(this)
                    .WithWriteCallback((_, __) => Update());

            for (var i = 0; i < numberOfChannels; ++i)
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
                        if (!val)
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
                        if (!val)
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
                        if (!val)
                        {
                            return;
                        }
                        channels[j].HalfTransfer = false;
                        channels[j].GlobalInterrupt = false;
                    },
                    name: $"Half transfer flag clear for channel {j} (CHTIF{j})");
                interruptFlagClear.Tag($"Transfer error flag clear for channel {j} (CTEIF{j})", j * 4 + 3, 1);
            }

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.SecureConfiguration, secureConfiguration },
                {(long)Registers.PrivilegedConfiguration, privilegedConfiguration },
                {(long)Registers.ConfigurationLock, configurationLock },
                {(long)Registers.NonsecureMaskedInteruptStatus, nonsecureMaskedInteruptStatus },
                {(long)Registers.SecureMaskedInteruptStatus, secureMaskedInteruptStatus },
            };

            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            if (registers.TryRead(offset, out var result))
            {
                return result;
            }
            if (TryGetChannelNumberBasedOnOffset(offset, out var channelNo))
            {
                return channels[channelNo].ReadDoubleWord(offset);
            }
            this.Log(LogLevel.Error, "Could not read from offset 0x{0:X} nor read from channel {1}, the channel has to be in range 0-{2}", offset, channelNo, numberOfChannels);
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if (registers.TryWrite(offset, value))
            {
                return;
            }
            if (TryGetChannelNumberBasedOnOffset(offset, out var channelNo))
            {
                channels[channelNo].WriteDoubleWord(offset, value);
                return;
            }
            this.Log(LogLevel.Error, "Could not write to offset 0x{0:X} nor write to channel {1}, the channel has to be in range 0-{2}", offset, channelNo, numberOfChannels);
        }

        public void OnGPIO(int number, bool value)
        {
            if (number == 0 || number > channels.Length)
            {
                this.Log(LogLevel.Error, "Channel number {0} is out of range, must be in [1; {1}]", number, channels.Length);
                return;
            }

            if (!value)
            {
                return;
            }

            this.Log(LogLevel.Debug, "DMA peripheral request on channel {0}", number);
            if (!channels[number - 1].TryTriggerTransfer())
            {
                this.Log(LogLevel.Warning, "DMA peripheral request on channel {0} ignored - channel is disabled "
                    + "or has data count set to 0", number);
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x1000;

        private bool TryGetChannelNumberBasedOnOffset(long offset, out int channel)
        {
            var shifted = offset - (long)Registers.Channel0LinkedListBaseAddress;
            channel = (int)(shifted / ShiftBetweenChannels);
            if (channel < 0 || channel > numberOfChannels)
            {
                return false;
            }
            return true;
        }

        private void Update()
        {
            for (var i = 0; i < numberOfChannels; ++i)
            {
                Connections[i].Set((channels[i].TransferComplete && channels[i].TransferCompleteInterruptEnable)
                        || (channels[i].HalfTransfer && channels[i].HalfTransferInterruptEnable)
                        || (channels[i].DataTransferError && channels[i].DataTransferErrorInterruptEnable)
                        || (channels[i].UpdateLinkTransferError && channels[i].UpdateLinkTransferErrorInterruptEnable)
                        || (channels[i].UserSettingError && channels[i].UserSettingErrorInterruptEnable)
                        || (channels[i].CompletedSuspension && channels[i].CompletedSuspensionInterruptEnable)
                        || (channels[i].TriggerOverrun && channels[i].TriggerOverrunInterruptEnable));
            }
        }

        private readonly IMachine machine;
        private readonly DmaEngine engine;
        private readonly DoubleWordRegisterCollection registers;
        private readonly Channel[] channels;
        private readonly int numberOfChannels;

        private const int ShiftBetweenChannels = 0x80;

        private class Channel
        {
            public Channel(STM32WBA55_GPDMA parent, int number)
            {
                this.parent = parent;
                channelNumber = number;

                var registersMap = new Dictionary<long, DoubleWordRegister>();
                registersMap.Add((long)ChannelRegisters.ChannelLinkedListBaseAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithReservedBits(0,16)
                    .WithValueField(16, 16, out linkedListBaseAddress, name: "Linked list base address (LBA)"));
                registersMap.Add((long)ChannelRegisters.ChannelFlagClear + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithReservedBits(0, 8)
                    .WithFlag(8, FieldMode.Write, name: "Transfer complete flag clear (TCF)",
                            writeCallback: (_, val) =>
                            {
                                if (val)
                                {
                                    TransferComplete = false;
                                }
                            })
                    .WithFlag(9, FieldMode.Write, name: "Half transfer flag clear (TCF)",
                            writeCallback: (_, val) =>
                            {
                                if (val)
                                {
                                    HalfTransfer = false;
                                }
                            })
                    .WithFlag(10, FieldMode.Write, name: "Data transfer error flag clear (DTEF)",
                            writeCallback: (_, val) =>
                            {
                                if (val) { DataTransferError = false; }
                            })
                    .WithFlag(11, FieldMode.Write, name: "Update link transfer error flag clear (ULEF)",
                            writeCallback: (_, val) =>
                            {
                                if (val)
                                {
                                    UpdateLinkTransferError = false;
                                }
                            })
                    .WithFlag(12, FieldMode.Write, name: "User setting error flag clear (USEF)",
                            writeCallback: (_, val) =>
                            {
                                if (val)
                                {
                                    UserSettingError = false;
                                }
                            })
                    .WithFlag(13, FieldMode.Write, name: "Completed suspension flag clear (SUSPF)",
                            writeCallback: (_, val) =>
                            {
                                if (val)
                                {
                                    CompletedSuspension = false;
                                }
                            })
                    .WithFlag(14, FieldMode.Write, name: "Trigger overrun flag clear (TOF)",
                            writeCallback: (_, val) =>
                            {
                                if (val)
                                {
                                    TriggerOverrun = false;
                                }
                            })
                    .WithReservedBits(15, 17)
                );
                registersMap.Add((long)ChannelRegisters.ChannelStatus + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithTag("Idle (IDLEF)", 0, 1)
                    .WithReservedBits(1, 7)
                    .WithFlag(8, FieldMode.Read, name: "Transfer complete flag (TCF)",
                        valueProviderCallback: _ => TransferComplete)
                    .WithFlag(9, FieldMode.Read, name: "Half transfer flag (HTF)",
                        valueProviderCallback: _ => HalfTransfer)
                    .WithFlag(10, FieldMode.Read, name: "Data transfer error flag (DTEF)",
                        valueProviderCallback: _ => DataTransferError)
                    .WithFlag(11, FieldMode.Read, name: "Update link transfer error flag (ULEF)",
                        valueProviderCallback: _ => UpdateLinkTransferError)
                    .WithFlag(12, FieldMode.Read, name: "User setting error flag (USEF)",
                        valueProviderCallback: _ => UserSettingError)
                    .WithFlag(13, FieldMode.Read, name: "Completed suspension flag (SUSPF)",
                        valueProviderCallback: _ => CompletedSuspension)
                    .WithFlag(14, FieldMode.Read, name: "Trigger overrun flag (TOF)",
                        valueProviderCallback: _ => TriggerOverrun)
                    .WithReservedBits(15, 1)
                    .WithTag("Monitored FIFO level (FIFOL)", 16, 8)
                    .WithReservedBits(24, 8));
                registersMap.Add((long)ChannelRegisters.ChannelControl + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithFlag(0, out channelEnable,
                        writeCallback: (_, val) =>
                        {
                            if (!val)
                            {
                                return;
                            }
                            if (memoryToMemory.Value || transferDirection.Value == TransferDirection.MemoryToPeripheral)
                            {
                                DoTransfer();
                            }
                        })
                      .WithFlag(1, out channelReset, FieldMode.Write,
                        writeCallback: (_, val) =>
                        {
                            if (val)
                            {
                                //TODO: check GPDMA_CxBR1,GPDMA_CxSAR, and GPDMA_CxDAR
                                //registers; propably need to be updated 
                                channelEnable.Value = false;
                                channelSuspend.Value = false;
                            }
                        },
                        valueProviderCallback: _ => false, name: "RESET (RESET)")
                      .WithFlag(2, out channelSuspend, FieldMode.Write,
                        writeCallback: (_, val) =>
                        {
                            if (!val)
                            {
                                return;
                            }
                        }, name: "SUSPEND (SUSP)")
                    .WithReservedBits(3, 5)
                    .WithFlag(8, out transferCompleteInterruptEnable, name: "Transfer complete interrupt enable (TCIE)")
                    .WithFlag(9, out halfTransferInterruptEnable, name: "Half transfer interrupt enable (HTIE)")
                    .WithFlag(10, out dataTransferErrorInterruptEnable, name: "Data transfer error interrupt enable (DTEIE)")
                    .WithFlag(11, out updateLinkTransferErrorInterruptEnable, name: "Update link transfer error interrupt enable (ULEIE)")
                    .WithFlag(12, out userSettingErrorInterruptEnable, name: "User setting error interrupt enable (USEIE)")
                    .WithFlag(13, out completedSuspensionInterruptEnable, name: "Completed suspension interrupt enable (SUSPIE)")
                    .WithFlag(14, out dataTransferErrorInterruptEnable, name: "Trigger overrun interrupt enable (TOIE)")
                    .WithReservedBits(15, 1)
                    .WithTag("Link step mode (LSM)", 16, 1)
                    .WithTag("Linked list allocated port (LAP)", 17, 1)
                    .WithReservedBits(18, 4)
                    .WithEnumField(22, 2, out priorityLevel, name: "Priority level (PRIO)")
                    .WithReservedBits(24, 8)
                    .WithWriteCallback(
                            (_, __) => parent.Update()));
                registersMap.Add((long)ChannelRegisters.ChannelTransfer1 + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithEnumField(0, 2, out sourceDataWith, name: "Binary logarithm source data with (SDW_LOG2)")
                    .WithReservedBits(2, 1)
                    .WithFlag(3, out sourceIncrementingBurst, name: "Source incrementing burst (SINC)")
                    .WithValueField(4, 5, out sourceBurstLength, name: "Source burst length (SBL_1)")                
                    .WithReservedBits(10, 1)
                    .WithTag("Padding alignment mode (PAM)", 11, 2)
                    .WithTag("Source byte exchange (SBX)", 13, 1)
                    .WithTag("Source allocated port (SAP)", 14, 1)
                    .WithTag("Security attribute source (SSEC)", 15, 1)
                    .WithEnumField(16, 2, out destinationDataWith, name: "Binary logarithm destination data with (DDW_LOG2)")              
                    .WithReservedBits(18, 1)
                    .WithFlag(19, out destinationIncrementingBurst, name: "Destination incrementing burst (DINC)")
                    .WithValueField(20, 6, out destinationBurstLength, name: "Destination burst length (DBL_1)")  
                    .WithTag("Destination byte exchange (DBX)", 26, 1)  
                    .WithTag("Destination half-word exchange (DHX)", 27, 1)  
                    .WithReservedBits(28, 2)
                    .WithTag("Destination allocated port (DAP)", 30, 1) 
                    .WithTag("Security attribute destination (DSEC)", 31, 1) );
                //TODO: implement registers & callbacks
                registersMap.Add((long)ChannelRegisters.ChannelTransfer2 + (number * ShiftBetweenChannels), new DoubleWordRegister(parent));
                registersMap.Add((long)ChannelRegisters.ChannelBlock1 + (number * ShiftBetweenChannels), new DoubleWordRegister(parent));
                registersMap.Add((long)ChannelRegisters.ChannelSourceAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent));
                registersMap.Add((long)ChannelRegisters.ChannelDestinationAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent));
                registersMap.Add((long)ChannelRegisters.ChannelLinkedListAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent));
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
                if (!Enabled || dataCount.Value == 0)
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
                    if (value)
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

            public bool DataTransferError { get; set; }

            public bool UpdateLinkTransferError { get; set; }

            public bool UserSettingError { get; set; }

            public bool CompletedSuspension { get; set; }

            public bool TriggerOverrun { get; set; }

            public bool HalfTransferInterruptEnable => halfTransferInterruptEnable.Value;

            public bool TransferCompleteInterruptEnable => transferCompleteInterruptEnable.Value;

            public bool DataTransferErrorInterruptEnable => dataTransferErrorInterruptEnable.Value;

            public bool UpdateLinkTransferErrorInterruptEnable => updateLinkTransferErrorInterruptEnable.Value;

            public bool UserSettingErrorInterruptEnable => userSettingErrorInterruptEnable.Value;

            public bool CompletedSuspensionInterruptEnable => completedSuspensionInterruptEnable.Value;

            public bool TriggerOverrunInterruptEnable => triggerOverrunInterruptEnable.Value;

            private void DoTransfer()
            {
                // This value is still valid in memory-to-memory mode, "peripheral" means
                // "the address specified by the peripheralAddress field" and not necessarily
                // a peripheral.
                if (transferDirection.Value == TransferDirection.PeripheralToMemory)
                {
                    var toCopy = (uint)dataCount.Value;
                    // In peripheral-to-memory mode, only copy one data unit. Otherwise, do the whole block.
                    if (!memoryToMemory.Value)
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
                if (circularMode.Value && !memoryToMemory.Value && dataCount.Value == 0)
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
                switch (size)
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

            //Channel Control CxCR
            private IFlagRegisterField channelEnable;
            private IFlagRegisterField channelReset;
            private IFlagRegisterField channelSuspend;
            private IFlagRegisterField transferCompleteInterruptEnable;
            private IFlagRegisterField halfTransferInterruptEnable;
            private IFlagRegisterField dataTransferErrorInterruptEnable;
            private IFlagRegisterField updateLinkTransferErrorInterruptEnable;
            private IFlagRegisterField userSettingErrorInterruptEnable;
            private IFlagRegisterField completedSuspensionInterruptEnable;
            private IFlagRegisterField triggerOverrunInterruptEnable;
            private IEnumRegisterField<Priotity> priorityLevel;

            //Channel Linked List Base Address CxLBAR
            private IValueRegisterField linkedListBaseAddress;

            //Transfer Register CxTR 1&2   
            private IFlagRegisterField sourceIncrementingBurst;
            private IFlagRegisterField destinationIncrementingBurst;
            private IValueRegisterField sourceBurstLength;
            private IValueRegisterField destinationBurstLength;
            private IEnumRegisterField<TransferSize> sourceDataWith;
            private IEnumRegisterField<TransferSize> destinationDataWith;

            private IEnumRegisterField<TransferSize> memoryTransferType;
            private IEnumRegisterField<TransferSize> peripheralTransferType;
            private ulong currentPeripheralAddress;
            private ulong currentMemoryAddress;
            private ulong originalDataCount;

            private readonly DoubleWordRegisterCollection registers;
            private readonly STM32WBA55_GPDMA parent;
            private readonly int channelNumber;

            private enum Priotity
            {
                LowPrioLowWeight = 0,
                LowPrioMidWeight = 1,
                LowPrioHighWeigt = 2,
                HighPrio = 3
            }

            private enum TransferSize
            {
                Bits8 = 0, //byte
                Bits16 = 1, // half-word (2bytes)
                Bits32 = 2, // word (4bytes)
                UserSettingError = 3
            }

            private enum TransferDirection
            {
                PeripheralToMemory = 0,
                MemoryToPeripheral = 1,
            }

            private enum ChannelRegisters
            {
                ChannelLinkedListBaseAddress = 0x50,
                ChannelFlagClear = 0x5C,
                ChannelStatus = 0x60,
                ChannelControl = 0x64,
                ChannelTransfer1 = 0x90,
                ChannelTransfer2 = 0x94,
                ChannelBlock1 = 0x98,
                ChannelSourceAddress = 0x9C,
                ChannelDestinationAddress = 0xA0,
                ChannelLinkedListAddress = 0xCC,
            }
        }

        private enum Registers : long
        {

            SecureConfiguration = 0x00, //SECCFGR
            PrivilegedConfiguration = 0x04, //PRIVCFGR
            ConfigurationLock = 0x08, //RCFGLOCKR
            NonsecureMaskedInteruptStatus = 0x0C, //MISR
            SecureMaskedInteruptStatus = 0x10, //SMISR

            Channel0LinkedListBaseAddress = 0x50, //CxLBAR
            Channel0FlagClear = 0x5C, //CxFCR
            Channel0Status = 0x60, //CxSR
            Channel0Control = 0x64, //CxCR
            Channel0Transfer1 = 0x90, //CxTR1
            Channel0Transfer2 = 0x94, //CxTR2
            Channel0Block1 = 0x98, //CxBR1
            Channel0SourceAddress = 0x9C, //CxSAR
            Channel0DestinationAddress = 0xA0, //CxDAR
            Channel0LinkedListAddress = 0xCC, //CxLLR

            //probably not needed
            /*Channel1LinkedListBaseAddress = 0xD0,
            Channel1FlagClear = 0x5C,
            Channel1Status = 0x60,
            Channel1Control = 0x64,
            Channel1Transfer1 = 0x90,
            Channel1Transfer2 = 0x94,
            Channel1Block1 = 0x98,
            Channel1SourceAddress = 0x9C,
            Channel1DestinationAddress = 0xA0,
            Channel1LinkedListAddress = 0xCC,

            Channel2LinkedListBaseAddress = 0x50,
            Channel2FlagClear = 0x5C,
            Channel2Status = 0x60,
            Channel2Control = 0x64,
            Channel2Transfer1 = 0x90,
            Channel2Transfer2 = 0x94,
            Channel2Block1 = 0x98,
            Channel2SourceAddress = 0x9C,
            Channel2DestinationAddress = 0xA0,
            Channel2LinkedListAddress = 0xCC,

            Channel3LinkedListBaseAddress = 0x50,
            Channel3FlagClear = 0x5C,
            Channel3Status = 0x60,
            Channel3Control = 0x64,
            Channel3Transfer1 = 0x90,
            Channel3Transfer2 = 0x94,
            Channel3Block1 = 0x98,
            Channel3SourceAddress = 0x9C,
            Channel3DestinationAddress = 0xA0,
            Channel3LinkedListAddress = 0xCC,

            Channel4LinkedListBaseAddress = 0x50,
            Channel4FlagClear = 0x5C,
            Channel4Status = 0x60,
            Channel4Control = 0x64,
            Channel4Transfer1 = 0x90,
            Channel4Transfer2 = 0x94,
            Channel4Block1 = 0x98,
            Channel4SourceAddress = 0x9C,
            Channel4DestinationAddress = 0xA0,
            Channel4LinkedListAddress = 0xCC,

            Channel5LinkedListBaseAddress = 0x50,
            Channel5FlagClear = 0x5C,
            Channel5Status = 0x60,
            Channel5Control = 0x64,
            Channel5Transfer1 = 0x90,
            Channel5Transfer2 = 0x94,
            Channel5Block1 = 0x98,
            Channel5SourceAddress = 0x9C,
            Channel5DestinationAddress = 0xA0,
            Channel5LinkedListAddress = 0xCC,

            Channel6LinkedListBaseAddress = 0x50,
            Channel6FlagClear = 0x5C,
            Channel6Status = 0x60,
            Channel6Control = 0x64,
            Channel6Transfer1 = 0x90,
            Channel6Transfer2 = 0x94,
            Channel6Block1 = 0x98,
            Channel6SourceAddress = 0x9C,
            Channel6DestinationAddress = 0xA0,
            Channel6LinkedListAddress = 0xCC,

            Channel7LinkedListBaseAddress = 0x50,
            Channel7FlagClear = 0x5C,
            Channel7Status = 0x60,
            Channel7Control = 0x64,
            Channel7Transfer1 = 0x90,
            Channel7Transfer2 = 0x94,
            Channel7Block1 = 0x98,
            Channel7SourceAddress = 0x9C,
            Channel7DestinationAddress = 0xA0,
            Channel7LinkedListAddress = 0xCC,*/
        }
    }
}