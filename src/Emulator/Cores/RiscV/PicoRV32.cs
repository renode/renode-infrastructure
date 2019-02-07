//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using ELFSharp.ELF;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class PicoRV32 : RiscV32
    {
        public PicoRV32(Core.Machine machine, string cpuType, bool latchedIrqs = true, uint hartId = 0, uint resetVectorAddress = 0x10) : base(null, cpuType, machine, hartId, PrivilegeArchitecture.Priv1_09, Endianess.LittleEndian)
        {
            this.latchedIrqs = latchedIrqs;

            // the frequency value is picked at random and not tested
            internalTimer = new LimitTimer(machine.ClockSource, 1000000, workMode: WorkMode.OneShot, eventEnabled: true);
            internalTimer.LimitReached += () =>
            {
                pendingInterrupts |= (1u << TimerInterruptSource);
                TlibSetReturnRequest();
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
            if(value)
            {
                pendingInterrupts |= (1u << number);
            }
            else if(!latchedIrqs)
            {
                pendingInterrupts &= ~(1u << number);
            }

            if(IrqIsPending(out var _))
            {
                TlibSetReturnRequest();
            }
        }

        public override void Reset()
        {
            base.Reset();

            interruptsMasked = false;
            pendingInterrupts = 0;
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

        protected override void ExecutionFinished(TranslationCPU.ExecutionResult result)
        {
            switch((Result)result)
            {
                case Result.IllegalInstruction:
                case Result.EBreak:
                case Result.ECall:
                    pendingInterrupts |= (1u << EBreakECallIllegalInstructionInterruptSource);
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

        private bool IrqIsPending(out uint interruptsToHandle)
        {
            interruptsToHandle = pendingInterrupts & (~disabledInterrupts);
            return !interruptsMasked && interruptsToHandle != 0;
        }

        private void HandleGetqInstruction(UInt64 opcode)
        {
            var rd = (int)BitHelper.GetValue(opcode, 7, 5);
            var qs = (int)BitHelper.GetValue(opcode, 15, 2);

            if(rd != 0)
            {
                X[rd] = qRegisters[qs];
            }
            
            this.Log(LogLevel.Noisy, "Handling getq instruction: setting X[{0}] to the value of q[{1}]: 0x{2:X}", rd, qs, qRegisters[qs]);
        }

        private void HandleSetqInstruction(UInt64 opcode)
        {
            var qd = (int)BitHelper.GetValue(opcode, 7, 2);
            var rs = (int)BitHelper.GetValue(opcode, 15, 5);

            qRegisters[qd] = X[rs];

            this.Log(LogLevel.Noisy, "Handling setq instruction: setting q[{0}] to the value of X[{1}]: 0x{2:X}", qd, rs, X[rs].RawValue);
        }

        private void HandleRetirqInstruction(UInt64 opcode)
        {
            PC = qRegisters[0];
            interruptsMasked = false;

            this.Log(LogLevel.Noisy, "Handling retirq instruction: jumping back to 0x{0:X}", qRegisters[0]);
        }

        private void HandleMaskirqInstruction(UInt64 opcode)
        {
            var rd = (int)BitHelper.GetValue(opcode, 7, 5);
            var rs = (int)BitHelper.GetValue(opcode, 15, 5);

            var newValue = (uint)X[rs].RawValue;
            var previousValue = disabledInterrupts;
            disabledInterrupts = newValue;
            if(rd != 0)
            {
                X[rd] = previousValue;
            }

            if(IrqIsPending(out var _))
            {
                TlibSetReturnRequest();
            }

            this.Log(LogLevel.Noisy, "Handling maskirq instruction: setting new mask of value 0x{0:X} read from register X[{1}]; old value 0x{2:X} written to X[{3}]", newValue, rs, previousValue, rd);
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

        private readonly bool latchedIrqs;
        private readonly uint resetVectorAddress;
        private readonly uint[] qRegisters;
        private readonly LimitTimer internalTimer;

// 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649

        [Import]
        private Action TlibEnterWfi;

        [Import]
        private FuncInt32Int32 TlibSetReturnOnException;

#pragma warning restore 649

        private const uint NumberOfQRegisters = 4;

        private const int TimerInterruptSource = 0;
        private const int EBreakECallIllegalInstructionInterruptSource = 1;

        private enum Result
        {
            IllegalInstruction = 0x2,
            EBreak = 0x3,
            ECall = 0x8
        }
    }
}
