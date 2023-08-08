//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Threading;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [Icon("wrench")]
    public class ArmSysCtl : IDoubleWordPeripheral
    {
        public ArmSysCtl(IMachine machine)
        {
            this.machine = machine;
            Reset();
        }

        public ArmSysCtl(IMachine machine,uint procId)
        {
            this.machine = machine;
            this.ProcId=procId;
            Reset();
        }
        
        public uint ReadDoubleWord (long offset)
        {   
            switch((CTL)offset)
            {
            case CTL.Id:
                return SysId;
            case CTL.Sw:
                return 0;
            case CTL.Led:
                return Leds;
            case CTL.Lock:
                return LockVal;
            case CTL.OSC0:
                return 0;
            case CTL.OSC1:
                return 0;
            case CTL.OSC2:
                return 0;
            case CTL.OSC3:
                return 0;
            case CTL.OSC4:
                return 0;
            case CTL.MHz100:
                return 0;
            case CTL.CfgData1:
                return CfgData1;
            case CTL.CfgData2:
                return CfgData2;
            case CTL.Flags:
                return Flags;
            case CTL.NvFlags:
                return NvFlags;
            case CTL.ResetCtl:
                return ResetLevel;
            case CTL.PCICtl:
                return 1;
            case CTL.Mci:
                return 0;
            case CTL.Flash:
                return 0;
            case CTL.Clcd:
                return 0x1000;
            case CTL.ClcdSer:
                return 0;
            case CTL.BootCs:
                return 0;
            case CTL.Mhz24:
                //TODO: verify
                uint v = unchecked((uint)((machine.ElapsedVirtualTime.TimeElapsed).TotalSeconds*24000000));
                return v;
            case CTL.Misc:
                return 0;
            case CTL.ProcId0:
                return ProcId;
            case CTL.ProcId1:
                return 0xff000000;
            case CTL.Dmapsr0:
                return 0;
            case CTL.Dmapsr1:
                return 0;
            case CTL.Dmapsr2:
                return 0;
            case CTL.IOSel:
                return 0;
            case CTL.PldCtl:
                return 0;
            case CTL.BusId:
                return 0;
            case CTL.OSCRESET0:
                return 0;
            case CTL.OSCRESET1:
                return 0;
            case CTL.OSCRESET2:
                return 0;
            case CTL.OSCRESET3:
                return 0;
            case CTL.OSCRESET4:
                return 0;
            case CTL.CFGDATA:
                return CfgData;
            case CTL.CFGCTRL:
                return CfgCtrl;
            case CTL.CFGSTAT:
                return CfgStat;
            case CTL.SYS_TEST_OSC0:
                return 0;
            case CTL.SYS_TEST_OSC1:
                return 0;
            case CTL.SYS_TEST_OSC2:
                return 0;
            case CTL.SYS_TEST_OSC3:
                return 0;
            case CTL.SYS_TEST_OSC4:
                return 0;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }
        
        private const int LOCK_VALUE = 0xa05f;
        
        public void WriteDoubleWord (long offset, uint value)
        {
            switch((CTL)offset)
            {
            case CTL.Led:
                Leds = value;
                break;
            case CTL.OSC0:
                break;
            case CTL.OSC1:
                break;
            case CTL.OSC2:
                break;
            case CTL.OSC3:
                break;
            case CTL.OSC4:
                break;
            case CTL.Lock:
                if(value == LOCK_VALUE)
                {
                    LockVal = (ushort) value;
                }
                else
                {
                    LockVal = (ushort)(value & 0x7fff);
                }
                break;
            case CTL.CfgData1:
                CfgData1 = value;
                break;
            case CTL.CfgData2:
                CfgData2 = value;
                break;
            case CTL.Flags:
                Flags |= value;
                break;
            case CTL.FlagsClr:
                Flags &= ~value;
                break;
            case CTL.NvFlags:
                NvFlags |= value;
                break;
            case CTL.NvFlagsClr:
                NvFlags &= ~value;
                break;
            case CTL.ResetCtl:
                if(LockVal == LOCK_VALUE)
                {
                    ResetLevel = value;
                    if (ResetLevel == 0x105) 
                    {
                        this.Log(LogLevel.Info, "System reset triggered.");
                        new Thread(() => machine.Reset()) { IsBackground = true }.Start();
                    }
                }
                break;
            case CTL.PCICtl:
                break;
            case CTL.Flash:
                break;
            case CTL.Clcd:
                break;
            case CTL.ClcdSer:
                break;
            case CTL.Dmapsr0:
                break;
            case CTL.Dmapsr1:
                break;
            case CTL.Dmapsr2:
                break;
            case CTL.IOSel:
                break;
            case CTL.PldCtl:
                break;
            case CTL.BusId:
                break;
            case CTL.ProcId0:
                break;
            case CTL.ProcId1:
                break;
            case CTL.CFGDATA:
                CfgData =value;
            break;
            case CTL.CFGCTRL:
                if(value == MachineReset)
                {
                    this.Log(LogLevel.Info, "System reset triggered.");
                    new Thread(() => machine.Reset()) { IsBackground = true }.Start();
                }
                if(value == MachineShutdown)
                {
                    this.Log(LogLevel.Info, "System shutdown triggered.");
                    /* Put shutdown code here */
                }
                CfgCtrl = (uint)(value & (uint)(~(3u << 18)));
                CfgStat=1;
                break;
            case CTL.CFGSTAT:
                CfgStat=value& 3;
                break;
            case CTL.OSCRESET0:
                break;
            case CTL.OSCRESET1:
                break;
            case CTL.OSCRESET2:
                break;
            case CTL.OSCRESET3:
                break;
            case CTL.OSCRESET4:
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                return;
            }
        }

        public void Reset ()
        {
            Leds = 0;
            LockVal = 0;
            CfgData1 = 0;
            CfgData2 = 0;
            Flags = 0;
            ResetLevel = 0;
        }
        
        private uint SysId = 0x41007004;
        private uint Leds;
        private ushort LockVal;
        private uint CfgData1;
        private uint CfgData2;
        private uint Flags;
        private uint NvFlags;
        private uint ResetLevel;
        private uint CfgData;
        private uint CfgCtrl;
        private uint CfgStat;
        private uint ProcId = 0x02000000;
        private uint MachineReset = 0xC0900000;
        private uint MachineShutdown = 0xC0800000;
        private readonly IMachine machine;
        
        private enum CTL : uint
        {
            Id = 0x00,
            Sw = 0x04,
            Led = 0x08,
            OSC0 = 0x0c,
            OSC1 = 0x10,
            OSC2 = 0x14,
            OSC3 = 0x18,
            OSC4 = 0x1c,            
            Lock = 0x20,
            MHz100 = 0x24,
            CfgData1 = 0x28,
            CfgData2 = 0x2c,
            Flags = 0x30,
            FlagsClr = 0x34,
            NvFlags = 0x38,
            NvFlagsClr = 0x3c,
            ResetCtl = 0x40,
            PCICtl = 0x44,
            Mci = 0x48,
            Flash = 0x4c,
            Clcd = 0x50,
            ClcdSer = 0x54,
            BootCs = 0x58,
            Mhz24 = 0x5c,
            Misc = 0x60,
            Dmapsr0 = 0x64,
            Dmapsr1 = 0x68,
            Dmapsr2 = 0x6c,
            IOSel = 0x70,
            PldCtl = 0x74,
            BusId = 0x80,
            ProcId0 = 0x84,
            ProcId1 = 0x88,
            OSCRESET0 = 0x8c,
            OSCRESET1 = 0x90,
            OSCRESET2 = 0x94,
            OSCRESET3 = 0x98,
            OSCRESET4 = 0x9c,
            CFGDATA = 0xa0,
            CFGCTRL = 0xa4,
            CFGSTAT = 0xa8,
            SYS_TEST_OSC0 = 0xc0,
            SYS_TEST_OSC1 = 0xc4,
            SYS_TEST_OSC2 = 0xc8,
            SYS_TEST_OSC3 = 0xcc,
            SYS_TEST_OSC4 = 0xd0            
        }
    }
}

