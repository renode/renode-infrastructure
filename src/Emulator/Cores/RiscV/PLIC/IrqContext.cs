//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.IRQControllers.PLIC
{
    public class IrqContext
    {
        public IrqContext(uint id, IPlatformLevelInterruptController irqController)
        {
            this.irqController = irqController;
            this.id = id;

            enabledSources = new HashSet<IrqSource>();
            pendingSources = new HashSet<IrqSource>();
            activeInterrupts = new Stack<IrqSource>();
        }

        public override string ToString()
        {
            return $"[Context #{id}]";
        }

        public void Reset()
        {
            activeInterrupts.Clear();
            enabledSources.Clear();

            RefreshInterrupt();
        }

        public void RefreshInterrupt()
        {
            var forcedContext = irqController.ForcedContext;
            if(forcedContext != -1 && this.id != forcedContext)
            {
                irqController.Connections[(int)this.id].Set(false);
                return;
            }

            var currentPriority = activeInterrupts.Count > 0 ? activeInterrupts.Peek().Priority : 0;
            var isPending = pendingSources.Any(x => x.Priority > currentPriority);
            irqController.Connections[(int)this.id].Set(isPending);
        }

        public void CompleteHandlingInterrupt(IrqSource irq)
        {
            irqController.Log(LogLevel.Noisy, "Completing irq {0} at {1}", irq.Id, this);

            if(activeInterrupts.Count == 0)
            {
                irqController.Log(LogLevel.Error, "Trying to complete irq {0} @ {1}, there are no active interrupts left", irq.Id, this);
                return;
            }
            var topActiveInterrupt = activeInterrupts.Pop();
            if(topActiveInterrupt != irq)
            {
                irqController.Log(LogLevel.Error, "Trying to complete irq {0} @ {1}, but {2} is the active one", irq.Id, this, topActiveInterrupt.Id);
                return;
            }

            if(irq.State) 
            {
                MarkSourceAsPending(irq);
            }
            else 
            {
                RemovePendingStatusFromSource(irq);
            }
            RefreshInterrupt();
        }

        public void EnableSource(IrqSource s, bool enabled)
        {
            if(enabled)
            {
                enabledSources.Add(s);
                if(s.State)
                {
                    MarkSourceAsPending(s);
                }
            }
            else
            {
                enabledSources.Remove(s);
                RemovePendingStatusFromSource(s);
            }
            irqController.Log(LogLevel.Noisy, "{0} source #{1} @ {2}", enabled ? "Enabling" : "Disabling", s.Id, this);
            RefreshInterrupt();
        }

        public void MarkSourceAsPending(IrqSource s)
        {
            if(enabledSources.Contains(s))
            {
                if(pendingSources.Add(s))
                {
                    irqController.Log(LogLevel.Noisy, "Setting pending status to True for source #{0}", s.Id);
                }
            }
        }

        public uint AcknowledgePendingInterrupt()
        {
            IrqSource pendingIrq;

            var forcedContext = irqController.ForcedContext;
            if(forcedContext != -1 && this.id != forcedContext)
            {
                pendingIrq = null;
            }
            else
            {
                pendingIrq = pendingSources
                    .OrderByDescending(x => x.Priority)
                    .ThenBy(x => x.Id).FirstOrDefault();
            }

            if(pendingIrq == null)
            {
                irqController.Log(LogLevel.Noisy, "There is no pending interrupt to acknowledge at the moment for {0}. Currently enabled sources: {1}", this, string.Join(", ", enabledSources.Select(x => x.ToString())));
                return 0;
            }
            RemovePendingStatusFromSource(pendingIrq);
            activeInterrupts.Push(pendingIrq);

            irqController.Log(LogLevel.Noisy, "Acknowledging pending interrupt #{0} @ {1}", pendingIrq.Id, this);

            RefreshInterrupt();
            return pendingIrq.Id;
        }

        private void RemovePendingStatusFromSource(IrqSource s)
        {
            if(pendingSources.Remove(s)) 
            {
                irqController.Log(LogLevel.Noisy, "Setting pending status to False for source #{0}", s.Id);
            }
        }

        private readonly uint id;
        private readonly IPlatformLevelInterruptController irqController;
        private readonly HashSet<IrqSource> enabledSources;
        private readonly HashSet<IrqSource> pendingSources;
        private readonly Stack<IrqSource> activeInterrupts;
    }
}
