//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities.Packets;
using Antmicro.Renode.Storage.SCSI;
using Antmicro.Renode.Storage.SCSI.Commands;

namespace Antmicro.Renode.Peripherals.Storage
{
    // Based on JESD220F, Universal Flash Storage (UFS), Version 4.0
    public class UFSDevice : IPeripheral, IDisposable
    {
        public UFSDevice(int logicalUnits, ulong logicalBlockSize, ulong blockCount,
                string manufacturerName = "01234567", string productName = "0123456789ABCDEF", string serialNumber = "SomeSerialNumber",
                string oemID = "SomeOemID", string productRevisionLevel = "0123")
        {
            if(logicalUnits <= 1 || logicalUnits > MaxLogicalUnits)
            {
                throw new ConstructionException($"Minimum one and maximum {MaxLogicalUnits} logical units are allowed.");
            }

            if(!Misc.IsPowerOfTwo(logicalBlockSize) || logicalBlockSize < MinimumLogicalBlockSize)
            {
                throw new ConstructionException($"Logical block size must be a power of two and at least {MinimumLogicalBlockSize}, but got {logicalBlockSize}.");
            }

            if(manufacturerName.Length != ManufacturerNameLength)
            {
                manufacturerName = manufacturerName.PadRight(ManufacturerNameLength).Substring(0, ManufacturerNameLength);
                this.Log(LogLevel.Warning, $"Manufacturer Name String Descriptor must have exactly {ManufacturerNameLength} characters. String was normalized.");
            }

            if(productName.Length != ProductNameLength)
            {
                productName = productName.PadRight(totalWidth: ProductNameLength).Substring(0, ProductNameLength);
                this.Log(LogLevel.Warning, $"Product Name String Descriptor must have exactly {ProductNameLength} characters. String was normalized.");
            }

            if(productRevisionLevel.Length != ProductRevisionLevelLength)
            {
                productRevisionLevel = productRevisionLevel.PadRight(totalWidth: ProductRevisionLevelLength).Substring(0, ProductRevisionLevelLength);
                this.Log(LogLevel.Warning, $"Product Revision Level String Descriptor must have exactly {ProductRevisionLevelLength} characters. String was normalized.");
            }

            if(serialNumber.Length > SerialNumberMaxLength)
            {
                serialNumber = serialNumber.Substring(0, SerialNumberMaxLength);
                this.Log(LogLevel.Warning, $"Serial Number String Descriptor can consist of up to {SerialNumberMaxLength} characters. String was normalized.");
            }

            if(oemID.Length > OemIDMaxLength)
            {
                oemID = oemID.Substring(0, OemIDMaxLength);
                this.Log(LogLevel.Warning, $"OEM ID String Descriptor can consist of up to {OemIDMaxLength} characters. String was normalized.");
            }

            LogicalUnits = logicalUnits;
            LogicalBlockCount = blockCount;
            LogicalBlockSize = logicalBlockSize;

            MaxInBufferSize = MaxAllowedInBufferSize; // number of 512-byte units
            MaxOutBufferSize = MaxAllowedOutBufferSize; // number of 512-byte units

            // Device specific properties - values set by manufacturer
            IndexManufacturerName = 0;
            IndexProductName = 1;
            IndexSerialNumber = 2;
            IndexOemID = 3;
            IndexProductRevisionLevel = 4;

            ManufacturerName = manufacturerName;
            ProductName = productName;
            SerialNumber = serialNumber;
            OemID = oemID;
            ProductRevisionLevel = productRevisionLevel;

            dataBackends = new Stream[LogicalUnits];
            for(int i = 0; i < LogicalUnits; i++)
            {
                dataBackends[i] = DataStorage.Create(size: (long)logicalBlockSize * (long)blockCount);
            }
            InitConfiguration();
        }

        public void LoadFromFile(uint logicalUnitNumber, string file, bool persistent = false)
        {
            if(logicalUnitNumber >= LogicalUnits)
            {
                this.Log(LogLevel.Error, "Invalid logical unit number");
                return;
            }
            dataBackends[logicalUnitNumber].Dispose();
            dataBackends[logicalUnitNumber] = DataStorage.Create(file, persistent: persistent);
        }

        public void Dispose()
        {
            for(int i = 0; i < LogicalUnits; i++)
            {
                dataBackends[i].Dispose();
            }
        }

        public void Reset()
        {
            for(int i = 0; i < LogicalUnits; i++)
            {
                dataBackends[i].Position = 0;
            }
            InitConfiguration();
        }

        public byte[] HandleRequest(byte[] requestData, byte[] dataOutTransfer, out byte[] dataInTransfer)
        {
            dataInTransfer = null;
            var basicUPIUHeader = Packet.Decode<BasicUPIUHeader>(requestData);
            var transactionCode = (UPIUTransactionCodeInitiatorToTarget)basicUPIUHeader.TransactionCode;
            switch(transactionCode)
            {
                case UPIUTransactionCodeInitiatorToTarget.NopOut:
                {
                    return HandleNopOutRequest(requestData);
                }
                case UPIUTransactionCodeInitiatorToTarget.Command:
                {
                    return HandleCommandRequest(requestData, dataOutTransfer, out dataInTransfer);
                }
                case UPIUTransactionCodeInitiatorToTarget.TaskManagementRequest:
                {
                    return HandleTaskManagementRequest(requestData);
                }
                case UPIUTransactionCodeInitiatorToTarget.QueryRequest:
                {
                    return HandleQueryRequest(requestData);
                }
                default:
                    // Data Out UPIUs are handled as a part of whole request (dataOutTransfer)
                    this.Log(LogLevel.Warning, "Unhandled UFS UPIU with transaction code {0}", transactionCode);
                    return new byte[0];
            }
        }

        private byte[] HandleNopOutRequest(byte[] requestData)
        {
            var nopOut = Packet.Decode<NopOutUPIU>(requestData);
            var nopIn = HandleNopOut(nopOut);
            var responseData = Packet.Encode<NopInUPIU>(nopIn);
            return responseData;
        }

        private NopInUPIU HandleNopOut(NopOutUPIU request)
        {
            var nopIn = new NopInUPIU
            {
                DataSegmentsCRC = false,
                HeaderSegmentsCRC = false,
                TransactionCode = (byte)UPIUTransactionCodeTargetToInitiator.NopIn,
                Flags = 0,
                TaskTag = request.TaskTag,
                Response = (byte)UTPTransferStatus.Success,
                DataSegmentLength = 0,
            };
            return nopIn;
        }

        private byte[] HandleCommandRequest(byte[] requestData, byte[] dataOutTransfer, out byte[] dataInTransfer)
        {
            var command = Packet.Decode<CommandUPIU>(requestData);
            var responseData = HandleCommand(command, dataOutTransfer, out dataInTransfer);
            return responseData;
        }

        private byte[] HandleCommand(CommandUPIU request, byte[] dataOutTransfer, out byte[] dataInTransfer)
        {
            dataInTransfer = null;

            var lun = request.LogicalUnitNumber;
            var cdbBytes = request.CommandDescriptorBlock;
            var senseData = new byte[0];
            var status = SCSIStatus.Good;

            if(!unitDescriptors.ContainsKey(lun))
            {
                status = SCSIStatus.CheckCondition;
                this.Log(LogLevel.Warning, "Command UPIU: Invalid LUN");
            }
            else
            {
                HandleSCSICommand(lun, cdbBytes, dataOutTransfer, out dataInTransfer, out senseData);
            }

            var response = new ResponseUPIU
            {
                TransactionCode = (byte)UPIUTransactionCodeTargetToInitiator.Response,
                LogicalUnitNumber = request.LogicalUnitNumber,
                TaskTag = request.TaskTag,
                InitiatorID = request.InitiatorID,
                CommandSetType = request.CommandSetType,
                NexusInitiatorID = request.NexusInitiatorID,
                Response = (byte)UTPTransferStatus.Success,
                Status = (byte)status,
                TotalEHSLength = 0,
                DeviceInformation = 0,
                DataSegmentLength = (ushort)senseData.Length, // used for error reporting
                ResidualTransferCount = 0
            };

            var responseBytes = Packet.Encode(response);
            return responseBytes;
        }

