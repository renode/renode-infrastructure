using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class MH1903_SSC : BasicDoubleWordPeripheral, IKnownSize
    {
        public MH1903_SSC(IMachine machine) : base(machine)
        {
            BuildRegisterNameMap();
            DefineRegisters();
        }

        public long Size => 0x400;

        public GPIO IRQ { get; set; } = new GPIO();

        public override uint ReadDoubleWord(long offset)
        {
            var value = base.ReadDoubleWord(offset);
            var regName = GetRegisterName(offset);
            this.Log(LogLevel.Info, "SSC read at {0}: 0x{1:X8}", regName, value);
            return value;
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            var regName = GetRegisterName(offset);
            this.Log(LogLevel.Info, "SSC write at {0}: 0x{1:X8}", regName, value);
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

            registerNames[(long)Registers.SSC_CR3] = "SSC_CR3";
            registerNames[(long)Registers.SSC_SR] = "SSC_SR";
            registerNames[(long)Registers.SSC_SR_CLR] = "SSC_SR_CLR";
            registerNames[(long)Registers.SSC_ACK] = "SSC_ACK";
            registerNames[(long)Registers.DATARAM_SCR] = "DATARAM_SCR";
            registerNames[(long)Registers.BPU_RWC] = "BPU_RWC";
            registerNames[(long)Registers.MAIN_SEN_LOCK] = "MAIN_SEN_LOCK";
            registerNames[(long)Registers.MAIN_SEN_EN] = "MAIN_SEN_EN";
        }

        private void DefineRegisters()
        {
            // Reserved 0x00-0x04
            Registers.RESERVED0_0.Define(this)
                .WithReservedBits(0, 32);
            Registers.RESERVED0_1.Define(this)
                .WithReservedBits(0, 32);

            // SSC_CR3 at 0x08
            Registers.SSC_CR3.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ssc_cr3");

            // Reserved 0x0C-0x100 (62 words)
            for(int i = 0; i < 62; i++)
            {
                ((Registers)((long)Registers.RESERVED1_0 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // SSC Status and Control registers 0x104-0x10C
            Registers.SSC_SR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ssc_sr");
            Registers.SSC_SR_CLR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "ssc_sr_clr");
            Registers.SSC_ACK.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ssc_ack");

            // Reserved 0x110-0x180 (29 words)
            for(int i = 0; i < 29; i++)
            {
                ((Registers)((long)Registers.RESERVED2_0 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // DATARAM_SCR at 0x184
            Registers.DATARAM_SCR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "dataram_scr");

            // Reserved 0x188-0x1F8 (29 words)
            for(int i = 0; i < 29; i++)
            {
                ((Registers)((long)Registers.RESERVED3_0 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // BPU_RWC at 0x1FC
            Registers.BPU_RWC.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "bpu_rwc");

            // Reserved 0x200-0x3E8 (123 words)
            for(int i = 0; i < 123; i++)
            {
                ((Registers)((long)Registers.RESERVED4_0 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // Main sensor lock and enable at 0x3EC-0x3F0
            Registers.MAIN_SEN_LOCK.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "main_sen_lock");
            Registers.MAIN_SEN_EN.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "main_sen_en");
        }

        private Dictionary<long, string> registerNames;

        private enum Registers : long
        {
            // Reserved 0x00-0x04
            RESERVED0_0 = 0x000,
            RESERVED0_1 = 0x004,

            // SSC_CR3 at 0x08
            SSC_CR3 = 0x008,

            // Reserved 0x0C-0x100
            RESERVED1_0 = 0x00C,

            // SSC Status and Control 0x104-0x10C
            SSC_SR = 0x104,
            SSC_SR_CLR = 0x108,
            SSC_ACK = 0x10C,

            // Reserved 0x110-0x180
            RESERVED2_0 = 0x110,

            // DATARAM_SCR 0x184
            DATARAM_SCR = 0x184,

            // Reserved 0x188-0x1F8
            RESERVED3_0 = 0x188,

            // BPU_RWC 0x1FC
            BPU_RWC = 0x1FC,

            // Reserved 0x200-0x3E8
            RESERVED4_0 = 0x200,

            // Main sensor lock and enable 0x3EC-0x3F0
            MAIN_SEN_LOCK = 0x3EC,
            MAIN_SEN_EN = 0x3F0,
        }
    }
}
