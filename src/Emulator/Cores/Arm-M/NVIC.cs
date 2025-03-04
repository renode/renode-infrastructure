//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class NVIC : IDoubleWordPeripheral, IHasDivisibleFrequency, IKnownSize, IIRQController
    {
        public NVIC(IMachine machine, long systickFrequency = 50 * 0x800000, byte priorityMask = 0xFF, bool haltSystickOnDeepSleep = true)
        {
            priorities = new ExceptionSimpleArray<byte>();
            activeIRQs = new Stack<int>();
            pendingIRQs = new SortedSet<int>();
            this.machine = machine;
            this.priorityMask = priorityMask;
            defaultHaltSystickOnDeepSleep = haltSystickOnDeepSleep;
            binaryPointPosition = new SecurityBanked<int>();
            currentSevOnPending = new SecurityBanked<bool>();
            basepri = new SecurityBanked<byte>();
            ccr = new SecurityBanked<uint>();
            irqs = new ExceptionSimpleArray<IRQState>();
            targetInterruptSecurityState = new InterruptTargetSecurityState[IRQCount];
            IRQ = new GPIO();
            resetMachine = machine.RequestReset;
            systick = new SecurityBanked<SysTick>
            {
                NonSecureVal = new SysTick(machine, this, systickFrequency)
            };
            RegisterCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public void AttachCPU(CortexM cpu)
        {
            if(this.cpu != null)
            {
                throw new RecoverableException("The NVIC has already attached CPU.");
            }
            this.cpu = cpu;
            this.cpuId = cpu.ModelID;
            mpuVersion = cpu.IsV8 ? MPUVersion.PMSAv8 : MPUVersion.PMSAv7;

            if(cpu.TrustZoneEnabled)
            {
                systick.SecureVal = new SysTick(machine, this, systick.NonSecureVal.Frequency, true);
            }

            if(cpu.Model == "cortex-m7")
            {
                DefineTightlyCoupledMemoryControlRegisters();
            }

            cpu.AddHookAtWfiStateChange(HandleWfiStateChange);
        }

        public bool MaskedInterruptPresent { get { return maskedInterruptPresent; } }

        public bool PauseInsteadOfReset { get; set; }

        public IEnumerable<int> GetEnabledExternalInterrupts()
        {
            return irqs.Skip(16).Select((x,i) => new {x,i}).Where(y => (y.x & IRQState.Enabled) != 0).Select(y => y.i).OrderBy(x => x);
        }

        public IEnumerable<int> GetEnabledInternalInterrupts()
        {
            return irqs.Take(16).Select((x,i) => new {x,i}).Where(y => (y.x & IRQState.Enabled) != 0).Select(y => y.i).OrderBy(x => x);
        }

        public long Frequency
        {
            get => systick.Get(IsCurrentCPUInSecureState(out var _)).Frequency;
            set
            {
                systick.Get(IsCurrentCPUInSecureState(out var _)).Frequency = value;
            }
        }

        public int Divider
        {
            get => systick.Get(IsCurrentCPUInSecureState(out var _)).Divider;
            set
            {
                systick.Get(IsCurrentCPUInSecureState(out var _)).Divider = value;
            }
        }

        public bool HaltSystickOnDeepSleep { get; set; }

        [ConnectionRegion("NonSecure")]
        public uint ReadDoubleWordNonSecureAlias(long offset)
        {
            if(!cpu.TrustZoneEnabled)
            {
                throw new RecoverableException(TrustZoneNSRegionWarning);
            }
            return ReadDoubleWord(offset, false);
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadDoubleWord(offset, IsCurrentCPUInSecureState(out var _));
        }

        public uint ReadDoubleWord(long offset, bool isSecure)
        {
            if(offset >= PriorityStart && offset < PriorityEnd)
            {
                return HandlePriorityRead(offset - PriorityStart, true, isSecure);
            }
            if(offset >= SetEnableStart && offset < SetEnableEnd)
            {
                return HandleEnableRead((int)(offset - SetEnableStart), isSecure);
            }
            if(offset >= ClearEnableStart && offset < ClearEnableEnd)
            {
                return HandleEnableRead((int)(offset - ClearEnableStart), isSecure);
            }
            if(offset >= SetPendingStart && offset < SetPendingEnd)
            {
                return GetPending((int)(offset - SetPendingStart), isSecure);
            }
            if(offset >= ClearPendingStart && offset < ClearPendingEnd)
            {
                return GetPending((int)(offset - ClearPendingStart), isSecure);
            }
            if(offset >= ActiveBitStart && offset < ActiveBitEnd)
            {
                return GetActive((int)(offset - ActiveBitStart), isSecure);
            }
            if(offset >= TargetNonSecureStart && offset < TargetNonSecureEnd)
            {
                bool isCpu = machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu);
                // For convenience, if we access this from outside CPU context (e.g. Monitor) let's allow this access through
                if(!isSecure && isCpu)
                {
                    this.WarningLog("CPU {0} tries to read from ITNS register, but it's in Non-secure state", cpu);
                    return 0;
                }
                return GetSecurityTarget((int)(offset - TargetNonSecureStart));
            }
            if(offset >= MPUStart && offset < MPUEnd)
            {
                return HandleMPURead(offset - MPUStart);
            }
            if(offset >= SAUStart && offset < SAUEnd)
            {
                // SAU access is filtered at tlib level
                return HandleSAURead(offset - SAUStart);
            }
            switch((Registers)offset)
            {
            case Registers.VectorTableOffset:
                return (isSecure || !cpu.TrustZoneEnabled) ? cpu.VectorTableOffset : cpu.VectorTableOffsetNonSecure;
            case Registers.CPUID:
                return cpuId;
            case Registers.CoprocessorAccessControl:
                return (isSecure || !cpu.TrustZoneEnabled) ? cpu.CPACR : cpu.CPACR_NS;
            case Registers.FPContextControl:
                if(!IsPrivilegedMode())
                {
                    this.Log(LogLevel.Warning, "Tried to read FPContextControl from an unprivileged context. Returning 0.");
                    return 0;
                }
                return (isSecure || !cpu.TrustZoneEnabled) ? cpu.FPCCR: cpu.FPCCR_NS;
            case Registers.FPContextAddress:
                if(!IsPrivilegedMode())
                {
                    this.Log(LogLevel.Warning, "Tried to read FPContextAddress from an unprivileged context. Returning 0.");
                    return 0;
                }
                return isSecure || !cpu.TrustZoneEnabled ? cpu.FPCAR : cpu.FPCAR_NS;
            case Registers.FPDefaultStatusControl:
                if(!IsPrivilegedMode())
                {
                    this.Log(LogLevel.Warning, "Tried to read FPDefaultStatusControl from an unprivileged context. Returning 0.");
                    return 0;
                }
                return isSecure || !cpu.TrustZoneEnabled ? cpu.FPDSCR : cpu.FPDSCR_NS;
            case Registers.ConfigurationAndControl:
                return ccr.Get(isSecure);
            case Registers.SystemHandlerPriority1:
            case Registers.SystemHandlerPriority2:
            case Registers.SystemHandlerPriority3:
                return HandlePriorityRead(offset - 0xD14, false, isSecure);
            case Registers.ConfigurableFaultStatus:
                return isSecure || !cpu.TrustZoneEnabled ? cpu.FaultStatus : cpu.FaultStatusNonSecure;
            case Registers.InterruptControllerType:
                return 0b0111;
            case Registers.MemoryFaultAddress:
                return isSecure || !cpu.TrustZoneEnabled ? cpu.MemoryFaultAddress : cpu.MemoryFaultAddressNonSecure;
            default:
                lock(RegisterCollection)
                {
                    isNextAccessSecure = isSecure;
                    return RegisterCollection.Read(offset);
                }
            }
        }

        public GPIO IRQ { get; private set; }

        [ConnectionRegion("NonSecure")]
        public void WriteDoubleWordNonSecureAlias(long offset, uint value)
        {
            if(!cpu.TrustZoneEnabled)
            {
                throw new RecoverableException(TrustZoneNSRegionWarning);
            }
            WriteDoubleWord(offset, value, false);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteDoubleWord(offset, value, IsCurrentCPUInSecureState(out var _));
        }

        public void WriteDoubleWord(long offset, uint value, bool isSecure)
        {
            if(offset >= SetEnableStart && offset < SetEnableEnd)
            {
                EnableOrDisableInterrupt((int)offset - SetEnableStart, value, true, isSecure);
                return;
            }
            if(offset >= PriorityStart && offset < PriorityEnd)
            {
                HandlePriorityWrite(offset - PriorityStart, true, value, isSecure);
                return;
            }
            if(offset >= ClearEnableStart && offset < ClearEnableEnd)
            {
                EnableOrDisableInterrupt((int)offset - ClearEnableStart, value, false, isSecure);
                return;
            }
            if(offset >= ClearPendingStart && offset < ClearPendingEnd)
            {
                SetOrClearPendingInterrupt((int)offset - ClearPendingStart, value, false, isSecure);
                return;
            }
            if(offset >= SetPendingStart && offset < SetPendingEnd)
            {
                SetOrClearPendingInterrupt((int)offset - SetPendingStart, value, true, isSecure);
                return;
            }
            if(offset >= TargetNonSecureStart && offset < TargetNonSecureEnd)
            {
                bool isCpu = machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu);
                // For convenience, if we access this from outside CPU context (e.g. Monitor) let's allow this access through
                if(!isSecure && isCpu)
                {
                    this.WarningLog("CPU {0} tries to write to ITNS register, but it's in Non-secure state", cpu);
                    return;
                }
                ModifySecurityTarget((int)(offset - TargetNonSecureStart), value);
                return;
            }
            if(offset >= MPUStart && offset < MPUEnd)
            {
                HandleMPUWrite(offset - MPUStart, value);
                return;
            }
            if(offset >= SAUStart && offset < SAUEnd)
            {
                // SAU access is filtered at tlib level
                HandleSAUWrite(offset - SAUStart, value);
                return;
            }
            switch((Registers)offset)
            {
            case Registers.VectorTableOffset:
                if(isSecure || !cpu.TrustZoneEnabled)
                {
                    cpu.VectorTableOffset = value & 0xFFFFFF80;
                }
                else
                {
                    cpu.VectorTableOffsetNonSecure = value & 0xFFFFFF80;
                }
                break;
            case Registers.ApplicationInterruptAndReset:
                var key = value >> 16;
                if(key != VectKey)
                {
                    this.DebugLog("Wrong key while accessing Application Interrupt and Reset Control register 0x{0:X}.", key);
                    break;
                }
                // Key is OK, allow access to go through
                goto default;
            case Registers.ConfigurableFaultStatus:
                if(isSecure || !cpu.TrustZoneEnabled)
                {
                    cpu.FaultStatus &= ~value;
                }
                else
                {
                    cpu.FaultStatusNonSecure &= ~value;
                }
                break;
            case Registers.SystemHandlerPriority1:
                // 7th interrupt is ignored
                priorities[(int)(isSecure ? SystemException.MemManageFault_S : SystemException.MemManageFault)] = (byte)value;
                priorities[(int)SystemException.BusFault] = (byte)(value >> 8);
                priorities[(int)(isSecure ? SystemException.UsageFault_S : SystemException.UsageFault)] = (byte)(value >> 16);
                this.DebugLog("Priority of IRQs 4, 5, 6 set to 0x{0:X}, 0x{1:X}, 0x{2:X} respectively.", (byte)value, (byte)(value >> 8), (byte)(value >> 16));
                break;
            case Registers.SystemHandlerPriority2:
                // only 11th is not ignored
                priorities[(int)(isSecure ? SystemException.SuperVisorCall_S : SystemException.SuperVisorCall)] = (byte)(value >> 24);
                this.DebugLog("Priority of IRQ 11 set to 0x{0:X}.", (byte)(value >> 24));
                break;
            case Registers.SystemHandlerPriority3:
                priorities[(int)(isSecure ? SystemException.PendSV_S : SystemException.PendSV)] = (byte)(value >> 16);
                priorities[(int)(isSecure ? SystemException.SysTick_S : SystemException.SysTick)] = (byte)(value >> 24);
                this.DebugLog("Priority of IRQs 14, 15 set to 0x{0:X}, 0x{1:X} respectively.", (byte)(value >> 16), (byte)(value >> 24));
                break;
            case Registers.CoprocessorAccessControl:
                // for ARM v8 and CP10 values:
                //      0b11 Full access to the FP Extension and MVE
                //      0b01 Privileged access only to the FP Extension and MVE
                //      0b00 No access to the FP Extension and MVE
                //      0b10 Reserved
                // Any attempted use without access generates a NOCP UsageFault.
                // same for ARM v7, but if values of CP11 and CP10 differ then effects are unpredictable
                if(isSecure || !cpu.TrustZoneEnabled)
                {
                    cpu.CPACR = value;
                }
                else
                {
                    cpu.CPACR_NS = value;
                }
                // Enable FPU if any access is permitted, privilege checks in tlib use CPACR register.
                // Similarly, if TrustZone is enabled, CPACR should be used to check if FPU is enabled in respective Security state
                if((value & 0x100000) == 0x100000)
                {
                    this.DebugLog("Enabling FPU.");
                    cpu.FpuEnabled = true;
                }
                else
                {
                    this.DebugLog("Disabling FPU.");
                    cpu.FpuEnabled = false;
                }
                break;
            case Registers.SoftwareTriggerInterrupt:
                // This register is implemented only in ARMv7m and ARMv8m
                if(cpu.Model == "cortex-m3" || cpu.Model == "cortex-m4" || cpu.Model == "cortex-m4f" || cpu.Model == "cortex-m7")
                {
                    SetPendingIRQ((int)(16 + value));
                }
                else
                {
                    this.Log(LogLevel.Error, "Software Trigger Interrupt Register not implemented for {0}", cpu.Model);
                }
                break;
            case Registers.FPContextControl:
                if(!IsPrivilegedMode())
                {
                    this.Log(LogLevel.Warning, "Writing to FPContextControl requires privileged access.");
                    break;
                }
                if(isSecure || !cpu.TrustZoneEnabled)
                {
                    cpu.FPCCR = value;
                }
                else
                {
                    cpu.FPCCR_NS = value;
                }
                break;
            case Registers.FPContextAddress:
                if(!IsPrivilegedMode())
                {
                    this.Log(LogLevel.Warning, "Writing to FPContextAddress requires privileged access.");
                    break;
                }
                // address must be 8-byte aligned
                if(isSecure || !cpu.TrustZoneEnabled)
                {
                    cpu.FPCAR = value & ~0x7u;
                }
                else
                {
                    cpu.FPCAR_NS = value & ~0x7u;
                }
                break;
            case Registers.FPDefaultStatusControl:
                if(!IsPrivilegedMode())
                {
                    this.Log(LogLevel.Warning, "Writing to FPDefaultStatusControl requires privileged access.");
                    break;
                }
                // set only not reserved values
                if(isSecure || !cpu.TrustZoneEnabled)
                {
                    cpu.FPDSCR = value & 0x07c00000;
                }
                else
                {
                    cpu.FPDSCR_NS = value & 0x07c00000;
                }
                break;
            case Registers.ConfigurationAndControl:
                ccr.Get(isSecure) = value;
                break;
            default:
                lock(RegisterCollection)
                {
                    isNextAccessSecure = isSecure;
                    RegisterCollection.Write(offset, value);
                }
                break;
            }
        }

        public void Reset()
        {
            RegisterCollection.Reset();
            InitInterrupts();
            for(var i = 0; i < priorities.Length; i++)
            {
                priorities[i] = 0x00;
            }
            activeIRQs.Clear();
            systick.NonSecureVal.Reset();
            systick.SecureVal?.Reset();

            IRQ.Unset();
            currentSevOnPending.Reset();
            mpuControlRegister = 0;
            HaltSystickOnDeepSleep = defaultHaltSystickOnDeepSleep;
            canResetOnlyFromSecure = false;
            deepSleepOnlyFromSecure = false;
            binaryPointPosition.Reset();

            // bit [16] DC / Cache enable. This is a global enable bit for data and unified caches.
            ccr.Reset(0x10000);
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
                    pendingIRQs.Remove(result);
                    this.NoisyLog("Acknowledged IRQ {0}.", ExceptionToString(result));
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
                    this.Log(LogLevel.Error, "Trying to complete not active IRQ {0}.", ExceptionToString(number));
                    return;
                }
                irqs[number] &= ~IRQState.Active;
                var activeIRQ = activeIRQs.Pop();
                if(activeIRQ != number)
                {
                    this.Log(LogLevel.Error, "Trying to complete IRQ {0} that was not the last active. Last active was {1}.", ExceptionToString(number), ExceptionToString(activeIRQ));
                    return;
                }
                if((currentIRQ & IRQState.Running) > 0)
                {
                    this.NoisyLog("Completed IRQ {0} active -> pending.", ExceptionToString(number));
                    irqs[number] |= IRQState.Pending;
                    pendingIRQs.Add(number);
                }
                else if((currentIRQ & IRQState.Pending) != 0)
                {
                    this.NoisyLog("Completed IRQ {0} active -> pending.", number);
                }
                else
                {
                    this.NoisyLog("Completed IRQ {0} active -> inactive.", ExceptionToString(number));
                }
                FindPendingInterrupt();
            }
        }

        public void SetPendingIRQ(int number)
        {
            lock(irqs)
            {
                this.NoisyLog("Internal IRQ {0}.", ExceptionToString(number));
                SetPending(number);
                FindPendingInterrupt();
            }
        }

        public void OnGPIO(int number, bool value)
        {
            number += 16; // because this is HW interrupt
            this.NoisyLog("External IRQ {0}: {1}", number, value);
            var pendingInterrupt = SpuriousInterrupt;
            lock(irqs)
            {
                if(value)
                {
                    irqs[number] |= IRQState.Running;
                    SetPending(number);
                }
                else
                {
                    irqs[number] &= ~IRQState.Running;
                }
                pendingInterrupt = FindPendingInterrupt();
            }
            if(pendingInterrupt != SpuriousInterrupt && value)
            {
                // We assume both SysTicks are woken up on exiting deep sleep
                // docs aren't clear on this, but this seems like a logical behavior
                if(!systick.NonSecureVal.Enabled)
                {
                    this.NoisyLog("Waking up from deep sleep");
                }
                systick.NonSecureVal.Enabled |= value;
                if(cpu.TrustZoneEnabled)
                {
                    systick.SecureVal.Enabled |= value;
                }
            }
        }

        public void SetSevOnPendingOnAllCPUs(bool value)
        {
            foreach(var cpu in machine.SystemBus.GetCPUs().OfType<Arm>())
            {
                cpu.SetSevOnPending(value);
            }
        }

        public void SetSleepOnExceptionExitOnAllCPUs(bool value)
        {
            foreach(var cpu in machine.SystemBus.GetCPUs().OfType<CortexM>())
            {
                cpu.SetSleepOnExceptionExit(value);
            }
        }

        public DoubleWordRegisterCollection RegisterCollection { get; }

        public bool DeepSleepEnabled { get; set; }

        private void DefineRegisters()
        {
            Registers.SysTickControl.Define(RegisterCollection)
                .WithFlag(0,
                    changeCallback: (_, value) => systick.Get(isNextAccessSecure).Enabled = value,
                    valueProviderCallback: _ => systick.Get(isNextAccessSecure).Enabled,
                    name: "ENABLE")
                .WithFlag(1,
                    valueProviderCallback: _ => systick.Get(isNextAccessSecure).TickInterruptEnabled,
                    changeCallback: (_, newValue) =>
                    {
                        this.NoisyLog("Systick_{0} interrupt {1}", isNextAccessSecure ? "S" : "NS", newValue  ? "enabled" : "disabled");
                        systick.Get(isNextAccessSecure).TickInterruptEnabled = newValue;
                    }, name: "TICKINT")
                // If no external clock is provided, this bit reads as 1 and ignores writes.
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => true, name: "CLKSOURCE") // SysTick uses the processor clock
                .WithReservedBits(3, 13)
                .WithFlag(16, FieldMode.ReadToClear,
                    valueProviderCallback: _ =>
                    {
                        var ret = systick.Get(isNextAccessSecure).CountFlag;
                        systick.Get(isNextAccessSecure).CountFlag = false;
                        return ret;
                    },
                    name: "COUNTFLAG")
                .WithReservedBits(17, 15);

            Registers.SysTickReloadValue.Define(RegisterCollection)
                .WithValueField(0, 24,
                    valueProviderCallback: _ => systick.Get(isNextAccessSecure).Reload,
                    changeCallback: (_, newValue) => systick.Get(isNextAccessSecure).Reload = newValue,
                    name: "RELOAD")
                .WithReservedBits(24, 8);

            Registers.SysTickValue.Define(RegisterCollection)
                .WithValueField(0, 24,
                    writeCallback: (_, __) => systick.Get(isNextAccessSecure).UpdateSystickValue(),
                    valueProviderCallback: _ =>
                    {
                        cpu?.SyncTime();
                        return (uint)systick.Get(isNextAccessSecure).Value;
                    }, name: "CURRENT")
                .WithReservedBits(24, 8);

            Registers.SysTickCalibrationValue.Define(RegisterCollection)
                // Note that some reference manuals state that this value is for 1ms interval and not for 10ms
                .WithValueField(0, 24, FieldMode.Read, valueProviderCallback: _ => SysTickMaxValue & (uint)(systick.Get(isNextAccessSecure).Frequency / SysTickCalibration100Hz), name: "TENMS")
                .WithReservedBits(24, 6)
                .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => systick.Get(isNextAccessSecure).Frequency % SysTickCalibration100Hz != 0, name: "SKEW")
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => true, name: "NOREF");

            Registers.InterruptControlState.Define(RegisterCollection)
                .WithValueField(0, 9, FieldMode.Read, valueProviderCallback: _ => (uint)(activeIRQs.Count == 0 ? 0 : activeIRQs.Peek()), name: "VECTACTIVE")
                .WithReservedBits(9, 2)
                .WithTaggedFlag("RETTOBASE", 11)
                .WithValueField(12, 9, FieldMode.Read, valueProviderCallback: _ => (uint)FindPendingInterrupt(), name: "VECTPENDING")
                .WithReservedBits(21, 1)
                .WithTaggedFlag("ISRPENDING", 22)
                .WithTaggedFlag("ISRPREEMPT", 23)
                .WithTaggedFlag("STTNS", 24)
                .WithFlag(25, FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        ClearPending((int)(isNextAccessSecure ? SystemException.SysTick_S : SystemException.SysTick));
                    }
                }, name: "PENDSTCLR")
                .WithFlag(26, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        SetPendingIRQ((int)(isNextAccessSecure ? SystemException.SysTick_S : SystemException.SysTick));
                    }
                }, valueProviderCallback: _ => irqs[(int)SystemException.SysTick].HasFlag(IRQState.Pending), name: "PENDSTSET")
                .WithFlag(27, FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        ClearPending((int)(isNextAccessSecure ? SystemException.PendSV_S : SystemException.PendSV));
                    }
                }, name: "PENDSVCLR")
                .WithFlag(28, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        SetPendingIRQ((int)(isNextAccessSecure ? SystemException.PendSV_S : SystemException.PendSV));
                    }
                }, valueProviderCallback: _ => irqs[(int)SystemException.PendSV].HasFlag(IRQState.Pending), name: "PENDSVSET")
                .WithReservedBits(29, 1)
                .WithFlag(30, FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        ClearPending((int)SystemException.NMI);
                    }
                }, name: "PENDNMICLR")
                .WithFlag(31, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        SetPendingIRQ((int)SystemException.NMI);
                    }
                }, valueProviderCallback: _ => irqs[(int)SystemException.NMI].HasFlag(IRQState.Pending), name: "PENDNMISET");

            Registers.SystemControlRegister.Define(RegisterCollection)
                .WithReservedBits(0, 1)
                .WithFlag(1, out sleepOnExitEnabled, name: "SLEEPONEXIT",
                    changeCallback: (_, value) => SetSleepOnExceptionExitOnAllCPUs(value))
                .WithFlag(2,
                    writeCallback: (_, value) =>
                    {
                        if(!isNextAccessSecure && deepSleepOnlyFromSecure)
                        {
                            this.WarningLog("Trying to set SLEEPDEEP but SLEEPDEEPS is set and the access is Non-secure. Ignoring");
                            return;
                        }
                        DeepSleepEnabled = value;
                    },
                    valueProviderCallback: _ =>
                    {
                        if(!isNextAccessSecure && deepSleepOnlyFromSecure)
                        {
                            return false;
                        }
                        return DeepSleepEnabled;
                    },
                    name: "SLEEPDEEP")
                .WithFlag(3,
                    writeCallback: (_, value) =>
                    {
                        if(isNextAccessSecure)
                        {
                            deepSleepOnlyFromSecure = value;
                        }
                    },
                    valueProviderCallback: _ =>
                    {
                        if(!isNextAccessSecure)
                        {
                            return false;
                        }
                        return deepSleepOnlyFromSecure;
                    },
                    name: "SLEEPDEEPS")
                .WithFlag(4,
                    changeCallback: (_, value) =>
                    {
                        SetSevOnPendingOnAllCPUs(value);
                        currentSevOnPending.Get(isNextAccessSecure) = value;
                    },
                    valueProviderCallback: _ => currentSevOnPending.Get(isNextAccessSecure),
                    name: "SEVONPEND")
                .WithReservedBits(5, 27);

            Registers.ApplicationInterruptAndReset.Define(RegisterCollection)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("VECTCLRACTIVE", 1)
                .WithFlag(2, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        if(canResetOnlyFromSecure && !isNextAccessSecure)
                        {
                            this.WarningLog("Requested reset with SYSRESETREQ but SYSRESETREQS is set and the access is Non-secure. Ignoring");
                            return;
                        }
                        this.InfoLog("Resetting platform with SYSRESETREQ");
                        if(PauseInsteadOfReset)
                        {
                            machine.Pause();
                        }
                        else
                        {
                            resetMachine();
                        }
                    }
                }, name: "SYSRESETREQ")
                .WithFlag(3, writeCallback: (_, value) =>
                {
                    if(!isNextAccessSecure)
                    {
                        // This bit is RAZ/WI from Non-secure state.
                        return;
                    }
                    canResetOnlyFromSecure = value;
                }, valueProviderCallback: _ =>
                {
                    if(!isNextAccessSecure)
                    {
                        // This bit is RAZ/WI from Non-secure state.
                        return false;
                    }
                    return canResetOnlyFromSecure;
                }, name: "SYSRESETREQS")
                .WithTaggedFlag("DIT", 4)
                .WithTaggedFlag("IESB", 5)
                .WithReservedBits(6, 2)
                .WithValueField(8, 3, writeCallback: (_, value) =>
                {
                    binaryPointPosition.Get(isNextAccessSecure) = (int)value;
                }, name: "PRIGROUP")
                .WithReservedBits(11, 2)
                .WithFlag(13, writeCallback: (_, value) =>
                {
                    if(!isNextAccessSecure)
                    {
                        // This bit is WI from Non-secure state.
                        return;
                    }
                    var sec = value ? InterruptTargetSecurityState.NonSecure : InterruptTargetSecurityState.Secure;
                    foreach(var excp in new SystemException[] {SystemException.NMI, SystemException.HardFault, SystemException.BusFault})
                    {
                        targetInterruptSecurityState[(int)excp] = sec;
                    }
                }, name: "BFHFNMINS")
                .WithFlag(14, writeCallback: (_, value) =>
                {
                    if(!isNextAccessSecure)
                    {
                        // This bit is RAZ/WI from Non-secure state.
                        return;
                    }
                    prioritizeSecureInterrupts = value;
                }, valueProviderCallback: _ =>
                {
                    if(!isNextAccessSecure)
                    {
                        // This bit is RAZ/WI from Non-secure state.
                        return false;
                    }
                    return prioritizeSecureInterrupts;
                }, name: "PRIS")
                .WithTaggedFlag(name: "ENDIANNESS", 15)
                // We guard access with VectKey in `WriteDoubleWord` - here we just return the expected value
                .WithValueField(16, 16, valueProviderCallback: _ => VectKeyStat, name: "VECTKEYSTAT");


            Registers.SystemHandlerControlAndState.Define(RegisterCollection)
                .WithTaggedFlag("MEMFAULTACT (Memory Manage Active)", 0)
                .WithTaggedFlag("BUSFAULTACT (Bus Fault Active)", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("USGFAULTACT (Usage Fault Active)", 3)
                .WithReservedBits(4, 3)
                .WithTaggedFlag("SVCALLACT (SV Call Active)", 7)
                .WithTaggedFlag("MONITORACT (Monitor Active)", 8)
                .WithReservedBits(9, 1)
                .WithTaggedFlag("PENDSVACT (Pend SV Active)", 10)
                .WithTaggedFlag("SYSTICKACT (Sys Tick Active)", 11)
                .WithTaggedFlag("USGFAULTPENDED (Usage Fault Pending)", 12)
                .WithTaggedFlag("MEMFAULTPENDED (Mem Manage Pending)", 13)
                .WithTaggedFlag("BUSFAULTPENDED (Bus Fault Pending)", 14)
                .WithTaggedFlag("SVCALLPENDED (SV Call Pending)", 15)
                // The enable flags only store written data.
                // Changing them doesn't change a behavior of the model.
                .WithFlag(16, name: "MEMFAULTENA (Memory Manage Fault Enable)")
                .WithFlag(17, name: "BUSFAULTENA (Bus Fault Enable)")
                .WithFlag(18, name: "USGFAULTENA (Usage Fault Enable)")
                .WithReservedBits(19, 13)
                .WithChangeCallback((_, val) =>
                    this.Log(LogLevel.Warning, "Changing value of the SHCSR register to 0x{0:X}, the register isn't supported by Renode", val)
                );

            Registers.CacheSizeSelection.Define(RegisterCollection)
                .WithTaggedFlag("InD (Instruction or Data Selection)", 0)
                .WithTag("Level", 1, 3)
                .WithReservedBits(4, 28);

            Registers.CacheSizeID.Define(RegisterCollection)
                .WithTag("LineSize", 0, 3)
                .WithTag("Associativity", 3, 10)
                .WithTag("NumSets", 13, 15)
                .WithTaggedFlag("WA (Write Allocation Support)", 28)
                .WithTaggedFlag("RA (Read Allocation Support)", 29)
                .WithTaggedFlag("WB (Write Back Support)", 30)
                .WithTaggedFlag("WT (Write Through Support)", 31);

            Registers.DebugExceptionAndMonitorControlRegister.Define(RegisterCollection)
                .WithTaggedFlag("VC_CORERESET (Reset Vector Catch)", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("VC_MMERR (Debug trap on Memory Management faults)", 4)
                .WithTaggedFlag("VC_NOCPERR (Debug trap on Usage Fault access to Coprocessor which is not present)", 5)
                .WithTaggedFlag("VC_CHKERR (Debug trap on Usage Fault enabled checking errors)", 6)
                .WithTaggedFlag("VC_STATERR (Debug trap on Usage Fault state error)", 7)
                .WithTaggedFlag("VC_BUSERR (Debug trap on normal Bus error)", 8)
                .WithTaggedFlag("VC_INTERR (Debug trap on interrupt/exception service errors)", 9)
                .WithTaggedFlag("VC_HARDERR (Debug trap on Hard Fault)", 10)
                .WithReservedBits(11, 5)
                .WithTaggedFlag("MON_EN (Monitor Enable)", 16)
                .WithTaggedFlag("MON_PEND (Monitor Pend)", 17)
                .WithTaggedFlag("MON_STEP (Monitor Step)", 18)
                .WithTaggedFlag("MON_REQ (Monitor Request)", 19)
                .WithReservedBits(20, 4)
                // The trace flag only store written data.
                // Changing it doesn't change the behavior of the model.
                .WithFlag(24, name: "TRCENA (Trace Enable)")
                .WithReservedBits(25, 7);

            Registers.SecureFaultStatus.Define(RegisterCollection)
                .WithValueField(0, 8, writeCallback: (_, value) =>
                {
                    if(!isNextAccessSecure)
                    {
                        // This bit is RAZ/WI from Non-secure state.
                        return;
                    }
                    cpu.SecureFaultStatus = (uint)value;
                }, valueProviderCallback: _ =>
                {
                    if(!isNextAccessSecure)
                    {
                        // This bit is RAZ/WI from Non-secure state.
                        return 0;
                    }
                    return cpu.SecureFaultStatus;
                }, name: "Status bits")
                .WithReservedBits(8, 24);

            /* While the ISA manual permits this to be shared with MMFAR, we keep it separate,
             * so we don't have to worry about invalidating it between exceptions.
             * If there is an address here, it's always valid */
            Registers.SecureFaultAddress.Define(RegisterCollection)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => isNextAccessSecure ? cpu.SecureFaultAddress : 0, name: "Address");
        }

        private void DefineTightlyCoupledMemoryControlRegisters()
        {
            // The ITCMC and DTCMC registers have same fields.
            Registers.InstructionTightlyCoupledMemoryControl.DefineMany(RegisterCollection, 2, setup: (reg, index) => reg
                .WithTaggedFlag("EN (TCM Enable)", 0)
                .WithTaggedFlag("RMW (Read Modify Write Enable)", 1)
                .WithTaggedFlag("RETEN (Retry Phase Enable)", 2)
                .WithTag("SZ (TCM Size)", 3, 4)
                .WithReservedBits(7, 25)
            );
        }

        private void InitInterrupts()
        {
            irqs.Clear();
            Array.Clear(targetInterruptSecurityState, 0, targetInterruptSecurityState.Length);
            for(var i = 0; i < 16; i++)
            {
                irqs[i] = IRQState.Enabled;
            }
            foreach(var i in bankedInterrupts)
            {
                irqs[i] = IRQState.Enabled;
            }
            maskedInterruptPresent = false;
            prioritizeSecureInterrupts = false;
            pendingIRQs.Clear();
        }

        private static int GetStartingInterrupt(long offset, bool externalInterrupt)
        {
            return (int)(offset + (externalInterrupt ? 16 : 0));
        }

        private void HandlePriorityWrite(long offset, bool externalInterrupt, uint value, bool isSecure)
        {
            lock(irqs)
            {
                var startingInterrupt = GetStartingInterrupt(offset, externalInterrupt);
                for(var i = startingInterrupt; i < startingInterrupt + 4; i++)
                {
                    if(!isSecure && !IsInterruptTargetNonSecure(i))
                    {
                        this.WarningLog("Cannot set priority for IRQ {0}, since it targets Secure state, and the access is in Non-secure",
                            ExceptionToString(i));
                        continue;
                    }

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

        private uint HandlePriorityRead(long offset, bool externalInterrupt, bool isSecure)
        {
            lock(irqs)
            {
                var returnValue = 0u;
                var startingInterrupt = GetStartingInterrupt(offset, externalInterrupt);
                for(var i = startingInterrupt + 3; i >= startingInterrupt; i--)
                {
                    returnValue <<= 8;
                    // If we are in Secure state, get Secure variant for banked exception
                    if(isSecure && bankedInterrupts.Contains(i))
                    {
                        returnValue |= priorities[i | BankedExcpSecureBit];
                    }
                    else
                    {
                        // Cannot read the priority of a Secure interrupt in a Non-secure state, read zero instead
                        if(isSecure || IsInterruptTargetNonSecure(i))
                        {
                            returnValue |= priorities[i];
                        }
                    }
                }
                return returnValue;
            }
        }

        private uint HandleMPURead(long offset)
        {
            switch(mpuVersion)
            {
                case MPUVersion.PMSAv7:
                    return HandleMPUReadV7(offset);
                case MPUVersion.PMSAv8:
                    return HandleMPUReadV8(offset);
                default:
                    throw new Exception("Attempted MPU read, but the MPU version is unknown");
            }
        }

        private void HandleMPUWrite(long offset, uint value)
        {
            switch(mpuVersion)
            {
                case MPUVersion.PMSAv7:
                    HandleMPUWriteV7(offset, value);
                    break;
                case MPUVersion.PMSAv8:
                    HandleMPUWriteV8(offset, value);
                    break;
                default:
                    throw new Exception("Attempted MPU write, but the mpuVersion is unknown");
            }
        }

        private void HandleMPUWriteV7(long offset, uint value)
        {
            this.Log(LogLevel.Debug, "MPU: Trying to write to {0} (value: 0x{1:X08})", Enum.GetName(typeof(RegistersV7), offset), value);
            switch((RegistersV7)offset)
            {
                case RegistersV7.Type:
                    this.Log(LogLevel.Warning, "MPU: Trying to write to a read-only register (MPU_TYPE)");
                    break;
                case RegistersV7.Control:
                    if((mpuControlRegister & 0x1) != (value & 0x1))
                    {
                        this.cpu.MPUEnabled = (value & 0x1) != 0x0;
                    }
                    mpuControlRegister = value;
                    break;
                case RegistersV7.RegionNumber: // MPU_RNR
                    cpu.MPURegionNumber = value;
                    break;
                case RegistersV7.RegionBaseAddress:
                case RegistersV7.RegionBaseAddressAlias1:
                case RegistersV7.RegionBaseAddressAlias2:
                case RegistersV7.RegionBaseAddressAlias3:
                    cpu.MPURegionBaseAddress = value;
                    break;
                case RegistersV7.RegionAttributeAndSize:
                case RegistersV7.RegionAttributeAndSizeAlias1:
                case RegistersV7.RegionAttributeAndSizeAlias2:
                case RegistersV7.RegionAttributeAndSizeAlias3:
                    cpu.MPURegionAttributeAndSize = value;
                    break;
            }
        }

        private void HandleMPUWriteV8(long offset, uint value)
        {
            this.Log(LogLevel.Debug, "MPU: Trying to write to {0} (value: 0x{1:X08})", Enum.GetName(typeof(RegistersV8), offset), value);

            if (cpu.NumberOfMPURegions == 0)
            {
                this.Log(LogLevel.Error, $"CPU abort [PC={cpu.PC:x}]. Attempted a write to an MPU register, but the CPU doesn't support MPU. Set 'numberOfMPURegions' in CPU configuration to enable it.");
                throw new CpuAbortException();
            }
            switch((RegistersV8)offset)
            {
                case RegistersV8.Type:
                    this.Log(LogLevel.Warning, "MPU: Trying to write to a read-only register (MPU_TYPE)");
                    break;
                case RegistersV8.Control:
                    cpu.PmsaV8Ctrl = value;
                    break;
                case RegistersV8.RegionNumberRegister:
                    cpu.PmsaV8Rnr = value;
                    break;
                case RegistersV8.RegionBaseAddressRegister:
                case RegistersV8.RegionBaseAddressRegisterAlias1:
                case RegistersV8.RegionBaseAddressRegisterAlias2:
                case RegistersV8.RegionBaseAddressRegisterAlias3:
                    cpu.PmsaV8Rbar = value;
                    break;
                case RegistersV8.RegionLimitAddressRegister:
                case RegistersV8.RegionLimitAddressRegisterAlias1:
                case RegistersV8.RegionLimitAddressRegisterAlias2:
                case RegistersV8.RegionLimitAddressRegisterAlias3:
                    cpu.PmsaV8Rlar = value;
                    break;
                case RegistersV8.MemoryAttributeIndirectionRegister0:
                    cpu.PmsaV8Mair0 = value;
                    break;
                case RegistersV8.MemoryAttributeIndirectionRegister1:
                    cpu.PmsaV8Mair1 = value;
                    break;
            }
        }

        private uint HandleMPUReadV7(long offset)
        {
            uint value;
            switch((RegistersV7)offset)
            {
                case RegistersV7.Type:
                    value = (cpu.NumberOfMPURegions & 0xFF) << 8;
                    break;
                case RegistersV7.Control:
                    value = mpuControlRegister;
                    break;
                case RegistersV7.RegionNumber:
                    value = cpu.MPURegionNumber;
                    break;
                case RegistersV7.RegionBaseAddress:
                case RegistersV7.RegionBaseAddressAlias1:
                case RegistersV7.RegionBaseAddressAlias2:
                case RegistersV7.RegionBaseAddressAlias3:
                    value = cpu.MPURegionBaseAddress;
                    break;
                case RegistersV7.RegionAttributeAndSize:
                case RegistersV7.RegionAttributeAndSizeAlias1:
                case RegistersV7.RegionAttributeAndSizeAlias2:
                case RegistersV7.RegionAttributeAndSizeAlias3:
                    value = cpu.MPURegionAttributeAndSize;
                    break;
                default:
                    value = 0x0;
                    break;
            }
            this.Log(LogLevel.Debug, "MPU: Trying to read {0} (value: 0x{1:X08})", Enum.GetName(typeof(RegistersV7), offset), value);
            return value;
        }

        private uint HandleMPUReadV8(long offset)
        {
            if (cpu.NumberOfMPURegions == 0)
            {
                this.Log(LogLevel.Debug, $"Attempted a read from an MPU register, but the CPU doesn't support MPU. Set 'numberOfMPURegions' in CPU configuration to enable it.");
                return 0;
            }
            uint value;
            switch((RegistersV8)offset)
            {
                case RegistersV8.Type:
                    value = (cpu.NumberOfMPURegions & 0xFF) << 8;
                    break;
                case RegistersV8.Control:
                    value = cpu.PmsaV8Ctrl;
                    break;
                case RegistersV8.RegionNumberRegister:
                    value = cpu.PmsaV8Rnr;
                    break;
                case RegistersV8.RegionBaseAddressRegister:
                case RegistersV8.RegionBaseAddressRegisterAlias1:
                case RegistersV8.RegionBaseAddressRegisterAlias2:
                case RegistersV8.RegionBaseAddressRegisterAlias3:
                    value = cpu.PmsaV8Rbar;
                    break;
                case RegistersV8.RegionLimitAddressRegister:
                case RegistersV8.RegionLimitAddressRegisterAlias1:
                case RegistersV8.RegionLimitAddressRegisterAlias2:
                case RegistersV8.RegionLimitAddressRegisterAlias3:
                    value = cpu.PmsaV8Rlar;
                    break;
                case RegistersV8.MemoryAttributeIndirectionRegister0:
                    value = cpu.PmsaV8Mair0;
                    break;
                case RegistersV8.MemoryAttributeIndirectionRegister1:
                    value = cpu.PmsaV8Mair1;
                    break;
                default:
                    value = 0x0;
                    break;
            }
            this.Log(LogLevel.Debug, "MPU: Trying to read {0} (value: 0x{1:X08})", Enum.GetName(typeof(RegistersV7), offset), value);
            return value;
        }

        private uint HandleSAURead(long offset)
        {
            switch((RegistersSAU)offset)
            {
                case RegistersSAU.Control:
                    return cpu.SAUControl;
                case RegistersSAU.Type:
                    return cpu.NumberOfSAURegions;
                case RegistersSAU.RegionNumber:
                    return cpu.SAURegionNumber;
                case RegistersSAU.RegionBaseAddress:
                    return cpu.SAURegionBaseAddress;
                case RegistersSAU.RegionLimitAddress:
                    return cpu.SAURegionLimitAddress;
                default:
                    this.WarningLog("SAU: Read from unhandled register 0x{0:x}", offset);
                    return 0;
            }
        }

        private void HandleSAUWrite(long offset, uint value)
        {
            switch((RegistersSAU)offset)
            {
                case RegistersSAU.Control:
                    cpu.SAUControl = value;
                    break;
                case RegistersSAU.Type:
                    this.WarningLog("SAU: Write to read-only register 0x{0:x} ({1}), value 0x{2:x}", offset, nameof(RegistersSAU.Type), value);
                    break;
                case RegistersSAU.RegionNumber:
                    cpu.SAURegionNumber = value;
                    break;
                case RegistersSAU.RegionBaseAddress:
                    cpu.SAURegionBaseAddress = value;
                    break;
                case RegistersSAU.RegionLimitAddress:
                    cpu.SAURegionLimitAddress = value;
                    break;
                default:
                    this.WarningLog("SAU: Write to unhandled register 0x{0:x}, value 0x{1:x}", offset, value);
                    break;
            }
        }

        private void HandleWfiStateChange(bool enteredWfi)
        {
            if(enteredWfi && DeepSleepEnabled && HaltSystickOnDeepSleep)
            {
                systick.NonSecureVal.Enabled = false;
                if(cpu.TrustZoneEnabled)
                {
                    systick.SecureVal.Enabled = false;
                }
                this.NoisyLog("Entering deep sleep");
            }
        }

        private void EnableOrDisableInterrupt(int offset, uint value, bool enable, bool isSecure)
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
                            if(!isSecure && !IsInterruptTargetNonSecure(i))
                            {
                                this.WarningLog("Cannot {0} IRQ {1}, since it targets Secure state, and the access is in Non-secure",
                                    enable ? "enable" : "disable", ExceptionToString(i));
                                continue;
                            }

                            if(enable)
                            {
                                this.NoisyLog("Enabled IRQ {0}.", ExceptionToString(i));
                                irqs[i] |= IRQState.Enabled;
                            }
                            else
                            {
                                this.NoisyLog("Disabled IRQ {0}.", ExceptionToString(i));
                                irqs[i] &= ~IRQState.Enabled;
                            }
                        }
                        mask <<= 1;
                    }
                    FindPendingInterrupt();
                }
            }
        }

        private uint HandleEnableRead(int offset, bool isSecure)
        {
            lock(irqs)
            {
                var firstIRQNo = 8 * offset + 16;
                var lastIRQNo = firstIRQNo + 31;
                var result = 0u;
                if(firstIRQNo < 0 || lastIRQNo > irqs.Length)
                {
                    this.Log(LogLevel.Error, "Trying to access IRQs from range {0}-{1}, but only {2} are defined (offset 0x{3:X})", firstIRQNo, lastIRQNo, irqs.Length, offset);
                    return result;
                }
                for(var i = lastIRQNo; i > firstIRQNo; i--)
                {
                    // Cannot read the enable state of a Secure interrupt in a Non-secure state, read zero instead
                    if(isSecure || IsInterruptTargetNonSecure(i))
                    {
                        result |= ((irqs[(int)i] & IRQState.Enabled) != 0) ? 1u : 0u;
                    }
                    result <<= 1;
                }
                if(isSecure || IsInterruptTargetNonSecure(firstIRQNo))
                {
                    result |= ((irqs[(int)firstIRQNo] & IRQState.Enabled) != 0) ? 1u : 0u;
                }
                return result;
            }
        }

        private void SetPending(int i)
        {
            this.DebugLog("Set pending IRQ {0}.", ExceptionToString(i));
            var before = irqs[i];
            irqs[i] |= IRQState.Pending;
            pendingIRQs.Add(i);

            // when SEVONPEND is set all interrupts (even those masked)
            // generate an event when entering the pending state
            if(before != irqs[i] && currentSevOnPending.Get(IsInterruptTargetNonSecure(i)))
            {
                foreach(var cpu in machine.SystemBus.GetCPUs().OfType<CortexM>())
                {
                    cpu.SetEventFlag(true);
                }
            }
        }

        private void ClearPending(int i)
        {
            lock(irqs)
            {
                if((irqs[i] & IRQState.Running) == 0)
                {
                    this.DebugLog("Cleared pending IRQ {0}.", ExceptionToString(i));
                    irqs[i] &= ~IRQState.Pending;
                    pendingIRQs.Remove(i);
                }
                else
                {
                    this.DebugLog("Not clearing pending IRQ {0} as it is currently running.", ExceptionToString(i));
                }
            }
        }

        private void SetOrClearPendingInterrupt(int offset, uint value, bool set, bool isSecure)
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
                            if(!isSecure && !IsInterruptTargetNonSecure(i))
                            {
                                this.WarningLog("Cannot {0} pending IRQ {1}, since it targets Secure state, and the access is in Non-secure",
                                    set ? "set" : "clear", ExceptionToString(i));
                                continue;
                            }
                            if (set)
                            {
                                SetPending(i);
                            }
                            else
                            {
                                ClearPending(i);
                            }
                        }
                        mask <<= 1;
                    }
                    FindPendingInterrupt();
                }
            }
        }

        private void ModifySecurityTarget(int offset, uint value)
        {
            lock(irqs)
            {
                var mask = 1u;
                // Only HW interrupts are modified by this
                for(var i = 0; i < 32; i++)
                {
                    var pos = 16 + offset * 8 + i;
                    this.NoisyLog("IRQ {0} configured as {1}", ExceptionToString(pos), (value & mask) > 0 ? "Non-secure" : "Secure");
                    targetInterruptSecurityState[pos] = (value & mask) > 0 ? InterruptTargetSecurityState.NonSecure : InterruptTargetSecurityState.Secure;
                    mask <<= 1;
                }
            }
        }

        public int FindPendingInterrupt()
        {
            lock(irqs)
            {
                var bestPriority = 0xFF + 1;
                var preemptNeeded = activeIRQs.Count != 0;
                var result = SpuriousInterrupt; // TODO (and some log?)

                foreach(int i in pendingIRQs)
                {
                    var currentIRQ = irqs[i];
                    if(IsCandidate(currentIRQ, i) && AdjustPriority(i) < bestPriority)
                    {
                        result = i;
                        bestPriority = AdjustPriority(i);
                    }
                }
                if(preemptNeeded)
                {
                    var activeTop = activeIRQs.Peek();
                    var activePriority = AdjustPriority(activeTop);
                    if(!DoesAPreemptB(bestPriority, activePriority, !IsInterruptTargetNonSecure(result), !IsInterruptTargetNonSecure(activeTop)))
                    {
                        result = SpuriousInterrupt;
                    }
                    else
                    {
                        this.NoisyLog("IRQ {0} preempts {1}.", ExceptionToString(result), ExceptionToString(activeTop));
                    }
                }

                if(result != SpuriousInterrupt)
                {
                    if(ShouldRaiseException(result))
                    {
                        IRQ.Set(true);
                    }
                    // This field has side-effects, and can cause Cortex-M CPU running in another thread to exit WFI immediately.
                    // Make absolutely sure to execute last, after signaling IRQ handler to run with `IRQ.Set`.
                    // Only this way the CPU will enter an exception handler immediately upon waking from WFI.
                    // This doesn't matter for async (HW) interrupts, arriving when the core is executing normally.
                    maskedInterruptPresent = true;
                }
                else
                {
                    maskedInterruptPresent = false;
                }

                return result;
            }
        }

        /// <remarks>
        /// This exposes raw value of <see cref="targetInterruptSecurityState"/>
        /// note, that for some exceptions, this doesn't mean that they are Secure, since some exceptions are banked
        /// </remarks>
        [HideInMonitor]
        public InterruptTargetSecurityState GetTargetInterruptSecurityState(int interruptNumber)
        {
            // Don't check this for banked IRQs - this makes no sense at all!
            // since they are taken to the state in which they occurred - they can be triggered independently
            DebugHelper.Assert(!bankedInterrupts.Contains(interruptNumber));

            return targetInterruptSecurityState[interruptNumber];
        }


        private bool ShouldRaiseException(int excp)
        {
            /* The intuition to understanding this:
             * PRIMASK is used to mask all exceptions, minus Reset, NMI and HardFault
             * FAULTMASK is a more restrictive PRIMASK, also masking HardFault
             * _NS variants will attempt to only disable NonSecure exceptions, but this can only happen if "PRIS" is set
             * here `BFHFNMINS` will also matter, since it retargets several exceptions (e.g. NMI), and enables HardFault banking
             */
            if(!cpu.TrustZoneEnabled)
            {
                // These have prio below -1, so they are never maskable
                if(excp == (int)SystemException.NMI || excp == (int)SystemException.Reset)
                {
                    return true;
                }

                if(cpu.PRIMASK == 0 && cpu.FAULTMASK == 0)
                {
                    return true;
                }
                else if(cpu.PRIMASK != 0 && cpu.FAULTMASK == 0)
                {
                    // If only PRIMASK is set, HardFault always goes through
                    return excp == (int)SystemException.HardFault;
                }
                // Otherwise, if FAULTMASK is set, deny everything
            }
            else
            {
                // Reset is not maskable
                if(excp == (int)SystemException.Reset)
                {
                    return true;
                }

                if(cpu.GetFaultmask(true) > 0 || cpu.GetFaultmask(false) > 0)
                {
                    // "BFHFNMINS" is 1
                    bool isNSEnabled = GetTargetInterruptSecurityState((int)SystemException.HardFault) == InterruptTargetSecurityState.NonSecure;
                    if(cpu.GetFaultmask(true) > 0)
                    {
                        if(isNSEnabled)
                        {
                            return false;
                        }
                        else
                        {
                            return excp == (int)SystemException.HardFault_S || excp == (int)SystemException.NMI;
                        }
                    }
                    else if(cpu.GetFaultmask(false) > 0)
                    {
                        if(isNSEnabled)
                        {
                            // If Configurable exceptions target Non-secure mode, and PRIS is set, we boost current execution priority to 0x80
                            // which is the boundary between Secure and Non-secure priorities, so only Secure exceptions will pass.
                            // If PRIS is unset, we block everything (raise priotity to 0), but not HardFault (Secure and Non-Secure) and NMI.
                            return (prioritizeSecureInterrupts && !IsInterruptTargetNonSecure(excp))
                                || excp == (int)SystemException.NMI || excp == (int)SystemException.HardFault || excp == (int)SystemException.HardFault_S;
                        }
                        else
                        {
                            // Configurable exceptions target Secure mode only, so we raise execution priority to -1
                            return excp == (int)SystemException.NMI;
                        }
                    }
                }
                // Secure PRIMASK is unset, so all pass, unless Non-secure is set
                else if(cpu.GetPrimask(true) == 0)
                {
                    if(cpu.GetPrimask(false) == 0)
                    {
                        return true;
                    }
                    else
                    {
                        // If PRIMASK_NS is set, and PRIS is set, we boost current execution priority to 0x80
                        // which is the boundary between Secure and Non-secure priorities, so only Secure exceptions will pass.
                        // If PRIS is unset, we block everything (raise priotity to 0), but not HardFault and NMI.
                        return (prioritizeSecureInterrupts && !IsInterruptTargetNonSecure(excp))
                            || excp == (int)SystemException.NMI || excp == (int)SystemException.HardFault;
                    }
                }
                // Secure PRIMASK is set - deny all, except HardFault and NMI (exec priority boosted to 0)
                else if(cpu.GetPrimask(true) > 0)
                {
                    return excp == (int)SystemException.HardFault || excp == (int)SystemException.HardFault_S || excp == (int)SystemException.NMI;
                }
            }

            // Ignore exception otherwise
            return false;
        }

        private int AdjustPriority(int interruptNo)
        {
            byte priority = priorities[interruptNo];
            if(!prioritizeSecureInterrupts)
            {
                return priority;
            }
            /* Rule: RWQWK (ARMv8-M Architecture Reference Manual)
             * When AIRCR.PRIS is 1, each Non-secure SHPRn_NS.PRI_n priority field value [7:0] has the following sequence
             * applied to it, it:
             *  1. Is divided by two.
             *  2. The constant 0x80 is then added to it.
             * though it appears that it applies to all Non-secure interrupts, not only exceptions configurable with SHPRn_NS.PRI_n
             */

            // It is a "byte" after all
            DebugHelper.Assert(priority <= 255);

            if(IsInterruptTargetNonSecure(interruptNo))
            {
                // Divide by two, and set 7th bit. Since 255 is the lowest priority (highest number), this is fine
                priority >>= 1;
                priority |= 0x80;
            }
            return priority;
        }

        private bool IsInterruptTargetNonSecure(int interruptNo)
        {
            if(!cpu.TrustZoneEnabled)
            {
                return true;
            }
            if(bankedInterrupts.Contains(interruptNo))
            {
                return (interruptNo & BankedExcpSecureBit) == 0;
            }
            // HardFault is banked if "BFHFNMINS" is set
            if((interruptNo == (int)SystemException.HardFault_S || interruptNo == (int)SystemException.HardFault)
                && targetInterruptSecurityState[(int)SystemException.HardFault] == InterruptTargetSecurityState.NonSecure)
            {
                return (interruptNo & BankedExcpSecureBit) == 0;
            }
            return targetInterruptSecurityState[interruptNo] == InterruptTargetSecurityState.NonSecure;
        }

        private bool IsCandidate(IRQState state, int index)
        {
            const IRQState mask = IRQState.Pending | IRQState.Enabled | IRQState.Active;
            const IRQState candidate = IRQState.Pending | IRQState.Enabled;

            return ((state & mask) == candidate) &&
                   (basepri.Get(!IsInterruptTargetNonSecure(index)) == 0 || priorities[index] < basepri.Get(!IsInterruptTargetNonSecure(index)));
        }

        private bool DoesAPreemptB(int priorityA, int priorityB, bool secureA, bool secureB)
        {
            var binaryPointMaskA = ~((1 << binaryPointPosition.Get(secureA) + 1) - 1);
            var binaryPointMaskB = ~((1 << binaryPointPosition.Get(secureB) + 1) - 1);
            return (priorityA & binaryPointMaskA) < (priorityB & binaryPointMaskB);
        }

        private uint GetPending(int offset, bool isSecure)
        {
            int startIndex = 16 + offset * 8;
            return BitHelper.GetValueFromBitsArray(irqs.Skip(startIndex).Take(32).Select((irq, idx) => (isSecure || IsInterruptTargetNonSecure(startIndex + idx)) && ((irq & IRQState.Pending) != 0)));
        }

        private uint GetActive(int offset, bool isSecure)
        {
            int startIndex = 16 + offset * 8;
            return BitHelper.GetValueFromBitsArray(irqs.Skip(startIndex).Take(32).Select((irq, idx) => (isSecure || IsInterruptTargetNonSecure(startIndex + idx)) && ((irq & IRQState.Active) != 0)));
        }

        private uint GetSecurityTarget(int offset)
        {
            // We skip first 16 entries, as these are not Hard IRQs. Some of them can be configured by `AIRCR.BFHFNMINS`, but we use common list to store their configuration
            return BitHelper.GetValueFromBitsArray(targetInterruptSecurityState.Skip(16 + offset * 8).Take(32).Select(irq => irq == InterruptTargetSecurityState.NonSecure));
        }

        private bool IsPrivilegedMode()
        {
            // Is in handler mode or is privileged
            return (cpu.XProgramStatusRegister & InterruptProgramStatusRegisterMask) != 0 || (cpu.Control & 1) == 0;
        }

        /* Expect this to return `true` only if the CPU is in Secure state
         * otherwise this will return `false` - also for CPUs with disabled TrustZone
         * or for Monitor access. If the CPU was not the originator (e.g. Monitor), then `currentCpu` will be `null`
         */
        private bool IsCurrentCPUInSecureState(out ICPU currentCpu)
        {
            currentCpu = null;
            try
            {
                currentCpu = machine.GetSystemBus(this).GetCurrentCPU();
                // Checking if TZ is enabled is a short-cut to avoid lengthy lookups into CPU state
                // and stack unwinding if an exception is thrown.
                // It results in a significant speed-up
                if(currentCpu is CortexM mcpu && mcpu.TrustZoneEnabled)
                {
                    return mcpu.SecureState;
                }
                return false;
            }
            catch(RecoverableException)
            {
                // TrustZone might not be enabled
                return false;
            }
        }

        private static string ExceptionToString(int exception)
        {
            if(Enum.IsDefined(typeof(SystemException), exception))
            {
                return ((SystemException)exception).ToString();
            }
            // Otherwise, it's an external Hard IRQ.
            return $"HardwareIRQ#{exception - ((int)SystemException.SysTick) - 1} ({exception})";
        }

        // This is just a cache of BASEPRI register, present in ARMv7M and newer CPUs
        // modifying them here will cause a bad desync with the CPU state
        private readonly SecurityBanked<byte> basepri;

        [HideInMonitor]
        public byte BASEPRI_NS
        {
            get { return basepri.NonSecureVal; }
            set
            {
                if(value == basepri.NonSecureVal)
                {
                    return;
                }
                basepri.NonSecureVal = value;
                FindPendingInterrupt();
            }
        }

        [HideInMonitor]
        public byte BASEPRI_S
        {
            get { return basepri.SecureVal; }
            set
            {
                if(value == basepri.SecureVal)
                {
                    return;
                }
                basepri.SecureVal = value;
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
            InterruptControllerType = 0x4,
            SysTickControl = 0x10,
            SysTickReloadValue = 0x14,
            SysTickValue = 0x18,
            SysTickCalibrationValue = 0x1C,
            SetEnable = 0x100,
            ClearEnable = 0x180,
            SetPending = 0x200,
            ClearPending = 0x280,
            ActiveBit = 0x300,
            InterruptTargetNonSecure = 0x380, // ITNS
            InterruptPriority = 0x400,
            CPUID = 0xD00,
            InterruptControlState = 0xD04,
            VectorTableOffset = 0xD08,
            ApplicationInterruptAndReset = 0xD0C, // AIRCR
            SystemControlRegister = 0xD10, // SCR
            ConfigurationAndControl = 0xD14, // CCR
            SystemHandlerPriority1 = 0xD18, // SHPR1
            SystemHandlerPriority2 = 0xD1C, // SHPR2
            SystemHandlerPriority3 = 0xD20, // SHPR3
            SystemHandlerControlAndState = 0xD24, // SHCSR
            ConfigurableFaultStatus = 0xD28, // CFSR
            HardFaultStatus = 0xD2C, // HFSR
            DebugFaultStatus = 0xD30, // DFSR
            // FPU registers 0xD88 .. F3C
            MemoryFaultAddress = 0xD34, // MMFAR
            BusFaultAddress = 0xD38, // BFAR
            AuxiliaryFaultStatus = 0xD3C, // AFSR
            ProcessorFeature0 = 0xD40, // ID_PFR0
            ProcessorFeature1 = 0xD44, // ID_PFR1
            DebugFeature0 = 0xD48, // ID_DFR0
            AuxiliaryFeature0 = 0xD4C, // ID_AFR0
            MemoryModelFeature0 = 0xD50, // ID_MMFR0
            MemoryModelFeature1 = 0xD54, // ID_MMFR1
            MemoryModelFeature2 = 0xD58, // ID_MMFR2
            MemoryModelFeature3 = 0xD5C, // ID_MMFR3
            InstructionSetAttribute0 = 0xD60, // ID_ISAR0
            InstructionSetAttribute1 = 0xD64, // ID_ISAR1
            InstructionSetAttribute2 = 0xD68, // ID_ISAR2
            InstructionSetAttribute3 = 0xD6C, // ID_ISAR3
            InstructionSetAttribute4 = 0xD70, // ID_ISAR4
            ID_ISAR5 = 0xD74, // ID_ISAR5
            CacheLevelID = 0xD78, // CLIDR
            CacheType = 0xD7C, // CTR
            CacheSizeID = 0xD80, // CCSIDR
            CacheSizeSelection = 0xD84, // CSSELR
            CoprocessorAccessControl = 0xD88, // CPACR
            MPUType = 0xD90, // MPU_TYPE
            MPUControl = 0xD94, // MPU_CTRL
            MPURegionNumber = 0xD98, // MPU_RNR
            MPURegionBaseAddress = 0xD9C, // MPU_RBAR
            MPURegionAttributeAndSize = 0xDA0, // MPU_RASR
            Alias1OfMPURegionBaseAddress = 0xDA4, // MPU_RBAR_A1
            Alias1OfMPURegionAttributeAndSize = 0xDA8, // MPU_RASR_A1
            Alias2OfMPURegionBaseAddress = 0xDAC, // MPU_RBAR_A2
            Alias2OfMPURegionAttributeAndSize = 0xDB0, // MPU_RASR_A2
            Alias3OfMPURegionBaseAddress = 0xDB4, // MPU_RBAR_A3
            Alias3OfMPURegionAttributeAndSize = 0xDB8, // MPU_RASR_A3
            SAUControl = 0xDD0, // SAU_CTRL
            SAUType = 0xDD4, // SAU_TYPE
            SAURegionNumber = 0xDD8, // SAU_RNR
            SAURegionBaseAddress = 0xDDC, // SAU_RBAR
            SAURegionLimitAddress = 0xDE0, // SAU_RLAR
            SecureFaultStatus = 0xDE4, // SAU_SFSR
            SecureFaultAddress = 0xDE8, // SAU_SFAR
            DebugExceptionAndMonitorControlRegister = 0xDFC, // DEMCR
            SoftwareTriggerInterrupt = 0xF00, // STIR
            FPContextControl = 0xF34, // FPCCR
            FPContextAddress = 0xF38, // FPCAR
            FPDefaultStatusControl = 0xF3C, // FPDSCR
            FloatingPointDefaultStatusControl = 0xF3C, // FPDSCR
            MediaAndFPFeature0 = 0xF40, // MVFR0
            MediaAndFPFeature1 = 0xF44, // MVFR1
            MediaAndFPFeature2 = 0xF48, // MVFR2
            ICacheInvalidateAllToPoUaIgnored = 0xF50, // ICIALLU
            ICacheInvalidateByMVAToPoUaAddress = 0xF58, // ICIMVAU
            DCacheInvalidateByMVAToPoCAddress = 0xF5C, // DCIMVAC
            DCacheInvalidateBySetWay= 0xF60, // DCISW
            DCacheCleanByMVAToPoUAddress = 0xF64, // DCCMVAU
            DCacheCleanByMVAToPoCAddress = 0xF68, // DCCMVAC
            DCacheCleanBySetWay= 0xF6C, // DCCSW
            DCacheCleanAndInvalidateByMVAToPoCAddress = 0xF70, // DCCIMVAC
            DCacheCleanAndInvalidateBySetWay= 0xF74, // DCCISW
            BranchPredictorInvalidateAllIgnored = 0xF78, // BPIALL

            // Registers with addresses from 0xF90 to 0xFCF are implementation defined.
            // The following ones are valid for Cortex-M7.
            InstructionTightlyCoupledMemoryControl = 0xF90, // ITCMCR
            DataTightlyCoupledMemoryControl = 0xF94, // DTCMCR
            AHBPControl = 0xF98, // AHBPCR
            L1CacheControl = 0xF9C, // CACR
            AHBSlaveControl = 0xFA0, // AHBSCR
            AuxiliaryBusFaultStatus = 0xFA8, // ABFSR
        }

        private enum RegistersV7
        {
            Type = 0x00,
            Control = 0x04,
            RegionNumber = 0x08,
            RegionBaseAddress = 0x0C,
            RegionAttributeAndSize = 0x10,
            RegionBaseAddressAlias1 = 0x14,
            RegionAttributeAndSizeAlias1 = 0x18,
            RegionBaseAddressAlias2 = 0x1C,
            RegionAttributeAndSizeAlias2 = 0x20,
            RegionBaseAddressAlias3 = 0x24,
            RegionAttributeAndSizeAlias3 = 0x28,
        }

        private enum RegistersV8
        {
            Type = 0x00,
            Control = 0x04,
            RegionNumberRegister = 0x08,
            RegionBaseAddressRegister = 0x0C,
            RegionLimitAddressRegister = 0x10,
            RegionBaseAddressRegisterAlias1 = 0x14,
            RegionLimitAddressRegisterAlias1 = 0x18,
            RegionBaseAddressRegisterAlias2 = 0x1C,
            RegionLimitAddressRegisterAlias2 = 0x20,
            RegionBaseAddressRegisterAlias3 = 0x24,
            RegionLimitAddressRegisterAlias3 = 0x28,
            MemoryAttributeIndirectionRegister0 = 0x30,
            MemoryAttributeIndirectionRegister1 = 0x34
        }

        private enum RegistersSAU
        {
            Control = 0x0, // SAU_CTRL
            Type = 0x4, // SAU_TYPE
            RegionNumber = 0x8, // SAU_RNR
            RegionBaseAddress = 0xc, // SAU_RBAR
            RegionLimitAddress = 0x10, // SAU_RLAR
        }

        private enum MPUVersion
        {
            PMSAv7,
            PMSAv8
        }

        private enum SystemException
        {
            Reset = 1,
            NMI = 2,
            HardFault = 3,
            MemManageFault = 4,
            BusFault = 5,
            UsageFault = 6,
            SecureFault = 7,
            SuperVisorCall = 11,
            DebugMonitor = 12,
            PendSV = 14,
            SysTick = 15,

            // These are not real exceptions!
            // We cheat here a bit, to create banked exceptions for TrustZone
            // since they can live indepenently from their not-banked variants (so effectively they behave like separate exceptions)
            HardFault_S = HardFault | BankedExcpSecureBit,
            MemManageFault_S = MemManageFault | BankedExcpSecureBit,
            UsageFault_S = UsageFault | BankedExcpSecureBit,
            SuperVisorCall_S = SuperVisorCall | BankedExcpSecureBit,
            DebugMonitor_S = DebugMonitor | BankedExcpSecureBit,
            PendSV_S = PendSV | BankedExcpSecureBit,
            SysTick_S = SysTick | BankedExcpSecureBit,
        }

        public enum InterruptTargetSecurityState
        {
            Secure = 0,
            NonSecure = 1,
        }

        private class SecurityBanked<T>
        {
            public T SecureVal;
            public T NonSecureVal;

            public void Reset()
            {
                SecureVal = default(T);
                NonSecureVal = default(T);
            }

            public void Reset(T val)
            {
                SecureVal = val;
                NonSecureVal = val;
            }

            public ref T Get(bool IsSecure)
            {
                return ref IsSecure ? ref SecureVal : ref NonSecureVal; 
            }
        }

        private class SysTick
        {
            public SysTick(IMachine machine, NVIC parent, long systickFrequency, bool isSecure = false)
            {
                IsSecure = isSecure;
                this.parent = parent;
                // We set initial limit to the maximum 24-bit value, and don't modify it afterwards.
                // Instead we reload counter with RELOAD value when counter reaches 0.
                systick = new LimitTimer(machine.ClockSource, systickFrequency, parent, nameof(systick) + (isSecure ? "_S" : "_NS"), SysTickMaxValue, Direction.Descending, false, eventEnabled: true);
                systick.LimitReached += () =>
                {
                    CountFlag = true;
                    if(TickInterruptEnabled)
                    {
                        parent.SetPendingIRQ((int)(IsSecure ? SystemException.SysTick_S : SystemException.SysTick));
                    }

                    // If the systick timer is running and the reload value is 0, this has the effect of disabling the counter on the expiration.
                    if(Reload == 0)
                    {
                        systick.Enabled = false;
                    }
                    else
                    {
                        systick.Value = Reload;
                    }
                };
            }

            public void Reset()
            {
                systickEnabled = false;
                reloadValue = 0;

                systick.Reset();
                systick.AutoUpdate = true;
            }

            public void UpdateSystickValue()
            {
                if(reloadValue != 0)
                {
                    // Write to this register does not trigger the SysTick exception logic - we can't write zero to timer value as it would trigger an event.
                    systick.Value = reloadValue;
                }
                CountFlag = false;
            }

            public bool IsSecure { get; }

            public bool TickInterruptEnabled { get; set; } // TICKINT
            public bool CountFlag { get; set; } // COUNTFLAG

            // Systick can be enabled but doesn't have to count, e.g. if RELOAD value is set to 0,
            // or we are in DEEPSLEEP
            public bool Enabled
            {
                get => systickEnabled;
                set
                {
                    systickEnabled = value;
                    if(value && Reload == 0)
                    {
                        parent.DebugLog("Systick_{0} enabled but it won't be started as long as the reload value is zero", IsSecure ? "S" : "NS");
                        return;
                    }
                    parent.NoisyLog("Systick_{0} {1}", IsSecure ? "S" : "NS", value ? "enabled" : "disabled");
                    systick.Enabled = value;
                }
            }

            public ulong Value // CURRENT
            {
                get => systick.Value;
            }

            public ulong Reload // RELOAD
            {
                get => reloadValue;
                set
                {
                    if(Enabled && reloadValue == 0 && !systick.Enabled)
                    {
                        // We explicitly enable underlying SysTick counter only in the case it was blocked by RELOAD=0.
                        // We ignore other cases, as we don't want to accidentally enable counter in DEEPSLEEP just by writing to this register.
                        parent.DebugLog("Resuming Systick_{0} counter due to reload value change from 0x0 to 0x{1:X}", IsSecure ? "_S" : "_NS", value);
                        systick.Value = value;
                        systick.Enabled = true;
                    }
                    reloadValue = value;
                }
            }

            public long Frequency
            {
                get => systick.Frequency;
                set
                {
                    systick.Frequency = value;
                }
            }

            public int Divider
            {
                get => systick.Divider;
                set
                {
                    systick.Divider = value;
                }
            }

            private ulong reloadValue;
            private bool systickEnabled; // ENABLE
            private readonly LimitTimer systick;
            private readonly NVIC parent;
        }

        /// This is the simplest hash-map-like class, to store exceptions.
        /// We use <see cref="BankedExcpSecureBit"/> to mark banked exception as Secure.
        /// Such exception behaves like separate exception (e.g. Secure and Non-secure banked exception can exist at once).
        /// This way, we can use the end of the array to place the extra Secure banked exceptions
        /// it's still easier than using a dictionary.
        private class ExceptionSimpleArray<T> : IEnumerable<T>
        {
            public ExceptionSimpleArray()
            {
                // Regular IRQs, plus banked, plus HardFault (which can be banked, but doesn't have to be)
                container = new T[IRQCount + bankedInterrupts.Length / 2 + 1];
            }

            public void Clear()
            {
                Array.Clear(container, 0, container.Length);
            }

            public T this[int index]
            {
                get
                {
                    return container[MapSystemExceptionToInteger(index)];
                }
                set
                {
                    container[MapSystemExceptionToInteger(index)] = value;
                }
            }

            public int Length => container.Length;

            private static int MapSystemExceptionToInteger(int exception)
            {
                if(exception < BankedExcpSecureBit)
                {
                    return exception;
                }
                switch(exception)
                {
                    case (int)SystemException.MemManageFault_S:
                        return IRQCount;
                    case (int)SystemException.UsageFault_S:
                        return IRQCount + 1;
                    case (int)SystemException.SuperVisorCall_S:
                        return IRQCount + 2;
                    case (int)SystemException.PendSV_S:
                        return IRQCount + 3;
                    case (int)SystemException.SysTick_S:
                        return IRQCount + 4;
                    case (int)SystemException.HardFault_S:
                        return IRQCount + 5;
                    case (int)SystemException.DebugMonitor_S:
                        return IRQCount + 6;
                    default:
                        throw new InvalidOperationException($"Exception number {exception} is invalid");
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                return container.AsEnumerable().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private readonly T[] container;
        }

        // bit [16] DC / Cache enable. This is a global enable bit for data and unified caches.
        private readonly SecurityBanked<uint> ccr;

        private readonly bool defaultHaltSystickOnDeepSleep;
        private readonly SecurityBanked<SysTick> systick;
        private readonly byte priorityMask;
        private readonly SecurityBanked<bool> currentSevOnPending;
        private readonly Stack<int> activeIRQs;
        private readonly ISet<int> pendingIRQs;
        private readonly SecurityBanked<int> binaryPointPosition; // from the right
        private bool isNextAccessSecure;
        private bool canResetOnlyFromSecure;
        private bool deepSleepOnlyFromSecure;
        private bool prioritizeSecureInterrupts;
        private uint mpuControlRegister;
        private MPUVersion mpuVersion;

        private bool maskedInterruptPresent;

        // This is configurable through NVIC_ITNSx only for Hardware Interrupts (exception numbered 16 and above)
        // for configuring selected exceptions, look at AIRCR.BFHFNMINS
        // for other exceptions, this will be completely ignored
        private readonly InterruptTargetSecurityState[] targetInterruptSecurityState;

        private readonly ExceptionSimpleArray<IRQState> irqs;
        private readonly ExceptionSimpleArray<byte> priorities;
        private readonly Action resetMachine;
        private CortexM cpu;
        private readonly IMachine machine;
        private uint cpuId;

        private IFlagRegisterField sleepOnExitEnabled;

        private static readonly int[] bankedInterrupts = new int []
        {
            (int)SystemException.MemManageFault,
            (int)SystemException.UsageFault,
            (int)SystemException.SuperVisorCall,
            (int)SystemException.PendSV,
            (int)SystemException.SysTick,
            (int)SystemException.DebugMonitor,

            (int)SystemException.MemManageFault_S,
            (int)SystemException.UsageFault_S,
            (int)SystemException.SuperVisorCall_S,
            (int)SystemException.PendSV_S,
            (int)SystemException.SysTick_S,
            (int)SystemException.DebugMonitor_S,
            // Lack of HardFault here is not a mistake
            // HardFault is by default handled as Secure exception
        };

        private const string TrustZoneNSRegionWarning = "Without TrustZone enabled in the CPU, a NonSecure region should not be registered";
        private const int MPUStart             = 0xD90;
        private const int MPUEnd               = 0xDC4;    // resized for compat. with V8 MPU
        private const int SAUStart             = 0xDD0;
        private const int SAUEnd               = 0xDE4;
        private const int SetEnableStart       = 0x100;
        private const int SetEnableEnd         = 0x140;
        private const int ClearEnableStart     = 0x180;
        private const int ClearEnableEnd       = 0x1C0;
        private const int SetPendingStart      = 0x200;
        private const int SetPendingEnd        = 0x240;
        private const int ClearPendingStart    = 0x280;
        private const int ClearPendingEnd      = 0x2C0;
        private const int ActiveBitStart       = 0x300;
        private const int ActiveBitEnd         = 0x320;
        private const int TargetNonSecureStart = 0x380;
        private const int TargetNonSecureEnd   = 0x3C0;
        private const int PriorityStart        = 0x400;
        private const int PriorityEnd          = 0x7F0;
        private const int IRQCount             = 512 + 16 + 1;
        private const int SpuriousInterrupt    = IRQCount - 1;
        private const int VectKey              = 0x5FA;
        private const int VectKeyStat          = 0xFA05;
        private const uint SysTickMaximumValue = 0x00FFFFFF;

        private const int BankedExcpSecureBit  = 1 << 30;
        private const uint InterruptProgramStatusRegisterMask = 0x1FF;
        private const int SysTickCalibration100Hz = 100;
        private const int SysTickMaxValue = (1 << 24) - 1;
    }
}
