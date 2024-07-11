//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Extensions;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class SEMA4 : IBytePeripheral, IKnownSize, IDoubleWordPeripheral
    {
        public SEMA4(IMachine machine)
        {
            sysbus = machine.GetSystemBus(this);
            irqLock = new object();
            locks = new Lock[NumberOfEntries];
            for(var i = 0; i < locks.Length; i++)
            {
                locks[i] =  new Lock(this, i);
            }
            CPU0 = new GPIO();
            CPU1 = new GPIO();
        }

        public GPIO CPU0 { get; private set; }

        public GPIO CPU1 { get; private set; }

        public byte ReadByte(long offset)
        {
            if(offset < 16)
            {
                return locks[(int)offset].Read();
            }
            return this.ReadByteUsingDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            if(offset < 16)
            {
                locks[(int)offset].Write(value);
            }
            else
            {
                this.WriteByteUsingDoubleWord(offset, value);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(irqLock)
            {
                switch((Register)offset)
                {
                case Register.InterruptNotificationEnable1:
                    return (uint)(ApplyVybridErrata(interruptEnabled0) << 16);
                case Register.InterruptNotificationEnable2:
                    return (uint)(ApplyVybridErrata(interruptEnabled0) << 16);
                case Register.InterruptNotificationStatus1:
                    return (uint)(ApplyVybridErrata(notifyCPU0) << 16);
                case Register.InterruptNotificationStatus2:
                    return (uint)(ApplyVybridErrata(notifyCPU1) << 16);
                default:
                    if(offset < 16)
                    {
                        return this.ReadDoubleWordUsingByte(offset);
                    }
                    this.LogUnhandledRead(offset);
                    return 0;
                }
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(irqLock)
            {
                ushort valueToWrite;
                switch((Register)offset)
                {
                case Register.InterruptNotificationEnable1:
                    valueToWrite = ApplyVybridErrata((ushort)(value >> 16));
                    interruptEnabled0 = valueToWrite;
                    this.NoisyLog("Interrupt enabled for CPU0 set to 0x{0:X}.", valueToWrite);
                    break;
                case Register.InterruptNotificationEnable2:
                    valueToWrite = ApplyVybridErrata((ushort)(value >> 16));
                    interruptEnabled1 = valueToWrite;
                    this.NoisyLog("Interrupt enabled for CPU1 set to 0x{0:X}.", valueToWrite);
                    break;
                default:
                    this.WriteDoubleWordUsingByte(offset, value);
                    break;
                }
            }
        }

        public void Reset()
        {
            foreach(var @lock in locks)
            {
                @lock.Reset();
            }
            lock(irqLock)
            {
                interruptEnabled0 = 0;
                interruptEnabled1 = 0;
                notifyCPU0 = 0;
                notifyCPU1 = 0;
                RefreshInterrupts();
            }
        }

        public long Size
        {
            get
            {
                return 0x108;
            }
        }

        private void RefreshInterrupts()
        {
            var effectiveNotifyCPU0 = notifyCPU0 & interruptEnabled0;
            var effectiveNotifyCPU1 = notifyCPU1 & interruptEnabled1;
            this.NoisyLog("Effective interrupt state: CPU0: 0x{0:X} (0x{1:X} & 0x{2:X}), CPU1: 0x{3:X} (0x{4:X} & 0x{5:X}).", effectiveNotifyCPU0, notifyCPU0,
                interruptEnabled0, effectiveNotifyCPU1, notifyCPU1, interruptEnabled1);
            CPU0.Set(effectiveNotifyCPU0 != 0);
            CPU1.Set(effectiveNotifyCPU1 != 0);
        }

        private static ushort ApplyVybridErrata(ushort value)
        {
            var result = 0;
            for(var i = 0; i < 16; i++)
            {
                result |= (value & (1 << VybridErrataIndices[i])) != 0 ? (1 << i) : 0;
            }
            return (ushort)result;
        }

        private readonly Lock[] locks;
        private ushort interruptEnabled0;
        private ushort interruptEnabled1;
        private ushort notifyCPU0;
        private ushort notifyCPU1;
        private readonly IBusController sysbus;
        private readonly object irqLock;
        private const int NumberOfEntries = 16;
        private static readonly int[] VybridErrataIndices = { 12, 13, 14, 15, 8, 9, 10, 11, 4, 5, 6, 7, 0, 1, 2, 3 };

        private enum Register
        {
            InterruptNotificationEnable1 = 0x40,
            InterruptNotificationEnable2 = 0x48,
            InterruptNotificationStatus1 = 0x80,
            InterruptNotificationStatus2 = 0x88,
        }

        private class Lock
        {
            public Lock(SEMA4 sema, int number)
            {
                this.sema = sema;
                this.number = number;
            }

            public byte Read()
            {
                lock(this)
                {
                    return (byte)currentValue;
                }
            }

            public void Write(byte value)
            {
                if(value > 2)
                {
                    sema.Log(LogLevel.Warning, "Gate {0}: unsupported value {1} written to lock.", number, value);
                    return;
                    // ignore such writes
                }
                if(!sema.sysbus.TryGetCurrentCPU(out var cpu))
                {
                    sema.Log(LogLevel.Warning, "Gate {0}: write outside the CPU thread.", number);
                    return;
                    // write that did not come from CPU
                }
                int id = (int)cpu.MultiprocessingId + 1;
                lock(this)
                {
                    if(value == 0)
                    {
                        // unlock
                        if(currentValue == id)
                        {
                            currentValue = 0;
                            sema.DebugLog("Gate {0}: unlock by CPU {1}.", number, id - 1);
                            lock(sema.irqLock)
                            {
                                sema.NoisyLog("CPU waiting for lock: {0}.", cpuWaitingForLock);
                                switch(cpuWaitingForLock)
                                {
                                case 1:
                                    sema.notifyCPU0 |= (ushort)(1 << number);
                                    break;
                                case 2:
                                    sema.notifyCPU1 |= (ushort)(1 << number);
                                    break;
                                }
                                sema.RefreshInterrupts();
                            }
                        }
                        else
                        {
                            // unlock failed
                            sema.Log(LogLevel.Warning, "Gate {0}: unsuccesful unlock try by CPU {1}.", number, id - 1);
                        }
                    }
                    else if(value == id && (currentValue == 0 || currentValue == id))
                    {
                        // lock
                        currentValue = value;
                        sema.DebugLog("Gate {0}: lock by CPU {1}.", number, id - 1);
                        if(cpuWaitingForLock != 0)
                        {
                            lock(sema.irqLock)
                            {
                                // interrupt is always deasserted in that case
                                sema.notifyCPU0 &= (ushort)~(1 << number);
                                sema.notifyCPU1 &= (ushort)~(1 << number);
                                sema.RefreshInterrupts();
                            }
                            if(cpuWaitingForLock == id)
                            {
                                cpuWaitingForLock = 0;
                            }
                        }
                    }
                    else
                    {
                        // lock failed
                        sema.DebugLog("Gate {0}: unsuccessful lock try by CPU {1}.", number, id - 1);
                        cpuWaitingForLock = id;
                    }
                }
            }

            public void Reset()
            {
                lock(this)
                {
                    currentValue = 0;
                }
            }

            public int CPUWaitingForLock
            {
                get
                {
                    return cpuWaitingForLock;
                }
            }

            public bool InterruptActive { get; private set; }

            private uint currentValue;
            private int cpuWaitingForLock;
            private readonly SEMA4 sema;
            private readonly int number;
        }
    }
}

