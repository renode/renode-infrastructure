//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Network;
using System.Collections.Generic;
using Antmicro.Renode.Network;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class SMSC9500 : IUSBPeripheral, IMACInterface
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

        public SMSC9500()
        {
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            for(int i=0; i<NumberOfEndpoints; i++)
            {
                dataToggleBit[i] = false;
            }
            endpointDescriptor = new EndpointUSBDescriptor[NumberOfEndpoints];
            for(int i=0; i<NumberOfEndpoints; i++)
            {
                endpointDescriptor[i] = new EndpointUSBDescriptor();
            }
            fillEndpointsDescriptors(endpointDescriptor);
            interfaceDescriptor[0].EndpointDescriptor = endpointDescriptor;
            configurationDescriptor.InterfaceDescriptor = interfaceDescriptor;

            rxPacketQueue = new Queue<EthernetFrame>();
        }

        public byte[] GetData()
        {
            return null;
        }

        uint addr;

        public uint GetAddress()
        {
            return addr;
        }

        public USBDeviceSpeed GetSpeed()
        {
            return USBDeviceSpeed.High;
        }
        #region IUSBDevice implementation
        public byte[] ProcessClassGet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        bool linkUp = false;

        public byte[] WriteInterrupt(USBPacket packet)
        {
            if(linkUp == false)
            {
                linkUp = true;
                packet.data = new byte[4];
                packet.data[0] = 0x00;
                packet.data[1] = 0x80;
                packet.data[2] = 0x00;
                packet.data[3] = 0x00;
                return packet.data;
            }
            else
                return null;
        }

        public     byte GetTransferStatus()
        {
            return 0;
        }

        byte[] controlPacket;

        public byte[] GetDataControl(USBPacket packet)
        {
            return controlPacket;
        }

        public byte[] GetDescriptor(USBPacket packet, USBSetupPacket setupPacket)
        {
            DescriptorType type;
            type = (DescriptorType)((setupPacket.value & 0xff00) >> 8);
            uint index = (uint)(setupPacket.value & 0xff);
            switch(type)
            {
            case DescriptorType.Device:
                controlPacket = new byte[deviceDescriptor.ToArray().Length];
                deviceDescriptor.ToArray().CopyTo(controlPacket, 0);
                return deviceDescriptor.ToArray();
            case DescriptorType.Configuration:
                controlPacket = new byte[configurationDescriptor.ToArray().Length];
                configurationDescriptor.ToArray().CopyTo(controlPacket, 0);
                return configurationDescriptor.ToArray();
            case DescriptorType.DeviceQualifier:
                controlPacket = new byte[deviceQualifierDescriptor.ToArray().Length];
                deviceQualifierDescriptor.ToArray().CopyTo(controlPacket, 0);
                return deviceQualifierDescriptor.ToArray();
            case DescriptorType.InterfacePower:
                throw new NotImplementedException("Interface Power Descriptor is not yet implemented. Please contact AntMicro for further support.");
            case DescriptorType.OtherSpeedConfiguration:
                controlPacket = new byte[otherConfigurationDescriptor.ToArray().Length];
                otherConfigurationDescriptor.ToArray().CopyTo(controlPacket, 0);
                return otherConfigurationDescriptor.ToArray();
            case DescriptorType.String:
                if(index == 0)
                {
                    stringDescriptor = new StringUSBDescriptor(1);
                    stringDescriptor.LangId[0] = EnglishLangId;
                }
                else
                {
                    if(index < stringValues.Count)
                    {
                        stringDescriptor = new StringUSBDescriptor(stringValues[setupPacket.index][index]);
                    }
                    else
                    {
                        stringDescriptor = new StringUSBDescriptor("");
                    }

                }
                controlPacket = new byte[stringDescriptor.ToArray().Length];
                stringDescriptor.ToArray().CopyTo(controlPacket, 0);
                return stringDescriptor.ToArray();
            default:
                return null;
            }
        }

        public void WriteDataControl(USBPacket packet)
        {
        }

        public void ProcessClassSet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public void SetDataToggle(byte endpointNumber)
        {
            dataToggleBit[endpointNumber] = true;
        }

        public void Reset()
        {
            linkUp = false;
        }

        public void CleanDataToggle(byte endpointNumber)
        {
            dataToggleBit[endpointNumber] = false;
        }

        public void ToggleDataToggle(byte endpointNumber)
        {
            dataToggleBit[endpointNumber] = !dataToggleBit[endpointNumber];
        }

        public bool GetDataToggle(byte endpointNumber)
        {
            return dataToggleBit[endpointNumber];
        }

        public void ClearFeature(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public byte[] GetConfiguration()
        {
            throw new System.NotImplementedException();
        }

        public byte[] GetInterface(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public byte[] GetStatus(USBPacket packet, USBSetupPacket setupPacket)
        {
            var arr = new byte[2];
            MessageRecipient recipient = (MessageRecipient)(setupPacket.requestType & 0x3);
            switch(recipient)
            {
            case MessageRecipient.Device:
                arr[0] = (byte)(((configurationDescriptor.RemoteWakeup ? 1 : 0) << 1) | (configurationDescriptor.SelfPowered ? 1 : 0));
                break;
            case MessageRecipient.Endpoint:
                //TODO: endpoint halt status
                goto default;
            default:
                arr[0] = 0;
                break;
            }
            controlPacket = new byte[arr.Length];
            arr.CopyTo(controlPacket, 0);
            return arr;
        }

        public void SetAddress(uint address)
        {
            //this.address = address;
            addr = address;
            this.Log(LogLevel.Info, "Device addres set to 0x{0:X}", address);
        }

        public void SetConfiguration(USBPacket packet, USBSetupPacket setupPacket)
        {

        }

        public void SetDescriptor(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public void SetFeature(USBPacket packet, USBSetupPacket setupPacket)
        {

        }

        public void SetInterface(USBPacket packet, USBSetupPacket setupPacket)
        {

        }

        public void SyncFrame(uint endpointId)
        {
            throw new System.NotImplementedException();
        }
        private ushort CalculateChecksumTX(byte[] data)
        {
            ulong sum = 0;
            int size = data.Length;
            uint i = 0;
            ushort addVal;
            while(size > 1)
            {
                addVal = (ushort)((data[i] << 8) | data[i + 1]);
                sum += addVal;
                i += 2;
                size -= 2;
            }
            if(size != 0) //if odd length
                sum += (ushort)((data[i] << 8) | 0x00);


            while((sum >>16) != 0)
            {
                sum = (sum >> 16) + (sum & 0xffff);
            }
            return (ushort)((~sum) + 1);
        }
        public void WriteDataBulk(USBPacket packet)
        {
            if(packet.data == null)
                return;

            byte[] packetToSend;
            if(packet.data[5] != 64)
            {
                packetToSend = new byte[packet.data.Length - 8];
                Array.Copy(packet.data, 8, packetToSend, 0, packetToSend.Length);
            }
            else
            {
                packetToSend = new byte[packet.data.Length - 12];
                Array.Copy(packet.data, 12, packetToSend, 0, packetToSend.Length);

                if((packetToSend[14] & 0xF0) == 0x40) //IP packet
                {
                    ushort cksum;
                    IPHeaderLength = (ushort)((packetToSend[14] & 0x0F) * 4);
                    if(packetToSend[23] == 0x06) // TCP packet
                    {

                        IPpacket tcpPacket = new IPpacket(IPHeaderLength, IPpacket.PacketType.TCP);
                        tcpPacket.ReadFromBuffer(packetToSend);
                        cksum = tcpPacket.GetChecksum();
                        cksum -= 1;
                        packetToSend[MACHeaderLegth + IPHeaderLength + 16] = (byte)((cksum >> 8) & 0xFF);
                        packetToSend[MACHeaderLegth + IPHeaderLength + 17] = (byte)((cksum) & 0xFF);
                    }
                    else if(packetToSend[23] == 0x11) // UDP packet
                    {
                        IPpacket udpPacket = new IPpacket(IPHeaderLength, IPpacket.PacketType.UDP);
                        udpPacket.ReadFromBuffer(packetToSend);
                        cksum = udpPacket.GetChecksum();
                        cksum -= 1;
                        packetToSend[MACHeaderLegth + IPHeaderLength + 6] = (byte)((cksum >> 8) & 0xFF);
                        packetToSend[MACHeaderLegth + IPHeaderLength + 7] = (byte)((cksum) & 0xFF);
                    }
                }

            }

            if(Misc.TryCreateFrameOrLogWarning(this, packetToSend, out var frame, addCrc: true))
            {
                FrameReady?.Invoke(frame);
            }
        }

        private ushort CalculateChecksumRX(byte[] data)
        {
            ulong sum = 0;
            ulong part = 0;
            int size = data.Length;
            uint i = 0;
            while(size > 1)
            {
                part = (ulong)((data[i + 1] * 256) + data[i]);
                sum += ((part & 0xFFFFu) + (part >> 16));
                i += 2;
                size -= 2;
            }
            if(size != 0) //if odd length
                sum += (ushort)(data[size - 1]);

            return (ushort)sum;
        }

        private readonly Queue<EthernetFrame> rxPacketQueue;

        public byte[] GetDataBulk(USBPacket packet)
        {
            lock(sync)
            {
                if(packet.bytesToTransfer > 0)
                if(rxPacketQueue.Count > 0)
                {

                    EthernetFrame receivedFrame = rxPacketQueue.Dequeue();

                    //byte frameBytes []= rxFifo.Dequeue();
                    var size = receivedFrame.Bytes.Length;
                    uint packetSize;
                    //  var packetSize = Math.Max(64, size & ~1); //64 is the minimal length
                    packetSize = (uint)size;
                    packetSize += 6;


                    packetSize += 6;

                    if(packetSize > 1514 + 12)
                    {
                        //Maybe we should react to overruns. Now we just drop.
                        return null;
                    }

                    byte[] currentBuffer = new byte[(uint)packetSize];
                    currentBuffer[2] = (byte)((packetSize - 6) & 0xff);
                    currentBuffer[3] = (byte)((packetSize - 6) >> 8);
                    var frameBytes = receivedFrame.Bytes;
                    ushort cksum = 0;

                    byte[] tmp = new byte[(uint)frameBytes.Length - 14];
                    Array.Copy(frameBytes, 14, tmp, 0, tmp.Length);
                    cksum = CalculateChecksumRX(tmp);
                    if((frameBytes[14] & 0xF0) == 0x40) //IP packet
                    {

                        if(frameBytes[23] == 0x06) // TCP packet
                        {

                            uint sa = (uint)((frameBytes[MACHeaderLegth + 12 + 3] << 24) | (frameBytes[MACHeaderLegth + 12 + 2] << 16) | (frameBytes[MACHeaderLegth + 12 + 1] << 8) | (frameBytes[MACHeaderLegth + 12 + 0] << 0));
                            uint da = (uint)((frameBytes[MACHeaderLegth + 16 + 3] << 24) | (frameBytes[MACHeaderLegth + 16 + 2] << 16) | (frameBytes[MACHeaderLegth + 16 + 1] << 8) | (frameBytes[MACHeaderLegth + 16 + 0] << 0));
                            ushort protocol = frameBytes[MACHeaderLegth + 9];
                            ushort IPHeaderLength = (ushort)((frameBytes[14] & 0x0F) * 4);
                            ushort packetLength = (ushort)(System.Net.IPAddress.HostToNetworkOrder((ushort)(frameBytes.Length - (MACHeaderLegth + IPHeaderLength))) >> 16);
                            long s = sa + da + (protocol << 8) + packetLength;
                            s += (s >> 32);
                            s = (s & 0xffff) + (s >> 16);
                            s = (s & 0xffff) + (s >> 16);
                            cksum = (ushort)~s;
                        }
                    }

                    if((frameBytes[14] & 0xF0) == 0x40) //IP packet
                    {
                        if(frameBytes[23] == 0x01) // UDP packet
                        {
                            Array.Copy(frameBytes, 14, tmp, 0, tmp.Length);
                            ushort cksumm = CalculateChecksumRX(tmp);
                            frameBytes[36] = (byte)((cksumm >> 8) & 0xFF);
                            frameBytes[37] = (byte)((cksumm) & 0xFF);
                        }
                    }

                    for(int i=0; i< size; i++)
                    {
                        currentBuffer[6 + i] = frameBytes[i];
                    }

                    if((frameBytes[14] & 0xF0) == 0x40) //IP packet
                    {
                        if(frameBytes[23] == 0x06)
                        {
                            currentBuffer[packetSize - 1] = (byte)(((cksum) >> 8) & 0xFF);
                            currentBuffer[packetSize - 2] = (byte)((cksum) & 0xFF);
                        }
                        else if(frameBytes[23] == 0x11)
                        {
                            currentBuffer[packetSize - 1] = (byte)(((cksum) >> 8) & 0xFF);
                            currentBuffer[packetSize - 2] = (byte)((cksum) & 0xFF);
                        }
                    }
                    return currentBuffer;
                }
                return null;
            }


        }
        #endregion
        #region ping hack variables
        private readonly object sync = new object();
        #endregion
        #region INetworkInterface implementation
        public event Action<EthernetFrame> FrameReady;

        public void ReceiveFrame(EthernetFrame frame)//when data is send to us
        {
            lock(sync)
            {
                if(!frame.DestinationMAC.IsBroadcast && frame.DestinationMAC != MAC)
                {
                    return;
                }

                if(!EthernetFrame.CheckCRC(frame.Bytes))
                {
                    this.Log(LogLevel.Info, "Invalid CRC, packet discarded");
                    return;
                }

                rxPacketQueue.Enqueue(frame);
            }
        }
        #endregion
        #region device registers
        //   private byte[] macAddress = new byte[] {0,0,0,0,0,0};
        #endregion
        #region USB descriptors

        private ConfigurationUSBDescriptor configurationDescriptor = new ConfigurationUSBDescriptor() {
            ConfigurationIndex = 3,
            SelfPowered = true,
            NumberOfInterfaces = 1,
            RemoteWakeup = true,
            MaxPower = 0x01, //2mA
            ConfigurationValue = 1
        };
        private ConfigurationUSBDescriptor otherConfigurationDescriptor = new ConfigurationUSBDescriptor();
        private StringUSBDescriptor stringDescriptor = null;
        private StandardUSBDescriptor deviceDescriptor = new StandardUSBDescriptor {
            DeviceClass=0xff,//vendor specific
            DeviceSubClass = 0xff,//vendor specific
            USB = 0x0200,
            DeviceProtocol = 0xff,//vendor specific
            MaxPacketSize = 64,
            VendorId = 0x0424,
            ProductId = 0xec00,
            Device = 0x0200,
            ManufacturerIndex = 4,
            ProductIndex = 1,
            SerialNumberIndex = 2,
            NumberOfConfigurations = 1
        };
        private DeviceQualifierUSBDescriptor deviceQualifierDescriptor = new DeviceQualifierUSBDescriptor();
        private EndpointUSBDescriptor[] endpointDescriptor;
        private InterfaceUSBDescriptor[] interfaceDescriptor = new[] {new InterfaceUSBDescriptor
        {
            AlternateSetting = 0,
            InterfaceNumber = 0,
            NumberOfEndpoints = NumberOfEndpoints,
            InterfaceClass = 0xff, //vendor specific
            InterfaceProtocol = 0xff,
            InterfaceSubClass = 0xff,
            InterfaceIndex = 0
        }
        };
        //private uint address;
        private Dictionary<ushort, string[]> stringValues = new Dictionary<ushort, string[]>() {
            {EnglishLangId, new string[]{
                    "",
                    "SMSC914",
                    "0xALLMAN",
                    "Configuration",
                    "AntMicro"
                }}
        };

        private void fillEndpointsDescriptors(EndpointUSBDescriptor[] endpointDesc)
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

            endpointDesc[2].EndpointNumber = 3;
            endpointDesc[2].InEnpoint = true;
            endpointDesc[2].TransferType = EndpointUSBDescriptor.TransferTypeEnum.Interrupt;
            endpointDesc[2].MaxPacketSize = 16;
            endpointDesc[2].SynchronizationType = EndpointUSBDescriptor.SynchronizationTypeEnum.NoSynchronization;
            endpointDesc[2].UsageType = EndpointUSBDescriptor.UsageTypeEnum.Data;
            endpointDesc[2].Interval = 2;

        }
        #endregion
        #region Device enums
        private enum vendorRequest : byte
        {
            WriteRegister = 0xA0,
            ReadRegister = 0xA1
        }

        private enum txCommands
        {
            FirstSegment = 0x00002000,
            LastSegment = 0x00001000
        }

        private enum rxStatus
        {
            FrameLength = 0x3FFF0000,
            ErrorSummary = 0x00008000
        }

        private enum SCSR
        {
            IdRevision = 0x00,
            InterruptStatus = 0x08,
            TxConfig = 0x10,
            HwConfig = 0x14,
            PmControl = 0x20,
            AfcConfig = 0x2C,
            E2PCommand = 0x30,
            E2PData = 0x34,
            BurstCapabilities = 0x38,
            InterruptEndpointControl = 0x68,
            BulkInDly = 0x6C,
            MACControl = 0x100,
            MACAddressHi = 0x104,
            MACAddressLo = 0x108,
            MediaIndependentInterfaceAddress = 0x114,
            MediaIndependentInterfaceData = 0x118,
            Flow = 0x11C,
            Vlan1 = 0x120,
            ConnectionOrientedEthernetControl = 0x130
        }
        #endregion
        #region Device constans
        private const byte NumberOfEndpoints = 3;
        private const ushort EnglishLangId = 0x09;
        #endregion
        #region Device variables
        private uint macControlRegister = 0x00;
        private uint e2pCommand = 0x00;
        private uint hardwareConfigurationRegister = 0x00;
        private uint powerMenagementConfigurationRegister = 0x00;
        private uint miiData = 0x04;
        private uint miiAddress = 0x04;
        private bool[] dataToggleBit = new bool[NumberOfEndpoints + 1];
        #endregion
        #region IUSBDevice implementation
        public byte[] ProcessVendorGet(USBPacket packet, USBSetupPacket setupPacket)
        {
            ushort index = setupPacket.index;
            byte request = setupPacket.request;
            ushort value = setupPacket.value;
            if(request == (byte)vendorRequest.ReadRegister)
            {
                switch((SCSR)index)
                {
                case SCSR.MACAddressLo:

                    break;
                    case SCSR.MACAddressHi:

                    break;
                    case SCSR.E2PData:
                    if((e2pCommand & 0x000001FF) >= 0x1 && (e2pCommand & 0x000001FF) <= 0x6)
                    {
                        controlPacket = new byte[1];
                        controlPacket[0] = MAC.Bytes[(e2pCommand & 0x000001FF) - 1];
                        return controlPacket;
                    }
                    else
                    {
                        controlPacket = BitConverter.GetBytes((uint)0);
                        return BitConverter.GetBytes((uint)0);
                    }
                    case SCSR.MACControl:
                    controlPacket = BitConverter.GetBytes(macControlRegister);
                    return BitConverter.GetBytes(macControlRegister);
                    case SCSR.E2PCommand:
                    controlPacket = BitConverter.GetBytes(e2pCommand);
                    return BitConverter.GetBytes(e2pCommand);
                    case SCSR.HwConfig:
                    controlPacket = BitConverter.GetBytes(hardwareConfigurationRegister & (~0x00000008));
                    return (BitConverter.GetBytes(hardwareConfigurationRegister & (~0x00000008)));
                    case SCSR.PmControl:
                    controlPacket = BitConverter.GetBytes(powerMenagementConfigurationRegister & (~0x00000010));
                    return BitConverter.GetBytes(powerMenagementConfigurationRegister & (~0x00000010));
                    case SCSR.MediaIndependentInterfaceData:
                    controlPacket = BitConverter.GetBytes(miiData & (~0x8000) | 0x0004 | 0x0100);
                    return BitConverter.GetBytes(miiData & (~0x8000) | 0x0004 | 0x0100);
                    case SCSR.MediaIndependentInterfaceAddress:
                    controlPacket = BitConverter.GetBytes(miiAddress);
                    controlPacket[0] &= ((byte)(0xFEu));
                    return controlPacket;
                default:
                    this.Log(LogLevel.Warning, "Unknown register read request (request=0x{0:X}, value=0x{1:X}, index=0x{2:X})", request, value, index);
                    break;
                }
            }
            var arr = new byte[] { 0 };
            controlPacket = arr;
            return arr;
        }

        public void ProcessVendorSet(USBPacket packet, USBSetupPacket setupPacket)
        {
            ushort index = setupPacket.index;
            byte request = setupPacket.request;
            ushort value = setupPacket.value;
            if(request == (byte)vendorRequest.WriteRegister)
            {
                switch((SCSR)index)
                {
                case SCSR.HwConfig:
                    if(packet.data != null)
                        hardwareConfigurationRegister = BitConverter.ToUInt32(packet.data, 0);
                    break;
                    case SCSR.PmControl:
                    if(packet.data != null)
                        powerMenagementConfigurationRegister = BitConverter.ToUInt32(packet.data, 0);
                    break;
                    case SCSR.MACAddressLo:
                    break;
                    case SCSR.MACAddressHi:
                    break;
                    case SCSR.MACControl:
                    if(packet.data != null)
                        macControlRegister = BitConverter.ToUInt32(packet.data, 0);
                    this.Log(LogLevel.Warning, "macControlRegister=0x{0:X}", macControlRegister);
                    break;
                    case SCSR.E2PData:
                    break;
                    case SCSR.E2PCommand:
                    if(packet.data != null)
                        e2pCommand = BitConverter.ToUInt32(packet.data, 0) & (~(0x80000000 | 0x00000400));
                    break;
                    case SCSR.MediaIndependentInterfaceAddress:
                    if(packet.data != null)
                        miiAddress = BitConverter.ToUInt32(packet.data, 0);
                    break;
                    case SCSR.MediaIndependentInterfaceData:
                    if(packet.data != null)
                        miiData = BitConverter.ToUInt32(packet.data, 0);
                    break;
                    default:
                    this.Log(LogLevel.Warning, "Unknown register write request  (request=0x{0:X}, value=0x{1:X}, index=0x{2:X})", request, value, index);
                    break;
                }
            }
        }
        #endregion
        #region IMACInterface implementation
        public MACAddress MAC { get; set; }
        #endregion

        private ushort IPHeaderLength = 20;
        private const ushort MACHeaderLegth = 14;


        private class IPpacket
        {
            public IPpacket(ushort IPLength, PacketType type)
            {
                IPHeaderLength = IPLength;
                packetType = type;
                pseudoheader = new PseudoHeader();
            }

            public void ReadFromBuffer(byte[] buffer)
            {
                pseudoheader.FillFromBuffer( buffer );

                packet = new byte[buffer.Length - (MACHeaderLegth + IPHeaderLength) ];
                Array.Copy(buffer, MACHeaderLegth + IPHeaderLength, packet, 0, ( buffer.Length - (MACHeaderLegth + IPHeaderLength) ) );
                if( packetType == PacketType.TCP )
                {
                    packet[16] = 0;
                    packet[17] = 0;
                }
                else if( packetType == PacketType.UDP )
                {
                    packet[6] = 0;
                    packet[7] = 0;
                }

            }

            private ushort CalculateChecksum(byte [] data)
            {
                ulong sum = 0;
                int size = data.Length;
                uint i = 0;
                ushort addVal;
                while( size > 1 )
                {
                    addVal = (ushort)((data[i] << 8) | data[i+1]);
                    sum += addVal;
                    i+=2;
                    size -= 2;
                }
                if( size != 0) //if odd length
                    sum += (ushort)((data[i] << 8) | 0x00);


                while ( (sum >>16) != 0 )
                {
                    sum = (sum >> 16) + (sum & 0xffff);
                }
                return (ushort)( (~sum) + 1 );
            }

            public ushort GetChecksum()
            {
                ushort cksum;

                checksumCalculationBase = new byte[packet.Length + pseudoheader.Length];

                Array.Copy(pseudoheader.ToArray(),0,checksumCalculationBase,0,pseudoheader.Length);
                Array.Copy(packet,0,checksumCalculationBase,pseudoheader.Length,packet.Length);

                cksum = CalculateChecksum(checksumCalculationBase);
                return (ushort)(cksum);

            }

            private class PseudoHeader
            {
                public void FillFromBuffer(byte[] buffer)
                {
                    sourceAddress = new byte[4];
                    destinationAddress = new byte[4];
                    Array.Copy(buffer,MACHeaderLegth+12,sourceAddress,0,4);
                    Array.Copy(buffer,MACHeaderLegth+16,destinationAddress,0,4);
                    protocol = buffer[MACHeaderLegth + 9];
                    packetLength = (ushort)(System.Net.IPAddress.HostToNetworkOrder((ushort)(buffer.Length - (MACHeaderLegth + IPHeaderLength)))>>16);
                }

                public byte[] ToArray()

                {
                    byte[] arr = new byte[Length];
                    Array.Copy(sourceAddress,0,arr,0,4);
                    Array.Copy(destinationAddress,0,arr,4,4);
                    arr[8] = zeros;
                    arr[9] = protocol;
                    Array.Copy(BitConverter.GetBytes(packetLength),0,arr,10,2);
                    return arr;
                }

                private byte[] sourceAddress;
                private byte[] destinationAddress;
                private readonly byte zeros = 0x00;
                private byte protocol;
                private ushort packetLength;

                public readonly ushort Length = 12;

            }

            public enum PacketType
            {
                TCP = 1,
                UDP = 2
            }

            private PacketType packetType;
            private static ushort IPHeaderLength;
            private const ushort MACHeaderLegth = 14;

            private PseudoHeader pseudoheader;
            private byte[] packet;
            private byte[] checksumCalculationBase;

        }
    }
}

