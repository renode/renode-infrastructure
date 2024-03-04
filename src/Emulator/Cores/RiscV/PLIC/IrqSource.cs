//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.IRQControllers.PLIC
{
    public class IrqSource
    {
        public IrqSource(uint id, IPlatformLevelInterruptController irqController)
        {
            this.parent = irqController;

            Id = id;
            Reset();
        }

        public override string ToString()
        {
            return $"IrqSource id: {Id}, priority: {Priority}, state: {State}";
        }

        public void Reset()
        {
            Priority = DefaultPriority;
            State = false;
        }

        public uint Id { get; private set; }

        public uint Priority
        {
             get { return priority; }
             set
             {
                 if(value == priority)
                 {
                     return;
                 }

                 parent.Log(LogLevel.Noisy, "Setting priority {0} for source #{1}", value, Id);
                 priority = value;
             }
        }

        public bool State
        {
            get { return state; }
            set
            {
                if(value == state)
                {
                    return;
                }

                state = value;
                parent.Log(LogLevel.Noisy, "Setting state to {0} for source #{1}", value, Id);
            }
        }

        private uint priority;
        private bool state;

        private readonly IPlatformLevelInterruptController parent;

        // 1 is the default, lowest value. 0 means "no interrupt".
        private const uint DefaultPriority = 1;
    }
}
