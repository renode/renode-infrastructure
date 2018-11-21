//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Utilities;
using System.IO;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class MassStorage: IUSBPeripheral, IDisposable
    {
        public event Action <uint> SendInterrupt
        {
            add {}
            remove {}
        }

        public event Action <uint> SendPacket
        {
            add {}
            remove {}
        }

        byte[] controlPacket;
        uint addr;

        public uint GetAddress()
        {
            return addr;
        }

        public USBDeviceSpeed GetSpeed()
        {
            return USBDeviceSpeed.High;
        }

        public void Dispose()
        {
            lbaBackend.Dispose();
        }

        public MassStorage(int numberOfBlocks, int blockSize = 512)
        {
            lbaBackend = new LBABackend(numberOfBlocks, blockSize);
            Init();
        }

        public MassStorage(string underlyingFile, int? numberOfBlocks = null, int blockSize = 512, bool persistent = true)
        {
            lbaBackend = new LBABackend(underlyingFile, numberOfBlocks, blockSize, persistent);
            Init();

        }

        public string ImageFile
        {
            get
            {
                return lbaBackend.UnderlyingFile;
            }
            set
            {
                if(lbaBackend != null)
                {
                    lbaBackend.Dispose();
                }
                if(!File.Exists(value))
                {
                    if(lbaBackend.UnderlyingFile != null && File.Exists(lbaBackend.UnderlyingFile))
                    {
                        FileCopier.Copy(lbaBackend.UnderlyingFile, value);
                    }
                    else
                    {
                        lbaBackend = new LBABackend(value, lbaBackend.NumberOfBlocks, lbaBackend.BlockSize);
                        return;
                    }
                }
                lbaBackend = new LBABackend(value, lbaBackend.BlockSize);
            }
        }

        public void Reset()
        {
            //throw new NotImplementedException();
        }

        #region IUSBDevice implementation
        public byte[] ProcessClassGet(USBPacket packet, USBSetupPacket setupPacket)
        {
            byte request = setupPacket.request;
            switch((MassStorageRequestCode)request)
            {

            case MassStorageRequestCode.GetMaxLUN:
                controlPacket = new [] {MaxLun};
                return new [] {MaxLun};
            default:
                controlPacket = new byte[0];
                return new byte[0];
            }
        }

        public byte[] GetDescriptor(USBPacket packet, USBSetupPacket setupPacket)
        {
            var type = (DescriptorType)((setupPacket.value & 0xff00) >> 8);
            switch(type)
            {
            case DescriptorType.Device:
                controlPacket = deviceDescriptor.ToArray();
                break;
            case DescriptorType.Configuration:
                controlPacket = configurationDescriptor.ToArray();
                break;
            case DescriptorType.DeviceQualifier:
                controlPacket = deviceQualifierDescriptor.ToArray();
                break;
            case DescriptorType.InterfacePower:
                throw new NotImplementedException("Interface Power Descriptor is not yet implemented. Please contact AntMicro for further support.");
            case DescriptorType.OtherSpeedConfiguration:
                controlPacket = otherConfigurationDescriptor.ToArray();
                break;
            case DescriptorType.String:
                uint index = (uint)(setupPacket.value & 0xff);
                if(index == 0)
                {
                    stringDescriptor = new StringUSBDescriptor(1);
                    stringDescriptor.LangId[0] = EnglishLangId;
                }
                else
                {
                    stringDescriptor = new StringUSBDescriptor(stringValues[setupPacket.index][index]);
                }
                controlPacket = stringDescriptor.ToArray();
                break;
            default:
                this.Log(LogLevel.Warning, "Unsupported descriptor");
                return null;
            }
            return controlPacket;
        }

        public void WriteDataBulk(USBPacket packet)
        {
            if(packet.data != null && packet.bytesToTransfer != 31)
            {
                oData.AddRange(packet.data);
            }
            else if(packet.bytesToTransfer == 31 || oData.Count > 0)
            {
                byte[] data;
                var cbw = new CommandBlockWrapper();
                var cdb = new SCSI.CommandDescriptorBlock();
                if(packet.bytesToTransfer == 31)
                {
                    data = packet.data;
                }
                else
                {
                    data = oData.ToArray();
                    oData.Clear();
                }
                if(!cbw.Fill(data))
                {
                    if(writeFlag)
                    {
                        writeFlag = false;
                        lbaBackend.Write((int)writeCDB.LogicalBlockAddress, data, (int)writeCDB.TransferLength);
                        writeCSW.DataResidue -= (uint)(data.Length);
                        transmissionQueue.Enqueue(writeCSW.ToArray());

                    }
                    else
                    {
                        //throw new InvalidOperationException ("Corrupted Command Block Wrapper");
                        this.Log(LogLevel.Warning, "Corrupted Command Block Wrapper");
                    }
                }
                else
                {
                    ReceiveCommandBlockWrapper(cbw, cdb, data);
                }
            }
        }

        void ReceiveCommandBlockWrapper(CommandBlockWrapper cbw, SCSI.CommandDescriptorBlock cdb, byte[] data)
        {
            this.DebugLog("Received Command Block Wrapper");
            var csw = new CommandStatusWrapper();
            var cdbData = new byte[cbw.Length];
            Array.Copy(data, 15, cdbData, 0, cbw.Length);
            cdb.Fill(cdbData);
            switch((SCSI.CommandDescriptorBlock.GroupCode)cdb.OperationCode)
            {
            case SCSI.CommandDescriptorBlock.GroupCode.Inquiry:
                csw.Tag = cbw.Tag;
                csw.DataResidue = 0x00;
                csw.Status = 0x00;
                transmissionQueue.Enqueue(inquiry.ToArray());
                //enqueue inquiry data
                transmissionQueue.Enqueue(csw.ToArray());
                break;
            case SCSI.CommandDescriptorBlock.GroupCode.ModeSense:
                var msc = new SCSI.ModeSenseCommand();
                msc.Fill(cdbData);
                var retArr = new byte[192];
                retArr[0] = 0x03;
                //FIXME: probably it should return sth with more sense
                csw.Tag = cbw.Tag;
                csw.DataResidue = cbw.DataTransferLength - 0x03;
                csw.Status = 0x00;
                transmissionQueue.Enqueue(retArr);
                transmissionQueue.Enqueue(csw.ToArray());
                break;
            case SCSI.CommandDescriptorBlock.GroupCode.PreventAllowMediumRemoval:
                csw.Tag = cbw.Tag;
                csw.DataResidue = 0x00;
                csw.Status = 0x00;
                transmissionQueue.Enqueue(csw.ToArray());
                break;
            case SCSI.CommandDescriptorBlock.GroupCode.Read10:
                var dataRead = lbaBackend.Read((int)cdb.LogicalBlockAddress, (int)cdb.TransferLength);
                csw.Tag = cbw.Tag;
                csw.DataResidue = (uint)(cbw.DataTransferLength - dataRead.Length);
                csw.Status = 0x00;
                transmissionQueue.Enqueue(dataRead);
                transmissionQueue.Enqueue(csw.ToArray());
                break;
            case SCSI.CommandDescriptorBlock.GroupCode.Write10:
                writeFlag = true;
                //next write command could be data
                csw.Tag = cbw.Tag;
                csw.DataResidue = cbw.DataTransferLength;
                csw.Status = 0x00;
                writeCSW = csw;
                writeCDB = cdb;
                //transmissionQueue.Enqueue(csw.ToArray());
                break;
            case SCSI.CommandDescriptorBlock.GroupCode.ReadCapacity:
                var capData = new SCSI.CapacityDataStructure();
                capData.ReturnedLBA = (uint)lbaBackend.NumberOfBlocks - 1;
                capData.BlockLength = (uint)lbaBackend.BlockSize;
                csw.Tag = cbw.Tag;
                csw.DataResidue = 0x00;
                csw.Status = 0x00;
                transmissionQueue.Enqueue(capData.ToArray());
                transmissionQueue.Enqueue(csw.ToArray());
                break;
	    case SCSI.CommandDescriptorBlock.GroupCode.RequestSense:
		// TODO: this was copied from TestUnitReady. do a proper implementation
                csw.Tag = cbw.Tag;
                csw.DataResidue = 0x00;
                csw.Status = 0x00;
                transmissionQueue.Enqueue(csw.ToArray());
	    	break;
            case SCSI.CommandDescriptorBlock.GroupCode.TestUnitReady:
                csw.Tag = cbw.Tag;
                csw.DataResidue = 0x00;
                csw.Status = 0x00;
                transmissionQueue.Enqueue(csw.ToArray());
                break;
            default:
                this.Log(LogLevel.Warning, "Unsuported Command Code: 0x{0:X}", cdb.OperationCode);
                break;
            }
        }

        public void WriteDataControl(USBPacket packet)
        {

        }

        public byte[] WriteInterrupt(USBPacket packet)
        {
            return null;
        }

        byte[] currentIDataRegister;
        int currentIDataPointer;
        List<byte> oData;

        public byte[] GetDataBulk(USBPacket packet)
        {

            USBPacket pack;
            pack.data = null;
            pack.ep = 0;
            pack.bytesToTransfer = 0;
            if(oData.Count != 0)
            {
                WriteDataBulk(pack);
                oData.Clear();
            }
            if(transmissionQueue.Count > 0)
            {
                if(packet.bytesToTransfer > 0)
                {
                    var dataPacket = new byte[packet.bytesToTransfer];
                    if(currentIDataRegister == null)
                    {
                        currentIDataRegister = transmissionQueue.Dequeue();

                    }
                    Array.Copy(currentIDataRegister, currentIDataPointer, dataPacket, 0, (int)packet.bytesToTransfer);
                    currentIDataPointer += (int)packet.bytesToTransfer;
                    if(currentIDataPointer >= currentIDataRegister.Length)
                    {
                        currentIDataRegister = null;
                        currentIDataPointer = 0;
                    }

                    return dataPacket;
                }
                //TODO: Rly? A nie przypadkiem "Trying to read 0 bytes"?
                this.Log(LogLevel.Warning, "Trying to read from empty queue");
                return new byte[0];

            }
            return null;
        }

        public byte GetTransferStatus()
        {
            return 0;
        }

        public byte[] GetDataControl(USBPacket packet)
        {
            return controlPacket;
        }

        public void ProcessClassSet(USBPacket packet, USBSetupPacket setupPacket)
        {
            byte request = setupPacket.request;

            switch((MassStorageRequestCode)request)
            {
            case MassStorageRequestCode.MassStorageReset:
                this.DebugLog("Mass storage reset");
                break;
            default:
                this.Log(LogLevel.Warning, "Unknown Class Set Code ({0:X})", request);
                break;
            }
        }

        public void SetDataToggle(byte endpointNumber)
        {
            throw new NotImplementedException();
        }

        public void CleanDataToggle(byte endpointNumber)
        {
            throw new NotImplementedException();
        }

        public void ToggleDataToggle(byte endpointNumber)
        {
            throw new NotImplementedException();
        }

        public bool GetDataToggle(byte endpointNumber)
        {
            throw new NotImplementedException();
        }

        public void ClearFeature(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public byte[] GetConfiguration()
        {
            throw new NotImplementedException();
        }

        public byte[] GetInterface(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public byte[] GetStatus(USBPacket packet, USBSetupPacket setupPacket)
        {
            controlPacket = new byte[2];
            var recipient = (MessageRecipient)(setupPacket.requestType & 0x3);
            switch(recipient)
            {
            case MessageRecipient.Device:
                controlPacket[0] = (byte)(((configurationDescriptor.RemoteWakeup ? 1 : 0) << 1) | (configurationDescriptor.SelfPowered ? 1 : 0));
                break;
            case MessageRecipient.Endpoint:
                //TODO: endpoint halt status
                goto default;
            default:
                controlPacket[0] = 0;
                break;
            }
            return controlPacket;
        }

        public void SetAddress(uint address)
        {
            addr = address;
        }

        public void SetConfiguration(USBPacket packet, USBSetupPacket setupPacket)
        {
            // throw new NotImplementedException();
        }

        public void SetDescriptor(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public void SetFeature(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public void SetInterface(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public void SyncFrame(uint endpointId)
        {
            throw new NotImplementedException();
        }

        public void WriteData(byte[] data)
        {

        }

        public byte[] GetData()
        {

            return null;
        }

        public byte[] ProcessVendorGet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public void ProcessVendorSet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }
        #endregion

        void Init()
        {
            endpointDescriptor = new EndpointUSBDescriptor[NumberOfEndpoints];
            for(int i = 0; i < NumberOfEndpoints; i++)
            {
                endpointDescriptor[i] = new EndpointUSBDescriptor();
            }
            FillEndpointsDescriptors(endpointDescriptor);
            interfaceDescriptor[0].EndpointDescriptor = endpointDescriptor;
            configurationDescriptor.InterfaceDescriptor = interfaceDescriptor;
            inquiry.FillVendor("Generic ");
            inquiry.FillIdentification("STORAGE DEVICE  ");
            inquiry.FillRevision("0207");
            oData = new List<byte>();
        }

        #region Massage Data Structure

        private Queue<byte[]> transmissionQueue = new Queue<byte[]>();

        #endregion

        #region Device constans
        private const byte MaxLun = 0;
        private const byte NumberOfEndpoints = 2;
        private const ushort EnglishLangId = 0x09;

        #endregion

        #region Mass Storage data structures

        private class CommandBlockWrapper
        {

            public bool Fill(byte[] data)
            {
                if(data.Length != 31)
                {
                    return false;
                }

                this.Signature = BitConverter.ToUInt32(data, 0);

                if(this.Signature != ProperSignature)
                {
                    return false;
                }
                this.Tag = BitConverter.ToUInt32(data, 4);
                this.DataTransferLength = BitConverter.ToUInt32(data, 8);
                this.Flags = data[12];
                this.LogicalUnitNumber = (byte)(data[13] & (byte)(0x0fu));
                this.Length = (byte)(data[14] & (byte)(0x1fu));

                return true;
            }

            private const uint ProperSignature = 0x43425355;
            public uint Signature;
            public uint Tag;
            public uint DataTransferLength;
            public byte Flags;
            public byte LogicalUnitNumber;
            public byte Length;
        }

        private class CommandStatusWrapper
        {
            public byte[] ToArray()
            {
                arr[0] = (byte)(CommandStatusWrapperSignature & 0xFF);
                arr[1] = (byte)((CommandStatusWrapperSignature & 0xFF00) >> 8);
                arr[2] = (byte)((CommandStatusWrapperSignature & 0xFF0000) >> 16);
                arr[3] = (byte)((CommandStatusWrapperSignature & 0xFF000000) >> 24);

                arr[4] = (byte)(Tag & 0xFF);
                arr[5] = (byte)((Tag & 0xFF00) >> 8);
                arr[6] = (byte)((Tag & 0xFF0000) >> 16);
                arr[7] = (byte)((Tag & 0xFF000000) >> 24);

                arr[8] = (byte)(DataResidue & 0xFF);
                arr[9] = (byte)((DataResidue & 0xFF00) >> 8);
                arr[10] = (byte)((DataResidue & 0xFF0000) >> 16);
                arr[11] = (byte)((DataResidue & 0xFF000000) >> 24);

                arr[12] = Status;

                return arr;
            }
            private byte[] arr = new byte[13];
            private const uint CommandStatusWrapperSignature = 0x53425355;
            public uint Tag;
            public uint DataResidue;
            public byte Status;
        }

        #endregion

        #region USB descriptors

        private ConfigurationUSBDescriptor configurationDescriptor = new ConfigurationUSBDescriptor
        {
            ConfigurationIndex = 0,
            SelfPowered = false,
            NumberOfInterfaces = 1,
            RemoteWakeup = true,
            MaxPower = 250, //500mA
            ConfigurationValue = 1
        };
        private ConfigurationUSBDescriptor otherConfigurationDescriptor = new ConfigurationUSBDescriptor();
        private StringUSBDescriptor stringDescriptor;
        private StandardUSBDescriptor deviceDescriptor = new StandardUSBDescriptor
        {
            DeviceClass=0x00,//specified in interface descritor
            DeviceSubClass = 0x00,//specified in interface descritor
            USB = 0x0200,
            DeviceProtocol = 0x00,//specified in interface descritor
            MaxPacketSize = 64,
            VendorId = 0x05e3,
            ProductId = 0x0727,
            Device = 0x0207,
            ManufacturerIndex = 0,
            ProductIndex = 0,
            SerialNumberIndex = 0,
            NumberOfConfigurations = 1
        };
        private DeviceQualifierUSBDescriptor deviceQualifierDescriptor = new DeviceQualifierUSBDescriptor();
        private EndpointUSBDescriptor[] endpointDescriptor;
        private InterfaceUSBDescriptor[] interfaceDescriptor = new[]{new InterfaceUSBDescriptor
        {
            AlternateSetting = 0,
            InterfaceNumber = 0,
            NumberOfEndpoints = NumberOfEndpoints,
            InterfaceClass = 0x08, //vendor specific
            InterfaceProtocol = 0x50, //Bulk only
            InterfaceSubClass = 0x06, //SCSI transparent
            InterfaceIndex = 0
        }
        };
        private Dictionary<ushort, string[]> stringValues = new Dictionary<ushort, string[]>
        {
            {EnglishLangId, new string[]{
                    "",
                    "Mass Storage",
                    "0xALLMAN",
                    "Configuration",
                    "AntMicro"
                }}
        };

        private void FillEndpointsDescriptors(EndpointUSBDescriptor[] endpointDesc)
        {
            endpointDesc[0].EndpointNumber = 1;
            endpointDesc[0].InEnpoint = true;
            endpointDesc[0].TransferType = EndpointUSBDescriptor.TransferTypeEnum.Bulk;
            endpointDesc[0].MaxPacketSize = 512;
            endpointDesc[0].SynchronizationType = EndpointUSBDescriptor.SynchronizationTypeEnum.NoSynchronization;
            endpointDesc[0].UsageType = EndpointUSBDescriptor.UsageTypeEnum.Data;
            endpointDesc[0].Interval = 0;

            endpointDesc[1].EndpointNumber = 2;
            endpointDesc[1].InEnpoint = false;
            endpointDesc[1].TransferType = EndpointUSBDescriptor.TransferTypeEnum.Bulk;
            endpointDesc[1].MaxPacketSize = 512;
            endpointDesc[1].SynchronizationType = EndpointUSBDescriptor.SynchronizationTypeEnum.NoSynchronization;
            endpointDesc[1].UsageType = EndpointUSBDescriptor.UsageTypeEnum.Data;
            endpointDesc[1].Interval = 0;


        }

        private SCSI.StandardInquiryData inquiry = new SCSI.StandardInquiryData//all data sniffed from real device
        {

            PeripheralQualifier = (byte)SCSI.PeripheralQualifier.Connected,
            PeripheralDeviceType = (byte)SCSI.PeripheralDeviceType.DirectAccessBlockDevice,
            RMB = true,
            Version = (byte)SCSI.VersionCode.NotStandard,
            NormalACASupport = false,
            HierachicalSupport = false,
            ResponseDataFormat = 0x00,
            AdditionalLength = 0x29,
            SCCSupport = false,
            AccessControlsCoordintor = false,
            TargetPortGroupSupport = 0x00,
            ThirdPartyCopy = false,
            Protect = false,
            BasingQueuing = false,
            EnclosureServices = false,
            VS1 = false,
            MultiPort = false,
            MediumChanger = false,
            ADDR16 = false,
            WBUS16a = false,
            Sync = false,
            LinkedCommand = false,
            VS2 = false

        };

        private enum MassStorageRequestCode
        {
            AcceptDeviceSpecificCommand = 0x00,
            GetRequest = 0xFC,
            PutRequest = 0xFD,
            GetMaxLUN = 0xFE,
            MassStorageReset = 0xFE//Bulk Only
        }
     #endregion

     #region lba backend

        private LBABackend lbaBackend;
        private CommandStatusWrapper writeCSW;
        private SCSI.CommandDescriptorBlock writeCDB;
        private bool writeFlag;

     #endregion


    }

}
