//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Video
{
    public class VybridDCU : AutoRepaintingVideo, IDoubleWordPeripheral
    {
        public VybridDCU(IMachine machine) : base(machine)
        {
            Reconfigure(format: PixelFormat.BGR888);

            sysbus = machine.GetSystemBus(this);
            lock_obj = new object();
        }

        public void WriteDoubleWord(long address, uint value)
        {
            switch(address)
            {
            case 0x200:
                lock(lock_obj)
                {
                    var width = (int)(value & 0xFFFF);
                    var height = (int)((value >> 16) & 0xFFFF);
                    this.DebugLog("Setting resolution to {0} x {1}", Width, Height);

                    if(value == 0)
                    {
                        break;
                    }
                    Reconfigure(width, height);
                }
                break;
            case 0x208:
                this.DebugLog("Setting buffer addr to 0x{0:X}", value);
                bufferAddress = value;
                break;
            case 0x20c:
                lock(lock_obj)
                {
                    var bpp = (value >> 16) & 7;
                    switch(bpp)
                    {
                    case 4: // BPP_16_RGB565
                        Reconfigure(format: PixelFormat.RGB565);
                        break;
                    case 5: // BPP_24_RGB888
                        Reconfigure(format: PixelFormat.RGB888);
                        break;
                    case 6: // BPP_32_ARGB8888
                        Reconfigure(format: PixelFormat.ARGB8888);
                        break;
                    default:
                        this.Log(LogLevel.Warning, "Unsupported layer encoding format: 0x{0:X}", bpp);
                        break;
                    }
                }
                this.DebugLog("Change dpp mode to {0}-bit", 8 * (((value >> 16) & 7) - 2));
                break;
            default:
                this.LogUnhandledWrite(address, value);
                break;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            this.LogUnhandledRead(offset);
            return 0x00;
        }

        public override void Reset()
        {
            // TODO!
        }

        protected override void Repaint()
        {
            lock (lock_obj) 
            {
                if ((bufferAddress == 0xFFFFFFFF)) 
                {
                    return;
                }
                sysbus.ReadBytes(bufferAddress, buffer.Length, buffer, 0);
            }
        }

        private uint bufferAddress = 0xFFFFFFFF;

        private readonly object lock_obj;

        private readonly IBusController sysbus;
    }
}

