//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.SPI
{
    public sealed class STM32SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IWordPeripheral, IDoubleWordPeripheral, IBytePeripheral, IKnownSize
    {
        public STM32SPI(Machine machine) : base(machine)
        {
            receiveBuffer = new Queue<byte>();
            IRQ = new GPIO();
            SetupRegisters();
            Reset();
        }

        public byte ReadByte(long offset)
        {
            // byte interface is there for DMA
            if(offset % 4 == 0)
            {
                return (byte)ReadDoubleWord(offset);
            }
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            if(offset % 4 == 0)
            {
                WriteDoubleWord(offset, (uint)value);
            }
            else
            {
                this.LogUnhandledWrite(offset, value);
            }
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)ReadDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteDoubleWord(offset, (uint)value);
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.Data:
                return HandleDataRead();
            default:
                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.Data:
                HandleDataWrite(value);
                break;
            default:
                registers.Write(offset, value);
                break;
            }
        }

        public override void Reset()
        {
            lock(receiveBuffer)
            {
                receiveBuffer.Clear();
            }
            registers.Reset();
        }

        public long Size
        {
            get
            {
                return 0x400;
            }
        }

        public GPIO IRQ
        {    
            get;
            private set;
        }

        private uint HandleDataRead()
        {
            IRQ.Unset();
            lock(receiveBuffer)
            {
                if(receiveBuffer.Count > 0)
                {
                    var value = receiveBuffer.Dequeue();
                    return value; // TODO: verify if Update should be called
                }
                this.Log(LogLevel.Warning, "Trying to read data register while no data has been received.");
                return 0;
            }
        }

        private void HandleDataWrite(uint value)
        {
            IRQ.Unset();
            lock(receiveBuffer)
            {
                var peripheral = RegisteredPeripheral;
                if(peripheral == null)
                {
                    this.Log(LogLevel.Warning, "SPI transmission while no SPI peripheral is connected.");
                    receiveBuffer.Enqueue(0x0);
                    return;
                }
                receiveBuffer.Enqueue(peripheral.Transmit((byte)value)); // currently byte mode is the only one we support
                this.NoisyLog("Transmitted 0x{0:X}, received 0x{1:X}.", value, receiveBuffer.Peek());
            }
            Update();
        }

        private void Update()
        {
            // TODO: verify this condition
            IRQ.Set(txBufferEmptyInterruptEnable.Value || rxBufferNotEmptyInterruptEnable.Value || txDmaEnable.Value || rxDmaEnable.Value);
        }

        private void SetupRegisters()
        {
            var control2 = new DoubleWordRegister(this);
            txBufferEmptyInterruptEnable = control2.DefineFlagField(7);
            rxBufferNotEmptyInterruptEnable = control2.DefineFlagField(6);
            txDmaEnable = control2.DefineFlagField(1);
            rxDmaEnable = control2.DefineFlagField(0, writeCallback: (_,__) => Update());

            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            { 
                { (long)Registers.Control1, new DoubleWordRegister(this).WithValueField(3,3, name:"Baud").WithFlag(2, name:"Master")
                        .WithFlag(8, name:"SSI").WithFlag(9, name:"SSM").WithFlag(6, changeCallback: (oldValue, newValue) => {
                    if(!newValue)
                    {
                        IRQ.Unset();
                    }
                }, name:"SpiEnable")},
                {(long)Registers.Status, new DoubleWordRegister(this, 2).WithFlag(1, FieldMode.Read, name:"TXE").WithFlag(0, FieldMode.Read, valueProviderCallback: _ => receiveBuffer.Count != 0 , name:"RXNE")},
                {(long)Registers.CRCPolynomial, new DoubleWordRegister(this, 7).WithValueField(0, 16, name:"CRCPoly") },
                {(long)Registers.I2SConfiguration, new DoubleWordRegister(this, 0).WithFlag(10, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (oldValue, newValue) => {
                    // write one to clear to keep this bit 0
                    if(newValue)
                    {
                        this.Log(LogLevel.Warning, "Trying to enable not supported I2S mode.");
                    }
                }, name:"I2SE")},
                { (long)Registers.Control2, control2 }
            };
            registers = new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection registers;
        private IFlagRegisterField txBufferEmptyInterruptEnable, rxBufferNotEmptyInterruptEnable, txDmaEnable, rxDmaEnable;

        private readonly Queue<byte> receiveBuffer;

        private enum Registers
        {
            Control1 = 0x0, // SPI_CR1,
            Control2 = 0x4, // SPI_CR2
            Status = 0x8, // SPI_SR
            Data = 0xC, // SPI_DR
            CRCPolynomial = 0x10, // SPI_CRCPR
            I2SConfiguration = 0x1C // SPI_I2SCFGR
        }
    }
}
