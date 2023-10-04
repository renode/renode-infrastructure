//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System;

namespace Antmicro.Renode.Peripherals.Video
{
    public class PL110 : AutoRepaintingVideo, IDoubleWordPeripheral
    {
        public PL110(IMachine machine, int? screenWidth = null, int? screenHeight = null) : base(machine)
        {
            Reconfigure(screenWidth ?? DefaultWidth, screenHeight ?? DefaultHeight, PixelFormat.RGB565);
            sysbus = machine.GetSystemBus(this);
        }

        public void WriteDoubleWord(long address, uint value)
        {
            if(address == 0x10)
            {
                this.DebugLog("Setting buffer addr to 0x{0:X}", value);
                bufferAddress = value;
                return;
            }
            this.LogUnhandledWrite(address, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            switch(offset)
            {
            case 0xFE0:
                return 0x10;
            case 0xFE4:
                return 0x11;
            case 0xFE8:
                return 0x04;
            case 0xFEC:
                return 0x00;
            case 0xFF0:
                return 0x0d;
            case 0xFF4:
                return 0xf0;
            case 0xFF8:
                return 0x05;
            case 0xFFC:
                return 0xb1;
            default:
                this.LogUnhandledRead(offset);
                return 0x0;
            }
        }

        public override void Reset()
        {
            // TODO!
        }

        protected override void Repaint()
        {
            if(bufferAddress == 0xFFFFFFFF)
            {
                return;
            }
            sysbus.ReadBytes(bufferAddress, buffer.Length, buffer, 0);
        }

        private uint bufferAddress = 0xFFFFFFFF;

        private readonly IBusController sysbus;
       
        private const int DefaultWidth = 640;
        private const int DefaultHeight = 480;
    }
}

