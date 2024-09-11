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
            this.onTicks = onTicks;
            this.offTicks = offTicks;
            Connections = new Dictionary<int, IGPIO> { [0] = Output };
            timer = new LimitTimer(machine.ClockSource, frequency, this, nameof(timer), enabled: true, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += () =>
            {
                timer.Limit = state ? offTicks : onTicks;
                state = !state;
                Output.Set(state);
                this.DebugLog("Output set to {0}", state);
            };
            Reset();
        }

        public void Reset()
        {
            timer.Limit = !startState ? offTicks : onTicks;
            state = startState;
            Output.Set(state);
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

        private bool state;

        private readonly bool startState;
        private readonly ulong onTicks;
        private readonly ulong offTicks;
        private readonly LimitTimer timer;
    }
}
