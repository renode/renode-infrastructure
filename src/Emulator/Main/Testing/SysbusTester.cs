//
// Copyright (c) 2010-2022 Antmicro
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
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Testing
{
    public static class SysbusTesterExtensions
    {
        public static void CreateSysbusTester(this IMachine machine, string name)
        {
            var tester = new SysbusTester(machine);
            EmulationManager.Instance.CurrentEmulation.ExternalsManager.AddExternal(tester, name);
        }
    }

    public class SysbusTester : IExternal
    {
        public SysbusTester(IMachine machine)
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

            machine.SystemBus.AddWatchpointHook(address, SysbusAccessWidth.QuadWord, Access.Write, (cpu, encounteredAddress, width, encounteredValue) =>
            {
                this.Log(LogLevel.Noisy, "Registering quad word write of 0x{0:X} at address 0x{1:X}", encounteredValue, encounteredAddress);
                InnerRegisterWrite(quadWordWrites, encounteredAddress, encounteredValue);
            });

            observedAddresses.Add(address);
        }

        public void WaitForByteWrite(ulong address, byte expectedValue, float timeout)
        {
            InnerWaitForWrite(byteWrites, address, expectedValue, timeout);
        }

        public void WaitForWordWrite(ulong address, uint expectedValue, float timeout)
        {
            InnerWaitForWrite(wordWrites, address, expectedValue, timeout);
        }

        public void WaitForDoubleWordWrite(ulong address, uint expectedValue, float timeout)
        {
            InnerWaitForWrite(doubleWordWrites, address, expectedValue, timeout);
        }

        public void WaitForQuadWordWrite(ulong address, uint expectedValue, float timeout)
        {
            InnerWaitForWrite(quadWordWrites, address, expectedValue, timeout);
        }

        private void InnerRegisterWrite(List<Tuple<ulong, ulong>> list, ulong offset, ulong value)
        {
            lock(list)
            {
                list.Add(Tuple.Create(offset, value));
            }
            newWriteEvent.Set();
        }

        private void InnerWaitForWrite(List<Tuple<ulong, ulong>> list, ulong offset, ulong value, float timeout)
        {
            if(!observedAddresses.Contains(offset))
            {
                throw new ArgumentException($"Address 0x{offset:X} is not currenlty observed. Did you forget to call 'ObserveAddress' before starting the emulation?");
            }

            var timeoutEvent = machine.LocalTimeSource.EnqueueTimeoutEvent((ulong)(timeout * 1000));

            do
            {
                lock(list)
                {
                    for(var i = 0; i < list.Count; i++)
                    {
                        if(list[i].Item1 == offset && list[i].Item2 == value)
                        {
                            list.RemoveRange(0, i);
                            return;
                        }
                    }
                }

                WaitHandle.WaitAny(new [] { timeoutEvent.WaitHandle, newWriteEvent });
            }
            while(!timeoutEvent.IsTriggered);

            throw new InvalidOperationException("Sysbus write assertion not met.");
        }

        private readonly IMachine machine;

        private readonly AutoResetEvent newWriteEvent = new AutoResetEvent(false);
        private readonly HashSet<ulong> observedAddresses = new HashSet<ulong>();
        private readonly List<Tuple<ulong, ulong>> byteWrites = new List<Tuple<ulong, ulong>>();
        private readonly List<Tuple<ulong, ulong>> wordWrites = new List<Tuple<ulong, ulong>>();
        private readonly List<Tuple<ulong, ulong>> doubleWordWrites = new List<Tuple<ulong, ulong>>();
        private readonly List<Tuple<ulong, ulong>> quadWordWrites = new List<Tuple<ulong, ulong>>();
    }
}
