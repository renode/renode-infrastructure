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

            registerNames[(long)Registers.Control3] = "Control3";
            registerNames[(long)Registers.Status] = "Status";
            registerNames[(long)Registers.StatusClear] = "StatusClear";
            registerNames[(long)Registers.Acknowledge] = "Acknowledge";
            registerNames[(long)Registers.DataRamSecure] = "DataRamSecure";
            registerNames[(long)Registers.BpuReadWriteControl] = "BpuReadWriteControl";
            registerNames[(long)Registers.MainSensorLock] = "MainSensorLock";
            registerNames[(long)Registers.MainSensorEnable] = "MainSensorEnable";
        }

        private void DefineRegisters()
        {
            // Reserved 0x00-0x04
            Registers.Reserved00.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved04.Define(this)
                .WithReservedBits(0, 32);

            // Control3 at 0x08
            Registers.Control3.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "Control");

            // Reserved 0x0C-0x100 (62 words)
            for(int i = 0; i < 62; i++)
            {
                ((Registers)((long)Registers.Reserved0C + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // Status and Control registers 0x104-0x10C
            Registers.Status.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "Status");
            Registers.StatusClear.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "StatusClear");
            Registers.Acknowledge.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "Acknowledge");

            // Reserved 0x110-0x180 (29 words)
            for(int i = 0; i < 29; i++)
            {
                ((Registers)((long)Registers.Reserved110 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // DataRamSecure at 0x184
            Registers.DataRamSecure.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "DataRamSecure");

            // Reserved 0x188-0x1F8 (29 words)
            for(int i = 0; i < 29; i++)
            {
                ((Registers)((long)Registers.Reserved188 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // BpuReadWriteControl at 0x1FC
            Registers.BpuReadWriteControl.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "BpuReadWriteControl");

            // Reserved 0x200-0x3E8 (123 words)
            for(int i = 0; i < 123; i++)
            {
                ((Registers)((long)Registers.Reserved200 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // Main sensor lock and enable at 0x3EC-0x3F0
            Registers.MainSensorLock.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MainSensorLock");
            Registers.MainSensorEnable.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "MainSensorEnable");
        }

        private Dictionary<long, string> registerNames;

        private enum Registers : long
        {
            // Reserved 0x00-0x04
            Reserved00 = 0x000,
            Reserved04 = 0x004,

            // Control3 at 0x08
            Control3 = 0x008,

            // Reserved 0x0C-0x100
            Reserved0C = 0x00C,

            // Status and Control 0x104-0x10C
            Status = 0x104,
            StatusClear = 0x108,
            Acknowledge = 0x10C,

            // Reserved 0x110-0x180
            Reserved110 = 0x110,

            // DataRamSecure 0x184
            DataRamSecure = 0x184,

            // Reserved 0x188-0x1F8
            Reserved188 = 0x188,

            // BpuReadWriteControl 0x1FC
            BpuReadWriteControl = 0x1FC,

            // Reserved 0x200-0x3E8
            Reserved200 = 0x200,

            // Main sensor lock and enable 0x3EC-0x3F0
            MainSensorLock = 0x3EC,
            MainSensorEnable = 0x3F0,
        }
    }
}
