//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals;
using System.Threading;
using System.Collections.Generic;
using Antmicro.Renode.Time;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class NVIC : IDoubleWordPeripheral, IKnownSize, IIRQController
    {
        public NVIC(Machine machine, long systickFrequency = 50 * 0x800000, byte priorityMask = 0xFF)
        {
            priorities = new byte[IRQCount];
            activeIRQs = new Stack<int>();
            systick = new LimitTimer(machine.ClockSource, systickFrequency, this, nameof(systick), uint.MaxValue, Direction.Descending, false, autoUpdate: true);
            this.priorityMask = priorityMask;
            irqs = new IRQState[IRQCount];
            IRQ = new GPIO();
            resetMachine = machine.RequestReset;
            systick.LimitReached += () =>
            {
                countFlag = true;
                SetPendingIRQ(15);
            };
            InitInterrupts();
            Init();
        }

        public void AttachCPU(CortexM cpu)
        {
            this.cpu = cpu;
        }

        public ManualResetEvent MaskedInterruptPresent { get { return maskedInterruptPresent; } }

        public IEnumerable<int> GetEnabledExternalInterrupts()
        {
            return irqs.Skip(16).Select((x,i) => new {x,i}).Where(y => (y.x & IRQState.Enabled) != 0).Select(y => y.i).OrderBy(x => x);
        }

        public IEnumerable<int> GetEnabledInternalInterrupts()
        {
            return irqs.Take(16).Select((x,i) => new {x,i}).Where(y => (y.x & IRQState.Enabled) != 0).Select(y => y.i).OrderBy(x => x);
        }

        public int Divider
        {
            set
            {
                systick.Divider = value;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset >= PriorityStart && offset < PriorityEnd)
            {
                return HandlePriorityRead(offset - PriorityStart, true);
            }
            if(offset >= SetEnableStart && offset < SetEnableEnd)
            {
                return HandleSetEnableRead(offset - SetEnableStart);
            }
            switch((Registers)offset)
            {
            case Registers.VectorTableOffset:
                return cpu.VectorTableOffset;
            case Registers.CPUID:
                return CPUID;
            case Registers.CoprocessorAccessControl:
                return CPACR;
            case Registers.InterruptControlState:
                lock(irqs)
                {
                    var activeIRQ = activeIRQs.Count == 0 ? SpuriousInterrupt : activeIRQs.Peek();
                    return (uint)activeIRQ;
                }
            case Registers.SysTickControl:
                var currentCountFlag = countFlag ? 1u << 16 : 0;
                countFlag = false;
                return (currentCountFlag
                        | 4u // core clock CLKSOURCE
                        | ((systick.EventEnabled ? 1u : 0u) << 1)
                        | (systick.Enabled ? 1u : 0u));
            case Registers.SysTickReloadValue:
                return (uint)systick.Limit;
            case Registers.SysTickValue:
                return (uint)systick.Value;
            case Registers.SystemControlRegister:
                return 0;
            case Registers.SystemHandlerPriority1:
            case Registers.SystemHandlerPriority2:
            case Registers.SystemHandlerPriority3:
                return HandlePriorityRead(offset - 0xD14, false);
            case Registers.SystemHandlerControlAndState:
                this.DebugLog("Read from SHCS register. This is not yet implemented. Returning 0");
                return 0;
            case Registers.ApplicationInterruptAndReset:
                return HandleApplicationInterruptAndResetRead();
            case Registers.ConfigurableFaultStatus:
                // this is not a full implementation but should be enough to get lazy FPU implementations to work.
                this.DebugLog("ConfigurableFaultStatus read. Returning NOCP.");
                return (1 << 19) /* NOCP */;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public GPIO IRQ { get; private set; }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset >= SetEnableStart && offset < SetEnableEnd)
            {
                EnableOrDisableInterrupt((int)offset - SetEnableStart, value, true);
                return;
            }
            if(offset >= PriorityStart && offset < PriorityEnd)
            {
                HandlePriorityWrite(offset - PriorityStart, true, value);
                return;
            }
            if(offset >= ClearEnableStart && offset < ClearEnableEnd)
            {
                EnableOrDisableInterrupt((int)offset - ClearEnableStart, value, false);
                return;
            }
            if(offset >= ClearPendingStart && offset < ClearPendingEnd)
            {
                SetOrClearPendingInterrupt((int)offset - ClearPendingStart, value, false);
                return;
            }
            if(offset >= SetPendingStart && offset < SetPendingEnd)
            {
                SetOrClearPendingInterrupt((int)offset - SetPendingStart, value, true);
                return;
            }
            switch((Registers)offset)
            {
            case Registers.SysTickControl:
                systick.EventEnabled = ((value & 2) >> 1) != 0;
                this.NoisyLog("Systick interrupt {0}.", systick.EventEnabled ? "enabled" : "disabled");
                systick.Enabled = (value & 1) != 0;
                this.NoisyLog("Systick timer {0}.", systick.Enabled ? "enabled" : "disabled");
                break;
            case Registers.SysTickReloadValue:
                systick.Limit = value & SysTickMaximumValue;
                if(value > SysTickMaximumValue)
                {
                    this.Log(LogLevel.Warning, "Given value {0} exceeds maximum available {1}. Writing {2}", value, SysTickMaximumValue, systick.Limit);
                }
                break;
            case Registers.SysTickValue:
                systick.Value = systick.Limit;
                break;
            case Registers.InterruptControlState:
                if((value & (1u << 28)) != 0)
                {
                    SetPendingIRQ(14);
                }
                break;
            case Registers.VectorTableOffset:
                cpu.VectorTableOffset = value & 0xFFFFFF80;
                break;
            case Registers.ApplicationInterruptAndReset:
                var key = value >> 16;
                if(key != VectKey)
                {
                    this.DebugLog("Wrong key while accessing Application Interrupt and Reset Control register 0x{0:X}.", key);
                    break;
                }
                binaryPointPosition = (int)(value >> 8) & 7;
                if (((value >> 2) & 1) == 1)
                {
                    resetMachine();
                }
                break;
            case Registers.SystemControlRegister:
                if((value & DeepSleep) != 0)
                {
                    systick.Enabled = false;
                    this.NoisyLog("Entering deep sleep");
                }
                else if((value & ~DeepSleep) != 0)
                {
                    this.Log(LogLevel.Warning, "Unhandled value written to System Control Register: 0x{0:X}.", value & ~DeepSleep);
                }
                break;
            case Registers.SystemHandlerPriority1:
                // 7th interrupt is ignored
                priorities[4] = (byte)value;
                priorities[5] = (byte)(value >> 8);
                priorities[6] = (byte)(value >> 16);
                this.DebugLog("Priority of IRQs 4, 5, 6 set to 0x{0:X}, 0x{1:X}, 0x{2:X} respectively.", (byte)value, (byte)(value >> 8), (byte)(value >> 16));
                break;
            case Registers.SystemHandlerPriority2:
                // only 11th is not ignored
                priorities[11] = (byte)(value >> 24);
                this.DebugLog("Priority of IRQ 11 set to 0x{0:X}.", (byte)(value >> 24));
                break;
            case Registers.SystemHandlerPriority3:
                priorities[14] = (byte)(value >> 16);
                priorities[15] = (byte)(value >> 24);
                this.DebugLog("Priority of IRQs 14, 15 set to 0x{0:X}, 0x{1:X} respectively.", (byte)(value >> 16), (byte)(value >> 24));
                break;
            case Registers.SystemHandlerControlAndState:
                this.DebugLog("Write to SHCS register. This is not yet implemented. Value written was 0x{0:X}.", value);
                break;
            case Registers.CoprocessorAccessControl:
                // if CP10 and CP11 both are set to full access (0b11) - turn on FPU
                if ((value & 0xF00000) == 0xF00000) {
                    this.DebugLog("Enabling FPU.");
                    cpu.FpuEnabled = true;
                } else {
                    this.DebugLog("Disabling FPU.");
                    cpu.FpuEnabled = false;
                }
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void Reset()
        {
            InitInterrupts();
            for(var i = 0; i < priorities.Length; i++)
            {
                priorities[i] = 0x00;
            }
            activeIRQs.Clear();
            systick.Reset();
            systick.EventEnabled = false;
            systick.AutoUpdate = true;
            IRQ.Unset();
            countFlag = false;
        }

        public long Size
        {
            get
            {
                return 0x1000;
            }
        }

        public int AcknowledgeIRQ()
        {
            lock(irqs)
            {
                var result = FindPendingInterrupt();
                if(result != SpuriousInterrupt)
                {
                    irqs[result] |= IRQState.Active;
                    irqs[result] &= ~IRQState.Pending;
                    this.NoisyLog("Acknowledged IRQ {0}.", result);
                    activeIRQs.Push(result);
                }
                // at this point we can surely deactivate interrupt, because the best was chosen
                IRQ.Set(false);
                return result;
            }
        }

        public void CompleteIRQ(int number)
        {
            lock(irqs)
            {
                var currentIRQ = irqs[number];
                if((currentIRQ & IRQState.Active) == 0)
                {
                    throw new InvalidOperationException(string.Format("Trying to complete not active IRQ {0}.", number));
                }
                irqs[number] &= ~IRQState.Active;
                var activeIRQ = activeIRQs.Pop();
                if(activeIRQ != number)
                {
                    throw new InvalidOperationException(string.Format("Trying to complete IRQ {0} that was not the last active. Last active was {1}.",
                                number, activeIRQ));
                }
                if((currentIRQ & IRQState.Running) > 0)
                {
                    this.NoisyLog("Completed IRQ {0} active -> pending.", number);
                    irqs[number] |= IRQState.Pending;
                }
                else
                {
                    this.NoisyLog("Completed IRQ {0} active -> inactive.", number);
                }
                FindPendingInterrupt();
            }
        }

        public void SetPendingIRQ(int number)
        {
            lock(irqs)
            {
                this.NoisyLog("Internal IRQ {0}.", number);
                if((irqs[number] & IRQState.Active) == 0)
                {
                    irqs[number] |= IRQState.Pending;
                }
                FindPendingInterrupt();
            }
        }

        public void OnGPIO(int number, bool value)
        {
            number += 16; // because this is HW interrupt
            this.NoisyLog("External IRQ {0}: {1}", number, value);
            lock(irqs)
            {
                if(value)
                {
                    systick.Enabled = true;
                    this.NoisyLog("Waking up from deep sleep");
                    irqs[number] |= IRQState.Running;
                    // let's latch it if not active
                    if((irqs[number] & IRQState.Active) == 0)
                    {
                        irqs[number] |= IRQState.Pending;
                    }
                }
                else
                {
                    irqs[number] &= ~IRQState.Running;
                }
                FindPendingInterrupt();
            }
        }

        private void InitInterrupts()
        {
            Array.Clear(irqs, 0, irqs.Length);
            for(var i = 0; i < 16; i++)
            {
                irqs[i] = IRQState.Enabled;
            }
        }

        private static int GetStartingInterrupt(long offset, bool externalInterrupt)
        {
            return (int)(offset + (externalInterrupt ? 16 : 0));
        }

        private void HandlePriorityWrite(long offset, bool externalInterrupt, uint value)
        {
            lock(irqs)
            {
                var startingInterrupt = GetStartingInterrupt(offset, externalInterrupt);
                for(var i = startingInterrupt; i < startingInterrupt + 4; i++)
                {

                    if((((byte)value) & ~priorityMask) != 0)
                    {
                        this.Log(LogLevel.Warning, "Trying to set the priority for interrupt {0} to 0x{1:X}, but it should be maskable with 0x{2:X}", i, value, priorityMask);
                    }

                    priorities[i] = (byte)(value & priorityMask);

                    this.DebugLog("Priority 0x{0:X} set for interrupt {1}.", priorities[i], i);
                    value >>= 8;
                }
            }
        }

        private uint HandlePriorityRead(long offset, bool externalInterrupt)
        {
            lock(irqs)
            {
                var returnValue = 0u;
                var startingInterrupt = GetStartingInterrupt(offset, externalInterrupt);
                for(var i = startingInterrupt + 3; i > startingInterrupt; i--)
                {
                    returnValue |= priorities[i];
                    returnValue <<= 8;
                }
                returnValue |= priorities[startingInterrupt];
                return returnValue;
            }
        }

        private uint HandleApplicationInterruptAndResetRead()
        {
            var returnValue = (uint)VectKeyStat << 16;
            returnValue |= ((uint)binaryPointPosition << 8);
            return returnValue;
        }

        private void EnableOrDisableInterrupt(int offset, uint value, bool enable)
        {
            lock(irqs)
            {
                var firstIRQNo = 8 * offset + 16;  // 16 is added because this is HW interrupt
                {
                    var lastIRQNo = firstIRQNo + 31;
                    var mask = 1u;
                    for(var i = firstIRQNo; i <= lastIRQNo; i++)
                    {
                        if((value & mask) > 0)
                        {
                            if(enable)
                            {
                                this.NoisyLog("Enabled IRQ {0}.", i);
                                irqs[i] |= IRQState.Enabled;
                            }
                            else
                            {
                                this.NoisyLog("Disabled IRQ {0}.", i);
                                irqs[i] &= ~IRQState.Enabled;
                            }
                        }
                        mask <<= 1;
                    }
                    FindPendingInterrupt();
                }
            }
        }

        private uint HandleSetEnableRead(long offset)
        {
            lock(irqs)
            {
                var firstIRQNo = 8 * offset + 16;
                var lastIRQNo = firstIRQNo + 31;
                var result = 0u;
                for(var i = lastIRQNo; i > firstIRQNo; i--)
                {
                    result |= ((irqs[i] & IRQState.Enabled) != 0) ? 1u : 0u;
                    result <<= 1;
                }
                result |= ((irqs[firstIRQNo] & IRQState.Enabled) != 0) ? 1u : 0u;
                return result;
            }
        }

        private void SetOrClearPendingInterrupt(int offset, uint value, bool set)
        {
            lock(irqs)
            {
                var firstIRQNo = 8 * offset + 16;  // 16 is added because this is HW interrupt
                {
                    var lastIRQNo = firstIRQNo + 31;
                    var mask = 1u;
                    for(var i = firstIRQNo; i <= lastIRQNo; i++)
                    {
                        if((value & mask) > 0)
                        {
                            if (set)
                            {
                                this.DebugLog("Set pending IRQ {0}.", i);
                                irqs[i] |= IRQState.Pending;
                            }
                            else
                            {
                                this.DebugLog("Cleared pending IRQ {0}.", i);
                                irqs[i] &= ~IRQState.Running;
                            }
                        }
                        mask <<= 1;
                    }
                    FindPendingInterrupt();
                }
            }
        }

        private int FindPendingInterrupt()
        {
            lock(irqs)
            {
                var bestPriority = 0xFF + 1;
                var preemptNeeded = activeIRQs.Count != 0;
                var result = SpuriousInterrupt; // TODO (and some log?)

                for(var i = 0; i < irqs.Length; i++)
                {
                    var currentIRQ = irqs[i];
                    if(IsCandidate(currentIRQ, i) && priorities[i] < bestPriority)
                    {
                        result = i;
                        bestPriority = priorities[i];
                    }
                }
                if(preemptNeeded)
                {
                    var activePriority = preemptNeeded ? (int)priorities[activeIRQs.Peek()] : 0;
                    if(!DoesAPreemptB(bestPriority, activePriority))
                    {
                        result = SpuriousInterrupt;
                    }
                    else
                    {
                        this.NoisyLog("IRQ {0} preempts {1}.", result, activeIRQs.Peek());
                    }
                }
                IRQ.Set(!PRIMASK && result != SpuriousInterrupt);

                if(result != SpuriousInterrupt)
                {
                    maskedInterruptPresent.Set();
                }
                else
                {
                    maskedInterruptPresent.Reset();
                }

                return result;
            }
        }

        private bool IsCandidate(IRQState state, int index)
        {
            var result = (state & IRQState.Pending) != 0 && (state & IRQState.Enabled) != 0 && (state & IRQState.Active) == 0;
            if (BASEPRI != 0)
            {
                result &= (priorities[index] < BASEPRI);
            }

            return result;
        }

        private bool DoesAPreemptB(int priorityA, int priorityB)
        {
            var binaryPointMask = ~((1 << binaryPointPosition + 1) - 1);
            return (priorityA & binaryPointMask) < (priorityB & binaryPointMask);
        }

        [PostDeserialization]
        private void Init()
        {
            maskedInterruptPresent = new ManualResetEvent(false);
        }

        private bool primask;
        public bool PRIMASK
        {
            get { return primask; }
            set
            {
                primask = value;
                FindPendingInterrupt();
            }
        }

        private byte basepri;
        public byte BASEPRI
        {
            get { return basepri; }
            set
            {
                basepri = value;
                FindPendingInterrupt();
            }
        }

        [Flags]
        private enum IRQState : byte
        {
            Running = 1,
            Pending = 2,
            Active = 4,
            Enabled = 32
        }

        private enum Registers
        {
            SysTickControl = 0x10,
            SysTickReloadValue = 0x14,
            SysTickValue = 0x18,
            CPUID = 0xD00,
            InterruptControlState = 0xD04,
            VectorTableOffset = 0xD08,
            SystemControlRegister = 0xD10,
            ApplicationInterruptAndReset = 0xD0C,
            SystemHandlerPriority1 = 0xD18,
            SystemHandlerPriority2 = 0xD1C,
            SystemHandlerPriority3 = 0xD20,
            SystemHandlerControlAndState = 0xD24,
            ConfigurableFaultStatus = 0xD28,
            HardFaultStatus = 0xD2C,
            DebugFaultStatus = 0xD30,
            // FPU registers 0xD88 .. F3C
            CoprocessorAccessControl = 0xD88
        }

        private uint CPACR = 0x0;

        private bool countFlag;
        private byte priorityMask;
        private Stack<int> activeIRQs;
        private int binaryPointPosition; // from the right

        [Transient]
        private ManualResetEvent maskedInterruptPresent;

        private readonly IRQState[] irqs;
        private readonly byte[] priorities;
        private readonly Action resetMachine;
        private CortexM cpu;
        private readonly LimitTimer systick;

        private const int SpuriousInterrupt    = 256;
        private const int SetEnableStart       = 0x100;
        private const int SetEnableEnd         = 0x120;
        private const int ClearEnableStart     = 0x180;
        private const int ClearEnableEnd       = 0x200;
        private const int ClearPendingStart    = 0x280;
        private const int ClearPendingEnd      = 0x300;
        private const int SetPendingStart      = 0x200;
        private const int SetPendingEnd        = 0x280;
        private const int PriorityStart        = 0x400;
        private const int PriorityEnd          = 0x4F0;
        private const int IRQCount             = 256 + 16;
        private const uint CPUID               = 0x412FC231;
        private const int VectKey              = 0x5FA;
        private const int VectKeyStat          = 0xFA05;
        private const uint SysTickMaximumValue = 0x00FFFFFF;
        private const uint DeepSleep           = 0x4;
    }
}

