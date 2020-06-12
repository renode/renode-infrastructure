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
using Antmicro.Renode.Utilities;
using System.Collections.Generic;
using Antmicro.Renode.Network;

namespace Antmicro.Renode.Peripherals.Network
{
    [AllowedTranslations(AllowedTranslation.ByteToWord)]
    public class SMC91X :  IKnownSize, IMACInterface,  IWordPeripheral, IDoubleWordPeripheral
    {
        public SMC91X()
        {
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            IRQ = new GPIO();
            Reset();
        }

        public void Reset()
        {
            IRQ.Unset();
            memoryBuffer = new MemoryRegion[NumberOfPackets];
            for(var i = 0; i < memoryBuffer.Length; ++i)
            {
                memoryBuffer[i] = new MemoryRegion();
            }
           
            rxFifo.Clear();
            txFifo.Clear();
            sentFifo.Clear();

            currentBank = Bank.Bank0;
            transmitControl = 0x0000;
            receiveControl = 0x0000;
            configuration = 0xA0B1;
            generalPurposeRegister = 0x0000;
            control = 0x1210;
            packetNumber = 0x00;
            allocationResult = 0x0;
            pointer = 0x0000;
            interruptMask = 0x0;
            interruptStatus = TxEmptyInterrupt;
            earlyReceive = 0x001f;
            Update();
        }

        public GPIO IRQ { get; private set; }

        public event Action<EthernetFrame> FrameReady;

        private ushort ReadBank0(long offset)
        {
            ushort value = 0;
            switch((Bank0Register)offset)
            {
            case Bank0Register.TransmitControl:
                value = transmitControl;
                break;
            case Bank0Register.EthernetProtocolStatus:
                value = 0x40 << 8;
                break;
            case Bank0Register.ReceiveControl: 
                value = receiveControl;
                break;
            case Bank0Register.MemoryInformation:
                value = (ushort)(((memoryBuffer.Length - rxFifo.Count) << 8) | memoryBuffer.Length);
                break;
            }
            return value;
        }
       
        private ushort ReadBank1(long offset)
        {
            ushort value = 0;
            switch((Bank1Register)offset)
            {
            case Bank1Register.Configuration:
                value = configuration;
                break;
            case Bank1Register.IndividualAddress0:
            case Bank1Register.IndividualAddress2:
            case Bank1Register.IndividualAddress4: 
                value = (ushort)((MAC.Bytes[offset - 3] << 8) | MAC.Bytes[offset - 4]);
                break;
            case Bank1Register.GeneralPurposeRegister:
                value = generalPurposeRegister;
                break;
            case Bank1Register.ControlRegister:
                value = control;
                break;
            }
            return value;
        }

        private ushort ReadBank2(long offset)
        {
            ushort value = 0;
            switch((Bank2Register)offset)
            {
            case Bank2Register.PacketNumber:
                value = (ushort)((allocationResult << 8) | packetNumber);
                break;
            case Bank2Register.FIFOPorts:
                byte low, high;
                if(sentFifo.Count == 0)
                {
                    low = FIFOEmpty;
                }
                else
                {
                    low = sentFifo.Peek();
                }
                if(rxFifo.Count == 0)
                {
                    high = FIFOEmpty;
                }
                else
                {
                    high = rxFifo.Peek();
                }
                value = (ushort)((high << 8) | low);
                break;
            case Bank2Register.Pointer:
                value = pointer;
                break;
            case Bank2Register.Data0:
            case Bank2Register.Data1:
                value = (ushort)(GetData(offset) | (GetData(offset + 1) << 8));
                break;
            case Bank2Register.InterruptStatus:
                value = (ushort)((interruptMask << 8) | interruptStatus);
                break;
            }
            return value;
        }

