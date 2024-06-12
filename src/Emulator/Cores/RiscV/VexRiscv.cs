//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using ELFSharp.ELF;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class VexRiscv : RiscV32
    {
        public VexRiscv(IMachine machine, uint hartId = 0, IRiscVTimeProvider timeProvider = null, [NameAlias("privilegeArchitecture")] PrivilegedArchitecture privilegedArchitecture = PrivilegedArchitecture.Priv1_10, string cpuType = "rv32im_Zicsr_Zifencei", bool builtInIrqController = true) : base(machine, cpuType, timeProvider, hartId, privilegedArchitecture, Endianess.LittleEndian)
        {
            this.builtInIrqController = builtInIrqController;
            if(builtInIrqController)
            {
                RegisterCustomCSRs();
            }

            RegisterCustomInstructions();
        }

        // GPIOs for the original (non-standard) VexRiscv configuration
        // are divided into the following sections:
        //      0 - 31 : machine level external interrupts
        //         100 : machine level timer interrupt
        //         101 : machine level software interrupt
        // 1000 - 1031 : supervisor level external interrupts
        public override void OnGPIO(int number, bool value)
        {
            lock(locker)
            {
                this.Log(LogLevel.Noisy, "GPIO #{0} set to: {1}", number, value);
                if(!builtInIrqController)
                {
                    base.OnGPIO(number, value);
                }
                else
                {
                    if(number == MachineTimerInterruptCustomNumber)
                    {
                        base.OnGPIO((int)IrqType.MachineTimerInterrupt, value);
                    }
                    else if (number == MachineSoftwareInterruptCustomNumber)
                    {
                        base.OnGPIO((int)IrqType.MachineSoftwareInterrupt, value);
                    }
                    else if(number >= SupervisorExternalInterruptsOffset)
                    {
                        BitHelper.SetBit(ref supervisorInterrupts.Pending, (byte)(number - SupervisorExternalInterruptsOffset), value);
                        Update();
                    }
                    else // machine external
                    {
                         BitHelper.SetBit(ref machineInterrupts.Pending, (byte)number, value);
                         Update();
                    }
                }
            }
        }

        public override void Reset()
        {
            base.Reset();
            dCacheInfo = 0;

            if(builtInIrqController)
            {
                machineInterrupts = new Interrupts();
                supervisorInterrupts = new Interrupts();
            }
        }

        // this is a helper method to allow
        // setting irq mask from the monitor
        public void SetMachineIrqMask(uint mask)
        {
            machineInterrupts.Mask = mask;
        }

        // this is a helper method to allow
        // setting irq mask from the monitor
        public void SetSupervisorIrqMask(uint mask)
        {
            supervisorInterrupts.Mask = mask;
        }

        private void Update()
        {
            base.OnGPIO((int)IrqType.MachineExternalInterrupt, machineInterrupts.Any);
            base.OnGPIO((int)IrqType.SupervisorExternalInterrupt, supervisorInterrupts.Any);
        }

        private void HandleFlushDataCacheInstruction(UInt64 opcode)
        {
            // intentionally do nothing
            // there is no data cache in Renode anyway
        }

        private void RegisterCustomInstructions()
        {
            InstallCustomInstruction(pattern: "00000000000000000101000000001111", handler: HandleFlushDataCacheInstruction);
        }

        private void RegisterCustomCSRs()
        {
            // validate only privilege level when accessing CSRs
            // do not validate rw bit as VexRiscv custom CSRs do not follow the standard
            CSRValidation = CSRValidationLevel.PrivilegeLevel;

            RegisterCSR((ulong)CSRs.MachineIrqMask, () => (ulong)machineInterrupts.Mask, value =>
            {
                lock(locker)
                {
                    machineInterrupts.Mask = (uint)value;
                    this.Log(LogLevel.Noisy, "Machine IRQ mask set to 0x{0:X}", machineInterrupts.Mask);
                    Update();
                }
            });
            RegisterCSR((ulong)CSRs.MachineIrqPending, () => (ulong)machineInterrupts.Pending, value =>
            {
                lock(locker)
                {
                    machineInterrupts.Pending = (uint)value;
                    this.Log(LogLevel.Noisy, "Machine IRQ pending set to 0x{0:X}", machineInterrupts.Pending);
                    Update();
                }
            });
            RegisterCSR((ulong)CSRs.SupervisorIrqMask, () => (ulong)supervisorInterrupts.Mask, value =>
            {
                lock(locker)
                {
                    supervisorInterrupts.Mask = (uint)value;
                    this.Log(LogLevel.Noisy, "Supervisor IRQ mask set to 0x{0:X}", supervisorInterrupts.Mask);
                    Update();
                }
            });
            RegisterCSR((ulong)CSRs.SupervisorIrqPending, () => (ulong)supervisorInterrupts.Pending, value =>
            {
                lock(locker)
                {
                    supervisorInterrupts.Pending = (uint)value;
                    this.Log(LogLevel.Noisy, "Supervisor IRQ pending set to 0x{0:X}", supervisorInterrupts.Pending);
                    Update();
                }
            });
            RegisterCSR((ulong)CSRs.DCacheInfo, () => (ulong)dCacheInfo, value => dCacheInfo = (uint)value);
        }

        private Interrupts machineInterrupts;
        private Interrupts supervisorInterrupts;
        private uint dCacheInfo;

        private readonly bool builtInIrqController;
        private readonly object locker = new object();

        // this is non-standard number for Machine Timer Interrupt,
        // but it's moved to 100 to avoid conflicts with VexRiscv
        // built-in interrupt manager that is mapped to IRQs 0-31
        private const int MachineTimerInterruptCustomNumber = 100;

        // this is non-standard number for Machine Software Interrupt,
        // but it's moved to 101 to avoid conflicts with VexRiscv
        // built-in interrupt manager that is mapped to IRQs 0-31
        private const int MachineSoftwareInterruptCustomNumber = 101;

        private const int SupervisorExternalInterruptsOffset = 1000;

        private struct Interrupts
        {
            public uint Mask;
            public uint Pending;

            public bool Any => (Mask & Pending) != 0;
        }

        private enum CSRs
        {
            MachineIrqMask = 0xBC0,
            MachineIrqPending = 0xFC0,
            SupervisorIrqMask = 0x9C0,
            SupervisorIrqPending = 0xDC0,
            DCacheInfo = 0xCC0
        }
    }
}
