//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    // MPC56xx single-core INTC software-vector subset used by MPC5645S.
    // Hardware vector mode and multi-core routing are intentionally unsupported.
    public sealed class MPC56xx_INTC : IIRQController, IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral
    {
        public MPC56xx_INTC(int numberOfSources = DefaultNumberOfSources)
        {
            if(numberOfSources <= 0 || numberOfSources > MaximumNumberOfSources)
            {
                throw new ArgumentOutOfRangeException(nameof(numberOfSources));
            }

            this.numberOfSources = numberOfSources;
            inputLevels = new bool[numberOfSources];
            softwarePending = new bool[numberOfSources];
            priorities = new byte[numberOfSources];
            sourceAcknowledgeCounts = new ulong[numberOfSources];
            priorityStack = new List<byte>(MaximumNestingDepth);
            IRQ = new GPIO();
            Reset();
        }

        public ulong GetAcknowledgeCount(int source)
        {
            lock(sync)
            {
                if(source < 0 || source >= numberOfSources)
                {
                    return 0;
                }
                return sourceAcknowledgeCounts[source];
            }
        }

        public void OnGPIO(int number, bool value)
        {
            lock(sync)
            {
                if(number < 0 || number >= numberOfSources)
                {
                    this.Log(LogLevel.Warning, "Ignoring interrupt source {0}; valid range is 0..{1}", number, numberOfSources - 1);
                    return;
                }

                inputLevels[number] = value;
                UpdateInterrupt(preserveLatchedRequest: true);
            }
        }

        public void Reset()
        {
            lock(sync)
            {
                Array.Clear(inputLevels, 0, inputLevels.Length);
                Array.Clear(softwarePending, 0, softwarePending.Length);
                Array.Clear(priorities, 0, priorities.Length);
                Array.Clear(sourceAcknowledgeCounts, 0, sourceAcknowledgeCounts.Length);
                priorityStack.Clear();
                vectorTableBase = 0;
                currentPriority = ResetCurrentPriority;
                vectorTableEntrySize8 = false;
                latchedSource = null;
                AcknowledgeCount = 0;
                EndOfInterruptCount = 0;
                LastAcknowledgedSource = -1;
                IRQ.Unset();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadSized(offset, 4);
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)ReadSized(offset, 2);
        }

        public byte ReadByte(long offset)
        {
            return (byte)ReadSized(offset, 1);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteSized(offset, 4, value);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteSized(offset, 2, value);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteSized(offset, 1, value);
        }

        public GPIO IRQ { get; private set; }

        public long Size => 0x4000;

        public int NumberOfSources => numberOfSources;

        public uint VectorTableBase => vectorTableBase;

        public byte CurrentPriority => currentPriority;

        public int NestingDepth => priorityStack.Count;

        public ulong AcknowledgeCount { get; private set; }

        public ulong EndOfInterruptCount { get; private set; }

        public int LastAcknowledgedSource { get; private set; }

        public bool HardwareVectorModeSupported => false;

        private static uint Merge(uint previous, uint value, uint mask)
        {
            return (previous & ~mask) | (value & mask);
        }

        private uint ReadSized(long offset, int size)
        {
            lock(sync)
            {
                var lane = (int)(offset & 0x3L);
                if((size != 1 && size != 2 && size != 4) || lane + size > 4 || (size == 4 && lane != 0))
                {
                    this.Log(LogLevel.Warning, "Unsupported INTC read size/alignment: offset 0x{0:X}, size {1}", offset, size);
                    return 0;
                }

                var alignedOffset = offset & ~0x3L;
                var registerValue = ReadRegister(alignedOffset);
                var bitShift = (4 - lane - size) * 8;
                var mask = size == 1 ? 0xFFu : size == 2 ? 0xFFFFu : uint.MaxValue;
                return (registerValue >> bitShift) & mask;
            }
        }

        private void WriteSized(long offset, int size, uint value)
        {
            lock(sync)
            {
                var lane = (int)(offset & 0x3L);
                if((size != 1 && size != 2 && size != 4) || lane + size > 4 || (size == 4 && lane != 0))
                {
                    this.Log(LogLevel.Warning, "Unsupported INTC write size/alignment: offset 0x{0:X}, size {1}", offset, size);
                    return;
                }

                var alignedOffset = offset & ~0x3L;
                var bitShift = (4 - lane - size) * 8;
                var valueMask = size == 1 ? 0xFFu : size == 2 ? 0xFFFFu : uint.MaxValue;
                var mask = valueMask << bitShift;
                WriteRegister(alignedOffset, (value & valueMask) << bitShift, mask);
            }
        }

        private uint ReadRegister(long offset)
        {
            switch(offset)
            {
            case ModuleConfigurationOffset:
                return vectorTableEntrySize8 ? VectorTableEntrySizeMask : 0u;
            case CurrentPriorityOffset:
                return currentPriority;
            case InterruptAcknowledgeOffset:
                return AcknowledgeInterrupt();
            case EndOfInterruptOffset:
                return 0;
            }

            if(offset >= SoftwareSetClearBaseOffset && offset < SoftwareSetClearEndOffset)
            {
                uint result = 0;
                for(var lane = 0; lane < 4; lane++)
                {
                    var source = (int)(offset - SoftwareSetClearBaseOffset) + lane;
                    var byteValue = source < SoftwareInterruptCount && softwarePending[source] ? 1u : 0u;
                    result |= byteValue << ((3 - lane) * 8);
                }
                return result;
            }

            if(offset >= PriorityBaseOffset && offset < PriorityBaseOffset + numberOfSources)
            {
                uint result = 0;
                for(var lane = 0; lane < 4; lane++)
                {
                    var source = (int)(offset - PriorityBaseOffset) + lane;
                    if(source < numberOfSources)
                    {
                        result |= (uint)priorities[source] << ((3 - lane) * 8);
                    }
                }
                return result;
            }

            return 0;
        }

        private void WriteRegister(long offset, uint value, uint mask)
        {
            switch(offset)
            {
            case ModuleConfigurationOffset:
                WriteModuleConfiguration(value, mask);
                return;
            case CurrentPriorityOffset:
                currentPriority = (byte)(Merge(currentPriority, value, mask) & PriorityMask);
                UpdateInterrupt(preserveLatchedRequest: false);
                return;
            case InterruptAcknowledgeOffset:
                var mergedBase = Merge(vectorTableBase, value, mask);
                vectorTableBase = mergedBase & VectorTableBaseMask;
                return;
            case EndOfInterruptOffset:
                EndOfInterrupt();
                return;
            }

            if(offset >= SoftwareSetClearBaseOffset && offset < SoftwareSetClearEndOffset)
            {
                for(var lane = 0; lane < 4; lane++)
                {
                    var laneMask = 0xFFu << ((3 - lane) * 8);
                    if((mask & laneMask) == 0)
                    {
                        continue;
                    }
                    var source = (int)(offset - SoftwareSetClearBaseOffset) + lane;
                    if(source < SoftwareInterruptCount)
                    {
                        WriteSoftwareSetClear(source, (byte)(value >> ((3 - lane) * 8)));
                    }
                }
                UpdateInterrupt(preserveLatchedRequest: true);
                return;
            }

            if(offset >= PriorityBaseOffset && offset < PriorityBaseOffset + numberOfSources)
            {
                for(var lane = 0; lane < 4; lane++)
                {
                    var laneMask = 0xFFu << ((3 - lane) * 8);
                    if((mask & laneMask) == 0)
                    {
                        continue;
                    }
                    var source = (int)(offset - PriorityBaseOffset) + lane;
                    if(source < numberOfSources)
                    {
                        priorities[source] = (byte)((value >> ((3 - lane) * 8)) & PriorityMask);
                    }
                }
                UpdateInterrupt(preserveLatchedRequest: false);
            }
        }

        private void WriteModuleConfiguration(uint value, uint mask)
        {
            var current = vectorTableEntrySize8 ? VectorTableEntrySizeMask : 0u;
            var merged = Merge(current, value, mask);
            if((merged & HardwareVectorEnableMask) != 0)
            {
                this.Log(LogLevel.Warning, "MPC56xx INTC hardware vector mode is unsupported; HVEN remains clear");
            }
            vectorTableEntrySize8 = (merged & VectorTableEntrySizeMask) != 0;
            vectorTableBase &= VectorTableBaseMask;
        }

        private void WriteSoftwareSetClear(int source, byte value)
        {
            var set = (value & SoftwareSetMask) != 0;
            var clear = (value & SoftwareClearMask) != 0;
            if(set == clear)
            {
                return;
            }
            softwarePending[source] = set;
        }

        private uint AcknowledgeInterrupt()
        {
            if(!latchedSource.HasValue)
            {
                UpdateInterrupt(preserveLatchedRequest: false);
            }
            if(!latchedSource.HasValue)
            {
                return vectorTableBase;
            }

            var source = latchedSource.Value;
            PushPriority(currentPriority);
            currentPriority = priorities[source];
            latchedSource = null;
            AcknowledgeCount++;
            sourceAcknowledgeCounts[source]++;
            LastAcknowledgedSource = source;
            IRQ.Unset();
            UpdateInterrupt(preserveLatchedRequest: false);
            return vectorTableBase | ((uint)source * VectorTableEntrySize);
        }

        private void EndOfInterrupt()
        {
            if(priorityStack.Count > 0)
            {
                var last = priorityStack.Count - 1;
                currentPriority = priorityStack[last];
                priorityStack.RemoveAt(last);
            }
            else
            {
                currentPriority = 0;
                this.Log(LogLevel.Warning, "EOIR written with an empty priority stack; CPR restored to 0");
            }
            EndOfInterruptCount++;
            UpdateInterrupt(preserveLatchedRequest: false);
        }

        private void PushPriority(byte priority)
        {
            if(priorityStack.Count == MaximumNestingDepth)
            {
                priorityStack.RemoveAt(0);
                this.Log(LogLevel.Warning, "INTC priority stack overflow; oldest entry overwritten");
            }
            priorityStack.Add(priority);
        }

        private void UpdateInterrupt(bool preserveLatchedRequest)
        {
            if(latchedSource.HasValue && preserveLatchedRequest)
            {
                IRQ.Set();
                return;
            }

            latchedSource = FindBestEligibleSource();
            IRQ.Set(latchedSource.HasValue);
        }

        private int? FindBestEligibleSource()
        {
            var bestSource = -1;
            byte bestPriority = 0;
            for(var source = 0; source < numberOfSources; source++)
            {
                if(!IsPending(source))
                {
                    continue;
                }
                var priority = priorities[source];
                if(priority == 0 || priority <= currentPriority)
                {
                    continue;
                }
                if(bestSource == -1 || priority > bestPriority)
                {
                    bestSource = source;
                    bestPriority = priority;
                }
            }
            return bestSource == -1 ? (int?)null : bestSource;
        }

        private bool IsPending(int source)
        {
            return inputLevels[source] || (source < SoftwareInterruptCount && softwarePending[source]);
        }

        private uint VectorTableEntrySize => vectorTableEntrySize8 ? 8u : 4u;

        private uint VectorTableBaseMask => vectorTableEntrySize8 ? 0xFFFFF000u : 0xFFFFF800u;

        private uint vectorTableBase;
        private byte currentPriority;
        private bool vectorTableEntrySize8;
        private int? latchedSource;

        private readonly object sync = new object();
        private readonly int numberOfSources;
        private readonly bool[] inputLevels;
        private readonly bool[] softwarePending;
        private readonly byte[] priorities;
        private readonly ulong[] sourceAcknowledgeCounts;
        private readonly List<byte> priorityStack;

        private const int DefaultNumberOfSources = 239;
        private const int MaximumNumberOfSources = 512;
        private const int SoftwareInterruptCount = 8;
        private const int MaximumNestingDepth = 14;
        private const byte ResetCurrentPriority = 15;

        private const long ModuleConfigurationOffset = 0x0;
        private const long CurrentPriorityOffset = 0x8;
        private const long InterruptAcknowledgeOffset = 0x10;
        private const long EndOfInterruptOffset = 0x18;
        private const long SoftwareSetClearBaseOffset = 0x20;
        private const long SoftwareSetClearEndOffset = 0x28;
        private const long PriorityBaseOffset = 0x40;

        private const uint VectorTableEntrySizeMask = 0x80000000u;
        private const uint HardwareVectorEnableMask = 0x1u;
        private const uint PriorityMask = 0xFu;
        private const byte SoftwareSetMask = 0x2;
        private const byte SoftwareClearMask = 0x1;
    }
}
