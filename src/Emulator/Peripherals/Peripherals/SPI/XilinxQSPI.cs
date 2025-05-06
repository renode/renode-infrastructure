//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.MTD;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using System.Collections.Specialized;
using System;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class XilinxQSPI : NullRegistrationPointPeripheralContainer<ISPIFlash>, IDoubleWordPeripheral
    {
        public XilinxQSPI(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            InnerReset();
        }

        public override void Reset()
        {
            InnerReset();
        }

        public uint ReadDoubleWord(long offset)
        {
            this.Log(LogLevel.Info, "Reading from offset {0:X}", offset);
            switch((Offset)offset)
            {
            case Offset.Config:
                return registers.Config.Get();
            case Offset.InterruptStatus:
                return registers.InterruptStatus;
                /*case Offset.InterruptEnable:
                return registers.InterruptEnable;
            case Offset.InterruptDisable:
                return registers.InterruptDisable;
            case Offset.Enable:
                return registers.Enable;
            case Offset.Delay:
                return registers.Delay;*/
            case Offset.ReceiveData:
                receiveFIFODataCount = 0;
                registers.InterruptStatus &= ~(1u << 4); //clear rx FIFO not empty
                return registers.ReceiveData;
                /*case Offset.SlaveIdleCount:
                return registers.SlaveIdleCount;
            case Offset.TransmitFifoThreshold:
                return registers.TransmitFifoThreshold;
            case Offset.ReceiveFifoThreshold:
                return registers.ReceiveFifoThreshold;
            case Offset.GPIO:
                return registers.GPIO;
            case Offset.LoopbackMasterClockDelayAdjustment:
                return registers.LoopbackMasterClockDelayAdjustment;
            case Offset.LinearQSPIConfig:
                return registers.LinearQSPIConfig;
            case Offset.LinearQSPIStatus:
                return registers.LinearQSPIStatus;
            case Offset.ModuleID:
                return registers.ModuleID;*/
            default:
                this.LogUnhandledRead(offset);
                break;
            }
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Info, "Writing to offset {0:X} value {1:X}", offset, value);
            switch((Offset)offset)
            {
            case Offset.Config:
                registers.Config.Set(value);
                if(registers.Config.ManualStartEnable && registers.Config.ManualStart)
                {
                    /*manual start*/
                    registers.Config.ManualStart = false;
                    sendCommand(registers.TransmitData, commandLength);
                    commandLength = 0;
                }
                break;
            case Offset.InterruptStatus:
                registers.InterruptStatus &= ~value;
                break;
            case Offset.InterruptEnable:
                registers.InterruptEnable |= value;
                break;
            case Offset.InterruptDisable:
                registers.InterruptEnable &= ~value;
                break;
            case Offset.Enable:
                registers.Enable = value;
                break;
            /*case Offset.Delay:
                return registers.Delay;*/
            case Offset.TransmitData0:
                registers.TransmitData = value;
                commandLength = 4;
                transferFIFODataCount += 4;
                break;
                /*case Offset.ReceiveData:
                return registers.ReceiveData;
            case Offset.SlaveIdleCount:
                return registers.SlaveIdleCount;
            case Offset.TransmitFifoThreshold:
                return registers.TransmitFifoThreshold;*/
            case Offset.ReceiveFifoThreshold:
                registers.ReceiveFifoThreshold = value;
                break;
                /*case Offset.GPIO:
                return registers.GPIO;
            case Offset.LoopbackMasterClockDelayAdjustment:
                return registers.LoopbackMasterClockDelayAdjustment;*/
            case Offset.TransmitData1:
                registers.TransmitData = value;
                commandLength = 1;
                transferFIFODataCount += 1;
                break;
            case Offset.TransmitData2:
                registers.TransmitData = value;
                commandLength = 2;
                transferFIFODataCount += 2;
                break;
            case Offset.TransmitData3:
                registers.TransmitData = value;
                commandLength = 3;
                transferFIFODataCount += 3;
                break;
                /*case Offset.LinearQSPIConfig:
                return registers.LinearQSPIConfig;
            case Offset.LinearQSPIStatus:
                return registers.LinearQSPIStatus;
            case Offset.ModuleID:
                return registers.ModuleID;*/
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
            checkInterrupt();
        }

        private void checkInterrupt()
        {
            var interrupt = false;
            if(transferFIFODataCount >= registers.TransmitFifoThreshold)
            {
                registers.InterruptStatus |= 1u << 3;
                if((registers.InterruptEnable & (1u << 3)) != 0)
                {
                    interrupt = true;
                }
            }
            if(receiveFIFODataCount >= registers.ReceiveFifoThreshold)
            {
                registers.InterruptStatus |= 1u << 4;
                if((registers.InterruptEnable & (1u<<4))!=0) //RX FIFO not empty
                {
                    interrupt = true;
                }
            }

            if(((registers.InterruptEnable & (1u << 2)) != 0) && writeOccured) //TX FIFO not full (always in emulation)
            {
                registers.InterruptStatus |= 1u << 2;
                interrupt = true;
                writeOccured = false;
            }
            if(interrupt)
            {
                this.Log(LogLevel.Noisy, "IRQ.SET");
                IRQ.Set();
            }
            else
            {
                this.Log(LogLevel.Noisy, "IRQ.UNSET");
                IRQ.Unset();
            }
        }

        private void sendCommand(uint command, uint length)
        {
            //TODO: Endianess 
            writeOccured = true;
            transferFIFODataCount = 0;
            this.Log(LogLevel.Warning, "Flash command {0:X}", command);
            switch((SPIFlashCommand)(command & 0xff))
            {
            case SPIFlashCommand.ReadID:
                //TODO: polarization 
                var ID = 0xffffffffu; //return 0xffffffff if there is no flash attached
                if(RegisteredPeripheral != null)
                {
                    ID = RegisteredPeripheral.ReadID();
                }
                registers.ReceiveData = ID;
                receiveFIFODataCount += 1;
                break;
            default:
                receiveFIFODataCount += 1; //XXX: ugly
                this.Log(LogLevel.Warning, "Unimplemented SPI Flash command {0:X}", command);
                break;
            }
        }

        private void InnerReset()
        {
            registers = new regsValues();
            writeOccured = false;
            commandLength = 0;
            receiveFIFODataCount = 0;
            transferFIFODataCount = 0;
        }

        /* variables */
        // Analysis disable RedundantDefaultFieldInitializer
        public GPIO IRQ { get; private set; }
        private bool writeOccured = false;
        private uint commandLength = 0;
        private uint receiveFIFODataCount = 0;
        private uint transferFIFODataCount = 0;
        // Analysis restore RedundantDefaultFieldInitializer

        /* internal registers */
        private class ConfigRegister
        {
            public ConfigRegister()
            {
                mode = BitVector32.CreateSection(1);
                var clkPol = BitVector32.CreateSection(1, mode);
                var clkPh = BitVector32.CreateSection(1, clkPol);
                var baud = BitVector32.CreateSection(7, clkPh);
                fifoWidth = BitVector32.CreateSection(3, baud);
                var refClk = BitVector32.CreateSection(1, fifoWidth);
                var reserved0 = BitVector32.CreateSection(1, refClk);
                var pcs = BitVector32.CreateSection(1, reserved0);
                var reserved1 = BitVector32.CreateSection(7, pcs);
                var manualCS = BitVector32.CreateSection(1, reserved1);
                manualStartEnable = BitVector32.CreateSection(1, manualCS);
                manualStart = BitVector32.CreateSection(1, manualStartEnable);
                var reserved2 = BitVector32.CreateSection(3, manualStart);
                var holdb = BitVector32.CreateSection(1,reserved2);
                var reserved3 = BitVector32.CreateSection(63, holdb);
                endian = BitVector32.CreateSection(1, reserved3);
            }

            public void Set(uint value)
            {
                registerValue = value;
                var register = new BitVector32((int)value);
                Mode = register[mode] != 0;
                FifoWidth = (byte)register[fifoWidth];
                ManualStartEnable = register[manualStartEnable] != 0;
                ManualStart = register[manualStart] != 0;
                Endian = register[endian] != 0;
            }

            public uint Get()
            {
                return registerValue;
            }

            private BitVector32.Section mode;
            private BitVector32.Section fifoWidth;
            private BitVector32.Section manualStartEnable;
            private BitVector32.Section manualStart;
            private BitVector32.Section endian;
            private uint registerValue = 0x80020000;

            // Analysis disable RedundantDefaultFieldInitializer
            public bool Mode = false;
            public byte FifoWidth = 0;
            public bool ManualStartEnable = false;
            public bool ManualStart = false;
            public bool Endian = false;
            // Analysis restore RedundantDefaultFieldInitializer
        }

        private regsValues registers;
        private class regsValues
        {
            public regsValues()
            {
                Config = new ConfigRegister();
            }
            public ConfigRegister Config;
            // Analysis disable RedundantDefaultFieldInitializer
            public uint InterruptStatus = 0x00000000;
            public uint InterruptEnable = 0x00000000;
            public uint InterruptMask = 0x00000000;
            public uint Enable = 0x00000000;
            public uint Delay = 0x00000000;
            public uint TransmitData = 0x00000000;
            public uint ReceiveData = 0x00000000;
            public uint SlaveIdleCount = 0x000000FF;
            public uint TransmitFifoThreshold = 0x00000001;
            public uint ReceiveFifoThreshold = 0x00000001;
            public uint GPIO = 0x00000001;
            public uint LoopbackMasterClockDelayAdjustment = 0x00000033;
            public uint LinearQSPIConfig = 0x07A002EB;
            public uint LinearQSPIStatus = 0x00000000;
            public uint ModuleID = 0x01090101;
            // Analysis restore RedundantDefaultFieldInitializer
        }

        private enum Offset
        {
            Config = 0x00,
            InterruptStatus = 0x04,
            InterruptEnable = 0x08,
            InterruptDisable = 0x0C,
            InterruptMask = 0x10,
            Enable = 0x14,
            Delay = 0x18,
            TransmitData0 = 0x1C,
            ReceiveData = 0x20,
            SlaveIdleCount = 0x24,
            TransmitFifoThreshold = 0x28,
            ReceiveFifoThreshold = 0x2C,
            GPIO = 0x30,
            LoopbackMasterClockDelayAdjustment = 0x38,
            TransmitData1 = 0x80,
            TransmitData2 = 0x84,
            TransmitData3 = 0x88,
            LinearQSPIConfig = 0xA0,
            LinearQSPIStatus = 0xA4,
            ModuleID = 0xFC
        }
    }
}

