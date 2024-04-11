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
                this.Log(LogLevel.Info, "Created channel " + i);
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
                        if (val)
                        {
                            channels[j].GlobalInterrupt = false;
                            channels[j].TransferComplete = false;
                            channels[j].HalfTransfer = false;
                        }
                    },
                    name: $"Global interrupt flag clear for channel {j} (CGIF{j})");
                interruptFlagClear.DefineFlagField(j * 4 + 1, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if (val)
                        {
                            channels[j].TransferComplete = false;
                            channels[j].GlobalInterrupt = false;
                        }
                    },
                    name: $"Transfer complete flag clear for channel {j} (CTEIF{j})");
                interruptFlagClear.DefineFlagField(j * 4 + 2, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if (val)
                        {
                            channels[j].HalfTransfer = false;
                            channels[j].GlobalInterrupt = false;
                        }
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

                this.Log(LogLevel.Info, "Write in Channel {0} with offset: {1} and value: {2}", channelNo, offset, value);
                channels[channelNo].WriteDoubleWord(offset, value);
                return;
            }
            this.Log(LogLevel.Error, "Could not write to offset 0x{0:X} nor write to channel {1}, the channel has to be in range 0-{2}", offset, channelNo, numberOfChannels);
        }

        public void OnGPIO(int number, bool value)
        {
            if (number > channels.Length)
            {
                this.Log(LogLevel.Error, "Channel number {0} is out of range, must be in [0; {1}]", number, channels.Length - 1);
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
                    .WithReservedBits(0, 16)
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
                                if (val)
                                {
                                    DataTransferError = false;
                                }
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
                    .WithValueField(16, 8, out monitoredFIFOlevel, name: "Monitored FIFO level (FIFOL)")   //TODO 
                    .WithReservedBits(24, 8));
                registersMap.Add((long)ChannelRegisters.ChannelControl + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithFlag(0, out channelEnable,
                        writeCallback: (_, val) =>
                        {
                            if (val)
                            {
                                parent.Log(LogLevel.Info, "Before DoTransfer in Enable register");
                                DoTransfer();
                            }
                        })
                      .WithFlag(1, out channelReset,
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
                      .WithFlag(2, out channelSuspend,
                        writeCallback: (_, val) =>
                        {
                            if (val)
                            {
                                // TODO
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
                    .WithValueField(4, 5, out sourceBurstLength, name: "Source burst length (SBL_1)")  //TODO              
                    .WithReservedBits(10, 1)
                    .WithTag("Padding alignment mode (PAM)", 11, 2)
                    .WithTag("Source byte exchange (SBX)", 13, 1)
                    .WithTag("Source allocated port (SAP)", 14, 1)
                    .WithTag("Security attribute source (SSEC)", 15, 1)
                    .WithEnumField(16, 2, out destinationDataWith, name: "Binary logarithm destination data with (DDW_LOG2)")
                    .WithReservedBits(18, 1)
                    .WithFlag(19, out destinationIncrementingBurst, name: "Destination incrementing burst (DINC)")
                    .WithValueField(20, 6, out destinationBurstLength, name: "Destination burst length (DBL_1)")   //TODO   
                    .WithTag("Destination byte exchange (DBX)", 26, 1)
                    .WithTag("Destination half-word exchange (DHX)", 27, 1)
                    .WithReservedBits(28, 2)
                    .WithTag("Destination allocated port (DAP)", 30, 1)
                    .WithTag("Security attribute destination (DSEC)", 31, 1));
                registersMap.Add((long)ChannelRegisters.ChannelTransfer2 + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithEnumField(0, 5, out hardwareRequestSelection, name: "Hardware request selection (REQSEL)")
                    .WithReservedBits(6, 3)
                    .WithFlag(9, out softwareRequest, name: "Software request (SWREQ)")
                    .WithTag("Destination hardware request (DREQ)", 10, 1)
                    .WithTag("Block hardware request (BREQ)", 11, 1)
                    .WithReservedBits(12, 2)
                    .WithTag("Trigger mode (TRIGM)", 14, 2)
                    .WithEnumField(16, 5, out triggerEventInputSelection, name: "Trigger event input selection (TRIGSEL)")
                    .WithReservedBits(21, 3)
                    .WithTag("Trigger event polarity (TRIGPOL)", 24, 2)
                    .WithReservedBits(26, 4)
                    .WithValueField(30, 2, out transferCompleteEventMode, name: "Transfer complete event mode (TCEM)")   //TODO 
                    );
                //TODO: implement registers & callbacks
                registersMap.Add((long)ChannelRegisters.ChannelBlock1 + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                /* TODO: Block size transferred from the source. When the channel is enabled, this field becomes
                    read-only and is decremented, indicating the remaining number of data items in the current
                    source block to be transferred. BNDT[15:0] is programmed in number of bytes, maximum
                    source block size is 64 Kbytes -1. Once the last data transfer is completed (BNDT[15:0] = 0)*/
                    .WithValueField(0, 16, out blockNumberDataBytesFromSource, name: "Block number data bytes from source (BNDT)")
                    );
                registersMap.Add((long)ChannelRegisters.ChannelSourceAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out sourceAddress, name: "Source address (SA)")
                    );
                registersMap.Add((long)ChannelRegisters.ChannelDestinationAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out destinationAddress, name: "Destination address (DA)"));
                registersMap.Add((long)ChannelRegisters.ChannelLinkedListAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithReservedBits(0, 2)
                    /*TODO: If UT1 = UT2 = UB1 = USA = UDA = ULL = 0 and if LA[15:2] = 0, the current LLI is the last
                    one. The channel transfer is completed without any update of the linked-list GPDMA register
                    file. Else, this field is the pointer to the memory address offset from which the next linked-list data
                    structure is automatically fetched from, once the data transfer is completed, in order to
                    conditionally update the linked-list GPDMA internal register file (GPDMA_CxTR1,
                    GPDMA_CxTR2, GPDMA_CxBR1, GPDMA_CxSAR, GPDMA_CxDAR, and
                    GPDMA_CxLLR*/
                    .WithValueField(2, 14, out lowSignificantAddress, name: "Low-significant address (LA)")
                    .WithFlag(16, out updateLLRfromMemory, name: "Update CxLLR register from memory (ULL)")
                    .WithReservedBits(17, 10)
                    .WithFlag(27, out updateDARfromMemory, name: "Update CxDAR register from memory (UDA)")
                    .WithFlag(28, out updateSARfromMemory, name: "Update CxSAR register from memory (USA)")
                    .WithFlag(29, out updateBR1fromMemory, name: "Update CxBR1 register from memory (UB1)")
                    .WithFlag(30, out updateTR2fromMemory, name: "Update CxTR2 register from memory (UT2)")
                    .WithFlag(31, out updateTR1fromMemory, name: "Update CxTR1 register from memory (UT1)")
                    );
                registers = new DoubleWordRegisterCollection(parent, registersMap);
            }

            public uint ReadDoubleWord(long offset)
            {
                parent.Log(LogLevel.Debug, "Read in register {0}", offset);
                return registers.Read(offset);
            }

            public void WriteDoubleWord(long offset, uint value)
            {
                parent.Log(LogLevel.Debug, "Write in register offset {0}", offset);
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
                if (!Enabled || monitoredFIFOlevel.Value == 0)
                {
                    return false;
                }
                parent.Log(LogLevel.Info, "Before DoTransfer from TryTriggerTransfer");
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
                parent.Log(LogLevel.Info, "Start DoTransfer");
                //TODO: implement linked list mode / 
                var toCopy = (uint)blockNumberDataBytesFromSource.Value;
                parent.Log(LogLevel.Info, "toCopy: {0}", toCopy);
                toCopy = Math.Max((uint)SizeToType(sourceDataWith.Value),
                   (uint)SizeToType(destinationDataWith.Value));
                parent.Log(LogLevel.Info, "toCopy: {0}", toCopy);
                blockNumberDataBytesFromSource.Value -= 1; //TODO update proper register
                parent.Log(LogLevel.Info, "blockNumberDataBytesFromSource: {0}", blockNumberDataBytesFromSource.Value);

                parent.Log(LogLevel.Info, "IssueCopy - currentSourceAddress: {0}, currentDestinationAddress: {1}, " + 
                        "sourceIncrementingBurst: {2}, destinationIncrementingBurst: {3}, " + 
                        "sourceDataWith: {4}, destinationDataWith: {5} ",
                            currentSourceAddress, currentDestinationAddress,
                            sourceIncrementingBurst.Value, destinationIncrementingBurst.Value,
                            sourceDataWith.Value,destinationDataWith.Value);
                var response = IssueCopy(currentSourceAddress, currentDestinationAddress, toCopy,
                    sourceIncrementingBurst.Value, destinationIncrementingBurst.Value, sourceDataWith.Value,
                    destinationDataWith.Value);
                parent.Log(LogLevel.Info, "response read/write addr: {0} / {1}", response.ReadAddress, response.WriteAddress);
                currentSourceAddress = response.ReadAddress.Value;
                currentDestinationAddress = response.WriteAddress.Value;
                HalfTransfer = blockNumberDataBytesFromSource.Value <= originalBlockNumberDataBytesFromSource / 2;
                TransferComplete = blockNumberDataBytesFromSource.Value == 0;

                // TODO: check if this still applies to WBA55. currently leads to NullRef
                // Loop around if circular mode is enabled
                /*if (circularMode.Value && blockNumberDataBytesFromSource.Value == 0)
                {
                    blockNumberDataBytesFromSource.Value = originalBlockNumberDataBytesFromSource;
                    currentSourceAddress = sourceAddress.Value;
                    currentDestinationAddress = destinationAddress.Value;
                }*/
                // No parent.Update - this is called by the register write and TryTriggerTransfer
                // to avoid calling it twice in the former case
            }

            private Response IssueCopy(ulong sourceAddress, ulong destinationAddress, uint size,
                bool incrementReadAddress, bool incrementWriteAddress, TransferSize sourceDataWith,
                TransferSize destinationDataWith)
            {
                var request = new Request(
                    sourceAddress,
                    destinationAddress,
                    (int)size,
                    SizeToType(sourceDataWith),
                    SizeToType(destinationDataWith),
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

            private IFlagRegisterField circularMode;
            private IValueRegisterField monitoredFIFOlevel;

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
            private IEnumRegisterField<Priority> priorityLevel;

            private IValueRegisterField linkedListBaseAddress;

            //Transfer Register CxTR 1&2   
            private IFlagRegisterField sourceIncrementingBurst;
            private IFlagRegisterField destinationIncrementingBurst;
            private IValueRegisterField sourceBurstLength;
            private IValueRegisterField destinationBurstLength;
            private IEnumRegisterField<TransferSize> sourceDataWith;
            private IEnumRegisterField<TransferSize> destinationDataWith;
            private IEnumRegisterField<HardwareRequestSelection> hardwareRequestSelection;
            private IFlagRegisterField softwareRequest;
            private IEnumRegisterField<TriggerEventInputSelection> triggerEventInputSelection;
            private IValueRegisterField transferCompleteEventMode;

            private IValueRegisterField blockNumberDataBytesFromSource;
            private IValueRegisterField sourceAddress;
            private IValueRegisterField destinationAddress;

            // Linked list address
            private IValueRegisterField lowSignificantAddress;
            private IFlagRegisterField updateLLRfromMemory;
            private IFlagRegisterField updateDARfromMemory;
            private IFlagRegisterField updateSARfromMemory;
            private IFlagRegisterField updateBR1fromMemory;
            private IFlagRegisterField updateTR2fromMemory;
            private IFlagRegisterField updateTR1fromMemory;

            private ulong currentSourceAddress;
            private ulong currentDestinationAddress;
            private ulong originalBlockNumberDataBytesFromSource;

            private readonly DoubleWordRegisterCollection registers;
            private readonly STM32WBA55_GPDMA parent;
            private readonly int channelNumber;

            private enum Priority
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

            private enum HardwareRequestSelection
            {
                Adc4_dma = 0,
                Spi1_rx_dma = 1,
                Spi1_tx_dma = 2,
                Spi3_rx_dma = 3,
                Spi3_tx_dma = 4,
                I2c1_rx_dma = 5,
                I2c1_tx_dma = 6,
                I2c1_evc_dma = 7,
                I2c3_rx_dma = 8,
                I2c3_tx_dma = 9,
                I2c3_evc_dma = 10,
                Usart1_rx_dma = 11,
                Usart1_tx_dma = 12,
                Usart2_rx_dma = 13,
                Usart2_tx_dma = 14,
                Lpuart1_rx_dma = 15,
                Lpuart1_tx_dma = 16,
                Sai_a_dma = 17,
                Sai_b_dma = 18,
                Tim1_cc1_dma = 19,
                Tim1_cc2_dma = 20,
                Tim1_cc3_dma = 21,
                Tim1_cc4_dma = 22,
                Tim1_upd_dma = 23,
                Tim1_trg_dma = 24,
                Tim1_com_dma = 25,
                Tim2_cc1_dma = 26,
                Tim2_cc2_dma = 27,
                Tim2_cc3_dma = 28,
                Tim2_cc4_dma = 29,
                Tim2_upd_dma = 30,
                Tim3_cc1_dma = 31,
                Tim3_cc2_dma = 32,
                Tim3_cc3_dma = 33,
                Tim3_cc4_dma = 34,
                Tim3_upd_dma = 35,
                Tim3_trg_dma = 36,
                Tim16_cc1_dma = 37,
                Tim16_upd_dma = 38,
                Tim17_cc1_dma = 39,
                Tim17_upd_dma = 40,
                Aes_in_dma = 41,
                Aes_out_dma = 42,
                Hash_in_dma = 43,
                Saes_in_dma = 44,
                Saes_out_dma = 45,
                Lptim1_ic1_dma = 46,
                Lptim1_ic2_dma = 47,
                Lptim1_ue_dma = 48,
                Lptim2_ic1_dma = 49,
                Lptim2_ic2_dma = 50,
                Lptim2_ue_dma = 51,
            }

            private enum TriggerEventInputSelection
            {
                Exti0 = 0,
                Exti1 = 1,
                Exti2 = 2,
                Exti3 = 3,
                Exti4 = 4,
                Exti5 = 5,
                Exti6 = 6,
                Exti7 = 7,
                Tamp_trg1 = 8,
                Tamp_trg2 = 9,
                Tamp_trg3 = 10,
                Lptim1_ch1 = 11,
                Lptim1_ch2 = 12,
                Lptim2_ch1 = 13,
                Lptim2_ch2 = 14,
                Comp1_out = 15,
                Comp2_out = 16,
                Rtc_alra_trg = 17,
                Rtc_alrb_trg = 18,
                Rtc_wut_trg = 19,
                Gpdma1_ch0_tc = 20,
                Gpdma1_ch1_tc = 21,
                Gpdma1_ch2_tc = 22,
                Gpdma1_ch3_tc = 23,
                Gpdma1_ch4_tc = 24,
                Gpdma1_ch5_tc = 25,
                Gpdma1_ch6_tc = 26,
                Gpdma1_ch7_tc = 27,
                Tim2_trgo = 28,
                Adc4_awd1 = 29,
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