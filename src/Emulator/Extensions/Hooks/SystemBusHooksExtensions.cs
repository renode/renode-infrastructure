//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Hooks
{
    public static class SystemBusHooksExtensions
    {
        public static void SetHookAfterPeripheralRead(this SystemBus sysbus, IBusPeripheral peripheral, string pythonScript, Range? subrange = null)
        {
            var runner = new BusPeripheralsHooksPythonEngine(sysbus, peripheral, pythonScript);
            sysbus.SetHookAfterPeripheralRead<uint>(peripheral, runner.ReadHook, subrange);
            sysbus.SetHookAfterPeripheralRead<ushort>(peripheral, (readValue, offset) => (ushort)runner.ReadHook(readValue, offset), subrange);
            sysbus.SetHookAfterPeripheralRead<byte>(peripheral, (readValue, offset) => (byte)runner.ReadHook(readValue, offset), subrange);
        }

        public static void SetHookBeforePeripheralWrite(this SystemBus sysbus, IBusPeripheral peripheral, string pythonScript, Range? subrange = null)
        {
            var runner = new BusPeripheralsHooksPythonEngine(sysbus, peripheral, null, pythonScript);
            sysbus.SetHookBeforePeripheralWrite<uint>(peripheral, runner.WriteHook, subrange);
            sysbus.SetHookBeforePeripheralWrite<ushort>(peripheral, (valueToWrite, offset) => (ushort)runner.WriteHook(valueToWrite, offset), subrange);
            sysbus.SetHookBeforePeripheralWrite<byte>(peripheral, (valueToWrite, offset) => (byte)runner.WriteHook(valueToWrite, offset), subrange);
        }

        public static void AddWatchpointHook(this SystemBus sysbus, ulong address, SysbusAccessWidth width, Access access, string pythonScript)
        {
            var engine = new WatchpointHookPythonEngine(sysbus, pythonScript);
            sysbus.AddWatchpointHook(address, width, access, engine.Hook);
        }
    }
}

