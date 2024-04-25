//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;
using System.Threading;
using System.Collections.ObjectModel;
using System.Text;
using Machine = Antmicro.Renode.Core.Machine;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using ELFSharp.ELF;
using ELFSharp.ELF.Segments;
using ELFSharp.UImage;
using System.IO;
using Antmicro.Renode.Core.Extensions;
using System.Reflection;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Peripherals.Memory;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Bus
{
    /// <summary>
    ///     The <c>SystemBus</c> is the main system class, where all data passes through.
    /// </summary>
    [Icon("sysbus")]
    [ControllerMask(typeof(IPeripheral))]
    public sealed partial class SystemBus : IBusController, IDisposable
    {
        internal SystemBus(IMachine machine)
        {
            this.Machine = machine;
            cpuSync = new object();
            binaryFingerprints = new List<BinaryFingerprint>();
            cpuById = new Dictionary<int, ICPU>();
            idByCpu = new Dictionary<ICPU, int>();
            hooksOnRead = new Dictionary<ulong, List<BusHookHandler>>();
            hooksOnWrite = new Dictionary<ulong, List<BusHookHandler>>();
            InitStructures();
            this.Log(LogLevel.Info, "System bus created.");
        }

        public void LoadFileChunks(string path, IEnumerable<FileChunk> chunks, ICPU cpu)
        {
            var minAddr = this.LoadFileChunks(chunks, cpu);
            AddFingerprint(path);
            UpdateLowestLoadedAddress(minAddr);
            this.DebugLog(path + " File loaded.");
        }

        public void Unregister(IBusPeripheral peripheral)
        {
            using(Machine.ObtainPausedState(true))
            {
                Machine.UnregisterAsAChildOf(this, peripheral);
                UnregisterInner(peripheral);
            }
        }

        public void Unregister(IBusRegistered<IBusPeripheral> busRegisteredPeripheral)
        {
            using(Machine.ObtainPausedState(true))
            {
                Machine.UnregisterAsAChildOf(this, busRegisteredPeripheral.RegistrationPoint);
                UnregisterInner(busRegisteredPeripheral);
            }
        }

        public void Unregister(IPeripheral peripheral)
        {
            using(Machine.ObtainPausedState(true))
            {
                Machine.UnregisterAsAChildOf(this, peripheral);
            }
        }

        public void Register(IPeripheral peripheral, NullRegistrationPoint registrationPoint)
        {
            using(Machine.ObtainPausedState(true))
            {
                // NullRegistrationPoint peripherals are not mapped on the bus and
                // are not directly accessible from the emulated software.
                Machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            }
        }

        public void Register(IBusPeripheral peripheral, BusRangeRegistration registrationPoint)
        {
            var methods = PeripheralAccessMethods.CreateWithLock();
            if(registrationPoint is BusMultiRegistration multiRegistrationPoint)
            {
                if(peripheral is IMapped)
                {
                    throw new ConstructionException(string.Format("It is not allowed to register `{0}` peripheral using `{1}`", typeof(IMapped).Name, typeof(BusMultiRegistration).Name));
                }
                FillAccessMethodsWithTaggedMethods(peripheral, multiRegistrationPoint.ConnectionRegionName, ref methods);
            }
            else
            {
                FillAccessMethodsWithDefaultMethods(peripheral, ref methods);
            }
            RegisterInner(peripheral, methods, registrationPoint, context: registrationPoint.CPU);
        }

        public void Register(IBusPeripheral peripheral, BusMultiRegistration registrationPoint)
        {
            Register(peripheral, (BusRangeRegistration)registrationPoint);
        }

        void IPeripheralRegister<IBusPeripheral, BusMultiRegistration>.Unregister(IBusPeripheral peripheral)
        {
            Unregister(peripheral);
        }

        public void Register(IKnownSize peripheral, BusPointRegistration registrationPoint)
        {
            Register(peripheral, new BusRangeRegistration(new Range(registrationPoint.StartingPoint, checked((ulong)peripheral.Size)), registrationPoint.Offset, registrationPoint.CPU));
        }

        public void Unregister(IKnownSize peripheral)
        {
            Unregister((IBusPeripheral)peripheral);
        }

        public void MoveRegistrationWithinContext(IBusPeripheral peripheral, ulong newAddress, ICPU context, Func<IEnumerable<IBusRegistered<IBusPeripheral>>, IBusRegistered<IBusPeripheral>> selector = null)
        {
            if(context == null && !TryFindCurrentThreadCPUAndId(out context, out var _))
            {
                throw new RecoverableException("Moving a peripheral is supported only from CPU thread if context isn't explicitly set");
            }
            var wasMapped = RemoveMappingsForPeripheral(peripheral);
            var container = context != null ? cpuLocalPeripherals[context] : globalPeripherals;
            var registrationPoints = container.Peripherals.Where(x => x.Peripheral == peripheral).ToList();
            if(registrationPoints.Count == 0)
            {
                throw new RecoverableException("Attempted to move a peripheral that isn't registered within current context");
            }
            IBusRegistered<IBusPeripheral> registrationPoint;
            if(selector != null)
            {
                registrationPoint = selector(registrationPoints);
                if(registrationPoint == null)
                {
                    throw new RecoverableException("Provided selector failed to find a suitable registration point");
                }
            }
            else
            {
                if(registrationPoints.Count > 1)
                {
                    throw new RecoverableException($"Unable to unambiguously determine a registration point, found multiple candidates: {String.Join(", ", registrationPoints.Select(x => x.RegistrationPoint))}");
                }
                registrationPoint = registrationPoints[0];
            }
            var newRegistrationPoint = container.Move(registrationPoint, newAddress);
            Machine.ExchangeRegistrationPointForPeripheral(this, peripheral, registrationPoint.RegistrationPoint, newRegistrationPoint.RegistrationPoint);
            if(wasMapped)
            {
                AddMappingsForPeripheral(peripheral, newRegistrationPoint.RegistrationPoint, context);
            }
        }

        public void Register(ICPU cpu, CPURegistrationPoint registrationPoint)
        {
            lock(cpuSync)
            {
                if(mappingsRemoved)
                {
                    throw new RegistrationException("Currently cannot register CPU after some memory mappings have been dynamically removed.");
                }
                if(!registrationPoint.Slot.HasValue)
                {
                    var i = 0;
                    while(cpuById.ContainsKey(i))
                    {
                        i++;
                    }
                    registrationPoint = new CPURegistrationPoint(i);
                }
                Machine.RegisterAsAChildOf(this, cpu, registrationPoint);
                cpuById.Add(registrationPoint.Slot.Value, cpu);
                cpuLocalPeripherals[cpu] = new PeripheralCollection(this);
                idByCpu.Add(cpu, registrationPoint.Slot.Value);
                if(cpu is ICPUWithMappedMemory memoryMappedCpu)
                {
                    foreach(var mapping in mappingsForPeripheral.SelectMany(x => x.Value)
                            .Where(x => x.Context == null || x.Context == cpu))
                    {
                        memoryMappedCpu.MapMemory(mapping);
                    }
                }

                if(cpu.Endianness != Endianess)
                {
                    hasCpuWithMismatchedEndianness = true;
                    UpdateAccessMethods();
                }

                if(GetCPUs().Select(x => x.Endianness).Distinct().Count() > 1)
                {
                    throw new RegistrationException("Currently there can't be CPUs with different endiannesses on the same bus.");
                }
            }
        }

        public void Unregister(ICPU cpu)
        {
            using(Machine.ObtainPausedState(true))
            {
                Machine.UnregisterAsAChildOf(this, cpu);
                lock(cpuSync)
                {
                    var id = idByCpu[cpu];
                    idByCpu.Remove(cpu);
                    cpuById.Remove(id);
                    cpuLocalPeripherals.Remove(cpu);
                }
            }
        }

        public void SetPCOnAllCores(ulong pc)
        {
            using(Machine.ObtainPausedState(true))
            {
                lock(cpuSync)
                {
                    foreach(var p in idByCpu.Keys.Cast<ICPU>())
                    {
                        p.PC = pc;
                    }
                }
            }
        }

        public void LogAllPeripheralsAccess(bool enable = true)
        {
            lock(cpuSync)
            {
                foreach(var p in allPeripherals.SelectMany(x => x.Peripherals))
                {
                    LogPeripheralAccess(p.Peripheral, enable);
                }
            }
        }

        public void LogPeripheralAccess(IBusPeripheral busPeripheral, bool enable = true)
        {
            foreach(var peripherals in allPeripherals)
            {
                peripherals.VisitAccessMethods(busPeripheral, pam =>
                {
                    // first check whether logging is already enabled, method should be idempotent
                    var loggingAlreadEnabled = pam.WriteByte.Target is HookWrapper;
                    this.Log(LogLevel.Info, "Logging already enabled: {0}.", loggingAlreadEnabled);
                    if(enable == loggingAlreadEnabled)
                    {
                        return pam;
                    }
                    if(enable)
                    {
                        pam.WriteByte = new BusAccess.ByteWriteMethod(new WriteLoggingWrapper<byte>(busPeripheral, new Action<long, byte>(pam.WriteByte)).Write);
                        pam.WriteWord = new BusAccess.WordWriteMethod(new WriteLoggingWrapper<ushort>(busPeripheral, new Action<long, ushort>(pam.WriteWord)).Write);
                        pam.WriteDoubleWord = new BusAccess.DoubleWordWriteMethod(new WriteLoggingWrapper<uint>(busPeripheral, new Action<long, uint>(pam.WriteDoubleWord)).Write);
                        pam.WriteQuadWord = new BusAccess.QuadWordWriteMethod(new WriteLoggingWrapper<ulong>(busPeripheral, new Action<long, ulong>(pam.WriteQuadWord)).Write);
                        pam.ReadByte = new BusAccess.ByteReadMethod(new ReadLoggingWrapper<byte>(busPeripheral, new Func<long, byte>(pam.ReadByte)).Read);
                        pam.ReadWord = new BusAccess.WordReadMethod(new ReadLoggingWrapper<ushort>(busPeripheral, new Func<long, ushort>(pam.ReadWord)).Read);
                        pam.ReadDoubleWord = new BusAccess.DoubleWordReadMethod(new ReadLoggingWrapper<uint>(busPeripheral, new Func<long, uint>(pam.ReadDoubleWord)).Read);
                        pam.ReadQuadWord = new BusAccess.QuadWordReadMethod(new ReadLoggingWrapper<ulong>(busPeripheral, new Func<long, ulong>(pam.ReadQuadWord)).Read);
                        return pam;
                    }
                    else
                    {
                        pam.WriteByte = new BusAccess.ByteWriteMethod(((WriteLoggingWrapper<byte>)pam.WriteByte.Target).OriginalMethod);
                        pam.WriteWord = new BusAccess.WordWriteMethod(((WriteLoggingWrapper<ushort>)pam.WriteWord.Target).OriginalMethod);
                        pam.WriteDoubleWord = new BusAccess.DoubleWordWriteMethod(((WriteLoggingWrapper<uint>)pam.WriteDoubleWord.Target).OriginalMethod);
                        pam.WriteQuadWord = new BusAccess.QuadWordWriteMethod(((WriteLoggingWrapper<ulong>)pam.WriteQuadWord.Target).OriginalMethod);
                        pam.ReadByte = new BusAccess.ByteReadMethod(((ReadLoggingWrapper<byte>)pam.ReadByte.Target).OriginalMethod);
                        pam.ReadWord = new BusAccess.WordReadMethod(((ReadLoggingWrapper<ushort>)pam.ReadWord.Target).OriginalMethod);
                        pam.ReadDoubleWord = new BusAccess.DoubleWordReadMethod(((ReadLoggingWrapper<uint>)pam.ReadDoubleWord.Target).OriginalMethod);
                        pam.ReadQuadWord = new BusAccess.QuadWordReadMethod(((ReadLoggingWrapper<ulong>)pam.ReadQuadWord.Target).OriginalMethod);
                        return pam;
                    }
                });
            }
        }

        public IEnumerable<ICPU> GetCPUs()
        {
            lock(cpuSync)
            {
                return new ReadOnlyCollection<ICPU>(idByCpu.Keys.ToList());
            }
        }

        public int GetCPUId(ICPU cpu)
        {
            lock(cpuSync)
            {
                if(idByCpu.ContainsKey(cpu))
                {
                    return idByCpu[cpu];
                }
                throw new KeyNotFoundException("Given CPU is not registered.");
            }
        }

        public ICPU GetCurrentCPU()
        {
            ICPU cpu;
            if(!TryGetCurrentCPU(out cpu))
            {
                // TODO: inline
                throw new RecoverableException(CantFindCpuIdMessage);
            }
            return cpu;
        }

        public int GetCurrentCPUId()
        {
            int id;
            if(!TryGetCurrentCPUId(out id))
            {
                throw new RecoverableException(CantFindCpuIdMessage);
            }
            return id;
        }

        private bool TryGetCurrentCPUId(out int cpuId)
        {
            if(threadLocalContext.InUse)
            {
                cpuId = threadLocalContext.CPUId;
                return true;
            }
            /*
             * Because getting cpu id can possibly be a heavy operation, we cache the
             * obtained ID in the thread local storage. Note that we assume here that the
             * thread with such storage won't be used for another purposes than it was
             * used originally (i.e. cpu loop).
             */
            if(cachedCpuId.IsValueCreated)
            {
                cpuId = cachedCpuId.Value;
                return true;
            }

            return TryFindCurrentThreadCPUAndId(out var _, out cpuId);
        }

        private bool TryFindCurrentThreadCPUAndId(out ICPU cpu, out int cpuId)
        {
            lock(cpuSync)
            {
                foreach(var entry in cpuById)
                {
                    cpu = entry.Value;
                    if(!cpu.OnPossessedThread)
                    {
                        continue;
                    }
                    cpuId = entry.Key;
                    cachedCpuId.Value = cpuId;
                    return true;
                }
                cpu = default(ICPU);
                cpuId = -1;
                return false;
            }
        }

        public bool TryGetCurrentCPU(out ICPU cpu)
        {
            lock(cpuSync)
            {
                int id;
                if(TryGetCurrentCPUId(out id))
                {
                    cpu = cpuById[id];
                    return true;
                }
                cpu = null;
                return false;
            }
        }

        /// <summary>
        /// Unregister peripheral from the specified address.
        ///
        /// NOTE: After calling this method, peripheral may still be
        /// registered in the SystemBus at another address. In order
        /// to remove peripheral completely use 'Unregister' method.
        /// </summary>
        /// <param name="address">Address on system bus where the peripheral is registered.</param>
        /// <param name="context">
        ///     CPU context in which peripherals should be scanned.
        ///     This is useful when some peripherals are only accessible from selected CPUs.
        ///
        ///     If not provided, the global peripherals collection (i.e., peripherals available for all CPUs) is searched.
        /// </param>
        public void UnregisterFromAddress(ulong address, ICPU context = null)
        {
            var busRegisteredPeripheral = WhatIsAt(address, context);
            if(busRegisteredPeripheral == null)
            {
                throw new RecoverableException(string.Format(
                    "There is no peripheral registered at 0x{0:X}.", address));
            }
            Unregister(busRegisteredPeripheral);
        }

        public void Dispose()
        {
            cachedCpuId.Dispose();
            threadLocalContext.Dispose();
            #if DEBUG
            foreach(var peripherals in allPeripherals)
            {
                peripherals.ShowStatistics();
            }
            #endif
        }

        /// <summary>Checks what is at a given address.</summary>
        /// <param name="address">
        ///     A <see cref="ulong"/> with the address to check.
        /// </param>
        /// <param name="context">
        ///     CPU context in which peripherals should be scanned.
        ///     This is useful when some peripherals are only accessible from selected CPUs.
        ///
        ///     If not provided, the global peripherals collection (i.e., peripherals available for all CPUs) is searched.
        /// </param>
        /// <returns>
        ///     A peripheral which is at the given address.
        /// </returns>
        public IBusRegistered<IBusPeripheral> WhatIsAt(ulong address, ICPU context = null)
        {
            return GetPeripheralsForContext(context).FirstOrDefault(x => x.RegistrationPoint.Range.Contains(address));
        }

        public IPeripheral WhatPeripheralIsAt(ulong address, ICPU context = null)
        {
            var registered = WhatIsAt(address, context);
            if(registered != null)
            {
                return registered.Peripheral;
            }
            return null;
        }

        public IBusRegistered<MappedMemory> FindMemory(ulong address, ICPU context = null)
        {
            return GetPeripheralsForContext(context)
                .Where(x => x.Peripheral is MappedMemory)
                .Convert<IBusPeripheral, MappedMemory>()
                .FirstOrDefault(x => x.RegistrationPoint.Range.Contains(address));
        }

        public IEnumerable<IBusRegistered<IMapped>> GetMappedPeripherals(ICPU context = null)
        {
            return GetPeripheralsForContext(context)
                .Where(x => x.Peripheral is IMapped)
                .Convert<IBusPeripheral, IMapped>();
        }

        public IEnumerable<IBusRegistered<IBusPeripheral>> GetRegisteredPeripherals(ICPU context = null)
        {
            return GetPeripheralsForContext(context);
        }

        public void SilenceRange(Range range)
        {
            var silencer = new Silencer();
            Register(silencer, new BusRangeRegistration(range));
        }

        public void ReadBytes(ulong address, int count, byte[] destination, int startIndex, bool onlyMemory = false, ICPU context = null)
        {
            using(SetLocalContext(context))
            {
                var targets = FindTargets(address, checked((ulong)count), context);
                if(onlyMemory)
                {
                    ThrowIfNotAllMemory(targets);
                }
                foreach(var target in targets)
                {
                    var memory = target.What.Peripheral as MappedMemory;
                    if(memory != null)
                    {
                        checked
                        {
                            memory.ReadBytes(checked((long)(target.Offset - target.What.RegistrationPoint.Range.StartAddress + target.What.RegistrationPoint.Offset)), (int)target.SourceLength, destination, startIndex + (int)target.SourceIndex);
                        }
                    }
                    else
                    {
                        for(var i = 0UL; i < target.SourceLength; ++i)
                        {
                            destination[checked((ulong)startIndex) + target.SourceIndex + i] = ReadByte(target.Offset + i, context);
                        }
                    }
                }
            }
        }

        public byte[] ReadBytes(ulong address, int count, bool onlyMemory = false, ICPU context = null)
        {
            var result = new byte[count];
            ReadBytes(address, count, result, 0, onlyMemory, context);
            return result;
        }

        public byte[] ReadBytes(long offset, int count, ICPU context = null)
        {
            return ReadBytes((ulong)offset, count, context: context);
        }

        public void WriteBytes(byte[] bytes, ulong address, bool onlyMemory = false, ICPU context = null)
        {
            WriteBytes(bytes, address, bytes.Length, onlyMemory, context);
        }

        public void WriteBytes(byte[] bytes, ulong address, int startingIndex, long count, bool onlyMemory = false, ICPU context = null)
        {
            using(SetLocalContext(context))
            {
                var targets = FindTargets(address, checked((ulong)count), context);
                if(onlyMemory)
                {
                    ThrowIfNotAllMemory(targets);
                }
                foreach(var target in targets)
                {
                    var multibytePeripheral = target.What.Peripheral as IMultibyteWritePeripheral;
                    if(multibytePeripheral != null)
                    {
                        checked
                        {
                            multibytePeripheral.WriteBytes(checked((long)(target.Offset - target.What.RegistrationPoint.Range.StartAddress + target.What.RegistrationPoint.Offset)), bytes, startingIndex + (int)target.SourceIndex, (int)target.SourceLength);
                        }
                    }
                    else
                    {
                        for(var i = 0UL; i < target.SourceLength; ++i)
                        {
                            WriteByte(target.Offset + i, bytes[target.SourceIndex + (ulong)startingIndex + i]);
                        }
                    }
                }
            }
        }

        public void WriteBytes(byte[] bytes, ulong address, long count, bool onlyMemory = false, ICPU context = null)
        {
            WriteBytes(bytes, address, 0, count, onlyMemory, context);
        }

        public void WriteBytes(long offset, byte[] array, int startingIndex, int count, ICPU context = null)
        {
            WriteBytes(array, (ulong)offset, startingIndex, count, context: context);
        }

        public void ZeroRange(Range range, ICPU context = null)
        {
            var zeroBlock = new byte[1024 * 1024];
            var blocksNo = range.Size / (ulong)zeroBlock.Length;
            for(var i = 0UL; i < blocksNo; i++)
            {
                WriteBytes(zeroBlock, range.StartAddress + i * (ulong)zeroBlock.Length, context: context);
            }
            WriteBytes(zeroBlock, range.StartAddress + blocksNo * (ulong)zeroBlock.Length, (int)range.Size % zeroBlock.Length, context: context);
        }

        // Specifying `textAddress` will override the address of the program text - the symbols will be applied
        // as if the first loaded segment started at the specified address. This is equivalent to the ADDR parameter
        // to GDB's add-symbol-file.
        public void LoadSymbolsFrom(ReadFilePath fileName, bool useVirtualAddress = false, ulong? textAddress = null)
        {
            using (var elf = GetELFFromFile(fileName))
            {
                Lookup.LoadELF(elf, useVirtualAddress, textAddress);
            }
            pcCache.Invalidate();
        }

        public void AddSymbol(Range address, string name, bool isThumb = false)
        {
            checked
            {
                Lookup.InsertSymbol(name, address.StartAddress, address.Size);
            }
            pcCache.Invalidate();
        }

        public void LoadELF(ReadFilePath fileName, bool useVirtualAddress = false, bool allowLoadsOnlyToMemory = true, IInitableCPU cpu = null)
        {
            if(!Machine.IsPaused)
            {
                throw new RecoverableException("Cannot load ELF on an unpaused machine.");
            }
            this.DebugLog("Loading ELF {0}.", fileName);
            using(var elf = GetELFFromFile(fileName))
            {
                var segmentsToLoad = elf.Segments.Where(x => x.Type == SegmentType.Load);
                if(!segmentsToLoad.Any())
                {
                    throw new RecoverableException($"ELF '{fileName}' has no loadable segments.");
                }

                foreach (var s in segmentsToLoad)
                {
                    var contents = s.GetContents();
                    var loadAddress = useVirtualAddress ? s.GetSegmentAddress() : s.GetSegmentPhysicalAddress();
                    this.Log(LogLevel.Info,
                        "Loading segment of {0} bytes length at 0x{1:X}.",
                        s.GetSegmentSize(),
                        loadAddress
                    );
                    this.WriteBytes(contents, loadAddress, allowLoadsOnlyToMemory, cpu);
                    UpdateLowestLoadedAddress(loadAddress);
                    this.DebugLog("Segment loaded.");
                }
                Lookup.LoadELF(elf, useVirtualAddress);
                pcCache.Invalidate();

                if(cpu != null)
                {
                    cpu.InitFromElf(elf);
                }
                else
                {
                    foreach(var c in GetCPUs().OfType<IInitableCPU>())
                    {
                        c.InitFromElf(elf);
                    }
                }
                AddFingerprint(fileName);
            }
        }

        public void LoadUImage(ReadFilePath fileName, IInitableCPU cpu = null)
        {
            if(!Machine.IsPaused)
            {
                throw new RecoverableException("Cannot load ELF on an unpaused machine.");
            }
            UImage uImage;
            this.DebugLog("Loading uImage {0}.", fileName);

            switch(UImageReader.TryLoad(fileName, out uImage))
            {
            case UImageResult.NotUImage:
                throw new RecoverableException(string.Format("Given file '{0}' is not a U-Boot image.", fileName));
            case UImageResult.BadChecksum:
                throw new RecoverableException(string.Format("Header checksum does not match for the U-Boot image '{0}'.", fileName));
            case UImageResult.NotSupportedImageType:
                throw new RecoverableException(string.Format("Given file '{0}' is not of a supported image type.", fileName));
            }
            byte[] toLoad;
            switch(uImage.TryGetImageData(out toLoad))
            {
            case ImageDataResult.BadChecksum:
                throw new RecoverableException("Bad image checksum, probably corrupted image.");
            case ImageDataResult.UnsupportedCompressionFormat:
                throw new RecoverableException(string.Format("Unsupported compression format '{0}'.", uImage.Compression));
            }
            WriteBytes(toLoad, uImage.LoadAddress, context: cpu);
            if(cpu != null)
            {
                cpu.InitFromUImage(uImage);
            }
            else
            {
                foreach(var c in GetCPUs().OfType<IInitableCPU>())
                {
                    c.InitFromUImage(uImage);
                }
            }
            this.Log(LogLevel.Info, string.Format(
                "Loaded U-Boot image '{0}'\n" +
                "load address: 0x{1:X}\n" +
                "size:         {2}B = {3}B\n" +
                "timestamp:    {4}\n" +
                "entry point:  0x{5:X}\n" +
                "architecture: {6}\n" +
                "OS:           {7}",
                uImage.Name,
                uImage.LoadAddress,
                uImage.Size, Misc.NormalizeBinary(uImage.Size),
                uImage.Timestamp,
                uImage.EntryPoint,
                uImage.Architecture,
                uImage.OperatingSystem
            ));
            AddFingerprint(fileName);
            UpdateLowestLoadedAddress(uImage.LoadAddress);
        }

        public IEnumerable<BinaryFingerprint> GetLoadedFingerprints()
        {
            return binaryFingerprints.ToArray();
        }

        public BinaryFingerprint GetFingerprint(ReadFilePath fileName)
        {
            return new BinaryFingerprint(fileName);
        }

        public bool TryGetAllSymbolAddresses(string symbolName, out IEnumerable<ulong> symbolAddresses)
        {
            var result = Lookup.TryGetSymbolsByName(symbolName, out var symbols);
            symbolAddresses = symbols.Select(symbol => symbol.Start.RawValue);
            return result;
        }

        public IEnumerable<ulong> GetAllSymbolAddresses(string symbolName)
        {
            if(!TryGetAllSymbolAddresses(symbolName, out var symbolAddresses))
            {
                throw new RecoverableException(string.Format("No symbol with name `{0}` found.", symbolName));
            }
            return symbolAddresses;
        }

        public ulong GetSymbolAddress(string symbolName)
        {
            var addresses = GetAllSymbolAddresses(symbolName).ToArray();
            if(addresses.Length > 1)
            {
                throw new RecoverableException(string.Format("Ambiguous symbol name: `{0}`. Use GetAllSymbolAddresses to get all possible addresses.", symbolName));
            }
            return addresses[0];

        }

        public string FindSymbolAt(ulong offset)
        {
            if(!TryFindSymbolAt(offset, out var name, out var _))
            {
                return null;
            }
            return name;
        }

        public bool TryFindSymbolAt(ulong offset, out string name, out Symbol symbol)
        {
            if(!pcCache.TryGetValue(offset, out var entry))
            {
                if(!Lookup.TryGetSymbolByAddress(offset, out symbol))
                {
                    symbol = null;
                    name = null;
                    return false;
                }
                else
                {
                    name = symbol.ToStringRelative(offset);
                }
                pcCache.Add(offset, Tuple.Create(name, symbol));
            }
            else
            {
                name = entry.Item1;
                symbol = entry.Item2;
            }

            return true;
        }

        public void MapMemory(IMappedSegment segment, IBusPeripheral owner, bool relative = true, ICPUWithMappedMemory context = null)
        {
            if(relative)
            {
                var wrappers = new List<MappedSegmentWrapper>();
                foreach(var registrationPoint in GetRegistrationPoints(owner, context))
                {
                    var wrapper = FromRegistrationPointToSegmentWrapper(segment, registrationPoint, context);
                    if(wrapper != null)
                    {
                        wrappers.Add(wrapper);
                    }
                }
                AddMappings(wrappers, owner);
            }
            else
            {
                AddMappings(new [] { new MappedSegmentWrapper(segment, 0, long.MaxValue, context) }, owner);
            }
        }

        public void UnmapMemory(ulong start, ulong size)
        {
            UnmapMemory(start.By(size));
        }

        public void UnmapMemory(Range range)
        {
            lock(cpuSync)
            {
                mappingsRemoved = true;
                foreach(var cpu in idByCpu.Keys.OfType<ICPUWithMappedMemory>())
                {
                    cpu.UnmapMemory(range);
                }
            }
        }

        public void SetPageAccessViaIo(ulong address)
        {
            foreach(var cpu in cpuById.Values.OfType<ICPUWithMappedMemory>())
            {
                cpu.SetPageAccessViaIo(address);
            }
        }

        public void ClearPageAccessViaIo(ulong address)
        {
            foreach(var cpu in cpuById.Values.OfType<ICPUWithMappedMemory>())
            {
                cpu.ClearPageAccessViaIo(address);
            }
        }

        public void AddWatchpointHook(ulong address, SysbusAccessWidth width, Access access, BusHookDelegate hook)
        {
            if(!Enum.IsDefined(typeof(Access), access))
            {
                throw new RecoverableException("Undefined access value.");
            }
            if(((((int)width) & 15) != (int)width) || width == 0)
            {
                throw new RecoverableException("Undefined width value.");
            }

            var handler = new BusHookHandler(hook, width);

            var dictionariesToUpdate = new List<Dictionary<ulong, List<BusHookHandler>>>();

            if((access & Access.Read) != 0)
            {
                dictionariesToUpdate.Add(hooksOnRead);
            }
            if((access & Access.Write) != 0)
            {
                dictionariesToUpdate.Add(hooksOnWrite);
            }
            foreach(var dictionary in dictionariesToUpdate)
            {
                if(dictionary.ContainsKey(address))
                {
                    dictionary[address].Add(handler);
                }
                else
                {
                    dictionary[address] = new List<BusHookHandler> { handler };
                }
            }
            UpdatePageAccesses();
        }

        public void RemoveWatchpointHook(ulong address, BusHookDelegate hook)
        {
            foreach(var hookDictionary in new [] { hooksOnRead, hooksOnWrite })
            {
                List<BusHookHandler> handlers;
                if(hookDictionary.TryGetValue(address, out handlers))
                {
                    handlers.RemoveAll(x => x.ContainsAction(hook));
                    if(handlers.Count == 0)
                    {
                        hookDictionary.Remove(address);
                    }
                }
            }

            ClearPageAccessViaIo(address);
            UpdatePageAccesses();
        }

        public void RemoveAllWatchpointHooks(ulong address)
        {
            hooksOnRead.Remove(address);
            hooksOnWrite.Remove(address);
            ClearPageAccessViaIo(address);
            UpdatePageAccesses();
        }

        public bool TryGetWatchpointsAt(ulong address, Access access, out List<BusHookHandler> result)
        {
            if(access == Access.ReadAndWrite || access == Access.Read)
            {
                if(hooksOnRead.TryGetValue(address, out result))
                {
                    return true;
                }
                else if(access == Access.Read)
                {
                    result = null;
                    return false;
                }
            }
            return hooksOnWrite.TryGetValue(address, out result);
        }

        public IEnumerable<BusRangeRegistration> GetRegistrationPoints(IBusPeripheral peripheral, ICPU context = null)
        {
            return GetPeripheralsForContext(context)
                .Where(x => x.Peripheral == peripheral)
                .Select(x => x.RegistrationPoint);
        }

        public IEnumerable<BusRangeRegistration> GetRegistrationPoints(IBusPeripheral peripheral)
        {
            // try to detect the CPU context based on the current thread
            TryGetCurrentCPU(out var context);
            return GetRegistrationPoints(peripheral, context);
        }

        public void ApplySVD(string path)
        {
            var svdDevice = new SVDParser(path, this);
            svdDevices.Add(svdDevice);
        }

        public void Tag(Range range, string tag, ulong defaultValue = 0, bool pausing = false)
        {
            var intersectings = tags.Where(x => x.Key.Intersects(range)).ToArray();
            if(intersectings.Length == 0)
            {
                tags.Add(range, new TagEntry { Name = tag, DefaultValue = defaultValue });
                if(pausing)
                {
                    pausingTags.Add(tag);
                }
                return;
            }
            // tag splitting
            if(intersectings.Length != 1)
            {
                throw new RecoverableException(string.Format(
                    "Currently subtag has to be completely contained in other tag. Given one intersects with tags: {0}",
                    intersectings.Select(x => x.Value.Name).Aggregate((x, y) => x + ", " + y)));
            }
            var parentRange = intersectings[0].Key;
            var parentName = intersectings[0].Value.Name;
            var parentDefaultValue = intersectings[0].Value.DefaultValue;
            var parentPausing = pausingTags.Contains(parentName);
            if(!parentRange.Contains(range))
            {
                throw new RecoverableException(string.Format(
                    "Currently subtag has to be completely contained in other tag, in this case {0}.", parentName));
            }
            RemoveTag(parentRange.StartAddress);
            var parentRangeAfterSplitSizeLeft = range.StartAddress - parentRange.StartAddress;
            if(parentRangeAfterSplitSizeLeft > 0)
            {
                Tag(new Range(parentRange.StartAddress, parentRangeAfterSplitSizeLeft), parentName, parentDefaultValue, parentPausing);
            }
            var parentRangeAfterSplitSizeRight = parentRange.EndAddress - range.EndAddress;
            if(parentRangeAfterSplitSizeRight > 0)
            {
                Tag(new Range(range.EndAddress + 1, parentRangeAfterSplitSizeRight), parentName, parentDefaultValue, parentPausing);
            }
            Tag(range, string.Format("{0}/{1}", parentName, tag), defaultValue, pausing);
        }

        public void RemoveTag(ulong address)
        {
            var tagsToRemove = tags.Where(x => x.Key.Contains(address)).ToArray();
            if(tagsToRemove.Length == 0)
            {
                throw new RecoverableException(string.Format("There is no tag at address 0x{0:X}.", address));
            }
            foreach(var tag in tagsToRemove)
            {
                tags.Remove(tag.Key);
                pausingTags.Remove(tag.Value.Name);
            }
        }

        public void SetAddressRangeLocked(Range range, bool locked)
        {
            if(locked)
            {
                lockedRangesCollection.Add(range);
            }
            else
            {
                lockedRangesCollection.Remove(range);
            }
        }

        public void SetPeripheralEnabled(IPeripheral peripheral, bool enabled)
        {
            if(enabled)
            {
                lockedPeripherals.Remove(peripheral);
            }
            else
            {
                if(peripheral != null)
                {
                    lockedPeripherals.Add(peripheral);
                }
            }
        }

        public bool IsAddressRangeLocked(Range range)
        {
            return lockedRangesCollection.ContainsOverlappingRange(range);
        }

        public bool IsPeripheralEnabled(IPeripheral peripheral)
        {
            if(lockedPeripherals.Contains(peripheral))
            {
                return false;
            }
            return true;
        }

        public void Clear()
        {
            ClearAll();
        }

        public void Reset()
        {
            LowestLoadedAddress = null;
            Lookup = new SymbolLookup();
            pcCache.Invalidate();
        }

        public string DecorateWithCPUNameAndPC(string str)
        {
            if(!TryGetCurrentCPU(out var cpu) || !Machine.TryGetLocalName(cpu, out var cpuName))
            {
                return str;
            }

            // you probably wonder why 26?
            // * we assume at least 3 characters for cpu name
            // * 64-bit PC value nees 18 characters
            // * there are 5 more separating characters
            var builder = new StringBuilder(str.Length + 26);
            builder
                .Append("[")
                .Append(cpuName);

            builder.AppendFormat(": 0x{0:X}", cpu.PC.RawValue);

            builder
                .Append("] ")
                .Append(str);

            return builder.ToString();
        }

        public IMachine Machine { get; }

        public int UnexpectedReads
        {
            get
            {
                return Interlocked.CompareExchange(ref unexpectedReads, 0, 0);
            }
        }

        public int UnexpectedWrites
        {
            get
            {
                return Interlocked.CompareExchange(ref unexpectedWrites, 0, 0);
            }
        }

        public ulong? LowestLoadedAddress { get; private set; }

        public IEnumerable<IRegistered<IBusPeripheral, BusRangeRegistration>> Children
        {
            get
            {
                foreach(var peripheral in GetPeripheralsForCurrentCPU())
                {
                    yield return peripheral;
                }
            }
        }

        public bool IsMultiCore
        {
            get
            {
                return cpuById.Count() > 1;
            }
        }

        public Endianess Endianess
        {
            get
            {
                return endianess;
            }
            set
            {
                if(peripheralRegistered)
                {
                    throw new RecoverableException("Currently one has to set endianess before any peripheral is registered.");
                }
                endianess = value;
            }
        }

        public SymbolLookup Lookup
        {
            get;
            private set;
        }

        public UnhandledAccessBehaviour UnhandledAccessBehaviour { get; set; }

        private void UnregisterInner(IBusPeripheral peripheral)
        {
            RemoveMappingsForPeripheral(peripheral);

            // remove the peripheral from all cpu-local and the global mappings
            foreach(var context in cpuLocalPeripherals.Keys.ToArray())
            {
                cpuLocalPeripherals[context].Remove(peripheral);
            }
            globalPeripherals.Remove(peripheral);
        }

        private void UnregisterInner(IBusRegistered<IBusPeripheral> busRegistered)
        {
            if(mappingsForPeripheral.ContainsKey(busRegistered.Peripheral))
            {
                var toRemove = new HashSet<MappedSegmentWrapper>();
                // it is assumed that mapped segment cannot be partially outside the registration point range
                foreach(var mapping in mappingsForPeripheral[busRegistered.Peripheral].Where(x => busRegistered.RegistrationPoint.Range.Contains(x.StartingOffset)))
                {
                    UnmapMemory(new Range(mapping.StartingOffset, checked((ulong)mapping.Size)));
                    toRemove.Add(mapping);
                }
                mappingsForPeripheral[busRegistered.Peripheral].RemoveAll(x => toRemove.Contains(x));
                if(mappingsForPeripheral[busRegistered.Peripheral].Count == 0)
                {
                    mappingsForPeripheral.Remove(busRegistered.Peripheral);
                }
            }
            var perCoreRegistration = busRegistered.RegistrationPoint as IPerCoreRegistration;
            if(perCoreRegistration?.CPU != null)
            {
                cpuLocalPeripherals[perCoreRegistration.CPU].Remove(busRegistered.RegistrationPoint.Range.StartAddress, busRegistered.RegistrationPoint.Range.EndAddress);
            }
            else
            {
                globalPeripherals.Remove(busRegistered.RegistrationPoint.Range.StartAddress, busRegistered.RegistrationPoint.Range.EndAddress);
            }
        }

        // this wrapper is to avoid compiler crashing on Ubuntu 20.04;
        // for unknown reasons calling `TryGetCurrentCPU` in the `Children` getter
        // caused the compiler to throw na InternalErrorException/NullReferenceException
        // when building sources
        private IEnumerable<IBusRegistered<IBusPeripheral>> GetPeripheralsForCurrentCPU()
        {
            TryGetCurrentCPU(out var context);
            return GetPeripheralsForContext(context);
        }

        private IEnumerable<IBusRegistered<IBusPeripheral>> GetPeripheralsForContext(ICPU context)
        {
            return (context != null)
                ? cpuLocalPeripherals[context].Peripherals.Concat(globalPeripherals.Peripherals)
                : globalPeripherals.Peripherals;
        }

        private void FillAccessMethodsWithTaggedMethods(IBusPeripheral peripheral, string tag, ref PeripheralAccessMethods methods)
        {
            methods.Peripheral = peripheral;
            methods.Tag = tag;

            var customAccessMethods = new Dictionary<Tuple<BusAccess.Method, BusAccess.Operation>, MethodInfo>();
            foreach(var method in peripheral.GetType().GetMethods())
            {
                Type signature = null;
                if(!Misc.TryGetMatchingSignature(BusAccess.Delegates, method, out signature))
                {
                    continue;
                }

                var accessGroupAttribute = (ConnectionRegionAttribute)method.GetCustomAttributes(typeof(ConnectionRegionAttribute), true).FirstOrDefault();
                if(accessGroupAttribute == null || accessGroupAttribute.Name != tag)
                {
                    continue;
                }

                var accessMethod = BusAccess.GetMethodFromSignature(signature);
                var accessOperation = BusAccess.GetOperationFromSignature(signature);

                var tuple = Tuple.Create(accessMethod, accessOperation);
                if(customAccessMethods.ContainsKey(tuple))
                {
                    throw new RegistrationException(string.Format("Only one method for operation {0} accessing {1} registers is allowed.", accessOperation, accessMethod));
                }

                customAccessMethods[tuple] = method;
                methods.SetMethod(method, peripheral, accessOperation, accessMethod);
            }

            FillAccessMethodsWithDefaultMethods(peripheral, ref methods);
        }

        private void FillAccessMethodsWithDefaultMethods(IBusPeripheral peripheral, ref PeripheralAccessMethods methods)
        {
            methods.Peripheral = peripheral;

            var bytePeripheral = peripheral as IBytePeripheral;
            var wordPeripheral = peripheral as IWordPeripheral;
            var dwordPeripheral = peripheral as IDoubleWordPeripheral;
            var qwordPeripheral = peripheral as IQuadWordPeripheral;
            BytePeripheralWrapper byteWrapper = null;
            WordPeripheralWrapper wordWrapper = null;
            DoubleWordPeripheralWrapper dwordWrapper = null;
            QuadWordPeripheralWrapper qwordWrapper = null;

            if(methods.ReadByte != null)
            {
                byteWrapper = new BytePeripheralWrapper(methods.ReadByte, methods.WriteByte);
            }
            if(methods.ReadWord != null)
            {
                // why there are such wrappers? since device can be registered through
                // method specific registration points
                wordWrapper = new WordPeripheralWrapper(methods.ReadWord, methods.WriteWord);
            }
            if(methods.ReadDoubleWord != null)
            {
                dwordWrapper = new DoubleWordPeripheralWrapper(methods.ReadDoubleWord, methods.WriteDoubleWord);
            }
            if(methods.ReadQuadWord != null)
            {
                qwordWrapper = new QuadWordPeripheralWrapper(methods.ReadQuadWord, methods.WriteQuadWord);
            }

            if(bytePeripheral == null && wordPeripheral == null && dwordPeripheral == null && qwordPeripheral == null
                    && byteWrapper == null && wordWrapper == null && dwordWrapper == null && qwordWrapper == null)
            {
                throw new RegistrationException(string.Format("Cannot register peripheral {0}, it does not implement any of IBusPeripheral derived interfaces," +
                "nor any other methods were pointed.", peripheral));
            }

            // We need to pass in Endianess as a default because at this point the peripheral
            // is not yet associated with a machine.
            Endianess periEndianess = peripheral.GetEndianness(Endianess);
            // Note that the condition for applying ReadByteUsingDwordBigEndian et al is different than the condition
            // for byte-swapping correctly-sized accesses! The former is needed for all big-endian peripherals, the
            // latter - only in the cross-endianness case. This is to ensure that on a big-endian bus, reading a byte
            // at offset 0 from a peripheral that only supports double word access and has a single register with value
            // 0x11223344 correctly returns 0x11, and not 0x44 as it would if ReadByteUsingDword were used.
            var translatedAccessNeedsSwap = Endianess == Endianess.BigEndian;
            var matchingAccessNeedsSwap = periEndianess != Endianess;

            var allowedTranslations = default(AllowedTranslation);
            var allowedTranslationsAttributes = peripheral.GetType().GetCustomAttributes(typeof(AllowedTranslationsAttribute), true);
            if(allowedTranslationsAttributes.Length != 0)
            {
                allowedTranslations = ((AllowedTranslationsAttribute)allowedTranslationsAttributes[0]).AllowedTranslations;
            }

            // If the CPU endianness does not match the bus endianness, this endianness mismatch
            // is basically an additional swap. Instead of performing it we reverse the condition.
            // This is the case on PowerPC, for example: there the translation library is always built
            // in big-endian mode, and if the CPU is running in little-endian mode, it performs byte
            // swapping separately. This is to support switching endianness at runtime, but note that
            // if this is actually done, peripheral accesses will be reversed.
            if(hasCpuWithMismatchedEndianness)
            {
                matchingAccessNeedsSwap = !matchingAccessNeedsSwap;
                translatedAccessNeedsSwap = !translatedAccessNeedsSwap;
                // Other translation types will behave incorrectly in this case.
                allowedTranslations &=
                    AllowedTranslation.ByteToWord | AllowedTranslation.ByteToDoubleWord | AllowedTranslation.ByteToQuadWord |
                    AllowedTranslation.WordToByte | AllowedTranslation.DoubleWordToByte | AllowedTranslation.QuadWordToByte;
            }

            if(methods.ReadByte == null) // they are null or not always in pairs
            {
                if(bytePeripheral != null)
                {
                    methods.ReadByte = bytePeripheral.ReadByte;
                    methods.WriteByte = bytePeripheral.WriteByte;
                }
                else if(qwordWrapper != null && (allowedTranslations & AllowedTranslation.ByteToQuadWord) != 0)
                {
                    methods.ReadByte = translatedAccessNeedsSwap ? (BusAccess.ByteReadMethod)qwordWrapper.ReadByteUsingQwordBigEndian : qwordWrapper.ReadByteUsingQword;
                    methods.WriteByte = translatedAccessNeedsSwap ? (BusAccess.ByteWriteMethod)qwordWrapper.WriteByteUsingQwordBigEndian : qwordWrapper.WriteByteUsingQword;
                }
                else if(dwordWrapper != null && (allowedTranslations & AllowedTranslation.ByteToDoubleWord) != 0)
                {
                    methods.ReadByte = translatedAccessNeedsSwap ? (BusAccess.ByteReadMethod)dwordWrapper.ReadByteUsingDwordBigEndian : dwordWrapper.ReadByteUsingDword;
                    methods.WriteByte = translatedAccessNeedsSwap ? (BusAccess.ByteWriteMethod)dwordWrapper.WriteByteUsingDwordBigEndian : dwordWrapper.WriteByteUsingDword;
                }
                else if(wordWrapper != null && (allowedTranslations & AllowedTranslation.ByteToWord) != 0)
                {
                    methods.ReadByte = translatedAccessNeedsSwap ? (BusAccess.ByteReadMethod)wordWrapper.ReadByteUsingWordBigEndian : wordWrapper.ReadByteUsingWord;
                    methods.WriteByte = translatedAccessNeedsSwap ? (BusAccess.ByteWriteMethod)wordWrapper.WriteByteUsingWordBigEndian : wordWrapper.WriteByteUsingWord;
                }
                else if(qwordPeripheral != null && (allowedTranslations & AllowedTranslation.ByteToQuadWord) != 0)
                {
                    methods.ReadByte = translatedAccessNeedsSwap ? (BusAccess.ByteReadMethod)qwordPeripheral.ReadByteUsingQwordBigEndian : qwordPeripheral.ReadByteUsingQword;
                    methods.WriteByte = translatedAccessNeedsSwap ? (BusAccess.ByteWriteMethod)qwordPeripheral.WriteByteUsingQwordBigEndian : qwordPeripheral.WriteByteUsingQword;
                }
                else if(dwordPeripheral != null && (allowedTranslations & AllowedTranslation.ByteToDoubleWord) != 0)
                {
                    methods.ReadByte = translatedAccessNeedsSwap ? (BusAccess.ByteReadMethod)dwordPeripheral.ReadByteUsingDwordBigEndian : dwordPeripheral.ReadByteUsingDword;
                    methods.WriteByte = translatedAccessNeedsSwap ? (BusAccess.ByteWriteMethod)dwordPeripheral.WriteByteUsingDwordBigEndian : dwordPeripheral.WriteByteUsingDword;
                }
                else if(wordPeripheral != null && (allowedTranslations & AllowedTranslation.ByteToWord) != 0)
                {
                    methods.ReadByte = translatedAccessNeedsSwap ? (BusAccess.ByteReadMethod)wordPeripheral.ReadByteUsingWordBigEndian : wordPeripheral.ReadByteUsingWord;
                    methods.WriteByte = translatedAccessNeedsSwap ? (BusAccess.ByteWriteMethod)wordPeripheral.WriteByteUsingWordBigEndian : wordPeripheral.WriteByteUsingWord;
                }

                else
                {
                    methods.ReadByte = peripheral.ReadByteNotTranslated;
                    methods.WriteByte = peripheral.WriteByteNotTranslated;
                }
            }

            if(methods.ReadWord == null)
            {
                if(wordPeripheral != null)
                {
                    methods.ReadWord = matchingAccessNeedsSwap ? (BusAccess.WordReadMethod)wordPeripheral.ReadWordBigEndian : wordPeripheral.ReadWord;
                    methods.WriteWord = matchingAccessNeedsSwap ? (BusAccess.WordWriteMethod)wordPeripheral.WriteWordBigEndian : wordPeripheral.WriteWord;
                }
                else if(qwordWrapper != null && (allowedTranslations & AllowedTranslation.WordToQuadWord) != 0)
                {
                    methods.ReadWord = translatedAccessNeedsSwap ? (BusAccess.WordReadMethod)qwordWrapper.ReadWordUsingQwordBigEndian : qwordWrapper.ReadWordUsingQword;
                    methods.WriteWord = translatedAccessNeedsSwap ? (BusAccess.WordWriteMethod)qwordWrapper.WriteWordUsingQwordBigEndian : qwordWrapper.WriteWordUsingQword;
                }
                else if(dwordWrapper != null && (allowedTranslations & AllowedTranslation.WordToDoubleWord) != 0)
                {
                    methods.ReadWord = translatedAccessNeedsSwap ? (BusAccess.WordReadMethod)dwordWrapper.ReadWordUsingDwordBigEndian : dwordWrapper.ReadWordUsingDword;
                    methods.WriteWord = translatedAccessNeedsSwap ? (BusAccess.WordWriteMethod)dwordWrapper.WriteWordUsingDwordBigEndian : dwordWrapper.WriteWordUsingDword;
                }
                else if(byteWrapper != null && (allowedTranslations & AllowedTranslation.WordToByte) != 0)
                {
                    methods.ReadWord = translatedAccessNeedsSwap ? (BusAccess.WordReadMethod)byteWrapper.ReadWordUsingByteBigEndian : byteWrapper.ReadWordUsingByte;
                    methods.WriteWord = translatedAccessNeedsSwap ? (BusAccess.WordWriteMethod)byteWrapper.WriteWordUsingByteBigEndian : byteWrapper.WriteWordUsingByte;
                }
                else if(qwordPeripheral != null && (allowedTranslations & AllowedTranslation.WordToQuadWord) != 0)
                {
                    methods.ReadWord = translatedAccessNeedsSwap ? (BusAccess.WordReadMethod)qwordPeripheral.ReadWordUsingQwordBigEndian : qwordPeripheral.ReadWordUsingQword;
                    methods.WriteWord = translatedAccessNeedsSwap ? (BusAccess.WordWriteMethod)qwordPeripheral.WriteWordUsingQwordBigEndian : qwordPeripheral.WriteWordUsingQword;
                }
                else if(dwordPeripheral != null && (allowedTranslations & AllowedTranslation.WordToDoubleWord) != 0)
                {
                    methods.ReadWord = translatedAccessNeedsSwap ? (BusAccess.WordReadMethod)dwordPeripheral.ReadWordUsingDwordBigEndian : dwordPeripheral.ReadWordUsingDword;
                    methods.WriteWord = translatedAccessNeedsSwap ? (BusAccess.WordWriteMethod)dwordPeripheral.WriteWordUsingDwordBigEndian : dwordPeripheral.WriteWordUsingDword;
                }
                else if(bytePeripheral != null && (allowedTranslations & AllowedTranslation.WordToByte) != 0)
                {
                    methods.ReadWord = translatedAccessNeedsSwap ? (BusAccess.WordReadMethod)bytePeripheral.ReadWordUsingByteBigEndian : bytePeripheral.ReadWordUsingByte;
                    methods.WriteWord = translatedAccessNeedsSwap ? (BusAccess.WordWriteMethod)bytePeripheral.WriteWordUsingByteBigEndian : bytePeripheral.WriteWordUsingByte;
                }
                else
                {
                    methods.ReadWord = peripheral.ReadWordNotTranslated;
                    methods.WriteWord = peripheral.WriteWordNotTranslated;
                }
            }
            else if(matchingAccessNeedsSwap)
            {
                // if methods.ReadWord != null then we have a wordWrapper
                methods.ReadWord = (BusAccess.WordReadMethod)wordWrapper.ReadWordBigEndian;
                methods.WriteWord = (BusAccess.WordWriteMethod)wordWrapper.WriteWordBigEndian;
            }

            if(methods.ReadDoubleWord == null)
            {
                if(dwordPeripheral != null)
                {
                    methods.ReadDoubleWord = matchingAccessNeedsSwap ? (BusAccess.DoubleWordReadMethod)dwordPeripheral.ReadDoubleWordBigEndian : dwordPeripheral.ReadDoubleWord;
                    methods.WriteDoubleWord = matchingAccessNeedsSwap ? (BusAccess.DoubleWordWriteMethod)dwordPeripheral.WriteDoubleWordBigEndian : dwordPeripheral.WriteDoubleWord;
                }
                else if(qwordWrapper != null && (allowedTranslations & AllowedTranslation.DoubleWordToQuadWord) != 0)
                {
                    methods.ReadDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordReadMethod)qwordWrapper.ReadDoubleWordUsingQwordBigEndian : qwordWrapper.ReadDoubleWordUsingQword;
                    methods.WriteDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordWriteMethod)qwordWrapper.WriteDoubleWordUsingQwordBigEndian : qwordWrapper.WriteDoubleWordUsingQword;
                }
                else if(wordWrapper != null && (allowedTranslations & AllowedTranslation.DoubleWordToWord) != 0)
                {
                    methods.ReadDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordReadMethod)wordWrapper.ReadDoubleWordUsingWordBigEndian : wordWrapper.ReadDoubleWordUsingWord;
                    methods.WriteDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordWriteMethod)wordWrapper.WriteDoubleWordUsingWordBigEndian : wordWrapper.WriteDoubleWordUsingWord;
                }
                else if(byteWrapper != null && (allowedTranslations & AllowedTranslation.DoubleWordToByte) != 0)
                {
                    methods.ReadDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordReadMethod)byteWrapper.ReadDoubleWordUsingByteBigEndian : byteWrapper.ReadDoubleWordUsingByte;
                    methods.WriteDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordWriteMethod)byteWrapper.WriteDoubleWordUsingByteBigEndian : byteWrapper.WriteDoubleWordUsingByte;
                }
                else if(qwordPeripheral != null && (allowedTranslations & AllowedTranslation.DoubleWordToQuadWord) != 0)
                {
                    methods.ReadDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordReadMethod)qwordPeripheral.ReadDoubleWordUsingQwordBigEndian : qwordPeripheral.ReadDoubleWordUsingQword;
                    methods.WriteDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordWriteMethod)qwordPeripheral.WriteDoubleWordUsingQwordBigEndian : qwordPeripheral.WriteDoubleWordUsingQword;
                }
                else if(wordPeripheral != null && (allowedTranslations & AllowedTranslation.DoubleWordToWord) != 0)
                {
                    methods.ReadDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordReadMethod)wordPeripheral.ReadDoubleWordUsingWordBigEndian : wordPeripheral.ReadDoubleWordUsingWord;
                    methods.WriteDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordWriteMethod)wordPeripheral.WriteDoubleWordUsingWordBigEndian : wordPeripheral.WriteDoubleWordUsingWord;
                }
                else if(bytePeripheral != null && (allowedTranslations & AllowedTranslation.DoubleWordToByte) != 0)
                {
                    methods.ReadDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordReadMethod)bytePeripheral.ReadDoubleWordUsingByteBigEndian : bytePeripheral.ReadDoubleWordUsingByte;
                    methods.WriteDoubleWord = translatedAccessNeedsSwap ? (BusAccess.DoubleWordWriteMethod)bytePeripheral.WriteDoubleWordUsingByteBigEndian : bytePeripheral.WriteDoubleWordUsingByte;
                }
                else
                {
                    methods.ReadDoubleWord = peripheral.ReadDoubleWordNotTranslated;
                    methods.WriteDoubleWord = peripheral.WriteDoubleWordNotTranslated;
                }
            }
            else if(matchingAccessNeedsSwap)
            {
                // if methods.ReadDoubleWord != null then we have a dwordWrapper
                methods.ReadDoubleWord = (BusAccess.DoubleWordReadMethod)dwordWrapper.ReadDoubleWordBigEndian;
                methods.WriteDoubleWord = (BusAccess.DoubleWordWriteMethod)dwordWrapper.WriteDoubleWordBigEndian;
            }
            if(methods.ReadQuadWord == null)
            {
                if(qwordPeripheral != null)
                {
                    methods.ReadQuadWord = matchingAccessNeedsSwap ? (BusAccess.QuadWordReadMethod)qwordPeripheral.ReadQuadWordBigEndian : qwordPeripheral.ReadQuadWord;
                    methods.WriteQuadWord = matchingAccessNeedsSwap ? (BusAccess.QuadWordWriteMethod)qwordPeripheral.WriteQuadWordBigEndian : qwordPeripheral.WriteQuadWord;
                }
                else if(dwordWrapper != null && (allowedTranslations & AllowedTranslation.QuadWordToDoubleWord) != 0)
                {
                    methods.ReadQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordReadMethod)dwordWrapper.ReadQuadWordUsingDwordBigEndian : dwordWrapper.ReadQuadWordUsingDword;
                    methods.WriteQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordWriteMethod)dwordWrapper.WriteQuadWordUsingDwordBigEndian : dwordWrapper.WriteQuadWordUsingDword;
                }
                else if(wordWrapper != null && (allowedTranslations & AllowedTranslation.QuadWordToWord) != 0)
                {
                    methods.ReadQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordReadMethod)wordWrapper.ReadQuadWordUsingWordBigEndian : wordWrapper.ReadQuadWordUsingWord;
                    methods.WriteQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordWriteMethod)wordWrapper.WriteQuadWordUsingWordBigEndian : wordWrapper.WriteQuadWordUsingWord;
                }
                else if(byteWrapper != null && (allowedTranslations & AllowedTranslation.QuadWordToByte) != 0)
                {
                    methods.ReadQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordReadMethod)byteWrapper.ReadQuadWordUsingByteBigEndian : byteWrapper.ReadQuadWordUsingByte;
                    methods.WriteQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordWriteMethod)byteWrapper.WriteQuadWordUsingByteBigEndian : byteWrapper.WriteQuadWordUsingByte;
                }
                else if(dwordPeripheral != null && (allowedTranslations & AllowedTranslation.QuadWordToDoubleWord) != 0)
                {
                    methods.ReadQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordReadMethod)dwordPeripheral.ReadQuadWordUsingDwordBigEndian : dwordPeripheral.ReadQuadWordUsingDword;
                    methods.WriteQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordWriteMethod)dwordPeripheral.WriteQuadWordUsingDwordBigEndian : dwordPeripheral.WriteQuadWordUsingDword;
                }
                else if(wordPeripheral != null && (allowedTranslations & AllowedTranslation.QuadWordToWord) != 0)
                {
                    methods.ReadQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordReadMethod)wordPeripheral.ReadQuadWordUsingWordBigEndian : wordPeripheral.ReadQuadWordUsingWord;
                    methods.WriteQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordWriteMethod)wordPeripheral.WriteQuadWordUsingWordBigEndian : wordPeripheral.WriteQuadWordUsingWord;
                }
                else if(bytePeripheral != null && (allowedTranslations & AllowedTranslation.QuadWordToByte) != 0)
                {
                    methods.ReadQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordReadMethod)bytePeripheral.ReadQuadWordUsingByteBigEndian : bytePeripheral.ReadQuadWordUsingByte;
                    methods.WriteQuadWord = translatedAccessNeedsSwap ? (BusAccess.QuadWordWriteMethod)bytePeripheral.WriteQuadWordUsingByteBigEndian : bytePeripheral.WriteQuadWordUsingByte;
                }
                else
                {
                    methods.ReadQuadWord = peripheral.ReadQuadWordNotTranslated;
                    methods.WriteQuadWord = peripheral.WriteQuadWordNotTranslated;
                }
            }
            else if(matchingAccessNeedsSwap)
            {
                // if methods.ReadQuadWord != null then we have a qwordWrapper
                methods.ReadQuadWord = (BusAccess.QuadWordReadMethod)qwordWrapper.ReadQuadWordBigEndian;
                methods.WriteQuadWord = (BusAccess.QuadWordWriteMethod)qwordWrapper.WriteQuadWordBigEndian;
            }
        }

        private void UpdateAccessMethods()
        {
            foreach(var peripherals in allPeripherals)
            {
                peripherals.VisitAccessMethods(null, pam =>
                {
                    if(pam.Tag != null)
                    {
                        FillAccessMethodsWithTaggedMethods(pam.Peripheral, pam.Tag, ref pam);
                    }
                    else
                    {
                        FillAccessMethodsWithDefaultMethods(pam.Peripheral, ref pam);
                    }
                    return pam;
                });
            }
        }

        private void RegisterInner(IBusPeripheral peripheral, PeripheralAccessMethods methods, BusRangeRegistration registrationPoint, ICPU context)
        {
            PeripheralCollection peripherals = null;

            using(Machine.ObtainPausedState(true))
            {
                // Register only for the selected core
                if(context != null)
                {
                    peripherals = cpuLocalPeripherals[context];
                }
                else
                {
                    peripherals = globalPeripherals;
                }

                var intersecting = GetPeripheralsForContext(context).FirstOrDefault(x => x.RegistrationPoint.Range.Intersects(registrationPoint.Range));
                if(intersecting != null)
                {
                    throw new RegistrationException($"Given address {registrationPoint.Range} for peripheral {peripheral} conflicts with address {intersecting.RegistrationPoint.Range} of peripheral {intersecting.Peripheral}", "address");
                }

                var registeredPeripheral = new BusRegistered<IBusPeripheral>(peripheral, registrationPoint);

                // we also have to put missing methods
                var absoluteAddressAware = peripheral as IAbsoluteAddressAware;
                if(absoluteAddressAware != null)
                {
                    methods.SetAbsoluteAddress = absoluteAddressAware.SetAbsoluteAddress;
                }
                peripherals.Add(registrationPoint.Range.StartAddress, registrationPoint.Range.EndAddress + 1, registeredPeripheral, methods);
                // let's add new mappings
                AddMappingsForPeripheral(peripheral, registrationPoint, context);
                Machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
                Machine.RegisterBusController(peripheral, this);
            }

            peripheralRegistered = true;
        }

        private IEnumerable<PeripheralLookupResult> FindTargets(ulong address, ulong count, ICPU context = null)
        {
            var result = new List<PeripheralLookupResult>();
            var written = 0UL;
            while(written < count)
            {
                var currentPosition = address + written;
                // what peripheral is at the current write position?
                var what = WhatIsAt(currentPosition, context);
                if(what == null)
                {
                    var holeStart = currentPosition;
                    // we can omit part of the array
                    // but how much?
                    var nextPeripheral = GetPeripheralsForContext(context).OrderBy(x => x.RegistrationPoint.Range.StartAddress).FirstOrDefault(x => x.RegistrationPoint.Range.StartAddress > currentPosition);
                    if(nextPeripheral == null)
                    {
                        // hole reaches the end of the required range
                        written = count;
                    }
                    else
                    {
                        written += Math.Min(nextPeripheral.RegistrationPoint.Range.StartAddress - currentPosition, count - written);
                    }
                    var holeSize = address + written - currentPosition;
                    this.Log(LogLevel.Warning, "Tried to access bytes at non-existing peripheral in range {0}.", new Range(holeStart, holeSize));
                    continue;
                }
                var toWrite = Math.Min(count - written, what.RegistrationPoint.Range.EndAddress - currentPosition + 1);
                var singleResult = new PeripheralLookupResult();
                singleResult.What = what;
                singleResult.SourceIndex = written;
                singleResult.SourceLength = toWrite;
                singleResult.Offset = currentPosition;
                written += toWrite;
                result.Add(singleResult);
            }

            return result;
        }

        private static void ThrowIfNotAllMemory(IEnumerable<PeripheralLookupResult> targets)
        {
            foreach(var target in targets)
            {
                var iMemory = target.What.Peripheral as IMemory;
                var redirector = target.What.Peripheral as Redirector;
                if(iMemory == null && redirector == null)
                {
                    throw new RecoverableException(String.Format("Tried to access {0} but only memory accesses were allowed.", target.What.Peripheral));
                }
            }
        }

        private void UpdatePageAccesses()
        {
            foreach(var address in hooksOnRead.Select(x => x.Key).Union(hooksOnWrite.Select(x => x.Key)))
            {
                SetPageAccessViaIo(address);
            }
        }

        private static IELF GetELFFromFile(ReadFilePath fileName)
        {
            try
            {
                if(!ELFReader.TryLoad(fileName, out var result))
                {
                    throw new RecoverableException($"Could not load ELF from path: {fileName}");
                }
                return result;
            }
            catch(Exception e)
            {
                // ELF creating exception are recoverable in the sense of emulator state
                throw new RecoverableException(string.Format("Error while loading ELF: {0}.", e.Message), e);
            }
        }

        private void ClearAll()
        {
            lock(cpuSync)
            {
                foreach(var group in Machine.PeripheralsGroups.ActiveGroups)
                {
                    group.Unregister();
                }

                foreach(var p in allPeripherals.SelectMany(x => x.Peripherals).Select(x => x.Peripheral).Distinct().Union(GetCPUs().Cast<IPeripheral>()).ToList())
                {
                    Machine.UnregisterFromParent(p);
                }

                mappingsRemoved = false;
                InitStructures();
            }
        }

        private void UpdateLowestLoadedAddress(ulong lowestLoadedAddress)
        {
            if(!LowestLoadedAddress.HasValue)
            {
                LowestLoadedAddress = lowestLoadedAddress;
                return;
            }
            LowestLoadedAddress = Math.Min(LowestLoadedAddress.Value, lowestLoadedAddress);
        }

        private void AddFingerprint(string fileName)
        {
            binaryFingerprints.Add(new BinaryFingerprint(fileName));
        }

        private void InitStructures()
        {
            cpuById.Clear();
            idByCpu.Clear();
            hooksOnRead.Clear();
            hooksOnWrite.Clear();
            pcCache.Invalidate();
            Lookup = new SymbolLookup();
            cachedCpuId = new ThreadLocal<int>();
            globalPeripherals = new PeripheralCollection(this);
            cpuLocalPeripherals = new Dictionary<ICPU, PeripheralCollection>();
            lockedPeripherals = new HashSet<IPeripheral>();
            lockedRangesCollection = new MinimalRangesCollection();
            mappingsForPeripheral = new Dictionary<IBusPeripheral, List<MappedSegmentWrapper>>();
            tags = new Dictionary<Range, TagEntry>();
            svdDevices = new List<SVDParser>();
            pausingTags = new HashSet<string>();
            PostDeserializationInitStructures();
        }

        private List<MappedMemory> ObtainMemoryList()
        {
            return allPeripherals.SelectMany(x => x.Peripherals).Where(x => x.Peripheral is MappedMemory).OrderBy(x => x.RegistrationPoint.Range.StartAddress).
                Select(x => x.Peripheral).Cast<MappedMemory>().Distinct().ToList();
        }

        private void AddMappings(IEnumerable<MappedSegmentWrapper> newMappings, IBusPeripheral owner)
        {
            using(Machine.ObtainPausedState(true))
            {
                lock(cpuSync)
                {
                    var mappingsList = newMappings.ToList();
                    if(mappingsForPeripheral.ContainsKey(owner))
                    {
                        mappingsForPeripheral[owner].AddRange(newMappings);
                    }
                    else
                    {
                        mappingsForPeripheral[owner] = mappingsList;
                    }
                    // Old mappings are given to the CPU in the moment of its registration.
                    foreach(var mapping in mappingsList)
                    {
                        if(mapping.Context != null)
                        {
                            mapping.Context.MapMemory(mapping);
                        }
                        else
                        {
                            foreach(var cpu in idByCpu.Keys.OfType<ICPUWithMappedMemory>())
                            {
                                cpu.MapMemory(mapping);
                            }
                        }
                    }
                }
            }
        }

        private void AddMappingsForPeripheral(IBusPeripheral peripheral, BusRangeRegistration registrationPoint, ICPU context)
        {
            var mappedPeripheral = peripheral as IMapped;
            if(mappedPeripheral == null) // The context can be null to map it globally
            {
                return;
            }
            var cpuWithMappedMemory = context as ICPUWithMappedMemory;
            var segments = mappedPeripheral.MappedSegments;
            var mappings = segments.Select(x => FromRegistrationPointToSegmentWrapper(x, registrationPoint, cpuWithMappedMemory)).Where(x => x != null);
            AddMappings(mappings, mappedPeripheral);
        }

        private bool RemoveMappingsForPeripheral(IBusPeripheral peripheral)
        {
            if(!mappingsForPeripheral.ContainsKey(peripheral))
            {
                return false;
            }
            foreach(var mapping in mappingsForPeripheral[peripheral])
            {
                UnmapMemory(new Range(mapping.StartingOffset, mapping.Size));
            }
            mappingsForPeripheral.Remove(peripheral);
            return true;
        }

        private string TryGetTag(ulong address, out ulong defaultValue)
        {
            var tag = tags.FirstOrDefault(x => x.Key.Contains(address));
            defaultValue = default(ulong);
            if(tag.Key == Range.Empty)
            {
                return null;
            }
            defaultValue = tag.Value.DefaultValue;
            return tag.Value.Name;
        }

        private string EnterTag(string str, ulong address, out bool tagEntered, out ulong defaultValue)
        {
            // TODO: also pausing here in a bit hacky way
            var tag = TryGetTag(address, out defaultValue);
            if(tag == null)
            {
                tagEntered = false;
                return str;
            }
            tagEntered = true;
            if(pausingTags.Contains(tag))
            {
                Machine.Pause();
            }
            return string.Format("(tag: '{0}') {1}", tag, str);
        }

        private ulong ReportNonExistingRead(ulong address, SysbusAccessWidth type)
        {
            Interlocked.Increment(ref unexpectedReads);
            bool tagged;
            ulong defaultValue;
            var warning = EnterTag(NonExistingRead, address, out tagged, out defaultValue);
            warning = DecorateWithCPUNameAndPC(warning);
            if(UnhandledAccessBehaviour == UnhandledAccessBehaviour.DoNotReport)
            {
                return defaultValue;
            }
            if((UnhandledAccessBehaviour == UnhandledAccessBehaviour.ReportIfTagged && !tagged)
                || (UnhandledAccessBehaviour == UnhandledAccessBehaviour.ReportIfNotTagged && tagged))
            {
                return defaultValue;
            }
            if(tagged)
            {
                //strange parsing of default value ensures we get the right width, depending on the access type
                this.Log(LogLevel.Warning, warning.TrimEnd('.') + ", returning 0x{2}.", address, type, defaultValue.ToString("X16").Substring(16 - (int)type * 2));
            }
            else
            {
                ulong value;
                foreach(var svdDevice in svdDevices)
                {
                    if(svdDevice.TryReadAccess(address, out value, type))
                    {
                        return value;
                    }
                }
                this.Log(LogLevel.Warning, warning, address, type);
            }
            return defaultValue;
        }

        private void ReportNonExistingWrite(ulong address, ulong value, SysbusAccessWidth type)
        {
            Interlocked.Increment(ref unexpectedWrites);
            if(UnhandledAccessBehaviour == UnhandledAccessBehaviour.DoNotReport)
            {
                return;
            }
            bool tagged;
            var warning = EnterTag(NonExistingWrite, address, out tagged, out var _);
            warning = DecorateWithCPUNameAndPC(warning);
            if((UnhandledAccessBehaviour == UnhandledAccessBehaviour.ReportIfTagged && !tagged)
                || (UnhandledAccessBehaviour == UnhandledAccessBehaviour.ReportIfNotTagged && tagged))
            {
                return;
            }
            foreach(var svdDevice in svdDevices)
            {
                if(svdDevice.TryWriteAccess(address, value, type))
                {
                    return;
                }
            }
            this.Log(LogLevel.Warning, warning, address, value, type);
        }

        private IDisposable SetLocalContext(ICPU cpu)
        {
            return threadLocalContext.Initialize(cpu);
        }

        [PostDeserialization]
        private void PostDeserializationInitStructures()
        {
            threadLocalContext = new ThreadLocalContext(this);
        }

        private static MappedSegmentWrapper FromRegistrationPointToSegmentWrapper(IMappedSegment segment, BusRangeRegistration registrationPoint, ICPUWithMappedMemory context)
        {
            if(segment.StartingOffset >= registrationPoint.Range.Size + registrationPoint.Offset)
            {
                return null;
            }

            var desiredSize = Math.Min(segment.Size, registrationPoint.Range.Size + registrationPoint.Offset - segment.StartingOffset);
            return new MappedSegmentWrapper(segment, registrationPoint.Range.StartAddress - registrationPoint.Offset, desiredSize, context);
        }

        private IEnumerable<PeripheralCollection> allPeripherals => cpuLocalPeripherals.Values.Concat(new [] { globalPeripherals }).Distinct();

        private PeripheralCollection globalPeripherals;
        private Dictionary<ICPU, PeripheralCollection> cpuLocalPeripherals;
        private ISet<IPeripheral> lockedPeripherals;
        private MinimalRangesCollection lockedRangesCollection;
        private Dictionary<IBusPeripheral, List<MappedSegmentWrapper>> mappingsForPeripheral;
        private bool mappingsRemoved;
        private bool peripheralRegistered;
        private Endianess endianess;
        private bool hasCpuWithMismatchedEndianness;
        private readonly Dictionary<ICPU, int> idByCpu;
        private readonly Dictionary<int, ICPU> cpuById;
        private readonly Dictionary<ulong, List<BusHookHandler>> hooksOnRead;
        private readonly Dictionary<ulong, List<BusHookHandler>> hooksOnWrite;

        [Constructor]
        private ThreadLocal<int> cachedCpuId;
        [Transient]
        private ThreadLocalContext threadLocalContext;
        private object cpuSync;
        private Dictionary<Range, TagEntry> tags;
        private List<SVDParser> svdDevices;
        private HashSet<string> pausingTags;
        private readonly List<BinaryFingerprint> binaryFingerprints;
        private const string NonExistingRead = "Read{1} from non existing peripheral at 0x{0:X}.";
        private const string NonExistingWrite = "Write{2} to non existing peripheral at 0x{0:X}, value 0x{1:X}.";
        private const string IOExceptionMessage = "I/O error while loading ELF: {0}.";
        private const string CantFindCpuIdMessage = "Can't verify current CPU in the given context.";
        private const bool Overlap = true;
        // TODO

        private int unexpectedReads;
        private int unexpectedWrites;

        private LRUCache<ulong, Tuple<string, Symbol>> pcCache = new LRUCache<ulong, Tuple<string, Symbol>>(10000);

        public class MappedSegmentWrapper : IMappedSegment
        {
            public MappedSegmentWrapper(IMappedSegment wrappedSegment, ulong peripheralOffset, ulong maximumSize, ICPUWithMappedMemory context)
            {
                this.wrappedSegment = wrappedSegment;
                this.peripheralOffset = peripheralOffset;
                usedSize = Math.Min(maximumSize, wrappedSegment.Size);
                this.context = context;
            }

            public void Touch()
            {
                wrappedSegment.Touch();
            }

            public override string ToString()
            {
                return string.Format("[MappedSegmentWrapper: StartingOffset=0x{0:X}, Size=0x{1:X}, OriginalStartingOffset=0x{2:X}, PeripheralOffset=0x{3:X}, Context={4}]",
                    StartingOffset, Size, OriginalStartingOffset, PeripheralOffset, context);
            }

            public ICPUWithMappedMemory Context
            {
                get
                {
                    return context;
                }
            }

            public ulong StartingOffset
            {
                get
                {
                    return peripheralOffset + wrappedSegment.StartingOffset;
                }
            }

            public ulong Size
            {
                get
                {
                    return usedSize;
                }
            }

            public IntPtr Pointer
            {
                get
                {
                    return wrappedSegment.Pointer;
                }
            }

            public ulong OriginalStartingOffset
            {
                get
                {
                    return wrappedSegment.StartingOffset;
                }
            }

            public ulong PeripheralOffset
            {
                get
                {
                    return peripheralOffset;
                }
            }

            public override bool Equals(object obj)
            {
                var objAsMappedSegmentWrapper = obj as MappedSegmentWrapper;
                if(objAsMappedSegmentWrapper == null)
                {
                    return false;
                }

                return wrappedSegment.Equals(objAsMappedSegmentWrapper.wrappedSegment)
                    && peripheralOffset == objAsMappedSegmentWrapper.peripheralOffset
                    && usedSize == objAsMappedSegmentWrapper.usedSize;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 23 + wrappedSegment.GetHashCode();
                    hash = hash * 23 + (int)peripheralOffset;
                    hash = hash * 23 + (int)usedSize;
                    return hash;
                }
            }

            private readonly IMappedSegment wrappedSegment;
            private readonly ulong peripheralOffset;
            private readonly ulong usedSize;
            private readonly ICPUWithMappedMemory context;
        }

        private struct PeripheralLookupResult
        {
            public IBusRegistered<IBusPeripheral> What;
            public ulong Offset;
            public ulong SourceIndex;
            public ulong SourceLength;
        }

        private struct TagEntry
        {
            public string Name;
            public ulong DefaultValue;
        }

        private class ThreadLocalContext : IDisposable
        {
            public ThreadLocalContext(IBusController parent)
            {
                this.parent = parent;
                context = new ThreadLocal<int?>(() => null, true);
                emptyDisposable.Disable();
            }

            public IDisposable Initialize(ICPU cpu)
            {
                if(cpu == null)
                {
                    return emptyDisposable;
                }
                var previousContext = context.Value;
                context.Value = parent.GetCPUId(cpu);
                return DisposableWrapper.New(() =>
                {
                    context.Value = previousContext;
                });
            }

            public void Dispose()
            {
                context.Dispose();
            }

            public bool InUse => context.Value.HasValue;
            public int CPUId => context.Value.Value;

            private readonly ThreadLocal<int?> context;
            private readonly IBusController parent;

            private static readonly DisposableWrapper emptyDisposable = new DisposableWrapper();
        }
    }
}

