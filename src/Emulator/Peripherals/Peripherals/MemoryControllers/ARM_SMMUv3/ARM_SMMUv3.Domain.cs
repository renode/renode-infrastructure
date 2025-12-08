//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public partial class ARM_SMMUv3
    {
        private class Domain
        {
            public Domain(ARM_SMMUv3 parent, SecurityState securityState)
            {
                this.parent = parent;
                SecurityState = securityState;
                StreamTable = new StreamTableEntry[1 << StreamIdBits];
                GlobalErrorIRQ = new GPIO();
                EventQueueIRQ = new GPIO();
            }

            public void Reset()
            {
                Enabled = false;
            }

            public void CreateQueues()
            {
                commandQueue = new WrappingQueue<Command>(parent, parent.sysbus, WrappingQueue<Command>.Role.Consumer,
                    CommandQueueAddress, CommandQueueShift, CommandQueueProduce, CommandQueueConsume, GetCommandType);
                eventQueue = new WrappingQueue<Event>(parent, parent.sysbus, WrappingQueue<Event>.Role.Producer,
                    EventQueueAddress, EventQueueShift, EventQueueProduce, EventQueueConsume);
            }

            public void ProcessCommandQueue()
            {
                while(commandQueue.TryPeek(out var command))
                {
                    parent.DebugLog("Executing command {0} {1}", command.Opcode, command);
                    CommandQueueErrorReason.Value = command.ValidateAndRun(parent, SecurityState);
                    if(CommandQueueErrorReason.Value != CommandError.None)
                    {
                        CommandQueueErrorPresent.Value = !CommandQueueErrorPresent.Value;
                        UpdateInterrupts();
                        break;
                    }

                    // If the command fails, SMMU_(*_)CMDQ_CONS.RD remains pointing to the erroneous command in the Command queue
                    commandQueue.AdvanceConsumerIndex();
                }
            }

            public void SignalEvent(Event ev, bool record = true)
            {
                if(!EventQueueEnable.Value || !record)
                {
                    if(record)
                    {
                        parent.WarningLog("Event enqueued to a disabled event queue, discarding: {0}", ev);
                    }
                    return;
                }

                var wasEmpty = eventQueue.IsEmpty;
                // TODO: Stall events should never be discarded
                if(!eventQueue.TryEnqueue(ev) && eventQueue.IsFull)
                {
                    // Overflow can only be reported if software acknowledged the previous overflow i.e.
                    // copied the value of the `OVFLG` flag to the `OVACKFLG` flag
                    if(EventQueueOverflow.Value == EventQueueOverflowAcknowledge.Value)
                    {
                        // Overflow is signaled by toggling the flag
                        EventQueueOverflow.Value = !EventQueueOverflow.Value;
                    }
                }

                if(wasEmpty && !eventQueue.IsEmpty && EventQueueInterruptEnable.Value)
                {
                    parent.DebugLog("Blinking: {0}{1}", SecurityState, nameof(EventQueueIRQ));
                    // Intentional Blink - there is no status bit for this interrupt
                    EventQueueIRQ.Blink();
                }
            }

            public void InvalidateSte(uint streamId)
            {
                // TODO: Errors, leaf, multilevel stream table
                if(streamId >= StreamTableSize)
                {
                    parent.WarningLog("Attempt to invalidate STE {0} which is out of range (table size: {1})", streamId, StreamTableSize);
                    return;
                }
                var ste = parent.ReadStruct<StreamTableEntry>(StreamTableAddress.Value << 6, streamId);
                parent.NoisyLog("Invalidated STE {0} = {1}", streamId, ste);
                StreamTable[streamId] = ste;
            }

            public void UpdateInterrupts()
            {
                var globalError = CommandQueueErrorPresent.Value; // TODO: Other global error flags
                globalError &= GlobalErrorInterruptEnable.Value;
                parent.DebugLog("{0}{1}: {2}", SecurityState, nameof(GlobalErrorIRQ), globalError);
                GlobalErrorIRQ.Set(globalError);
                // EventQueueIRQ is Blink()'ed so don't set it here
            }

            public SecurityState SecurityState { get; }

            public StreamTableEntry[] StreamTable { get; }

            public int StreamTableSize => 1 << Math.Min((int)StreamTableShift.Value, StreamIdBits);

            public bool Enabled
            {
                get => enabled;
                set
                {
                    if(enabled == value)
                    {
                        return;
                    }
                    enabled = value;
                    foreach(var controller in parent.streamControllers.Where(x => x.Key.SecurityState == SecurityState).Select(x => x.Value))
                    {
                        controller.Enabled = enabled;
                    }
                }
            }

            public GPIO GlobalErrorIRQ { get; }

            public GPIO EventQueueIRQ { get; }

            public IValueRegisterField CommandQueueShift;
            public IValueRegisterField CommandQueueAddress;
            public IValueRegisterField CommandQueueConsume;
            public IValueRegisterField CommandQueueProduce;
            public IEnumRegisterField<CommandError> CommandQueueErrorReason;
            public IFlagRegisterField CommandQueueErrorPresent;
            public IValueRegisterField StreamTableShift;
            public IValueRegisterField StreamTableAddress;
            public IEnumRegisterField<StreamTableLevel> StreamTableFormat;
            public IFlagRegisterField GlobalErrorInterruptEnable;
            public IFlagRegisterField EventQueueEnable;
            public IFlagRegisterField EventQueueInterruptEnable;
            public IValueRegisterField EventQueueShift;
            public IValueRegisterField EventQueueAddress;
            public IValueRegisterField EventQueueConsume;
            public IValueRegisterField EventQueueProduce;
            public IFlagRegisterField EventQueueOverflow;
            public IFlagRegisterField EventQueueOverflowAcknowledge;

            private bool enabled;
            private WrappingQueue<Command> commandQueue;
            private WrappingQueue<Event> eventQueue;

            private readonly ARM_SMMUv3 parent;
        }
    }
}
