//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class SAM4S_RSTC : BasicDoubleWordPeripheral, IGPIOReceiver, IKnownSize
    {
        public SAM4S_RSTC(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public void InvokeReset(ResetType reason, bool resetProcessor = false, bool resetPeripherals = false, bool assertPin = false)
        {
            if(!resetProcessor && !resetPeripherals && assertPin)
            {
                this.Log(LogLevel.Info, "Asserting IRQ pin");
                userResetStatus = true;
                IRQ.Set();
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
            else
            {
                return;
            }

            this.Log(LogLevel.Info, "Resetting {0}", resetVector);
            lastResetReason.Value = reason;

            if(machine.SystemBus.TryGetCurrentCPU(out var cpu) && resetProcessor)
            {
                if(!(cpu is CortexM cortexM))
                {
                    this.Log(LogLevel.Error, "CPU reset has been requested, but the CPU is not cortex-m; is platform configuration correct?");
                    return;
                }

                machine.LocalTimeSource.ExecuteInNearestSyncedState(_ =>
                {
                    var currentVectorBase = cortexM.VectorTableOffset;
                    cpu.Reset();
                    cortexM.VectorTableOffset = currentVectorBase;
                    cpu.Resume();
                });
            }

            if(resetPeripherals)
            {
                machine.RequestResetInSafeState(unresetable: new IPeripheral[] { this, cpu, machine.SystemBus });
            }
        }

        public void OnGPIO(int pin, bool state)
        {
            if(pin != 0)
            {
                this.Log(LogLevel.Warning, "Tried to write to pin different than 0, ignoring");
                return;
            }

            if(!state && userResetEnabled)
            {
                InvokeReset(ResetType.UserReset, resetProcessor: true);
            }
            else if(previousPinState && !state)
            {
                InvokeReset(ResetType.UserReset, assertPin: !userResetEnabled && userResetInterrupt);
            }

            previousPinState = state;
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
        }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x10;

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
                        var previousValue = userResetStatus;
                        if(previousValue && userResetInterrupt)
                        {
                            IRQ.Unset();
                        }
                        userResetStatus = false;
                        return previousValue;
                    })
                .WithReservedBits(1, 7)
                .WithEnumField(8, 3, out lastResetReason, name: "RSTTYP")
                .WithReservedBits(11, 5)
                .WithTaggedFlag("NRSTL", 16)
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

                    userResetEnabled = resetEnabled.Value;
                    userResetInterrupt = interruptEnabled.Value;
                })
            ;
        }

        private IEnumRegisterField<ResetType> lastResetReason;

        private bool previousPinState;
        private bool userResetStatus;
        private bool userResetEnabled;
        private bool userResetInterrupt;

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
