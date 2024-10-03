//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Memory;
using ELFSharp.ELF;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Bus
{
    public interface IBusController: IPeripheralContainer<IBusPeripheral, BusRangeRegistration>, IPeripheralRegister<IKnownSize, BusPointRegistration>,
        IPeripheralRegister<ICPU, CPURegistrationPoint>, IPeripheralRegister<IBusPeripheral, BusMultiRegistration>, IPeripheralRegister<IPeripheral, NullRegistrationPoint>,
        IPeripheralRegister<IBusPeripheral, BusParametrizedRegistration>, ICanLoadFiles, IPeripheral, IMultibyteWritePeripheral
    {
        byte ReadByte(ulong address, ICPU context = null);
        void WriteByte(ulong address, byte value, ICPU context = null);

        ushort ReadWord(ulong address, ICPU context = null);
        void WriteWord(ulong address, ushort value, ICPU context = null);

        uint ReadDoubleWord(ulong address, ICPU context = null);
        void WriteDoubleWord(ulong address, uint value, ICPU context = null);

        ulong ReadQuadWord(ulong address, ICPU context = null);
        void WriteQuadWord(ulong address, ulong value, ICPU context = null);

        void ReadBytes(ulong address, int count, byte[] destination, int startIndex, bool onlyMemory = false, ICPU context = null);
        byte[] ReadBytes(ulong address, int count, bool onlyMemory = false, ICPU context = null);

        void WriteBytes(byte[] bytes, ulong address, bool onlyMemory = false, ICPU context = null);
        void WriteBytes(byte[] bytes, ulong address, int startingIndex, long count, bool onlyMemory = false, ICPU context = null);
        void WriteBytes(byte[] bytes, ulong address, long count, bool onlyMemory = false, ICPU context = null);

        void ZeroRange(Range range, ICPU context = null);

        IBusRegistered<IBusPeripheral> WhatIsAt(ulong address, ICPU context = null);
        IPeripheral WhatPeripheralIsAt(ulong address, ICPU context = null);

        bool IsAddressRangeLocked(Range range, ICPU context = null);
        void SetAddressRangeLocked(Range range, bool locked, ICPU context = null);

        void SetPeripheralEnabled(IPeripheral peripheral, bool value);
        bool IsPeripheralEnabled(IPeripheral peripheral);

        IEnumerable<ICPU> GetCPUs();
        int GetCPUSlot(ICPU cpu);
        ICPU GetCurrentCPU();
        IEnumerable<IBusRegistered<IBusPeripheral>> GetRegisteredPeripherals(ICPU context = null);
        bool TryGetCurrentCPU(out ICPU cpu);

        void UnregisterFromAddress(ulong address, ICPU context = null);
        void MoveRegistrationWithinContext(IBusPeripheral peripheral, BusRangeRegistration newRegistration, ICPU context, Func<IEnumerable<IBusRegistered<IBusPeripheral>>, IBusRegistered<IBusPeripheral>> selector = null);

        void AddWatchpointHook(ulong address, SysbusAccessWidth width, Access access, BusHookDelegate hook);
        void RemoveWatchpointHook(ulong address, BusHookDelegate hook);
        bool TryGetWatchpointsAt(ulong address, Access access, out List<BusHookHandler> result);
        void RemoveAllWatchpointHooks(ulong address);

        void SetHookAfterPeripheralRead<T>(IBusPeripheral peripheral, Func<T, long, T> hook, Range? subrange = null);
        void SetHookBeforePeripheralWrite<T>(IBusPeripheral peripheral, Func<T, long, T> hook, Range? subrange = null);
        void ClearHookAfterPeripheralRead<T>(IBusPeripheral peripheral);

        string FindSymbolAt(ulong offset, ICPU context = null);

        bool TryGetAllSymbolAddresses(string symbolName, out IEnumerable<ulong> symbolAddresses, ICPU context = null);
        bool TryFindSymbolAt(ulong offset, out string name, out Symbol symbol, ICPU context = null);
        string DecorateWithCPUNameAndPC(string str);

        void MapMemory(IMappedSegment segment, IBusPeripheral owner, bool relative = true, ICPUWithMappedMemory context = null);
        IBusRegistered<MappedMemory> FindMemory(ulong address, ICPU context = null);

        void Tag(Range range, string tag, ulong defaultValue = 0, bool pausing = false);

        void ApplySVD(string path);

        void LoadUImage(ReadFilePath fileName, IInitableCPU cpu = null);
        void LoadELF(ReadFilePath fileName, bool useVirtualAddress = false, bool allowLoadsOnlyToMemory = true, ICluster<IInitableCPU> cpu = null);

        SymbolLookup GetLookup(ICPU context = null);

        void EnableAllTranslations(bool enable = true);
        void EnableAllTranslations(IBusPeripheral busPeripheral, bool enable = true);

        IMachine Machine { get; }

        bool IsMultiCore { get; }

        Endianess Endianess { get; }

        event Action<IMachine> OnSymbolsChanged;
    }

    public static class BusControllerExtensions
    {
        public static void EnablePeripheral(this IBusController bus, IPeripheral peripheral)
        {
            bus.SetPeripheralEnabled(peripheral, true);
        }

        public static void DisablePeripheral(this IBusController bus, IPeripheral peripheral)
        {
            bus.SetPeripheralEnabled(peripheral, false);
        }

        public static void MoveBusMultiRegistrationWithinContext(this IBusController bus, IBusPeripheral peripheral, BusMultiRegistration newRegistration, ICPU cpu)
        {
            var regionName = newRegistration.ConnectionRegionName;
            bus.MoveRegistrationWithinContext(peripheral, newRegistration, cpu,
                selector: busRegisteredEnumerable =>
                {
                    return busRegisteredEnumerable.Where(
                            busRegistered => (busRegistered.RegistrationPoint is BusMultiRegistration multiRegistration) && multiRegistration.ConnectionRegionName == regionName
                        ).Single();
                }
            );
        }

        public static void ZeroRange(this IBusController bus, long from, long size, ICPU context = null)
        {
            bus.ZeroRange(from.By(size), context);
        }

        public static ulong GetSymbolAddress(this IBusController bus, string symbolName, int index, ICPU context = null)
        {
            if(!bus.TryGetAllSymbolAddresses(symbolName, out var addressesEnumerable, context))
            {
                throw new RecoverableException($"Could not find any address for symbol: {symbolName}");
            }
            var addresses = addressesEnumerable.ToArray();
            if(index < 0 || index >= addresses.Length)
            {
                var msg = (addresses.Length == 1)
                    ? "there is only one address"
                    : "there are only {addresses.Length} addresses";

                throw new RecoverableException($"Wrong index {index}: {msg} (0-based index) for '{symbolName}'");
            }
            return addresses[index];
        }

        public static ulong GetSymbolAddress(this IBusController bus, string symbolName, ICPU context = null)
        {
            if(!bus.TryGetAllSymbolAddresses(symbolName, out var addressesEnumerable, context))
            {
                throw new RecoverableException($"Could not find any address for symbol: {symbolName}");
            }
            var addresses = addressesEnumerable.ToArray();
            if(addresses.Length != 1)
            {
                throw new RecoverableException($"Found {addresses.Length} possible addresses for the symbol. Select which one you're interested in by providing a 0-based index or use the `GetAllSymbolAddresses` method");
            }
            return addresses[0];
        }
    }
}