        private byte GetData(long offset)
        {
            int p;
            byte n;
            
            if((pointer & ReceivePointer) != 0)
            {
                n = rxFifo.Peek();
            }
            else
            {
                n = packetNumber;
            }
            p = pointer & PointerMask;
            if((pointer & AutoIncrementPointer) != 0)
            {
                pointer = (ushort)((pointer & ~PointerMask) | ((pointer + 1) & PointerMask));
            }
            else
            {
                p += (int)(offset & 3);
            }
            return memoryBuffer[n].Data[p];
        }

        private ushort ReadBank3(long offset)
        {
            ushort value = 0;
            switch((Bank3Register)offset)
            {
            case Bank3Register.ManagementInterface:
                value = 0x3330;
                break;
            case Bank3Register.Revision:
                value = 0x3392; //According to datasheet
                break;
            case Bank3Register.ReceiveDiscard:
                value = earlyReceive;
                break;
            }
            return value;
        }

        private void WriteBank0(long offset, ushort value)
        {
            switch((Bank0Register)offset)
            {
            case Bank0Register.TransmitControl:
                transmitControl = value;
                break;
            case Bank0Register.ReceiveControl:
                receiveControl = value;
                if((receiveControl & SoftwareReset) != 0)
                {
                    Reset();
                }
                break;
            }

        }

        private void WriteBank1(long offset, ushort value)
        {
            switch((Bank1Register)offset)
            {
            case Bank1Register.Configuration:
                configuration = value;
                break;
            case Bank1Register.GeneralPurposeRegister:
                generalPurposeRegister = value;
                break;
            case Bank1Register.ControlRegister:
                control = (ushort)(value & ~3); //EEPROM registers not implemented
                break;
            }
        }
        
        private void WriteBank2(long offset, ushort value)
        {
            switch((Bank2Register)offset)
            {
            case Bank2Register.MMUCommand:
                ExecuteMMUCommand((byte)(value.LoByte() >> 5));
                break;
            case Bank2Register.PacketNumber:
                packetNumber = value.LoByte(); //only lower byte writable
                break;
            case Bank2Register.Pointer:
                pointer = value;
                break;
            case Bank2Register.Data0:
            case Bank2Register.Data1:
                SetData(offset, value.LoByte());
                SetData(offset + 1, value.HiByte());
                break;
            case Bank2Register.InterruptStatus: //Acknowledge interrupts
                interruptStatus = (byte)(interruptStatus & ~(value.LoByte() & WritableInterrupts));
                if((value & TxInterrupt) != 0)
                {
                    if(sentFifo.Count == 0)
                    {
                        return;
                    }
                    sentFifo.Dequeue();
                }              
                if(interruptMask != value.HiByte())
                {
                    interruptMask = value.HiByte();
                    IRQ.Set(false);
                }
                Update();
                break;
            }
        }

        private void ExecuteMMUCommand(byte value)
        {
            switch((MMUCommand)value)
            {
            case MMUCommand.Noop:
                break;
            case MMUCommand.AllocateForTX:
                allocationResult = AllocationFailed;
                interruptStatus = (byte)(interruptStatus & ~AllocationSuccessfulInterrupt);
                Update();
                AllocateForTx();
                break;
            case MMUCommand.ResetMMU:
                foreach(var region in memoryBuffer)
                {
                    region.IsAllocated = false;
                }
                txFifo.Clear();
                sentFifo.Clear();
                rxFifo.Clear();
                allocationResult = AllocationFailed;
                break;
            case MMUCommand.RemoveFromRxFifo:
                PopRxFifo();
                break;
            case MMUCommand.RemoveFromRxFifoAndRelease:
                if(rxFifo.Count > 0)
                {
                    memoryBuffer[rxFifo.Peek()].IsAllocated = false;
                }
                PopRxFifo();
                break;
            case MMUCommand.ReleasePacket:
                memoryBuffer[packetNumber].IsAllocated = false;
                break;
            case MMUCommand.EnqueuePacketIntoTxFifo:
                txFifo.Enqueue(packetNumber);
                Transmit();    
                break;
            case MMUCommand.ResetTxFifos:
                txFifo.Clear();
                sentFifo.Clear();
                break;
            }
        }

