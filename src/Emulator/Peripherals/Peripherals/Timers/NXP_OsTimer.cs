//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class NXP_OsTimer : BasicDoubleWordPeripheral, IKnownSize
    {
        public NXP_OsTimer(IMachine machine, long frequency) : base(machine)
        {
            innerTimer = new ComparingTimer(machine.ClockSource, frequency, this, nameof(innerTimer), workMode: WorkMode.Periodic, eventEnabled: true, direction: Direction.Ascending, enabled: true);
            innerTimer.CompareReached += () =>
            {
                this.Log(LogLevel.Debug, "Compare value reached");
                irqFlag.Value = true;
                UpdateInterrupt();
            };

            DefineRegisters();
        }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.MatchLow.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) =>
                {
                    // it's not fully clear if HW does the latching,
                    // but the SDK driver is always acccessing the Low register first
                    // hence this implementation
                    matchLowLatched = (uint)val;
                });

            Registers.MatchHigh.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) =>
                {
                    // here we assume that the driver will first write the Match Low
                    // register and set the correct `matchLowLatched` value
                    if(!matchLowLatched.HasValue)
                    {
                        this.Log(LogLevel.Error, "Match low register expected to be set before the match high one");
                        matchLowLatched = (uint)Misc.BinaryToGray((uint)innerTimer.Compare);
                    }

                    var nextCompare = ((ulong)val << 32) + matchLowLatched.Value;
                    nextCompare = Misc.GrayToBinary(nextCompare);

                    this.Log(LogLevel.Debug, "Changing compare value from 0x{0:X} to 0x{0:X}", innerTimer.Compare, nextCompare);
                    innerTimer.Compare = nextCompare;
                    matchLowLatched = null;
                });

            Registers.CaptureLow.Define(this)
                .WithReservedBits(0, 32);

            Registers.CaptureHigh.Define(this)
                .WithReservedBits(0, 32);

            Registers.EventTimerHigh.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                {
                    SyncTime();
                    return Misc.BinaryToGray(innerTimer.Value) >> 32;
                });

            Registers.EventTimerLow.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                {
                    SyncTime();
                    return Misc.BinaryToGray(innerTimer.Value);
                });

            Registers.Control.Define(this)
                .WithFlag(0, out irqFlag, FieldMode.WriteOneToClear | FieldMode.Read)
                .WithFlag(1, out irqEnabled)
                .WithFlag(2, name: "MATCH_WR_RDY - EVTimer Match Write Ready", valueProviderCallback: _ => false) // by looking at the driver, it seems this needs to be 0 to allow for match register to be set
                .WithWriteCallback((_, __) => UpdateInterrupt());
        }

        private void SyncTime()
        {
            if(machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
        }

        private void UpdateInterrupt()
        {
            var flag = irqFlag.Value && irqEnabled.Value;

            this.Log(LogLevel.Debug, "Setting IRQ to {0}", flag);
            IRQ.Set(flag);
        }

        private IFlagRegisterField irqEnabled;
        private IFlagRegisterField irqFlag;
        private uint? matchLowLatched;

        private readonly ComparingTimer innerTimer;

        private enum Registers
        {
            EventTimerLow = 0x0,
            EventTimerHigh = 0x4,
            CaptureLow = 0x8,
            CaptureHigh = 0xc,
            MatchLow = 0x10,
            MatchHigh = 0x14,
            // reserved
            Control = 0x1c,
        }
    }
}