        public void HandleSCSICommand(byte lun, byte[] cdb, byte[] dataOutTransfer, out byte[] dataInTransfer, out byte[] senseData)
        {
            senseData = new byte[0];
            dataInTransfer = null;
            var wellKnownLUN = IsWellKnownLU(lun);
            var opcode = (SCSICommand)cdb[0];
            switch(opcode)
            {
                case SCSICommand.Inquiry:
                {
                    HandleSCSIInquiry(lun, cdb, out dataInTransfer, out senseData);
                    break;
                }
                case SCSICommand.Read10:
                {
                    if(wellKnownLUN)
                    {
                        this.Log(LogLevel.Warning, "Ignoring Read (10) SCSI command to Well Known LUN");
                        break;
                    }
                    HandleSCSIRead10(lun, cdb, out dataInTransfer, out senseData);
                    break;
                }
                case SCSICommand.Write10:
                {
                    if(wellKnownLUN)
                    {
                        this.Log(LogLevel.Warning, "Ignoring Write (10) SCSI command to Well Known LUN");
                        break;
                    }
                    HandleSCSIWrite10(lun, cdb, dataOutTransfer, out senseData);
                    break;
                }
                case SCSICommand.TestUnitReady:
                {
                    this.Log(LogLevel.Debug, "Unit {0} is ready.", lun);
                    break;
                }
                case SCSICommand.ReadCapacity16:
                {
                    HandleSCSIReadCapacity16(lun, cdb, out dataInTransfer, out senseData);
                    break;
                }
                case SCSICommand.ReportLUNs:
                {
                    HandleSCSIReportLUNs(lun, cdb, out dataInTransfer, out senseData);
                    break;
                }
                default:
                {
                    this.Log(LogLevel.Warning, "Unhandled SCSI command: {0}", Enum.GetName(typeof(SCSICommand), opcode));
                    break;
                }
            }
        }

        private void HandleSCSIInquiry(byte lun, byte[] cdb, out byte[] dataInTransfer, out byte[] senseData)
        {
            // Error handling for SCSI subsystem is omitted, so sense data is not used yet
            senseData = new byte[0];
            dataInTransfer = new byte[0];
            var scsi = Packet.Decode<Inquiry>(cdb);

            if(scsi.EnableVitalProductData)
            {
                var vpdPage = new VitalProductDataPageHeader
                {
                    PeripheralQualifier = 0b000,
                    PeripheralDeviceType = IsWellKnownLU(lun) ? (byte)0x1e : (byte)0x00,
                    PageCode = scsi.PageCode,
                };

                switch(scsi.PageCode)
                {
                    case VitalProductDataPageCode.SupportedVPDPages:
                    {
                        vpdPage.PageLength = (ushort)SupportedVPDPages.Length;
                        var vpdPageHeader = Packet.Encode(vpdPage);
                        dataInTransfer = new byte[vpdPageHeader.Length + vpdPage.PageLength];
                        Array.Copy(vpdPageHeader, dataInTransfer, vpdPageHeader.Length);
                        Array.Copy(SupportedVPDPages, 0, dataInTransfer, vpdPageHeader.Length, SupportedVPDPages.Length);
                        break;
                    }
                    case VitalProductDataPageCode.ModePagePolicy:
                    {
                        var modePagePolicyDescriptor = new ModePagePolicyDescriptor
                        {
                            // Combination of the following PolicyPageCode and PolicySubpageCode means that descriptor
                            // applies to all mode pages and subpages not described by other mode page policy descriptors.
                            // Because ModePagePolicy is always set to Shared for UFS device, it allows to simplify returned structure.
                            PolicyPageCode = 0x3f,
                            PolicySubpageCode = 0xff,
                            ModePagePolicy = ModePagePolicy.Shared,
                            MultipleLogicalUnitsShare = false
                        };

                        var descriptor = Packet.Encode(modePagePolicyDescriptor);
                        vpdPage.PageLength = (ushort)descriptor.Length;
                        var vpdPageHeader = Packet.Encode(vpdPage);
                        dataInTransfer = new byte[vpdPageHeader.Length + vpdPage.PageLength];
                        Array.Copy(vpdPageHeader, dataInTransfer, vpdPageHeader.Length);
                        Array.Copy(descriptor, 0, dataInTransfer, vpdPageHeader.Length, descriptor.Length);
                        break;
                    }
                    default:
                    {
                        // Support for other VPD pages is optional in UFS device
                        this.Log(LogLevel.Warning, "Inquiry: vital product data was requested for page {0} but not supported", scsi.PageCode);
                        break;
                    }
                }
            }
            else if(!scsi.EnableVitalProductData && scsi.PageCode == 0)
            {
                var inquiryResponse = new StandardInquiryResponse
                {
                    PeripheralQualifier = 0b000,
                    PeripheralDeviceType = IsWellKnownLU(lun) ? (byte)0x1e : (byte)0x00,
                    RemovableMedium = false,
                    Version = 0x06,
                    ResponseDataFormat = 0b0010,
                    AdditionalLength = 31,
                    CommandQueue = true,
                    VendorIdentification = Encoding.ASCII.GetBytes(stringDescriptors[IndexManufacturerName]),
                    ProductIdentification = Encoding.ASCII.GetBytes(stringDescriptors[IndexProductName]),
                    ProductRevisionLevel = Encoding.ASCII.GetBytes(stringDescriptors[IndexProductRevisionLevel])
                };

                var inquiryResponseBytes = Packet.Encode(inquiryResponse);
                dataInTransfer = inquiryResponseBytes;
            }
            else
            {
                this.Log(LogLevel.Warning, "Invalid parameters for SCSI Inquiry command");
            }
        }

        private void HandleSCSIRead10(byte lun, byte[] cdb, out byte[] dataInTransfer, out byte[] senseData)
        {
            // Error handling for SCSI subsystem is omitted, so sense data is not used yet
            senseData = new byte[0];
            dataInTransfer = new byte[0];
            var scsi = Packet.Decode<Read10>(cdb);
            var bytesCount = scsi.TransferLength * LogicalBlockSize;
            var readPosition = scsi.LogicalBlockAddress * LogicalBlockSize;

            if(dataBackends[lun].Length <= (long)readPosition || dataBackends[lun].Length < (long)(readPosition + bytesCount))
            {
                this.Log(LogLevel.Warning, "Trying to read invalid range of the disk.");
                return;
            }

            dataBackends[lun].Position = (long)readPosition;
            var dataSegment = dataBackends[lun].ReadBytes((int)bytesCount);
            this.Log(LogLevel.Debug, "Reading {0} bytes from address 0x{1:x}", bytesCount, readPosition);
            dataInTransfer = dataSegment;
        }

