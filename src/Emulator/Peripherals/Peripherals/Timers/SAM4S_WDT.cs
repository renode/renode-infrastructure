//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class SAM4S_WDT : LimitTimer, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public SAM4S_WDT(IMachine machine, SAM4S_RSTC resetController, long slowClockFrequency = 32768)
            : base(machine.ClockSource, slowClockFrequency, enabled: false, divider: 128, limit: MaximumWatchdogValue, workMode: WorkMode.Periodic, autoUpdate: true, eventEnabled: true)
        {
            if(resetController == null)
            {
                throw new ConstructionException($"'{nameof(resetController)}' was null");
            }

            RegistersCollection = new DoubleWordRegisterCollection(this);
            this.resetController = resetController;
            this.machine = machine;

            DefineRegisters();

            LimitReached += () =>
            {
                underflowPending.Value = true;
                TriggerWatchdogFault();
            };
        }

        public override void Reset()
        {
            base.Reset();

            modeRegisterLocked = false;
            RegistersCollection.Reset();
            IRQ.Unset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset == (uint)Registers.Mode && modeRegisterLocked)
            {
                this.Log(LogLevel.Warning, "Tried to write WDT_MR register after it has been locked");
                return;
            }

            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x10;

        public GPIO IRQ { get; } = new GPIO();

        private void TriggerWatchdogFault()
        {
            if(interruptEnabled.Value)
            {
                IRQ.Set();
            }

            if(watchdogReset.Value)
            {
                resetController.InvokeReset(SAM4S_RSTC.ResetType.WatchdogReset, resetProcessor: true, resetPeripherals: !resetOnlyProcessor.Value);
            }
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, out var resetPending, name: "WDRSTT")
                .WithReservedBits(1, 23)
                .WithValueField(24, 8, out var providedKey, name: "KEY")
                .WithWriteCallback((_, __) =>
                {
                    if(providedKey.Value != WatchdogPassword)
                    {
                        this.Log(LogLevel.Debug, "Software tried to modify WDT_CR with invalid password: {0:X} instead of {1:X}",
                            providedKey.Value, WatchdogPassword);
                        return;
                    }

                    if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                    {
                        cpu.SyncTime();
                    }

                    if(Value > watchdogDelta.Value)
                    {
                        this.Log(LogLevel.Info, "Tried to refresh watchdog while not in correct interval: {0} > {1}", Value, watchdogDelta.Value);
                        errorPending.Value = true;
                        TriggerWatchdogFault();
                        return;
                    }

                    if(resetPending.Value)
                    {
                        Value = Limit;
                    }

                    resetPending.Value = false;
                })
            ;

            // NOTE: Only first write is accepted by this register,
            //       after first write has been handled this register behaves as read-only.
            Registers.Mode.Define(this, 0x3FFF2FFF)
                .WithValueField(0, 12, name: "WDV",
                    valueProviderCallback: _ => Value,
                    writeCallback: (_, value) => Limit = value)
                // NOTE: We don't have to update IRQ on interruptEnabled as this value
                //       can be only changed once
                .WithFlag(12, out interruptEnabled, name: "WDIFEN")
                .WithFlag(13, out watchdogReset, name: "WDRSTEN")
                .WithFlag(14, out resetOnlyProcessor, name: "WDRPROC")
                .WithFlag(15, name: "WDDIS",
                    valueProviderCallback: _ => !Enabled,
                    writeCallback: (_, value) => Enabled = !value)
                .WithValueField(16, 12, out watchdogDelta, name: "WDD")
                .WithTaggedFlag("WDDBGHLT", 28)
                .WithTaggedFlag("WDIDLEHLT", 29)
                .WithReservedBits(30, 2)
                .WithWriteCallback((_, __) => modeRegisterLocked = true)
            ;

            Registers.Status.Define(this, 0x00000000)
                .WithFlag(0, out underflowPending, FieldMode.ReadToClear, name: "WDUNF")
                .WithFlag(1, out errorPending, FieldMode.ReadToClear, name: "WDERR")
                .WithReservedBits(2, 30)
                .WithReadCallback((_, __) =>
                {
                    IRQ.Unset();
                })
            ;
        }

        private bool modeRegisterLocked;
        private IValueRegisterField watchdogDelta;
        private IFlagRegisterField interruptEnabled;
        private IFlagRegisterField watchdogReset;
        private IFlagRegisterField resetOnlyProcessor;
        private IFlagRegisterField underflowPending;
        private IFlagRegisterField errorPending;

        private readonly SAM4S_RSTC resetController;
        private readonly IMachine machine;

        private const int MaximumWatchdogValue = 0xFFF;
        private const int WatchdogPassword = 0xA5;

        public enum Registers
        {
            Control = 0x00,
            Mode = 0x04,
            Status = 0x08,
        }
    }
}
