using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class MH1903_GPIO_Control : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public MH1903_GPIO_Control(IMachine machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public void Reset()
        {
            RegistersCollection.Reset();
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x840;

        private void DefineRegisters()
        {
            // Interrupt status registers for external interrupt inputs
            DefineINTPStatus(Registers.INTP5_STA);
            DefineINTPStatus(Registers.INTP4_STA);
            DefineINTPStatus(Registers.INTP3_STA);
            DefineINTPStatus(Registers.INTP2_STA);
            DefineINTPStatus(Registers.INTP1_STA);
            DefineINTPStatus(Registers.INTP0_STA);

            // Alternate function registers for all GPIO ports (A-H)
            DefineALTRegister(Registers.PA_ALT, "PA");
            DefineALTRegister(Registers.PB_ALT, "PB");
            DefineALTRegister(Registers.PC_ALT, "PC");
            DefineALTRegister(Registers.PD_ALT, "PD");
            DefineALTRegister(Registers.PE_ALT, "PE");
            DefineALTRegister(Registers.PF_ALT, "PF");
            DefineALTRegister(Registers.PG_ALT, "PG");
            DefineALTRegister(Registers.PH_ALT, "PH");

            // System control register
            Registers.SYS_CR1.Define(this, resetValue: 0xFFFF0000)
                .WithValueField(0, 32, name: "SYS_CR1");

            // Wakeup configuration registers
            Registers.WKUP_TYPE_EN.Define(this)
                .WithReservedBits(15, 17)
                .WithFlag(14, name: "sensor_wk_en")
                .WithFlag(13, name: "msr_wk_en")
                .WithFlag(12, name: "rtc_wk_en")
                .WithFlag(11, name: "kbd_wk_en")
                .WithReservedBits(1, 10)
                .WithFlag(0, name: "gpio_wkup_type");

            Registers.WKUP_P0_EN.Define(this)
                .WithValueField(16, 16, name: "pb_wk_en")
                .WithValueField(0, 16, name: "pa_wk_en");

            Registers.WKUP_P1_EN.Define(this)
                .WithValueField(16, 16, name: "pd_wk_en")
                .WithValueField(0, 16, name: "pc_wk_en");

            Registers.WKUP_P2_EN.Define(this)
                .WithReservedBits(24, 8)
                .WithValueField(16, 8, name: "pf_wk_en")
                .WithValueField(0, 16, name: "pe_wk_en");

            // Interrupt type and status registers for GPIO ports
            DefineINTPTypeAndStatus(Registers.PA_INTP_TYPE, Registers.PA_INTP_STA, "PA");
            DefineINTPTypeAndStatus(Registers.PB_INTP_TYPE, Registers.PB_INTP_STA, "PB");
            DefineINTPTypeAndStatus(Registers.PC_INTP_TYPE, Registers.PC_INTP_STA, "PC");
            DefineINTPTypeAndStatus(Registers.PD_INTP_TYPE, Registers.PD_INTP_STA, "PD");
            DefineINTPTypeAndStatus(Registers.PE_INTP_TYPE, Registers.PE_INTP_STA, "PE");
            DefineINTPTypeAndStatus(Registers.PF_INTP_TYPE, Registers.PF_INTP_STA, "PF");
            DefineINTPTypeAndStatus(Registers.PG_INTP_TYPE, Registers.PG_INTP_STA, "PG");
            DefineINTPTypeAndStatus(Registers.PH_INTP_TYPE, Registers.PH_INTP_STA, "PH");
        }

        private void DefineINTPStatus(Registers register)
        {
            register.Define(this, resetValue: 0x00000000)
                .WithFlag(0, mode: FieldMode.ReadToClear, name: "int_state")
                .WithReservedBits(1, 30);
        }

        private void DefineALTRegister(Registers register, string portName)
        {
            var reg = register.Define(this, 0x55555555);
            // Alternate function: 2 bits per pin, pins ordered from 15 to 0
            for(int pin = 15; pin >= 0; pin--)
            {
                reg.WithValueField(pin * 2, 2, name: $"{portName}{pin}_alt");
            }
        }

        private void DefineINTPTypeAndStatus(Registers typeRegister, Registers staRegister, string portName)
        {
            // INTP_TYPE - Interrupt Type Register
            // 00: No interrupt generated
            // 01: Rising edge interrupt
            // 10: Falling edge interrupt
            // 11: Both edges interrupt (rising and falling)
            var typeReg = typeRegister.Define(this);
            for(int pin = 15; pin >= 0; pin--)
            {
                typeReg.WithValueField(pin * 2, 2, name: $"{portName}{pin}_int_type");
            }

            // INTP_STA - Interrupt Status Register (WriteToClear)
            staRegister.Define(this)
                .WithReservedBits(16, 16)
                .WithValueField(0, 16, mode: FieldMode.WriteToClear, name: $"{portName}_int_state");
        }

        private enum Registers : ulong
        {
            // External interrupt status (offsets relative to control base 0x4001d100)
            INTP5_STA = 0x14,
            INTP4_STA = 0x18,
            INTP3_STA = 0x1C,
            INTP2_STA = 0x20,
            INTP1_STA = 0x24,
            INTP0_STA = 0x28,

            // Alternate function registers
            PA_ALT = 0x80,
            PB_ALT = 0x84,
            PC_ALT = 0x88,
            PD_ALT = 0x8C,
            PE_ALT = 0x90,
            PF_ALT = 0x94,
            PG_ALT = 0x98,
            PH_ALT = 0x9C,

            // System control
            SYS_CR1 = 0x100,

            // Wakeup control registers
            WKUP_TYPE_EN = 0x120,
            WKUP_P0_EN = 0x124,
            WKUP_P1_EN = 0x128,
            WKUP_P2_EN = 0x12C,

            // GPIO Port Interrupt Type and Status
            PA_INTP_TYPE = 0x700,
            PA_INTP_STA = 0x704,
            PB_INTP_TYPE = 0x708,
            PB_INTP_STA = 0x70C,
            PC_INTP_TYPE = 0x710,
            PC_INTP_STA = 0x714,
            PD_INTP_TYPE = 0x718,
            PD_INTP_STA = 0x71C,
            PE_INTP_TYPE = 0x720,
            PE_INTP_STA = 0x724,
            PF_INTP_TYPE = 0x728,
            PF_INTP_STA = 0x72C,
            PG_INTP_TYPE = 0x730,
            PG_INTP_STA = 0x734,
            PH_INTP_TYPE = 0x738,
            PH_INTP_STA = 0x73C,
        }
    }
}
