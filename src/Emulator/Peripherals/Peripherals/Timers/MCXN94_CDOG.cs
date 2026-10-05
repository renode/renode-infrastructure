//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class MCXN94_CDOG : BasicDoubleWordPeripheral, IKnownSize
    {
        public MCXN94_CDOG(IMachine machine, ulong frequency = DefaultFrequency) : base(machine)
        {
            IRQ = new GPIO();

            instructionTimer = new LimitTimer(machine.ClockSource, frequency, this, "CDOGInstructionTimer", uint.MaxValue, workMode: WorkMode.OneShot, enabled: false, eventEnabled: true, autoUpdate: true);
            instructionTimer.LimitReached += () => RaiseFault(FaultFlags.Timeout, "CDOG instruction timer expired");

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();

            instructionTimer.Reset();

            lockControl = LockControl.Unlocked;
            timeoutAction = FaultAction.Interrupt;
            miscompareAction = FaultAction.Interrupt;
            sequenceAction = FaultAction.Interrupt;
            stateAction = FaultAction.Disabled;
            addressAction = FaultAction.Disabled;
            irqPauseControl = PauseControl.KeepRunning;
            debugHaltControl = PauseControl.KeepRunning;

            reloadValue = DefaultReloadValue;
            secureCounter = 0;
            running = false;
            timeoutFaultCount = 0;
            miscompareFaultCount = 0;
            sequenceFaultCount = 0;

            // Fault flags and persistent data survive a software reset on MCXN94.
            // Renode does not expose a separate power-on reset path for this peripheral.
            IRQ.Unset();
        }

        public void InjectMiscompareFault()
        {
            RaiseFault(FaultFlags.Miscompare, "CDOG miscompare fault injected");
        }

        public void InjectSequenceFault()
        {
            RaiseFault(FaultFlags.Sequence, "CDOG sequence fault injected");
        }

        public void InjectIllegalSequenceFault()
        {
            InjectSequenceFault();
        }

        public void InjectTimeoutFault()
        {
            RaiseFault(FaultFlags.Timeout, "CDOG timeout fault injected");
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithValueField(0, 32, name: "CONTROL",
                    valueProviderCallback: _ => ComposeControl(),
                    writeCallback: (_, value) => ApplyControl((uint)value));

            Registers.Reload.Define(this, DefaultReloadValue)
                .WithValueField(0, 32, name: "RELOAD",
                    valueProviderCallback: _ => reloadValue,
                    writeCallback: (_, value) =>
                    {
                        reloadValue = (uint)value;
                        if(running)
                        {
                            ReloadInstructionTimer();
                        }
                    });

            Registers.InstructionTimer.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "INSTRUCTION_TIMER",
                    valueProviderCallback: _ => running ? (uint)instructionTimer.Value : reloadValue);

            Registers.Status.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "STATUS",
                    valueProviderCallback: _ => ComposeStatus());

            Registers.Status2.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "STATUS2",
                    valueProviderCallback: _ => secureCounter);

            Registers.Flags.Define(this)
                .WithValueField(0, 32, name: "FLAGS",
                    valueProviderCallback: _ => (uint)flags,
                    writeCallback: (_, value) =>
                    {
                        flags &= ~(FaultFlags)(uint)value;
                        UpdateInterrupts();
                    });

            Registers.Persistent.Define(this)
                .WithValueField(0, 32, name: "PERSISTENT",
                    valueProviderCallback: _ => persistentValue,
                    writeCallback: (_, value) => persistentValue = (uint)value);

            Registers.Start.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "START",
                    writeCallback: (_, value) => Start((uint)value));

            Registers.Stop.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "STOP",
                    writeCallback: (_, value) => Stop((uint)value));

            Registers.Restart.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "RESTART",
                    writeCallback: (_, value) => Restart((uint)value));

            Registers.Add.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "ADD",
                    writeCallback: (_, value) => Add((uint)value));

            Registers.Add1.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "ADD1",
                    writeCallback: (_, __) => Add(1));

            Registers.Add16.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "ADD16",
                    writeCallback: (_, __) => Add(16));

            Registers.Add256.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "ADD256",
                    writeCallback: (_, __) => Add(256));

            Registers.Sub.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "SUB",
                    writeCallback: (_, value) => Sub((uint)value));

            Registers.Sub1.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "SUB1",
                    writeCallback: (_, __) => Sub(1));

            Registers.Sub16.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "SUB16",
                    writeCallback: (_, __) => Sub(16));

            Registers.Sub256.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "SUB256",
                    writeCallback: (_, __) => Sub(256));

            Registers.Assert16.Define(this)
                .WithValueField(0, 16, FieldMode.Write, name: "ASSERT16",
                    writeCallback: (_, value) => Assert16((ushort)value))
                .WithReservedBits(16, 16);
        }

        private void Start(uint value)
        {
            if(running)
            {
                RaiseFault(FaultFlags.Sequence, "CDOG START issued while already running");
                return;
            }

            secureCounter = value;
            running = true;
            ReloadInstructionTimer();
            UpdateInterrupts();
        }

        private void Stop(uint expectedValue)
        {
            if(!EnsureRunning(Command.Stop))
            {
                return;
            }

            if(secureCounter != expectedValue)
            {
                RaiseFault(FaultFlags.Miscompare, $"CDOG STOP expected 0x{expectedValue:X8}, got 0x{secureCounter:X8}");
                return;
            }

            running = false;
            instructionTimer.Enabled = false;
            UpdateInterrupts();
        }

        private void Restart(uint value)
        {
            if(!EnsureRunning(Command.Restart))
            {
                return;
            }

            if(value != 0)
            {
                secureCounter = value;
            }

            ReloadInstructionTimer();
        }

        private void Add(uint value)
        {
            if(!EnsureRunning(Command.Add))
            {
                return;
            }

            secureCounter += value;
            ReloadInstructionTimer();
        }

        private void Sub(uint value)
        {
            if(!EnsureRunning(Command.Sub))
            {
                return;
            }

            secureCounter -= value;
            ReloadInstructionTimer();
        }

        private void Assert16(ushort expectedValue)
        {
            if(!EnsureRunning(Command.Assert16))
            {
                return;
            }

            if((secureCounter & 0xFFFFu) != expectedValue)
            {
                RaiseFault(FaultFlags.Miscompare, $"CDOG ASSERT16 expected 0x{expectedValue:X4}, got 0x{secureCounter & 0xFFFFu:X4}");
                return;
            }

            ReloadInstructionTimer();
        }

        private bool EnsureRunning(Command command)
        {
            if(!running)
            {
                RaiseFault(FaultFlags.Sequence, $"CDOG {command} issued while idle");
                return false;
            }

            return true;
        }

        private void ReloadInstructionTimer()
        {
            var value = Math.Max(1u, reloadValue);
            instructionTimer.Limit = value;
            instructionTimer.Value = value;
            instructionTimer.Enabled = running;
            instructionTimer.EventEnabled = running;
        }

        private uint ComposeControl()
        {
            var value = 0u;
            value |= (uint)lockControl;
            value |= ((uint)timeoutAction & 0x7u) << 2;
            value |= ((uint)miscompareAction & 0x7u) << 5;
            value |= ((uint)sequenceAction & 0x7u) << 8;
            value |= ((uint)stateAction & 0x7u) << 14;
            value |= ((uint)addressAction & 0x7u) << 17;
            value |= ((uint)irqPauseControl & 0x3u) << 28;
            value |= ((uint)debugHaltControl & 0x3u) << 30;
            return value;
        }

        private void ApplyControl(uint value)
        {
            if(lockControl == LockControl.Locked)
            {
                this.Log(LogLevel.Warning, "Ignoring write to locked CDOG CONTROL register");
                return;
            }

            lockControl = (LockControl)(value & 0x3u);
            timeoutAction = (FaultAction)((value >> 2) & 0x7u);
            miscompareAction = (FaultAction)((value >> 5) & 0x7u);
            sequenceAction = (FaultAction)((value >> 8) & 0x7u);
            stateAction = (FaultAction)((value >> 14) & 0x7u);
            addressAction = (FaultAction)((value >> 17) & 0x7u);
            irqPauseControl = (PauseControl)((value >> 28) & 0x3u);
            debugHaltControl = (PauseControl)((value >> 30) & 0x3u);
            UpdateInterrupts();
        }

        private uint ComposeStatus()
        {
            var value = 0u;
            value |= timeoutFaultCount;
            value |= (uint)miscompareFaultCount << 8;
            value |= (uint)sequenceFaultCount << 16;
            value |= (running ? 1u : 0u) << 28;
            return value;
        }

        private void RaiseFault(FaultFlags flag, string reason)
        {
            flags |= flag;
            if((flag & FaultFlags.Timeout) != 0)
            {
                timeoutFaultCount++;
            }
            if((flag & FaultFlags.Miscompare) != 0)
            {
                miscompareFaultCount++;
            }
            if((flag & FaultFlags.Sequence) != 0)
            {
                sequenceFaultCount++;
            }
            running = false;
            instructionTimer.Enabled = false;
            this.Log(LogLevel.Warning, reason);

            switch(GetFaultAction(flag))
            {
            case FaultAction.Reset:
                UpdateInterrupts();
                machine.RequestReset();
                break;
            case FaultAction.Interrupt:
            case FaultAction.Disabled:
                UpdateInterrupts();
                break;
            default:
                this.Log(LogLevel.Warning, "Unsupported CDOG fault action, treating as disabled");
                UpdateInterrupts();
                break;
            }
        }

        private void UpdateInterrupts()
        {
            var interrupt = ((flags & FaultFlags.Timeout) != 0 && timeoutAction == FaultAction.Interrupt)
                || ((flags & FaultFlags.Miscompare) != 0 && miscompareAction == FaultAction.Interrupt)
                || ((flags & FaultFlags.Sequence) != 0 && sequenceAction == FaultAction.Interrupt);
            IRQ.Set(interrupt);
        }

        private FaultAction GetFaultAction(FaultFlags flag)
        {
            if((flag & FaultFlags.Timeout) != 0)
            {
                return timeoutAction;
            }
            if((flag & FaultFlags.Miscompare) != 0)
            {
                return miscompareAction;
            }
            if((flag & FaultFlags.Sequence) != 0)
            {
                return sequenceAction;
            }
            return FaultAction.Disabled;
        }

        private bool running;

        private uint reloadValue;
        private uint secureCounter;
        private uint persistentValue;

        private LockControl lockControl;
        private FaultAction timeoutAction;
        private FaultAction miscompareAction;
        private FaultAction sequenceAction;
        private FaultAction stateAction;
        private FaultAction addressAction;
        private PauseControl irqPauseControl;
        private PauseControl debugHaltControl;
        private FaultFlags flags;
        private byte timeoutFaultCount;
        private byte miscompareFaultCount;
        private byte sequenceFaultCount;

        private readonly LimitTimer instructionTimer;

        private const uint DefaultReloadValue = 0xFFFF;
        private const ulong DefaultFrequency = 1;

        [Flags]
        private enum FaultFlags : uint
        {
            None = 0,
            Timeout = 1u << 0,
            Miscompare = 1u << 1,
            Sequence = 1u << 2,
        }

        private enum FaultAction : uint
        {
            Reset = 0b001,
            Interrupt = 0b010,
            Disabled = 0b100,
        }

        private enum LockControl : uint
        {
            Locked = 0b01,
            Unlocked = 0b10,
        }

        private enum PauseControl : uint
        {
            KeepRunning = 0b01,
            Pause = 0b10,
        }

        private enum Command : uint
        {
            Start = 1,
            Stop = 2,
            Restart = 3,
            Add = 4,
            Sub = 5,
            Assert16 = 6,
        }

        private enum Registers : long
        {
            Control = 0x0,
            Reload = 0x4,
            InstructionTimer = 0x8,
            Status = 0x10,
            Status2 = 0x14,
            Flags = 0x18,
            Persistent = 0x1C,
            Start = 0x20,
            Stop = 0x24,
            Restart = 0x28,
            Add = 0x2C,
            Add1 = 0x30,
            Add16 = 0x34,
            Add256 = 0x38,
            Sub = 0x3C,
            Sub1 = 0x40,
            Sub16 = 0x44,
            Sub256 = 0x48,
            Assert16 = 0x4C,
        }
    }
}
