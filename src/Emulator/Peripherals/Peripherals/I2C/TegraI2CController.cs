//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class TegraI2CController : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral
    {

        public TegraI2CController(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
        }

        public virtual uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.Config:
                return config;
            case Registers.Status:
                return imInUse ? 1 << 8 : 0u;
            case Registers.SlaveConfig:
                return slaveConfig;
            case Registers.SlaveAddress1:
                return slaveAddress1;
            case Registers.SlaveAddress2:
                return slaveAddress2;
            case Registers.TxFifo:
                return 0; //very confusing manual, 0 in other sources
            case Registers.RxFifo:
                if(rxQueue.Count == 0)
                {
                    SetInterrupt(Interrupts.RxFifoUnderflow);
                    return 0;
                }
                var value = 0u;
                for(var i = 0; i < 32 && rxQueue.Count > 0; i += 8)
                {
                    value |= (uint)(rxQueue.Dequeue() << i);
                }
                if(rxQueue.Count == 0)
                {
                    ClearInterrupt(Interrupts.RxFifoDataReq);
                }
                if(payloadDone < payloadSize)
                {
                    PrepareRead();
                }
                return value;
            case Registers.PacketTransferStatus:
                return packetTransferStatus;
            case Registers.FifoControl:
                return fifoControl;
            case Registers.FifoStatus:
                return (uint)(((rxQueue.Count + 3) / 4) | (8 << 4));
            case Registers.InterruptMask:
                return interruptMask;
            case Registers.InterruptStatus:
                return interruptStatus;
            case Registers.ClockDivisor:
                return clockDivisor;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.Config:
                config = value;
                break;
            case Registers.SlaveConfig:
                slaveConfig = value;
                break;
            case Registers.SlaveAddress1:
                slaveAddress1 = value;
                break;
            case Registers.SlaveAddress2:
                slaveAddress2 = value;
                break;
            case Registers.TxFifo:
                TransferData(value);
                break;
            case Registers.FifoControl:
                if((value & (1 << 1)) != 0)
                { //tx flush
                    mode = Mode.FirstHeader;
                    ClearInterrupt(Interrupts.TxFifoOverflow);
                }
                if((value & (1 << 0)) != 0)
                { //rx flush
                    rxQueue.Clear();
                    ClearInterrupt(Interrupts.RxFifoUnderflow);
                }
                fifoControl = value & 0xFC; //flush rx and tx fifos, both are cleared, but they should generate an interrupt
                break;
            case Registers.InterruptMask:
                interruptMask = (value & 0x6f);
                Update();
                break;
            case Registers.InterruptStatus:
                interruptStatus &= (~value | (1u << (int)Interrupts.RxFifoDataReq) | (1u << (int)Interrupts.TxFifoDataReq)); //last two bytes cannot be cleared
                Update();
                break;
            case Registers.ClockDivisor:
                clockDivisor = value;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public override void Reset()
        {
            config = 0;
            interruptMask = 0;
            interruptStatus = 0;
            mode = Mode.FirstHeader;
            payloadDone = 0;
            payloadSize = 0;
        }

        private void TransferData(uint value)
        {
            this.Log(LogLevel.Debug, "TransferData(0x{0:X}) in mode {1}.", value, mode);
            switch(mode)
            {
            case Mode.FirstHeader:
                packetTransferStatus = value & (0xFF << 16); //packet ID
                mode = Mode.SecondHeader;
                break;
            case Mode.SecondHeader:
                payloadSize = (value & 0x7FF) + 1;
                this.Log(LogLevel.Debug, "Payloald size: {0}.", payloadSize);
                payloadDone = 0;
                mode = Mode.HeaderSpecific;
                break;
            case Mode.HeaderSpecific:
                imInUse = true;
                enableInterruptAfterPacket = (value & (1 << 17)) != 0;
                slaveAddressForPacket = (byte)((value >> 1) & 0x7F);

                II2CPeripheral device;
                if(!TryGetByAddress(slaveAddressForPacket, out device))
                {
                    this.Log(LogLevel.Debug, "Not found {0}", slaveAddressForPacket);
                    SetInterrupt(Interrupts.NoACK);
                    return;
                }
                if((value & (1 << 19)) != 0) //read
                {
                    this.Log(LogLevel.Debug, "Will read from 0x{0:X}.", slaveAddressForPacket);
                    PrepareRead();
                }
                else //write
                {
                    this.Log(LogLevel.Debug, "Will write to 0x{0:X}.", slaveAddressForPacket);
                    mode = Mode.Payload;
                }
                break;
            case Mode.Payload:
                var bytesSent = 0;
                //payloadSize might be > 4
                while(payloadDone < payloadSize && bytesSent++ < 4)
                {
                    this.Log(LogLevel.Noisy, "Writing 0x{0:X}", value);
                    packet.Add((byte)(value & 0xFF));
                    value >>= 8;
                    payloadDone++;
                    packetTransferStatus = (uint)((packetTransferStatus & ~0xFFF0) | (payloadDone << 4));
                }
                SetInterrupt(Interrupts.TxFifoDataReq);
                if(payloadDone == payloadSize)
                {
                    this.Log(LogLevel.Noisy, "Writing done, {0} bytes.", payloadDone);
                    GetByAddress(slaveAddressForPacket).Write(packet.ToArray());
                    packet.Clear();
                    FinishTransfer();
                }
                Update();
                break;
            }
        }

        private void FinishTransfer()
        {
            imInUse = false;
            packetTransferStatus |= (1 << 24);
            mode = Mode.FirstHeader;
            if(enableInterruptAfterPacket)
            {
                SetInterrupt(Interrupts.PacketXferComplete, Interrupts.AllPacketsXferComplete);
            }
        }

        private void PrepareRead()
        {
            var packet = GetByAddress(slaveAddressForPacket).Read();
            foreach(var item in packet)
            {
                rxQueue.Enqueue(item);
                payloadDone++;
            }
            if(packet.Count() > 0)
            {
                SetInterrupt(Interrupts.RxFifoDataReq);
            }
            if(payloadDone == payloadSize)
            {
                FinishTransfer();
            }
        }

        public GPIO IRQ{ get; private set; }

        private void Update()
        {
            if((interruptStatus & (interruptMask | (1 << (int)Interrupts.PacketXferComplete))) > 0)
            {
                this.NoisyLog("Irq set");
                IRQ.Set();
            }
            else
            {
                this.NoisyLog("Irq unset");
                IRQ.Unset();
            }
        }

        private void ClearInterrupt(params Interrupts[] interrupt)
        {
            foreach(var item in interrupt)
            {
                interruptStatus &= (uint)~(1 << (int)item);
            }
            Update();
        }


        private void SetInterrupt(params Interrupts[] interrupt)
        {
            foreach(var item in interrupt)
            {
                interruptStatus |= (uint)(1 << (int)item);
            }
            Update();
        }

        private uint config;
        private uint slaveConfig;
        private uint slaveAddress1;
        private uint slaveAddress2;
        private uint fifoControl;
        private uint interruptMask;
        private uint interruptStatus;
        private uint clockDivisor;
        private uint packetTransferStatus;
        private Mode mode;
        private uint payloadSize;
        private uint payloadDone;
        private bool enableInterruptAfterPacket;
        private byte slaveAddressForPacket;
        private bool imInUse;

        private List<byte> packet = new List<byte>();
        private Queue<byte> rxQueue = new Queue<byte>();

        private enum Mode
        {
            FirstHeader,
            SecondHeader,
            HeaderSpecific,
            Payload
        }

        private enum Interrupts
        {
            RxFifoDataReq = 0x0,
            TxFifoDataReq = 0x1,
            ArbitrationLost = 0x2,
            NoACK = 0x3,
            RxFifoUnderflow = 0x4,
            TxFifoOverflow = 0x5,
            AllPacketsXferComplete = 0x6,
            PacketXferComplete = 0x7
        }

        private enum Registers
        {
            Config = 0x0,
            Status = 0x1C,
            SlaveConfig = 0x20,
            SlaveAddress1 = 0x2C,
            SlaveAddress2 = 0x30,
            TxFifo = 0x50,
            RxFifo = 0x54,
            PacketTransferStatus = 0x58,
            FifoControl = 0x5C,
            FifoStatus = 0x60,
            InterruptMask = 0x64,
            InterruptStatus = 0x68,
            ClockDivisor = 0x6C,


        }
    }
}

