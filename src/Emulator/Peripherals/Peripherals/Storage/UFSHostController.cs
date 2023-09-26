//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Storage
{
    // Based on JESD223E, Universal Flash Storage Host Controller Interface (UFSHCI), Version 4.0
    public class UFSHostController : NullRegistrationPointPeripheralContainer<UFSDevice>, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public UFSHostController(IMachine machine, int transferRequestSlots = 32, int readyToTransferRequests = 16, int taskManagementRequestSlots = 8) : base(machine)
        {
            if(transferRequestSlots <= 0 || transferRequestSlots > 32)
            {
                // Linux driver requires at least 2 slots
                throw new ConstructionException("Minimum one and maximum 32 UTP Transfer Request slots are supported.");
            }

            if(readyToTransferRequests < 2 || readyToTransferRequests > 256)
            {
                throw new ConstructionException("Minimum two and maximum 256 Ready To Transfer (RTT) requests are supported.");
            }

            if(taskManagementRequestSlots <= 0 || taskManagementRequestSlots > 8)
            {
                throw new ConstructionException("Minimum one and maximum 8 UTP Task Management Request slots are supported.");
            }

            // Host Capabilities
            TransferRequestSlots = transferRequestSlots;
            ReadyToTransferRequests = readyToTransferRequests;
            TaskManagementRequestSlots = taskManagementRequestSlots;
            
            ExtraHeaderSegmentsLengthInUTRD = true;
            AutoHibernation = true;
            Addressing64Bit = true;
            OutOfOrderDataDelivery = true;

            // The following capabilities shouldn't be modified, because they are specific to this model 
            DMETestModeCommand = false;
            CryptoSupport = false;
            LegacySingleDoorBellRemoved = true;
            MultiQueue = false; // Multi-Circular Queue is not supported yet
            EventSpecificInterrupt = false;

            IRQ = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            sysbus = machine.GetSystemBus(this);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            aggregationCounter = 0;
            RegistersCollection.Reset();
            UpdateInterrupts();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        // IKnownSize
        public long Size => 0x2000;

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            // Host Capabilities
            Registers.HostControllerCapabilities.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (ulong)TransferRequestSlots - 1, name: "NUTRS") // '0' based value
                .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => (ulong)ReadyToTransferRequests - 1, name: "NORTT") // '0' based value
                .WithValueField(16, 3, FieldMode.Read, valueProviderCallback: _ => (ulong)TaskManagementRequestSlots - 1, name: "NUTMRS") // '0' based value
                .WithReservedBits(19, 3)
                .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => ExtraHeaderSegmentsLengthInUTRD, name: "EHSLUTRDS")
                .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => AutoHibernation, name: "AUTOH8")
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => Addressing64Bit, name: "64AS")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => OutOfOrderDataDelivery, name: "OODDS")
                .WithFlag(26, FieldMode.Read, valueProviderCallback: _ => DMETestModeCommand, name: "UICDMETMS")
                .WithTaggedFlag(" Unified Memory Extension", 27)
                .WithFlag(28, FieldMode.Read, valueProviderCallback: _ => CryptoSupport, name: "CS")
                .WithFlag(29, FieldMode.Read, valueProviderCallback: _ => LegacySingleDoorBellRemoved, name: "LSDBS")
                .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => MultiQueue, name: "MCQS")
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => EventSpecificInterrupt, name: "ESI");
            Registers.MCQCapabilities.Define(this) // MCQ not implemented
                .WithTag("MAXQ", 0, 8)
                .WithTaggedFlag("SP", 8)
                .WithTaggedFlag("RRP", 9)
                .WithTaggedFlag("EIS", 10)
                .WithReservedBits(11, 5)
                .WithTag("QCFGPTR", 16, 8)
                .WithTag("MIAG", 24, 8); 
            Registers.UFSVersion.Define(this)
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => VersionSuffix, name: "VS")
                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => MinorVersionNumber, name: "MNR")
                .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => MajorVersionNumber, name: "MJR")
                .WithReservedBits(16, 16);
            Registers.ExtendedControllerCapabilities.Define(this)
                .WithTag("wHostHintCacheSize", 0, 16)
                .WithReservedBits(16, 16);
            Registers.HostControllerIdentificationDescriptorProductId.Define(this)
                .WithTag("PID", 0, 32);
            Registers.HostControllerIdentificationDescriptorManufacturerId.Define(this)
                .WithTag("MIC", 0, 8)
                .WithTag("BI", 8, 8)
                .WithReservedBits(16, 16);
            Registers.AutoHibernateIdleTimer.Define(this)
                .WithTag("AH8ITV", 0, 10)
                .WithTag("TS", 10, 3)
                .WithReservedBits(13, 19);

            // Operation and Runtime
            Registers.InterruptStatus.Define(this)
                .WithFlags(0, 19, out interruptStatusFlags, FieldMode.Read | FieldMode.WriteOneToClear)
                .WithTag("MCQ", 19, 3) // MCQ not implemented
                .WithReservedBits(22, 10)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            Registers.InterruptEnable.Define(this)
                .WithFlags(0, 19, out interruptEnableFlags)
                .WithTag("MCQ", 19, 3) // MCQ not implemented
                .WithReservedBits(22, 10)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            Registers.HostControllerStatusExtended.Define(this)
                .WithValueField(0, 4, out utpErrorIID, FieldMode.Read, name: "IIDUTPE")
                .WithValueField(4, 4, out utpErrorExtIID, FieldMode.Read, name: "EXT_IIDUTPE")
                .WithReservedBits(8,24);
            Registers.HostControllerStatus.Define(this)
                .WithFlag(0, out devicePresent, FieldMode.Read, name: "DP")
                .WithFlag(1, out transferRequestListReady, FieldMode.Read, name: "UTRLRDY")
                .WithFlag(2, out taskManagementRequestListReady, FieldMode.Read, name: "UTMRLRDY")
                .WithFlag(3, out uicCommandReady, FieldMode.Read, name: "UCRDY")
                .WithReservedBits(4,4)
                .WithEnumField(8, 3, out uicPowerModeChangeRequestStatus, FieldMode.Read, name: "UPMCRS")
                .WithReservedBits(11, 1)
                .WithValueField(12, 4, out utpErrorCode, FieldMode.Read, name: "UTPEC")
                .WithValueField(16, 8, out utpErrorTaskTag, FieldMode.Read, name: "TTAGUTPE")
                .WithValueField(24, 8, out utpErrorTargetLUN, FieldMode.Read, name: "TLUNUTPE");
            Registers.HostControllerEnable.Define(this)
                .WithFlag(0, out hostControllerEnable, name: "hostControllerEnable", changeCallback: (_, val) =>
                {
                    if(val)
                    {
                        // UFS Device is ready for a link startup
                        interruptStatusFlags[(int)UFSInterruptFlag.UICLinkStartupStatus].Value = true;
                        uicCommandReady.Value = true;

                        ExecuteDMECommand(DMECommand.Reset);
                        ExecuteDMECommand(DMECommand.Enable);

                        transferRequestListReady.Value = true;
                        taskManagementRequestListReady.Value = true;
                    }
                })
                .WithTaggedFlag("cryptoGeneralEnable", 1) // Crypto Engine not implemented
                .WithReservedBits(2, 30);
            Registers.UICErrorCodePHYAdapterLayer.Define(this)
                .WithTag("EC", 0, 5)
                .WithReservedBits(5, 26)
                .WithTaggedFlag("ERR", 31);
            Registers.UICErrorCodeDataLinkLayer.Define(this)
                .WithTag("EC", 0, 16)
                .WithReservedBits(16, 15)
                .WithTaggedFlag("ERR", 31);
            Registers.UICErrorCodeNetworkLayer.Define(this)
                .WithTag("EC", 0, 3)
                .WithReservedBits(3, 28)
                .WithTaggedFlag("ERR", 31);
            Registers.UICErrorCodeTransportLayer.Define(this)
                .WithTag("EC", 0, 7)
                .WithReservedBits(7, 24)
                .WithTaggedFlag("ERR", 31);
            Registers.UICErrorCodeDME.Define(this)
                .WithTag("EC", 0, 4)
                .WithReservedBits(4, 27)
                .WithTaggedFlag("ERR", 31);
            Registers.UTPTransferRequestInterruptAggregationControl.Define(this)
                .WithTag("IATOVAL", 0, 8)
                .WithValueField(8, 5, out interruptAggregationCounterThreshold, name: "IACTH")
                .WithReservedBits(13, 3)
                .WithFlag(16, FieldMode.Write, writeCallback: (_, newVal) =>
                {
                    if(!newVal)
                    {
                        return;
                    }
                    aggregationCounter = 0;
                }, name: "CTR")
                .WithReservedBits(17, 3)
                .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => aggregationCounter > 0, name: "IASB")
                .WithReservedBits(21, 3)
                .WithTaggedFlag("IAPWEN", 24)
                .WithReservedBits(25, 6)
                .WithFlag(31, out interruptAggregationEnable, name: "IAEN");

            // UTP Transfer
            Registers.UTPTransferRequestListBaseAddressLow.Define(this)
                .WithReservedBits(0, 10)
                .WithValueField(10, 22, out transferRequestListBaseAddressLow, name: "UTRLBA");
            Registers.UTPTransferRequestListBaseAddressHigh.Define(this)
                .WithValueField(0, 32, out transferRequestListBaseAddressHigh, name: "UTRLBAU");
            Registers.UTPTransferRequestDoorBell.Define(this)
                .WithFlags(0, 32, out transferRequestListDoorBellFlags, name: "UTRLDBR")
                .WithWriteCallback((_, __) => ProcessTransferRequestList());
            Registers.UTPTransferRequestListClear.Define(this)
                .WithFlags(0, 32, out transferRequestListClearFlags, writeCallback: (idx, _, newVal) =>
                {
                    if(newVal)
                    {
                        return;
                    }
                    transferRequestListDoorBellFlags[idx].Value = false;
                }, name: "UTRLCLR");
            Registers.UTPTransferRequestListRunStop.Define(this)
                .WithFlag(0, out transferRequestListRunStop, changeCallback: (_, newVal) =>
                {
                    if(!newVal)
                    {
                        return;
                    }
                    for(int i = 0; i < transferRequestListCompletionNotifications.Length; i++)
                    {
                        transferRequestListCompletionNotifications[i].Value = false;
                    }
                    ProcessTransferRequestList();
                }, name: "UTRLRSR")
                .WithReservedBits(1, 31);
            Registers.UTPTransferRequestListCompletionNotification.Define(this)
                .WithFlags(0, 32, out transferRequestListCompletionNotifications, FieldMode.WriteOneToClear, name: "UTRLCNR");

            // UTP Task Management
            Registers.UTPTaskManagementRequestListBaseAddressLow.Define(this)
                .WithReservedBits(0, 10)
                .WithValueField(10, 22, out taskManagementRequestListBaseAddressLow, name: "UTMRLBA");
            Registers.UTPTaskManagementRequestListBaseAddressHigh.Define(this)
                .WithValueField(0, 32, out taskManagementRequestListBaseAddressHigh, name: "UTMRLBAU");
            Registers.UTPTaskManagementRequestDoorBell.Define(this)
                .WithFlags(0, 8, out taskManagementRequestListDoorBellFlags, name: "UTMRLDBR")
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, __) => ProcessTaskManagementRequestList());
            Registers.UTPTaskManagementRequestListClear.Define(this)
                .WithFlags(0, 8, out taskManagementRequestListClearFlags, writeCallback: (idx, _, newVal) =>
                {
                    if(newVal)
                    {
                        return;
                    }
                    taskManagementRequestListDoorBellFlags[idx].Value = false;
                }, name: "UTMRLCLR")
                .WithReservedBits(8, 24);
            Registers.UTPTaskManagementRequestListRunStop.Define(this)
                .WithFlag(0, out taskManagementRequestListRunStop, changeCallback: (_, newVal) =>
                {
                    if(!newVal)
                    {
                        return;
                    }
                    ProcessTaskManagementRequestList();
                }, name: "UTMRLRSR")
                .WithReservedBits(1, 31);

            // UIC Command
            Registers.UICCommand.Define(this)
                .WithEnumField(0, 8, out commandOpcode, writeCallback: (_, val) => ExecuteDMECommand(val), name: "CMDOP")
                .WithReservedBits(8, 24);
            Registers.UICCommandArg1.Define(this)
                .WithValueField(0, 32, out arg1, name: "ARG1");
            Registers.UICCommandArg2.Define(this)
                .WithValueField(0, 32, out arg2, name: "ARG2");
            Registers.UICCommandArg3.Define(this)
                .WithValueField(0, 32, out arg3, name: "ARG3");

            // Crypto
            Registers.CryptoCapability.Define(this) // Crypto Engine not implemented
                .WithTag("CC", 0, 7)
                .WithReservedBits(7, 1)
                .WithTag("CFGC", 8, 8)
                .WithReservedBits(16, 8)
                .WithTag("CFGPTR", 24, 8);

            // Config
            Registers.GlobalConfiguration.Define(this)
                .WithEnumField(0, 1, out queueType, writeCallback: (_, newVal) =>
                {
                    if(newVal == QueueType.MultiCircular)
                    {
                        this.Log(LogLevel.Warning, "Multi-Circular, Multi Doorbell Queue Mode is not supported");
                        newVal = QueueType.LegacySingleDoorbell; // Legacy Single Doorbell mode is supported
                    }
                }, name: "QT")
                .WithFlag(1, writeCallback: (_, newVal) =>
                {
                    if(!newVal)
                    {
                        return;
                    }
                    this.Log(LogLevel.Warning, "Event Specific Interrupt topology is not supported");
                }, name: "ESIE")
                .WithFlag(2, out prdtFormatEnable, name: "2DWPRDTEN")
                .WithReservedBits(3, 29);

            // MCQ Configuration
            Registers.MCQConfig.Define(this) // MCQ not implemented
                .WithTag("AS", 0, 2)
                .WithReservedBits(2, 6)
                .WithTag("MAC", 8, 9)
                .WithReservedBits(17, 15);
        }

        /* UPIU - UFS Protocol Information Units
        CDB - Command Descriptor Block
        UTRD - UFS Transfer Request Descriptor */
        private void ProcessTransferRequestList()
        {
            var utrlBaseAddress = transferRequestListBaseAddressHigh.Value << 32 | transferRequestListBaseAddressLow.Value << 10;
            var utrl = sysbus.ReadBytes(utrlBaseAddress, count: UTPTransferRequestDescriptorSizeInBytes * TransferRequestSlots, onlyMemory: true);
            // In batch mode, commands are executed from the lowest index
            for(int i = 0; i < TransferRequestSlots; i++)
            {
                var doorBell = transferRequestListDoorBellFlags[i];
                if(!doorBell.Value)
                {
                    continue;
                }
                var utrdOffset = UTPTransferRequestDescriptorSizeInBytes * i;
                var data = utrl.Skip(utrdOffset).Take(UTPTransferRequestDescriptorSizeInBytes).ToArray();
                var utrd = Packet.Decode<UTPTransferRequest>(data);

                var ucdBaseAddress = (ulong)utrd.UTPCommandDescriptorBaseAddressUpper << 32 | utrd.UTPCommandDescriptorBaseAddressLower << 7;
                var requestLength = 4 * utrd.ResponseUPIUOffset; // dword offset
                if(requestLength < UTPTransferRequestBaseUPIUSizeInBytes)
                {
                    this.Log(LogLevel.Warning, "UFS Protocol Information Unit is too short.");
                    continue;
                }
                var upiuBytes = sysbus.ReadBytes(ucdBaseAddress, requestLength, onlyMemory: true);

                var prdtBytes = new byte[0];
                if(utrd.PRDTLength > 0)
                {
                    var prtdBaseAddress = ucdBaseAddress + 4 * (ulong)utrd.PRDTOffset;
                    var prdtBytesCount = utrd.PRDTLength * PRDTLengthInBytes;
                    prdtBytes = sysbus.ReadBytes(prtdBaseAddress, prdtBytesCount, onlyMemory: true); 
                }

                var responseLength = 4 * utrd.ResponseUPIULength;
                var responseBytes = HandleUTPTransaction(utrd, upiuBytes, prdtBytes);
                responseBytes = responseBytes.Take(responseLength).ToArray();
                
                var responseUPIUBaseAddress = ucdBaseAddress + 4 * (ulong)utrd.ResponseUPIUOffset;
                sysbus.WriteBytes(responseBytes, responseUPIUBaseAddress, onlyMemory: true);
                
                var responseBasicHeaderData = responseBytes.Take(BasicHeaderLength).ToArray();
                var responseBasicHeader = Packet.Decode<BasicUPIUHeader>(responseBasicHeaderData);

                // Update Overall Command Status field of utrd
                utrd.OverallCommandStatus = (UTPTransferStatus)responseBasicHeader.Response;
                var utrdUpdated = Packet.Encode<UTPTransferRequest>(utrd);
                sysbus.WriteBytes(utrdUpdated, utrlBaseAddress + (ulong)utrdOffset);

                doorBell.Value = false;
                transferRequestListCompletionNotifications[i].Value = true;
                
                if(queueType.Value == QueueType.LegacySingleDoorbell && 
                    (utrd.Interrupt || utrd.OverallCommandStatus != UTPTransferStatus.Success || 
                    (aggregationCounter >= interruptAggregationCounterThreshold.Value && interruptAggregationEnable.Value)))
                {
                    interruptStatusFlags[(int)UFSInterruptFlag.UTPTransferRequestCompletionStatus].Value = true;
                    UpdateInterrupts();
                }
            }
        }

        private byte[] HandleUTPTransaction(UTPTransferRequest utrd, byte[] requestBytes, byte[] prdtBytes)
        {
            var dataOutTransfer = PrepareDataOutTransfer(utrd, prdtBytes);
            // The UniPro Service Data Unit requires no additional headers or trailer wrapped around the UPIU structure.
            var responseBytes = RegisteredPeripheral.HandleRequest(requestBytes, dataOutTransfer, out var dataInTransfer);
            
            if(dataInTransfer != null)
            {
                HandleDataInTransfer(utrd, prdtBytes, dataInTransfer);
            }

            var basicHeaderBytes = responseBytes.Take(BasicHeaderLength).ToArray();
            var basicHeader = Packet.Decode<BasicUPIUHeader>(basicHeaderBytes);
            var transactionCode = (UPIUTransactionCodeTargetToInitiator)basicHeader.TransactionCode;

            switch(transactionCode)
            {
                case UPIUTransactionCodeTargetToInitiator.Response:
                {
                    if(!utrd.Interrupt && interruptAggregationEnable.Value)
                    {
                        aggregationCounter++;
                    }
                    break;
                }
                case UPIUTransactionCodeTargetToInitiator.TaskManagementResponse:
                case UPIUTransactionCodeTargetToInitiator.QueryResponse:
                case UPIUTransactionCodeTargetToInitiator.NopIn:
                {
                    // Shouldn't be counted towards interrupt aggregation
                    break; // finish UTP transaction
                }
                default:
                    // DataIn / ReadyToTransfer should be handled within dataInTransfer / dataOutTransfer blocks
                    // because we control both Host Controller and Device implementation
                    this.Log(LogLevel.Warning, "Unexpected response received from UFS device");
                    break;
            }
            return responseBytes;
        }

        private byte[] PrepareDataOutTransfer(UTPTransferRequest utrd, byte[] prdtBytes)
        {
            var prdtLength = utrd.PRDTLength;
            var dataOutTransfer = new byte[prdtLength][];
            var totalDataLength = 0;

            for(int i = 0; i < prdtLength; i++)
            {
                var prdtEntryBytes = prdtBytes.Skip(i * PRDTLengthInBytes).Take(PRDTLengthInBytes).ToArray();
                var dataSegmentLength = GetMemoryBlockDescription(utrd, prdtEntryBytes, i == prdtLength - 1, out var dataBaseAddress);
                var dataSegment = sysbus.ReadBytes(dataBaseAddress, dataSegmentLength, onlyMemory: true);
                dataOutTransfer[i] = dataSegment;
                totalDataLength += dataSegmentLength;
            }

            var dataOut = new byte[totalDataLength];
            var offset = 0;

            for(int i = 0; i < prdtLength; i++)
            {
                var dataSegmentLength = dataOutTransfer[i].Length;
                Array.Copy(dataOutTransfer[i], 0, dataOut, offset, dataSegmentLength);
                offset += dataSegmentLength;
            }

            return dataOut;
        }

        private void HandleDataInTransfer(UTPTransferRequest utrd, byte[] prdtBytes, byte[] dataInTransfer)
        {
            var prdtLength = utrd.PRDTLength;
            var dataTransferLength = dataInTransfer.Length;
            var remainingBytes = dataTransferLength;
            var offset = 0;
            for(int i = 0; i < prdtLength; i++)
            {
                var prdtEntryBytes = prdtBytes.Skip(i * PRDTLengthInBytes).Take(PRDTLengthInBytes).ToArray();
                var dataSegmentLength = GetMemoryBlockDescription(utrd, prdtEntryBytes, i == prdtLength-1, out var dataBaseAddress);
                if(i < prdtLength - 1 && remainingBytes < dataSegmentLength)
                {
                    this.Log(LogLevel.Warning, $"Data In transfer too short - it's not the last packet. Expected {dataSegmentLength}, got {dataTransferLength}");
                    break;
                }
                var dataSegment = dataInTransfer.Skip(offset).Take(Math.Min(dataSegmentLength, remainingBytes)).ToArray();
                sysbus.WriteBytes(dataSegment, dataBaseAddress, onlyMemory: true);
                offset += dataSegmentLength;
                remainingBytes -= dataSegmentLength;
            }
        }

        private int GetMemoryBlockDescription(UTPTransferRequest utrd, byte[] prdtEntryBytes, bool isLastBlock, out ulong dataBaseAddress)
        {
            uint dataSegmentLength;
            if(prdtFormatEnable.Value)
            {
                var prdt2dw = Packet.Decode<PRDT2DW>(prdtEntryBytes);
                dataBaseAddress = prdt2dw.DataBaseAddressUpper << 32 | (prdt2dw.DataBaseAddress << 2);
                dataSegmentLength = isLastBlock ? (4u * utrd.LastDataByteCount) : (4096u * utrd.CommonDataSize);
            }
            else
            {
                var prdt4dw = Packet.Decode<PRDT4DW>(prdtEntryBytes);
                dataBaseAddress = prdt4dw.DataBaseAddressUpper << 32 | (prdt4dw.DataBaseAddress << 2);
                dataSegmentLength = prdt4dw.DataByteCount + 1; // dataByteCount is 0 based value - 0 means 1, 1 means 2 etc
            }
            return (int)dataSegmentLength;
        }

        private void ProcessTaskManagementRequestList()
        {
            var utmrlBaseAddress = taskManagementRequestListBaseAddressHigh.Value << 32 | taskManagementRequestListBaseAddressLow.Value << 10;
            var utmrl = sysbus.ReadBytes(utmrlBaseAddress, UTPTaskManagementRequestDescriptorSizeInBytes * TaskManagementRequestSlots, onlyMemory: true);
            // In batch mode, commands are executed from the lowest index
            for(int i = 0; i < TaskManagementRequestSlots; i++)
            {
                if(!taskManagementRequestListDoorBellFlags[i].Value)
                {
                    continue;
                }
                var utmrdOffset = UTPTaskManagementRequestDescriptorSizeInBytes * i;

                var data = utmrl.Skip(count: utmrdOffset).Take(UTPTaskManagementRequestDescriptorSizeInBytes).ToArray();
                var headerBytes = data.Take(UTPTaskManagementRequestHeaderSizeInBytes).ToArray();
                var utmrd = Packet.Decode<UTPTaskManagementRequestHeader>(headerBytes);

                var upiuBytes = data.Skip(UTPTaskManagementRequestHeaderSizeInBytes).Take(UTPTaskManagementRequestUPIUSizeInBytes).ToArray();

                var responseBytes = RegisteredPeripheral.HandleRequest(upiuBytes, null, out var _);
                responseBytes = responseBytes.Take(UTPTaskManagementResponseUPIUSizeInBytes).ToArray();
                sysbus.WriteBytes(responseBytes, utmrlBaseAddress + (ulong)utmrdOffset + UTPTaskManagementRequestHeaderSizeInBytes + UTPTaskManagementRequestUPIUSizeInBytes, onlyMemory: true);

                var responseBasicHeaderData = responseBytes.Take(BasicHeaderLength).ToArray();
                var responseBasicHeader = Packet.Decode<BasicUPIUHeader>(responseBasicHeaderData);
                
                // Update Overall Command Status field
                utmrd.OverallCommandStatus = (UTPTaskManagementStatus)responseBasicHeader.Response;
                var utmrdUpdated = Packet.Encode<UTPTaskManagementRequestHeader>(utmrd);
                sysbus.WriteBytes(utmrdUpdated, utmrlBaseAddress + (ulong)utmrdOffset);
                taskManagementRequestListDoorBellFlags[i].Value = false;

                if(queueType.Value == QueueType.LegacySingleDoorbell && utmrd.Interrupt)
                {
                    interruptStatusFlags[(int)UFSInterruptFlag.UTPTaskManagementRequestCompletionStatus].Value = true;
                    UpdateInterrupts();
                }
            }
        }

        private void ExecuteDMECommand(DMECommand command)
        {
            // DME commands are part of MIPI UniPro specification
            switch(command)
            {
                case DMECommand.Get:
                    var mibAttr = arg1.Value >> 16;
                    if(mibAttr == UniProPowerStateAttribute)
                    {
                        arg3.Value = (ulong)LinkStatus.Up;
                    }
                    interruptStatusFlags[(int)UFSInterruptFlag.UICCommandCompletionStatus].Value = true;
                    UpdateInterrupts();
                    break;
                case DMECommand.Set:
                case DMECommand.PeerSet:
                    interruptStatusFlags[(int)UFSInterruptFlag.UICCommandCompletionStatus].Value = true;
                    UpdateInterrupts();
                    break;
                case DMECommand.LinkStartup:
                    interruptStatusFlags[(int)UFSInterruptFlag.UICCommandCompletionStatus].Value = true;
                    UpdateInterrupts();
                    if(RegisteredPeripheral != null)
                    {
                        devicePresent.Value = true;
                    }
                    break;
                case DMECommand.Enable:
                case DMECommand.Reset:
                    this.Log(LogLevel.Debug, $"Part of auto-initialization: {Enum.GetName(typeof(DMECommand), command)}");
                    break;
                default:
                    this.Log(LogLevel.Warning, $"Unhandled UIC Command 0x{command:X}");
                    break;
            }
        }

        private void UpdateInterrupts()
        {
            var flag = false;
            for(int i = 0; i < interruptStatusFlags.Length; i++)
            {
                flag |= interruptStatusFlags[i].Value && interruptEnableFlags[i].Value;
            }

            IRQ.Set(flag);
        }

        public int TransferRequestSlots { get; }
        public int ReadyToTransferRequests { get; }
        public int TaskManagementRequestSlots { get; }

        public bool ExtraHeaderSegmentsLengthInUTRD { get; }
        public bool AutoHibernation { get; }
        public bool Addressing64Bit { get; }
        public bool OutOfOrderDataDelivery { get; }
        public bool DMETestModeCommand { get; }
        public bool CryptoSupport { get; }
        public bool LegacySingleDoorBellRemoved { get; }
        public bool MultiQueue { get; }
        public bool EventSpecificInterrupt { get; }
        public int PRDTLengthInBytes => prdtFormatEnable.Value ? 8 : 16;
        public static string UFSVersion => $"{MajorVersionNumber}.{MinorVersionNumber}{VersionSuffix}";

        private IFlagRegisterField hostControllerEnable;

        // UTPTransferRequestInterruptAggregationControl
        private IValueRegisterField interruptAggregationCounterThreshold;
        private IFlagRegisterField interruptAggregationEnable;

        private IEnumRegisterField<DMECommand> commandOpcode;
        private IValueRegisterField arg1;
        private IValueRegisterField arg2;
        private IValueRegisterField arg3;

        private IFlagRegisterField[] interruptEnableFlags;
        private IFlagRegisterField[] interruptStatusFlags;
        // HostControllerStatusExtended
        private IValueRegisterField utpErrorIID;
        private IValueRegisterField utpErrorExtIID;
        // HostControllerStatus
        private IFlagRegisterField devicePresent;
        private IFlagRegisterField transferRequestListReady;
        private IFlagRegisterField taskManagementRequestListReady;
        private IFlagRegisterField uicCommandReady;
        private IEnumRegisterField<UICPowerModeChangeRequestStatus> uicPowerModeChangeRequestStatus;
        private IValueRegisterField utpErrorCode;
        private IValueRegisterField utpErrorTaskTag;
        private IValueRegisterField utpErrorTargetLUN;
        // UTP Transfer
        private IValueRegisterField transferRequestListBaseAddressLow;
        private IValueRegisterField transferRequestListBaseAddressHigh;
        private IFlagRegisterField[] transferRequestListDoorBellFlags;
        private IFlagRegisterField[] transferRequestListClearFlags;
        private IFlagRegisterField transferRequestListRunStop;
        private IFlagRegisterField[] transferRequestListCompletionNotifications;
        // UTP Task Management
        private IValueRegisterField taskManagementRequestListBaseAddressLow;
        private IValueRegisterField taskManagementRequestListBaseAddressHigh;
        private IFlagRegisterField[] taskManagementRequestListDoorBellFlags;
        private IFlagRegisterField[] taskManagementRequestListClearFlags;
        private IFlagRegisterField taskManagementRequestListRunStop;
        //GlobalConfiguration
        private IEnumRegisterField<QueueType> queueType;
        private IFlagRegisterField prdtFormatEnable;

        private ulong aggregationCounter;
        private readonly IBusController sysbus;

        private const ushort UniProPowerStateAttribute = 0xd083;
        private const int MajorVersionNumber = 4;
        private const int MinorVersionNumber = 0;
        private const int VersionSuffix = 0;
        private const int UTPTaskManagementRequestDescriptorSizeInBytes = 80;
        private const int UTPTaskManagementRequestUPIUSizeInBytes = 32;
        private const int UTPTaskManagementResponseUPIUSizeInBytes = 32;
        private const int UTPTaskManagementRequestHeaderSizeInBytes = 16;
        private const int UTPTransferRequestDescriptorSizeInBytes = 32;
        private const int UTPTransferRequestBaseUPIUSizeInBytes = 32;
        private const int BasicHeaderLength = 12;

        private enum DMECommand: byte
        {
            // Configuration commands
            Get = 0x01,
            Set = 0x02,
            PeerGet = 0x03,
            PeerSet = 0x04,
            // Control commands
            PowerOn = 0x10, // optional
            PowerOff = 0x11, //optional
            Enable = 0x12,
            Reset = 0x14,
            EndpointReset = 0x15,
            LinkStartup = 0x16,
            HibernateEnter = 0x17,
            HibernateExit = 0x18,
            TestMode = 0x1a, // optional
        }

        private enum UICPowerModeChangeRequestStatus: byte
        {
            PowerOk = 0x0,
            PowerLocal = 0x1,
            PowerRemote = 0x2,
            PowerBusy = 0x3,
            PowerErrorCapability = 0x4,
            PowerFatalError = 0x5,
        }

        private enum UFSInterruptFlag
        {
            UTPTransferRequestCompletionStatus = 0, // UTRCS
            UICDMEEndpointResetIndication = 1, // UDEPRI
            UICError = 2, // UE
            UICTestModeStatus = 3, // UTMS
            UICPowerModeStatus = 4, // UPMS
            UICHibernateExitStatus = 5, // UHXS
            UICHibernateEnterStatus = 6, // UHES
            UICLinkLostStatus = 7, // ULLS
            UICLinkStartupStatus = 8, // ULSS
            UTPTaskManagementRequestCompletionStatus = 9, // UTMRCS
            UICCommandCompletionStatus = 10, // UCCS
            DeviceFatalErrorStatus = 11, // DFES
            UTPErrorStatus = 12, // UTPES
            HostControllerFatalErrorStatus = 16, // HCFES
            SystemBusFatalErrorStatus = 17, // SBFES
            CryptoEngineFatalErrorStatus = 18, // CEFES
        }

        private enum LinkStatus
        {
            Down = 1,
            Up = 2,
        }

        private enum QueueType
        {
            LegacySingleDoorbell = 0,
            MultiCircular = 1
        }

        private enum Registers : uint
        {
            // Host Capabilities
            HostControllerCapabilities = 0x00,
            MCQCapabilities = 0x04,
            UFSVersion = 0x08,
            ExtendedControllerCapabilities = 0xc,
            HostControllerIdentificationDescriptorProductId = 0x10,
            HostControllerIdentificationDescriptorManufacturerId = 0x14,
            AutoHibernateIdleTimer = 0x18,
            // Operation and Runtime
            InterruptStatus = 0x20,
            InterruptEnable = 0x24,
            HostControllerStatusExtended = 0x2c,
            HostControllerStatus = 0x30,
            HostControllerEnable = 0x34,
            UICErrorCodePHYAdapterLayer = 0x38,
            UICErrorCodeDataLinkLayer = 0x3c,
            UICErrorCodeNetworkLayer = 0x40,
            UICErrorCodeTransportLayer = 0x44,
            UICErrorCodeDME = 0x48,
            UTPTransferRequestInterruptAggregationControl = 0x4c,
            // UTP Transfer
            UTPTransferRequestListBaseAddressLow = 0x50,
            UTPTransferRequestListBaseAddressHigh = 0x54,
            UTPTransferRequestDoorBell = 0x58,
            UTPTransferRequestListClear = 0x5c,
            UTPTransferRequestListRunStop = 0x60,
            UTPTransferRequestListCompletionNotification = 0x64,
            // UTP Task Management
            UTPTaskManagementRequestListBaseAddressLow = 0x70,
            UTPTaskManagementRequestListBaseAddressHigh = 0x74,
            UTPTaskManagementRequestDoorBell = 0x78,
            UTPTaskManagementRequestListClear = 0x7c,
            UTPTaskManagementRequestListRunStop = 0x80,
            // UIC Command
            UICCommand = 0x90,
            UICCommandArg1 = 0x94,
            UICCommandArg2 = 0x98,
            UICCommandArg3 = 0x9c,
            // Crypto
            CryptoCapability = 0x100,
            // Config
            GlobalConfiguration = 0x300,
            // MCQ Configuration
            MCQConfig = 0x380,
        }
    }
}