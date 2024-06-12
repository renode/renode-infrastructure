//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;
using SysbusAccessWidth = Antmicro.Renode.Peripherals.Bus.SysbusAccessWidth;
using System.Collections.Generic;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class BreakpointCommand : Command
    {
        public BreakpointCommand(CommandsManager manager) : base(manager)
        {
            watchpoints = new Dictionary<WatchpointDescriptor, int>();
        }

        [Execute("Z")]
        public PacketData InsertBreakpoint(
            [Argument(Separator = ',')]BreakpointType type,
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]ulong address,
            [Argument(Separator = ';', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]uint kind)
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
                    break;
                case BreakpointType.HardwareBreakpoint:
                    foreach(var cpu in manager.ManagedCpus)
                    {
                        cpu.AddHook(address, HardwareBreakpointHook);
                    }
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

        [Execute("z")]
        public PacketData RemoveBreakpoint(
            [Argument(Separator = ',')]BreakpointType type,
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]ulong address,
            [Argument(Separator = ';', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]uint kind)
        {
            switch(type)
            {
                case BreakpointType.MemoryBreakpoint:
                    foreach(var cpu in manager.ManagedCpus)
                    {
                        cpu.RemoveHook(address, MemoryBreakpointHook);
                    }
                    break;
                case BreakpointType.HardwareBreakpoint:
                    foreach(var cpu in manager.ManagedCpus)
                    {
                        cpu.RemoveHook(address, HardwareBreakpointHook);
                    }
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

        private void HardwareBreakpointHook(ICpuSupportingGdb cpu, ulong address)
        {
            cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu, breakpointType: BreakpointType.HardwareBreakpoint));
        }

        private void MemoryBreakpointHook(ICpuSupportingGdb cpu, ulong address)
        {
            cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu, breakpointType: BreakpointType.MemoryBreakpoint));
        }

        private void AccessWatchpointHook(ICpuSupportingGdb cpu, ulong address, SysbusAccessWidth width, ulong value)
        {
            //? I See a possible problem here.
            //? Here we call `Halt` event with T05 argument, but in a second we will call it once again with S05 in HandleStepping@TranlationCPU.
            //? It seems to work fine with GDB, but... I don't know if it is fully correct.
            cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu, address, BreakpointType.AccessWatchpoint));
        }

        private void WriteWatchpointHook(ICpuSupportingGdb cpu, ulong address, SysbusAccessWidth width, ulong value)
        {
            cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu, address, BreakpointType.WriteWatchpoint));
        }

        private void ReadWatchpointHook(ICpuSupportingGdb cpu, ulong address, SysbusAccessWidth width, ulong value)
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

        private readonly Dictionary<WatchpointDescriptor, int> watchpoints;

        private class WatchpointDescriptor
        {
            public WatchpointDescriptor(ulong address, SysbusAccessWidth width, Access access, BusHookDelegate hook)
            {
                Address = address;
                Width = width;
                Access = access;
                Hook = hook;
            }

            public override bool Equals(object obj)
            {
                var objAsBreakpointDescriptor = obj as WatchpointDescriptor;
                if(objAsBreakpointDescriptor == null)
                {
                    return false;
                }

                return objAsBreakpointDescriptor.Address == Address
                        && objAsBreakpointDescriptor.Width == Width
                        && objAsBreakpointDescriptor.Access == Access
                        && objAsBreakpointDescriptor.Hook == Hook;
            }

            public override int GetHashCode()
            {
                return 17 * (int)Address
                    + 23 * (int)Width
                    + 17 * (int)Access
                    + 17 * Hook.GetHashCode();
            }

            public readonly ulong Address;
            public readonly SysbusAccessWidth Width;
            public readonly Access Access;
            public readonly BusHookDelegate Hook;
        }
    }
}

