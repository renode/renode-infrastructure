//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging.Profiling;

namespace Antmicro.Renode.Utilities.BitObserver
{
    public sealed class BitObserver
    {
        public BitObserver(Machine machine)
        {
            observedBits = new Dictionary<string, BitHookEntry>();		
            this.machine = machine;
        }

        public void ObserveBit(string entryName, IPeripheral peripheral, long offset, byte bit, bool enabled = false)
        {
            if(observedBits.ContainsKey(entryName))
            {
                throw new RecoverableException($"There is an observed bit registered under the name \"{entryName}\"");
                return;
            }
            observedBits.Add(entryName, new BitHookEntry(peripheral, offset, bit, enabled));
            if(peripheral is IProvidesRegisterCollection<QuadWordRegisterCollection> quadWordPeripheral)
            {
                UpdateHooks(quadWordPeripheral, offset);
            }
            else if(peripheral is IProvidesRegisterCollection<DoubleWordRegisterCollection> doubleWordPeripheral)
            {
                UpdateHooks(doubleWordPeripheral, offset);
            }
            else if(peripheral is IProvidesRegisterCollection<WordRegisterCollection> wordPeripheral)
            {
                UpdateHooks(wordPeripheral, offset);
            }
            else if(peripheral is IProvidesRegisterCollection<ByteRegisterCollection> bytePeripheral)
            {
                UpdateHooks(bytePeripheral, offset);
            }
            else
            {
                throw new RecoverableException("Only bits in peripherals that implement IProvidesRegisterCollection<T> can be observed");
                return;
            }
        }

        public void StopObservingBit(string entryName)
        {
            if(!observedBits.TryGetValue(entryName, out var entry))
            {
                throw new RecoverableException($"There is no observed bit registered under the name \"{entryName}\"");
                return;
            }
            var peripheral = entry.Peripheral; 
            var offset = entry.Offset;
            observedBits.Remove(entryName);
            if(peripheral is IProvidesRegisterCollection<QuadWordRegisterCollection> quadWordPeripheral)
            {
                UpdateHooks(quadWordPeripheral, offset);
            }
            if(peripheral is IProvidesRegisterCollection<DoubleWordRegisterCollection> doubleWordPeripheral)
            {
                UpdateHooks(doubleWordPeripheral, offset);
            }
            if(peripheral is IProvidesRegisterCollection<WordRegisterCollection> wordPeripheral)
            {
                UpdateHooks(wordPeripheral, offset);
            }
            if(peripheral is IProvidesRegisterCollection<ByteRegisterCollection> bytePeripheral)
            {
                UpdateHooks(bytePeripheral, offset);
            }
        }

        public bool GetObservedBitState(string entryName)
        {
            if(!observedBits.TryGetValue(entryName, out var entry))
            {
                throw new RecoverableException($"No bit with the name \"{entryName}\"");
            }
            return entry.Enabled;
        }

        public string[,] GetAllObservedBitsStates()
        {
            var table = new Table().AddRow("Name", "Peripheral", "Offset", "Bit Index", "Enabled");
            table.AddRows(observedBits,
                    x => x.Key,
                    x => x.Value.Peripheral.GetName(),
                    x => "0x" + x.Value.Offset.ToString("X2"),
                    x => x.Value.Bit.ToString(),
                    x => x.Value.Enabled.ToString());
            return table.ToArray();
        }

        public List<string> GetAllObservedBitsNames()
        {
            return observedBits.Keys.ToList();
        }

        private void UpdateHooks<T>(IProvidesRegisterCollection<T> peripheral, long offset) where T : IRegisterCollection
        {
            if(peripheral.HasAfterWriteHook(offset))
            {
                peripheral.RemoveAfterWriteHook(offset);
            }

            peripheral.AddAfterWriteHook<uint, T>(offset, (addr, value) => 
            {
                var keys = observedBits.Where(elem => elem.Value.Offset == addr).Select(elem => elem.Key);
                var bitNamesList = observedBits.Keys.ToList();
                foreach(var key in keys.ToList())
                {
                    var entry = observedBits[key];
                    var newEnabled = (value & (1 << entry.Bit)) != 0;
                    if(newEnabled != entry.Enabled)
                    {
                        var index = bitNamesList.IndexOf(key);
                        machine.Profiler?.Log(new BitObserverEntry(machine, index, newEnabled));
                        entry.Enabled = newEnabled;
                        observedBits[key] = entry;
                    }
                }
            });
        }
        
        private Dictionary<string, BitHookEntry> observedBits; 
        private readonly Machine machine;
        private struct BitHookEntry
        {
            public BitHookEntry(IPeripheral peripheral, long offset, byte bit, bool enabled)
            {
                Peripheral = peripheral;
                Offset = offset;
                Bit = bit;
                Enabled = enabled;
            }

            public IPeripheral Peripheral { get; }
            public long Offset { get; }
            public byte Bit { get; }
            public bool Enabled { get; set; }
        }
    }
}
