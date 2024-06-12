//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using ELFSharp.ELF;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class Minerva : RiscV32
    {
        public Minerva(IMachine machine, uint hartId = 0, IRiscVTimeProvider timeProvider = null) : base(machine, "rv32i", timeProvider, hartId, PrivilegedArchitecture.Priv1_09, Endianess.LittleEndian)
        {
            CSRValidation = CSRValidationLevel.None;

            RegisterCSR((ulong)CSRs.IrqMask, () => (ulong)irqMask, value =>
            {
                lock(locker)
                {
                    irqMask = (uint)value;
                    this.Log(LogLevel.Noisy, "IRQ mask set to 0x{0:X}", irqMask);
                    Update();
                }
            });
            RegisterCSR((ulong)CSRs.IrqPending, () => (ulong)irqPending, value =>
            {
                lock(locker)
                {
                    irqPending = (uint)value;
                    this.Log(LogLevel.Noisy, "IRQ pending set to 0x{0:X}", irqPending);
                    Update();
                }
            });
        }

        public override void OnGPIO(int number, bool value)
        {
            lock(locker)
            {
                this.Log(LogLevel.Noisy, "GPIO #{0} set to: {1}", number, value);
                BitHelper.SetBit(ref irqPending, (byte)number, value);
                Update();
            }
        }

        private void Update()
        {
            //We support only Machine mode, with external interrupts. An additional interrupt controller would be required to have more advanced handling.
            base.OnGPIO((int)IrqType.MachineExternalInterrupt, (irqPending & irqMask) != 0);
        }

        private uint irqMask;
        private uint irqPending;

        private readonly object locker = new object();

        private enum CSRs
        {
            IrqMask = 0x330,
            IrqPending = 0x360,
        }
    }
}
