//
// Copyright (c) 2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Testing
{
    public static class SysbusTesterExtensions
    {
        public static void CreateSysbusTester(this Machine machine, string name)
        {
            var tester = new SysbusTester(machine);
            EmulationManager.Instance.CurrentEmulation.ExternalsManager.AddExternal(tester, name);
        }
    }

    public class SysbusTester : IExternal
    {
        public SysbusTester(Machine machine)
        {
            this.machine = machine;
        }

        public void ObserveAddress(ulong address)
        {
            if(observedAddresses.Contains(address))
            {
                return;
            }

            machine.SystemBus.AddWatchpointHook(address, SysbusAccessWidth.Byte, Access.Write, (cpu, encounteredAddress, width, encounteredValue) =>
            {
                this.Log(LogLevel.Noisy, "Registering byte write of 0x{0:X} at address 0x{1:X}", encounteredValue, encounteredAddress);
                InnerRegisterWrite(byteWrites, encounteredAddress, encounteredValue);
            });

            machine.SystemBus.AddWatchpointHook(address, SysbusAccessWidth.Word, Access.Write, (cpu, encounteredAddress, width, encounteredValue) =>
            {
                this.Log(LogLevel.Noisy, "Registering word write of 0x{0:X} at address 0x{1:X}", encounteredValue, encounteredAddress);
                InnerRegisterWrite(wordWrites, encounteredAddress, encounteredValue);
            });

            machine.SystemBus.AddWatchpointHook(address, SysbusAccessWidth.DoubleWord, Access.Write, (cpu, encounteredAddress, width, encounteredValue) =>
            {
                this.Log(LogLevel.Noisy, "Registering double word write of 0x{0:X} at address 0x{1:X}", encounteredValue, encounteredAddress);
                InnerRegisterWrite(doubleWordWrites, encounteredAddress, encounteredValue);
            });

            observedAddresses.Add(address);
        }

        public void WaitForByteWrite(ulong address, byte expectedValue, int timeout)
        {
            InnerWaitForWrite(byteWrites, address, expectedValue, timeout);
        }

        public void WaitForWordWrite(ulong address, uint expectedValue, int timeout)
        {
            InnerWaitForWrite(wordWrites, address, expectedValue, timeout);
        }

        public void WaitForDoubleWordWrite(ulong address, uint expectedValue, int timeout)
        {
            InnerWaitForWrite(doubleWordWrites, address, expectedValue, timeout);
        }

        private void InnerRegisterWrite(List<Tuple<ulong, uint>> list, ulong offset, uint value)
        {
            lock(list)
            {
                list.Add(Tuple.Create(offset, value));
                Monitor.PulseAll(list);
            }
        }

        private void InnerWaitForWrite(List<Tuple<ulong, uint>> list, ulong offset, uint value, int timeout)
        {
            if(!observedAddresses.Contains(offset))
            {
                throw new ArgumentException($"Address 0x{offset:X} is not currenlty observed. Did you forget to call 'ObserveAddress' before starting the emulation?");
            }

            lock(list)
            {
                while(true)
                {
                    for(var i = 0; i < list.Count; i++)
                    {
                        if(list[i].Item1 == offset && list[i].Item2 == value)
                        {
                            list.RemoveRange(0, i);
                            return;
                        }
                    }

                    if(!Monitor.Wait(list, timeout))
                    {
                        throw new InvalidOperationException("Sysbus write assertion not met.");
                    }
                }
            }
        }

        private readonly Machine machine;

        private readonly HashSet<ulong> observedAddresses = new HashSet<ulong>();
        private readonly List<Tuple<ulong, uint>> byteWrites = new List<Tuple<ulong, uint>>();
        private readonly List<Tuple<ulong, uint>> wordWrites = new List<Tuple<ulong, uint>>();
        private readonly List<Tuple<ulong, uint>> doubleWordWrites = new List<Tuple<ulong, uint>>();
    }
}
