//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using System;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class AINTC : IDoubleWordPeripheral, IIRQController, IKnownSize
    {
        public AINTC()
        {
            IRQ = new GPIO();
            FIQ = new GPIO();
            interrupts = new InterruptStatus[64];
            SetupRegisters();
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.FastInterruptRequestStatus0:
                return ReadStatusRegister(0, InterruptType.FIQ);
            case Registers.FastInterruptRequestStatus1:
                return ReadStatusRegister(1, InterruptType.FIQ);
            case Registers.InterruptRequestStatus0:
                return ReadStatusRegister(0, InterruptType.IRQ);
            case Registers.InterruptRequestStatus1:
                return ReadStatusRegister(1, InterruptType.IRQ);
            case Registers.InterruptEnable0:
                return ReadInterruptEnableRegister(0);
            case Registers.InterruptEnable1:
                return ReadInterruptEnableRegister(1);
            case Registers.InterruptRequestEntryAddress:
                return (uint)(entryTableBaseAddress + (bestIrq + 1) * 4);
            case Registers.FastInterruptRequestEntryAddress:
                return (uint)(entryTableBaseAddress + (bestFiq + 1) * 4);
            case Registers.EntryTableBaseAddress:
                return entryTableBaseAddress;
            case Registers.InterruptOperationControl:
                return interruptOperationControl.Read();
            default:
                return interruptPriorityRegisterCollection.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.FastInterruptRequestStatus0:
            case Registers.InterruptRequestStatus0:
                WriteStatusRegister(0, value);
                break;
            case Registers.InterruptRequestStatus1:
            case Registers.FastInterruptRequestStatus1:
                WriteStatusRegister(1, value);
                break;
            case Registers.InterruptEnable0:
                WriteEnableRegister(0, value);
                break;
            case Registers.InterruptEnable1:
                WriteEnableRegister(1, value);
                break;
            case Registers.InterruptOperationControl:
                interruptOperationControl.Write(offset, value);
                break;
            case Registers.EntryTableBaseAddress:
                entryTableBaseAddress = value;
                break;
            default:
                interruptPriorityRegisterCollection.Write(offset, value);
                break; 
            }
        }

        public void Reset()
        {
            Array.Clear(interrupts, 0, interrupts.Length);
            interruptPriorityRegisterCollection.Reset();
            interruptOperationControl.Reset();
            entryTableBaseAddress = 0;
        }

        public void OnGPIO(int number, bool value)
        {
            this.NoisyLog("Received interrupt! Number {0} value {1}", number, value);
            lock(interrupts)
            {
                if(value)
                {
                    interrupts[number] |= InterruptStatus.Running;
                    interrupts[number] |= InterruptStatus.Pending;
                }
                else
                {
                    interrupts[number] &= ~InterruptStatus.Running;
                }
                Update();
            }
        }

        public long Size
        {
            get
            {
                return 0x400;
            }
        }

        public GPIO IRQ
        {
            get;
            private set;
        }

        public GPIO FIQ
        {
            get;
            private set;
        }

        private void Update()
        {
            var bestFiqPriority = 3u;
            var bestIrqPriority = 8u;
            bestFiq = -1;
            bestIrq = -1;

            for(var i = 0; i < interrupts.Length; i++)
            {
                if(IsCandidate(i))
                {
                    var type = GetInterruptType(i);
                    if(type == InterruptType.FIQ)
                    {
                        if(priorities[i].Value < bestFiqPriority)
                        {
                            bestFiqPriority = (uint)priorities[i].Value;
                            bestFiq = i;
                        }
                    }
                    else
                    {
                        if(priorities[i].Value < bestIrqPriority)
                        {
                            bestIrqPriority = (uint)priorities[i].Value;
                            bestIrq = i;
                        }
                    }
                }
            }
            IRQ.Set(bestIrq != -1);
            FIQ.Set(bestFiq != -1);
        }

        private uint ReadStatusRegister(int number, InterruptType type)
        {
            var value = 0u;
            lock(interrupts)
            {
                for(var i = 0; i < 32; ++i)
                {
                    var status = interrupts[32 * number + i];
                    if((status & InterruptStatus.Running) != 0 && GetInterruptType(i) == type)
                    {
                        value |= (1u << i);
                    }
                }
            }
            return value;
        }

        private void WriteStatusRegister(int number, uint value)
        {
            lock(interrupts)
            {
                for(var i = 0; i < 32; ++i)
                {
                    var offset = i + 32 * number;
                    if((value & (1 << i)) != 0)
                    {
                        if((interrupts[offset] & InterruptStatus.Running) != 0)
                        {
                            interrupts[offset] |= InterruptStatus.Pending;
                        }
                        else
                        {
                            interrupts[offset] &= ~InterruptStatus.Pending;
                        }
                    }
                }    
                Update();
            }
        }

        private uint ReadInterruptEnableRegister(int number)
        {
            var value = 0u;
            lock(interrupts)
            {
                for(var i = 0; i < 32; ++i)
                {
                    if((interrupts[32 * number + i] & InterruptStatus.Enabled) != 0)
                    {
                        value |= (1u << i);
                    }
                }
            }
            return value;
        }

        private void WriteEnableRegister(int number, uint value)
        {
            lock(interrupts)
            {
                for(var i = 0; i < 32; ++i)
                {
                    interrupts[32 * number + i] = (value & (1 << i)) != 0 ? InterruptStatus.Enabled : 0;
                }
                Update();
            }
        }

        private bool IsCandidate(int number)
        {
            var status = interrupts[number];
            var type = GetInterruptType(number);
            var isEnabled = (status & InterruptStatus.Enabled) != 0 || (reflectMaskedFiq.Value && type == InterruptType.FIQ) || (reflectMaskedIrq.Value && type == InterruptType.IRQ);
            return (status & InterruptStatus.Pending) != 0 && isEnabled;
        }

        private void SetupRegisters()
        {
            var interruptPriorityRegisters = new Dictionary<long, DoubleWordRegister>();
            priorities = new IValueRegisterField[64];

            for(var i = 0; i < 8; i++)
            {
                var registerKey = (long)Registers.InterruptPriority0 + 4 * i;
                interruptPriorityRegisters.Add(registerKey, new DoubleWordRegister(this, 0x77777777));
                for(var j = 0; j < 8; j++)
                {
                    priorities[i * 8 + j] = interruptPriorityRegisters[registerKey].DefineValueField(4 * j, 3, writeCallback: (oldValue, newValue) => Update());
                }
            }

            interruptPriorityRegisterCollection = new DoubleWordRegisterCollection(this, interruptPriorityRegisters);

            interruptOperationControl = new DoubleWordRegister(this);
            reflectMaskedFiq = interruptOperationControl.DefineFlagField(0, writeCallback: (oldValue, newValue) => Update());
            reflectMaskedIrq = interruptOperationControl.DefineFlagField(1, writeCallback: (oldValue, newValue) => Update());
            interruptOperationControl.DefineFlagField(2, changeCallback: (oldValue, newValue) => {
                if(newValue)
                {
                    this.Log(LogLevel.Warning, "Unsupported delayed interrupt enable/disable mode was set.");
                }
            });
        }

        private InterruptType GetInterruptType(int number)
        {
            return priorities[number].Value < 2 ? InterruptType.FIQ : InterruptType.IRQ;
        }

        private IValueRegisterField[] priorities;
        private IFlagRegisterField reflectMaskedIrq, reflectMaskedFiq;
        private InterruptStatus[] interrupts;
        private DoubleWordRegisterCollection interruptPriorityRegisterCollection;
        private DoubleWordRegister interruptOperationControl;
        private uint entryTableBaseAddress;
        private int bestIrq = -1, bestFiq = -1;

        private enum Registers
        {
            FastInterruptRequestStatus0 = 0x0,
            FastInterruptRequestStatus1 = 0x4,
            InterruptRequestStatus0 = 0x8,
            InterruptRequestStatus1 = 0xc,
            FastInterruptRequestEntryAddress = 0x10,
            InterruptRequestEntryAddress = 0x14,
            InterruptEnable0 = 0x18,
            InterruptEnable1 = 0x1c,
            InterruptOperationControl = 0x20,
            EntryTableBaseAddress = 0x24,
            InterruptPriority0 = 0x30,
            InterruptPriority1 = 0x34,
            InterruptPriority2 = 0x38,
            InterruptPriority3 = 0x3c,
            InterruptPriority4 = 0x40,
            InterruptPriority5 = 0x44,
            InterruptPriority6 = 0x48,
            InterruptPriority7 = 0x4c,
        }

        [Flags]
        private enum InterruptStatus
        {
            Enabled = 1,
            Running = 2,
            Pending = 4
        }

        private enum InterruptType
        {
            FIQ,
            IRQ
        }
    }
}
