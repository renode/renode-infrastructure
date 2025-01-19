//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class STM32WBx5_HardwareSemaphore : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32WBx5_HardwareSemaphore(IMachine machine) : base(machine)
        {
            this.IRQ = new GPIO();

            for(var i = 0; i < SemaphoreCount; ++i)
            {
                semaphores[i] = new Semaphore();
            }

            DefineRegisters();
        }

        public override void Reset()
        {
            lock(lockObject)
            {
                base.Reset();
                foreach(var semaphore in semaphores)
                {
                    semaphore.Reset();
                }
                IRQ.Unset();
            }
        }

        public override uint ReadDoubleWord(long offset)
        {
            lock(lockObject)
            {
                return RegistersCollection.Read(offset);
            }
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            lock(lockObject)
            {
                RegistersCollection.Write(offset, value);
            }
        }

        public GPIO IRQ { get; }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            //  Two-step write lock
            //  Locking happens by writing a LOCK=1 value, along with the identifiers
            //  Reading this register allows checking the status of the lock
            Registers.Semaphore.DefineMany(this, SemaphoreCount, (reg, idx) =>
            {
                reg
                    .WithValueField(0, 8, out var processIdBits, name: "PROCID", valueProviderCallback: _ => semaphores[idx].ProcessID)
                    .WithValueField(8, 8, out var coreIdBits, name: "COREID", valueProviderCallback: _ => semaphores[idx].CoreID)
                    .WithReservedBits(16, 15)
                    .WithFlag(31, out var lockBits, name: "LOCK", valueProviderCallback: _ => semaphores[idx].Locked)
                    .WithWriteCallback((_, __) =>
                    {
                        semaphores[idx].WriteLock(lockBits.Value, (uint)processIdBits.Value, (uint)coreIdBits.Value);
                    });
            });

            //  One-step lock, read-only
            //  Locking happens automatically just by reading the register
            Registers.ReadLockSemaphore.DefineMany(this, SemaphoreCount, (reg, idx) =>
            {
                reg
                    .WithValueField(0, 8, FieldMode.Read, name: "PROCID", valueProviderCallback: _ => semaphores[idx].ProcessID)
                    .WithValueField(8, 8, FieldMode.Read, name: "COREID", valueProviderCallback: _ => semaphores[idx].CoreID)
                    .WithReservedBits(16, 15)
                    .WithFlag(31, FieldMode.Read, name: "LOCK", valueProviderCallback: _ => semaphores[idx].Locked)
                    .WithReadCallback((_, __) =>
                    {
                        // When software uses 1 Step locking method, hardware is expected
                        // to read CoreID from AHB bus, which is a constant value dependent
                        // on the CPU type. There are 2 possible values: 0x4 for Cortex-M4
                        // and 0x8 for Cortex-M0
                        if(!machine.SystemBus.TryGetCurrentCPU(out var cpu))
                        {
                            this.Log(LogLevel.Warning, "Failed getting current CPU");
                            return;
                        }

                        var coreId = 0u;
                        if(cpu.Model == "cortex-m4" || cpu.Model == "cortex-m4f")
                        {
                            coreId = 0x4;
                        }
                        else if(cpu.Model == "cortex-m0")
                        {
                            coreId = 0x8;
                        }
                        else
                        {
                            this.Log(LogLevel.Warning, "Unsupported cpu model: {0}", cpu.Model);
                            return;
                        }
                        semaphores[idx].ReadLock(coreId);
                    });
            });
        }

        private readonly object lockObject = new object();
        private readonly Semaphore[] semaphores = new Semaphore[SemaphoreCount];

        private const uint SemaphoreCount = 32;

        private enum Registers
        {
            Semaphore = 0x0, // R0
            ReadLockSemaphore = 0x80, // RLR0
            InterruptEnable1 = 0x100, // C1IER
            InterruptClear1 = 0x104, // C1ICR
            InterruptStatus1 = 0x108, // C1ISR
            MaskedInterruptStatus1 = 0x10c, // C1MISR
            InterruptEnable2 = 0x110, // C1IER
            InterruptClear2 = 0x114, // C1ICR
            InterruptStatus2 = 0x118, // C1ISR
            MaskedInterruptStatus2 = 0x11c, // C1MISR
            Clear = 0x140, // CR
            Key = 0x144 // KEYR
        }

        private class Semaphore
        {
            public Semaphore()
            {
                Reset();
            }

            //  Reset the state of the semaphore
            public void Reset()
            {
                Locked = false;
                ProcessID = 0;
                CoreID = 0;
            }

            //  Attempt to lock the semaphore via 1-step read lock
            public void ReadLock(uint coreId)
            {
                //  Process ID is unused and is always 0 in this case
                TryLock(0x0000, coreId);
            }

            //  Handles a write directed at the 2-step write lock register
            public void WriteLock(bool lockBit, uint processId, uint coreId)
            {
                if(lockBit)
                {
                    TryUnlock(processId, coreId);
                }
                else
                {
                    TryLock(processId, coreId);
                }
            }

            public bool Locked { get; private set; }
            public uint ProcessID { get; private set; }
            public uint CoreID { get; private set; }

            //  Tries to acquire a lock on the semaphore, with the given process ID and master ID
            private void TryLock(uint processId, uint coreId)
            {
                if(Locked)
                {
                    return;
                }

                Locked = true;
                ProcessID = processId;
                CoreID = coreId;
            }

            //  Tries to unlock the semaphore with the given credentials
            //  The semaphore will only be unlocked if it was previously locked, and its process ID and the core ID
            //  matches the ones used for unlocking.
            private void TryUnlock(uint processId, uint coreId)
            {
                if(!Locked)
                {
                    return;
                }
                if(ProcessID != processId)
                {
                    return;
                }
                if(CoreID != coreId)
                {
                    return;
                }

                Reset();
            }
        }
    }
}
