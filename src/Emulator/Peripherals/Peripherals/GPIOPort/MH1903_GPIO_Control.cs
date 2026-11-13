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
            DefineINTPStatus(Registers.ExternalInterrupt5Status);
            DefineINTPStatus(Registers.ExternalInterrupt4Status);
            DefineINTPStatus(Registers.ExternalInterrupt3Status);
            DefineINTPStatus(Registers.ExternalInterrupt2Status);
            DefineINTPStatus(Registers.ExternalInterrupt1Status);
            DefineINTPStatus(Registers.ExternalInterrupt0Status);

            // Alternate function registers for all GPIO ports (A-H)
            DefineALTRegister(Registers.PortAAlternateFunction, "PA");
            DefineALTRegister(Registers.PortBAlternateFunction, "PB");
            DefineALTRegister(Registers.PortCAlternateFunction, "PC");
            DefineALTRegister(Registers.PortDAlternateFunction, "PD");
            DefineALTRegister(Registers.PortEAlternateFunction, "PE");
            DefineALTRegister(Registers.PortFAlternateFunction, "PF");
            DefineALTRegister(Registers.PortGAlternateFunction, "PG");
            DefineALTRegister(Registers.PortHAlternateFunction, "PH");

            // System control register
            Registers.SystemControl1.Define(this, resetValue: 0xFFFF0000)
                .WithValueField(0, 32, name: "SystemControl1");

            // Wakeup configuration registers
            Registers.WakeupTypeEnable.Define(this)
                .WithReservedBits(15, 17)
                .WithFlag(14, name: "SensorWakeupEnable")
                .WithFlag(13, name: "MeasurementWakeupEnable")
                .WithFlag(12, name: "RealTimeClockWakeupEnable")
                .WithFlag(11, name: "KeyboardWakeupEnable")
                .WithReservedBits(1, 10)
                .WithFlag(0, name: "GpioWakeupType");

            Registers.WakeupPort0Enable.Define(this)
                .WithValueField(16, 16, name: "PortBWakeupEnable")
                .WithValueField(0, 16, name: "PortAWakeupEnable");

            Registers.WakeupPort1Enable.Define(this)
                .WithValueField(16, 16, name: "PortDWakeupEnable")
                .WithValueField(0, 16, name: "PortCWakeupEnable");

            Registers.WakeupPort2Enable.Define(this)
                .WithReservedBits(24, 8)
                .WithValueField(16, 8, name: "PortFWakeupEnable")
                .WithValueField(0, 16, name: "PortEWakeupEnable");

            // Interrupt type and status registers for GPIO ports
            DefineINTPTypeAndStatus(Registers.PortAInterruptType, Registers.PortAInterruptStatus, "PA");
            DefineINTPTypeAndStatus(Registers.PortBInterruptType, Registers.PortBInterruptStatus, "PB");
            DefineINTPTypeAndStatus(Registers.PortCInterruptType, Registers.PortCInterruptStatus, "PC");
            DefineINTPTypeAndStatus(Registers.PortDInterruptType, Registers.PortDInterruptStatus, "PD");
            DefineINTPTypeAndStatus(Registers.PortEInterruptType, Registers.PortEInterruptStatus, "PE");
            DefineINTPTypeAndStatus(Registers.PortFInterruptType, Registers.PortFInterruptStatus, "PF");
            DefineINTPTypeAndStatus(Registers.PortGInterruptType, Registers.PortGInterruptStatus, "PG");
            DefineINTPTypeAndStatus(Registers.PortHInterruptType, Registers.PortHInterruptStatus, "PH");
        }

        private void DefineINTPStatus(Registers register)
        {
            register.Define(this, resetValue: 0x00000000)
                .WithFlag(0, mode: FieldMode.ReadToClear, name: "InterruptState")
                .WithReservedBits(1, 30);
        }

        private void DefineALTRegister(Registers register, string portName)
        {
            var reg = register.Define(this, 0x55555555);
            // Alternate function: 2 bits per pin, pins ordered from 15 to 0
            for(int pin = 15; pin >= 0; pin--)
            {
                reg.WithValueField(pin * 2, 2, name: $"{portName}{pin}AlternateFunction");
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
                typeReg.WithValueField(pin * 2, 2, name: $"{portName}{pin}InterruptType");
            }

            // INTP_STA - Interrupt Status Register (WriteToClear)
            staRegister.Define(this)
                .WithReservedBits(16, 16)
                .WithValueField(0, 16, mode: FieldMode.WriteToClear, name: $"{portName}InterruptStatus");
        }

        private enum Registers : ulong
        {
            // External interrupt status (offsets relative to control base 0x4001d100)
            ExternalInterrupt5Status = 0x14,
            ExternalInterrupt4Status = 0x18,
            ExternalInterrupt3Status = 0x1C,
            ExternalInterrupt2Status = 0x20,
            ExternalInterrupt1Status = 0x24,
            ExternalInterrupt0Status = 0x28,

            // Alternate function registers
            PortAAlternateFunction = 0x80,
            PortBAlternateFunction = 0x84,
            PortCAlternateFunction = 0x88,
            PortDAlternateFunction = 0x8C,
            PortEAlternateFunction = 0x90,
            PortFAlternateFunction = 0x94,
            PortGAlternateFunction = 0x98,
            PortHAlternateFunction = 0x9C,

            // System control
            SystemControl1 = 0x100,

            // Wakeup control registers
            WakeupTypeEnable = 0x120,
            WakeupPort0Enable = 0x124,
            WakeupPort1Enable = 0x128,
            WakeupPort2Enable = 0x12C,

            // GPIO Port Interrupt Type and Status
            PortAInterruptType = 0x700,
            PortAInterruptStatus = 0x704,
            PortBInterruptType = 0x708,
            PortBInterruptStatus = 0x70C,
            PortCInterruptType = 0x710,
            PortCInterruptStatus = 0x714,
            PortDInterruptType = 0x718,
            PortDInterruptStatus = 0x71C,
            PortEInterruptType = 0x720,
            PortEInterruptStatus = 0x724,
            PortFInterruptType = 0x728,
            PortFInterruptStatus = 0x72C,
            PortGInterruptType = 0x730,
            PortGInterruptStatus = 0x734,
            PortHInterruptType = 0x738,
            PortHInterruptStatus = 0x73C,
        }
    }
}