        private void HandleSCSIWrite10(byte lun, byte[] cdb, byte[] dataOutTransfer, out byte[] senseData)
        {
            // Error handling for SCSI subsystem is omitted, so sense data is not used yet
            senseData = new byte[0];
            var scsi = Packet.Decode<Write10>(cdb);
            var bytesCount = (int)(scsi.TransferLength * LogicalBlockSize);
            var writePosition = scsi.LogicalBlockAddress * LogicalBlockSize;

            var dataTransferLength = dataOutTransfer.Length;

            if(bytesCount != dataTransferLength)
            {
                this.Log(LogLevel.Warning, "SCSI command bytes count {0} is not equal to data transfer length {1}", bytesCount, dataTransferLength);
            }

            if(dataBackends[lun].Length <= (long)writePosition || dataBackends[lun].Length < (long)writePosition + dataTransferLength)
            {
                this.Log(LogLevel.Warning, "Writing beyond the disk is unsupported.");
                return;
            }

            dataBackends[lun].Position = (long)writePosition;
            dataBackends[lun].Write(dataOutTransfer, 0, dataTransferLength);
            this.Log(LogLevel.Debug, "Writing {0} bytes to address 0x{1:x}", dataTransferLength, writePosition);
        }

        private void HandleSCSIReadCapacity16(byte lun, byte[] cdb, out byte[] dataInTransfer, out byte[] senseData)
        {
            // Error handling for SCSI subsystem is omitted, so sense data is not used yet
            senseData = new byte[0];
            var scsi = Packet.Decode<ReadCapacity16>(cdb);

            var luDescriptor = unitDescriptors[lun];

            var readCapacity16Response = new ReadCapacity16ParameterData
            {
                ReturnedLogicalBlockAddress = luDescriptor.LogicalBlockCount - 1,
                LogicalBlockLengthInBytes = (uint)(1 << luDescriptor.LogicalBlockSize),
                ProtectionEnable = false,
                ProtectionType = 0,
                LogicalBlocksPerPhysicalBlockExponent = 0,
                ThinProvisioningEnable = luDescriptor.ProvisioningType != 0x00,
            };

            var readCapacity16ResponseBytes = Packet.Encode(readCapacity16Response);
            dataInTransfer = readCapacity16ResponseBytes;
        }

        private void HandleSCSIReportLUNs(byte lun, byte[] cdb, out byte[] dataInTransfer, out byte[] senseData)
        {
            // Error handling for SCSI subsystem is omitted, so sense data is not used yet
            senseData = new byte[0];
            var scsi = Packet.Decode<ReportLUNs>(cdb);

            var selectReport = (SelectReport)scsi.SelectReport;

            var lunListLength = 0;
            switch(selectReport)
            {
                case SelectReport.LogicalUnits:
                    lunListLength = 8 * LogicalUnits;
                    break;
                case SelectReport.WellKnownLogicalUnits:
                    lunListLength = 8 * WellKnownLUNsNumber;
                    break;
                case SelectReport.AllLogicalUnits:
                    lunListLength = 8 * LogicalUnits + 8 * WellKnownLUNsNumber;
                    break;
                default:
                    this.Log(LogLevel.Warning, "Reserved SELECT REPORT field.");
                    break;
            }

            // The first 4 bytes contain LUN list length itself and the next 4 bytes are reserved fields.
            // In total, the first 8 bytes are not counted towards LUN list length itself.
            var reportLUNSParameterData = new byte[lunListLength + 8];
            var lunListLengthBytes = lunListLength.AsRawBytes().Reverse().ToArray();
            Array.Copy(lunListLengthBytes, 0, reportLUNSParameterData, 0, lunListLengthBytes.Length);

            var lunOffset = 8;

            if(selectReport == SelectReport.LogicalUnits || selectReport == SelectReport.AllLogicalUnits)
            {
                for(int i = 0; i < LogicalUnits; i++)
                {
                    reportLUNSParameterData[lunOffset] = ReportLUNStandardLogicalUnitAddressing;
                    reportLUNSParameterData[lunOffset + 1] = (byte)i;
                    lunOffset += 8;
                }
            }

            if(selectReport == SelectReport.WellKnownLogicalUnits || selectReport == SelectReport.AllLogicalUnits)
            {
                var wellKnownLUs = Enum.GetValues(typeof(WellKnownLUNId)).Cast<byte>().ToArray();
                for(int i = 0; i < WellKnownLUNsNumber; i++)
                {
                    reportLUNSParameterData[lunOffset] = ReportLUNWellKnownLogicalUnitAddressing;
                    reportLUNSParameterData[lunOffset + 1] = wellKnownLUs[i];
                    lunOffset += 8;
                }
            }

            reportLUNSParameterData = reportLUNSParameterData.Take((int)scsi.AllocationLength).ToArray();
            dataInTransfer = reportLUNSParameterData;
        }

        private byte[] HandleTaskManagementRequest(byte[] requestData)
        {
            var command = Packet.Decode<TaskManagementRequestUPIU>(requestData);
            var response = HandleTaskManagement(command);
            var responseData = Packet.Encode<TaskManagementResponseUPIU>(response);
            return responseData;
        }

        private TaskManagementResponseUPIU HandleTaskManagement(TaskManagementRequestUPIU request)
        {
            // UFS Task Management Functions include Abort Task, Abort Task Set, Clear Task Set,
            // Logical Unit Reset, Query Task, Query Task Set and have meaning if there are
            // ongoing tasks in the task queue list. UFS is emulated in a synchronous way 
            // and all tasks finish immediately, so task management should never be needed.
            var response = new TaskManagementResponseUPIU
            {
                TransactionCode = (byte)UPIUTransactionCodeTargetToInitiator.TaskManagementResponse,
                HeaderSegmentsCRC = false,
                DataSegmentsCRC = false,
                InitiatorID = request.InitiatorID,
                NexusInitiatorID = request.NexusInitiatorID,
                Response = (byte)UTPTaskManagementStatus.Success, // Target Success
                TotalEHSLength = 0,
                DataSegmentLength = 0,
                OutputParameter1 = 0x00 // Task Management Function Complete
            };
            return response;
        }

        private byte[] HandleQueryRequest(byte[] requestData)
        {
            var command = Packet.Decode<QueryRequestUPIU>(requestData);
            var dataInSegmentOffset = command.HeaderSegmentsCRC ? BasicUPIULength + 4 : BasicUPIULength;
            var dataSegmentIn = requestData.Skip(dataInSegmentOffset).Take(command.DataSegmentLength).ToArray();

            var response = HandleQuery(command, dataSegmentIn, out var dataSegmentOut);
            var responseLength = 32 + (response.HeaderSegmentsCRC ? 4 : 0) + (response.DataSegmentLength > 0 ? dataSegmentOut.Length : 0) + (response.DataSegmentsCRC ? 4 : 0);
            var responseData = new byte[responseLength];

            var responseHeader = Packet.Encode<QueryResponseUPIU>(response);
            Array.Copy(responseHeader, responseData, responseHeader.Length);
            if(response.HeaderSegmentsCRC)
            {
                var e2eCRCHeader = GetE2ECRC(responseHeader);
                var crcBytes = e2eCRCHeader.AsRawBytes();
                Array.Copy(crcBytes, 0, responseData, 32, crcBytes.Length);
            }
            var dataSegmentOffset = command.HeaderSegmentsCRC ? 36 : 32;
            if(response.DataSegmentLength > 0 && dataSegmentOut.Length > 0)
            {
                Array.Copy(dataSegmentOut, 0, responseData, dataSegmentOffset, dataSegmentOut.Length);
                if(response.DataSegmentsCRC)
                {
                    var e2eCRCData = GetE2ECRC(dataSegmentOut);
                    var crcBytes = e2eCRCData.AsRawBytes();
                    Array.Copy(crcBytes, 0, responseData, dataSegmentOffset + response.DataSegmentLength, crcBytes.Length);
                }
            }
            return responseData;
        }

