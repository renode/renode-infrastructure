//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class SCSI
    {
        public SCSI ()
        {
        }

        public class CommandDescriptorBlock
        {
            public byte Size;
            public byte OperationCode;
            public byte MiscCDBInformation1;
            public uint LogicalBlockAddress;
            public byte MiscCDBInformation2;
            public byte ServiceAction;

            public uint TransferLength;
            public uint ParameterListLength;
            public uint AllocationLength;
            public byte Control;

            public enum GroupCode:byte
            {
                TestUnitReady = 0x00,
		RequestSense = 0x03,
                Inquiry = 0x12,
                ReadCapacity = 0x25,
                ModeSense = 0x1A,
                PreventAllowMediumRemoval = 0x1E,
                Read10 = 0x28,
                Write10 = 0x2A
            }

            public void Fill(byte[] data)
            {
                this.Size = (byte)data.Length;
                this.OperationCode = data[0];
                this.MiscCDBInformation1 = (byte)((data[1] & 0xE0u) >> 5);
                if(this.Size == 6)
                {
                    this.LogicalBlockAddress = (uint)((uint)((data[1] & 0x1fu) << 16) | (uint)(data[2] << 8) | data[3]);
                    this.TransferLength = data[4];
                    this.AllocationLength = data[4];
                    this.ParameterListLength = data[4];
                    this.Control = data[5];
                }
                else
                if(this.Size == 10)
                {
                    this.ServiceAction = (byte)(data[1] & 0xE0u);
                    this.LogicalBlockAddress = (uint)(data[2] << 24 | data[3] << 16 | data[4] << 8 | data[5]);
                    this.MiscCDBInformation2 = data[6];
                    this.TransferLength = (uint)((data[7] << 8) | data[8]);
                    this.AllocationLength = this.TransferLength;
                    this.ParameterListLength = this.TransferLength;
                    this.Control = data[9];
                }
                else
                if(this.Size == 12)
                {
                    this.ServiceAction = (byte)(data[1] & 0xE0u);
                    this.LogicalBlockAddress = (uint)(data[2] << 24 | data[3] << 16 | data[4] << 8 | data[5]);
                    this.TransferLength = (uint)(data[6] << 24 | data[7] << 16 | data[8] << 8 | data[9]);
                    this.AllocationLength = this.TransferLength;
                    this.ParameterListLength = this.TransferLength;
                    this.MiscCDBInformation2 = data[10];
                    this.Control = data[11];
                }
                else
                {
                    Logger.LogAs(this, LogLevel.Warning, "Unsupported Command Descriptor Block Length");
                }

            }
       }

       public class StandardInquiryData
        {
            public byte PeripheralQualifier;
            public byte PeripheralDeviceType;
            public bool RMB;
            public byte Version;
            public bool NormalACASupport;
            public bool HierachicalSupport;
            public byte ResponseDataFormat;
            public byte AdditionalLength;
            public bool SCCSupport;
            public bool AccessControlsCoordintor;
            public byte TargetPortGroupSupport;
            public bool ThirdPartyCopy;
            public bool Protect;
            public bool BasingQueuing;
            public bool EnclosureServices;
            public bool VS1;
            public bool MultiPort;
            public bool MediumChanger;
            public bool ADDR16;
            public bool WBUS16a;
            public bool Sync;
            public bool LinkedCommand;
            public bool CommandQueuing;
            public bool VS2;
            public byte[] VendorIdentificationT10 = new byte[8];
            public byte[] ProductIdentification = new byte[16];
            public byte[] ProductRevisionLevel = new byte[4];

            public void FillVendor(string vendorStr)
            {
                for(int i=0;i<8;i++)
                {
                    this.VendorIdentificationT10[i] = (byte)vendorStr[i];
                }
            }

            public void FillIdentification(string identStr)
            {
                for(int i=0;i<16;i++)
                {
                    this.ProductIdentification[i] = (byte)identStr[i];
                }
            }

            public void FillRevision(string revisionStr)
            {
                for(int i=0;i<4;i++)
                {
                    this.ProductRevisionLevel[i] = (byte)revisionStr[i];
                }
            }

            public byte[] ToArray()
            {
                arr[0] = (byte)(((this.PeripheralQualifier & 0x07)<<5) | (byte)(this.PeripheralDeviceType & 0x1fu));
                arr[1] = this.RMB ? (byte) (1u<<7): (byte) 0;
                arr[2] = this.Version;
                arr[3] = (byte)((this.NormalACASupport ? (byte) (1u<<5) : (byte) 0u) | (this.HierachicalSupport ? (byte) (1u<<4) : (byte) 0u) | (byte)(this.ResponseDataFormat & 0x0fu));
                arr[4] = this.AdditionalLength;
                arr[5] = (byte)( (byte)(this.SCCSupport ? 1u<<7 : 0u) | (byte)(this.AccessControlsCoordintor ? 1u<<6 : 0u) | (byte)((this.TargetPortGroupSupport & 0x03u) << 4));
                arr[5]|= (byte)( (byte)(this.ThirdPartyCopy ? 1u<<3 : 0u) | (byte)(this.Protect ? 1u<<0 : 0u) );
                arr[6] = (byte)( (byte)(this.BasingQueuing ? 1u<<7 : 0u) | (byte)(this.EnclosureServices ? 1u<<6 : 0u) | (byte)(this.VS1 ? 1u<<5 : 0u) | (byte)(this.MultiPort ? 1u<<7 : 0u));
                arr[6]|= (byte)( (byte)(this.MediumChanger ? 1u<<3 : 0u) | (byte)(this.ADDR16 ? 1u<<0 : 0u));
                arr[7] = (byte)( (byte)(this.WBUS16a ? 1u<<5 : 0u) | (byte)(this.Sync ? 1u<<4 : 0u) | (byte)(this.CommandQueuing ? 1u<<1 : 0u) | (byte)(this.VS2 ? 1u<<0 : 0u));
                Array.Copy(this.VendorIdentificationT10, 0, arr, 8, this.VendorIdentificationT10.Length);
                Array.Copy(this.ProductIdentification, 0, arr, 16, this.ProductIdentification.Length);
                Array.Copy(this.ProductRevisionLevel, 0, arr, 32, this.ProductRevisionLevel.Length);

                return arr;
            }

            private byte[] arr = new byte[36];

        }

        public class CapacityDataStructure
        {
            public uint ReturnedLBA;
            public uint BlockLength;
            private byte[] arr = new byte[8];
            public byte[] ToArray()
            {
                arr[0] = (byte) ((ReturnedLBA & 0xff000000) >> 24);
                arr[1] = (byte) ((ReturnedLBA & 0x00ff0000) >> 16);
                arr[2] = (byte) ((ReturnedLBA & 0x0000ff00) >> 8);
                arr[3] = (byte) ((ReturnedLBA & 0x000000ff) >> 0);

                arr[4] = (byte) ((BlockLength & 0xff000000) >> 24);
                arr[5] = (byte) ((BlockLength & 0x00ff0000) >> 16);
                arr[6] = (byte) ((BlockLength & 0x0000ff00) >> 8);
                arr[7] = (byte) ((BlockLength & 0x000000ff) >> 0);

                return arr;
            }
        }

        public class ModeSenseCommand
        {
            public byte OperationCode;
            public bool DisableBlockDescriptors;
            public byte PageControl;
            public byte PageCode;
            public byte SubpageCode;
            public byte AllocationLength;
            public byte Control;

            public void Fill(byte[] data)
            {
                this.OperationCode = data[0];
                this.DisableBlockDescriptors = ((data[1] & 1u<<3)!=0) ? true:false;
                this.PageCode = (byte)((data[2]&0xC0) >> 6);
                this.SubpageCode = data[3];
                this.AllocationLength = data[4];
                this.Control = data[5];
            }
        }

        public enum PeripheralQualifier:byte
        {
            Connected = 0x0,
            Disconnected = 0x01,
            NotSuported = 0x3
        }

        public enum PeripheralDeviceType:byte
        {
            DirectAccessBlockDevice = 0x00,
            SequentialAccessBlockDevice = 0x01,
            PrinterDevice = 0x02,
            ProcessorDevice = 0x03,
            WriteOnceDevice = 0x04,
            CDDVDDevice = 0x05,
            ScannerDevice = 0x06,
            OpticalDevice = 0x07,
            MediumChangerDevice = 0x08,
            CommunicationsDevice = 0x09,
            StorageArrayControllerDevice = 0x0C,
            EnclosureServicesDevice = 0x0D,
            SimplifiedDirectAccessDevice = 0x0E,
            OpticalCardReaderDevice = 0x0F,
            BridgeControllerCommands = 0x10,
            ObjectBasedStorageDevice = 0x11,
            AutomationDriveInterface = 0x12,
            WellKnownLogicalUnit = 0x1E,
            UnknownOrNoDevice = 0x1F
        }

        public enum VersionCode
        {
            NotStandard = 0x00,
            ANSISPC = 0x03,
            ANSISPC2 = 0x04,
            Standard = 0x05
        }

        public enum TargetGroupPortSupportCode:byte
        {
            AsimetricLogicalUnitAccesNotSupported = 0x00,
            ImplicitAsimetricLogicalUnitAccessOnly = 0x01,
            ExplicitAsimetricLogicalUnitAccessOnly = 0x02,
            BothAsimetricLogicalUnitAccess = 0x03,
        }

    }
}

