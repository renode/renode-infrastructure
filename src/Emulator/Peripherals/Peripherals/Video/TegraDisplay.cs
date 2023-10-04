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
    public class TegraDisplay : AutoRepaintingVideo, IDoubleWordPeripheral, IKnownSize
    {
        public TegraDisplay(IMachine machine) : base(machine)
        {
            Reconfigure(640, 480, PixelFormat.RGB565);
            sysbus = machine.GetSystemBus(this);
            sync = new object();
        }

        public long Size
        {
            get
            {
                return 0x40000;
            }
        }

        public void WriteDoubleWord(long address, uint value)
        {
            if(address == 0x1c14) // DC_WIN_SIZE
            {
                int w = (int)(value & 0xFFFF);
                int h = (int)((value >> 16) & 0xFFFF);
                this.DebugLog("Setting resolution to {0}x{1}", w, h);
                Reconfigure(w, h);
            }
    	    if(address == 0x1c0c) // DC_WIN_COLOR_DEPTH
    	    {
    	    	this.Log(LogLevel.Warning, "Depth ID={0}", value);
        		lock (sync) {
        			switch (value) 
                    {
                    case 3:
                        Reconfigure(format: PixelFormat.RGB565);
    					break;
                    case 12:
                        Reconfigure(format: PixelFormat.BGRX8888);
    					break;
                    case 13:
                        Reconfigure(format: PixelFormat.RGBX8888);
    					break;
                    default:
                        this.Log(LogLevel.Warning, "Depth ID={0} is not supported (might be YUV)!", value);
                        Reconfigure(format: PixelFormat.RGB565);
    					break;
        			}
        		}
    	    }
            if(address == 0x2000) // DC_WINBUF_START_ADDR
            {
                this.DebugLog("Setting buffer addr to 0x{0:X}", value);
                lock (sync) {
                        bufferAddress = value;
                }
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return 0x00;
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
            lock (sync) 
            {
                sysbus.ReadBytes(bufferAddress, buffer.Length, buffer, 0);
            }
        }

        private object sync;
        private uint bufferAddress = 0xFFFFFFFF;
        private readonly IBusController sysbus;
    }
}

