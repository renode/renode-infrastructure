//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class SAM4S_RSTC : BasicDoubleWordPeripheral, IGPIOReceiver, IKnownSize, IHasOwnLife
    {
        public SAM4S_RSTC(IMachine machine) : base(machine)
        {
            NRST.Set();
            DefineRegisters();
        }

        public void InvokeReset(ResetType reason, bool resetProcessor = false, bool resetPeripherals = false, bool resetExternal = false)
        {
            if(!resetProcessor && !resetPeripherals && !resetExternal)
            {
                return;
            }

            string resetVector = null;
            if(resetProcessor && resetPeripherals)
            {
                resetVector = "platform";
            }
            else if(resetProcessor)
            {
                resetVector = "CPU";
            }
            else if(resetPeripherals)
            {
                resetVector = "peripherals";
            }
            if(resetExternal)
            {
                if(resetVector == null)
                {
                    resetVector = "externals";
                }
                else
                {
                    resetVector += " with externals";
                }
            }

            this.Log(LogLevel.Info, "Resetting {0}", resetVector);
            lastResetReason.Value = reason;

            if(resetExternal)
            {
                outputState = false;
                if(inputState)
                {
                    this.Log(LogLevel.Info, "Asserting NRST pin");
                    NRST.Unset();
                }
            }

            if(resetPeripherals)
            {
                var cpu = CPU;
                var exclude = new List<IPeripheral> { this, machine.SystemBus, cpu };
                if(!resetProcessor && Watchdog != null)
                {
                    exclude.Add(Watchdog);
                }

                var currentVectorBase = cpu?.VectorTableOffset ?? 0x0;
                machine.RequestResetInSafeState(unresetable: exclude,
                    postReset: () =>
                    {
                        FinishExternalReset();
                        PostReset?.Invoke(lastResetReason.Value);
                        if(resetProcessor && cpu != null)
                        {
                            cpu.Reset();
                            cpu.VectorTableOffset = currentVectorBase;
                            cpu.Resume();
                        }
                    }
                );
            }
            else if(resetProcessor)
            {
                var cpu = CPU;
                machine.LocalTimeSource.ExecuteInNearestSyncedState(_ =>
                {
                    Watchdog?.Reset();
                    if(cpu != null)
                    {
                        var currentVectorBase = cpu.VectorTableOffset;
                        cpu.Reset();
                        cpu.VectorTableOffset = currentVectorBase;
                        cpu.Resume();
                    }
                    FinishExternalReset();
                    PostReset?.Invoke(lastResetReason.Value);
                });
            }
            else
            {
                FinishExternalReset();
                PostReset?.Invoke(lastResetReason.Value);
            }
        }

        public void OnGPIO(int pin, bool state)
        {
            if(pin != 0)
            {
                this.Log(LogLevel.Warning, "Tried to write to pin different than 0, ignoring");
                return;
            }
            if(state == inputState)
            {
                return;
            }

            // set userResetStatus on rising edge
            userResetStatus = NRST.IsSet && !state;
            inputState = state;
            NRST.Set(outputState && state);
            if(userResetStatus && userResetEnabled)
            {
                InvokeReset(ResetType.UserReset, resetProcessor: true, resetPeripherals: true);
            }
            UpdateInterrupts();
        }

        public override void Reset()
        {
            base.Reset();
            inputState = true;
            outputState = true;
            NRST.Set();
            IRQ.Unset();
        }

        public void Start()
        {
            InitCPU();
        }

        public void Pause()
        {
            // Intentionally left empty
        }

        public void Resume()
        {
            // Intentionally left empty
        }

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        public GPIO NRST { get; } = new GPIO();

        public bool LingerNRST { get; set; }

        public long Size => 0x10;

        public IPeripheral Watchdog { get; set; }

        public bool IsPaused => false;

        public event Action<ResetType> PostReset;

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, out var processorReset, name: "PROCRST",
                    valueProviderCallback: _ => false)
                .WithReservedBits(1, 1)
                .WithFlag(2, out var peripheralReset, name: "PERRST",
                    valueProviderCallback: _ => false)
                .WithFlag(3, out var externalReset, name: "EXTRST",
                    valueProviderCallback: _ => false)
                .WithReservedBits(4, 20)
                .WithValueField(24, 8, out var controlKey, name: "KEY",
                    valueProviderCallback: _ => 0)
                .WithWriteCallback((_, __) =>
                {
                    if(controlKey.Value != ResetPassword)
                    {
                        this.Log(LogLevel.Info, "Software tried to invoke software reset, but wrong key has been provided: {0:X} vs {1:X}", controlKey, ResetPassword);
                        return;
                    }

                    InvokeReset(ResetType.SoftwareReset, processorReset.Value, peripheralReset.Value, externalReset.Value);
                })
            ;

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "URSTS",
                    valueProviderCallback: _ =>
                    {
                        UpdateInterrupts();
                        var previousValue = userResetStatus;
                        userResetStatus = false;
                        return previousValue;
                    })
                .WithReservedBits(1, 7)
                .WithEnumField(8, 3, out lastResetReason, name: "RSTTYP")
                .WithReservedBits(11, 5)
                .WithFlag(16, FieldMode.Read, name: "NRSTL",
                    valueProviderCallback: _ => NRST.IsSet)
                .WithFlag(17, FieldMode.Read, name: "SRCMP",
                    valueProviderCallback: _ => false)
                .WithReservedBits(18, 14)
            ;

            Registers.Mode.Define(this, 0x00000001)
                .WithFlag(0, out var resetEnabled, FieldMode.Write, name: "URSTEN")
                .WithReservedBits(1, 3)
                .WithFlag(4, out var interruptEnabled, FieldMode.Write, name: "URSTIEN")
                .WithReservedBits(5, 3)
                .WithTag("ERSTL", 8, 4)
                .WithReservedBits(12, 12)
                .WithValueField(24, 8, out var modeKey, FieldMode.Write, name: "KEY")
                .WithWriteCallback((_, __) =>
                {
                    if(modeKey.Value != ResetPassword)
                    {
                        this.Log(LogLevel.Info, "Software tried to modify configuration, but wrong key has been provided: {0:X} vs {1:X}", modeKey, ResetPassword);
                        return;
                    }

                    var userReset = !userResetEnabled && resetEnabled.Value && !inputState;
                    userResetEnabled = resetEnabled.Value;
                    userResetInterrupt = interruptEnabled.Value;
                    if(userReset)
                    {
                        InvokeReset(ResetType.UserReset, true, true, false);
                    }
                    UpdateInterrupts();
                })
            ;
        }

        private void InitCPU()
        {
            if(cpuChecked)
            {
                return;
            }

            var icpu = machine.SystemBus.GetCPUs().SingleOrDefault();
            if(icpu == null)
            {
                this.Log(LogLevel.Error, "Invalid configuration, a single-core platform is required");
            }
            else if(!(icpu is CortexM cortexM))
            {
                this.Log(LogLevel.Error, "Invalid configuration, the CPU is required to be a cortex-m");
            }
            else
            {
                cpu = cortexM;
            }
            cpuChecked = true;
        }

        private void FinishExternalReset()
        {
            if(outputState)
            {
                return;
            }
            outputState = true;

            if(inputState && !LingerNRST)
            {
                // both input and output are high
                NRST.Set();
                return;
            }
            if(lastResetReason.Value == ResetType.UserReset)
            {
                // trigger user reset only once
                this.Log(LogLevel.Noisy, "Breaking user resetting");
                if(inputState)
                {
                    // stop lingering NRST assert
                    NRST.Set();
                }
                return;
            }

            PostReset?.Invoke(lastResetReason.Value);

            if(!inputState)
            {
                this.Log(LogLevel.Debug, "Invoking User Reset post reset");
            }
            else
            {
                this.Log(LogLevel.Debug, "Invoking lingering User Reset post NRST assertion");
            }

            // won't recurse infinitely due to triggering user reset only once check
            InvokeReset(ResetType.UserReset, true, true, false);
        }

        private void UpdateInterrupts()
        {
            var state = userResetStatus && !userResetEnabled && userResetInterrupt;
            if(!IRQ.IsSet && state)
            {
                this.Log(LogLevel.Noisy, "Asserting IRQ pin");
            }
            IRQ.Set(state);
        }

        private CortexM CPU
        {
            get
            {
                InitCPU();
                return cpu;
            }
        }

        private IEnumRegisterField<ResetType> lastResetReason;

        private bool inputState = true;
        private bool outputState = true;
        private bool userResetStatus;
        private bool userResetEnabled;
        private bool userResetInterrupt;

        private CortexM cpu;
        private bool cpuChecked;
        private const int ResetPassword = 0xA5;

        public enum ResetType
        {
            GeneralReset,
            BackupReset,
            WatchdogReset,
            SoftwareReset,
            UserReset,
            Reserved0,
            Reserved1,
            Reserved2,
        }

        public enum Registers
        {
            Control = 0x00,
            Status = 0x04,
            Mode = 0x08,
        }
    }
}
