//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public sealed class GaislerGPIO : BaseGPIOPort, IDoubleWordPeripheral, IGaislerAPB
    {
        public GaislerGPIO(Machine machine, int numberOfPorts, int numberOfInterrupts) : base(machine, numberOfPorts)
        {
            this.numberOfPorts = numberOfPorts;
            if(numberOfPorts < 2 || numberOfPorts > 32)
            {
                throw new RecoverableException("Port number has to be in [2, 32].");
            }
            interrupts = new GPIO[numberOfInterrupts];
            for(var i = 0; i < interrupts.Length; i++)
            {
                interrupts[i] = new GPIO();
            }
            //interruptMap = new int[numberOfInterrupts];
            registers = new regs();
            Reset();
        }

        public GPIO IRQ
        {
            get
            {
                return interrupts[0];
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Offset)offset)
            {
            case Offset.Data:
                this.ManagePins();
                return registers.Data;
            case Offset.Output:
                return registers.Output;
            case Offset.Direction:
                return registers.Direction;
            case Offset.InterruptMask:
                return registers.InterruptMask;
            case Offset.InterruptPolarity:
                return registers.InterruptPolarity;
            case Offset.InterruptEdge:
                return registers.InterruptEdge;
            case Offset.Bypass:
                return registers.Bypass;
            default:
                this.LogUnhandledRead(offset);
                return 0u;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Offset)offset)
            {
            case Offset.Data:
                return;
            case Offset.Output:
                registers.Output = value & 0xffff;
                this.ManagePins();
                return;
            case Offset.Direction:
                registers.Direction = value & 0xffff;
                this.ManagePins();
                return;
            case Offset.InterruptMask:
                registers.InterruptMask = value & 0xffff;
                return;
            case Offset.InterruptPolarity:
                registers.InterruptPolarity = value & 0xffff;
                return;
            case Offset.InterruptEdge:
                registers.InterruptEdge = value & 0xffff;
                return;
            case Offset.Bypass:
                registers.Bypass = value & 0xffff;
                return;
            default:
                this.LogUnhandledWrite(offset, value);
                return;
            }
        }

        private void ManagePins()
        {
            registers.Data = 0;
            foreach(var cn in Connections)
            {
                var id = cn.Key;
                if(id < 0)
                {
                    //HACK: This controller fails on negative
                    continue;
                }
                if(((registers.Output & 1u << id) != 0)&&((registers.Direction & 1u << id) != 0))
                {
                    cn.Value.Set();
                }
                else
                {
                    cn.Value.Unset();
                }
                if(State[id]&&((registers.Direction & 1u << id) == 0))
                {
                    registers.Data |= 1u << id;
                }
            }
        }
        /*public void Reset()
        {
            foreach(var interrupt in interrupts)
            {
                interrupt.Unset();
            }
            for(var i = 0; i < interruptMap.Length; i++)
            {
                interruptMap[i] = i;
            }
        }*/

        /*public void Connect(int sourceNumber, IGPIOReceiver destination, int destinationNumber)
        {
            interrupts[sourceNumber].Connect(destination, destinationNumber);
        }*/

        public uint GetVendorID()
        {
            return vendorID;
        }

        public uint GetDeviceID()
        {
            return deviceID; 
        }

        public uint GetInterruptNumber()
        {
            return 0;
        }

        public GaislerAPBPlugAndPlayRecord.SpaceType GetSpaceType()
        {
            return GaislerAPBPlugAndPlayRecord.SpaceType.APBIOSpace;
        }

        private readonly uint vendorID = 0x01;  // Aeroflex Gaisler
        private readonly uint deviceID = 0x01A; // GRLIB GRGPIO
        #pragma warning disable 0414
        private readonly int numberOfPorts;
        #pragma warning restore 0414
        private readonly GPIO[] interrupts;
        //private readonly int[] interruptMap;

        #region registers
        private regs registers;

        private class regs
        {
            public uint Data;
            public uint Output;
            public uint Direction;
            public uint InterruptMask;
            public uint InterruptPolarity;
            public uint InterruptEdge;
            public uint Bypass;
        }

        private enum Offset : uint
        {
            Data = 0x00,
            Output = 0x04,
            Direction = 0x08,
            InterruptMask = 0x0c,
            InterruptPolarity = 0x10,
            InterruptEdge = 0x14,
            Bypass = 0x18
        }
        #endregion
    }
}