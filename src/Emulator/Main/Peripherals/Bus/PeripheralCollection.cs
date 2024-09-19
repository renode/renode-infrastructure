//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using System.Text;

namespace Antmicro.Renode.Peripherals.Bus
{
    partial class SystemBus
    {
        private class PeripheralCollection
        {
            internal PeripheralCollection(SystemBus sysbus)
            {
                this.sysbus = sysbus;
                blocks = new Block[0];
                shortBlocks = new Dictionary<ulong, Block>();
                sync = new object();
                InvalidateLastBlock();
            }

            public IEnumerable<IBusRegistered<IBusPeripheral>> Peripherals
            {
                get
                {
                    lock(sync)
                    {
                        return blocks.Union(shortBlocks.Select(x => x.Value)).Select(x => x.Peripheral).Distinct();
                    }
                }
            }

            public void Add(ulong start, ulong end, IBusRegistered<IBusPeripheral> peripheral, PeripheralAccessMethods accessMethods)
            {
                // the idea here is that we prefer the peripheral to go to dictionary
                // ideally it can go to dicitonary wholly, but we try to put there as much as we can
                lock(sync)
                {
                    var name = string.Format("{0} @ {1}.", peripheral.Peripheral.GetType().Name, peripheral.RegistrationPoint);
                    // TODO: check index (and start/stop)
                    var block = new Block { Start = start, End = end, AccessMethods = accessMethods, Peripheral = peripheral };
                    // let's decide whether block should go to array, dictionary or both
                    var goToDictionary = true;
                    // is the peripheral properly aligned?
                    if((start & PageAlign) != 0)
                    {
                        sysbus.NoisyLog("{0} is at not aligned address - not using dictionary.", name);
                        goToDictionary = false;
                    }
                    // is the peripheral small enough?
                    var size = end - start;
                    var numOfPages = size/PageSize;
                    if(numOfPages > NumOfPagesThreshold)
                    {
                        sysbus.NoisyLog("{0} is too large - not using dictionary.", name);
                        goToDictionary = false;
                    }
                    var goToArray = !goToDictionary; // peripheral will go to array if we couldn't put it in dictionary
                    if(goToDictionary && size % PageSize != 0)
                    {
                        // but it should also go to array if it isn't properly aligned on its last page
                        goToArray = true;
                    }
                    if(goToArray)
                    {
                        blocks = blocks.Union(new [] { block }).OrderBy(x => x.Start).ToArray();
                        sysbus.NoisyLog("Added {0} to binary search array.", name);
                    }
                    if(!goToDictionary)
                    {
                        return;
                    }
                    // note that truncating is in fact good thing here
                    for(var i = 0u; i < numOfPages; i++)
                    {
                        shortBlocks.Add(start + i * PageSize, block);
                    }
                    sysbus.NoisyLog("Added {0} to dictionary.", name);
                }
            }

            public void Move(IBusRegistered<IBusPeripheral> registeredPeripheral, BusRangeRegistration newRegistration)
            {
                var newRegisteredPeripheral = new BusRegistered<IBusPeripheral>(registeredPeripheral.Peripheral, newRegistration);
                lock(sync)
                {
                    var block = blocks.FirstOrDefault(x => x.Peripheral == registeredPeripheral);
                    if(block.Peripheral == registeredPeripheral)
                    {
                        blocks = blocks.Where(x => x.Peripheral != registeredPeripheral).ToArray();
                    }
                    else
                    {
                        block = shortBlocks.Values.FirstOrDefault(x => x.Peripheral == registeredPeripheral);
                        if(block.Peripheral != registeredPeripheral)
                        {
                            throw new RecoverableException("Attempted to move a peripheral that does not exist in the collection");
                        }
                        var toRemove = shortBlocks.Where(x => x.Value.Peripheral != registeredPeripheral).Select(x => x.Key).ToArray();
                        foreach(var keyToRemove in toRemove)
                        {
                            shortBlocks.Remove(keyToRemove);
                        }
                    }
                    InvalidateLastBlock();

                    var newStart = newRegistration.StartingPoint;
                    var size = newRegistration.Range.Size;
                    Add(newStart, newStart + size, newRegisteredPeripheral, block.AccessMethods);
                }
            }

            public void Remove(IPeripheral peripheral)
            {
                lock(sync)
                {
                    // list is scanned first
                    blocks = blocks.Where(x => x.Peripheral.Peripheral != peripheral).ToArray();
                    // then dictionary
                    var toRemove = shortBlocks.Where(x => x.Value.Peripheral.Peripheral == peripheral).Select(x => x.Key).ToArray();
                    foreach(var keyToRemove in toRemove)
                    {
                        shortBlocks.Remove(keyToRemove);
                    }
                    InvalidateLastBlock();
                }
            }