        private QueryResponseUPIU HandleQuery(QueryRequestUPIU request, byte[] dataSegmentIn, out byte[] dataSegmentOut)
        {
            dataSegmentOut = new byte[0];
            var transactionSpecificFieldsResponse = new byte[16];
            var transactionSpecificFieldsRequest = request.TransactionSpecificFields;
            var opcode = (QueryFunctionOpcode)transactionSpecificFieldsRequest[0];
            switch(opcode)
            {
                case QueryFunctionOpcode.Nop:
                {
                    transactionSpecificFieldsResponse = HandleQueryFunctionNop(transactionSpecificFieldsRequest);
                    break;
                }
                case QueryFunctionOpcode.ReadDescriptor:
                {
                    transactionSpecificFieldsResponse = HandleQueryFunctionReadDescriptor(transactionSpecificFieldsRequest, out dataSegmentOut);
                    break;
                }
                case QueryFunctionOpcode.WriteDescriptor:
                {
                    transactionSpecificFieldsResponse = HandleQueryFunctionWriteDescriptor(transactionSpecificFieldsRequest, dataSegmentIn);
                    break;
                }
                case QueryFunctionOpcode.ReadAttribute:
                {
                    transactionSpecificFieldsResponse = HandleQueryFunctionReadAttribute(transactionSpecificFieldsRequest);
                    break;
                }
                case QueryFunctionOpcode.WriteAttribute:
                {
                    transactionSpecificFieldsResponse = HandleQueryFunctionWriteAttribute(transactionSpecificFieldsRequest);
                    break;
                }
                case QueryFunctionOpcode.ReadFlag:
                case QueryFunctionOpcode.SetFlag:
                case QueryFunctionOpcode.ClearFlag:
                case QueryFunctionOpcode.ToggleFlag:
                {
                    transactionSpecificFieldsResponse = HandleQueryFunctionFlagOperation(transactionSpecificFieldsRequest, opcode);
                    break;
                }
                default:
                {
                    this.Log(LogLevel.Warning, "Invalid query function opcode");
                    break;
                }
            }

            // first four byte fields match the request
            transactionSpecificFieldsResponse[0] = transactionSpecificFieldsRequest[0];
            transactionSpecificFieldsResponse[1] = transactionSpecificFieldsRequest[1];
            transactionSpecificFieldsResponse[2] = transactionSpecificFieldsRequest[2];
            transactionSpecificFieldsResponse[3] = transactionSpecificFieldsRequest[3];

            var response = new QueryResponseUPIU
            {
                TransactionCode = (byte)UPIUTransactionCodeTargetToInitiator.QueryResponse,
                Flags = request.Flags,
                TaskTag = request.TaskTag,
                QueryFunction = request.QueryFunction,
                QueryResponse = IsValidQueryOpcode(request.QueryFunction, opcode) ? QueryResponseCode.Success : QueryResponseCode.InvalidOpcode,
                TotalEHSLength = 0x00,
                DeviceInformation = 0,
                DataSegmentLength = (ushort)dataSegmentOut.Length,
                TransactionSpecficFields = transactionSpecificFieldsResponse
            };
            return response;
        }

        private byte[] HandleQueryFunctionNop(byte[] request)
        {
            var response = new byte[16];
            return response;
        }

