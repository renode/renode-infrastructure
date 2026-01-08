//
// Copyright (c) 2010-2026 Antmicro
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
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class NXP_eDMA_Channels : IDoubleWordPeripheral, IWordPeripheral, IKnownSize
    {
        public NXP_eDMA_Channels(IMachine machine, NXP_eDMA dma, uint count, int firstChannel = 0, long channelSize = 0x1000, bool hasMuxingRegisters = true)
        {
            Count = count;
            FirstChannelNumber = firstChannel;
            ChannelSize = channelSize;
            this.dma = dma;

            channels = new Channel[count];

            var sysbus = machine.GetSystemBus(this);
            for(var i = 0; i < count; ++i)
            {
                channels[i] = new Channel(sysbus, this, firstChannel + i, hasMuxingRegisters);
                dma.SetChannel(firstChannel + i, channels[i]);
            }
        }

        public void Reset()
        {
            foreach(var channel in channels)
            {
                channel.Reset();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return channels[offset / ChannelSize].ReadDoubleWord(offset % ChannelSize);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            channels[offset / ChannelSize].WriteDoubleWord(offset % ChannelSize, value);
        }

        public ushort ReadWord(long offset)
        {
            return channels[offset / ChannelSize].ReadWord(offset % ChannelSize);
        }

        public void WriteWord(long offset, ushort value)
        {
            channels[offset / ChannelSize].WriteWord(offset % ChannelSize, value);
        }

        public long Size => ChannelSize * Count;

        public uint Count { get; }

        public long ChannelSize { get; }

        public int FirstChannelNumber { get; }

        public IEnumerable<Channel> Channels => channels;

        private readonly Channel[] channels;
        private readonly NXP_eDMA dma;

        public class Channel : IProvidesRegisterCollection<DoubleWordRegisterCollection>, IProvidesRegisterCollection<WordRegisterCollection>
        {
            public Channel(IBusController sysbus, NXP_eDMA_Channels channels, int channelNumber, bool hasMuxingRegisters = true)
            {
                this.sysbus = sysbus;
                this.channels = channels;
                dmaEngine = new DmaEngine(sysbus);
                IRQ = new GPIO();
                ChannelNumber = channelNumber;
                dwRegisters = new DoubleWordRegisterCollection(channels);
                wRegisters = new WordRegisterCollection(channels);

                DefineRegisters(hasMuxingRegisters);
            }

            public void Reset()
            {
                ServiceRequestSource = 0;
                dwRegisters.Reset();
                wRegisters.Reset();
                IRQ.Unset();
            }

            public uint ReadDoubleWord(long offset)
            {
                return dwRegisters.Read(offset);
            }

            public void WriteDoubleWord(long offset, uint value)
            {
                dwRegisters.Write(offset, value);
            }

            public ushort ReadWord(long offset)
            {
                return wRegisters.Read(offset);
            }

            public void WriteWord(long offset, ushort value)
            {
                wRegisters.Write(offset, value);
            }

            public void HardwareServiceRequest()
            {
                if(!enableDMARequest.Value && !enableAsynchronousDMARequest.Value)
                {
                    // It's not an error condition, as it's an intended programmed behavior.
                    channels.dma.DebugLog("CH{0}: Hardware request is currently disabled by channel configuration", ChannelNumber);
                    return;
                }
                ExecuteTransfer(true);
                UpdateInterrupts();
            }

            public void ChannelLinkInternalRequest()
            {
                tcdInMemory.START.Value = true;
                ExecuteTransfer();
                UpdateInterrupts();
            }

            DoubleWordRegisterCollection IProvidesRegisterCollection<DoubleWordRegisterCollection>.RegistersCollection => dwRegisters;

            WordRegisterCollection IProvidesRegisterCollection<WordRegisterCollection>.RegistersCollection => wRegisters;

            public GPIO IRQ { get; }

            public int ChannelNumber { get; }

            public ErrorFlags Errors => channelError.Value;

            public int? ServiceRequestSource { get; private set; }

            // A configuration error is an illegal setting in the transfer control descriptor.
            private static ErrorFlags CheckForConfigurationErrorsAtActivation(TransferControlDescriptorLocal tcd)
            {
                var errors = ErrorFlags.NoError;
                // A configuration error is reported when an inconsistent state is represented by one of these factors:
                // • Starting source or destination address
                // • Source or destination offsets
                // • Minor loop byte count
                // • Transfer size

                // The addresses and offsets must be aligned on zero-modulo-transfer-sized boundaries.
                var sourceTransferSize = tcd.SourceDataTransferSizeInBytes;
                var destinationTransferSize = tcd.DestinationDataTransferSizeInBytes;

                if(tcd.SourceAddress % sourceTransferSize != 0)
                {
                    errors |= ErrorFlags.SourceAddress;
                }
                if(tcd.SourceAddressSignedOffset % sourceTransferSize != 0)
                {
                    errors |= ErrorFlags.SourceOffset;
                }
                if(tcd.DestinationAddress % destinationTransferSize != 0)
                {
                    errors |= ErrorFlags.DestinationAddress;
                }
                if(tcd.DestinationAddressSignedOffset % destinationTransferSize != 0)
                {
                    errors |= ErrorFlags.DestinationOffset;
                }

                return errors;
            }

            // A scatter/gather configuration error is reported when the scatter/gather operation begins at major loop completion.
            private static ErrorFlags CheckForConfigurationErrorsAtMajorLoopCompletion(TransferControlDescriptorLocal tcd)
            {
                var errors = ErrorFlags.NoError;

                if(tcd.EnableScatterGatherProcessing)
                {
                    if(tcd.LastDestinationAddressAdjustmentOrScatterGatherAddress % TransferControlDescriptorSize != 0)
                    {
                        // The scatter/gather address is not aligned on a 32-byte boundary.
                        errors |= ErrorFlags.ScatterGatherConfiguration;
                    }
                }

                return errors;
            }

            private static ErrorFlags CheckForConfigurationErrorsAtMinorLoopCompletion(TransferControlDescriptorLocal tcd)
            {
                var errors = ErrorFlags.NoError;

                // The minor loop byte count must be a multiple of the source and destination transfer sizes.
                if(tcd.NBytes % (int)Math.Max(tcd.SourceDataTransferSizeInBytes, tcd.DestinationDataTransferSizeInBytes) != 0)
                {
                    errors |= ErrorFlags.NbytesCiterConfiguration;
                }

                if(tcd.EnableLinkCITER || tcd.EnableLinkBITER)
                {
                    if(tcd.EnableLinkCITER != tcd.EnableLinkBITER || tcd.MinorLoopLinkChannelNumberELinkYesCITER != tcd.MinorLoopLinkChannelNumberELinkYesBITER)
                    {
                        // The ELINK field does not equal for CITER and BITER or
                        // the LINKCH field does not equal for CITER and BITER.
                        errors |= ErrorFlags.NbytesCiterConfiguration;
                    }
                }

                return errors;
            }

            private ICPU GetCurrentCPUOrNull()
            {
                if(!sysbus.TryGetCurrentCPU(out var cpu))
                {
                    return null;
                }
                return cpu;
            }

            private void UpdateInterrupts()
            {
                IRQ.Set(interruptRequest.Value);
            }

            // The same flow for software and peripheral request.
            // Interrupts are updated once after exit from this method.
            private void ExecuteTransfer(bool singleIteration = false)
            {
                do
                {
                    var success = ExecuteMinorLoop();
                    if(!success)
                    {
                        return;
                    }
                    if(singleIteration)
                    {
                        break;
                    }
                }
                while(tcd.CurrentMajorIterationCount > 0);

                if(tcd.CurrentMajorIterationCount != 0)
                {
                    return;
                }

                // The major iteration count was exhausted.

                // Assert interrupt (if enabled).
                if(tcd.EnableInterruptIfMajorCounterComplete)
                {
                    interruptRequest.Value = true;
                }
                if(tcd.DisableRequest)
                {
                    enableDMARequest.Value = false;
                }

                // Reload the BITER field into the CITER field.
                tcd.CurrentMajorIterationCount = tcd.StartingMajorIterationCount;

                if(tcd.EnableStoreDestinationAddress)
                {
                    sysbus.WriteDoubleWord(tcd.LastSourceAddressAdjustmentOrStoreDADDRAddress, tcd.DestinationAddress, context);
                }
                else
                {
                    // In this context SLAST_SDA represents a signed value (negative adjustment value is allowed).
                    // We don't need to perform any conversion, as signed numbers in C# are represented in two's complement notation,
                    // so the binary representation of the result and hence the value of the register field is the same no matter the underlying type.
                    tcd.SourceAddress += tcd.LastSourceAddressAdjustmentOrStoreDADDRAddress;
                }

                if(tcd.EnableLinkWhenMajorLoopComplete)
                {
                    channels.dma.LinkChannel(ChannelNumber, tcd.MajorLoopLinkChannelNumber);
                }

                var errors = CheckForConfigurationErrorsAtMajorLoopCompletion(tcd);
                if(errors != ErrorFlags.NoError)
                {
                    ReportErrors(errors);
                    return;
                }

                if(tcd.EnableScatterGatherProcessing)
                {
                    // Fetch a new TCD using the scatter/gather address from system memory and load to a local memory.
                    var data = new byte[TransferControlDescriptorSize];
                    var destination = new Place(data, 0);
                    var req = new Request(
                        source: tcd.LastDestinationAddressAdjustmentOrScatterGatherAddress,
                        destination: destination,
                        size: TransferControlDescriptorSize,
                        readTransferType: TransferType.Byte,
                        writeTransferType: TransferType.Byte,
                        incrementReadAddress: true,
                        incrementWriteAddress: true
                    );

                    var resp = dmaEngine.IssueCopy(req, context);
                    tcd = Packet.Decode<TransferControlDescriptorLocal>(data);
                    UpdateTCDInLocalMemory(tcd);
                }
                else
                {
                    tcd.DestinationAddress += tcd.LastDestinationAddressAdjustmentOrScatterGatherAddress;
                }
            }

            private bool ExecuteMinorLoop()
            {
                if(!channels.dma.IsTransferAllowed())
                {
                    channels.dma.WarningLog("CH{0}: Transfer won't be executed, because debug or halt feature is active", ChannelNumber);
                    return false;
                }

                tcd = FetchTCDFromLocalMemory();
                channels.dma.DebugLog("CH{0}: Fetched TCD: {1}", ChannelNumber, tcd);
                var errors = CheckForConfigurationErrorsAtActivation(tcd);

                if(errors != ErrorFlags.NoError)
                {
                    // The first transfer is initiated on the internal bus, unless a configuration error is detected
                    ReportErrors(errors);
                    return false;
                }

                // These fields are updated only when some data was transferred.
                // If it was halted due to an error or debug mode, then no status is updated.
                // Hardware clears START flag after the channel begins execution,
                // what is immediate during emulation so sofware always reads 0 from these fields.
                channelDone.Value = false;
                tcd.ChannelStart = false;

                // Minor loop transfer.
                // Signed SOFF and DOFF short values are casted to ulong, but the semantic of negative value is not lost.
                // Signed numbers in C# are represented in two's complement notation.
                // Adding unsigned numbers when some of them are casted from a signed type to an unsigned type
                // gives the result with the same binary representation as adding numbers with a sign.
                // The cast is wrapped in an unchecked block to make it explicit that casting a negative number to the unsigned type is allowed here.
                var request = new Request(
                    source: tcd.SourceAddress,
                    destination: tcd.DestinationAddress,
                    size: (int)tcd.NBytes,
                    readTransferType: (TransferType)tcd.SourceDataTransferSizeInBytes,
                    writeTransferType: (TransferType)tcd.DestinationDataTransferSizeInBytes,
                    sourceIncrementStep: unchecked((ulong)tcd.SourceAddressSignedOffset),
                    destinationIncrementStep: unchecked((ulong)tcd.DestinationAddressSignedOffset),
                    incrementReadAddress: true,
                    incrementWriteAddress: true
                );

                channels.dma.DebugLog("CH{0}: Executing transfer from 0x{1:X} ({2}) to 0x{3:X} ({4}), size {5}B", ChannelNumber, request.Source.Address, request.ReadTransferType, request.Destination.Address, request.WriteTransferType, request.Size);
                var response = dmaEngine.IssueCopy(request, context);

                // Minor loop completion
                tcd.SourceAddress = (uint)response.ReadAddress;
                tcd.DestinationAddress = (uint)response.WriteAddress;
                tcd.CurrentMajorIterationCount--;

                // Signed extended value is used for the calculation, because MLOFF field width is 20 bits, so it wouldn't be automatically sign extended in C#.
                if(tcd.SourceMinorLoopOffsetEnable)
                {
                    tcd.SourceAddress += tcd.MinorLoopOffsetSignExtended;
                }
                if(tcd.DestinationMinorLoopOffsetEnable)
                {
                    tcd.DestinationAddress += tcd.MinorLoopOffsetSignExtended;
                }

                if(tcd.CurrentMajorIterationCount == tcd.StartingMajorIterationCount / 2)
                {
                    if(tcd.EnableInterruptIfMajorCounterHalfComplete)
                    {
                        interruptRequest.Value = true;
                    }
                }

                // Finished channel transfer activity.
                channelDone.Value = true;
                UpdateTCDInLocalMemory(tcd);

                errors = CheckForConfigurationErrorsAtMinorLoopCompletion(tcd);
                if(errors != ErrorFlags.NoError)
                {
                    ReportErrors(errors);
                    // Error prevents channel linking, but doesn't block other actions if the major iteration count was exhausted, so do not return here.
                }
                else
                {
                    if(tcd.EnableLinkCITER)
                    {
                        channels.dma.LinkChannel(ChannelNumber, tcd.MinorLoopLinkChannelNumberELinkYesCITER);
                    }
                }

                return true;
            }

            private void ReportErrors(ErrorFlags errors)
            {
                channelError.Value = errors;
                if(errors != ErrorFlags.NoError)
                {
                    channels.dma.ReportErrorOnChannel(ChannelNumber);
                    if(enableErrorInterrupt.Value)
                    {
                        interruptRequest.Value = true;
                    }
                }
            }

            private TransferControlDescriptorLocal FetchTCDFromLocalMemory()
            {
                return new TransferControlDescriptorLocal
                {
                    SourceAddress = (uint)tcdInMemory.SADDR.Value,
                    SourceAddressSignedOffset = (short)tcdInMemory.SOFF.Value,
                    DestinationDataTransferSize = (byte)tcdInMemory.DSIZE.Value,
                    DestinationAddressModulo = (byte)tcdInMemory.DMOD.Value,
                    SourceDataTransferSize = (byte)tcdInMemory.SSIZE.Value,
                    SourceAddressModulo = (byte)tcdInMemory.SMOD.Value,
                    NBytesWithMinorLoopOffsets = (uint)tcdInMemory.NBYTES.Value,
                    MinorLoopOffset = (uint)tcdInMemory.MLOFF.Value,
                    DestinationMinorLoopOffsetEnable = tcdInMemory.DMLOE.Value,
                    SourceMinorLoopOffsetEnable = tcdInMemory.SMLOE.Value,
                    LastSourceAddressAdjustmentOrStoreDADDRAddress = (uint)tcdInMemory.SLAST_SDA.Value,
                    DestinationAddress = (uint)tcdInMemory.DADDR.Value,
                    DestinationAddressSignedOffset = (short)tcdInMemory.DOFF.Value,
                    CurrentMajorIterationCountELinkYes = (ushort)tcdInMemory.CITER_ELINKYES.Value,
                    MinorLoopLinkChannelNumberELinkYesCITER = (ushort)tcdInMemory.CITERLINKCH.Value,
                    ReservedELinkYesCITER = (byte)tcdInMemory.CITERRESERVED.Value,
                    EnableLinkCITER = tcdInMemory.CITERELINK.Value,
                    LastDestinationAddressAdjustmentOrScatterGatherAddress = (uint)tcdInMemory.DLAST_SGA.Value,
                    ChannelStart = tcdInMemory.START.Value,
                    EnableInterruptIfMajorCounterComplete = tcdInMemory.INTMAJOR.Value,
                    EnableInterruptIfMajorCounterHalfComplete = tcdInMemory.INTHALF.Value,
                    DisableRequest = tcdInMemory.DREQ.Value,
                    EnableScatterGatherProcessing = tcdInMemory.ESG.Value,
                    EnableLinkWhenMajorLoopComplete = tcdInMemory.MAJORELINK.Value,
                    EnableEndOfPacketProcessing = tcdInMemory.EEOP.Value,
                    EnableStoreDestinationAddress = tcdInMemory.ESDA.Value,
                    MajorLoopLinkChannelNumber = (byte)tcdInMemory.MAJORLINKCH.Value,
                    BandwidthControl = (byte)tcdInMemory.BWC.Value,
                    StartingMajorIterationCountELinkYes = (ushort)tcdInMemory.BITER_ELINKYES.Value,
                    MinorLoopLinkChannelNumberELinkYesBITER = (ushort)tcdInMemory.BITERLINKCH.Value,
                    ReservedELinkYesBITER = (byte)tcdInMemory.BITERRESERVED.Value,
                    EnableLinkBITER = tcdInMemory.BITERELINK.Value
                };
            }

            private void UpdateTCDInLocalMemory(TransferControlDescriptorLocal tcd)
            {
                tcdInMemory.SADDR.Value = tcd.SourceAddress;
                tcdInMemory.SOFF.Value = (ushort)tcd.SourceAddressSignedOffset;
                tcdInMemory.DSIZE.Value = tcd.DestinationDataTransferSize;
                tcdInMemory.DMOD.Value = tcd.DestinationAddressModulo;
                tcdInMemory.SSIZE.Value = tcd.SourceDataTransferSize;
                tcdInMemory.SMOD.Value = tcd.SourceAddressModulo;
                tcdInMemory.NBYTES.Value = tcd.NBytesWithMinorLoopOffsets;
                tcdInMemory.MLOFF.Value = tcd.MinorLoopOffset;
                tcdInMemory.DMLOE.Value = tcd.DestinationMinorLoopOffsetEnable;
                tcdInMemory.SMLOE.Value = tcd.SourceMinorLoopOffsetEnable;
                tcdInMemory.SLAST_SDA.Value = tcd.LastSourceAddressAdjustmentOrStoreDADDRAddress;
                tcdInMemory.DADDR.Value = tcd.DestinationAddress;
                tcdInMemory.DOFF.Value = (ushort)tcd.DestinationAddressSignedOffset;
                tcdInMemory.CITER_ELINKYES.Value = (ushort)tcd.CurrentMajorIterationCountELinkYes;
                tcdInMemory.CITERLINKCH.Value = (ushort)tcd.MinorLoopLinkChannelNumberELinkYesCITER;
                tcdInMemory.CITERRESERVED.Value = tcd.ReservedELinkYesCITER;
                tcdInMemory.CITERELINK.Value = tcd.EnableLinkCITER;
                tcdInMemory.DLAST_SGA.Value = tcd.LastDestinationAddressAdjustmentOrScatterGatherAddress;
                tcdInMemory.START.Value = tcd.ChannelStart;
                tcdInMemory.INTMAJOR.Value = tcd.EnableInterruptIfMajorCounterComplete;
                tcdInMemory.INTHALF.Value = tcd.EnableInterruptIfMajorCounterHalfComplete;
                tcdInMemory.DREQ.Value = tcd.DisableRequest;
                tcdInMemory.ESG.Value = tcd.EnableScatterGatherProcessing;
                tcdInMemory.MAJORELINK.Value = tcd.EnableLinkWhenMajorLoopComplete;
                tcdInMemory.EEOP.Value = tcd.EnableEndOfPacketProcessing;
                tcdInMemory.ESDA.Value = tcd.EnableStoreDestinationAddress;
                tcdInMemory.MAJORLINKCH.Value = tcd.MajorLoopLinkChannelNumber;
                tcdInMemory.BWC.Value = tcd.BandwidthControl;
                tcdInMemory.BITER_ELINKYES.Value = (ushort)tcd.StartingMajorIterationCountELinkYes;
                tcdInMemory.BITERLINKCH.Value = (ushort)tcd.MinorLoopLinkChannelNumberELinkYesBITER;
                tcdInMemory.BITERRESERVED.Value = (byte)tcd.ReservedELinkYesBITER;
                tcdInMemory.BITERELINK.Value = tcd.EnableLinkBITER;
            }

            private void DefineRegisters(bool hasMuxingRegisters)
            {
                Registers.ChannelControlAndStatus.Define(dwRegisters, name: "CHn_CSR")
                    .WithFlag(0, out enableDMARequest, name: "ERQ")
                    .WithFlag(1, out enableAsynchronousDMARequest, name: "EARQ")
                    .WithFlag(2, out enableErrorInterrupt, name: "EEI")
                    .WithTaggedFlag("EBW", 3)
                    .WithReservedBits(4, 12)
                    .WithReservedBits(16, 14)
                    .WithFlag(30, out channelDone, FieldMode.WriteOneToClear | FieldMode.Read, name: "DONE")
                    .WithFlag(31, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        // Transfers are immediate so channel is always idle from the software perspective.
                        return false;
                    }, name: "ACTIVE")
                    .WithWriteCallback((_, __) =>
                    {
                        // Capture the context of cpu that configures DMA channel.
                        // It is used for DMA transfers triggered by other peripherals where cpu is not involved.
                        // Master ID Replication (Channel System Bus register) is a different mechanism that would capture
                        // the identity of core programming the eDMA's TCD.
                        context = GetCurrentCPUOrNull();
                    });

                Registers.ChannelErrorStatus.Define(dwRegisters, name: "CHn_ES")
                    .WithEnumField(0, 8, out channelError, FieldMode.Read, name: "DBE|SBE|SGE|NCE|DOE|DAE|SOE|SAE")
                    .WithReservedBits(8, 23)
                    .WithFlag(31, FieldMode.WriteOneToClear | FieldMode.Read, valueProviderCallback: _ =>
                    {
                        // This field is the logical OR of each error interrupt field (ERR).
                        return channelError.Value != ErrorFlags.NoError;
                    }, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            channelError.Value = ErrorFlags.NoError;
                        }
                    }, name: "ERR");

                Registers.ChannelInterruptStatus.Define(dwRegisters, name: "CHn_INT")
                    .WithFlag(0, out interruptRequest, FieldMode.WriteOneToClear | FieldMode.Read, name: "INT")
                    .WithReservedBits(1, 31)
                    .WithWriteCallback((_, __) => UpdateInterrupts());

                Registers.ChannelSystemBus.Define(dwRegisters, 0x00008001, name: "CHn_SBR")
                    .WithValueField(0, 5, out initiatorId, FieldMode.Read, name: "MID")
                    .WithReservedBits(5, 9)
                    .WithTaggedFlag("SEC", 14)
                    .WithTaggedFlag("PAL", 15)
                    .WithFlag(16, out enableInitiatorIdReplication, name: "EMI")
                    .WithTag("ATTR", 17, 3)
                    .WithReservedBits(20, 12);

                // All fields are marked as RW, because from the software perspective arbitration rules are respected during emulation.
                // Channel preemption and arbitration configuration is ignored, because all DMA transfers finish immediately during emulation.
                Registers.ChannelPriority.Define(dwRegisters, name: "CHn_PRI")
                    .WithValueField(0, 3, name: "APL")
                    .WithReservedBits(3, 27)
                    .WithFlag(30, name: "DPA")
                    .WithFlag(31, name: "ECP");

                if(hasMuxingRegisters)
                {
                    Registers.ChannelMultiplexorConfiguration.Define(dwRegisters, name: "CHn_MUX")
                        .WithValueField(0, 7, writeCallback: (_, value) =>
                        {
                            ServiceRequestSource = 0;
                            if(value == 0)
                            {
                                return;
                            }

                            if(channels.dma.TryGetChannelBySlot((int)value, out var occupiedChannelNumber))
                            {
                                channels.dma.WarningLog("CH{0}: Trying to select a peripheral slot already occupied by CH{1}", ChannelNumber, occupiedChannelNumber);
                                return;
                            }

                            ServiceRequestSource = (int)value;
                        }, name: "SRC")
                        .WithReservedBits(7, 25);
                    ServiceRequestSource = 0;
                }

                Registers.TCDSourceAddress.Define(dwRegisters, name: "TCDn_SADDR")
                    .WithValueField(0, 32, out tcdInMemory.SADDR, name: "SADDR");

                Registers.TCDSignedSourceAddressOffset.Define(wRegisters, name: "TCDn_SOFF")
                    .WithValueField(0, 16, out tcdInMemory.SOFF, name: "SOFF");

                Registers.TCDTransferAttributes.Define(wRegisters, name: "TCDn_ATTR")
                    .WithValueField(0, 3, out tcdInMemory.DSIZE, name: "DSIZE")
                    .WithValueField(3, 5, out tcdInMemory.DMOD, name: "DMOD")
                    .WithValueField(8, 3, out tcdInMemory.SSIZE, name: "SSIZE")
                    .WithValueField(11, 5, out tcdInMemory.SMOD, name: "SMOD");

                // Layout for TCDn_NBYTES_MLOFFYES. TCD_NBYTES_MLOFFNO merges NBYTES and MLOFF into NBYTES.
                // See NBytesWithoutMinorLoopOffsets.
                Registers.TCDTransferSize.Define(dwRegisters, name: "TCDn_NBYTES_MLOFF")
                    .WithValueField(0, 10, out tcdInMemory.NBYTES, name: "NBYTES")
                    .WithValueField(10, 20, out tcdInMemory.MLOFF, name: "MLOFF")
                    .WithFlag(30, out tcdInMemory.DMLOE, name: "DMLOE")
                    .WithFlag(31, out tcdInMemory.SMLOE, name: "SMLOE");

                Registers.TCDLastSourceAddressAdjustment.Define(dwRegisters, name: "TCDn_SLAST_SDA")
                    .WithValueField(0, 32, out tcdInMemory.SLAST_SDA, name: "SLAST_SDA");

                Registers.TCDDestinationAddress.Define(dwRegisters, name: "TCDn_DADDR")
                    .WithValueField(0, 32, out tcdInMemory.DADDR, name: "DADDR");

                Registers.TCDSignedDestinationAddressOffset.Define(wRegisters, name: "TCDn_DOFF")
                    .WithValueField(0, 16, out tcdInMemory.DOFF, name: "DOFF");

                // Layout for TCDn_CITER_ELINKYES. TCDn_CITER_ELINKNO merges CITER, LINKCH and RESERVED into CITER.
                // See CurrentMajorIterationCountELinkNo.
                Registers.TCDCurrentMajorLoopCount.Define(wRegisters, name: "TCDn_CITER_ELINK")
                    .WithValueField(0, 9, out tcdInMemory.CITER_ELINKYES, name: "CITER")
                    .WithValueField(9, 5, out tcdInMemory.CITERLINKCH, name: "LINKCH")
                    .WithValueField(14, 1, out tcdInMemory.CITERRESERVED, name: "RESERVED")
                    .WithFlag(15, out tcdInMemory.CITERELINK, name: "ELINK");

                Registers.TCDLastDestinationAddressAdjustment.Define(dwRegisters, name: "TCDn_DLAST_SGA")
                    .WithValueField(0, 32, out tcdInMemory.DLAST_SGA, name: "DLAST_SGA");

                Registers.TCDControlAndStatus.Define(wRegisters, name: "TCDn_CSR")
                    .WithFlag(0, out tcdInMemory.START, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            channels.dma.DebugLog("CH{0}: Channel started by a software initiated service request", ChannelNumber);
                            ExecuteTransfer();
                        }
                    }, name: "START")
                    .WithFlag(1, out tcdInMemory.INTMAJOR, name: "INTMAJOR")
                    .WithFlag(2, out tcdInMemory.INTHALF, name: "INTHALF")
                    .WithFlag(3, out tcdInMemory.DREQ, name: "DREQ")
                    .WithFlag(4, out tcdInMemory.ESG, name: "ESG")
                    .WithFlag(5, out tcdInMemory.MAJORELINK, name: "MAJORELINK")
                    .WithFlag(6, out tcdInMemory.EEOP, name: "EEOP")
                    .WithFlag(7, out tcdInMemory.ESDA, name: "ESDA")
                    .WithValueField(8, 5, out tcdInMemory.MAJORLINKCH, name: "MAJORLINKCH")
                    .WithReservedBits(13, 1)
                    .WithValueField(14, 2, out tcdInMemory.BWC, name: "BWC")
                    .WithWriteCallback((_, __) => UpdateInterrupts());

                // Layout for TCDn_BITER_ELINKYES. TCDn_BITER_ELINKNO merges BITER, LINKCH and RESERVED into BITER.
                Registers.TCDBeginningMajorLoopCount.Define(wRegisters, name: "TCDn_BITER_ELINK")
                    .WithValueField(0, 9, out tcdInMemory.BITER_ELINKYES, name: "BITER")
                    .WithValueField(9, 5, out tcdInMemory.BITERLINKCH, name: "LINKCH")
                    .WithValueField(14, 1, out tcdInMemory.BITERRESERVED, name: "RESERVED")
                    .WithFlag(15, out tcdInMemory.BITERELINK, name: "ELINK");
            }

            private IFlagRegisterField enableDMARequest;
            private IFlagRegisterField enableAsynchronousDMARequest;
            private IFlagRegisterField enableErrorInterrupt;
            private IFlagRegisterField channelDone;
            private IEnumRegisterField<ErrorFlags> channelError;
            private IFlagRegisterField interruptRequest;
            private IValueRegisterField initiatorId;
            private IFlagRegisterField enableInitiatorIdReplication;

            private TransferControlDescriptorLocal tcd;
            private TransferControlDescriptor tcdInMemory = new TransferControlDescriptor();
            private ICPU context;
            private readonly DoubleWordRegisterCollection dwRegisters;
            private readonly WordRegisterCollection wRegisters;
            private readonly IBusController sysbus;
            private readonly DmaEngine dmaEngine;
            private readonly NXP_eDMA_Channels channels;

            private const int TransferControlDescriptorSize = 32;

            [Flags]
            public enum ErrorFlags
            {
                NoError = 0,
                DestinationBus = 1 << 0,                // DBE
                SourceBus = 1 << 1,                     // SBE
                ScatterGatherConfiguration = 1 << 2,    // SGE
                NbytesCiterConfiguration = 1 << 3,      // NCE
                DestinationOffset = 1 << 4,             // DOE
                DestinationAddress = 1 << 5,            // DAE
                SourceOffset = 1 << 6,                  // SOE
                SourceAddress = 1 << 7                  // SAE
            }

            private struct TransferControlDescriptor
            {
                public IValueRegisterField SADDR;
                public IValueRegisterField SOFF;
                public IValueRegisterField DSIZE;
                public IValueRegisterField DMOD;
                public IValueRegisterField SSIZE;
                public IValueRegisterField SMOD;
                public IValueRegisterField NBYTES;
                public IFlagRegisterField DMLOE;
                public IFlagRegisterField SMLOE;
                public IValueRegisterField MLOFF;
                public IValueRegisterField SLAST_SDA;
                public IValueRegisterField DADDR;
                public IValueRegisterField DOFF;
                public IValueRegisterField CITER_ELINKYES;
                public IFlagRegisterField CITERELINK;
                public IValueRegisterField CITERLINKCH;
                public IValueRegisterField CITERRESERVED;
                public IValueRegisterField DLAST_SGA;
                public IFlagRegisterField START;
                public IFlagRegisterField INTMAJOR;
                public IFlagRegisterField INTHALF;
                public IFlagRegisterField DREQ;
                public IFlagRegisterField ESG;
                public IFlagRegisterField MAJORELINK;
                public IFlagRegisterField EEOP;
                public IFlagRegisterField ESDA;
                public IValueRegisterField MAJORLINKCH;
                public IValueRegisterField BWC;
                public IValueRegisterField BITER_ELINKYES;
                public IValueRegisterField BITERLINKCH;
                public IValueRegisterField BITERRESERVED;
                public IFlagRegisterField BITERELINK;
            }

            [LeastSignificantByteFirst]
            private struct TransferControlDescriptorLocal
            {
                public override string ToString()
                {
                    var mloff = (DestinationMinorLoopOffsetEnable || SourceMinorLoopOffsetEnable) ? MinorLoopOffset : 0;
                    return ""
                        + $"SADDR=0x{SourceAddress:X},"
                        + $"SOFF=0x{SourceAddressSignedOffset:X}={SourceAddressSignedOffset},"
                        + $"DSIZE={DestinationDataTransferSize},"
                        + $"DMOD={DestinationAddressModulo},"
                        + $"SSIZE={SourceDataTransferSize},"
                        + $"SMOD={SourceAddressModulo},"
                        + $"NBYTES=0x{NBytes:X}={NBytes},"
                        + $"DMLOE={DestinationMinorLoopOffsetEnable},"
                        + $"SMLOE={SourceMinorLoopOffsetEnable},"
                        + $"MLOFF=0x{mloff:X}={(int)MinorLoopOffsetSignExtended},"
                        + $"SLAST_SDA=0x{LastSourceAddressAdjustmentOrStoreDADDRAddress:X}={LastSourceAddressAdjustmentOrStoreDADDRAddress},"
                        + $"DADDR=0x{DestinationAddress:X},"
                        + $"DOFF=0x{DestinationAddressSignedOffset:X}={DestinationAddressSignedOffset},"
                        + $"CITER={CurrentMajorIterationCount},"
                        + $"CITERELINK={EnableLinkCITER},"
                        + $"CITERLINKCH={(EnableLinkCITER ? MinorLoopLinkChannelNumberELinkYesCITER : 0)},"
                        + $"DLAST_SGA=0x{LastDestinationAddressAdjustmentOrScatterGatherAddress:X}={LastDestinationAddressAdjustmentOrScatterGatherAddress},"
                        + $"START={ChannelStart},"
                        + $"INTMAJOR={EnableInterruptIfMajorCounterComplete},"
                        + $"INTHALF={EnableInterruptIfMajorCounterHalfComplete},"
                        + $"DREQ={DisableRequest},"
                        + $"ESG={EnableScatterGatherProcessing},"
                        + $"MAJORELINK={EnableLinkWhenMajorLoopComplete},"
                        + $"EEOP={EnableEndOfPacketProcessing},"
                        + $"ESDA=0x{EnableStoreDestinationAddress},"
                        + $"MAJORLINKCH={MajorLoopLinkChannelNumber},"
                        + $"BWC={BandwidthControl},"
                        + $"BITER={StartingMajorIterationCount},"
                        + $"BITERELINK={EnableLinkBITER},"
                        + $"BITERLINKCH={(EnableLinkBITER ? MinorLoopLinkChannelNumberELinkYesBITER : 0)}"
                    ;
                }

                [PacketField, Offset(doubleWords: 0, bits: 0), Width(bits: 32)]
                public uint SourceAddress;
                [PacketField, Offset(doubleWords: 1, bits: 0), Width(bits: 16)]
                public short SourceAddressSignedOffset;
                [PacketField, Offset(doubleWords: 1, bits: 16), Width(bits: 3)]
                public byte DestinationDataTransferSize;
                [PacketField, Offset(doubleWords: 1, bits: 19), Width(bits: 5)]
                public byte DestinationAddressModulo;
                [PacketField, Offset(doubleWords: 1, bits: 24), Width(bits: 3)]
                public byte SourceDataTransferSize;
                [PacketField, Offset(doubleWords: 1, bits: 27), Width(bits: 5)]
                public byte SourceAddressModulo;
                [PacketField, Offset(doubleWords: 2, bits: 0), Width(bits: 10)]
                public uint NBytesWithMinorLoopOffsets;
                [PacketField, Offset(doubleWords: 2, bits: 10), Width(bits: MinorLoopOffsetFieldWidth)]
                public uint MinorLoopOffset;
                [PacketField, Offset(doubleWords: 2, bits: 30), Width(bits: 1)]
                public bool DestinationMinorLoopOffsetEnable;
                [PacketField, Offset(doubleWords: 2, bits: 31), Width(bits: 1)]
                public bool SourceMinorLoopOffsetEnable;
                [PacketField, Offset(doubleWords: 3, bits: 0), Width(bits: 32)]
                public uint LastSourceAddressAdjustmentOrStoreDADDRAddress;
                [PacketField, Offset(doubleWords: 4, bits: 0), Width(bits: 32)]
                public uint DestinationAddress;
                [PacketField, Offset(doubleWords: 5, bits: 0), Width(bits: 16)]
                public short DestinationAddressSignedOffset;
                [PacketField, Offset(doubleWords: 5, bits: 16), Width(bits: 9)]
                public ushort CurrentMajorIterationCountELinkYes;
                [PacketField, Offset(doubleWords: 5, bits: 25), Width(bits: 5)]
                public ushort MinorLoopLinkChannelNumberELinkYesCITER;
                [PacketField, Offset(doubleWords: 5, bits: 30), Width(bits: 1)]
                public byte ReservedELinkYesCITER;
                [PacketField, Offset(doubleWords: 5, bits: 31), Width(bits: 1)]
                public bool EnableLinkCITER;
                [PacketField, Offset(doubleWords: 6, bits: 0), Width(bits: 32)]
                public uint LastDestinationAddressAdjustmentOrScatterGatherAddress;
                [PacketField, Offset(doubleWords: 7, bits: 0), Width(bits: 1)]
                public bool ChannelStart;
                [PacketField, Offset(doubleWords: 7, bits: 1), Width(bits: 1)]
                public bool EnableInterruptIfMajorCounterComplete;
                [PacketField, Offset(doubleWords: 7, bits: 2), Width(bits: 1)]
                public bool EnableInterruptIfMajorCounterHalfComplete;
                [PacketField, Offset(doubleWords: 7, bits: 3), Width(bits: 1)]
                public bool DisableRequest;
                [PacketField, Offset(doubleWords: 7, bits: 4), Width(bits: 1)]
                public bool EnableScatterGatherProcessing;
                [PacketField, Offset(doubleWords: 7, bits: 5), Width(bits: 1)]
                public bool EnableLinkWhenMajorLoopComplete;
                [PacketField, Offset(doubleWords: 7, bits: 6), Width(bits: 1)]
                public bool EnableEndOfPacketProcessing;
                [PacketField, Offset(doubleWords: 7, bits: 7), Width(bits: 1)]
                public bool EnableStoreDestinationAddress;
                [PacketField, Offset(doubleWords: 7, bits: 8), Width(bits: 5)]
                public byte MajorLoopLinkChannelNumber;
                // bit 13 is reserved
                [PacketField, Offset(doubleWords: 7, bits: 14), Width(bits: 2)]
                public byte BandwidthControl;
                [PacketField, Offset(doubleWords: 7, bits: 16), Width(bits: 9)]
                public ushort StartingMajorIterationCountELinkYes;
                [PacketField, Offset(doubleWords: 7, bits: 25), Width(bits: 5)]
                public ushort MinorLoopLinkChannelNumberELinkYesBITER;
                [PacketField, Offset(doubleWords: 7, bits: 30), Width(bits: 1)]
                public byte ReservedELinkYesBITER;
                [PacketField, Offset(doubleWords: 7, bits: 31), Width(bits: 1)]
                public bool EnableLinkBITER;

                private ushort CurrentMajorIterationCountELinkNo
                {
                    get
                    {
                        return (ushort)(ReservedELinkYesCITER << 13 | MinorLoopLinkChannelNumberELinkYesCITER << 9 | CurrentMajorIterationCountELinkYes);
                    }

                    set
                    {
                        CurrentMajorIterationCountELinkYes = BitHelper.GetValue(value, 0, 9);
                        MinorLoopLinkChannelNumberELinkYesCITER = BitHelper.GetValue(value, 9, 5);
                        ReservedELinkYesCITER = (byte)BitHelper.GetValue((ushort)value, 14, 1);
                    }
                }

                private ushort StartingMajorIterationCountELinkNo
                {
                    get
                    {
                        return (ushort)(ReservedELinkYesBITER << 14 | MinorLoopLinkChannelNumberELinkYesBITER << 9 | StartingMajorIterationCountELinkYes);
                    }

                    set
                    {
                        StartingMajorIterationCountELinkYes = BitHelper.GetValue(value, 0, 9);
                        MinorLoopLinkChannelNumberELinkYesBITER = BitHelper.GetValue(value, 9, 5);
                        ReservedELinkYesBITER = (byte)BitHelper.GetValue(value, 14, 2);
                    }
                }

                private uint NBytesWithoutMinorLoopOffsets
                {
                    get
                    {
                        return MinorLoopOffset << 10 | NBytesWithMinorLoopOffsets;
                    }

                    set
                    {
                        NBytesWithMinorLoopOffsets = BitHelper.GetValue((uint)value, 0, 10);
                        MinorLoopOffset = BitHelper.GetValue((uint)value, 10, 20);
                    }
                }

                public uint NBytes
                {
                    get
                    {
                        return SourceMinorLoopOffsetEnable ? NBytesWithMinorLoopOffsets : NBytesWithoutMinorLoopOffsets;
                    }

                    set
                    {
                        if(SourceMinorLoopOffsetEnable)
                        {
                            NBytesWithMinorLoopOffsets = value;
                        }
                        else
                        {
                            NBytesWithoutMinorLoopOffsets = value;
                        }
                    }
                }

                public uint MinorLoopOffsetSignExtended
                {
                    get
                    {
                        return BitHelper.SignExtend(MinorLoopOffset, MinorLoopOffsetFieldWidth);
                    }
                }

                public ushort CurrentMajorIterationCount
                {
                    get
                    {
                        return EnableLinkCITER ? CurrentMajorIterationCountELinkYes : CurrentMajorIterationCountELinkNo;
                    }

                    set
                    {
                        if(EnableLinkCITER)
                        {
                            CurrentMajorIterationCountELinkYes = value;
                        }
                        else
                        {
                            CurrentMajorIterationCountELinkNo = value;
                        }
                    }
                }

                public ushort StartingMajorIterationCount
                {
                    get
                    {
                        return EnableLinkBITER ? StartingMajorIterationCountELinkYes : StartingMajorIterationCountELinkNo;
                    }

                    set
                    {
                        if(EnableLinkBITER)
                        {
                            StartingMajorIterationCountELinkYes = value;
                        }
                        else
                        {
                            StartingMajorIterationCountELinkNo = value;
                        }
                    }
                }

                public int SourceDataTransferSizeInBytes => 1 << SourceDataTransferSize; // power of two

                public int DestinationDataTransferSizeInBytes => 1 << DestinationDataTransferSize; // power of two

                private const int MinorLoopOffsetFieldWidth = 20;
            }
        }

        public enum Registers
        {
            ChannelControlAndStatus = 0x00,
            ChannelErrorStatus = 0x04,
            ChannelInterruptStatus = 0x08,
            ChannelSystemBus = 0x0C,
            ChannelPriority = 0x10,
            ChannelMultiplexorConfiguration = 0x14,
            TCDSourceAddress = 0x20,
            TCDSignedSourceAddressOffset = 0x24,
            TCDTransferAttributes = 0x26,
            TCDTransferSize = 0x28,
            TCDLastSourceAddressAdjustment = 0x2C,
            TCDDestinationAddress = 0x30,
            TCDSignedDestinationAddressOffset = 0x34,
            TCDCurrentMajorLoopCount = 0x36,
            TCDLastDestinationAddressAdjustment = 0x38,
            TCDControlAndStatus = 0x3C,
            TCDBeginningMajorLoopCount = 0x3E,
        }
    }
}