            public void Remove(ulong start, ulong end)
            {
                lock(sync)
                {
                    blocks = blocks.Where(x => x.Start > end || x.End < start).ToArray();
                    var toRemove = shortBlocks.Where(x => x.Value.Start >= start && x.Value.End <= end).Select(x => x.Key).ToArray();
                    foreach(var keyToRemove in toRemove)
                    {
                        shortBlocks.Remove(keyToRemove);
                    }
                    InvalidateLastBlock();
                }
            }

            public void VisitAccessMethods(IBusPeripheral peripheral, Func<PeripheralAccessMethods, PeripheralAccessMethods> onPam)
            {
                lock(sync)
                {
                    blocks = blocks.Select(block =>
                    {
                        if(peripheral != null && block.Peripheral.Peripheral != peripheral)
                        {
                            return block;
                        }
                        block.AccessMethods = onPam(block.AccessMethods);
                        return block;
                    }).ToArray();
                    shortBlocks = shortBlocks.Select(dEntry =>
                    {
                        if(peripheral != null && dEntry.Value.Peripheral.Peripheral != peripheral)
                        {
                            return dEntry;
                        }
                        var block = dEntry.Value;
                        block.AccessMethods = onPam(block.AccessMethods);
                        return new KeyValuePair<ulong, Block>(dEntry.Key, block);
                    }).ToDictionary(x => x.Key, x => x.Value);
                    InvalidateLastBlock();
                }
            }

            public PeripheralAccessMethods FindAccessMethods(ulong address, out ulong startAddress, out ulong endAddress)
            {
                // no need to lock here yet, cause last block is in the thread local storage
                var lastBlock = lastBlockStorage.Value;
#if DEBUG
                Interlocked.Increment(ref queryCount);
#endif
                if (address >= lastBlock.Start && address < lastBlock.End)
                {
#if DEBUG
                    Interlocked.Increment(ref lastPeripheralCount);
#endif
                    startAddress = lastBlock.Start;
                    endAddress = lastBlock.End;
                    return lastBlock.AccessMethods;
                }
                lock(sync)
                {
                    // let's try dictionary
                    Block block;
                    if(!shortBlocks.TryGetValue(address & ~PageAlign, out block))
                    {
                        // binary search - our last resort
                        var index = BinarySearch(address);
                        if(index == -1)
                        {
                            startAddress = 0;
                            endAddress = 0;
                            return null;
                        }
#if DEBUG
                        Interlocked.Increment(ref binarySearchCount);
#endif
                        block = blocks[index];
                    }
#if DEBUG
                    else
                    {
                        Interlocked.Increment(ref dictionaryCount);
                    }
#endif
                    startAddress = block.Start;
                    endAddress = block.End;
                    lastBlockStorage.Value = block;
                    return block.AccessMethods;
                }
            }

#if DEBUG
            public void ShowStatistics()
            {
                var misses = queryCount - lastPeripheralCount - dictionaryCount - binarySearchCount;
                var line = new StringBuilder("\n  Memory queries statistics are as follows:");
                if(queryCount > 0)
                {
                    line.AppendFormat("\tConsecutive hits:   {0:00.00} ({1})\n", 100.0 * lastPeripheralCount / queryCount, lastPeripheralCount)
                        .AppendFormat("\tDictionary hits:    {0:00.00} ({1})\n", 100.0 * dictionaryCount / queryCount, dictionaryCount)
                        .AppendFormat("\tBinary search:      {0:00.00} ({1})\n", 100.0 * binarySearchCount / queryCount, binarySearchCount)
                        .AppendFormat("\tMisses:             {0:00.00} ({1})", 100.0 * misses / queryCount, misses);
                }
                else
                {
                    line.AppendLine("\tNo queries");
                }
                sysbus.DebugLog(line.ToString());
            }
#endif

            private int BinarySearch(ulong offset)
            {
                var min = 0;
                var max = blocks.Length - 1;
                if(blocks.Length == 0)
                {
                    return -1;
                }
                do
                {
                    var current = (min + max) / 2;
                    if (offset >= blocks[current].End)
                    {
                        min = current + 1;
                    }
                    else if (offset < blocks[current].Start)
                    {
                        max = current - 1;
                    }
                    else
                    {
                        return current;
                    }
                }
                while(min <= max);
                return -1;
            }

            private void InvalidateLastBlock()
            {
                lastBlockStorage = new ThreadLocal<Block>();
            }

            private Dictionary<ulong, Block> shortBlocks;
            private Block[] blocks;
            [Constructor]
            private ThreadLocal<Block> lastBlockStorage;
            private object sync;
            private readonly SystemBus sysbus;

#if DEBUG
            private long queryCount;
            private long lastPeripheralCount;
            private long dictionaryCount;
            private long binarySearchCount;
#endif

            private const ulong PageSize = 1 << 11;
            private const ulong PageAlign = PageSize - 1;
            private const long NumOfPagesThreshold = 4;

            private struct Block
            {
                public ulong Start;
                public ulong End;
                public PeripheralAccessMethods AccessMethods;
                public IBusRegistered<IBusPeripheral> Peripheral;
            }
        }
    }
}

