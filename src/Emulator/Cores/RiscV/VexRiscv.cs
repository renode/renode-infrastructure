//
// Copyright (c) 2010-2018 Antmicro
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
    public partial class VexRiscv : RiscV32
    {
        public VexRiscv(Core.Machine machine, uint hartId = 0, IRiscVTimeProvider timeProvider = null, PrivilegeArchitecture privilegeArchitecture = PrivilegeArchitecture.Priv1_09, string cpuType = "rv32im") : base(timeProvider, cpuType, machine, hartId, privilegeArchitecture, Endianess.LittleEndian)
        {
            TlibSetCsrValidation(0);

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
            RegisterCSR((ulong)CSRs.DCacheInfo, () => (ulong)dCacheInfo, value => dCacheInfo = (uint)value);
        }

        public override void OnGPIO(int number, bool value)
        {
            lock(locker)
            {
                this.Log(LogLevel.Noisy, "GPIO #{0} set to: {1}", number, value);
                if(number == MachineTimerInterruptCustomNumber)
                {
                    base.OnGPIO((int)IrqType.MachineTimerInterrupt, value);
                }
                else
                {
                    BitHelper.SetBit(ref irqPending, (byte)number, value);
                    Update();
                }
            }
        }

        private void Update()
        {
            //We support only Machine mode, with external interrupts. An additional interrupt controller would be required to have more advanced handling.
            base.OnGPIO((int)IrqType.MachineExternalInterrupt, (irqPending & irqMask) != 0);
        }

        private uint irqMask;
        private uint irqPending;
        private uint dCacheInfo;

        private readonly object locker = new object();

        // this is non-standard number for Machine Timer Interrupt,
        // but it's moved to 100 to avoid conflicts with VexRiscv
        // built-in interrupt manager that is mapped to IRQs 0-31
        private const int MachineTimerInterruptCustomNumber = 100;

        private enum CSRs
        {
            IrqMask = 0xBC0,
            IrqPending = 0xFC0,
            DCacheInfo = 0xCC0
        }
    }
}
