//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class MB85RC1MT : IGPIOReceiver
    {
        public MB85RC1MT()
        {
            Reset();
        }

        public void Reset()
        {
            address = 0x0;
            transmissionPending = false;
            writeProtected = false;
        }

        public void OnGPIO(int number, bool value)
        {
            if(number == 0)
            {
                writeProtected = value;
            }
            else
            {
                this.Log(LogLevel.Warning, "Invalid gpio number {0}", number);
            }
        }

        internal void Write(byte[] data, bool addr16)
        {
            this.Log(LogLevel.Noisy, "Write {0}", data.Select(x => x.ToString("X")).Aggregate((x, y) => x + " " + y));

            int writeDataIdx = 0;
            if(!transmissionPending)
            {
                transmissionPending = true;
                if(data.Length < 2)
                {
                    this.Log(LogLevel.Warning, "Received message is too short");
                    return;
                }
                address = (uint)((data[0] << 8) | data[1]);
                if(addr16)
                {
                    address |= 1 << 16;
                }
                writeDataIdx = 2;
            }
            if(!writeProtected)
            {
                for(int i = writeDataIdx; i < data.Length; i++)
                {
                    memory[address] = data[i];
                    IncreaseAddress();
                }
            }
            else
            {
                this.Log(LogLevel.Warning, "Attempt to write to write protected module");
            }
        }

        internal byte[] Read(int count = 0)
        {
            this.Log(LogLevel.Noisy, "Read {0}", count);

            byte[] buf = new byte[count];
            for(int i = 0; i < count && address < memory.Length; i++)
            {
                buf[i] = memory[address];
                IncreaseAddress();
            }
            return buf;
        }

        internal void FinishTransmission()
        {
            transmissionPending = false;
            //address is not reset, mb85rc1mt allows subsequent reads to use address of last transaction + 1
        }

        private void IncreaseAddress()
        {
            address = (uint)((address + 1) % memory.Length);
        }

        private readonly byte[] memory = new byte[8 * 128 * 1 << 10];
        private uint address;
        private bool writeProtected;
        private bool transmissionPending;
    }

    public class MB85RC1MTI2CRelay : II2CPeripheral
    {
        public MB85RC1MTI2CRelay(MB85RC1MT mb85rc1mt, bool addr16)
        {
            this.mb85rc1mt = mb85rc1mt;
            this.addr16 = addr16;
        }

        public void Reset()
        {
        }

        public void Write(byte[] data)
        {
            mb85rc1mt.Write(data, addr16);
        }

        public byte[] Read(int count = 0)
        {
            return mb85rc1mt.Read(count);
        }

        public void FinishTransmission()
        {
            mb85rc1mt.FinishTransmission();
        }

        private readonly MB85RC1MT mb85rc1mt;
        private readonly bool addr16;
    }

    public class MB85RC1MTLo : MB85RC1MTI2CRelay
    {
        public MB85RC1MTLo(MB85RC1MT mb85rc1mt) : base(mb85rc1mt, false)
        {
        }
    }

    public class MB85RC1MTHi : MB85RC1MTI2CRelay
    {
        public MB85RC1MTHi(MB85RC1MT mb85rc1mt) : base(mb85rc1mt, true)
        {
        }
    }
}