        private void SetData(long offset, byte value)
        {
            byte n;
            
            if((pointer & ReceivePointer) != 0)
            {
                n = rxFifo.Peek();
            }
            else
            {
                n = packetNumber;
            }
            int p = pointer & PointerMask;
            if((pointer & AutoIncrementPointer) != 0)
            {
                pointer = (ushort)((pointer & ~PointerMask) | ((pointer + 1) & PointerMask));
            }
            else
            {
                p += (int)(offset & 3);
            }
            memoryBuffer[n].Data[p] = value;
        }
              
        public ushort ReadWord(long offset)
        {
            lock(lockObj)
            {
                if(offset == (byte)Bank.BankSelectRegister)
                {
                    return (ushort)((0x33 << 8) | ((byte)currentBank));
                }
                switch(currentBank)
                {
                case Bank.Bank0:
                    return ReadBank0(offset);
                case Bank.Bank1:
                    return ReadBank1(offset);
                case Bank.Bank2:
                    return ReadBank2(offset);
                case Bank.Bank3:
                    return ReadBank3(offset);
                }
            }
            return 0;
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(lockObj)
            {
                uint value;
                value = (uint)ReadWord(offset);
                value |= (uint)ReadWord(offset + 2) << 16;
                return value;
            }
        }

        public void WriteWord(long offset, ushort value)
        {
            lock(lockObj)
            {
                if(offset == 14)
                {
                    currentBank = (Bank)(value & 7);
                    return;
                }
                switch(currentBank)
                {
                case Bank.Bank0:
                    WriteBank0(offset, value);
                    break;
                case Bank.Bank1:
                    WriteBank1(offset, value);
                    break;
                case Bank.Bank2:
                    WriteBank2(offset, value);
                    break;
                case Bank.Bank3:
                    //Not implemented
                    break;
                }
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(lockObj)
            {
                //32b write to 0xC in fact writes to bank select
                if(offset != 0xc)
                {
                    WriteWord(offset, (ushort)(value & 0xffff));
                }
                WriteWord(offset + 2, (ushort)(value >> 16));
                return;
            }
        }
       
        public long Size
        {
            get
            {
                return 0x10000;
            }
        }

        public MACAddress MAC { get; set; }

        public byte Transmit()
        {
            lock(lockObj)
            {
                int len;
                byte whichPacket;

                if((transmitControl & TransmitEnabled) == 0)
                {
                    return 0;
                }
                if(txFifo.Count == 0)
                {
                    return 0;
                }
                while(txFifo.Count > 0)
                {
                    whichPacket = txFifo.Dequeue();
                    var currentBuffer = memoryBuffer[whichPacket];
                    len = currentBuffer.Data[2];
                    len |= currentBuffer.Data[3] << 8;
                    len -= 6;
              
                    byte [] indata = new byte[len];

                    for(int j=0; j<len; j++)
                    {
                        indata[j] = currentBuffer.Data[j + 4];
                    }

                    if((control & ControlAutorelease) != 0)
                    {
                        currentBuffer.IsAllocated = false;
                    }
                    else
                    {
                        sentFifo.Enqueue((byte)whichPacket);
                    }
                    if(Misc.TryCreateFrameOrLogWarning(this, indata, out var frame, addCrc: true))
                    {
                        FrameReady?.Invoke(frame);
                    }
                }
                Update();
                return 0;
            }
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            lock(lockObj)
            {
                this.NoisyLog("Received frame on MAC {0}. Frame destination MAC is {1}", this.MAC.ToString(), frame.DestinationMAC);
                var size = frame.Bytes.Length;
                var isEven = (size & 1) == 0;
                if((receiveControl & ReceiveEnabled) == 0 || (receiveControl & SoftwareReset) != 0)
                {
                    //Drop if reset is on or receiving is not enabled.
                    return;
                }
                var packetSize = Math.Max(64, size & ~1);
                //64 is the minimal length
                packetSize += 6;
                var withCRC = (receiveControl & StripCRC) == 0;
                if(withCRC)
                {
                    packetSize += 4;
                }
                if(packetSize > MaxPacketSize)
                {
                    //Maybe we should react to overruns. Now we just drop.
                    return;
                }
                byte whichPacket;
                if(!TryAllocatePacket(out whichPacket))
                {
                    return;
                }
                rxFifo.Enqueue(whichPacket);
                var status = 0;
                if(size > 1518)
                {
                    status |= 0x0800;
                }
                if(!isEven)
                {
                    status |= 0x1000;
                }
                var currentBuffer = memoryBuffer[whichPacket];
                currentBuffer.Data[0] = (byte)(status & 0xff);
                currentBuffer.Data[1] = (byte)(status >> 8);
                currentBuffer.Data[2] = (byte)(packetSize & 0xff);
                currentBuffer.Data[3] = (byte)(packetSize >> 8);
                var frameBytes = frame.Bytes;
                for(int i = 0; i < (size & ~1); i++)
                {
                    currentBuffer.Data[4 + i] = frameBytes[i];
                }
                //Pad with 0s
                if(size < 64)
                {
                    var pad = 64 - size;
                    if(!isEven)
                    {
                        for(int i = 0; i < pad; i++)
                        {
                            currentBuffer.Data[4 + i + size] = 0;
                        }
                    }
                    else
                    {
                        for(int i = 0; i < pad; i++)
                        {
                            currentBuffer.Data[4 + i + size + 1] = 0;
                        }
                    }
                    size = 64;
                }
                if(withCRC)
                {
                    if(!EthernetFrame.CheckCRC(frame.Bytes))
                    {
                        this.Log(LogLevel.Info, "Invalid CRC, packet discarded");
                        return;
                    }
                }
                if(!isEven)
                {
                    //TODO: For a short, odd-length packet, will it work? Should it not be written before?
                    currentBuffer.Data[packetSize - 2] = frameBytes[size - 1];
                    currentBuffer.Data[packetSize - 1] = 0x60;
                }
                else
                {
                    currentBuffer.Data[packetSize - 1] = 0x40;
                }
                interruptStatus |= RxInterrupt;
                Update();
            }
        }
  
        public void Update()
        {
            lock(lockObj)
            {
                if(txFifo.Count == 0)
                {
                    interruptStatus |= TxEmptyInterrupt;
                }
                if(sentFifo.Count != 0)
                {
                    interruptStatus |= TxInterrupt;
                }
                if((interruptMask & interruptStatus) != 0)
                {
                    IRQ.Set(true);
                }
            }
        }

        public bool TryAllocatePacket(out byte regionNumber)
        {
            lock(lockObj)
            {
                for(regionNumber = 0; regionNumber < memoryBuffer.Length; regionNumber++)
                {
                    if(!memoryBuffer[regionNumber].IsAllocated)
                    {
                        memoryBuffer[regionNumber].IsAllocated = true;
                        return true;
                    }
                }
                regionNumber = AllocationFailed; //AllocationFailed is used as a flag in a register
                return false;
            }
        }

        public void AllocateForTx()
        {
            lock(lockObj)
            {
                if(TryAllocatePacket(out allocationResult))
                {
                    interruptStatus |= AllocationSuccessfulInterrupt;
                    Update();
                }
            }
        }

        public  void PopRxFifo()
        {
            lock(lockObj)
            {
                if(rxFifo.Count != 0)
                {
                    rxFifo.Dequeue();
                }
                if(rxFifo.Count != 0) //count changes after Dequeue!
                {
                    interruptStatus |= RxInterrupt;
                }
                else
                {
                    interruptStatus = (byte)(interruptStatus & ~RxInterrupt);
                }
                Update();
            }
        }

        private enum Bank
        {
            Bank0 = 0x0,
            Bank1 = 0x1,
            Bank2 = 0x2,
            Bank3 = 0x3,
            BankSelectRegister = 0xE
        }

        private enum Bank0Register : byte
        {
            TransmitControl             = 0x0,
            EthernetProtocolStatus      = 0x2,
            ReceiveControl              = 0x4,
            Counter                     = 0x6,
            MemoryInformation           = 0x8,
            PHYControl                  = 0xA
        }

        private enum Bank1Register : byte
        {
            Configuration               = 0x0,
            BaseAddress                 = 0x2,
            IndividualAddress0          = 0x4,
            IndividualAddress2          = 0x6,
            IndividualAddress4          = 0x8,
            GeneralPurposeRegister      = 0xA,
            ControlRegister             = 0xC
        }

        private enum Bank2Register : byte
        {
            MMUCommand                  = 0x0,
            PacketNumber                = 0x2,
            FIFOPorts                   = 0x4,
            Pointer                     = 0x6,
            Data0                       = 0x8,
            Data1                       = 0xA,
            InterruptStatus             = 0xC
        }

        private enum Bank3Register : byte
        {
            MulticastTable0             = 0x0,
            MulticastTable2             = 0x2,
            MulticastTable4             = 0x4,
            MulticastTable6             = 0x6,
            ManagementInterface         = 0x8,
            Revision                    = 0xA,
            ReceiveDiscard              = 0xC

        }

        private enum MMUCommand : byte
        {
            Noop                        = 0x0,
            AllocateForTX               = 0x1,
            ResetMMU                    = 0x2,
            RemoveFromRxFifo            = 0x3,
            RemoveFromRxFifoAndRelease  = 0x4,
            ReleasePacket               = 0x5,
            EnqueuePacketIntoTxFifo     = 0x6,
            ResetTxFifos                = 0x7
        }

        private class MemoryRegion
        {
            private bool _allocated;

            public bool IsAllocated
            {
                get
                {
                    return _allocated;
                }
                set
                {
                    if(!value)
                    {
                        Data = new byte[MaxPacketSize]; 
                    }
                    _allocated = value;
                }
            }

            public byte[] Data = new byte[MaxPacketSize];
        }

        // Bank select register
        private Bank currentBank;
        // Bank 0 registers
        private ushort transmitControl;
        private ushort receiveControl;
        // Bank 1 registers
        private ushort configuration;
        private ushort generalPurposeRegister;
        private ushort control;
        // Bank 2 registers
        private byte packetNumber;
        private byte allocationResult;
        private ushort pointer;
        private byte interruptStatus;
        private byte interruptMask;
        // Bank 3 registers
        private ushort earlyReceive;
        private Queue<byte> rxFifo = new Queue<byte>();
        private Queue<byte> txFifo = new Queue<byte>();
        private Queue<byte> sentFifo = new Queue<byte>();
        private MemoryRegion[] memoryBuffer;
        private object lockObj = new object();

        private const byte NumberOfPackets = 4;
        private const ushort MaxPacketSize = 2048;
        private const ushort ControlAutorelease = 0x0800;
        private const ushort TransmitEnabled = 0x0001;
        private const ushort ReceiveEnabled = 0x0100;
        private const ushort SoftwareReset = 0x8000;
        private const ushort StripCRC = 0x0200;
        private const ushort AutoIncrementPointer = 0x4000;
        private const ushort ReceivePointer = 0x8000;
        private const ushort PointerMask = 0x07FF;
        private const byte AllocationFailed = 0x90;
        private const byte FIFOEmpty = 0x80;
        private const byte RxInterrupt = 0x01;
        private const byte TxInterrupt = 0x02;
        private const byte TxEmptyInterrupt = 0x04;
        private const byte AllocationSuccessfulInterrupt = 0x08;
        private const byte RxOverrunInterrupt = 0x10;
        private const byte EthernetProtocolInterrupt = 0x20;
        private const byte PHYInterrupt = 0x80;
        private const byte WritableInterrupts = 0xDE;
    }
}
