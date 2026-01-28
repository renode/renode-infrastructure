using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class MH1903_BPU : BasicDoubleWordPeripheral, IKnownSize
    {
        public MH1903_BPU(IMachine machine) : base(machine)
        {
            BuildRegisterNameMap();
            DefineRegisters();
        }

        public long Size => 0x600;

        public override uint ReadDoubleWord(long offset)
        {
            var value = base.ReadDoubleWord(offset);
            var regName = GetRegisterName(offset);
            this.Log(LogLevel.Info, "BPU read at {0}: 0x{1:X8}", regName, value);
            return value;
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            var regName = GetRegisterName(offset);
            this.Log(LogLevel.Info, "BPU write at {0}: 0x{1:X8}", regName, value);
            base.WriteDoubleWord(offset, value);
        }

        private string GetRegisterName(long offset)
        {
            if(registerNames.TryGetValue(offset, out var name))
            {
                return name;
            }
            return $"0x{offset:X3}";
        }

        private void BuildRegisterNameMap()
        {
            registerNames = new Dictionary<long, string>();

            // KEY registers
            for(int i = 0; i < 16; i++)
            {
                registerNames[(long)Registers.KEY_0 + (i * 4)] = $"KEY_{i}";
            }

            // BPK registers
            registerNames[(long)Registers.BPK_RDY] = "BPK_RDY";
            registerNames[(long)Registers.BPK_CLR] = "BPK_CLR";
            registerNames[(long)Registers.BPK_LRA] = "BPK_LRA";
            registerNames[(long)Registers.BPK_LWA] = "BPK_LWA";
            registerNames[(long)Registers.BPK_LR] = "BPK_LR";
            registerNames[(long)Registers.BPK_SCR] = "BPK_SCR";
            registerNames[(long)Registers.BPK_POWER] = "BPK_POWER";

            // RTC registers
            registerNames[(long)Registers.RTC_CS] = "RTC_CS";
            registerNames[(long)Registers.RTC_REF] = "RTC_REF";
            registerNames[(long)Registers.RTC_ARM] = "RTC_ARM";
            registerNames[(long)Registers.RTC_TIM] = "RTC_TIM";
            registerNames[(long)Registers.RTC_INTCLR] = "RTC_INTCLR";
            registerNames[(long)Registers.OSC32K_CR] = "OSC32K_CR";
            registerNames[(long)Registers.RTC_ATTA_TIM] = "RTC_ATTA_TIM";
            registerNames[(long)Registers.BPK_RR] = "BPK_RR";

            // SEN registers
            registerNames[(long)Registers.SEN_EXT_TYPE] = "SEN_EXT_TYPE";
            registerNames[(long)Registers.SEN_EXT_CFG] = "SEN_EXT_CFG";
            registerNames[(long)Registers.SEN_SOFT_EN] = "SEN_SOFT_EN";
            registerNames[(long)Registers.SEN_STATE] = "SEN_STATE";
            registerNames[(long)Registers.SEN_BRIDGE] = "SEN_BRIDGE";
            registerNames[(long)Registers.SEN_SOFT_ATTACK] = "SEN_SOFT_ATTACK";
            registerNames[(long)Registers.SEN_SOFT_LOCK] = "SEN_SOFT_LOCK";
            registerNames[(long)Registers.SEN_ATTACK_CNT] = "SEN_ATTACK_CNT";
            registerNames[(long)Registers.SEN_ATTACK_TYP] = "SEN_ATTACK_TYP";
            registerNames[(long)Registers.SEN_VG_DETECT] = "SEN_VG_DETECT";
            registerNames[(long)Registers.SEN_RNG_INI] = "SEN_RNG_INI";

            // SEN_EN registers
            for(int i = 0; i < 19; i++)
            {
                registerNames[(long)Registers.SEN_EN_0 + (i * 4)] = $"SEN_EN_{i}";
            }

            // Additional sensor registers
            registerNames[(long)Registers.SEN_EXTS_START] = "SEN_EXTS_START";
            registerNames[(long)Registers.SEN_LOCK] = "SEN_LOCK";
            registerNames[(long)Registers.SEN_ANA0] = "SEN_ANA0";
            registerNames[(long)Registers.SEN_ANA1] = "SEN_ANA1";
            registerNames[(long)Registers.SEN_ATTCLR] = "SEN_ATTCLR";
            registerNames[(long)Registers.SEN_PUPU_CFG] = "SEN_PUPU_CFG";

            // BPK_RAM
            for(int i = 0; i < 256; i++)
            {
                registerNames[(long)Registers.BPK_RAM_0 + (i * 4)] = $"BPK_RAM_{i}";
            }
        }

        private void DefineRegisters()
        {
            // KEY registers 0x00-0x3C (16 words)
            for(int i = 0; i < 16; i++)
            {
                ((Registers)((long)Registers.KEY_0 + (i * 4))).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"key_{i}");
            }

            // Reserved 0x40-0x7C
            for(int i = 0; i < 16; i++)
            {
                ((Registers)((long)Registers.BPK_RSVD0_0 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // BPK (Backup Key) registers
            Registers.BPK_RDY.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0x00000001, name: "bpk_rdy");
            Registers.BPK_CLR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "bpk_clr");
            Registers.BPK_LRA.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "bpk_lra");
            Registers.BPK_LWA.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "bpk_lwa");
            Registers.BPK_RSVD1.Define(this)
                .WithReservedBits(0, 32);
            Registers.BPK_LR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "bpk_lr");
            Registers.BPK_SCR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "bpk_scr");
            Registers.BPK_POWER.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "bpk_power");

            // RTC (Real-Time Clock) registers
            Registers.RTC_CS.Define(this, resetValue: 0x00000008) // Default: 1 << 3
                .WithValueField(0, 32, name: "rtc_cs");
            Registers.RTC_REF.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "rtc_ref");
            Registers.RTC_ARM.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "rtc_arm");
            Registers.RTC_TIM.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "rtc_tim");
            Registers.RTC_INTCLR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "rtc_intclr");
            Registers.OSC32K_CR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "osc32k_cr");
            Registers.RTC_ATTA_TIM.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "rtc_atta_tim");
            Registers.BPK_RR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "bpk_rr");

            // SEN (Sensor) registers
            Registers.SEN_EXT_TYPE.Define(this, resetValue: 0x000FF000)
                .WithValueField(0, 32, name: "sen_ext_type");
            Registers.SEN_EXT_CFG.Define(this, resetValue: 0x00A5A000)
                .WithValueField(0, 32, name: "sen_ext_cfg");
            Registers.SEN_SOFT_EN.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "sen_soft_en");
            Registers.SEN_STATE.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "sen_state");
            Registers.SEN_BRIDGE.Define(this, resetValue: 0x000000F0)
                .WithValueField(0, 32, name: "sen_bridge");
            Registers.SEN_SOFT_ATTACK.Define(this, resetValue: 0x80000000)
                .WithValueField(0, 32, name: "sen_soft_attack");
            Registers.SEN_SOFT_LOCK.Define(this, resetValue: 0x0000000F)
                .WithValueField(0, 32, name: "sen_soft_lock");
            Registers.SEN_ATTACK_CNT.Define(this, resetValue: 0x0000000F)
                .WithValueField(0, 32, FieldMode.Read, name: "sen_attack_cnt");
            Registers.SEN_ATTACK_TYP.Define(this, resetValue: 0x00000001)
                .WithValueField(0, 32, FieldMode.Read, name: "sen_attack_typ");
            Registers.SEN_VG_DETECT.Define(this, resetValue: 0x00000003)
                .WithValueField(0, 32, name: "sen_vg_detect");
            Registers.SEN_RNG_INI.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "sen_rng_ini");

            // Reserved 0xEC-0x100
            for(int i = 0; i < 6; i++)
            {
                ((Registers)((long)Registers.RESERVED3_0 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // SEN_EN registers 0x104-0x14C
            // SEN_EXT0_EN through SEN_EXT7_EN (0x104-0x120): 0x000000AA
            for(int i = 0; i < 8; i++)
            {
                ((Registers)((long)Registers.SEN_EN_0 + (i * 4))).Define(this, resetValue: 0x000000AA)
                    .WithValueField(0, 32, name: $"sen_ext{i}_en");
            }

            // SEN_RSVD (0x124-0x130): 0x00000000
            for(int i = 8; i < 11; i++)
            {
                ((Registers)((long)Registers.SEN_EN_0 + (i * 4))).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"sen_rsvd_{i}");
            }

            // SEN_VH_EN (0x134): 0x000000AA
            ((Registers)((long)Registers.SEN_EN_0 + (11 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "sen_vh_en");

            // SEN_VL_EN (0x138): 0x000000AA
            ((Registers)((long)Registers.SEN_EN_0 + (12 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "sen_vl_en");

            // SEN_TH_EN (0x13C): 0x000000AA
            ((Registers)((long)Registers.SEN_EN_0 + (13 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "sen_th_en");

            // SEN_TL_EN (0x140): 0x000000AA
            ((Registers)((long)Registers.SEN_EN_0 + (14 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "sen_tl_en");

            // SEN_XTAL32_EN (0x144): 0x00000055
            ((Registers)((long)Registers.SEN_EN_0 + (15 * 4))).Define(this, resetValue: 0x00000055)
                .WithValueField(0, 32, name: "sen_xtal32_en");

            // SEN_MESG_EN (0x148): 0x000000AA
            ((Registers)((long)Registers.SEN_EN_0 + (16 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "sen_mesg_en");

            // SEN_VOLGLTCH_EN (0x14C): 0x000000AA
            ((Registers)((long)Registers.SEN_EN_0 + (17 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "sen_volgltch_en");

            // Additional sensor registers
            Registers.SEN_EXTS_START.Define(this, resetValue: 0x800000AA)
                .WithValueField(0, 32, name: "sen_exts_start");
            Registers.SEN_LOCK.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "sen_lock");
            Registers.SEN_ANA0.Define(this, resetValue: 0x02350220)
                .WithValueField(0, 32, name: "sen_ana0");
            Registers.SEN_ANA1.Define(this, resetValue: 0x00000024)
                .WithValueField(0, 32, name: "sen_ana1");

            // SEN_ATTCLR 0x160
            Registers.SEN_ATTCLR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "sen_attclr");

            // Reserved 0x164-0x170
            for(int i = 0; i < 4; i++)
            {
                ((Registers)(0x164 + (i * 4))).Define(this, resetValue: 0x00000000)
                    .WithReservedBits(0, 32);
            }

            // SEN_PUPU_CFG 0x174
            Registers.SEN_PUPU_CFG.Define(this, resetValue: 0xFF0000FF)
                .WithValueField(0, 32, name: "sen_pupu_cfg");

            // Reserved 0x178-0x1FC
            for(int i = 0; i < 34; i++)
            {
                ((Registers)((long)Registers.BPU_RSVD4_0 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // BPK_RAM 0x200-0x5FC (256 words)
            for(int i = 0; i < 256; i++)
            {
                ((Registers)((long)Registers.BPK_RAM_0 + (i * 4))).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"bpk_ram_{i}");
            }
        }

        private Dictionary<long, string> registerNames;

        private enum Registers : long
        {
            // KEY registers 0x00-0x3C
            KEY_0 = 0x000,
            KEY_1 = 0x004,
            KEY_2 = 0x008,
            KEY_3 = 0x00C,
            KEY_4 = 0x010,
            KEY_5 = 0x014,
            KEY_6 = 0x018,
            KEY_7 = 0x01C,
            KEY_8 = 0x020,
            KEY_9 = 0x024,
            KEY_10 = 0x028,
            KEY_11 = 0x02C,
            KEY_12 = 0x030,
            KEY_13 = 0x034,
            KEY_14 = 0x038,
            KEY_15 = 0x03C,

            // Reserved 0x40-0x7C
            BPK_RSVD0_0 = 0x040,

            // BPK registers 0x80-0x9C
            BPK_RDY = 0x080,
            BPK_CLR = 0x084,
            BPK_LRA = 0x088,
            BPK_LWA = 0x08C,
            BPK_RSVD1 = 0x090,
            BPK_LR = 0x094,
            BPK_SCR = 0x098,
            BPK_POWER = 0x09C,

            // RTC registers 0xA0-0xBC
            RTC_CS = 0x0A0,
            RTC_REF = 0x0A4,
            RTC_ARM = 0x0A8,
            RTC_TIM = 0x0AC,
            RTC_INTCLR = 0x0B0,
            OSC32K_CR = 0x0B4,
            RTC_ATTA_TIM = 0x0B8,
            BPK_RR = 0x0BC,

            // SEN registers 0xC0-0xE8
            SEN_EXT_TYPE = 0x0C0,
            SEN_EXT_CFG = 0x0C4,
            SEN_SOFT_EN = 0x0C8,
            SEN_STATE = 0x0CC,
            SEN_BRIDGE = 0x0D0,
            SEN_SOFT_ATTACK = 0x0D4,
            SEN_SOFT_LOCK = 0x0D8,
            SEN_ATTACK_CNT = 0x0DC,
            SEN_ATTACK_TYP = 0x0E0,
            SEN_VG_DETECT = 0x0E4,
            SEN_RNG_INI = 0x0E8,

            // Reserved 0xEC-0x100
            RESERVED3_0 = 0x0EC,

            // SEN_EN registers 0x104-0x14C
            SEN_EN_0 = 0x104,

            // Additional sensor registers 0x150-0x160
            SEN_EXTS_START = 0x150,
            SEN_LOCK = 0x154,
            SEN_ANA0 = 0x158,
            SEN_ANA1 = 0x15C,
            SEN_ATTCLR = 0x160,

            // SEN_PUPU_CFG 0x174
            SEN_PUPU_CFG = 0x174,

            // Reserved 0x178-0x1FC
            BPU_RSVD4_0 = 0x178,

            // BPK_RAM 0x200-0x5FC
            BPK_RAM_0 = 0x200,
        }
    }
}
