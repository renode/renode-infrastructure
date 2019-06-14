//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using Antmicro.Renode.Network;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public class ZynqEthernet : NetworkWithPHY, IDoubleWordPeripheral, IMACInterface, IKnownSize
    {
        public ZynqEthernet(Machine machine) : base(machine)
        {
            registers = new regs();        
            IRQ = new GPIO();
            //crc = new Crc16();
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            sync = new object();
            Reset();
        }

        public long Size
        {
            get
            {
                return 0x1000;
            }
        }

        public event Action<EthernetFrame> FrameReady;

        public uint ReadDoubleWord(long offset)
        {
            lock(sync)
            {
                switch((Offset)offset)
                {
                case Offset.NetControl:
                    return registers.Control;
                case Offset.NetConfig:
                    return registers.Config;
                case Offset.NetStatus:
                    return registers.Status;
                case Offset.DMAConfig:
                    return registers.DMAConfig;
                case Offset.TxStatus:
                    return registers.TxStatus;
                case Offset.RxQueueBaseAddr:
                    return registers.RxQueueBaseAddr;
                case Offset.TxQueueBaseAddr:
                    return registers.TxQueueBaseAddr;
                case Offset.RxStatus:
                    return registers.RxStatus;
                case Offset.InterruptStatus:
                    var retval = registers.InterruptStatus;
                    registers.InterruptStatus = 0;
                    IRQ.Unset();
                    return retval;
                case Offset.InterruptMask:
                    return registers.InterruptMask;
                case Offset.PhyMaintenance:
                    return registers.PhyMaintenance;
                case Offset.SpecificAddress1Bottom:
                //return registers.SpecificAddress1Bottom;
                    return BitConverter.ToUInt32(MAC.Bytes, 0);
                case Offset.SpecificAddress1Top:
                //return registers.SpecificAddress1Top;
                    return (uint)BitConverter.ToUInt16(MAC.Bytes, 4);
                case Offset.ModuleId:
                    return registers.ModuleId;
                default:
                //this.LogUnhandledRead(offset);
                    return 0;
                }
            }
            
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(sync)
            {
                switch((Offset)offset)
                {
                case Offset.NetConfig:
                    if((value & 1u << 3) == 0)
                    {
                        registers.TxQueueBaseAddr = txBufferBase;
                    }
                    return;
                case Offset.NetControl:
                    registers.Control = value & 0x619Fu; 
                    if((value & (1u << 9)) != 0) //if start tx
                    {
                        this.SendFrames();
                    }
                    return;
                case Offset.DMAConfig:
                    registers.DMAConfig = value & 0x01ff0fff;
                    return;
                case Offset.TxStatus:
                    var txstat = value & 0x1F3; //mask writable bits
                    registers.TxStatus &= ~(txstat);  
                    return;
                case Offset.RxQueueBaseAddr:
                    registers.RxQueueBaseAddr = value & (~(0x03u));
                    rxBufferBase = registers.RxQueueBaseAddr;
                    return;
                case Offset.TxQueueBaseAddr:
                    registers.TxQueueBaseAddr = value & (~(0x03u));
                    txBufferBase = registers.TxQueueBaseAddr;
                    return;
                case Offset.RxStatus:
                    var rxstat = value & 0xF;
                    registers.RxStatus &= ~(rxstat);
                    return;
                case Offset.InterruptStatus:
                    registers.InterruptStatus &= ~(value);
                    return;
                case Offset.InterruptEnable:
                    registers.InterruptMask &= ~(value);
                    return;
                case Offset.InterruptDisable:
                    registers.InterruptMask |= value;
                    return;
                case Offset.PhyMaintenance:
                    registers.Status |= 0x04;
                    var id = ((value >> 23) & 0xf);
                    var op = ((value >> 28) & 0x3);
                    var reg = ((value >> 18) & 0x1f);
                
                    if(!phys.ContainsKey(id))
                    {
                        this.Log(LogLevel.Warning, "Write to phy with unknown ID {0}", id);
                        return;
                    }
                    var phy = phys[id];
                    if(op == 0x2)//read
                    {
                        var phyRead = phy.Read((ushort)reg);
                        registers.PhyMaintenance &= 0xFFFF0000;//clear data
                        registers.PhyMaintenance |= phyRead;
                    }
                    else if(op == 0x1)//write
                    {
                        phy.Write((ushort)reg, (ushort)(value & 0xFF));
                    }
                    else//unknown 
                    {
                        this.Log(LogLevel.Warning, "Unknown phy op code 0x{0:X}", op);
                    }
                    if((registers.InterruptMask & (1u << 0)) == 0)
                    {
                        registers.InterruptStatus |= 1u << 0;
                        IRQ.Set();
                    }
                    return;  
                case Offset.SpecificAddress1Bottom:
                    registers.SpecificAddress1Bottom = value;
                    return;
                case Offset.SpecificAddress1Top:
                    registers.SpecificAddress1Top = value;
                    return;
                default:
                //this.LogUnhandledWrite(offset, value);
                    return;
                
                }
            }
            
        }

        private void SendFrames()
        {
            lock(sync)
            {
                txBufferDescriptor txBD = new txBufferDescriptor(machine.SystemBus);
                bool interrupt = false;
                List <byte> packet = new List<byte>();
            
            
                txBD.Fetch(registers.TxQueueBaseAddr);
                       
                while(!txBD.Used)
                {
                    while(!txBD.Last)
                    {
                        packet.AddRange(machine.SystemBus.ReadBytes(txBD.Word0, txBD.Length));
                        txBD.Used = true;
                        txBD.WriteBack();
                        if(txBD.Wrap)
                        {
                            registers.TxQueueBaseAddr = txBufferBase;
                        }
                        else
                        {
                            registers.TxQueueBaseAddr += 8;
                        }
                        txBD.Fetch(registers.TxQueueBaseAddr);
                    }
                    interrupt = false;
                    txBD.Used = true;
                
                    packet.AddRange(machine.SystemBus.ReadBytes(txBD.Word0, txBD.Length));
                
                    if((registers.DMAConfig & 1u << 11) != 0)//if checksum offload enable
                    {
                        if((packet[14] & 0xF0) == 0x40) //IP packet
                        {
                            ushort cksum;
                            IPHeaderLength = (ushort)((packet[14] & 0x0F) * 4);
                            if(packet[23] == 0x06) // TCP packet
                            {
                            
                                IPpacket tcpPacket = new IPpacket(IPHeaderLength, IPpacket.PacketType.TCP);
                                tcpPacket.ReadFromBuffer(packet.ToArray());
                                cksum = tcpPacket.GetChecksum();
                                cksum -= 1;
                                packet[MACHeaderLegth + IPHeaderLength + 16] = (byte)((cksum >> 8) & 0xFF);
                                packet[MACHeaderLegth + IPHeaderLength + 17] = (byte)((cksum) & 0xFF);
                            }
                            else if(packet[23] == 0x11) // UDP packet
                            {
                                IPpacket udpPacket = new IPpacket(IPHeaderLength, IPpacket.PacketType.UDP);
                                udpPacket.ReadFromBuffer(packet.ToArray());
                                cksum = udpPacket.GetChecksum();
                                cksum -= 1;
                                packet[MACHeaderLegth + IPHeaderLength + 6] = (byte)((cksum >> 8) & 0xFF);
                                packet[MACHeaderLegth + IPHeaderLength + 7] = (byte)((cksum) & 0xFF);
                            }
                        }
                    }

                    EthernetFrame frame;
                    if(!Misc.TryCreateFrameOrLogWarning(this, packet.ToArray(), out frame, !txBD.NoCRC))
                    {
                        continue;
                    }

                    this.Log(LogLevel.Noisy, "Sending packet length {0}", packet.ToArray().Length);
                    FrameReady?.Invoke(frame);

                    txBD.WriteBack();
                
                    if(txBD.Wrap)
                    {
                        registers.TxQueueBaseAddr = txBufferBase;
                    }
                    else
                    {
                        registers.TxQueueBaseAddr += 8;
                    }
                
                    registers.TxStatus |= 1u << 5; //tx complete
                    txBD.Fetch(registers.TxQueueBaseAddr);
                
                    if(txBD.Used)
                    {
                        registers.TxStatus |= 0x01;
                        if((registers.InterruptMask & (1u << 3)) == 0)
                        {
                            registers.InterruptStatus |= 1u << 3;
                            interrupt = true;
                        }
                    }
                
                    if((registers.InterruptMask & (1u << 7)) == 0)
                    {
                        registers.InterruptStatus |= 1u << 7;
                        interrupt = true;
                    
                    }
                
                    if(interrupt)
                    {
                        IRQ.Set();
                    }
            
                }
            }
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            lock(sync)
            {
                this.Log(LogLevel.Noisy, "Received packet len {0}", frame.Bytes.Length);
                if((registers.Control & (1u << 2)) == 0)
                {
                    this.Log(LogLevel.Debug, "Receiver not enabled, dropping frame.");
                    return;
                }

                if(!registers.IgnoreFCS && !EthernetFrame.CheckCRC(frame.Bytes))
                {
                    this.Log(LogLevel.Info, "Invalid CRC, packet discarded");
                    return;
                }

                bool interrupt = false;
                rxBufferDescriptor rxBD = new rxBufferDescriptor(machine.SystemBus);
                rxBD.Fetch(registers.RxQueueBaseAddr);
                if(!rxBD.Ownership)//if we could write data to this BD
                {
                    interrupt = false;
                    rxBD.Ownership = true;
                    rxBD.StartOfFrame = true;
                    rxBD.EndOfFrame = true;
                    rxBD.Length = (ushort)frame.Bytes.Length;
                    machine.SystemBus.WriteBytes(frame.Bytes, rxBD.BufferAddress);
                    rxBD.WriteBack();
                    if(rxBD.Wrap)
                    {
                        registers.RxQueueBaseAddr = rxBufferBase;
                    }
                    else
                    {
                        registers.RxQueueBaseAddr += 8;
                    }
                    registers.RxStatus |= 1u << 1;
                    if((registers.InterruptMask & (1u << 1)) == 0)
                    {
                        registers.InterruptStatus |= 1u << 1;
                        interrupt = true;
                    }
                    if(interrupt)
                    {
                        IRQ.Set();
                    }
                }
            }
        }

        public override void Reset()
        {
        }

        public void Start()
        {
            Resume();
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Dispose()
        {
        }

        public MACAddress MAC { get; set; }

        private void HandleError(int value)
        {
            this.Log(LogLevel.Warning, "Error");
        }

        public GPIO IRQ { get; private set; }

        private object sync;

        #region device registers

        private regs registers;

        private class regs
        {
            public uint Control = 0;
            public uint Config = 0x00080000;
            public uint Status = 4;
            // XEMACPS_NWSR_MDIOIDLE_MASK
            public uint DMAConfig = 0x00020784;
            public uint TxStatus = 0;
            public uint RxQueueBaseAddr = 0;
            public uint TxQueueBaseAddr = 0;
            public uint RxStatus = 0;
            public uint InterruptStatus = 0;
            public uint InterruptMask = 0xFFFFFFFF;
            public uint PhyMaintenance = 0;
            public uint SpecificAddress1Bottom = 0x01350a00;
            public uint SpecificAddress1Top = 0x302;
            public readonly uint ModuleId = 0x00020118;
            //module_id = 0x2(GEM), revision = 0x118
            public bool IgnoreFCS => (Config & 1u << 26) != 0;
    }

        private enum Offset : uint
        {
            NetControl = 0x00,
            NetConfig = 0x04,
            NetStatus = 0x08,
            DMAConfig = 0x10,
            TxStatus = 0x14,
            RxQueueBaseAddr = 0x18,
            TxQueueBaseAddr = 0x1C,
            RxStatus = 0x20,
            InterruptStatus = 0x24,
            InterruptEnable = 0x28,
            InterruptDisable = 0x2C,
            InterruptMask = 0x30,
            PhyMaintenance = 0x34,
            SpecificAddress1Bottom = 0x88,
            SpecificAddress1Top = 0x8C,
            ModuleId = 0xFC,
        }

        #endregion

        #region queue descriptors and data buffers

        private uint txBufferBase;
        private uint rxBufferBase;

        private class txBufferDescriptor
        {
            public txBufferDescriptor(SystemBus sysbus)
            {
                sbus = sysbus;
            }

            public uint Word0;
            public uint Word1;
            
            public bool Used;
            public bool Wrap;
            public bool Last;
            public ushort Length;
            public bool NoCRC;
            
            private uint ramAddr;
            private const uint noCrc = 0x10000;
            //location of this BD in ram
            private SystemBus sbus;

            public void Fetch(uint addr)
            {
                ramAddr = addr;
                
                Word0 = sbus.ReadDoubleWord(ramAddr);
                Word1 = sbus.ReadDoubleWord(ramAddr + 4);
                
                Used = (Word1 & 1u << 31) != 0;
                Wrap = (Word1 & 1u << 30) != 0;
                Last = (Word1 & 1u << 15) != 0;
                Length = (ushort)(Word1 & 0x3FFFu);
                NoCRC = (Word1 & 1u << 16) != 0;
                
            }

            public void WriteBack()
            {
                this.update();
                sbus.WriteDoubleWord(ramAddr, Word0);
                sbus.WriteDoubleWord(ramAddr + 4, Word1);
            }

            private void update()
            {
                var tempWord1 = Word1 & (~(1u << 31));
                tempWord1 |= Used ? (1u << 31) : 0;
                Word1 = tempWord1;
            }
        }

        
        private class rxBufferDescriptor
        {
            public rxBufferDescriptor(SystemBus sysbus)
            {
                sbus = sysbus;
            }

            public uint Word0;
            public uint Word1;
            
            public uint BufferAddress;
            public bool Wrap;
            public bool Ownership;
            public bool StartOfFrame;
            public bool EndOfFrame;

            public ushort Length;
            
            private uint ramAddr;
            //location of this BD in ram
            private SystemBus sbus;

            public void Fetch(uint addr)
            {
                ramAddr = addr;
                Word0 = sbus.ReadDoubleWord(addr);
                Word1 = sbus.ReadDoubleWord(addr + 4);
                
                BufferAddress = Word0 & (~(0x03u));
                Wrap = ((Word0 & (1u << 1)) != 0) ? true : false;
                Ownership = ((Word0 & (1u << 0)) != 0) ? true : false;
                StartOfFrame = ((Word1 & (1u << 14)) != 0) ? true : false;
                EndOfFrame = ((Word1 & (1u << 15)) != 0) ? true : false;
                Length = (ushort)(Word1 & 0x1FFF);
            }

            public void WriteBack()
            {
                this.update();
                sbus.WriteDoubleWord(ramAddr, Word0);
                sbus.WriteDoubleWord(ramAddr + 4, Word1);
            }

            private void update()
            {
                Word0 = BufferAddress;
                Word0 |= (Wrap ? 1u << 1 : 0u) | (Ownership ? 1u << 0 : 0u);
                
                var tmpWord1 = Word1 & (~(0xDFFFu));
                tmpWord1 |= (EndOfFrame ? 1u << 15 : 0) | (StartOfFrame ? 1u << 14 : 0) | ((uint)(Length & 0x1FFF));
                Word1 = tmpWord1;
            }
             
        }

        #endregion

        #region packets

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
                pseudoheader.FillFromBuffer(buffer);
                
                packet = new byte[buffer.Length - (MACHeaderLegth + IPHeaderLength) ];
                Array.Copy(buffer, MACHeaderLegth + IPHeaderLength, packet, 0, (buffer.Length - (MACHeaderLegth + IPHeaderLength)));
                if(packetType == PacketType.TCP)
                {
                    packet[16] = 0;
                    packet[17] = 0;
                }
                else if(packetType == PacketType.UDP)
                {
                    packet[6] = 0;
                    packet[7] = 0;
                }
                
            }

            private ushort CalculateChecksum(byte[] data)
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
                
                
                while((sum >> 16) != 0)
                {
                    sum = (sum >> 16) + (sum & 0xffff);
                }
                return (ushort)((~sum) + 1);
            }

            public ushort GetChecksum()
            {
                ushort cksum;
                
                checksumCalculationBase = new byte[packet.Length + pseudoheader.Length];
                
                Array.Copy(pseudoheader.ToArray(), 0, checksumCalculationBase, 0, pseudoheader.Length);
                Array.Copy(packet, 0, checksumCalculationBase, pseudoheader.Length, packet.Length);
                
                cksum = CalculateChecksum(checksumCalculationBase);
                return (ushort)(cksum);
                    
            }

            private class PseudoHeader
            {
                public void FillFromBuffer(byte[] buffer)
                {
                    sourceAddress = new byte[4];
                    destinationAddress = new byte[4];
                    Array.Copy(buffer, MACHeaderLegth + 12, sourceAddress, 0, 4);
                    Array.Copy(buffer, MACHeaderLegth + 16, destinationAddress, 0, 4);
                    protocol = buffer[MACHeaderLegth + 9];
                    packetLength = (ushort)(System.Net.IPAddress.HostToNetworkOrder((ushort)(buffer.Length - (MACHeaderLegth + IPHeaderLength))) >> 16);
                }

                public byte[] ToArray()
                {
                    byte[] arr = new byte[Length];
                    Array.Copy(sourceAddress, 0, arr, 0, 4);
                    Array.Copy(destinationAddress, 0, arr, 4, 4);
                    arr[8] = zeros;
                    arr[9] = protocol;
                    Array.Copy(BitConverter.GetBytes(packetLength), 0, arr, 10, 2);
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

        #endregion
             
    }
}

