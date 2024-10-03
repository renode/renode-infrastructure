//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Memory;
using ELFSharp.ELF;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Bus
{
    public abstract class BusControllerProxy : IBusController
    {
        public void Reset()
        {
            ParentController.Reset();
        }

        public BusControllerProxy(IBusController parentController)
        {
            ParentController = parentController;
        }

        public virtual byte ReadByte(ulong address, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                return ParentController.ReadByte(address, context);
            }
            return (byte)0;
        }

        public virtual void WriteByte(ulong address, byte value, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteByte(address, value, context);
            }
        }

        public virtual ushort ReadWord(ulong address, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                return ParentController.ReadWord(address, context);
            }
            return (ushort)0;
        }

        public virtual void WriteWord(ulong address, ushort value, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteWord(address, value, context);
            }
        }

        public virtual uint ReadDoubleWord(ulong address, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                return ParentController.ReadDoubleWord(address, context);
            }
            return (uint)0;
        }

        public virtual void WriteDoubleWord(ulong address, uint value, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteDoubleWord(address, value, context);
            }
        }

        public virtual void ReadBytes(ulong address, int count, byte[] destination, int startIndex, bool onlyMemory = false, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                ParentController.ReadBytes(address, count, destination, startIndex, onlyMemory, context);
            }
        }

        public virtual byte[] ReadBytes(ulong address, int count, bool onlyMemory = false, ICPU context = null)
        {
            var result = new byte[count];
            ReadBytes(address, count, result, 0, onlyMemory, context);
            return result;
        }

        public virtual byte[] ReadBytes(long offset, int count, ICPU context = null)
        {
            return ReadBytes((ulong)offset, count, context: context);
        }

        public virtual void WriteBytes(byte[] bytes, ulong address, bool onlyMemory = false, ICPU context = null)
        {
            WriteBytes(bytes, address, bytes.Length, onlyMemory, context);
        }

        public virtual void WriteBytes(byte[] bytes, ulong address, int startingIndex, long count, bool onlyMemory = false, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteBytes(bytes, address, startingIndex, count, onlyMemory, context);
            }
        }

        public virtual void WriteBytes(byte[] bytes, ulong address, long count, bool onlyMemory = false, ICPU context = null)
        {
            WriteBytes(bytes, address, 0, count, onlyMemory, context);
        }

        public virtual void WriteBytes(long offset, byte[] array, int startingIndex, int count, ICPU context = null)
        {
            WriteBytes(array, (ulong)offset, startingIndex, count, context: context);
        }

        public virtual IBusRegistered<IBusPeripheral> WhatIsAt(ulong address, ICPU context = null)
        {
            ValidateOperation(ref address, BusAccessPrivileges.Other, context);
            return ParentController.WhatIsAt(address, context);
        }

        public virtual IPeripheral WhatPeripheralIsAt(ulong address, ICPU context = null)
        {
            ValidateOperation(ref address, BusAccessPrivileges.Other, context);
            return ParentController.WhatPeripheralIsAt(address, context);
        }

        public virtual IEnumerable<ICPU> GetCPUs()
        {
            return ParentController.GetCPUs();
        }

        public virtual int GetCPUSlot(ICPU cpu)
        {
            return ParentController.GetCPUSlot(cpu);
        }

        public virtual bool TryGetCurrentCPU(out ICPU cpu)
        {
            return ParentController.TryGetCurrentCPU(out cpu);
        }

        public virtual ICPU GetCurrentCPU()
        {
            return ParentController.GetCurrentCPU();
        }

        public virtual IEnumerable<IBusRegistered<IBusPeripheral>> GetRegisteredPeripherals(ICPU context = null)
        {
            return ParentController.GetRegisteredPeripherals(context);
        }

        public virtual IEnumerable<BusRangeRegistration> GetRegistrationPoints(IBusPeripheral peripheral)
        {
            return ParentController.GetRegistrationPoints(peripheral);
        }

        public virtual string DecorateWithCPUNameAndPC(string str)
        {
            return ParentController.DecorateWithCPUNameAndPC(str);
        }

        public virtual void AddWatchpointHook(ulong address, SysbusAccessWidth width, Access access, BusHookDelegate hook)
        {
            ParentController.AddWatchpointHook(address, width, access, hook);
        }

        public virtual void SetHookAfterPeripheralRead<T>(IBusPeripheral peripheral, Func<T, long, T> hook, Range? subrange = null)
        {
            ParentController.SetHookAfterPeripheralRead(peripheral, hook, subrange);
        }

        public virtual void SetHookBeforePeripheralWrite<T>(IBusPeripheral peripheral, Func<T, long, T> hook, Range? subrange = null)
        {
            ParentController.SetHookBeforePeripheralWrite(peripheral, hook, subrange);
        }

        public virtual void ClearHookAfterPeripheralRead<T>(IBusPeripheral peripheral)
        {
            ParentController.ClearHookAfterPeripheralRead<T>(peripheral);
        }

        public virtual void RemoveWatchpointHook(ulong address, BusHookDelegate hook)
        {
            ParentController.RemoveWatchpointHook(address, hook);
        }

        public virtual bool TryGetWatchpointsAt(ulong address, Access access, out List<BusHookHandler> result)
        {
            return ParentController.TryGetWatchpointsAt(address, access, out result);
        }

        public virtual string FindSymbolAt(ulong offset, ICPU context = null)
        {
            return ParentController.FindSymbolAt(offset, context);
        }

        public virtual bool IsAddressRangeLocked(Range range, ICPU context = null)
        {
            return ParentController.IsAddressRangeLocked(range, context);
        }

        public virtual void SetAddressRangeLocked(Range range, bool locked, ICPU context = null)
        {
            ParentController.SetAddressRangeLocked(range, locked, context);
        }

        public virtual void DisablePeripheral(IPeripheral peripheral)
        {
            ParentController.DisablePeripheral(peripheral);
        }

        public virtual void EnablePeripheral(IPeripheral peripheral)
        {
            ParentController.EnablePeripheral(peripheral);
        }

        public virtual void SetPeripheralEnabled(IPeripheral peripheral, bool enabled)
        {
            ParentController.SetPeripheralEnabled(peripheral, enabled);
        }

        public virtual bool TryFindSymbolAt(ulong offset, out string name, out Symbol symbol, ICPU context = null)
        {
            return ParentController.TryFindSymbolAt(offset, out name, out symbol, context);
        }

        public virtual ulong ReadQuadWord(ulong address, ICPU context = null)
        {
            return ParentController.ReadQuadWord(address, context);
        }

        public virtual void WriteQuadWord(ulong address, ulong value, ICPU context = null)
        {
            ParentController.WriteQuadWord(address, value, context);
        }

        public virtual bool IsPeripheralEnabled(IPeripheral peripheral)
        {
            return ParentController.IsPeripheralEnabled(peripheral);
        }

        public virtual void Register(IBusPeripheral peripheral, BusRangeRegistration registrationPoint)
        {
            ParentController.Register(peripheral, registrationPoint);
        }

        public virtual void Register(IKnownSize peripheral, BusPointRegistration registrationPoint)
        {
            ParentController.Register(peripheral, registrationPoint);
        }

        public virtual void Register(IBusPeripheral peripheral, BusMultiRegistration registrationPoint)
        {
            ParentController.Register(peripheral, registrationPoint);
        }

        public virtual void Register(IBusPeripheral peripheral, BusParametrizedRegistration registrationPoint)
        {
            ParentController.Register(peripheral, registrationPoint);
        }

        public virtual void EnableAllTranslations(bool enable = true)
        {
            ParentController.EnableAllTranslations(enable);
        }

        public virtual void EnableAllTranslations(IBusPeripheral busPeripheral, bool enable = true)
        {
            ParentController.EnableAllTranslations(busPeripheral, enable);
        }

        public void MoveRegistrationWithinContext(IBusPeripheral peripheral, BusRangeRegistration newRegistration, ICPU context, Func<IEnumerable<IBusRegistered<IBusPeripheral>>, IBusRegistered<IBusPeripheral>> selector = null)
        {
            ParentController.MoveRegistrationWithinContext(peripheral, newRegistration, context, selector);
        }

        void IPeripheralRegister<IBusPeripheral, BusMultiRegistration>.Unregister(IBusPeripheral peripheral)
        {
            ((IPeripheralRegister<IBusPeripheral, BusMultiRegistration>)ParentController).Unregister(peripheral);
        }

        void IPeripheralRegister<IBusPeripheral, BusRangeRegistration>.Unregister(IBusPeripheral peripheral)
        {
            ((IPeripheralRegister<IBusPeripheral, BusRangeRegistration>)ParentController).Unregister(peripheral);
        }

        void IPeripheralRegister<IBusPeripheral, BusParametrizedRegistration>.Unregister(IBusPeripheral peripheral)
        {
            ((IPeripheralRegister<IBusPeripheral, BusParametrizedRegistration>)ParentController).Unregister(peripheral);
        }

        public void Unregister(IPeripheral peripheral)
        {
            ParentController.Unregister(peripheral);
        }

        public void Register(IPeripheral peripheral, NullRegistrationPoint registrationPoint)
        {
            ParentController.Register(peripheral, registrationPoint);
        }

        public virtual void Unregister(ICPU peripheral)
        {
            ParentController.Unregister(peripheral);
        }

        public virtual void Unregister(IKnownSize peripheral)
        {
            ParentController.Unregister(peripheral);
        }

        public virtual void ZeroRange(Range range, ICPU context = null)
        {
            ParentController.ZeroRange(range, context);
        }

        public virtual void Register(ICPU cpu, CPURegistrationPoint registrationPoint)
        {
            ParentController.Register(cpu, registrationPoint);
        }

        public virtual void UnregisterFromAddress(ulong address, ICPU context = null)
        {
            ParentController.UnregisterFromAddress(address, context);
        }

        public virtual IBusRegistered<MappedMemory> FindMemory(ulong address, ICPU context = null)
        {
            return ParentController.FindMemory(address, context);
        }

        public virtual void LoadELF(ReadFilePath fileName, bool useVirtualAddress = false, bool allowLoadsOnlyToMemory = true, ICluster<IInitableCPU> cpu = null)
        {
            ParentController.LoadELF(fileName, useVirtualAddress, allowLoadsOnlyToMemory, cpu);
        }

        public virtual void LoadFileChunks(string path, IEnumerable<FileChunk> chunks, ICPU cpu)
        {
            ParentController.LoadFileChunks(path, chunks, cpu);
        }

        public virtual void Tag(Range range, string tag, ulong defaultValue = 0, bool pausing = false)
        {
            ParentController.Tag(range, tag, defaultValue, pausing);
        }

        public virtual void ApplySVD(string path)
        {
            ParentController.ApplySVD(path);
        }

        public virtual void LoadUImage(ReadFilePath fileName, IInitableCPU cpu = null)
        {
            ParentController.LoadUImage(fileName, cpu);
        }

        public virtual void RemoveAllWatchpointHooks(ulong address)
        {
            ParentController.RemoveAllWatchpointHooks(address);
        }

        public virtual void MapMemory(IMappedSegment segment, IBusPeripheral owner, bool relative = true, ICPUWithMappedMemory context = null)
        {
            ParentController.MapMemory(segment, owner, relative, context);
        }

        public virtual SymbolLookup GetLookup(ICPU context = null)
        {
            return ParentController.GetLookup(context);
        }

        public virtual bool TryGetAllSymbolAddresses(string symbolName, out IEnumerable<ulong> symbolAddresses, ICPU context = null)
        {
            return ParentController.TryGetAllSymbolAddresses(symbolName, out symbolAddresses, context);
        }

        public virtual IMachine Machine => ParentController.Machine;

        public virtual IEnumerable<IRegistered<IBusPeripheral, BusRangeRegistration>> Children => ParentController.Children;

        public virtual bool IsMultiCore => ParentController.IsMultiCore;

        public virtual IBusController ParentController { get; protected set; }

        public virtual Endianess Endianess => ParentController.Endianess;

        protected virtual bool ValidateOperation(ref ulong address, BusAccessPrivileges accessType, ICPU context = null)
        {
            return true;
        }

        event Action<IMachine> IBusController.OnSymbolsChanged
        {
            add
            {
                ParentController.OnSymbolsChanged += value;
            }

            remove
            {
                ParentController.OnSymbolsChanged -= value;
            }
        }
    }
}
