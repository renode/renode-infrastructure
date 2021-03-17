//
// Copyright (c) 2010-2021 Antmicro
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
    public class IrqTarget
    {
        public IrqTarget(uint id, IPlatformLevelInterruptController irqController)
        {
            handlers = new []
            {
                new IrqTargetHandler(irqController, 0)
            };
        }

        public IrqTarget(uint id, IPlatformLevelInterruptController irqController, PrivilegeLevel[] privilegeLevels)
        {
            handlers = new IrqTargetHandler[privilegeLevels.Length];
            for(var i = 0; i < privilegeLevels.Length; i++)
            {
                handlers[i] = new IrqTargetHandler(irqController, id, privilegeLevels[i]);
            };
        }

        public void RefreshAllInterrupts()
        {
            foreach(var h in handlers)
            {
                h.RefreshInterrupt();
            }
        }

        public void Reset()
        {
            foreach(var h in handlers)
            {
                h.Reset();
            }
        }

        public readonly IrqTargetHandler[] handlers;

        public class IrqTargetHandler
        {
            public IrqTargetHandler(IPlatformLevelInterruptController irqController, uint targetId, PrivilegeLevel? privilegeLevel = null)
            {
                this.irqController = irqController;
                this.targetId = targetId;
                this.privilegeLevel = privilegeLevel;

                enabledSources = new HashSet<IrqSource>();
                activeInterrupts = new Stack<IrqSource>();
            }

            public override string ToString()
            {
                var result = $"[Hart #{targetId}";
                if(privilegeLevel.HasValue)
                {
                    result +=  $" at {privilegeLevel} level";
                }
                result +=  $" (connection output #{ConnectionNumber})]";

                return result;
            }

            public void Reset()
            {
                activeInterrupts.Clear();
                enabledSources.Clear();

                RefreshInterrupt();
            }

            public void RefreshInterrupt()
            {
                var forcedTarget = irqController.ForcedTarget;
                if(forcedTarget != -1 && this.targetId != forcedTarget)
                {
                    irqController.Connections[ConnectionNumber].Set(false);
                    return;
                }

                var currentPriority = activeInterrupts.Count > 0 ? activeInterrupts.Peek().Priority : 0;
                var isPending = enabledSources.Any(x => x.Priority > currentPriority && x.IsPending);
                irqController.Connections[ConnectionNumber].Set(isPending);
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

                irq.IsPending = irq.State;
                RefreshInterrupt();
            }

            public void EnableSource(IrqSource s, bool enabled)
            {
                if(enabled)
                {
                    enabledSources.Add(s);
                }
                else
                {
                    enabledSources.Remove(s);
                }
                irqController.Log(LogLevel.Noisy, "{0} source #{1} @ {2}", enabled ? "Enabling" : "Disabling", s.Id, this);
                RefreshInterrupt();
            }

            public uint AcknowledgePendingInterrupt()
            {
                IrqSource pendingIrq;

                var forcedTarget = irqController.ForcedTarget;
                if(forcedTarget != -1 && this.targetId != forcedTarget)
                {
                    pendingIrq = null;
                }
                else
                {
                    pendingIrq = enabledSources.Where(x => x.IsPending)
                        .OrderByDescending(x => x.Priority)
                        .ThenBy(x => x.Id).FirstOrDefault();
                }

                if(pendingIrq == null)
                {
                    irqController.Log(LogLevel.Noisy, "There is no pending interrupt to acknowledge at the moment for {0}. Currently enabled sources: {1}", this, string.Join(", ", enabledSources.Select(x => x.ToString())));
                    return 0;
                }
                pendingIrq.IsPending = false;
                activeInterrupts.Push(pendingIrq);

                irqController.Log(LogLevel.Noisy, "Acknowledging pending interrupt #{0} @ {1}", pendingIrq.Id, this);

                RefreshInterrupt();
                return pendingIrq.Id;
            }

            private int ConnectionNumber => (int)(4 * targetId + (privilegeLevel.HasValue ? (int)privilegeLevel.Value : 0));

            private readonly uint targetId;
            private readonly PrivilegeLevel? privilegeLevel;
            private readonly IPlatformLevelInterruptController irqController;
            private readonly HashSet<IrqSource> enabledSources;
            private readonly Stack<IrqSource> activeInterrupts;
        }
    }
}
