//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class NPCX_MTC : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public NPCX_MTC(IMachine machine)
        {
            IRQ = new GPIO();

            timer = new LimitTimer(machine.ClockSource, 1, this, "timer", 1, eventEnabled: true);
            timer.LimitReached += () =>
            {
                timerValue.Value++;
                if(BitHelper.GetValue(timerValue.Value, 0, PredefinedTimeBits) == predefinedTime.Value)
                {
                    predefinedTimeOccurred.Value = true;
                    UpdateInterrupt();
                }
            };

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.TimingTicksCount, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out timerValue, name: "TTC (Timing Ticks Count)", softResettable: false)
                },
                {(long)Registers.WakeUpTicksCount, new DoubleWordRegister(this)
                    .WithFlag(31, out interruptEnabled, name: "WIE (Wake-Up/Interrupt Enabled)")
                    .WithFlag(30, out predefinedTimeOccurred, FieldMode.Read | FieldMode.WriteOneToClear)
                    .WithReservedBits(25, 5)
                    .WithValueField(0, 25, out predefinedTime, name: "PT (Predefined Time)")
                    .WithWriteCallback((_, __) => UpdateInterrupt())
                },
            };
            RegistersCollection = new DoubleWordRegisterCollection(this, registerMap);

            Reset();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            timer.Enabled = true;
            IRQ.Unset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public GPIO IRQ { get; }
        public long Size => 0x08;
        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void UpdateInterrupt()
        {
            var irqState = interruptEnabled.Value && predefinedTimeOccurred.Value;
            this.DebugLog("{0} interrupt", irqState ? "Setting" : "Unsetting");
            IRQ.Set(irqState);
        }

        private readonly LimitTimer timer;

        private readonly IValueRegisterField timerValue;
        private readonly IValueRegisterField predefinedTime;
        private readonly IFlagRegisterField predefinedTimeOccurred;
        private readonly IFlagRegisterField interruptEnabled;

        private const int PredefinedTimeBits = 25;

        private enum Registers : long
        {
            TimingTicksCount = 0x00, // TTC
            WakeUpTicksCount = 0x04, // WTC
        }
    }
}
