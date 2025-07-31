//
// Copyright (c) 2010-2025 Antmicro
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

        public virtual byte ReadByte(ulong address, IPeripheral context = null, ulong? cpuState = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                return ParentController.ReadByte(address, context, cpuState);
            }
            return (byte)0;
        }

        public virtual byte ReadByteWithState(ulong address, IPeripheral context, IContextState stateObj)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                return ParentController.ReadByteWithState(address, context, stateObj);
            }
            return (byte)0;
        }

        public virtual void WriteByte(ulong address, byte value, IPeripheral context = null, ulong? cpuState = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteByte(address, value, context, cpuState);
            }
        }

        public virtual void WriteByteWithState(ulong address, byte value, IPeripheral context, IContextState stateObj)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteByteWithState(address, value, context, stateObj);
            }
        }

        public virtual ushort ReadWord(ulong address, IPeripheral context = null, ulong? cpuState = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                return ParentController.ReadWord(address, context, cpuState);
            }
            return (ushort)0;
        }

        public virtual ushort ReadWordWithState(ulong address, IPeripheral context, IContextState stateObj)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                return ParentController.ReadWordWithState(address, context, stateObj);
            }
            return (ushort)0;
        }

        public virtual void WriteWord(ulong address, ushort value, IPeripheral context = null, ulong? cpuState = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteWord(address, value, context, cpuState);
            }
        }

        public virtual void WriteWordWithState(ulong address, ushort value, IPeripheral context, IContextState stateObj)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteWordWithState(address, value, context, stateObj);
            }
        }

        public virtual uint ReadDoubleWord(ulong address, IPeripheral context = null, ulong? cpuState = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                return ParentController.ReadDoubleWord(address, context, cpuState);
            }
            return (uint)0;
        }

        public virtual uint ReadDoubleWordWithState(ulong address, IPeripheral context, IContextState stateObj)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context)) // todo state
            {
                return ParentController.ReadDoubleWordWithState(address, context, stateObj);
            }
            return (uint)0;
        }

        public virtual void WriteDoubleWord(ulong address, uint value, IPeripheral context = null, ulong? cpuState = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteDoubleWord(address, value, context, cpuState);
            }
        }

        public virtual void WriteDoubleWordWithState(ulong address, uint value, IPeripheral context, IContextState stateObj)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteDoubleWordWithState(address, value, context, stateObj);
            }
        }

        public virtual void ReadBytes(ulong address, int count, byte[] destination, int startIndex, bool onlyMemory = false, IPeripheral context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                ParentController.ReadBytes(address, count, destination, startIndex, onlyMemory, context);
            }
        }

        public virtual byte[] ReadBytes(ulong address, int count, bool onlyMemory = false, IPeripheral context = null)
        {
            var result = new byte[count];
            ReadBytes(address, count, result, 0, onlyMemory, context);
            return result;
        }

        public virtual byte[] ReadBytes(long offset, int count, IPeripheral context = null)
        {
            return ReadBytes((ulong)offset, count, context: context);
        }

        public virtual void WriteBytes(byte[] bytes, ulong address, bool onlyMemory = false, IPeripheral context = null)
        {
            WriteBytes(bytes, address, bytes.Length, onlyMemory, context);
        }

        public virtual void WriteBytes(byte[] bytes, ulong address, int startingIndex, long count, bool onlyMemory = false, IPeripheral context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteBytes(bytes, address, startingIndex, count, onlyMemory, context);
            }
        }

        public virtual void WriteBytes(byte[] bytes, ulong address, long count, bool onlyMemory = false, IPeripheral context = null)
        {
            WriteBytes(bytes, address, 0, count, onlyMemory, context);
        }

        public virtual void WriteBytes(long offset, byte[] array, int startingIndex, int count, IPeripheral context = null)
        {
            WriteBytes(array, (ulong)offset, startingIndex, count, context: context);
        }

        public virtual bool TryConvertStateToUlongForContext(IPeripheral context, IContextState stateObj, out ulong? state)
        {
            state = null;
            if(!ParentController.TryConvertStateToUlongForContext(context, stateObj, out state))
            {
                return false;
            }
            return true;
        }

        public virtual IBusRegistered<IBusPeripheral> WhatIsAt(ulong address, IPeripheral context = null)
        {
            ValidateOperation(ref address, BusAccessPrivileges.Other, context);
            return ParentController.WhatIsAt(address, context);
        }

        public virtual IPeripheral WhatPeripheralIsAt(ulong address, IPeripheral context = null)
        {
            ValidateOperation(ref address, BusAccessPrivileges.Other, context);
            return ParentController.WhatPeripheralIsAt(address, context);
        }

        public virtual IEnumerable<ICPU> GetCPUs()
        {
            return ParentController.GetCPUs();
        }

        public IEnumerable<IPeripheral> GetAllContextKeys()
        {
            return ParentController.GetAllContextKeys();
        }

        public virtual int GetCPUSlot(ICPU cpu)
        {
            return ParentController.GetCPUSlot(cpu);
        }

        public virtual bool TryGetCurrentCPU(out ICPU cpu)
        {
            return ParentController.TryGetCurrentCPU(out cpu);
        }

        public virtual bool TryGetCurrentContextState<T>(out IPeripheralWithTransactionState context, out T stateObj)
        {
            return ParentController.TryGetCurrentContextState(out context, out stateObj);
        }

        public virtual ICPU GetCurrentCPU()
        {
            return ParentController.GetCurrentCPU();
        }

        public virtual IEnumerable<IBusRegistered<IBusPeripheral>> GetRegisteredPeripherals(IPeripheral context = null)
        {
            return ParentController.GetRegisteredPeripherals(context);
        }

        public IEnumerable<IBusRegistered<IBusPeripheral>> GetRegistrationsForPeripheralType<T>(IPeripheral context = null)
        {
            return ParentController.GetRegistrationsForPeripheralType<T>(context);
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

        public virtual bool IsAddressRangeLocked(Range range, IPeripheral context = null)
        {
            return ParentController.IsAddressRangeLocked(range, context);
        }

        public virtual void SetAddressRangeLocked(Range range, bool locked, IPeripheral context = null)
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

        public virtual bool TryFindSymbolAt(ulong offset, out string name, out Symbol symbol, ICPU context = null, bool functionOnly = false)
        {
            return ParentController.TryFindSymbolAt(offset, out name, out symbol, context, functionOnly);
        }

        public virtual ulong ReadQuadWord(ulong address, IPeripheral context = null, ulong? cpuState = null)
        {
            return ParentController.ReadQuadWord(address, context, cpuState);
        }

        public virtual ulong ReadQuadWordWithState(ulong address, IPeripheral context, IContextState stateObj)
        {
            return ParentController.ReadQuadWordWithState(address, context, stateObj);
        }

        public virtual void WriteQuadWord(ulong address, ulong value, IPeripheral context = null, ulong? cpuState = null)
        {
            ParentController.WriteQuadWord(address, value, context, cpuState);
        }

        public virtual void WriteQuadWordWithState(ulong address, ulong value, IPeripheral context, IContextState stateObj)
        {
            ParentController.WriteQuadWordWithState(address, value, context, stateObj);
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

        public void ChangePeripheralAccessCondition(IBusPeripheral peripheral, string newCondition, string oldCondition = null)
        {
            ParentController.ChangePeripheralAccessCondition(peripheral, newCondition, oldCondition);
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

        public virtual void ZeroRange(Range range, IPeripheral context = null)
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

        public virtual bool IsMemory(ulong address, ICPU context = null)
        {
            return ParentController.IsMemory(address, context);
        }

        public virtual void LoadFileChunks(string path, IEnumerable<FileChunk> chunks, IPeripheral cpu)
        {
            ParentController.LoadFileChunks(path, chunks, cpu);
        }

        public virtual void Tag(Range range, string tag, ulong defaultValue = 0, bool pausing = false, bool silent = false)
        {
            ParentController.Tag(range, tag, defaultValue, pausing, silent);
        }

        public virtual void ApplySVD(string path)
        {
            ParentController.ApplySVD(path);
        }

        public void LoadSymbolsFrom(IELF elf, bool useVirtualAddress = false, ulong? textAddress = null, ICPU context = null)
        {
            ParentController.LoadSymbolsFrom(elf, useVirtualAddress, textAddress, context);
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

        public virtual IReadOnlyDictionary<string, int> GetCommonStateBits()
        {
            return ParentController.GetCommonStateBits();
        }

        public virtual IReadOnlyDictionary<string, int> GetStateBits(string initiatorName)
        {
            return ParentController.GetStateBits(initiatorName);
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

        protected virtual bool ValidateOperation(ref ulong address, BusAccessPrivileges accessType, IPeripheral context = null)
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
