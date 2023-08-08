//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.DMA
{
    public sealed class TegraDmaHost1X : IDoubleWordPeripheral, IKnownSize
    {
        public TegraDmaHost1X(IMachine machine)
        {
            dmaEngine = new DmaEngine(machine.GetSystemBus(this));
            sysbus = machine.GetSystemBus(this);
        }

        public long Size
        {
            get
            {
                return 0x100;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            this.LogUnhandledRead(offset);
            return 0;
        }

	public uint classid = 0xFFFFFFFF;

	public uint Execute_DMA(ulong offset) {
		uint result = 4;
		uint vl = sysbus.ReadDoubleWord(offset);
		var opcode = ((vl >> 28) & 0xF);
		uint count, addr;
		switch (opcode) {
			case 0x0:
				classid =  ((vl >> 6) & 0x3FF);
				this.Log(LogLevel.Warning, "Opcode 0x0 SETCL, set to 0x{0:X}",classid);
				break;
			case 0x1:
				count = vl & 0xFFFF;
				addr = ((vl >> 16) & 0xFFF) * 4;
				this.Log(LogLevel.Warning, "Opcode 0x1 INCR, wcount = {0}", count);
 				if (classid == 0x30) {
                                	// VIP module
                                        this.DebugLog("Write to VI @ 0x{0:X} count {1} bytes, first_val = {2:X}", 0x54080000 + addr, count*4, sysbus.ReadDoubleWord(offset + 4));
                                        dmaEngine.IssueCopy(new Request(offset + 4, 0x54080000 + addr, (int)(count*4), TransferType.DoubleWord, TransferType.DoubleWord));
                                } else if (classid == 0x01) {
                                        this.DebugLog("class nvhost 0x01 - syncpt control. next_val = {0:X} at addr {1:X}. not implemented yet.", sysbus.ReadDoubleWord(offset+4),addr);
                                } else {
                                        this.DebugLog("classid = {0:X} not recognized!", classid);
				}
				result += count * 4;
				break;
			case 0x6:
				count = vl & 0x3FFF;
				this.Log(LogLevel.Warning, "Opcode 0x6 GATHER, count = {0}", count);
				/*
				this.Log(LogLevel.Warning, "Executing gather list >>> ");
				ptr = 0;
				while (ptr < (count*4)) ptr += Execute_DMA(offset + ptr);
				this.Log(LogLevel.Warning, "Executing list done <<< ");
				*/
				break;
			case 0x2:
				count = vl & 0xFFFF;
	                        this.Log(LogLevel.Warning, "Opcode 0x1 INCR, count = {0}", count);
	                        result += count * 4;
				break;
			default:
				this.Log(LogLevel.Warning, "Opcode 0x{0:X} UNSUPPORTED!", opcode);
				break;
		}
		return result;
	}
		

        public void WriteDoubleWord(long offset, uint value)
        {
            if((Register)offset == Register.HOST1X_CHANNEL_DMAPUT)
            {
                //this.DebugLog("We will have a command at addr 0x{0:X}", value & 0xFFFFFFFC);
                if(this.command_addr == 0x0)
                {
                    this.command_addr = (value >> 2) * 4;
                    return;
                }
/*      } else if ((Register)offset == Register.HOST1X_CHANNEL_DMACTRL) {
            if (value == 0x7) {*/
		uint opc = (sysbus.ReadDoubleWord(this.command_addr) >>  28) & 0xF;
		Execute_DMA(this.command_addr);
		if (opc == 6) {
			// gather
                	uint cmd_l = 4 * (sysbus.ReadDoubleWord(this.command_addr) & 0x3FFF);
	                uint cmd_ad = (sysbus.ReadDoubleWord(this.command_addr + 4) >> 2) * 4;			
			uint ptr = 0;
			this.Log(LogLevel.Warning, ">>> Executing gather list of size {0} >>> ", cmd_l);
			while (ptr < cmd_l) {
				ptr = ptr + Execute_DMA(cmd_ad + ptr);	
			}
			this.Log(LogLevel.Warning, "<<< Executing list done <<< ");
		}

/*
                uint cmd_len = sysbus.ReadDoubleWord(this.command_addr) & 0x3FFF;
		uint op = (sysbus.ReadDoubleWord(this.command_addr) >>  28) & 0xF;
                uint cmd_addr = (sysbus.ReadDoubleWord(this.command_addr + 4) >> 2) * 4;
                this.Log(LogLevel.Warning, "Command buffer is at < 0x{0:X} .. 0x{3:X} >, len={1}, opcode={2:X}", cmd_addr, cmd_len,op,cmd_addr+cmd_len*4-1);
		if (op != 0x6) {
		 this.Log(LogLevel.Warning, "Weird. opcode should be GATHER (0x6). is = 0x{0:X}. Ommiting. Whole was {1:X}", op, sysbus.ReadDoubleWord(this.command_addr));
		 //TODO: this is probably an direct command. modify this so that it is supported. idea: read whole buf and send to additional method.
		 return;
		}
                int i = 0;
                while(i < cmd_len)
                {
                    var vl = sysbus.ReadDoubleWord(cmd_addr + (i * 4));
		     this.Log(LogLevel.Warning, "{0:X} = {1:X}", cmd_addr+(i*4), vl);

                    i++;
                    var count = (int)(vl & 0xFFFF);
                    var addr = ((vl >> 16) & 0xFFF) * 4;
                    var opcode = ((vl >> 28) & 0xF);
*/

/*
#define HCFCMD_OPCODE_SETCL                     _MK_ENUM_CONST(0)
#define HCFCMD_OPCODE_INCR                      _MK_ENUM_CONST(1)
#define HCFCMD_OPCODE_NONINCR                   _MK_ENUM_CONST(2)
#define HCFCMD_OPCODE_MASK                      _MK_ENUM_CONST(3)
#define HCFCMD_OPCODE_IMM                       _MK_ENUM_CONST(4)
#define HCFCMD_OPCODE_RESTART                   _MK_ENUM_CONST(5)
#define HCFCMD_OPCODE_GATHER                    _MK_ENUM_CONST(6)
#define HCFCMD_OPCODE_EXTEND                    _MK_ENUM_CONST(14)   
#define HCFCMD_OPCODE_CHDONE                    _MK_ENUM_CONST(15)

*/

  //                  if(opcode == 0x1)
    //                {

/*
 
#define NV_HOST1X_CLASS_ID                      0x01

#define NV_VIDEO_ENCODE_MPEG_CLASS_ID           0x20

#define NV_VIDEO_STREAMING_VI_CLASS_ID          0x30
#define NV_VIDEO_STREAMING_EPP_CLASS_ID         0x31
#define NV_VIDEO_STREAMING_ISP_CLASS_ID         0x32
#define NV_VIDEO_STREAMING_VCI_CLASS_ID         0x33

#define NV_GRAPHICS_2D_DOWNLOAD_CLASS_ID        0x50
#define NV_GRAPHICS_2D_CLASS_ID                 0x51
#define NV_GRAPHICS_2D_SB_CLASS_ID              0x52
#define NV_GRAPHICS_2D_DOWNLOAD_CTX1_CLASS_ID   0x54
#define NV_GRAPHICS_2D_CTX1_CLASS_ID            0x55
#define NV_GRAPHICS_2D_SB_CTX1_CLASS_ID         0x56
#define NV_GRAPHICS_2D_DOWNLOAD_CTX2_CLASS_ID   0x58
#define NV_GRAPHICS_2D_SB_CTX2_CLASS_ID         0x5A

#define NV_GRAPHICS_VS_CLASS_ID                 0x5C

#define NV_GRAPHICS_3D_CLASS_ID                 0x60

#define NV_DISPLAY_CLASS_ID                     0x70
#define NV_DISPLAYB_CLASS_ID                    0x71
#define NV_HDMI_CLASS_ID                        0x77
#define NV_DISPLAY_TVO_CLASS_ID                 0x78
#define NV_DISPLAY_DSI_CLASS_ID                 0x79

#define NV_GRAPHICS_VG_CLASS_ID                 0xD0
 
 
 */
/*
		    	if (classid == 0x30) {
	                        // VIP module
				this.DebugLog("Write to VI at {0:X} count {1}, first_val = {2:X}", 0x54080000 + addr, count*4, sysbus.ReadDoubleWord(cmd_addr+i*4));
        	                dmaEngine.IssueCopy(new Request(cmd_addr + i * 4, 0x54080000 + addr, count*4, TransferType.DoubleWord, TransferType.DoubleWord));
			} else if (classid == 0x01) {
				this.DebugLog("class nvhost 0x01 - syncpt control. next_val = {0:X} at addr {1:X}. not implemented yet.", sysbus.ReadDoubleWord(cmd_addr+i*4),addr);
			} else {
				this.DebugLog("classid = {0:X} not recognized!", classid);
			}
			i += count;
                    } else if (opcode == 0x2) {
		    	this.DebugLog("Opcode 0x02 NONINCR - not implemented. was {0} bytes.", count);
		    	i += count;
		    }
                    else if (opcode == 0x0) {
		    	this.DebugLog("Opcode 0x00 SETCL --> class {0} set.", ((vl >> 6) & 0x3FF));
			classid = ((vl >> 6) & 0x3FF);
		    } else
                    {
                        this.DebugLog("opcode 0x{0:X} not recognized. [Data size was {1}, vl={2:X}]", opcode, count,vl);
                    }
                }
                this.command_addr = value & 0xFFFFFFFC;

//          }*/
            }
            else
            {
                this.LogUnhandledWrite(offset, value);

            }
        }

        public void Reset()
        {
            command_addr = 0;
            classid = 0xFFFFFFFF;
        }

        private enum Register
        {
            HOST1X_CHANNEL_DMASTART = 0x14,
            HOST1X_CHANNEL_DMAPUT = 0x18,
            HOST1X_CHANNEL_DMAEND = 0x20,
            HOST1X_CHANNEL_DMACTRL = 0x24,
        }

        private uint command_addr = 0x0;
        private readonly DmaEngine dmaEngine;
        private readonly IBusController sysbus;

    }
}

