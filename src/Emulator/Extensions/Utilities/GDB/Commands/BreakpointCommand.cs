//
// Copyright (c) 2010-2023 Antmicro
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
                    foreach(var cpu in manager.ManagedCpus.Values)
                    {
                        cpu.AddHook(address, MemoryBreakpointHook);
                    }
                    break;
                case BreakpointType.HardwareBreakpoint:
                    foreach(var cpu in manager.ManagedCpus.Values)
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
                    foreach(var cpu in manager.ManagedCpus.Values)
                    {
                        cpu.RemoveHook(address, MemoryBreakpointHook);
                    }
                    break;
                case BreakpointType.HardwareBreakpoint:
                    foreach(var cpu in manager.ManagedCpus.Values)
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
            cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu.Id, breakpointType: BreakpointType.HardwareBreakpoint), manager.BlockOnStep);
        }

        private void MemoryBreakpointHook(ICpuSupportingGdb cpu, ulong address)
        {
            cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu.Id, breakpointType: BreakpointType.MemoryBreakpoint), manager.BlockOnStep);
        }

        private BusHookDelegate AccessWatchpointHook(ulong virtualAddress)
        {
            //? I See a possible problem here.
            //? Here we call `Halt` event with T05 argument, but in a second we will call it once again with S05 in HandleStepping@TranlationCPU.
            //? It seems to work fine with GDB, but... I don't know if it is fully correct.
            return (ICpuSupportingGdb cpu, ulong physicalAddress, SysbusAccessWidth width, ulong value) =>
            {
                // GDB uses virtual addresses for watchpoints so we ignore the physical address here
                // and send the virtual one to GDB
                cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu.Id, virtualAddress, BreakpointType.AccessWatchpoint), manager.BlockOnStep);
            };
        }

        private BusHookDelegate WriteWatchpointHook(ulong virtualAddress)
        {
            return (ICpuSupportingGdb cpu, ulong physicalAddress, SysbusAccessWidth width, ulong value) =>
            {
                // GDB uses virtual addresses for watchpoints so we ignore the physical address here
                // and send the virtual one to GDB
                cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu.Id, virtualAddress, BreakpointType.WriteWatchpoint), manager.BlockOnStep);
            };
        }

        private BusHookDelegate ReadWatchpointHook(ulong virtualAddress)
        {
            return (ICpuSupportingGdb cpu, ulong physicalAddress, SysbusAccessWidth width, ulong value) =>
            {
                // GDB uses virtual addresses for watchpoints so we ignore the physical address here
                // and send the virtual one to GDB
                cpu.EnterSingleStepModeSafely(new HaltArguments(HaltReason.Breakpoint, cpu.Id, virtualAddress, BreakpointType.ReadWatchpoint), manager.BlockOnStep);
            };
        }

        private void AddWatchpointsCoveringMemoryArea(ulong virtualAddress, uint kind, Access access, Func<ulong, BusHookDelegate> hookFactory)
        {
            // we need to register hooks for all possible access widths covering memory fragment
            // [virtualAddress, virtualAddress + kind) referred by GDB
            var hook = hookFactory(virtualAddress);
            foreach(var descriptor in CalculateAllCoveringAddressess(virtualAddress, kind, access, hook))
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
                        manager.Machine.SystemBus.AddWatchpointHook(descriptor.PhysicalAddress, descriptor.Width, access, descriptor.Hook);
                    }
                }
            }
        }

        private void RemoveWatchpointsCoveringMemoryArea(ulong virtualAddress, uint kind, Access access, Func<ulong, BusHookDelegate> hookFactory)
        {
            // we need to unregister hooks from all possible access widths convering memory fragment
            // [virtualAddress, virtualAddress + kind) referred by GDB
            // The hook (delegate) is not considered by Equals and GetHashCode which will let us get the
            // delegate that's actually set as a sysbus hook from our descriptor and remove it.
            foreach(var descriptor in CalculateAllCoveringAddressess(virtualAddress, kind, access, null))
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
                        manager.Machine.SystemBus.RemoveWatchpointHook(descriptor.PhysicalAddress, descriptor.Hook);
                    }
                }
            }
        }

        private IEnumerable<WatchpointDescriptor> CalculateAllCoveringAddressess(ulong virtualAddress, uint kind, Access access, BusHookDelegate hook)
        {
            foreach(SysbusAccessWidth width in Enum.GetValues(typeof(SysbusAccessWidth)))
            {
                for(var offset = -(long)(virtualAddress % (ulong)width); offset < kind; offset += (long)width)
                {
                    var offsetVirtualAddress = virtualAddress - (ulong)(-offset);
                    // offsetPhysicalAddress is declared here instead of using `out var` in the next
                    // line to avoid triggering an internal error in the Mono 6.8.0.105 compiler
                    // shipped by Ubuntu 20.04
                    ulong offsetPhysicalAddress;
                    if(!TryTranslateAddress(offsetVirtualAddress, out offsetPhysicalAddress, write: access != Access.Read))
                    {
                        // Address not translated by the MMU, assume physical == virtual.
                        offsetPhysicalAddress = offsetVirtualAddress;
                    }
                    yield return new WatchpointDescriptor(offsetVirtualAddress, offsetPhysicalAddress, width, access, hook);
                }
            }
        }

        private readonly Dictionary<WatchpointDescriptor, int> watchpoints;

        private class WatchpointDescriptor
        {
            public WatchpointDescriptor(ulong virtualAddress, ulong physicalAddress, SysbusAccessWidth width, Access access, BusHookDelegate hook)
            {
                VirtualAddress = virtualAddress;
                PhysicalAddress = physicalAddress;
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

                return objAsBreakpointDescriptor.VirtualAddress == VirtualAddress
                        && objAsBreakpointDescriptor.Width == Width
                        && objAsBreakpointDescriptor.Access == Access;
            }

            public override int GetHashCode()
            {
                return 17 * (int)VirtualAddress
                    + 23 * (int)Width
                    + 17 * (int)Access;
            }

            public readonly ulong VirtualAddress;
            public readonly ulong PhysicalAddress;
            public readonly SysbusAccessWidth Width;
            public readonly Access Access;
            public readonly BusHookDelegate Hook;
        }
    }
}

