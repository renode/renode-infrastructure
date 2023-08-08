//
// Copyright (c) 2010-2020 Antmicro
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
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.PCI.BAR;
using Antmicro.Renode.Peripherals.PCI.Capabilities;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.ATAPI
{
    //This device implements Fixed Disk Interface ATAPI connected CDROM
    public class ATAPI : SimpleContainer<IAtapiPeripheral>, IBytePeripheral, IWordPeripheral, IKnownSize
    {
        public ATAPI(IMachine machine) : base(machine)
        {
            CreateRegisters();
        }

        public byte ReadByte(long offset)
        {
            var value = registers.Read(offset);
            return (byte)value;
        }

        public void WriteByte(long offset, byte value)
        {
            registers.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            var value = dataRegisters.Read(offset);
            return (ushort)value;
        }

        public void WriteWord(long offset, ushort value)
        {
            dataRegisters.Write(offset, value);
        }

        public override void Register(IAtapiPeripheral peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            if(registrationPoint.Address != 0 & registrationPoint.Address != 1)
            {
                throw new RegistrationException("ATAPI does not allow registration point different than 0 (master) or 1 (slave)");
            }
            base.Register(peripheral, registrationPoint);
        }

        public override void Reset()
        {
            dataRegisters.Reset();
            registers.Reset();

            cmdCount = 0;
            cmdLength = 0;
            packetTransmission = false;
            selectedDevice = null;
        }

        public long Size => 0x10;

        private void CreateRegisters()
        {
            var registerMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.ErrorWPC, new ByteRegister(this)
                    .WithTaggedFlag("Bad block detected", 7)
                    .WithTaggedFlag("Uncorrectable ECC error", 6)
                    .WithReservedBits(5, 1)
                    .WithTaggedFlag("ID found", 4)
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("Command completed", 2)
                    .WithTaggedFlag("Track 000 not found", 1)
                    .WithTaggedFlag("DAM not found", 0)
                },
                //Four following registers are used only for device enumeration purposes.
                //They must preserve values, otherwise the firmware will assume that selected device is not present.
                {(long)Registers.SectorCount, new ByteRegister(this)
                    .WithValueField(0, 8, name: "Sector count")
                },
                {(long)Registers.SectorNumber, new ByteRegister(this)
                    .WithValueField(0, 8, name: "Sector number")
                },
                {(long)Registers.CylinderLow, new ByteRegister(this)
                    .WithValueField(0, 8, name: "Cyliner Low")
                },
                {(long)Registers.CylinderHigh, new ByteRegister(this)
                    .WithValueField(0, 8, name: "Cylinder High")
                },
                {(long)Registers.DriveHead, new ByteRegister(this, 0xA0)
                    .WithValueField(0, 3, name: "Head select bits")
                    .WithFlag(4, writeCallback: (_, flag) => SelectDevice(flag ? Device.Slave : Device.Master), name: "Drive select")
                    .WithFlag(5, FieldMode.Read, name: "Fixed bit 1")
                    .WithFlag(6, FieldMode.Read, name: "Fixed bit 2")
                    .WithFlag(7, FieldMode.Read, name: "Fixed bit 3")
                },
                {(long)Registers.StatusCommand , new ByteRegister(this, 0x50)
                    .WithValueField(0, 8, writeCallback: (_, value) => ExecuteCommand((Command)value),
                        valueProviderCallback: _ =>
                        {
                            return (packetTransmission || (selectedDevice?.DataReady ?? false)) ?
                                    (uint)StatusCodes.DataRequest : 0;
                        }, name: "Command")
                },
            };

            var dataRegistersMap = new Dictionary<long, WordRegister>
            {
                {(long)Registers.DataRegister, new WordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, value) => WriteData((ushort)value),
                         valueProviderCallback: _ => ReadData(), name: "Data")
                },
            };

            registers = new ByteRegisterCollection(this, registerMap);
            dataRegisters = new WordRegisterCollection(this, dataRegistersMap);
        }

        private void SelectDevice(Device device)
        {
            lock(selectedDeviceLock)
            {
                if(!ChildCollection.TryGetValue((int)device, out selectedDevice))
                {
                    this.Log(LogLevel.Debug, "Selected non existing device");
                    selectedDevice = null;
                }
            }
        }

        private void GatherPacket(ushort value)
        {
            commandBytes[cmdCount] = (byte)value;
            commandBytes[cmdCount + 1] = (byte)(value >> 8);
            cmdCount += 2;

            if(cmdLength == 0)
            {
                //First word of packet - we need to aquire length in bytes
                var groupCode = BitHelper.GetValue(value, 5, 3);
                switch(groupCode)
                {
                    case 0x0:
                        cmdLength = 6;
                        break;
                    case 0x1:
                    case 0x2:
                        cmdLength = 10;
                        break;
                    case 0x5:
                        cmdLength = 12;
                        break;
                    default:
                        this.Log(LogLevel.Error, "Wrong 'Group Code' : {0:x}. Rest of the packet will be discarded", groupCode);
                        cmdCount = 0;
                        packetTransmission = false;
                        break;
                }
            }
            // Packet transmission always consist of 12 bytes, if a command is shorter - redundant bytes have value 0x0
            else if(cmdCount == 12)
            {
                //This is the end of packet
                this.Log(LogLevel.Debug, "Finished gathering command with OperationCode {0:x}", commandBytes[0]);

                var receivedCommand = commandBytes.Take(cmdLength).ToArray();
#if DEBUG_PACKETS
                this.Log(LogLevel.Noisy , "Received command packet: {0}", Misc.Stringify(receivedCommand, " "));
#endif
                lock(selectedDeviceLock)
                {
                    if(selectedDevice != null)
                    {
                        selectedDevice.HandleCommand(receivedCommand);
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "Trying to send command to unexisting device. Command will be ignored");
                    }
                }

                packetTransmission = false;
                cmdCount = 0;
                cmdLength = 0;
            }
        }

        private void ExecuteCommand(Command command)
        {
            switch(command) {
            case Command.IdentifyPacketDevice:
                //During device enumeration firmware will try to send commands to unexisting device, such tries should be ignored
                if(selectedDevice == null)
                {
                    this.Log(LogLevel.Debug, "Sending 'IdentifyPacketDevice' command to unexisting device.");
                }
                else
                {
                    selectedDevice.SendIdentifyResponse();
                }
                break;
            case Command.Packet:
                packetTransmission = true;
                break;
            default:
                this.Log(LogLevel.Error, "Received unhandled command : '{0}'", command);
                break;
            }
        }

        private ushort ReadData()
        {
            lock(selectedDeviceLock)
            {
                //During device enumeration firmware might try to read data from unexisting device, such tries should return 0
                if(selectedDevice != null)
                {
                    return selectedDevice.DequeueData();
                }
                else
                {
                    this.Log(LogLevel.Debug, "Reading data from unexisting device.");
                }
            }
            return 0;
        }

        private void WriteData(ushort value)
        {
            if(packetTransmission)
            {
                GatherPacket(value);
            }
            else
            {
                this.Log(LogLevel.Error, "DataWrite other then packet transmission");
            }
        }

        private bool packetTransmission;
        private ushort cmdCount;
        private ushort cmdLength;
        private readonly byte[] commandBytes = new byte[16];
        private IAtapiPeripheral selectedDevice;
        private object selectedDeviceLock = new object();

        private ByteRegisterCollection registers;
        private WordRegisterCollection dataRegisters;

        private enum Device
        {
            Master = 0,
            Slave = 1,
        }

        private enum Registers
        {
            DataRegister  = 0x0,
            ErrorWPC      = 0x1,
            SectorCount   = 0x2,
            SectorNumber  = 0x3,
            CylinderLow   = 0x4,
            CylinderHigh  = 0x5,
            DriveHead     = 0x6,
            StatusCommand = 0x7,
        }

        private enum StatusCodes
        {
            Busy         = 0x80,
            Ready        = 0x40,
            DeviceFault  = 0x20,
            WriteFault   = 0x20,
            SeekComplete = 0x10,
            Service      = 0x10,
            DataRequest  = 0x08,
            Corrested    = 0x04,
            Index        = 0x02,
            Error        = 0x01,
        }

        private enum Command
        {
            NOP                               = 0x00,
            DataSetManagemen                  = 0x06,
            DeviceRese                        = 0x08,
            RequestSenseDataExt               = 0x0B,
            Recalibrate1                      = 0x11,
            Recalibrate2                      = 0x12,
            Recalibrate3                      = 0x13,
            Recalibrate4                      = 0x14,
            Recalibrate5                      = 0x15,
            Recalibrate6                      = 0x16,
            Recalibrate7                      = 0x17,
            Recalibrate8                      = 0x18,
            Recalibrate9                      = 0x19,
            Recalibrate10                     = 0x1A,
            Recalibrate11                     = 0x1B,
            Recalibrate12                     = 0x1C,
            Recalibrate13                     = 0x1D,
            Recalibrate14                     = 0x1E,
            Recalibrate15                     = 0x1E,
            Recalibrate16                     = 0x1F,
            ReadSectorswithRetry              = 0x20,
            ReadSectorswithoutRetry           = 0x21,
            ReadLongwithRetry                 = 0x22,
            ReadLongwithoutRetry              = 0x23,
            ReadSectorsExt                    = 0x24,
            ReadDMAExt                        = 0x25,
            ReadDMAQueuedExt                  = 0x26,
            ReadNativeMaxAddressExt           = 0x27,
            ReadMultipleExt                   = 0x29,
            ReadStreamDMA                     = 0x2A,
            ReadStream                        = 0x2B,
            ReadLogExt                        = 0x2F,
            WriteSectorswithRetry             = 0x30,
            WriteSectorswithoutRetry          = 0x31,
            WriteLongwithRetry                = 0x32,
            WriteLongwithoutRetry             = 0x33,
            WriteSectorsExt                   = 0x34,
            WriteDMAExt                       = 0x35,
            WriteDMAQueuedExt                 = 0x36,
            SetNativeMaxAddressExt            = 0x37,
            WriteMultipleExt                  = 0x39,
            WriteStreamDMA                    = 0x3A,
            WriteStream                       = 0x3B,
            WriteVerify                       = 0x3C,
            WriteDMAFUAExt                    = 0x3D,
            WriteDMAQueuedFUAExt              = 0x3E,
            WriteLogExt                       = 0x3F,
            ReadVerifySectorswithRetry        = 0x40,
            ReadVerifySectorswithoutRetry     = 0x41,
            ReadVerifySectorsExt              = 0x42,
            WriteUncorrectableExt             = 0x45,
            ReadLogDMAExt                     = 0x47,
            FormatTrack                       = 0x50,
            ConfigureStream                   = 0x51,
            WriteLogDMA                       = 0x57,
            TrustedNonData                    = 0x5B,
            TrustedReceive                    = 0x5C,
            TrustedReceiveDMA                 = 0x5D,
            TrustedSend                       = 0x5E,
            TrustedSendDMA                    = 0x5F,
            ReadFPDMAQueued                   = 0x60,
            WriteFPDMAQueued                  = 0x61,
            SATAReserved                      = 0x62,
            NCQQueueManagement                = 0x63,
            SendFPDMAQueued                   = 0x64,
            ReceiveFPDMAQueued                = 0x65,
            Seek1                             = 0x70,
            Seek2                             = 0x71,
            Seek3                             = 0x72,
            Seek4                             = 0x73,
            Seek5                             = 0x74,
            Seek6                             = 0x75,
            Seek7                             = 0x76,
            SetDateTimeExt                    = 0x77,
            AccessibleMaxAddressConfiguration = 0x78,
            Seek8                             = 0x79,
            Seek9                             = 0x7A,
            Seek10                            = 0x7B,
            Seek11                            = 0x7C,
            Seek12                            = 0x7D,
            Seek13                            = 0x7E,
            Seek14                            = 0x7F,
            ExecuteDeviceDiagnostics          = 0x90,
            InitializeDriveParameters         = 0x91,
            DownloadMicrocode                 = 0x92,
            DownloadMicrocodeDMA              = 0x93,
            StandbyImmediate1                 = 0x94,
            IdleImmediate1                    = 0x95,
            Standby1                          = 0x96,
            Idle1                             = 0x97,
            CheckPowerMode1                   = 0x98,
            Sleep1                            = 0x99,
            Packet                            = 0xA0,
            IdentifyPacketDevice              = 0xA1,
            Service                           = 0xA2,
            SMART                             = 0xB0,
            DeviceConfiguration               = 0xB1,
            SanitizeDevice                    = 0xB4,
            NVCache                           = 0xB6,
            CFAEraseSectors                   = 0xC0,
            ReadMultiple                      = 0xC4,
            WriteMultiple                     = 0xC5,
            SetMultipleMode                   = 0xC6,
            ReadDMAQueued                     = 0xC7,
            ReadDMAwithRetry                  = 0xC8,
            ReadDMAwithoutRetry               = 0xC9,
            WriteDMAwithRetry                 = 0xCA,
            WriteDMAwithoutRetry              = 0xCB,
            WriteDMAQueued                    = 0xCC,
            WriteMultipleFUAExt               = 0xCE,
            CheckMediaCardType                = 0xD1,
            GetMediaStatus                    = 0xDA,
            AcknowledgeMediaChange            = 0xDB,
            BootPostBoot                      = 0xDC,
            BootPreBoot                       = 0xDD,
            MediaLock                         = 0xDE,
            MediaUnlock                       = 0xDF,
            StandbyImmediate2                 = 0xE0,
            IdleImmediate2                    = 0xE1,
            Standby2                          = 0xE2,
            Idle2                             = 0xE3,
            ReadBuffer                        = 0xE4,
            CheckPowerMode2                   = 0xE5,
            Sleep2                            = 0xE6,
            FlushCache                        = 0xE7,
            WriteBuffer                       = 0xE8,
            ReadBufferDMA                     = 0xE9,
            FlushCacheExt                     = 0xEA,
            WriteBufferDMA                    = 0xEB,
            IdentifyDevice                    = 0xEC,
            MediaEject                        = 0xED,
            IdentifyDeviceDMA                 = 0xEE,
            SetFeatures                       = 0xEF,
            SecuritySetPassword               = 0xF1,
            SecurityUnlock                    = 0xF2,
            SecurityErasePrepare              = 0xF3,
            SecurityEraseUnit                 = 0xF4,
            SecurityFreezeLock                = 0xF5,
            SecurityDisablePassword           = 0xF6,
            ReadNativeMaxAddress              = 0xF8,
            SetMaxAddress                     = 0xF9,
        }
    }
}

