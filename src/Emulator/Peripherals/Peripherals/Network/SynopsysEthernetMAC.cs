//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System.Collections.Generic;
using Antmicro.Renode.Network;

namespace Antmicro.Renode.Peripherals.Network
{
    //TODO: Might be Word/BytePeripheral as well
    public sealed class SynopsysEthernetMAC : NetworkWithPHY, IDoubleWordPeripheral, IMACInterface, IKnownSize
    {
        public SynopsysEthernetMAC(IMachine machine, SynopsysEthernetVersion version = SynopsysEthernetVersion.STM32F4) : base(machine)
        {
            sysbus = machine.GetSystemBus(this);
            this.version = version;
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            IRQ = new GPIO();
            Reset();
        }

        public override void Reset()
        {
            macConfiguration = 0x8000;
            macFrameFilter = 0x0;
            macMiiAddress = 0x0;
            macMiiData = 0x0;
            macFlowControl = 0x0;
            dmaBusMode = 0x20100;
            dmaReceiveDescriptorListAddress = 0x0;
            dmaTransmitDescriptorListAddress = 0x0;
            dmaOperationMode = 0x0;
            dmaInterruptEnable = 0x0;
        }

        public uint ReadDoubleWord(long offset)
        {
            this.NoisyLog("Read from {0}", (Registers)offset);
            switch((Registers)offset)
            {
            case Registers.MACConfiguration:
                return macConfiguration;
            case Registers.MACFrameFilter:
                return macFrameFilter;
            case Registers.MACMIIAddress:
                return macMiiAddress;
            case Registers.MACMIIData:
                return macMiiData;
            case Registers.MACFlowControl:
                return macFlowControl;
            case Registers.MACAddress0High:
                return (uint)((MAC.F << 8) | MAC.E);
            case Registers.MACAddress0Low:
                return (uint)((MAC.D << 24) | (MAC.C << 16) | (MAC.B << 8) | MAC.A);
            case Registers.DMABusMode:
                return dmaBusMode;
            case Registers.DMAReceiveDescriptorListAddress:
                return dmaReceiveDescriptorListAddress;
            case Registers.DMATransmitDescriptorListAddress:
                return dmaTransmitDescriptorListAddress;
            case Registers.DMAStatusRegister:
                if((dmaStatus & ((1u << 14) | (1u << 6) | (1u << 2) | 1u)) != 0)
                {
                    dmaStatus |= 1u << 16; //Normal interrupt summary
                }
                return dmaStatus;
            case Registers.DMAOperationMode:
                return dmaOperationMode;
            case Registers.DMAInterruptEnable:
                return dmaInterruptEnable;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            this.NoisyLog("Write {0:X} to {1}", value, (Registers)offset);
            switch((Registers)offset)
            {
            case Registers.MACConfiguration:
                macConfiguration = value;
                crcStrippingForTypeFrames = (macConfiguration & 1u << 25) != 0;
                automaticPadCRCStripping = (macConfiguration & 1u << 7) != 0;
                break;
            case Registers.MACFrameFilter:
                macFrameFilter = value;
                break;
            case Registers.MACMIIAddress:
                macMiiAddress = value;
                var busyClear = (value & 0x1) != 0;
                if(busyClear)
                {
                    macMiiAddress = macMiiAddress & ~0x1u;
                }
                var phyId = (value >> 11) & 0x1F;
                var register = (ushort)((value >> 6) & 0x1F);
                var isRead = ((value >> 1) & 0x1) == 0;
                if(!TryGetPhy<ushort>(phyId, out var phy))
                {
                    this.Log(LogLevel.Warning, "Access to unknown phy {0}", phyId);
                    break;
                }
                if(isRead)
                {
                    macMiiData = phy.Read(register);
                }
                else
                {
                    phy.Write(register, macMiiData);
                }

                break;
            case Registers.MACMIIData:
                macMiiData = (ushort)value;
                break;
            case Registers.MACFlowControl:
                macFlowControl = value;
                break;
            case Registers.MACAddress0High:
                MAC = MAC.WithNewOctets(f: (byte)(value >> 8), e: (byte)value);
                break;
            case Registers.MACAddress0Low:
                MAC = MAC.WithNewOctets(d: (byte)(value >> 24), c: (byte)(value >> 16), b: (byte)(value >> 8), a: (byte)value);
                break;
            case Registers.DMABusMode:
                dmaBusMode = value & ~0x1u;
                if((value & 0x1) != 0)
                {
                    Reset();
                }
                break;
            case Registers.DMATransmitPollDemand:
                if((dmaStatus | StartStopTransmission) != 0)
                {
                    SendFrames();
                }
                break;
            case Registers.DMAReceiveDescriptorListAddress:
                this.Log(LogLevel.Info, "Setting RDLA to 0x{0:X}.", value);
                dmaReceiveDescriptorListAddress = value & ~3u;
                dmaReceiveDescriptorListAddressBegin = dmaReceiveDescriptorListAddress;
                break;
            case Registers.DMATransmitDescriptorListAddress:
                dmaTransmitDescriptorListAddress = value & ~3u;
                dmaTransmitDescriptorListAddressBegin = dmaReceiveDescriptorListAddress;
                break;
            case Registers.DMAStatusRegister:
                dmaStatus &= ~value; //write 1 to clear;
                if((value & 0x10000) > 0)
                {
                    IRQ.Unset();
                    TryDequeueFrame();
                }
                break;
            case Registers.DMAOperationMode:
                dmaOperationMode = value;
                if((value & StartStopTransmission) != 0)
                {
                    SendFrames();
                }
                break;
            case Registers.DMAInterruptEnable:
                if(BitHelper.IsBitSet(value, 16)) //normal interrupt summary enable
                {
                    value |= (1u << 14) | (1u << 6) | (1u << 2) | 1u;
                }
                dmaInterruptEnable = value;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            /*if(machine.ElapsedTime < TimeSpan.FromSeconds(30))
            {
                return;
            }*/
            lock(receiveLock)
            {
                if((dmaStatus & ReceiveStatus) != 0)
                {
                    queue.Enqueue(frame);
                    return;
                }
                if(frame.Bytes.Length < 14)
                {
                    this.Log(LogLevel.Error, "DROPPING - packet too short.");
                    return;
                }
                if(this.machine.IsPaused)
                {
                    this.Log(LogLevel.Debug, "DROPPING - cpu is halted.");
                    return;
                }
                var destinationMac = frame.DestinationMAC;
                if(!destinationMac.IsBroadcast && !destinationMac.Equals(MAC))
                {
                    this.Log(LogLevel.Debug, "DROPPING - not for us.");
                    return;
                }
		/*
                if((dmaInterruptEnable & (ReceiveStatus)) == 0)
                {
                    this.Log(LogLevel.Debug, "DROPPING - rx irq is turned off.");
                    return;
                }
		*/
                this.Log(LogLevel.Noisy, Misc.DumpPacket(frame, false, machine));
                if(dmaReceiveDescriptorListAddress < 0x20000000)
                {
                    // TODO: not in ram
                    this.Log(LogLevel.Error, "DROPPING - descriptor is not valid.");
                    return;
                }
                var written = 0;
                var first = true;
                var bytes = frame.Bytes;

                if(!EthernetFrame.CheckCRC(bytes))
                {
                    if(!(crcStrippingForTypeFrames && bytes.Length > 1536) || !(automaticPadCRCStripping && bytes.Length < 1500))
                    {
                        this.Log(LogLevel.Info, "Invalid CRC, packet discarded");
                        return;
                    }
                }

                var receiveDescriptor = new RxDescriptor(this, sysbus, version);
                receiveDescriptor.Fetch(dmaReceiveDescriptorListAddress);
                if(receiveDescriptor.IsOwnedByDMA)
                {
                    this.Log(LogLevel.Error, "DROPPING  - descriptor is used.");
                    return;
                }
                this.Log(LogLevel.Noisy, "DESCRIPTOR ADDR1={0:X}, ADDR2={1:X}", receiveDescriptor.Address1, receiveDescriptor.Address2);
                while(!receiveDescriptor.IsOwnedByDMA)
                {
                    if(receiveDescriptor.Address1 < 0x20000000)
                    {
                        this.Log(LogLevel.Error, "Descriptor points outside of ram, aborting... This should not happen!");
                        break;
                    }
                    receiveDescriptor.IsOwnedByDMA = true;
                    receiveDescriptor.IsFirstSegment = first;
                    first = false;
                    var howManyBytes = Math.Min(receiveDescriptor.Buffer1Length, frame.Bytes.Length - written);
                    var toWriteArray = new byte[howManyBytes];

                    Array.Copy(bytes, written, toWriteArray, 0, howManyBytes);
                    sysbus.WriteBytes(toWriteArray, receiveDescriptor.Address1);
                    written += howManyBytes;
                    //write second buffer
                    if(frame.Bytes.Length - written > 0 && !receiveDescriptor.IsNextDescriptorChained)
                    {
                        howManyBytes = Math.Min(receiveDescriptor.Buffer2Length, frame.Bytes.Length - written);
                        toWriteArray = new byte[howManyBytes];
                        Array.Copy(bytes, written, toWriteArray, 0, howManyBytes);
                        sysbus.WriteBytes(toWriteArray, receiveDescriptor.Address2);
                        written += howManyBytes;
                    }
                    if(frame.Bytes.Length - written <= 0)
                    {
                        receiveDescriptor.IsLastSegment = true;
                        this.NoisyLog("Setting descriptor length to {0}", (uint)frame.Bytes.Length);
                        receiveDescriptor.FrameLength = (uint)frame.Bytes.Length;
                    }
                    this.NoisyLog("Writing descriptor at 0x{6:X}, first={0}, last={1}, written {2} of {3}. next_chained={4}, endofring={5}", receiveDescriptor.IsFirstSegment, receiveDescriptor.IsLastSegment, written, frame.Bytes.Length, receiveDescriptor.IsNextDescriptorChained, receiveDescriptor.IsEndOfRing, dmaReceiveDescriptorListAddress);
                    receiveDescriptor.WriteBack();
                    if(!receiveDescriptor.IsNextDescriptorChained)
                    {
                        dmaReceiveDescriptorListAddress += 8;
                    }
                    else if(receiveDescriptor.IsEndOfRing)
                    {
                        dmaReceiveDescriptorListAddress = dmaReceiveDescriptorListAddressBegin;
                    }
                    else
                    {
                        dmaReceiveDescriptorListAddress = receiveDescriptor.Address2;
                    }
                    if(frame.Bytes.Length - written <= 0)
                    {
                        if((dmaInterruptEnable & (ReceiveStatus)) != 0)// receive interrupt
                        {
                            dmaStatus |= ReceiveStatus;
                            IRQ.Set();
                        }
                        else
                        {
                            this.DebugLog("Exiting but not scheduling an interrupt!");
                        }
                        break;
                    }
                    receiveDescriptor.Fetch(dmaReceiveDescriptorListAddress);
                }
                this.DebugLog("Packet of length {0} delivered.", frame.Bytes.Length);
                if(written < frame.Bytes.Length)
                {
                    this.Log(LogLevel.Error, "Delivered only {0} from {1} bytes!", written, frame.Bytes.Length);
                }
            }
        }

        public event Action<EthernetFrame> FrameReady;

        public MACAddress MAC { get; set; }

        public GPIO IRQ { get; private set; }

        public long Size
        {
            get
            {
                return 0x1400;
            }
        }

        private void SendFrames()
        {
            this.Log(LogLevel.Noisy, "Sending frame");
            var transmitDescriptor = new TxDescriptor(this, sysbus, version);
            var packetData = new List<byte>();

            transmitDescriptor.Fetch(dmaTransmitDescriptorListAddress);
            while(!transmitDescriptor.IsOwnedByDMA)
            {
                transmitDescriptor.IsOwnedByDMA = true;
                this.Log(LogLevel.Noisy, "GOING TO READ FROM {0:X}, len={1}", transmitDescriptor.Address1, transmitDescriptor.Buffer1Length);
                packetData.AddRange(sysbus.ReadBytes(transmitDescriptor.Address1, transmitDescriptor.Buffer1Length));
                if(!transmitDescriptor.IsNextDescriptorChained)
                {
                    packetData.AddRange(sysbus.ReadBytes(transmitDescriptor.Address2, transmitDescriptor.Buffer2Length));
                }

                transmitDescriptor.WriteBack();

                if(transmitDescriptor.IsEndOfRing)
                {
                    dmaTransmitDescriptorListAddress = dmaTransmitDescriptorListAddressBegin;
                }
                else if(transmitDescriptor.IsNextDescriptorChained)
                {
                    dmaTransmitDescriptorListAddress = transmitDescriptor.Address2;
                }
                else
                {
                    dmaTransmitDescriptorListAddress += 8;
                }
                if(transmitDescriptor.IsLastSegment)
                {
                    this.Log(LogLevel.Noisy, "Sending frame of {0} bytes.", packetData.Count);

                    if(!Misc.TryCreateFrameOrLogWarning(this, packetData.ToArray(), out var frame, addCrc: true))
                    {
                        continue;
                    }
                    if(transmitDescriptor.ChecksumInstertionControl > 0)
                    {
                        this.Log(LogLevel.Noisy, "Calculating checksum (mode {0}).", transmitDescriptor.ChecksumInstertionControl);
                        if(transmitDescriptor.ChecksumInstertionControl == 1)
                        {
                            //IP only
                            frame.FillWithChecksums(supportedEtherChecksums, new IPProtocolType[] {});
                        }
                        else
                        {
                            //IP and payload
                            frame.FillWithChecksums(supportedEtherChecksums, supportedIPChecksums);
                        }
                    }
                    this.Log(LogLevel.Debug, Misc.DumpPacket(frame, true, machine));

                    if((dmaInterruptEnable & (TransmitStatus)) != 0) // transmit interrupt
                    {
                        dmaStatus |= TransmitStatus;
                        IRQ.Set();
                    }

                    FrameReady?.Invoke(frame);
                }
                transmitDescriptor.Fetch(dmaTransmitDescriptorListAddress);
            }

            //set TransmitBufferUnavailable
            dmaStatus |= TransmitBufferUnavailableStatus;
            dmaStatus |= TransmitStatus;
            if((dmaInterruptEnable & (StartStopTransmission)) == 0)
            {
                IRQ.Set();
            }
            this.Log(LogLevel.Noisy, "Frame sent.");
        }

        private void TryDequeueFrame()
        {
            lock(receiveLock)
            {
                if(queue.Count > 0 && ((dmaStatus & ReceiveStatus) == 0))
                {
                    var frame = queue.Dequeue();
                    ReceiveFrame(frame);
                }
            }
        }

        private bool automaticPadCRCStripping;
        private bool crcStrippingForTypeFrames;
        private uint macConfiguration;
        private uint macFrameFilter;
        private uint macMiiAddress;
        private ushort macMiiData;
        private uint macFlowControl;
        private uint dmaBusMode;
        private uint dmaReceiveDescriptorListAddress;
        private uint dmaReceiveDescriptorListAddressBegin;
        private uint dmaTransmitDescriptorListAddress;
        private uint dmaTransmitDescriptorListAddressBegin;
        private uint dmaStatus;
        private uint dmaOperationMode;
        private uint dmaInterruptEnable;
        private readonly IBusController sysbus;
        private readonly object receiveLock = new object();
        private readonly Queue<EthernetFrame> queue = new Queue<EthernetFrame>();
        private readonly EtherType[] supportedEtherChecksums = { EtherType.IpV4, EtherType.Arp };
        private readonly IPProtocolType[] supportedIPChecksums = {
            IPProtocolType.TCP,
            IPProtocolType.UDP,
            IPProtocolType.ICMP
        };
        private readonly SynopsysEthernetVersion version;
        private const uint StartStopTransmission = 1 << 13;
        private const uint TransmitBufferUnavailableStatus = 1 << 2;
        private const uint ReceiveStatus = 1 << 6;
        private const uint TransmitStatus = 1 << 0;

        public enum SynopsysEthernetVersion
        {
            STM32F4,
            BeagleV,
        }

        private class Descriptor
        {
            public Descriptor(SynopsysEthernetMAC parent, IBusController sysbus, SynopsysEthernetVersion version)
            {
                this.sysbus = sysbus;
                this.version = version;
                this.parent = parent;
            }

            public void Fetch(uint address)
            {
                this.address = address;

                word0 = sysbus.ReadDoubleWord(address);
                word1 = sysbus.ReadDoubleWord(address + 4);
                word2 = sysbus.ReadDoubleWord(address + 8);
                word3 = sysbus.ReadDoubleWord(address + 12);
            }

            public void WriteBack()
            {
                sysbus.WriteDoubleWord(address, word0);
                sysbus.WriteDoubleWord(address + 4, word1);
                sysbus.WriteDoubleWord(address + 8, word2);
                sysbus.WriteDoubleWord(address + 12, word3);
            }

            public bool IsOwnedByDMA
            {
                get => !BitHelper.IsBitSet(word0, 31);
                set => BitHelper.SetBit(ref word0, 31, !value);
            }

            public uint Address1
            {
                get => word2;
            }

            public uint Address2
            {
                get => word3;
            }

            public int Buffer1Length
            {
                get
                {
                    switch(version)
                    {
                    case SynopsysEthernetVersion.BeagleV:
                        return (int)BitHelper.GetValue(word1, 0, 10);
                    case SynopsysEthernetVersion.STM32F4:
                        return (int)BitHelper.GetValue(word1, 0, 13);
                    default:
                        parent.ErrorLog("Unsupported {0}: {1}, returning 0", nameof(SynopsysEthernetVersion), version);
                        return 0;
                    }
                }
            }

            public int Buffer2Length
            {
                get
                {
                    switch(version)
                    {
                    case SynopsysEthernetVersion.BeagleV:
                        return (int)BitHelper.GetValue(word1, 11, 10);
                    case SynopsysEthernetVersion.STM32F4:
                        return (int)BitHelper.GetValue(word1, 16, 13);
                    default:
                        parent.ErrorLog("Unsupported {0}: {1}, returning 0", nameof(SynopsysEthernetVersion), version);
                        return 0;
                    }
                }
            }

            protected const uint UsedField = 1u << 31;
            protected uint address;
            protected uint word0;
            protected uint word1;
            protected uint word2;
            protected uint word3;
            protected readonly SynopsysEthernetVersion version;
            protected readonly SynopsysEthernetMAC parent;
            private readonly IBusController sysbus;
        }

        private class TxDescriptor : Descriptor
        {
            public TxDescriptor(SynopsysEthernetMAC parent, IBusController sysbus, SynopsysEthernetVersion version) : base(parent, sysbus, version)
            {
            }

            public uint ChecksumInstertionControl
            {
                get
                {
                    switch(version)
                    { 
                    case SynopsysEthernetVersion.BeagleV:
                        return BitHelper.GetValue(word1, 27, 2);
                    case SynopsysEthernetVersion.STM32F4:
                        return BitHelper.GetValue(word0, 22, 2);
                    default:
                        parent.ErrorLog("Unsupported {0}: {1}, returning 0", nameof(SynopsysEthernetVersion), version);
                        return 0;
                    }
                }
            }

            public bool IsLastSegment
            {
                get
                {
                    switch(version)
                    {
                    case SynopsysEthernetVersion.BeagleV:
                        return BitHelper.IsBitSet(word1, 30);
                    case SynopsysEthernetVersion.STM32F4:
                        return BitHelper.IsBitSet(word0, 29);
                    default:
                        parent.ErrorLog("Unsupported {0}: {1}, returning 0", nameof(SynopsysEthernetVersion), version);
                        return false;
                    }
                }
            }

            public bool IsNextDescriptorChained
            {
                get
                {
                    switch(version)
                    {
                    case SynopsysEthernetVersion.BeagleV:
                        return BitHelper.IsBitSet(word1, 24);
                    case SynopsysEthernetVersion.STM32F4:
                        return BitHelper.IsBitSet(word0, 20);
                    default:
                        parent.ErrorLog("Unsupported {0}: {1}, returning 0", nameof(SynopsysEthernetVersion), version);
                        return false;
                    }
                }
            }

            public bool IsEndOfRing
            {
                get
                {
                    switch(version)
                    {
                    case SynopsysEthernetVersion.BeagleV:
                        return BitHelper.IsBitSet(word1, 25);
                    case SynopsysEthernetVersion.STM32F4:
                        return BitHelper.IsBitSet(word0, 21);
                    default:
                        parent.ErrorLog("Unsupported {0}: {1}, returning 0", nameof(SynopsysEthernetVersion), version);
                        return false;
                    }
                }
            }
        }

        private class RxDescriptor : Descriptor
        {
            public RxDescriptor(SynopsysEthernetMAC parent, IBusController sysbus, SynopsysEthernetVersion version) : base(parent, sysbus, version)
            {
            }

            public bool IsNextDescriptorChained
            {
                get
                {
                    switch(version)
                    {
                    case SynopsysEthernetVersion.BeagleV:
                        return BitHelper.IsBitSet(word1, 24);
                    case SynopsysEthernetVersion.STM32F4:
                        return BitHelper.IsBitSet(word1, 14);
                    default:
                        parent.ErrorLog("Unsupported {0}: {1}, returning 0", nameof(SynopsysEthernetVersion), version);
                        return false;
                    }

                }
            }

            public bool IsEndOfRing
            {
                get
                {
                    switch(version)
                    {
                    case SynopsysEthernetVersion.BeagleV:
                        return BitHelper.IsBitSet(word1, 25);
                    case SynopsysEthernetVersion.STM32F4:
                        return BitHelper.IsBitSet(word1, 15);
                    default:
                        parent.ErrorLog("Unsupported {0}: {1}, returning 0", nameof(SynopsysEthernetVersion), version);
                        return false;
                    }
                }
            }

            public bool IsLastSegment
            {
                set
                {
                    BitHelper.SetBit(ref word0, 8, value);
                }
                get
                {
                    return BitHelper.IsBitSet(word0, 8);
                }
            }

            public bool IsFirstSegment
            {
                set
                {
                    BitHelper.SetBit(ref word0, 9, value);
                }
                get
                {
                    return BitHelper.IsBitSet(word0, 9);
                }
            }

            public uint FrameLength
            {
                set
                {
                    BitHelper.ReplaceBits(ref word0, value, width: 14, destinationPosition: 16);
                }
            }
        }

        private enum Registers
        {
            MACConfiguration = 0x0000,
            MACFrameFilter = 0x0004,
            MACMIIAddress = 0x0010,
            MACMIIData = 0x0014,
            MACFlowControl = 0x0018,
            MACAddress0High = 0x0040,
            MACAddress0Low = 0x0044,
            DMABusMode = 0x1000,
            DMATransmitPollDemand = 0x1004,
            DMAReceiveDescriptorListAddress = 0x100C,
            DMATransmitDescriptorListAddress = 0x1010,
            DMAStatusRegister = 0x1014,
            DMAOperationMode = 0x1018,
            DMAInterruptEnable = 0x101C
        }
    }
}
