//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class IOAPIC: IDoubleWordPeripheral, IIRQController, IKnownSize, INumberedGPIOOutput
    {
        public IOAPIC(LAPIC lapic)
        {
            this.lapic = lapic;
            var irqs = new Dictionary<int, IGPIO>();
            mask = new bool[MaxRedirectionTableEntries];
            internalLock = new object();
            for(int i = 0; i < NumberOfOutgoingInterrupts; i++)
            {
                irqs[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(irqs);
            externalIrqToVectorMapping = new Dictionary<int, int>();
            Reset();
        }

        public void OnGPIO(int number, bool value)
        {
            lock(internalLock)
            {
                if(number < 0 || number > MaxRedirectionTableEntries)
                {
                    throw new ArgumentOutOfRangeException(string.Format("IOAPIC has {0} interrupts, but {1} was triggered", MaxRedirectionTableEntries, number));
                }

                if(!mask[number])
                {
                    Connections[externalIrqToVectorMapping[number]].Set(value);
                }
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(internalLock)
            {
                if(offset == (long)Registers.Index)
                {
                    return lastIndex;
                }
                if(offset == (long)Registers.Data)
                {
                    if(lastIndex >= (uint)IndirectRegisters.IoRedirectionTable0 && lastIndex <= (uint)IndirectRegisters.IoRedirectionTable23 + 1)
                    {
                        if(lastIndex % 2 == 0)
                        {
                            var tableIndex = (int)((lastIndex - (uint)IndirectRegisters.IoRedirectionTable0) / 2);
                            return (uint)((mask[tableIndex] ? MaskedBitMask : 0x0) | externalIrqToVectorMapping[tableIndex]);
                        }
                        return 0;
                    }
                }
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(internalLock)
            {
                switch((Registers)offset)
                {
                    case Registers.Index:
                        lastIndex = value;
                        break;
                    case Registers.Data:
                        if(lastIndex >= (uint)IndirectRegisters.IoRedirectionTable0 && lastIndex <= (uint)IndirectRegisters.IoRedirectionTable23 + 1)
                        {
                            var tableIndex = (int)((lastIndex - (uint)IndirectRegisters.IoRedirectionTable0) / 2);
                            if (lastIndex % 2 != 0)
                            {
                                // high bits
                                this.Log(LogLevel.Noisy, "Write to high bits of {0} table index: {0}. It contains physical/logical destination address (APIC ID or set o processors) that is not supported right now.", tableIndex, value);
                            }
                            else
                            {
                                // low bits
                                externalIrqToVectorMapping[tableIndex] = (int)(value & 0xFF);
                                mask[tableIndex] = (value & MaskedBitMask) != 0;

                                this.Log(LogLevel.Info, "Setting {0} table index: interrupt vector=0x{1:X}, mask={2}", tableIndex, externalIrqToVectorMapping[tableIndex], mask[tableIndex]);
                            }
                        }
                        break;
                    case Registers.EndOfInterrupt:
                        // value here means irq vector
                        var externalIrqIds = externalIrqToVectorMapping.Where(x => x.Value == (int)value).Select(x => x.Key).ToArray();
                        if(externalIrqIds.Length == 0)
                        {
                            //We filter out lapic internal timer vector. Due to a bug in HW the software clears all interrupts on ioapic, although this one is only handled by lapic.
                            if(value != lapic.InternalTimerVector)
                            {
                                this.Log(LogLevel.Warning, "Calling end of interrupt on unmapped vector: {0}", value);
                            }
                            return;
                        }

                        foreach(var id in externalIrqIds.Where(x => Connections[x].IsSet))
                        {
                            Connections[id].Unset();
                            this.Log(LogLevel.Debug, "Ending interrupt #{0} (vector 0x{1:X})", id, value);
                        }
                        break;
                    default:
                        this.LogUnhandledWrite(offset, value);
                        break;
                }
            }
        }

        public void Reset()
        {
            lock(internalLock)
            {
                for(int i = 0; i < Connections.Count; i++)
                {
                    Connections[i].Unset();
                }
                for(int i = 0; i < mask.Length; i++)
                {
                    mask[i] = true;
                }
                externalIrqToVectorMapping.Clear();
                lastIndex = 0;
            }
        }

        public long Size
        {
            get
            {
                return 1.MB();
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }

        private bool[] mask;
        private uint lastIndex;
        private object internalLock;
        private readonly Dictionary<int, int> externalIrqToVectorMapping;
        private readonly LAPIC lapic;

        private const int MaxRedirectionTableEntries = 24;
        private const int NumberOfOutgoingInterrupts = 256;
        private const int MaskedBitMask = 0x10000;

        public enum Registers
        {
            Index = 0x0,
            Data = 0x10,
            IRQPinAssertion = 0x20,
            EndOfInterrupt = 0x40
        }

        public enum IndirectRegisters
        {
            IoRedirectionTable0 = 0x10,
            IoRedirectionTable23 = 0x3E
        }
    }
}
