//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class PulseGenerator : IPeripheral, INumberedGPIOOutput
    {
        public PulseGenerator(IMachine machine, long frequency, ulong onTicks, ulong offTicks, bool startState = false)
        {
            this.startState = startState;
            Connections = new Dictionary<int, IGPIO> { [0] = Output };
            timer = new LimitTimer(machine.ClockSource, frequency, this, nameof(timer), limit: startState ? offTicks : onTicks, enabled: true, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += () =>
            {
                var irq = timer.Limit == onTicks == startState;
                timer.Limit = timer.Limit == onTicks ? offTicks : onTicks;
                Output.Set(irq);
                this.DebugLog("Output set to {0}", irq);
            };
            Reset();
        }

        public void Reset()
        {
            timer.Reset();
            Output.Set(startState);
        }

        public GPIO Output { get; } = new GPIO();

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public bool Enabled
        {
            get { return timer.Enabled; }
            set { timer.Enabled = value; }
        }

        public long Frequency
        {
            get { return timer.Frequency; }
            set { timer.Frequency = value; }
        }

        private readonly bool startState;
        private readonly LimitTimer timer;
    }
}
