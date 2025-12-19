//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

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
            }

            public void Reset()
            {
                Enabled = false;
            }

            public void CreateQueues()
            {
                commandQueue = new WrappingQueue<Command>(parent, parent.sysbus, WrappingQueue<Command>.Role.Consumer,
                    CommandQueueAddress, CommandQueueShift, CommandQueueProduce, CommandQueueConsume, GetCommandType);
            }

            public void ProcessCommandQueue()
            {
                while(commandQueue.TryPeek(out var command))
                {
                    parent.DebugLog("Executing command {0} {1}", command.Opcode, command);
                    CommandQueueErrorReason.Value = command.Run(parent);
                    if(CommandQueueErrorReason.Value != CommandError.None)
                    {
                        CommandQueueErrorPresent.Value = !CommandQueueErrorPresent.Value;
                        break;
                    }

                    // If the command fails, SMMU_(*_)CMDQ_CONS.RD remains pointing to the erroneous command in the Command queue
                    commandQueue.AdvanceConsumerIndex();
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
            public IFlagRegisterField EventQueueInterruptEnable;

            private bool enabled;
            private WrappingQueue<Command> commandQueue;

            private readonly ARM_SMMUv3 parent;
        }
    }
}