        private byte[] HandleQueryFunctionReadDescriptor(byte[] request, out byte[] dataSegmentOut)
        {
            var descrIdn = (DescriptorTypeIdentification)request[1];
            var index = request[2];
            var length = BitHelper.ToUInt16(request, 6, reverse: false);

            var data = new byte[0];
            switch(descrIdn)
            {
                case DescriptorTypeIdentification.Device:
                {
                    data = Packet.Encode(deviceDescriptor);
                    break;
                }
                case DescriptorTypeIdentification.Configuration:
                {
                    if(index < configurationDescriptors.Length)
                    {
                        data = Packet.Encode(configurationDescriptors[index]);
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "Requested configuration descriptor for invalid index.");
                    }
                    break;
                }
                case DescriptorTypeIdentification.Unit:
                {
                    if(unitDescriptors.Keys.Contains(index))
                    {
                        data = Packet.Encode(unitDescriptors[index]);
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "Requested unit descriptor for invalid index.");
                    }
                    break;
                }
                case DescriptorTypeIdentification.Interconnect:
                {
                    data = Packet.Encode(interconnectDescriptor);
                    break;
                }
                case DescriptorTypeIdentification.String:
                {
                    if(index <= MaxNumberOfStringDescriptors-1)
                    {
                        data = GetStringDescriptor(index);
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "Requested string descriptor for invalid index.");
                    }
                    break;
                }
                case DescriptorTypeIdentification.Geometry:
                {
                    data = Packet.Encode(geometryDescriptor);
                    break;
                }
                case DescriptorTypeIdentification.Power:
                {
                    data = Packet.Encode(powerParametersDescriptor);
                    break;
                }
                case DescriptorTypeIdentification.DeviceHealth:
                {
                    data = Packet.Encode(deviceHealthDescriptor);
                    break;
                }
                default:
                    break;
            }

            dataSegmentOut = data.Take(Math.Min((ushort)data.Length, length)).ToArray();
            var actualLength = (ushort)dataSegmentOut.Length;
            var actualLengthBytes = actualLength.AsRawBytes().Reverse().ToArray();

            var response = new byte[16];
            response[6] = actualLengthBytes[0]; // LENGTH MSB
            response[7] = actualLengthBytes[1]; // LENGTH LSB
            return response;
        }

        private byte[] HandleQueryFunctionWriteDescriptor(byte[] request, byte[] dataSegmentIn)
        {
            var response = new byte[16];
            // Current implementation doesn't allow to dynamically modify descriptors.
            // There is no partial writes so it's either 0 or entire descriptor.
            response[6] = 0; // LENGTH MSB
            response[7] = 0; // LENGTH LSB

            this.Log(LogLevel.Warning, "Write Descriptor function not implemented yet");
            return response;
        }

        private byte[] HandleQueryFunctionReadAttribute(byte[] request)
        {
            var attrIdn = (UFSDeviceAttribute)request[1];
            var index = request[2];
            var selector = request[3];

            if (attrIdn == UFSDeviceAttribute.DynCapNeeded || attrIdn == UFSDeviceAttribute.ContextConf)
            {
                this.Log(LogLevel.Warning, "Trying to read an array attribute {0} for logical unit {1} and selector {2}. Assuming the same value for all logical units.", Enum.GetName(typeof(UFSDeviceAttribute), attrIdn), index, selector);
            }

            ulong attrValue;
            if (!attributes.TryGetValue(attrIdn, out attrValue))
            {
                this.Log(LogLevel.Warning, "Cannot read attribute: {0}", attrIdn);
            }

            var valueBytes = attrValue.AsRawBytes().Reverse().ToArray();
            var response = new byte[16];
            Array.Copy(valueBytes, 0, response, 4, valueBytes.Length);
            return response;
        }

        private byte[] HandleQueryFunctionWriteAttribute(byte[] request)
        {
            var attrIdn = (UFSDeviceAttribute)request[1];
            var index = request[2];
            var selector = request[3];

            var attrValue = BitConverter.ToUInt64(request, 4);

            if(attrIdn == UFSDeviceAttribute.DynCapNeeded || attrIdn == UFSDeviceAttribute.ContextConf)
            {
                this.Log(LogLevel.Warning, "Trying to write to an array attribute {0} for logical unit {1} and selector {2}. Assuming the same value for all logical units.", Enum.GetName(typeof(UFSDeviceAttribute), attrIdn), index, selector);
            }

            if(attributes.ContainsKey(attrIdn))
            {
                attributes[attrIdn] = attrValue;
            }
            else
            {
                this.Log(LogLevel.Warning, "Cannot write attribute: {0}", attrIdn);
            }
            var response = request;
            return response;
        }

        private byte[] HandleQueryFunctionFlagOperation(byte[] request, QueryFunctionOpcode op)
        {
            var flagIdn = (UFSDeviceFlag)request[1];
            var flagValue = false;

            if(flags.ContainsKey(flagIdn))
            {
                switch(op)
                {
                    case QueryFunctionOpcode.ReadFlag:
                    {
                        flagValue = flags[flagIdn];
                        break;
                    }
                    case QueryFunctionOpcode.SetFlag:
                    {
                        flagValue = true;
                        flags[flagIdn] = flagValue;
                        if(flagIdn == UFSDeviceFlag.DeviceInit)
                        {
                            flags[flagIdn] = false;
                            this.Log(LogLevel.Debug, "Initialization complete. Setting fDeviceInit flag to false.");
                        }
                        break;
                    }
                    case QueryFunctionOpcode.ClearFlag:
                    {
                        flagValue = false;
                        flags[flagIdn] = flagValue;
                        break;
                    }
                    case QueryFunctionOpcode.ToggleFlag:
                    {
                        flags[flagIdn] = !flags[flagIdn];
                        flagValue = flags[flagIdn];
                        break;
                    }
                }
            }
            else
            {
                this.Log(LogLevel.Warning, "Flag is not available");
            }

            var response = new byte[16];
            response[11] = flagValue ? (byte)1 : (byte)0; // FLAG VALUE
            return response;
        }

        private bool IsValidQueryOpcode(QueryFunction function, QueryFunctionOpcode opcode)
        {
            if(function == QueryFunction.StandardReadRequest
                && (opcode == QueryFunctionOpcode.WriteDescriptor
                    || opcode == QueryFunctionOpcode.WriteAttribute
                    || opcode == QueryFunctionOpcode.SetFlag
                    || opcode == QueryFunctionOpcode.ClearFlag
                    || opcode == QueryFunctionOpcode.ToggleFlag))
            {
                return false;
            }
            if(function == QueryFunction.StandardWriteRequest
                && (opcode == QueryFunctionOpcode.ReadDescriptor
                    || opcode == QueryFunctionOpcode.ReadAttribute
                    || opcode == QueryFunctionOpcode.ReadFlag))
            {
                return false;
            }
            return true;
        }

        private uint GetE2ECRC(byte[] data)
        {
            this.Log(LogLevel.Warning, "End-to-end CRC is not supported in this version of the standard");
            return 0;
        }

        private void InitConfiguration()
        {
            InitDeviceDescriptor(
                iManufacturerName: IndexManufacturerName,
                iProductName: IndexProductName,
                iSerialNumber: IndexSerialNumber,
                iOemID: IndexOemID,
                iProductRevisionLevel: IndexProductRevisionLevel
            );
            InitConfigurationDescriptors();
            InitGeometryDescriptor(
                bMaxInBufferSize: MaxInBufferSize,
                bMaxOutBufferSize: MaxOutBufferSize,
                bMaxNumberLU: LogicalUnits <= 8 ? (byte)0x0 : (byte)0x1
            );
            InitUnitDescriptors(
                qPhyMemResourceCount: LogicalBlockCount,
                bLogicalBlockSize: LogicalBlockSizeExponentBase2
            );
            InitRPMBUnitDescriptor();
            InitPowerParametersDescriptor();
            InitInterconnectDescriptor();
            InitDeviceHealthDescriptor();
            InitStringDescriptors();
            InitFlags();
            InitAttributes(
                bMaxDataInSize: MaxInBufferSize,
                bMaxDataOutSize: MaxOutBufferSize
            );
        }

        private void InitDeviceDescriptor(
            byte bDeviceSubClass = 0x01, // Embedded Non-Bootable
            byte bBackgroundOpsTermLat = 0x00, // Latency undefined
            ushort wManufactureDate = 0x0810, // August 2010
            byte iManufacturerName = 0, // Index to the string which contains the Manufacturer Name
            byte iProductName = 1, // Index to the string which contains the Product Name
            byte iSerialNumber = 2, // Index to the string which contains the Serial Number
            byte iOemID = 3, // Index to the string which contains the OEM ID
            ushort wManufacturerID = 0x0000, // Manufacturer ID as defined in JEDEC JEP106, Standard Manufacturer’s Identification Code.
            byte bDeviceRTTCap = 8, // Maximum number of outstanding RTTs supported by device
            byte bUFSFeaturesSupport = 0b00000001, // UFS Features Support, Bit 0 shall be set to one
            byte bFFUTimeout = 0, // No Timeout
            byte bQueueDepth = 0, // The device implements the per-LU queueing architecture
            ushort wDeviceVersion = 0x0000, // Device Version
            byte bNumSecureWPArea = 32, // Number of Secure Write Protect Areas
            uint dPSAMaxDataSize = 0x01, // PSA Maximum Data Size, 4Kbyte
            byte bPSAStateTimeout = 0x00, // PSA State Timeout, undefined
            byte iProductRevisionLevel = 4, // Index to the string which contains the Product Revision Level
            uint dExtendedUFSFeaturesSupport = 0b00000001) // shall be the same as bUFSFeaturesSupport
        {
            deviceDescriptor = new DeviceDescriptor
            {
                Length = 0x59,
                DescriptorIDN = 0x00,
                Device = 0x00, // Device
                DeviceClass = 0x00, // Mass Storage
                DeviceSubClass = bDeviceSubClass,
                Protocol = 0x00, // SCSI
                NumberLU = 0x00, // NumberLU field value is calculated by the device based on bLUEnable field value in the Unit Descriptors. NumberLU does not include well known logical units
                NumberWLU = 0x04,
                BootEnable = 0x00,
                DescrAccessEn = 0x00,
                InitPowerMode = 0x01,
                HighPriorityLUN = 0x7f,
                SecureRemovalType = 0x00,
                SecurityLU = 0x01,
                BackgroundOpsTermLat = bBackgroundOpsTermLat,
                InitActiveICCLevel = 0x00,
                SpecVersion = 0x0400,
                ManufactureDate = wManufactureDate,
                ManufacturerName = iManufacturerName,
                ProductName = iProductName,
                SerialNumber = iSerialNumber,
                OemID = iOemID,
                ManufacturerID = wManufacturerID,
                UD0BaseOffset = 0x16,
                UDConfigPLength = 0x1a,
                DeviceRTTCap = bDeviceRTTCap,
                PeriodicRTCUpdate = 0x0000,
                UFSFeaturesSupport = bUFSFeaturesSupport,
                FFUTimeout = bFFUTimeout,
                QueueDepth = bQueueDepth,
                DeviceVersion = wDeviceVersion,
                NumSecureWPArea = bNumSecureWPArea,
                PSAMaxDataSize = dPSAMaxDataSize,
                PSAStateTimeout = bPSAStateTimeout,
                ProductRevisionLevel = iProductRevisionLevel,
                ExtendedUFSFeaturesSupport = dExtendedUFSFeaturesSupport,
                WriteBoosterBufferPreserveUserSpaceEn = 0x00,
                WriteBoosterBufferType = 0x00,
                NumSharedWriteBoosterBufferAllocUnits = 0x00
            };
        }

        private void InitConfigurationDescriptors()
        {
            // Configuration Descriptors are only partially supported, because model doesn't support dynamic modification of device configuration.
            // It should be extended together with support for Write Descriptor function.
            var numberOfConfDescr = 1 + (LogicalUnits - 1) / 8;
            configurationDescriptors = new ConfigurationDescriptorHeader[numberOfConfDescr];

            for(int i = 0; i < numberOfConfDescr; i++)
            {
                var confDescrHeader = new ConfigurationDescriptorHeader
                {
                    Length = 0xe6, // size of this descriptor
                    DescriptorIDN = 0x01,
                    ConfDescContinue = 0x00,
                    WriteBoosterBufferPreserveUserSpaceEn = 0x00,
                    WriteBoosterBufferType = 0x00,
                    NumSharedWriteBoosterBufferAllocUnits = 0x00,
                };
                configurationDescriptors[i] = confDescrHeader;
            }
        }

        private void InitRPMBUnitDescriptor(
            byte bLUQueueDepth = 0x00,
            byte bPSASensitive = 0x00,
            ulong qLogicalBlockCount = 512,
            byte bRPMBRegion0Size = 0x00)
        {
            rpmbUnitDescriptor = new RPMBUnitDescriptor
            {
                Length = 0x23,
                DescriptorIDN = 0x2,
                UnitIndex = 0xc4,
                LUEnable = 0x01,
                BootLunID = 0x00,
                LUWriteProtect = 0x00,
                LUQueueDepth = bLUQueueDepth,
                PSASensitive = bPSASensitive,
                MemoryType = 0x0f,
                RPMBRegionEnable = 0x00,
                LogicalBlockSize = 0x08,
                LogicalBlockCount = qLogicalBlockCount,
                RPMBRegion0Size = bRPMBRegion0Size,
                RPMBRegion1Size = 0x00,
                RPMBRegion2Size = 0x00,
                RPMBRegion3Size = 0x00,
                ProvisioningType = 0x00,
                PhyMemResourceCount = qLogicalBlockCount,
            };
        }

        private void InitUnitDescriptors(
            byte bLUQueueDepth = 0x00, // LU queue not available (shared queuing is used)
            byte bPSASensitive = 0x00, // LU is not sensitive to soldering
            ulong qPhyMemResourceCount = 8, // Physical Memory Resource Count in units of Logical Block Size
            byte bLargeUnitGranularity_M1 = 0,
            byte bLogicalBlockSize = 0x0c
        )
        {
            unitDescriptors = new Dictionary<byte, UnitDescriptor>(capacity: LogicalUnits + WellKnownLUNsNumber);
            for(int i = 0; i < LogicalUnits; i++)
            {
                var unitDescr = new UnitDescriptor
                {
                    Length = 0x2d,
                    DescriptorIDN = 0x02,
                    UnitIndex = (byte)i,
                    LUEnable = 0x00,
                    BootLunID = 0x00,
                    LUWriteProtect = 0x00,
                    LUQueueDepth = bLUQueueDepth,
                    PSASensitive = bPSASensitive,
                    MemoryType = 0x00,
                    DataReliability = 0x00,
                    LogicalBlockSize = bLogicalBlockSize,
                    LogicalBlockCount = qPhyMemResourceCount,
                    EraseBlockSize = 0x00,
                    ProvisioningType = 0x00,
                    PhyMemResourceCount = qPhyMemResourceCount,
                    ContextCapabilities = 0x00,
                    LargeUnitGranularity_M1 = bLargeUnitGranularity_M1,
                    LUNumWriteBoosterBufferAllocUnits = 0x00
                };
                unitDescriptors[(byte)i] = unitDescr;
            }

            // Add well known units description
            foreach(byte wlun in Enum.GetValues(typeof(WellKnownLUNId)))
            {
                var wellKnownUnitDescr = new UnitDescriptor
                {
                    Length = 0x2d,
                    DescriptorIDN = 0x02,
                    UnitIndex = wlun,
                    LUEnable = 0x00,
                    BootLunID = 0x00,
                    LUWriteProtect = 0x00,
                    LUQueueDepth = bLUQueueDepth,
                    PSASensitive = bPSASensitive,
                    MemoryType = 0x00,
                    DataReliability = 0x00,
                    LogicalBlockSize = bLogicalBlockSize,
                    LogicalBlockCount = qPhyMemResourceCount,
                    EraseBlockSize = 0x00,
                    ProvisioningType = 0x00,
                    PhyMemResourceCount = qPhyMemResourceCount,
                    ContextCapabilities = 0x00,
                    LargeUnitGranularity_M1 = bLargeUnitGranularity_M1,
                    LUNumWriteBoosterBufferAllocUnits = 0x00
                };

                // Set the highest bit to obtain well known logical unit identifier
                var lun = (byte)(0x80 | wlun);
                unitDescriptors[lun] = wellKnownUnitDescr;
            }
        }

        private void InitPowerParametersDescriptor()
        {
            powerParametersDescriptor = new PowerParametersDescriptor
            {
                Length = 0x62,
                DescriptorIDN = 0x08,
                ActiveICCLevelsVCC = new byte[32],
                ActiveICCLevelsVCCQ = new byte[32],
                ActiveICCLevelsVCCQ2 = new byte[32]
            };
        }

        private void InitInterconnectDescriptor()
        {
            interconnectDescriptor = new InterconnectDescriptor
            {
                Length = 0x06,
                DescriptorIDN = 0x04,
                BCDUniproVersion = 0x0200,
                BCDMphyVersion = 0x0500
            };
        }

        private void InitGeometryDescriptor(
            ulong qTotalRawDeviceCapacity = 1024, // Total Raw Device Capacity in unit of 512 bytes
            byte bMaxNumberLU = 0x00, // 0x00 = 8 logical units, 0x01 == 32 logical units
            uint dSegmentSize = 16, // Segment Size in unit of 512 bytes
            byte bAllocationUnitSize = 0, // Multiple of Allocation Units.
            byte bMinAddrBlockSize = 0x08, // Minimum allowed value -> 4 Kbyte, in unit of 512 bytes
            byte bOptimalReadBlockSize = 0, // Information Not Available
            byte bOptimalWriteBlockSize = 0x08, // Shall be equal to or greater than bMinAddrBlockSize
            byte bMaxInBufferSize = 0x08, // Minimum allowed value, in unit of 512 bytes
            byte bMaxOutBufferSize = 0x08, // Minimum allowed value, in unit of 512 bytes
            byte bRPMB_ReadWriteSize = 0, // Max RPMB frames in Security Protocol
            byte bDynamicCapacityResourcePolicy = 0x00, // Spare blocks resource management policy is per logical unit
            byte bDataOrdering = 0x00, // Out-of-order data transfer is not supported by the device
            byte bMaxContexIDNumber = 5, // Minimum number of supported contexts
            byte bSysDataTagUnitSize = 0x00, // Minimum value
            byte bSysDataTagResSize = 0, // Valid values are from 0 to 6
            byte bSupportedSecRTypes = 0b00000011, // Supported Secure Removal Types
            ushort wSupportedMemoryTypes = 0xff, // Supported Memory Types - all
            uint dSystemCodeMaxNAllocU = 0x08, // Max Number of Allocation Units for the System Code memory type
            ushort wSystemCodeCapAdjFac = 256*8,
            uint dNonPersistMaxNAllocU = 0x08,
            ushort wNonPersistCapAdjFac = 256*8,
            uint dEnhanced1MaxNAllocU = 0x08,
            ushort wEnhanced1CapAdjFac = 256*8,
            uint dEnhanced2MaxNAllocU = 0x08,
            ushort wEnhanced2CapAdjFac = 256*8,
            uint dEnhanced3MaxNAllocU = 0x08,
            ushort wEnhanced3CapAdjFac = 256*8,
            uint dEnhanced4MaxNAllocU = 0x08,
            ushort wEnhanced4CapAdjFac = 256*8,
            uint dOptimalLogicalBlockSize = 0x00,
            uint dWriteBoosterBufferMaxNAllocUnits = 0,
            byte bDeviceMaxWriteBoosterLUs = 1, // In JESD220F, the valid value of this field is 1.
            byte bWriteBoosterBufferCapAdjFac = 2, // MLC NAND
            byte bSupportedWriteBoosterBufferUserSpaceReductionTypes = 0x00, // WriteBooster Buffer can be configured only in user space reduction type
            byte bSupportedWriteBoosterBufferTypes = 0 //  LU based WriteBooster Buffer configuration
        )
        {
            geometryDescriptor = new GeometryDescriptor
            {
                Length = 0x57,
                DescriptorIDN = 0x07,
                MediaTechnology = 0x00,
                TotalRawDeviceCapacity = qTotalRawDeviceCapacity,
                MaxNumberLU = bMaxNumberLU,
                SegmentSize = dSegmentSize,
                AllocationUnitSize = bAllocationUnitSize,
                MinAddrBlockSize = bMinAddrBlockSize,
                OptimalReadBlockSize = bOptimalReadBlockSize,
                OptimalWriteBlockSize = bOptimalWriteBlockSize,
                MaxInBufferSize = bMaxInBufferSize,
                MaxOutBufferSize = bMaxOutBufferSize,
                RPMBReadWriteSize = bRPMB_ReadWriteSize,
                DynamicCapacityResourcePolicy = bDynamicCapacityResourcePolicy,
                DataOrdering = bDataOrdering,
                MaxContexIDNumber = bMaxContexIDNumber,
                SysDataTagUnitSize = bSysDataTagUnitSize,
                SysDataTagResSize = bSysDataTagResSize,
                SupportedSecRTypes = bSupportedSecRTypes,
                SupportedMemoryTypes = wSupportedMemoryTypes,
                SystemCodeMaxNAllocU = dSystemCodeMaxNAllocU,
                SystemCodeCapAdjFac = wSystemCodeCapAdjFac,
                NonPersistMaxNAllocU = dNonPersistMaxNAllocU,
                NonPersistCapAdjFac = wNonPersistCapAdjFac,
                Enhanced1MaxNAllocU = dEnhanced1MaxNAllocU,
                Enhanced1CapAdjFac = wEnhanced1CapAdjFac,
                Enhanced2MaxNAllocU = dEnhanced2MaxNAllocU,
                Enhanced2CapAdjFac = wEnhanced2CapAdjFac,
                Enhanced3MaxNAllocU = dEnhanced3MaxNAllocU,
                Enhanced3CapAdjFac = wEnhanced3CapAdjFac,
                Enhanced4MaxNAllocU = dEnhanced4MaxNAllocU,
                Enhanced4CapAdjFac = wEnhanced4CapAdjFac,
                OptimalLogicalBlockSize = dOptimalLogicalBlockSize,
                WriteBoosterBufferMaxNAllocUnits = dWriteBoosterBufferMaxNAllocUnits,
                DeviceMaxWriteBoosterLUs = bDeviceMaxWriteBoosterLUs,
                WriteBoosterBufferCapAdjFac = bWriteBoosterBufferCapAdjFac,
                SupportedWriteBoosterBufferUserSpaceReductionTypes = bSupportedWriteBoosterBufferUserSpaceReductionTypes,
                SupportedWriteBoosterBufferTypes = bSupportedWriteBoosterBufferTypes,
            };
        }

        private void InitDeviceHealthDescriptor(
            byte bPreEOLInfo = 0x00, // Not defined
            byte bDeviceLifeTimeEstA = 0x00, // Information not available
            byte bDeviceLifeTimeEstB = 0x00, // Information not available
            byte [] vendorPropInfo = null, // Reserved for Vendor Proprietary Health Report
            uint dRefreshTotalCount = 1, // Total Refresh Count
            uint dRefreshProgress = 100000 // Refresh Progress (100%)
        )
        {
            deviceHealthDescriptor = new DeviceHealthDescriptor
            {
                Length = 0x2d,
                DescriptorIDN = 0x09,
                PreEOLInfo = bPreEOLInfo,
                DeviceLifeTimeEstA = bDeviceLifeTimeEstA,
                DeviceLifeTimeEstB = bDeviceLifeTimeEstB,
                VendorPropInfo = vendorPropInfo ?? (new byte[32]),
                RefreshTotalCount = dRefreshTotalCount,
                RefreshProgress = dRefreshProgress
            };
        }

        private void InitStringDescriptors()
        {
            stringDescriptors = new string[MaxNumberOfStringDescriptors];
            stringDescriptors[IndexManufacturerName] = ManufacturerName; // 8 UNICODE characters
            stringDescriptors[IndexProductName] = ProductName; // 16 UNICODE characters
            stringDescriptors[IndexOemID] = OemID; // up to 126 UNICODE characters
            stringDescriptors[IndexSerialNumber] = SerialNumber; // up to 126 UNICODE characters
            stringDescriptors[IndexProductRevisionLevel] = ProductRevisionLevel; // 4 UNICODE characters
        }

        private void InitFlags()
        {
            flags = new Dictionary<UFSDeviceFlag, bool>()
            {
                { UFSDeviceFlag.DeviceInit, false },
                { UFSDeviceFlag.PermanentWPEn, false },
                { UFSDeviceFlag.PowerOnWPEn, false },
                { UFSDeviceFlag.BackgroundOpsEn, true },
                { UFSDeviceFlag.DeviceLifeSpanModeEn, false },
                { UFSDeviceFlag.PurgeEnable, false },
                { UFSDeviceFlag.RefreshEnable, false },
                { UFSDeviceFlag.PhyResourceRemoval, false },
                { UFSDeviceFlag.BusyRTC, false },
                { UFSDeviceFlag.PermanentlyDisableFwUpdate, false },
                { UFSDeviceFlag.WriteBoosterEn, false },
                { UFSDeviceFlag.WriteBoosterBufferFlushEn, false },
                { UFSDeviceFlag.WriteBoosterBufferFlushDuringHibernate, false }
            };
        }

        private void InitAttributes(
            byte bMaxDataInSize = 0x08,
            byte bMaxDataOutSize = 0x08
        )
        {
            attributes = new Dictionary<UFSDeviceAttribute, ulong>()
            {
                { UFSDeviceAttribute.BootLunEn, 0x00 },
                { UFSDeviceAttribute.CurrentPowerMode, 0x11 }, // Active mode
                { UFSDeviceAttribute.ActiveICCLevel, 0x00 },
                { UFSDeviceAttribute.OutOfOrderDataEn, 0x00 },
                { UFSDeviceAttribute.BackgroundOpStatus, 0x00 },
                { UFSDeviceAttribute.PurgeStatus, 0x00 },
                { UFSDeviceAttribute.MaxDataInSize, bMaxDataInSize }, // =bMaxInBufferSize
                { UFSDeviceAttribute.MaxDataOutSize, bMaxDataOutSize }, // =bMaxOutBufferSize
                { UFSDeviceAttribute.DynCapNeeded, 0x00000000 },
                { UFSDeviceAttribute.RefClkFreq, 0x03 }, // 52 MHz default
                { UFSDeviceAttribute.ConfigDescrLock, 0x00 },
                { UFSDeviceAttribute.MaxNumOfRTT, 0x02 },
                { UFSDeviceAttribute.ExceptionEventControl, 0x0000 },
                { UFSDeviceAttribute.ExceptionEventStatus, 0x0000 },
                { UFSDeviceAttribute.SecondsPassed, 0x00000000 },
                { UFSDeviceAttribute.ContextConf, 0x0000 },
                { UFSDeviceAttribute.DeviceFFUStatus, 0x00 },
                { UFSDeviceAttribute.PSAState, 0x00 },
                { UFSDeviceAttribute.PSADataSize, 0x00000000 },
                { UFSDeviceAttribute.RefClkGatingWaitTime, 0x00 },
                { UFSDeviceAttribute.DeviceCaseRoughTemperaure, 0x00 },
                { UFSDeviceAttribute.DeviceTooHighTempBoundary, 0x00 },
                { UFSDeviceAttribute.DeviceTooLowTempBoundary, 0x00 },
                { UFSDeviceAttribute.ThrottlingStatus, 0x00 },
                { UFSDeviceAttribute.WriteBoosterBufferFlushStatus, 0 },
                { UFSDeviceAttribute.AvailableWriteBoosterBufferSize, 0 },
                { UFSDeviceAttribute.WriteBoosterBufferLifeTimeEst, 0x00 },
                { UFSDeviceAttribute.CurrentWriteBoosterBufferSize, 0 },
                { UFSDeviceAttribute.EXTIIDEn, 0x00 },
                { UFSDeviceAttribute.HostHintCacheSize, 0x0000 },
                { UFSDeviceAttribute.RefreshStatus, 0x00 },
                { UFSDeviceAttribute.RefreshFreq, 0x00 },
                { UFSDeviceAttribute.RefreshUnit, 0x00 },
                { UFSDeviceAttribute.RefreshMethod, 0x00 },
                { UFSDeviceAttribute.Timestamp, 0x00 }
            };
        }

        private byte[] GetStringDescriptor(byte index)
        {
            UnicodeEncoding unicode = new UnicodeEncoding();
            var unicodeString = stringDescriptors[index];

            if(unicodeString == null)
            {
                var emptyStringDescriptor = new byte[2];
                emptyStringDescriptor[0] = 0;
                emptyStringDescriptor[0] = (byte)DescriptorTypeIdentification.String;

                return emptyStringDescriptor;
            }

            var unicodeBytes = unicode.GetBytes(unicodeString);
            var length = 2 + unicodeBytes.Length; // bLength (1 byte) + bDescriptorIDN (1 byte) + Unicode string length
            var fullDescriptor = new byte[length];
            fullDescriptor[0] = (byte)length;
            fullDescriptor[1] = (byte)DescriptorTypeIdentification.String;
            Array.Copy(unicodeBytes, 0, fullDescriptor, 2, unicodeBytes.Length);

            return fullDescriptor;
        }

        private bool IsWellKnownLU(byte lun)
        {
            return BitHelper.IsBitSet(lun, 7);
        }

        public static readonly VitalProductDataPageCode[] SupportedVPDPages = new VitalProductDataPageCode[]
        {
            VitalProductDataPageCode.SupportedVPDPages,
            VitalProductDataPageCode.ModePagePolicy
        };

        public int LogicalUnits { get; }
        public byte IndexManufacturerName { get; }
        public byte IndexProductName { get; }
        public byte IndexSerialNumber { get; }
        public byte IndexOemID { get; }
        public byte IndexProductRevisionLevel { get; }
        public string ManufacturerName { get; }
        public string ProductName { get; }
        public string SerialNumber { get; }
        public string OemID { get; }
        public string ProductRevisionLevel { get; }
        public byte MaxInBufferSize { get; }
        public byte MaxOutBufferSize { get; }
        public ulong LogicalBlockSize { get; }
        public ulong LogicalBlockCount { get; }
        public byte LogicalBlockSizeExponentBase2 => (byte)BitHelper.GetMostSignificantSetBitIndex(LogicalBlockSize);

        private DeviceDescriptor deviceDescriptor;
        private ConfigurationDescriptorHeader[] configurationDescriptors;
        private RPMBUnitDescriptor rpmbUnitDescriptor;
        private PowerParametersDescriptor powerParametersDescriptor;
        private InterconnectDescriptor interconnectDescriptor;
        private GeometryDescriptor geometryDescriptor;
        private DeviceHealthDescriptor deviceHealthDescriptor;
        private string[] stringDescriptors;
        private Dictionary<UFSDeviceFlag, bool> flags;
        private Dictionary<UFSDeviceAttribute, ulong> attributes;
        private Dictionary<byte, UnitDescriptor> unitDescriptors;
        private Stream[] dataBackends;

        private const int WellKnownLUNsNumber = 4;
        private const int MaxLogicalUnits = 32;
        private const int MinimumLogicalBlockSize = 4096;
        private const int MaxAllowedInBufferSize = 255;
        private const int MaxAllowedOutBufferSize = 255;
        private const int MaxNumberOfStringDescriptors = 256;
        private const int BasicUPIULength = 32; // UPIU length without CRC and data segment
        private const byte ReportLUNStandardLogicalUnitAddressing = 0b00000000; // Format used for standard logical unit addressing
        private const byte ReportLUNWellKnownLogicalUnitAddressing = 0b11000001; // Format used for well known logical unit addressing
        private const int ManufacturerNameLength = 8;
        private const int ProductNameLength = 16;
        private const int ProductRevisionLevelLength = 4;
        private const int OemIDMaxLength = 126;
        private const int SerialNumberMaxLength = 126;

        private enum SelectReport : byte
        {
            LogicalUnits = 0x00,
            WellKnownLogicalUnits = 0x01,
            AllLogicalUnits = 0x02
        }

        private enum WellKnownLUNId : byte
        {
            ReportLUNs = 0x01,
            UFSDevice = 0x50,
            Boot = 0x30,
            RPMB = 0x44
        }

        private enum UFSDeviceFlag : byte
        {
            Reserved0 = 0x00,
            DeviceInit = 0x01,
            PermanentWPEn = 0x02,
            PowerOnWPEn = 0x03,
            BackgroundOpsEn = 0x04,
            DeviceLifeSpanModeEn = 0x05,
            PurgeEnable = 0x06,
            RefreshEnable = 0x07,
            PhyResourceRemoval = 0x08,
            BusyRTC = 0x09,
            Reserved1 = 0x0a,
            PermanentlyDisableFwUpdate = 0x0b,
            Reserved2 = 0x0c,
            Reserved3 = 0x0d,
            WriteBoosterEn = 0x0e,
            WriteBoosterBufferFlushEn = 0x0f,
            WriteBoosterBufferFlushDuringHibernate = 0x10,
        }

        private enum UFSDeviceAttribute : byte
        {
            BootLunEn = 0x00,
            Reserved0 = 0x01,
            CurrentPowerMode = 0x02,
            ActiveICCLevel = 0x03,
            OutOfOrderDataEn = 0x04,
            BackgroundOpStatus = 0x05,
            PurgeStatus = 0x06,
            MaxDataInSize = 0x07,
            MaxDataOutSize = 0x08,
            DynCapNeeded = 0x09,
            RefClkFreq = 0x0a,
            ConfigDescrLock = 0x0b,
            MaxNumOfRTT = 0x0c,
            ExceptionEventControl = 0x0d,
            ExceptionEventStatus = 0x0e,
            SecondsPassed = 0x0f,
            ContextConf = 0x10,
            Obsolete = 0x11,
            Reserved1 = 0x12,
            Reserved2 = 0x13,
            DeviceFFUStatus = 0x14,
            PSAState = 0x15,
            PSADataSize = 0x16,
            RefClkGatingWaitTime = 0x17,
            DeviceCaseRoughTemperaure = 0x18,
            DeviceTooHighTempBoundary = 0x19,
            DeviceTooLowTempBoundary = 0x1a,
            ThrottlingStatus = 0x1b,
            WriteBoosterBufferFlushStatus = 0x1c,
            AvailableWriteBoosterBufferSize = 0x1d,
            WriteBoosterBufferLifeTimeEst = 0x1e,
            CurrentWriteBoosterBufferSize = 0x1f,
            EXTIIDEn = 0x2a,
            HostHintCacheSize = 0x2b,
            RefreshStatus = 0x2c,
            RefreshFreq = 0x2d,
            RefreshUnit = 0x2e,
            RefreshMethod = 0x2f,
            Timestamp = 0x30,
        }

        private enum DescriptorTypeIdentification : byte
        {
            Device = 0x0,
            Configuration = 0x01,
            Unit = 0x02,
            Interconnect = 0x04,
            String = 0x05,
            Geometry = 0x07,
            Power = 0x08,
            DeviceHealth = 0x09,
            ReservedFBOExtension = 0x0a,
        }
    }
}
