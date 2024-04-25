﻿/********************************************************
*
* Warning!
* This file was generated automatically.
* Please do not edit. Changes should be made in the
* appropriate *.tt file.
*
*/

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Peripherals.CPU;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Bus
{
    public partial class SystemBus
    {
        public byte ReadByte(ulong address, ICPU context = null)
        {
            var accessWidth = SysbusAccessWidth.Byte;
            if(IsAddressRangeLocked(address.By((ulong)accessWidth)))
            {
                this.Log(LogLevel.Warning, "Tried to read {0} bytes at 0x{1:X} which is inside a locked address range, returning 0",
                    (uint)accessWidth, address);
                return 0;
            }

            using(SetLocalContext(context))
            {
                ulong startAddress, endAddress;

                var accessMethods = globalPeripherals.FindAccessMethods(address, out startAddress, out endAddress);
                if(accessMethods == null)
                {
                    if(context != null)
                    {
                        accessMethods = cpuLocalPeripherals[context].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                    else if(TryGetCurrentCPU(out var currentCPU))
                    {
                        accessMethods = cpuLocalPeripherals[currentCPU].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                }
                if(accessMethods == null)
                {
                    return (byte)ReportNonExistingRead(address, accessWidth);
                }
                if(!IsPeripheralEnabled(accessMethods.Peripheral))
                {
                    this.Log(LogLevel.Warning, "Tried to read a locked peripheral: {0}. Address 0x{1:X}.", accessMethods.Peripheral, address);
                    return 0;
                }
                var lockTaken = false;
                try
                {
                    if(!accessMethods.Lock.IsHeldByCurrentThread)
                    {
                        accessMethods.Lock.Enter(ref lockTaken);
                    }
                    if(accessMethods.SetAbsoluteAddress != null)
                    {
                        accessMethods.SetAbsoluteAddress(address);
                    }
                    return accessMethods.ReadByte(checked((long)(address - startAddress)));
                }
                finally
                {
                    if(lockTaken)
                    {
                        accessMethods.Lock.Exit();
                    }
                }
            }
        }

        public void WriteByte(ulong address, byte value, ICPU context = null)
        {
            var accessWidth = SysbusAccessWidth.Byte;
            if(IsAddressRangeLocked(address.By((ulong)accessWidth)))
            {
                this.Log(LogLevel.Warning, "Tried to write {0} bytes (0x{1:X}) at 0x{2:X} which is inside a locked address range, write ignored",
                    (uint)accessWidth, value, address);
                return;
            }

            using(SetLocalContext(context))
            {
                ulong startAddress, endAddress;

                var accessMethods = globalPeripherals.FindAccessMethods(address, out startAddress, out endAddress);
                if(accessMethods == null)
                {
                    if(context != null)
                    {
                        accessMethods = cpuLocalPeripherals[context].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                    else if(TryGetCurrentCPU(out var currentCPU))
                    {
                        accessMethods = cpuLocalPeripherals[currentCPU].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                }
                if(accessMethods == null)
                {
                    ReportNonExistingWrite(address, value, accessWidth);
                    return;
                }
                if(!IsPeripheralEnabled(accessMethods.Peripheral))
                {
                    this.Log(LogLevel.Warning, "Tried to write a locked peripheral: {0}. Address 0x{1:X}, value 0x{2:X}", accessMethods.Peripheral, address, value);
                    return;
                }

                var lockTaken = false;
                try
                {
                    if(!accessMethods.Lock.IsHeldByCurrentThread)
                    {
                        accessMethods.Lock.Enter(ref lockTaken);
                    }
                    if(accessMethods.SetAbsoluteAddress != null)
                    {
                        accessMethods.SetAbsoluteAddress(address);
                    }
                    accessMethods.WriteByte(checked((long)(address - startAddress)), value);
                }
                finally
                {
                    if(lockTaken)
                    {
                        accessMethods.Lock.Exit();
                    }
                }
            }
        }

        public ushort ReadWord(ulong address, ICPU context = null)
        {
            var accessWidth = SysbusAccessWidth.Word;
            if(IsAddressRangeLocked(address.By((ulong)accessWidth)))
            {
                this.Log(LogLevel.Warning, "Tried to read {0} bytes at 0x{1:X} which is inside a locked address range, returning 0",
                    (uint)accessWidth, address);
                return 0;
            }

            using(SetLocalContext(context))
            {
                ulong startAddress, endAddress;

                var accessMethods = globalPeripherals.FindAccessMethods(address, out startAddress, out endAddress);
                if(accessMethods == null)
                {
                    if(context != null)
                    {
                        accessMethods = cpuLocalPeripherals[context].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                    else if(TryGetCurrentCPU(out var currentCPU))
                    {
                        accessMethods = cpuLocalPeripherals[currentCPU].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                }
                if(accessMethods == null)
                {
                    return (ushort)ReportNonExistingRead(address, accessWidth);
                }
                if(!IsPeripheralEnabled(accessMethods.Peripheral))
                {
                    this.Log(LogLevel.Warning, "Tried to read a locked peripheral: {0}. Address 0x{1:X}.", accessMethods.Peripheral, address);
                    return 0;
                }
                var lockTaken = false;
                try
                {
                    if(!accessMethods.Lock.IsHeldByCurrentThread)
                    {
                        accessMethods.Lock.Enter(ref lockTaken);
                    }
                    if(accessMethods.SetAbsoluteAddress != null)
                    {
                        accessMethods.SetAbsoluteAddress(address);
                    }
                    return accessMethods.ReadWord(checked((long)(address - startAddress)));
                }
                finally
                {
                    if(lockTaken)
                    {
                        accessMethods.Lock.Exit();
                    }
                }
            }
        }

        public void WriteWord(ulong address, ushort value, ICPU context = null)
        {
            var accessWidth = SysbusAccessWidth.Word;
            if(IsAddressRangeLocked(address.By((ulong)accessWidth)))
            {
                this.Log(LogLevel.Warning, "Tried to write {0} bytes (0x{1:X}) at 0x{2:X} which is inside a locked address range, write ignored",
                    (uint)accessWidth, value, address);
                return;
            }

            using(SetLocalContext(context))
            {
                ulong startAddress, endAddress;

                var accessMethods = globalPeripherals.FindAccessMethods(address, out startAddress, out endAddress);
                if(accessMethods == null)
                {
                    if(context != null)
                    {
                        accessMethods = cpuLocalPeripherals[context].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                    else if(TryGetCurrentCPU(out var currentCPU))
                    {
                        accessMethods = cpuLocalPeripherals[currentCPU].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                }
                if(accessMethods == null)
                {
                    ReportNonExistingWrite(address, value, accessWidth);
                    return;
                }
                if(!IsPeripheralEnabled(accessMethods.Peripheral))
                {
                    this.Log(LogLevel.Warning, "Tried to write a locked peripheral: {0}. Address 0x{1:X}, value 0x{2:X}", accessMethods.Peripheral, address, value);
                    return;
                }

                var lockTaken = false;
                try
                {
                    if(!accessMethods.Lock.IsHeldByCurrentThread)
                    {
                        accessMethods.Lock.Enter(ref lockTaken);
                    }
                    if(accessMethods.SetAbsoluteAddress != null)
                    {
                        accessMethods.SetAbsoluteAddress(address);
                    }
                    accessMethods.WriteWord(checked((long)(address - startAddress)), value);
                }
                finally
                {
                    if(lockTaken)
                    {
                        accessMethods.Lock.Exit();
                    }
                }
            }
        }

        public uint ReadDoubleWord(ulong address, ICPU context = null)
        {
            var accessWidth = SysbusAccessWidth.DoubleWord;
            if(IsAddressRangeLocked(address.By((ulong)accessWidth)))
            {
                this.Log(LogLevel.Warning, "Tried to read {0} bytes at 0x{1:X} which is inside a locked address range, returning 0",
                    (uint)accessWidth, address);
                return 0;
            }

            using(SetLocalContext(context))
            {
                ulong startAddress, endAddress;

                var accessMethods = globalPeripherals.FindAccessMethods(address, out startAddress, out endAddress);
                if(accessMethods == null)
                {
                    if(context != null)
                    {
                        accessMethods = cpuLocalPeripherals[context].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                    else if(TryGetCurrentCPU(out var currentCPU))
                    {
                        accessMethods = cpuLocalPeripherals[currentCPU].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                }
                if(accessMethods == null)
                {
                    return (uint)ReportNonExistingRead(address, accessWidth);
                }
                if(!IsPeripheralEnabled(accessMethods.Peripheral))
                {
                    this.Log(LogLevel.Warning, "Tried to read a locked peripheral: {0}. Address 0x{1:X}.", accessMethods.Peripheral, address);
                    return 0;
                }
                var lockTaken = false;
                try
                {
                    if(!accessMethods.Lock.IsHeldByCurrentThread)
                    {
                        accessMethods.Lock.Enter(ref lockTaken);
                    }
                    if(accessMethods.SetAbsoluteAddress != null)
                    {
                        accessMethods.SetAbsoluteAddress(address);
                    }
                    return accessMethods.ReadDoubleWord(checked((long)(address - startAddress)));
                }
                finally
                {
                    if(lockTaken)
                    {
                        accessMethods.Lock.Exit();
                    }
                }
            }
        }

        public void WriteDoubleWord(ulong address, uint value, ICPU context = null)
        {
            var accessWidth = SysbusAccessWidth.DoubleWord;
            if(IsAddressRangeLocked(address.By((ulong)accessWidth)))
            {
                this.Log(LogLevel.Warning, "Tried to write {0} bytes (0x{1:X}) at 0x{2:X} which is inside a locked address range, write ignored",
                    (uint)accessWidth, value, address);
                return;
            }

            using(SetLocalContext(context))
            {
                ulong startAddress, endAddress;

                var accessMethods = globalPeripherals.FindAccessMethods(address, out startAddress, out endAddress);
                if(accessMethods == null)
                {
                    if(context != null)
                    {
                        accessMethods = cpuLocalPeripherals[context].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                    else if(TryGetCurrentCPU(out var currentCPU))
                    {
                        accessMethods = cpuLocalPeripherals[currentCPU].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                }
                if(accessMethods == null)
                {
                    ReportNonExistingWrite(address, value, accessWidth);
                    return;
                }
                if(!IsPeripheralEnabled(accessMethods.Peripheral))
                {
                    this.Log(LogLevel.Warning, "Tried to write a locked peripheral: {0}. Address 0x{1:X}, value 0x{2:X}", accessMethods.Peripheral, address, value);
                    return;
                }

                var lockTaken = false;
                try
                {
                    if(!accessMethods.Lock.IsHeldByCurrentThread)
                    {
                        accessMethods.Lock.Enter(ref lockTaken);
                    }
                    if(accessMethods.SetAbsoluteAddress != null)
                    {
                        accessMethods.SetAbsoluteAddress(address);
                    }
                    accessMethods.WriteDoubleWord(checked((long)(address - startAddress)), value);
                }
                finally
                {
                    if(lockTaken)
                    {
                        accessMethods.Lock.Exit();
                    }
                }
            }
        }

        public ulong ReadQuadWord(ulong address, ICPU context = null)
        {
            var accessWidth = SysbusAccessWidth.QuadWord;
            if(IsAddressRangeLocked(address.By((ulong)accessWidth)))
            {
                this.Log(LogLevel.Warning, "Tried to read {0} bytes at 0x{1:X} which is inside a locked address range, returning 0",
                    (uint)accessWidth, address);
                return 0;
            }

            using(SetLocalContext(context))
            {
                ulong startAddress, endAddress;

                var accessMethods = globalPeripherals.FindAccessMethods(address, out startAddress, out endAddress);
                if(accessMethods == null)
                {
                    if(context != null)
                    {
                        accessMethods = cpuLocalPeripherals[context].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                    else if(TryGetCurrentCPU(out var currentCPU))
                    {
                        accessMethods = cpuLocalPeripherals[currentCPU].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                }
                if(accessMethods == null)
                {
                    return (ulong)ReportNonExistingRead(address, accessWidth);
                }
                if(!IsPeripheralEnabled(accessMethods.Peripheral))
                {
                    this.Log(LogLevel.Warning, "Tried to read a locked peripheral: {0}. Address 0x{1:X}.", accessMethods.Peripheral, address);
                    return 0;
                }
                var lockTaken = false;
                try
                {
                    if(!accessMethods.Lock.IsHeldByCurrentThread)
                    {
                        accessMethods.Lock.Enter(ref lockTaken);
                    }
                    if(accessMethods.SetAbsoluteAddress != null)
                    {
                        accessMethods.SetAbsoluteAddress(address);
                    }
                    return accessMethods.ReadQuadWord(checked((long)(address - startAddress)));
                }
                finally
                {
                    if(lockTaken)
                    {
                        accessMethods.Lock.Exit();
                    }
                }
            }
        }

        public void WriteQuadWord(ulong address, ulong value, ICPU context = null)
        {
            var accessWidth = SysbusAccessWidth.QuadWord;
            if(IsAddressRangeLocked(address.By((ulong)accessWidth)))
            {
                this.Log(LogLevel.Warning, "Tried to write {0} bytes (0x{1:X}) at 0x{2:X} which is inside a locked address range, write ignored",
                    (uint)accessWidth, value, address);
                return;
            }

            using(SetLocalContext(context))
            {
                ulong startAddress, endAddress;

                var accessMethods = globalPeripherals.FindAccessMethods(address, out startAddress, out endAddress);
                if(accessMethods == null)
                {
                    if(context != null)
                    {
                        accessMethods = cpuLocalPeripherals[context].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                    else if(TryGetCurrentCPU(out var currentCPU))
                    {
                        accessMethods = cpuLocalPeripherals[currentCPU].FindAccessMethods(address, out startAddress, out endAddress);
                    }
                }
                if(accessMethods == null)
                {
                    ReportNonExistingWrite(address, value, accessWidth);
                    return;
                }
                if(!IsPeripheralEnabled(accessMethods.Peripheral))
                {
                    this.Log(LogLevel.Warning, "Tried to write a locked peripheral: {0}. Address 0x{1:X}, value 0x{2:X}", accessMethods.Peripheral, address, value);
                    return;
                }

                var lockTaken = false;
                try
                {
                    if(!accessMethods.Lock.IsHeldByCurrentThread)
                    {
                        accessMethods.Lock.Enter(ref lockTaken);
                    }
                    if(accessMethods.SetAbsoluteAddress != null)
                    {
                        accessMethods.SetAbsoluteAddress(address);
                    }
                    accessMethods.WriteQuadWord(checked((long)(address - startAddress)), value);
                }
                finally
                {
                    if(lockTaken)
                    {
                        accessMethods.Lock.Exit();
                    }
                }
            }
        }

        public void ClearHookAfterPeripheralRead<T>(IBusPeripheral peripheral)
        {
            SetHookAfterPeripheralRead<T>(peripheral, null);
        }

        public void SetHookAfterPeripheralRead<T>(IBusPeripheral peripheral, Func<T, long, T> hook, Range? subrange = null)
        {
            if(!Machine.IsRegistered(peripheral))
            {
                throw new RecoverableException(string.Format("Cannot set hook on peripheral {0}, it is not registered.", peripheral));
            }
            var type = typeof(T);
            if(type == typeof(byte))
            {
                foreach(var peripherals in allPeripherals)
                {
                    peripherals.VisitAccessMethods(peripheral, pam =>
                    {
                        if(pam.ReadByte.Target is ReadHookWrapper<byte>)
                        {
                            pam.ReadByte = new BusAccess.ByteReadMethod(((ReadHookWrapper<byte>)pam.ReadByte.Target).OriginalMethod);
                        }
                        if(hook != null)
                        {
                            pam.ReadByte = new BusAccess.ByteReadMethod(new ReadHookWrapper<byte>(peripheral, new Func<long, byte>(pam.ReadByte), (Func<byte, long, byte>)(object)hook, subrange).Read);
                        }
                        return pam;
                    });
                }
                return;
            }
            if(type == typeof(ushort))
            {
                foreach(var peripherals in allPeripherals)
                {
                    peripherals.VisitAccessMethods(peripheral, pam =>
                    {
                        if(pam.ReadWord.Target is ReadHookWrapper<ushort>)
                        {
                            pam.ReadWord = new BusAccess.WordReadMethod(((ReadHookWrapper<ushort>)pam.ReadWord.Target).OriginalMethod);
                        }
                        if(hook != null)
                        {
                            pam.ReadWord = new BusAccess.WordReadMethod(new ReadHookWrapper<ushort>(peripheral, new Func<long, ushort>(pam.ReadWord), (Func<ushort, long, ushort>)(object)hook, subrange).Read);
                        }
                        return pam;
                    });
                }
                return;
            }
            if(type == typeof(uint))
            {
                foreach(var peripherals in allPeripherals)
                {
                    peripherals.VisitAccessMethods(peripheral, pam =>
                    {
                        if(pam.ReadDoubleWord.Target is ReadHookWrapper<uint>)
                        {
                            pam.ReadDoubleWord = new BusAccess.DoubleWordReadMethod(((ReadHookWrapper<uint>)pam.ReadDoubleWord.Target).OriginalMethod);
                        }
                        if(hook != null)
                        {
                            pam.ReadDoubleWord = new BusAccess.DoubleWordReadMethod(new ReadHookWrapper<uint>(peripheral, new Func<long, uint>(pam.ReadDoubleWord), (Func<uint, long, uint>)(object)hook, subrange).Read);
                        }
                        return pam;
                    });
                }
                return;
            }
            if(type == typeof(ulong))
            {
                foreach(var peripherals in allPeripherals)
                {
                    peripherals.VisitAccessMethods(peripheral, pam =>
                    {
                        if(pam.ReadQuadWord.Target is ReadHookWrapper<ulong>)
                        {
                            pam.ReadQuadWord = new BusAccess.QuadWordReadMethod(((ReadHookWrapper<ulong>)pam.ReadQuadWord.Target).OriginalMethod);
                        }
                        if(hook != null)
                        {
                            pam.ReadQuadWord = new BusAccess.QuadWordReadMethod(new ReadHookWrapper<ulong>(peripheral, new Func<long, ulong>(pam.ReadQuadWord), (Func<ulong, long, ulong>)(object)hook, subrange).Read);
                        }
                        return pam;
                    });
                }
                return;
            }
        }
        public void ClearHookBeforePeripheralWrite<T>(IBusPeripheral peripheral)
        {
            SetHookBeforePeripheralWrite<T>(peripheral, null);
        }

        public void SetHookBeforePeripheralWrite<T>(IBusPeripheral peripheral, Func<T, long, T> hook, Range? subrange = null)
        {
            if(!Machine.IsRegistered(peripheral))
            {
                throw new RecoverableException(string.Format("Cannot set hook on peripheral {0}, it is not registered.", peripheral));
            }
            var type = typeof(T);
            if(type == typeof(byte))
            {
                foreach(var peripherals in allPeripherals)
                {
                    peripherals.VisitAccessMethods(peripheral, pam =>
                    {
                        if(pam.WriteByte.Target is WriteHookWrapper<byte>)
                        {
                            pam.WriteByte = new BusAccess.ByteWriteMethod(((WriteHookWrapper<byte>)pam.WriteByte.Target).OriginalMethod);
                        }
                        if(hook != null)
                        {
                            pam.WriteByte = new BusAccess.ByteWriteMethod(new WriteHookWrapper<byte>(peripheral, new Action<long, byte>(pam.WriteByte), (Func<byte, long, byte>)(object)hook, subrange).Write);
                        }
                        return pam;
                    });
                }
                return;
            }
            if(type == typeof(ushort))
            {
                foreach(var peripherals in allPeripherals)
                {
                    peripherals.VisitAccessMethods(peripheral, pam =>
                    {
                        if(pam.WriteWord.Target is WriteHookWrapper<ushort>)
                        {
                            pam.WriteWord = new BusAccess.WordWriteMethod(((WriteHookWrapper<ushort>)pam.WriteWord.Target).OriginalMethod);
                        }
                        if(hook != null)
                        {
                            pam.WriteWord = new BusAccess.WordWriteMethod(new WriteHookWrapper<ushort>(peripheral, new Action<long, ushort>(pam.WriteWord), (Func<ushort, long, ushort>)(object)hook, subrange).Write);
                        }
                        return pam;
                    });
                }
                return;
            }
            if(type == typeof(uint))
            {
                foreach(var peripherals in allPeripherals)
                {
                    peripherals.VisitAccessMethods(peripheral, pam =>
                    {
                        if(pam.WriteDoubleWord.Target is WriteHookWrapper<uint>)
                        {
                            pam.WriteDoubleWord = new BusAccess.DoubleWordWriteMethod(((WriteHookWrapper<uint>)pam.WriteDoubleWord.Target).OriginalMethod);
                        }
                        if(hook != null)
                        {
                            pam.WriteDoubleWord = new BusAccess.DoubleWordWriteMethod(new WriteHookWrapper<uint>(peripheral, new Action<long, uint>(pam.WriteDoubleWord), (Func<uint, long, uint>)(object)hook, subrange).Write);
                        }
                        return pam;
                    });
                }
                return;
            }
            if(type == typeof(ulong))
            {
                foreach(var peripherals in allPeripherals)
                {
                    peripherals.VisitAccessMethods(peripheral, pam =>
                    {
                        if(pam.WriteQuadWord.Target is WriteHookWrapper<ulong>)
                        {
                            pam.WriteQuadWord = new BusAccess.QuadWordWriteMethod(((WriteHookWrapper<ulong>)pam.WriteQuadWord.Target).OriginalMethod);
                        }
                        if(hook != null)
                        {
                            pam.WriteQuadWord = new BusAccess.QuadWordWriteMethod(new WriteHookWrapper<ulong>(peripheral, new Action<long, ulong>(pam.WriteQuadWord), (Func<ulong, long, ulong>)(object)hook, subrange).Write);
                        }
                        return pam;
                    });
                }
                return;
            }
        }
    }
}
