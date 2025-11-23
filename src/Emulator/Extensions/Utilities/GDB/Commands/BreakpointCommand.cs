//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

using SysbusAccessWidth = Antmicro.Renode.Peripherals.Bus.SysbusAccessWidth;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class BreakpointCommand : Command
    {
        public BreakpointCommand(CommandsManager manager) : base(manager)
        {
            watchpoints = new Dictionary<WatchpointDescriptor, int>();
            breakpoints = new HashSet<Tuple<ulong, BreakpointType>>();
        }

        [Execute("Z")]
        public PacketData InsertBreakpoint(
            [Argument(Separator = ',')] BreakpointType type,
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] ulong address,
            [Argument(Separator = ';', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint kind)
        {
            // The kind is the size of the breakpoint in bytes.
            // Currently, we only handle it for watchpoints, where it specifies the number of bytes to watch.
            // Setting a 0-byte watchpoint, for example with `watch *&variable_of_unknown_type`, gives
            // unexpected results: GDB says the watchpoint is set, but it can never be hit.
            if(kind == 0 && (type == BreakpointType.AccessWatchpoint
                          || type == BreakpointType.ReadWatchpoint
                          || type == BreakpointType.WriteWatchpoint))
            {
                return PacketData.ErrorReply(Error.InvalidArgument);
            }

            switch(type)
            {
            case BreakpointType.MemoryBreakpoint:
                foreach(var cpu in manager.ManagedCpus)
                {
                    cpu.AddHook(address, MemoryBreakpointHook);
                }
                breakpoints.Add(Tuple.Create(address, type));
                break;
            case BreakpointType.HardwareBreakpoint:
                foreach(var cpu in manager.ManagedCpus)
                {
                    cpu.AddHook(address, HardwareBreakpointHook);
                }
                breakpoints.Add(Tuple.Create(address, type));
                break;
            case BreakpointType.AccessWatchpoint:
                AddWatchpointsCoveringMemoryArea(address, kind, Access.ReadAndWrite, AccessWatchpointHook);
                break;
            case BreakpointType.ReadWatchpoint:
                AddWatchpointsCoveringMemoryArea(address, kind, Access.Read, ReadWatchpointHook);
                break;
            case BreakpointType.WriteWatchpoint:
                AddWatchpointsCoveringMemoryArea(address, kind, Access.Write, WriteWatchpointHook);
                break;
            default:
                Logger.LogAs(this, LogLevel.Warning, "Unsupported breakpoint type: {0}, not inserting.", type);
                return PacketData.ErrorReply();
            }

            return PacketData.Success;
        }

        public void InsertBreakpoint(BreakpointType type, ulong address, WatchpointDescriptor descriptor = null, int? counter = null)
        {
            if((type == BreakpointType.AccessWatchpoint ||
                type == BreakpointType.ReadWatchpoint ||
                type == BreakpointType.WriteWatchpoint) && (descriptor == null || counter == null))
            {
                throw new ArgumentException("Watchpoint descriptor and counter are required for watchpoint manual insertion");
            }

            switch(type)
            {
            case BreakpointType.MemoryBreakpoint:
                foreach(var cpu in manager.ManagedCpus)
                {
                    cpu.AddHook(address, MemoryBreakpointHook);
                }
                breakpoints.Add(Tuple.Create(address, type));
                break;
            case BreakpointType.HardwareBreakpoint:
                foreach(var cpu in manager.ManagedCpus)
                {
                    cpu.AddHook(address, HardwareBreakpointHook);
                }
                breakpoints.Add(Tuple.Create(address, type));
                break;
            case BreakpointType.AccessWatchpoint:
            case BreakpointType.ReadWatchpoint:
            case BreakpointType.WriteWatchpoint:
                manager.Machine.SystemBus.AddWatchpointHook(descriptor.Address, descriptor.Width, descriptor.Access, descriptor.Hook);
                watchpoints.Add(descriptor, counter.Value);
                break;
            default:
                throw new RecoverableException($"Unsupported breakpoint type: {type}");
            }
        }

        [Execute("z")]
        public PacketData RemoveBreakpoint(
            [Argument(Separator = ',')] BreakpointType type,
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] ulong address,
            [Argument(Separator = ';', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint kind)
        {
            switch(type)
            {
            case BreakpointType.MemoryBreakpoint:
                foreach(var cpu in manager.ManagedCpus)
                {
                    cpu.RemoveHook(address, MemoryBreakpointHook);
                }
                breakpoints.Remove(Tuple.Create(address, type));
                break;
            case BreakpointType.HardwareBreakpoint:
                foreach(var cpu in manager.ManagedCpus)
                {
                    cpu.RemoveHook(address, HardwareBreakpointHook);
                }
                breakpoints.Remove(Tuple.Create(address, type));
                break;
            case BreakpointType.AccessWatchpoint:
                RemoveWatchpointsCoveringMemoryArea(address, kind, Access.ReadAndWrite, AccessWatchpointHook);
                break;
            case BreakpointType.ReadWatchpoint:
                RemoveWatchpointsCoveringMemoryArea(address, kind, Access.Read, ReadWatchpointHook);
                break;
            case BreakpointType.WriteWatchpoint:
                RemoveWatchpointsCoveringMemoryArea(address, kind, Access.Write, WriteWatchpointHook);
                break;
            default:
                Logger.LogAs(this, LogLevel.Warning, "Unsupported breakpoint type: {0}, not removing.", type);
                return PacketData.ErrorReply();
            }

            return PacketData.Success;
        }

        public void RemoveAllBreakpoints()
        {
            foreach(var cpu in manager.ManagedCpus)
            {
                cpu.RemoveHooks(MemoryBreakpointHook);
                cpu.RemoveHooks(HardwareBreakpointHook);
            }
            breakpoints.Clear();
        }

        public void RemoveAllWatchpoints()
        {
            foreach(var watchpoint in watchpoints.Keys)
            {
                manager.Machine.SystemBus.RemoveWatchpointHook(watchpoint.Address, watchpoint.Hook);
            }
            watchpoints.Clear();
        }

        public ISet<Tuple<ulong, BreakpointType>> Breakpoints => new HashSet<Tuple<ulong, BreakpointType>>(breakpoints);

        public IDictionary<WatchpointDescriptor, int> Watchpoints => new Dictionary<WatchpointDescriptor, int>(watchpoints);

        private static IEnumerable<WatchpointDescriptor> CalculateAllCoveringAddressess(ulong address, uint kind, Access access, BusHookDelegate hook)
        {
            foreach(SysbusAccessWidth width in Enum.GetValues(typeof(SysbusAccessWidth)))
            {
                for(var offset = -(long)(address % (ulong)width); offset < kind; offset += (long)width)
                {
                    yield return new WatchpointDescriptor(address - (ulong)(-offset), width, access, hook);
                }
            }
        }

        private static void HardwareBreakpointHook(ICpuSupportingGdb cpu, ulong address)
        {
            cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu, breakpointType: BreakpointType.HardwareBreakpoint));
        }

        private static void MemoryBreakpointHook(ICpuSupportingGdb cpu, ulong address)
        {
            cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu, breakpointType: BreakpointType.MemoryBreakpoint));
        }

        private static void AccessWatchpointHook(ICpuSupportingGdb cpu, ulong address, SysbusAccessWidth width, ulong value)
        {
            //? I See a possible problem here.
            //? Here we call `Halt` event with T05 argument, but in a second we will call it once again with S05 in HandleStepping@TranlationCPU.
            //? It seems to work fine with GDB, but... I don't know if it is fully correct.
            cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu, address, BreakpointType.AccessWatchpoint));
        }

        private static void WriteWatchpointHook(ICpuSupportingGdb cpu, ulong address, SysbusAccessWidth width, ulong value)
        {
            cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu, address, BreakpointType.WriteWatchpoint));
        }

        private static void ReadWatchpointHook(ICpuSupportingGdb cpu, ulong address, SysbusAccessWidth width, ulong value)
        {
            cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu, address, BreakpointType.ReadWatchpoint));
        }

        private void AddWatchpointsCoveringMemoryArea(ulong address, uint kind, Access access, BusHookDelegate hook)
        {
            // we need to register hooks for all possible access widths covering memory fragment
            // [address, address + kind) referred by GDB
            foreach(var descriptor in CalculateAllCoveringAddressess(address, kind, access, hook))
            {
                lock(watchpoints)
                {
                    if(watchpoints.ContainsKey(descriptor))
                    {
                        watchpoints[descriptor]++;
                    }
                    else
                    {
                        watchpoints.Add(descriptor, 1);
                        manager.Machine.SystemBus.AddWatchpointHook(descriptor.Address, descriptor.Width, access, hook);
                    }
                }
            }
        }

        private void RemoveWatchpointsCoveringMemoryArea(ulong address, uint kind, Access access, BusHookDelegate hook)
        {
            // we need to unregister hooks from all possible access widths convering memory fragment
            // [address, address + kind) referred by GDB
            foreach(var descriptor in CalculateAllCoveringAddressess(address, kind, access, hook))
            {
                lock(watchpoints)
                {
                    if(watchpoints[descriptor] > 1)
                    {
                        watchpoints[descriptor]--;
                    }
                    else
                    {
                        watchpoints.Remove(descriptor);
                        manager.Machine.SystemBus.RemoveWatchpointHook(descriptor.Address, hook);
                    }
                }
            }
        }

        private readonly Dictionary<WatchpointDescriptor, int> watchpoints;
        private readonly HashSet<Tuple<ulong, BreakpointType>> breakpoints;
    }
}