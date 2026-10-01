//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Gerzain Mata <leftger@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class STM32_HSEM : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32_HSEM(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            semaphores = new SemaphoreChannel[NumberOfSemaphores];
            for(var i = 0; i < NumberOfSemaphores; ++i)
            {
                semaphores[i] = new SemaphoreChannel();
            }

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            for(var i = 0; i < NumberOfSemaphores; ++i)
            {
                semaphores[i].Reset();
            }
            interruptEnable = 0;
            interruptStatus = 0;
            UpdateInterrupt();
        }

        public GPIO IRQ { get; }
        public long Size => 0x400;

        private void DefineRegisters()
        {
            // 2-step write lock registers R0 - R31 (0x00 - 0x7C)
            for(var i = 0; i < NumberOfSemaphores; ++i)
            {
                var index = i;
                Registers.R0.AddOffset(this, (ulong)(index * 4))
                    .WithValueField(0, 32,
                        valueProviderCallback: _ => semaphores[index].ReadRegister(),
                        writeCallback: (_, val) =>
                        {
                            if(semaphores[index].WriteRegister((uint)val))
                            {
                                SignalChannelFreed(index);
                            }
                        },
                        name: $"R{index}");

                // 1-step read lock registers RLR0 - RLR31 (0x80 - 0xFC)
                Registers.RLR0.AddOffset(this, (ulong)(index * 4))
                    .WithValueField(0, 32,
                        valueProviderCallback: _ => semaphores[index].ReadLockRegister(),
                        writeCallback: (_, __) => {},
                        name: $"RLR{index}");
            }

            Registers.C1IER.Define(this)
                .WithValueField(0, 32,
                    valueProviderCallback: _ => interruptEnable,
                    writeCallback: (_, val) =>
                    {
                        interruptEnable = (uint)val;
                        UpdateInterrupt();
                    },
                    name: "IER");

            Registers.C1ICR.Define(this)
                .WithValueField(0, 32, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        interruptStatus &= ~(uint)val;
                        UpdateInterrupt();
                    },
                    name: "ICR");

            Registers.C1ISR.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => interruptStatus,
                    name: "ISR");

            Registers.C1MISR.Define(this)
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => interruptStatus & interruptEnable,
                    name: "MISR");

            Registers.CR.Define(this)
                .WithValueField(0, 32, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        var key = (val >> 16) & 0xFFFF;
                        var coreId = (byte)((val >> 8) & 0xF);
                        if(key == 0xAAAA)
                        {
                            for(var i = 0; i < NumberOfSemaphores; ++i)
                            {
                                if(semaphores[i].Locked && semaphores[i].CoreId == coreId)
                                {
                                    semaphores[i].Reset();
                                    SignalChannelFreed(i);
                                }
                            }
                        }
                    },
                    name: "CR");

            Registers.KEYR.Define(this)
                .WithValueField(16, 16, FieldMode.Read,
                    valueProviderCallback: _ => 0xAAAA,
                    name: "KEY");
        }

        private void SignalChannelFreed(int channel)
        {
            interruptStatus |= (1u << channel);
            UpdateInterrupt();
        }

        private void UpdateInterrupt()
        {
            var isAsserted = (interruptStatus & interruptEnable) != 0;
            IRQ.Set(isAsserted);
        }

        private const int NumberOfSemaphores = 32;
        private readonly SemaphoreChannel[] semaphores;
        private uint interruptEnable;
        private uint interruptStatus;

        private class SemaphoreChannel
        {
            public bool Locked { get; private set; }
            public byte CoreId { get; private set; }
            public byte ProcId { get; private set; }

            public void Reset()
            {
                Locked = false;
                CoreId = 0;
                ProcId = 0;
            }

            public uint ReadRegister()
            {
                if(!Locked)
                {
                    return 0;
                }
                return 0x80000000u | ((uint)CoreId << 8) | ProcId;
            }

            public bool WriteRegister(uint value)
            {
                var lockRequest = (value & 0x80000000u) != 0;
                var coreId = (byte)((value >> 8) & 0xF);
                var procId = (byte)(value & 0xFF);

                if(lockRequest)
                {
                    if(!Locked)
                    {
                        Locked = true;
                        CoreId = coreId;
                        ProcId = procId;
                    }
                    return false;
                }
                else
                {
                    // Unlock request: only succeeding if caller matches current lock holder
                    if(Locked && CoreId == coreId && ProcId == procId)
                    {
                        Locked = false;
                        CoreId = 0;
                        ProcId = 0;
                        return true; // Channel was freed
                    }
                    return false;
                }
            }

            public uint ReadLockRegister()
            {
                if(!Locked)
                {
                    // 1-step lock: successful read-lock returns 0 and locks channel
                    Locked = true;
                    CoreId = 0;
                    ProcId = 0;
                    return 0;
                }
                // Already locked: return locked state with lock bit set
                return 0x80000000u | ((uint)CoreId << 8) | ProcId;
            }
        }

        private enum Registers
        {
            R0 = 0x00,
            RLR0 = 0x80,
            C1IER = 0x100,
            C1ICR = 0x104,
            C1ISR = 0x108,
            C1MISR = 0x10C,
            C2IER = 0x110,
            C2ICR = 0x114,
            C2ISR = 0x118,
            C2MISR = 0x11C,
            CR = 0x140,
            KEYR = 0x144,
        }
    }
}
