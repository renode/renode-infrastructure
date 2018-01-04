//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public sealed class GIC : IBusPeripheral, ILocalGPIOReceiver, INumberedGPIOOutput, IIRQController
    {
        public GIC(int numberOfCPUs = 1, int itLinesNumber = 10)
        {
            this.numberOfCPUs = numberOfCPUs;
            this.itLinesNumber = itLinesNumber;
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfCPUs; i++)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            privateInterrupts = new IRQState[numberOfCPUs][];
            for(var i = 0; i < privateInterrupts.Length; i++)
            {
                privateInterrupts[i] = new IRQState[32];
            }
            publicInterrupts = new IRQState[991];
            privatePriorities = new byte[numberOfCPUs][];
            for(var i = 0; i < privatePriorities.Length; i++)
            {
                privatePriorities[i] = new byte[32];
            }
            publicPriorities = new byte[991];
            runningPriorities = new byte[numberOfCPUs];
            priorityMasks = new byte[numberOfCPUs];
            enabled = new bool[numberOfCPUs];
            localReceivers = new LocalGPIOReceiver[numberOfCPUs];
            for(var i = 0; i < localReceivers.Length; i++)
            {
                localReceivers[i] = new LocalGPIOReceiver(i, this);
            }
            Reset();
        }

        public void Reset()
        {
            privateInterrupts.Initialize();
            for(var i = 0; i < privateInterrupts.Length; i++)
            {
                for(var j = 0; j < privateInterrupts[i].Length; j++)
                {
                    privateInterrupts[i][j] = (IRQState)(1 << i); // private interrupt is forwarded to the owning CPU
                }
            }
            publicInterrupts.Initialize();
            for(var i = 0; i < privatePriorities.Length; i++)
            {
                privatePriorities[i].Initialize();
            }
            publicPriorities.Initialize();
            for(var i = 0; i < runningPriorities.Length; i++)
            {
                runningPriorities[i] = 0xFF;
            }
            foreach(var interrupt in Connections.Values)
            {
                interrupt.Unset();
            }
            for(var i = 0; i < priorityMasks.Length; i++)
            {
                priorityMasks[i] = 0xFF;
            }
            enabled.Initialize();
        }

        public IGPIOReceiver GetLocalReceiver(int cpuIndex)
        {
            return localReceivers[cpuIndex];
        } 

        [ConnectionRegion("distributor")]
        public uint ReadDoubleWordFromDistributor(long offset)
        {
            switch((DistributorOffset)offset)
            {
            case DistributorOffset.ControlRegister:
                return globallyEnabled ? 1u : 0u;
            case DistributorOffset.InterruptControllerType:
                return (uint)(((numberOfCPUs - 1) << 5) | itLinesNumber);
            }
            if(offset >= DistributorSetEnableStart && offset < DistributorSetEnableEnd)
            {
                return HandleDistributorClearOrSetEnableReadOrActiveRead(offset - DistributorSetEnableStart, true);
            }
            if(offset >= DistributorClearEnableStart && offset < DistributorClearEnableEnd)
            {
                return HandleDistributorClearOrSetEnableReadOrActiveRead(offset - DistributorClearEnableStart, true);
            }
            if(offset >= DistributorPriorityStart && offset < DistributorPriorityEnd)
            {
                return HandleDistributorPriorityRead(offset - DistributorPriorityStart);
            }
            if(offset >= DistributorProcessorTargetStart && offset < DistributorProcessorTargetEnd)
            {
                return HandleDistributorProcessorTargetRead(offset - DistributorProcessorTargetStart);
            }
            if(offset >= DistributorInterruptConfigurationStart && offset < DistributorInterruptConfigurationEnd)
            {
                return HandleDistributorConfigurationRead(offset - DistributorInterruptConfigurationStart);
            }
            if(offset >= DistributorActiveBitStart && offset < DistributorActiveBitEnd)
            {
                return HandleDistributorClearOrSetEnableReadOrActiveRead(offset - DistributorActiveBitStart, false);
            }
            this.Log(LogLevel.Warning, "Unhandled distributor read from 0x{0:X}.", offset);
            return 0;
        }

        [ConnectionRegion("distributor")]
        public void WriteDoubleWordToDistributor(long offset, uint value)
        {
            switch((DistributorOffset)offset)
            {
            case DistributorOffset.ControlRegister:
                globallyEnabled = value > 0;
                return;
            }
            if(offset >= DistributorSetEnableStart && offset < DistributorSetEnableEnd)
            {
                HandleDistributorClearOrSetEnableWrite(offset - DistributorSetEnableStart, value, true);
                return;
            }
            if(offset >= DistributorClearEnableStart && offset < DistributorClearEnableEnd)
            {
                HandleDistributorClearOrSetEnableWrite(offset - DistributorClearEnableStart, value, false);
                return;
            }
            if(offset >= DistributorPriorityStart && offset < DistributorPriorityEnd)
            {
                HandleDistributorPriorityWrite(offset - DistributorPriorityStart, value);
                return;
            }
            if(offset >= DistributorProcessorTargetStart && offset < DistributorProcessorTargetEnd)
            {
                HandleDistributorProcessorTargetWrite(offset - DistributorProcessorTargetStart, value);
                return;
            }
            if(offset >= DistributorInterruptConfigurationStart && offset < DistributorInterruptConfigurationEnd)
            {
                HandleDistributorConfigurationWrite(offset - DistributorInterruptConfigurationStart, value);
                return;
            }
            this.Log(LogLevel.Warning, "Unhandled distributor write to 0x{0:X}, value 0x{1:X}.", offset, value);
        }

        [ConnectionRegion("cpuInterface")]
        public uint ReadDoubleWordFromCPUInterface(long offset)
        {
            var cpu = GetAskingCpu();
            switch((CPUInterfaceOffset)offset)
            {
            case CPUInterfaceOffset.Control:
                return enabled[cpu] ? 1u : 0u;
            case CPUInterfaceOffset.InterruptPriorityMask:
                return priorityMasks[cpu];
            case CPUInterfaceOffset.InterruptAcknowledge:
                return AcknowledgeIRQ(cpu);
            case CPUInterfaceOffset.RunningPriority:
                return runningPriorities[cpu];
            }
            this.Log(LogLevel.Warning, "Unhandled CPU interface read from 0x{0:X}.", offset);
            return 0;
        }

        [ConnectionRegion("cpuInterface")]
        public void WriteDoubleWordToCPUInterface(long offset, uint value)
        {
            var cpu = GetAskingCpu();
            switch((CPUInterfaceOffset)offset)
            {
            case CPUInterfaceOffset.Control:
                enabled[cpu] = value > 0;
                return;
            case CPUInterfaceOffset.InterruptPriorityMask:
                priorityMasks[cpu] = (byte)value;
                Update(cpu);
                return;
            case CPUInterfaceOffset.EndOfInterrupt:
                CompleteIRQ((int)value & 1023, cpu);
                return;
            }
            this.Log(LogLevel.Warning, "Unhandled CPU interface write from 0x{0:X}, value 0x{1:X}.", offset, value);
        }

        public void OnGPIO(int number, bool value)
        {
            lock(publicInterrupts)
            {
                this.NoisyLog("External (public) IRQ {0} = {1} internal; value: {2}", number, number + 32, value);
                SetRunningInterrupt(ref publicInterrupts[number], value);
                UpdateAll();
            }
        }

        public IEnumerable<int> GetEnabledInterrupts(int cpu)
        {
            var enabledInterrupts = new List<int>();
            lock(publicInterrupts)
            {
                for(var i = 0; i < publicInterrupts.Length; i++)
                {
                    if(IsEnabled(publicInterrupts[i], cpu))
                    {
                        enabledInterrupts.Add(i + 32);
                    }
                }
                for(var i = 0; i < privateInterrupts[cpu].Length; i++)
                {
                    if(IsEnabled(privateInterrupts[cpu][i], cpu))
                    {
                        enabledInterrupts.Add(i);
                    }
                }
            }
            return enabledInterrupts.OrderBy(x => x);
        }

        [UiAccessible]
        public string[,] GetInterruptsState()
        {
            var result = new Table();
            lock(publicInterrupts)
            {
                result.AddRow("Globally enabled:", globallyEnabled.ToString());
                for(var i = 0; i < enabled.Length; i++)
                {
                    result.AddRow("Enabled for CPU {0}".FormatWith(i), enabled[i].ToString());
                }
                for(var i = 0; i < privateInterrupts.Length; i++)
                {
                    result.AddRow("CPU {0}".FormatWith(i));
                    result.AddRow("Private:");
                    for(var j = 0; j < privateInterrupts[i].Length; j++)
                    {
                        if(!IsEnabled(privateInterrupts[i][j], i))
                        {
                            continue;
                        }
                        result.AddRow(j.ToString(), Misc.PrettyPrintFlagsEnum(privateInterrupts[i][j]));
                    }
                    result.AddRow("Public:");
                    for(var j = 0; j < publicInterrupts.Length; j++)
                    {
                        if(!IsEnabled(publicInterrupts[j], i))
                        {
                            continue;
                        }
                        result.AddRow(j.ToString(), Misc.PrettyPrintFlagsEnum(publicInterrupts[j]));
                    }
                }
            }
            return result.ToArray();
        }

        private bool IsEnabled(IRQState state, int cpuNo)
        {
            return ((int)state & (1 << cpuNo)) != 0 && (state & IRQState.Enabled) != 0;
        }

        private int ScanInterrupts(IRQState[] interrupts, byte[] priorities, ref byte bestPriority, int cpu)
        {
            var bestInterrupt = SpuriousInterrupt;
            if(!globallyEnabled || !enabled[cpu])
            {
                return bestInterrupt;
            }
            var runningPriority = runningPriorities[cpu];
            var forwardedToThisCpu = 1 << cpu;
            for(var i = 0; i < interrupts.Length; i++)
            {
	        var state = interrupts[i];
                if((state & IRQState.Pending) != 0 && priorities[i] < bestPriority && priorities[i] < runningPriority && (state & (IRQState)forwardedToThisCpu) != 0
                   && priorities[i] < priorityMasks[cpu] && (state & IRQState.Enabled) != 0)
                {

                    bestPriority = priorities[i];
                    bestInterrupt = i;
                } 
            }
            return bestInterrupt;
        }

        private uint AcknowledgeIRQ(int cpu)
        {
            lock(publicInterrupts)
            {
                // TODO: return also the source of PPI as (caused by write to IDCSFGIR)
                byte bestPriority = 0xFF;
                var bestPrivate = ScanInterrupts(privateInterrupts[cpu], privatePriorities[cpu], ref bestPriority, cpu);
                var bestPublic = ScanInterrupts(publicInterrupts, publicPriorities, ref bestPriority, cpu);
                int bestInterrupt;
                if(bestPublic != SpuriousInterrupt)
                {
                    bestInterrupt = bestPublic + 32;
                    publicInterrupts[bestPublic] |= IRQState.Active;
                    if((publicInterrupts[bestPublic] & IRQState.EdgeTriggered) != 0)
                    {
                        publicInterrupts[bestPublic] &= ~IRQState.Pending;
                    }
                }
                else if(bestPrivate != SpuriousInterrupt)
                {
                    bestInterrupt = bestPrivate;
                    privateInterrupts[cpu][bestPrivate] |= IRQState.Active;
                    if((privateInterrupts[cpu][bestPrivate] & IRQState.EdgeTriggered) != 0)
                    {
                        privateInterrupts[cpu][bestPrivate] &= ~IRQState.Pending;
                    }
                }
                else
                {
                    bestInterrupt = SpuriousInterrupt;
                }
                runningPriorities[cpu] = bestPriority;
                // because we have selected the best interrupt, we can surely deassert the interrupt
                // we can also have selected spurious interrupt in which case we can deassert
                // the interrupt anyway
                Connections[cpu].Unset();
                this.NoisyLog("Acknowledged IRQ {0}.", bestInterrupt);
                return (uint)bestInterrupt;
            }
        }

        private void CompleteIRQ(int number, int cpu)
        {
            lock(publicInterrupts)
            {
                // we can reset priority even if there are waiting active IRQs
                runningPriorities[cpu] = 0xFF;
                if(number < 32)
                {
                    privateInterrupts[cpu][number] &= ~IRQState.Active;
                }
                else
                {
                    publicInterrupts[number - 32] &= ~IRQState.Active;
                }
                this.NoisyLog("Completed IRQ {0}.", number);
                Update(cpu);
            }
        }

        private void Update(int cpu)
        {
            byte bestPriority = 0xFF;
            var isPending = ScanInterrupts(privateInterrupts[cpu], privatePriorities[cpu], ref bestPriority, cpu) != SpuriousInterrupt ||
                ScanInterrupts(publicInterrupts, publicPriorities, ref bestPriority, cpu) != SpuriousInterrupt;
            Connections[cpu].Set(isPending);
        }

        private uint HandleDistributorClearOrSetEnableReadOrActiveRead(long offset, bool readEnableFalseMeansActive)
        {
            lock(publicInterrupts)
            {
                int interruptStart, interruptEnd;
                bool isInternal;
                var interrupts = GetInterruptArrayAndIndices(offset, out interruptStart, out interruptEnd, 8, out isInternal);
                var mask = 1u;
                var returnValue = 0u;
                var stateToCheck = readEnableFalseMeansActive ? IRQState.Enabled : IRQState.Active;
                for(var i = interruptStart; i < interruptEnd; i++)
                {
                    if((interrupts[i] & stateToCheck) != 0)
                    {
                        returnValue |= mask;
                    }
                    mask <<= 1;
                }
                return returnValue;
            }
        }

        private void HandleDistributorClearOrSetEnableWrite(long offset, uint value, bool set)
        {
            lock(publicInterrupts)
            {
                int interruptStart, interruptEnd;
                bool internalInterrupt;
                var interrupts = GetInterruptArrayAndIndices(offset, out interruptStart, out interruptEnd, 8, out internalInterrupt);
                var mask = 1u;
                var internalString = GetIsInternalString(internalInterrupt);
                for(var i = interruptStart; i < interruptEnd; i++)
                {
                    if((value & mask) != 0)
                    {
                        if(set)
                        {
                            interrupts[i] |= IRQState.Enabled;
                            this.DebugLog("Enabled IRQ {0} ({1}).", i, internalString);
                        }
                        else
                        {
                            interrupts[i] &= ~IRQState.Enabled;
                            this.DebugLog("Disabled IRQ {0} ({1}).", i, internalString);
                        }
                    }
                    mask <<= 1;
                }
                UpdateAll();
            }
        }

        private uint HandleDistributorPriorityRead(long offset)
        {
            lock(publicInterrupts)
            {
                int interruptStart, interruptEnd;
                var priorities = GetPriorityArrayAndIndices(offset, out interruptStart, out interruptEnd);
                var returnValue = 0u;
                for(var i = interruptEnd - 1; i > interruptStart; i--)
                {
                    returnValue |= priorities[i];
                    returnValue <<= 8;
                }
                returnValue |= priorities[interruptStart];
                return returnValue;
            }
        }

        private void HandleDistributorPriorityWrite(long offset, uint value)
        {
            lock(publicInterrupts)
            {
                int interruptStart, interruptEnd;
                var priorities = GetPriorityArrayAndIndices(offset, out interruptStart, out interruptEnd);
                for(var i = interruptStart; i < interruptEnd; i++)
                {
                    priorities[i] = (byte)value;
                    value >>= 8;
                }
                UpdateAll();
            }
        }

        private byte[] GetPriorityArrayAndIndices(long offset, out int interruptStart, out int interruptEnd)
        {
            byte[] priorities;
            interruptStart = (int)offset;
            if(offset < 0x20)
            {
                priorities = privatePriorities[GetAskingCpu()];
            }
            else
            {
                priorities = publicPriorities;
                interruptStart -= 32;
            }
            interruptEnd = interruptStart + 4;
            return priorities;
        }

        private uint HandleDistributorProcessorTargetRead(long offset)
        {
            lock(publicInterrupts)
            {
                if(offset < 0x20)
                {
                    // return current cpu
                    var currentCpuMask = 1u << GetAskingCpu();
                    currentCpuMask |= currentCpuMask << 8;
                    currentCpuMask |= currentCpuMask << 16;
                    return currentCpuMask;
                }
                var interruptStart = (int)offset - 32;
                var interruptEnd = interruptStart + 4;
                var returnValue = 0u;
                const uint mask = 0xFFU/*CK*/;
                for(var i = interruptEnd - 1; i > interruptStart; i--)
                {
                    returnValue |= ((uint)publicInterrupts[i] & mask);
                    returnValue <<= 8;
                }
                returnValue |= ((uint)publicInterrupts[interruptStart] & mask);
                return returnValue;
            }
        }

        private void HandleDistributorProcessorTargetWrite(long offset, uint value)
        {
            lock(publicInterrupts)
            {
                if(offset < 0x20)
                {
                    // read only, ignore it
                    return;
                }
                var interruptStart = (int)offset - 32; // -32, because they are public
                var interruptEnd = interruptStart + 4;
                for(var i = interruptStart; i < interruptEnd; i++)
                {
                    publicInterrupts[i] &= (IRQState)~0xFF; // zero all target bits
                    var mask = value & 0xFF;
                    publicInterrupts[i] |= (IRQState)(mask);
                    if(mask == 0)
                    {
                        this.DebugLog("IRQ {0}: target set to no CPUs.", i);
                        if((publicInterrupts[i] & IRQState.Enabled) != 0)
                        {
                            this.Log(LogLevel.Warning, "IRQ {0}: target set to no CPUs while the interrupt is enabled.", i);
                        }
                    }
                    else
                    {
                        this.DebugLog("IRQ {0}: target set to CPUs: {1}.", i, ToWhichCPU(mask));
                    }
                    value >>= 8;
                }
                UpdateAll();
            }
        }

        private uint HandleDistributorConfigurationRead(long offset)
        {
            lock(publicInterrupts)
            {
                if(offset == 0)
                {
                    return uint.MaxValue; // Not programmable, RAO/WI
                }
                int interruptStart, interruptEnd;
                bool isInternal;
                var interrupts = GetInterruptArrayAndIndices(offset, out interruptStart, out interruptEnd, 4, out isInternal);
                var returnValue = 0u;
                var currentMask = 1u;
                for(var i = interruptStart; i < interruptEnd; i++)
                {
                    if((interrupts[i] & IRQState.EdgeTriggered) != 0)
                    {
                        returnValue |= (currentMask << 1);
                    }
                    currentMask <<= 2;
                }
                return returnValue;
            }
        }

        private void HandleDistributorConfigurationWrite(long offset, uint value)
        {
            lock(publicInterrupts)
            {
                if(offset == 0)
                {
                    // For SGIs, Int_config fields are read-only, meaning that ICDICFR0 is read-only
                    return;
                }
                int interruptStart, interruptEnd;
                bool isInternal;
                var interrupts = GetInterruptArrayAndIndices(offset, out interruptStart, out interruptEnd, 4, out isInternal);
                var localOffset = 0;
                var internalString = GetIsInternalString(isInternal);
                for(var i = interruptStart; i < interruptEnd; i++, localOffset++)
                {
                    if((value & (1 << (2 * localOffset) + 1)) != 0)
                    {
                        interrupts[i] |= IRQState.EdgeTriggered;
                        this.DebugLog("IRQ {0} {1} set as edge triggered.", i, internalString);
                    }
                    else
                    {
                        interrupts[i] &= ~IRQState.EdgeTriggered;
                        this.DebugLog("IRQ {0} {1} set as level sensitive.", i, internalString);
                    }
                }
                UpdateAll();
            }
        }

        private IRQState[] GetInterruptArrayAndIndices(long offset, out int interruptStart, out int interruptEnd, int registersPerByte, out bool isInternal)
        {
            IRQState[] interrupts;
            interruptStart = (int)offset*registersPerByte;
            isInternal = false;
            if(offset < 32/registersPerByte)
            {
                interrupts = privateInterrupts[GetAskingCpu()];
                isInternal = true;
            }
            else
            {
                interrupts = publicInterrupts;
                interruptStart -= 32;
            }
            interruptEnd = interruptStart + 4*registersPerByte;
            return interrupts;
        }

        private void SetRunningInterrupt(ref IRQState state, bool isRunning)
        {
            if(isRunning)
            {
                state |= IRQState.Pending;
            }
            var levelTriggered = (state & IRQState.EdgeTriggered) == 0;
            if(levelTriggered && !isRunning)
            {
                state &= ~IRQState.Pending;
            }
        }

        private void UpdateAll()
        {
            for(var i = 0; i < numberOfCPUs; i++)
            {
                Update(i);
            }
        }

        private string GetIsInternalString(bool isInternal)
        {
            return isInternal ? "internal, private" : "external, public";
        }

        private int GetAskingCpu()
        {
            return 0;
        }

        private static string ToWhichCPU(uint mask)
        {
            return Enumerable.Range(0, 8).Where(x => ((1 << x) & mask) != 0).Select(x => x.ToString()).Aggregate((x, y) => x + ", " + y);
        }

        private byte[] runningPriorities;
        private byte[] priorityMasks;
        private bool[] enabled;
        private bool globallyEnabled;
        private readonly IRQState[] publicInterrupts;
        private readonly IRQState[][] privateInterrupts;
        private readonly byte[][] privatePriorities;
        private readonly byte[] publicPriorities;
        private readonly int numberOfCPUs;
        private readonly int itLinesNumber;
        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }
        private readonly LocalGPIOReceiver[] localReceivers;

        private const int SpuriousInterrupt = 1023;

        private enum DistributorOffset
        {
            ControlRegister = 0, // ICDDCR
            InterruptControllerType = 4 // ICDICTR
        }

        private const int DistributorSetEnableStart = 0x100;
        private const int DistributorSetEnableEnd = 0x120;
        private const int DistributorClearEnableStart = 0x180;
        private const int DistributorClearEnableEnd = 0x200;
        private const int DistributorPriorityStart = 0x400;
        private const int DistributorPriorityEnd = DistributorProcessorTargetStart;
        private const int DistributorProcessorTargetStart = 0x800;
        private const int DistributorProcessorTargetEnd = DistributorInterruptConfigurationStart;
        private const int DistributorInterruptConfigurationStart = 0xC00;
        private const int DistributorInterruptConfigurationEnd = 0xD00;
        private const int DistributorActiveBitStart = 0x300;
        private const int DistributorActiveBitEnd = 0x380;

        private enum CPUInterfaceOffset
        {
            Control = 0x0000, // ICCICR
            InterruptPriorityMask = 0x04, // ICCPMR
            InterruptAcknowledge = 0x0C, // ICCIAR
            EndOfInterrupt = 0x10, // ICCEOIR
            RunningPriority = 0x14, // ICCRPR
        }

        private class LocalGPIOReceiver : IGPIOReceiver
        {
            public LocalGPIOReceiver(int cpuIndex, GIC parent)
            {
                this.cpuIndex = cpuIndex;
                this.parent = parent;
            }

            public void OnGPIO(int number, bool value)
            {
                lock(parent.publicInterrupts)
                {
                    parent.NoisyLog("Internal (private) IRQ {0} (CPU {2}); value: {1}", number, value, cpuIndex);
                    parent.SetRunningInterrupt(ref parent.privateInterrupts[cpuIndex][number], value);
                    parent.Update(cpuIndex);
                }
            }

            public void Reset()
            {
            }

            private readonly int cpuIndex;
            private readonly GIC parent;
        }

        [Flags]
        private enum IRQState
        {
            // first eight bits are reserved for the per-cpu enable
            Pending = 1 << 9,
            EdgeTriggered = 1 << 10,
            Enabled = 1 << 11,
            Active = 1 << 12
        }
    }
}

