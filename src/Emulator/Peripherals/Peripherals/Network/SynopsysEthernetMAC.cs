//
// Copyright (c) 2010-2018 Antmicro
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
        public SynopsysEthernetMAC(Machine machine) : base(machine)
        {
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            IRQ = new GPIO();
            Reset();
        }

        public override void Reset()
        {
            packetSent = false;
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
                if(!phys.ContainsKey(phyId))
                {
                    this.Log(LogLevel.Warning, "Access to unknown phy {0}", phyId);
                    break;
                }
                if(isRead)
                {
                    macMiiData = phys[phyId].Read(register);
                }
                else
                {
                    phys[phyId].Write(register, macMiiData);
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
                if(!packetSent)
                {
                    this.Log(LogLevel.Error, "Dropping - no packets sent.");
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

                var receiveDescriptor = new RxDescriptor(machine.SystemBus);
                receiveDescriptor.Fetch(dmaReceiveDescriptorListAddress);
                if(receiveDescriptor.IsUsed)
                {
                    this.Log(LogLevel.Error, "DROPPING  - descriptor is used.");
                    return;
                }
                this.Log(LogLevel.Noisy, "DESCRIPTOR ADDR1={0:X}, ADDR2={1:X}", receiveDescriptor.Address1, receiveDescriptor.Address2);
                while(!receiveDescriptor.IsUsed)
                {
                    if(receiveDescriptor.Address1 < 0x20000000)
                    {
                        this.Log(LogLevel.Error, "Descriptor points outside of ram, aborting... This should not happen!");
                        break;
                    }
                    receiveDescriptor.IsUsed = true;
                    receiveDescriptor.IsFirst = first;
                    first = false;
                    var howManyBytes = Math.Min(receiveDescriptor.Buffer1Length, frame.Bytes.Length - written);
                    var toWriteArray = new byte[howManyBytes];

                    Array.Copy(bytes, written, toWriteArray, 0, howManyBytes);
                    machine.SystemBus.WriteBytes(toWriteArray, receiveDescriptor.Address1);
                    written += howManyBytes;
                    //write second buffer
                    if(frame.Bytes.Length - written > 0 && !receiveDescriptor.IsNextDescriptorChained)
                    {
                        howManyBytes = Math.Min(receiveDescriptor.Buffer2Length, frame.Bytes.Length - written);
                        toWriteArray = new byte[howManyBytes];
                        Array.Copy(bytes, written, toWriteArray, 0, howManyBytes);
                        machine.SystemBus.WriteBytes(toWriteArray, receiveDescriptor.Address2);
                        written += howManyBytes;
                    }
                    if(frame.Bytes.Length - written <= 0)
                    {
                        receiveDescriptor.IsLast = true;
                        this.NoisyLog("Setting descriptor length to {0}", (uint)frame.Bytes.Length);
                        receiveDescriptor.FrameLength = (uint)frame.Bytes.Length;
                    }
                    this.NoisyLog("Writing descriptor at 0x{6:X}, first={0}, last={1}, written {2} of {3}. next_chained={4}, endofring={5}", receiveDescriptor.IsFirst, receiveDescriptor.IsLast, written, frame.Bytes.Length, receiveDescriptor.IsNextDescriptorChained, receiveDescriptor.IsEndOfRing, dmaReceiveDescriptorListAddress);
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
            var transmitDescriptor = new TxDescriptor(machine.SystemBus);
            var packetData = new List<byte>();

            transmitDescriptor.Fetch(dmaTransmitDescriptorListAddress);
            while(!transmitDescriptor.IsUsed)
            {
                transmitDescriptor.IsUsed = true;
                this.Log(LogLevel.Noisy, "GOING TO READ FROM {0:X}, len={1}", transmitDescriptor.Address1, transmitDescriptor.Buffer1Length);
                packetData.AddRange(machine.SystemBus.ReadBytes(transmitDescriptor.Address1, transmitDescriptor.Buffer1Length));
                if(!transmitDescriptor.IsNextDescriptorChained)
                {
                    packetData.AddRange(machine.SystemBus.ReadBytes(transmitDescriptor.Address2, transmitDescriptor.Buffer2Length));
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
                if(transmitDescriptor.IsLast)
                {
                    this.Log(LogLevel.Noisy, "Sending frame of {0} bytes.", packetData.Count);

                    if(!Misc.TryCreateFrameOrLogWarning(this, packetData.ToArray(), out var frame, addCrc: false))
                    {
                        continue;
                    }
                    if(transmitDescriptor.ChecksumInstertionControl > 0)
                    {
                        this.Log(LogLevel.Noisy, "Calculating checksum (mode {0}).", transmitDescriptor.ChecksumInstertionControl);
                        if(transmitDescriptor.ChecksumInstertionControl == 1)
                        {
                            //IP only
                            frame.FillWithChecksums(supportedEtherChecksums, null);
                        }
                        else
                        {
                            //IP and payload
                            frame.FillWithChecksums(supportedEtherChecksums, supportedIPChecksums);
                        }
                    }
                    this.Log(LogLevel.Debug, Misc.DumpPacket(frame, true, machine));

                    packetSent = true;
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
        private bool packetSent;
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
        private readonly object receiveLock = new object();
        private readonly Queue<EthernetFrame> queue = new Queue<EthernetFrame>();
        private readonly EtherType[] supportedEtherChecksums = { EtherType.IpV4, EtherType.Arp };
        private readonly IPProtocolType[] supportedIPChecksums = {
            IPProtocolType.TCP,
            IPProtocolType.UDP,
            IPProtocolType.ICMP
        };
        private const uint StartStopTransmission = 1 << 13;
        private const uint TransmitBufferUnavailableStatus = 1 << 2;
        private const uint ReceiveStatus = 1 << 6;
        private const uint TransmitStatus = 1 << 0;

        private class Descriptor
        {
            public Descriptor(SystemBus sysbus)
            {
                this.sysbus = sysbus;
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

            public bool IsUsed
            {
                get
                {
                    return (word0 & UsedField) == 0;
                }
                set
                {
                    word0 = (word0 & ~UsedField) | (value ? 0u : UsedField);
                }
            }

            public uint Address1
            {
                get{ return word2; }
            }

            public uint Address2
            {
                get{ return word3; }
            }

            public int Buffer1Length
            {
                get{ return (int)(word1 & 0x1FFF); }
            }

            public int Buffer2Length
            {
                get{ return (int)((word1 >> 16) & 0x1FFF); }
            }

            protected const uint UsedField = 1u << 31;
            protected uint address;
            protected uint word0;
            protected uint word1;
            protected uint word2;
            protected uint word3;
            private readonly SystemBus sysbus;
        }

        private class TxDescriptor : Descriptor
        {
            public TxDescriptor(SystemBus sysbus) : base(sysbus)
            {
            }

            public uint ChecksumInstertionControl
            {
                get
                {
                    return ((word0 >> 22) & 3);
                }
            }

            public bool IsLast
            {
                get
                {
                    return (word0 & LastField) != 0;
                }
            }

            public bool IsNextDescriptorChained
            {
                get
                {
                    return (word0 & SecondDescriptorChainedField) != 0;
                }
            }

            public bool IsEndOfRing
            {
                get
                {
                    return (word0 & EndOfRingField) != 0;
                }
            }

            private const uint LastField = 1u << 29;
            private const uint SecondDescriptorChainedField = 1u << 20;
            private const uint EndOfRingField = 1u << 21;
        }

        private class RxDescriptor : Descriptor
        {
            public RxDescriptor(SystemBus sysbus) : base(sysbus)
            {
            }

            public bool IsNextDescriptorChained
            {
                get
                {
                    return (word1 & SecondDescriptorChainedField) != 0;
                }
            }

            public bool IsEndOfRing
            {
                get
                {
                    return (word1 & EndOfRingField) != 0;
                }
            }

            public bool IsLast
            {
                set
                {
                    word0 = (word0 & ~LastField) | (value ? LastField : 0u);
                }
                get
                {
                    return (word0 & LastField) != 0;
                }
            }

            public bool IsFirst
            {
                set
                {
                    word0 = (word0 & ~FirstField) | (value ? FirstField : 0u);
                }
                get
                {
                    return (word0 & FirstField) != 0;
                }
            }

            public uint FrameLength
            {
                set
                {
                    word0 = (word0 & ~FrameLengthMask) | (value << FrameLengthShift); 
                }
            }

            private const int FrameLengthShift = 16;
            private const uint FrameLengthMask = 0x3FFF0000;
            private const uint LastField = 1u << 8;
            private const uint FirstField = 1u << 9;
            private const uint EndOfRingField = 1u << 15;
            private const uint SecondDescriptorChainedField = 1u << 14;
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
