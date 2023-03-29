//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using ELFSharp.ELF;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class PicoRV32 : RiscV32
    {
        public PicoRV32(IMachine machine, string cpuType, bool latchedIrqs = true, uint hartId = 0, uint resetVectorAddress = 0x10) : base(machine, cpuType, null, hartId, PrivilegedArchitecture.Priv1_09, Endianess.LittleEndian)
        {
            this.latchedIrqs = latchedIrqs;

            // the frequency value is picked at random and not tested
            internalTimer = new LimitTimer(machine.ClockSource, 1000000, this, nameof(internalTimer), workMode: WorkMode.OneShot, eventEnabled: true);
            internalTimer.LimitReached += () =>
            {
                lock(irqLock)
                {
                    pendingInterrupts |= (1u << TimerInterruptSource);
                    TlibSetReturnRequest();
                }
            };

            qRegisters = new uint[NumberOfQRegisters];

            // this will enable handling interrupts locally
            TlibSetReturnOnException(1);

            this.resetVectorAddress = resetVectorAddress;

            InstallCustomInstruction(pattern: "0000000-----000ss---ddddd0001011", handler: HandleGetqInstruction);
            InstallCustomInstruction(pattern: "0000001-----sssss---000dd0001011", handler: HandleSetqInstruction);
            InstallCustomInstruction(pattern: "0000010-----00000---000000001011", handler: HandleRetirqInstruction);
            InstallCustomInstruction(pattern: "0000011-----sssss---ddddd0001011", handler: HandleMaskirqInstruction);
            InstallCustomInstruction(pattern: "0000100-----00000---ddddd0001011", handler: HandleWaitirqInstruction);
            InstallCustomInstruction(pattern: "0000101-----sssss---ddddd0001011", handler: HandleTimerInstruction);

            Reset();
        }

        public override void OnGPIO(int number, bool value)
        {
            this.Log(LogLevel.Noisy, "External interrupt #{0} set to {1}", number, value);
            lock(irqLock)
            {
                if(value)
                {
                    irqState |= (1u << number);
                    pendingInterrupts |= (1u << number);
                }
                else
                {
                    irqState &= ~(1u << number);
                    if(!latchedIrqs)
                    {
                        pendingInterrupts &= ~(1u << number);
                    }
                }

                if(IrqIsPending(out var _))
                {
                    TlibSetReturnRequest();
                }
            }
        }

        public override void Reset()
        {
            base.Reset();

            interruptsMasked = false;
            pendingInterrupts = 0;
            irqState = 0;
            // the cpu starts with all interrutps disabled
            disabledInterrupts = 0xFFFFFFFF;

            internalTimer.Reset();
        }

        // this CPU does not implement Privileged Architecture spec
        // so those CSRs should not be available at all
        public new RegisterValue SSTATUS => 0;
        public new RegisterValue SIE => 0;
        public new RegisterValue STVEC => 0;
        public new RegisterValue SSCRATCH => 0;
        public new RegisterValue SEPC => 0;
        public new RegisterValue SCAUSE => 0;
        public new RegisterValue STVAL => 0;
        public new RegisterValue SIP => 0;

        public new RegisterValue MSTATUS => 0;
        public new RegisterValue MISA => 0;
        public new RegisterValue MEDELEG => 0;
        public new RegisterValue MIDELEG => 0;
        public new RegisterValue MIE => 0;
        public new RegisterValue MTVEC => 0;
        public new RegisterValue MSCRATCH => 0;
        public new RegisterValue MEPC => 0;
        public new RegisterValue MCAUSE => 0;
        public new RegisterValue MTVAL => 0;
        public new RegisterValue MIP => 0;

        protected override bool ExecutionFinished(ExecutionResult result)
        {
            this.Log(LogLevel.Noisy, "PC@0x{1:X}: Execution finished with result: {0}", result, PC.RawValue);

            lock(irqLock)
            {
                switch((Result)result)
                {
                    case Result.IllegalInstruction:
                    case Result.EBreak:
                    case Result.ECall:
                        pendingInterrupts |= (1u << EBreakECallIllegalInstructionInterruptSource);
                        break;

                    case Result.LoadAddressMisaligned:
                    case Result.StoreAddressMisaligned:
                        pendingInterrupts |= (1u << UnalignedMemoryAccessInterruptSource);
                        break;

                    case (Result)ExecutionResult.Ok:
                        // to avoid warning
                        break;

                    default:
                        this.Log(LogLevel.Warning, "Unexpected execution result: {0}", result);
                        break;
                }

                if(IrqIsPending(out var interruptsToHandle))
                {
                    qRegisters[0] = PC;
                    qRegisters[1] = interruptsToHandle;
                    pendingInterrupts &= ~interruptsToHandle;
                    PC = resetVectorAddress;
                    interruptsMasked = true;

                    this.Log(LogLevel.Noisy, "Entering interrupt, return address: 0x{0:X}, interrupts: 0x{1:X}", qRegisters[0], qRegisters[1]);
                }
            }

            return base.ExecutionFinished(result);
        }

        private bool IrqIsPending(out uint interruptsToHandle)
        {
            var pendingExceptions = (pendingInterrupts & ExceptionsMask);
            if((pendingExceptions != 0)
                    && (interruptsMasked || ((disabledInterrupts & pendingExceptions) != 0)))
            {
                // according to the readme:
                // An illegal instruction or bus error while the illegal instruction or bus error interrupt is disabled will cause the processor to halt.
                // since we currently have no nice way of halting emulation from random thread, error message must do
                this.Log(LogLevel.Error, "Illegal instruction / bus error detected, but respective interrupt is disabled or the cpu is currently handling an IRQ");
                interruptsToHandle = 0;
                return false;
            }

            interruptsToHandle = pendingInterrupts & (~disabledInterrupts);
            if(interruptsMasked)
            {
                interruptsToHandle = 0;
            }

            return interruptsToHandle != 0;
        }

        private void HandleGetqInstruction(UInt64 opcode)
        {
            var rd = (int)BitHelper.GetValue(opcode, 7, 5);
            var qs = (int)BitHelper.GetValue(opcode, 15, 2);

            if(rd != 0)
            {
                X[rd] = qRegisters[qs];
            }

            this.Log(LogLevel.Noisy, "PC@0x{3:X}: Handling getq instruction: setting X[{0}] to the value of q[{1}]: 0x{2:X}", rd, qs, qRegisters[qs], PC.RawValue);
        }

        private void HandleSetqInstruction(UInt64 opcode)
        {
            var qd = (int)BitHelper.GetValue(opcode, 7, 2);
            var rs = (int)BitHelper.GetValue(opcode, 15, 5);

            qRegisters[qd] = X[rs];

            this.Log(LogLevel.Noisy, "PC@0x{3:X}: Handling setq instruction: setting q[{0}] to the value of X[{1}]: 0x{2:X}", qd, rs, X[rs].RawValue, PC.RawValue);
        }

        private void HandleRetirqInstruction(UInt64 opcode)
        {
            lock(irqLock)
            {
                var currentPC = PC.RawValue;

                PC = qRegisters[0];
                interruptsMasked = false;

                pendingInterrupts |= irqState;
                if(IrqIsPending(out var _))
                {
                    TlibSetReturnRequest();
                }

                this.Log(LogLevel.Noisy, "PC@0x{1:X}: Handling retirq instruction: jumping back to 0x{0:X}", qRegisters[0], currentPC);
            }
        }

        private void HandleMaskirqInstruction(UInt64 opcode)
        {
            var rd = (int)BitHelper.GetValue(opcode, 7, 5);
            var rs = (int)BitHelper.GetValue(opcode, 15, 5);

            var newValue = (uint)X[rs].RawValue;
            var previousValue = disabledInterrupts;

            lock(irqLock)
            {
                disabledInterrupts = newValue;
                if(rd != 0)
                {
                    X[rd] = previousValue;
                }

                if(IrqIsPending(out var _))
                {
                    TlibSetReturnRequest();
                }
            }

            this.Log(LogLevel.Noisy, "PC@0x{4:X}: Handling maskirq instruction: setting new mask of value 0x{0:X} read from register X[{1}]; old value 0x{2:X} written to X[{3}]", newValue, rs, previousValue, rd, PC.RawValue);
        }

        private void HandleWaitirqInstruction(UInt64 opcode)
        {
            TlibEnterWfi();
        }

        private void HandleTimerInstruction(UInt64 opcode)
        {
            var rd = (int)BitHelper.GetValue(opcode, 7, 5);
            var rs = (int)BitHelper.GetValue(opcode, 15, 5);

            var previousValue = (uint)internalTimer.Value;
            var newValue = X[rs];
            if(rd != 0)
            {
                X[rd] = previousValue;
            }
            internalTimer.Value = newValue;
            internalTimer.Enabled = (newValue != 0);

            this.Log(LogLevel.Noisy, "Handling timer instruction: setting new value 0x{0:X} read from register X[{1}]; old value 0x{2:X} written to X[{3}]", newValue, rs, previousValue, rd);
        }

        private bool interruptsMasked;
        private uint pendingInterrupts;
        private uint disabledInterrupts;
        private uint irqState;

        private readonly bool latchedIrqs;
        private readonly uint resetVectorAddress;
        private readonly uint[] qRegisters;
        private readonly LimitTimer internalTimer;
        private readonly object irqLock = new object();

// 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649

        [Import]
        private Action TlibEnterWfi;

        [Import]
        private Func<int, int> TlibSetReturnOnException;

#pragma warning restore 649

        private const uint NumberOfQRegisters = 4;

        private const int TimerInterruptSource = 0;
        private const int EBreakECallIllegalInstructionInterruptSource = 1;
        private const int UnalignedMemoryAccessInterruptSource = 2;

        private const uint ExceptionsMask = ((1u << UnalignedMemoryAccessInterruptSource)
                | (1u << EBreakECallIllegalInstructionInterruptSource));

        private enum Result
        {
            IllegalInstruction = 0x2,
            EBreak = 0x3,
            LoadAddressMisaligned = 0x4,
            StoreAddressMisaligned = 0x6,
            ECall = 0x8
        }
    }
}
