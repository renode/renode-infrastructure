//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Hooks
{
    public static class IBusControllerHooksExtensions
    {
        public static void SetHookAfterPeripheralRead(this IBusController sysbus, IBusPeripheral peripheral, string pythonScript, Range? subrange = null)
        {
            var runner = new BusPeripheralsHooksPythonEngine(sysbus, peripheral, pythonScript);
            sysbus.SetHookAfterPeripheralRead<ulong>(peripheral, runner.ReadHook, subrange);
            sysbus.SetHookAfterPeripheralRead<uint>(peripheral, (readValue, offset) => (uint)runner.ReadHook(readValue, offset), subrange);
            sysbus.SetHookAfterPeripheralRead<ushort>(peripheral, (readValue, offset) => (ushort)runner.ReadHook(readValue, offset), subrange);
            sysbus.SetHookAfterPeripheralRead<byte>(peripheral, (readValue, offset) => (byte)runner.ReadHook(readValue, offset), subrange);
        }

        public static void SetHookBeforePeripheralWrite(this IBusController sysbus, IBusPeripheral peripheral, string pythonScript, Range? subrange = null)
        {
            var runner = new BusPeripheralsHooksPythonEngine(sysbus, peripheral, null, pythonScript);
            sysbus.SetHookBeforePeripheralWrite<ulong>(peripheral, runner.WriteHook, subrange);
            sysbus.SetHookBeforePeripheralWrite<uint>(peripheral, (valueToWrite, offset) => (uint)runner.WriteHook(valueToWrite, offset), subrange);
            sysbus.SetHookBeforePeripheralWrite<ushort>(peripheral, (valueToWrite, offset) => (ushort)runner.WriteHook(valueToWrite, offset), subrange);
            sysbus.SetHookBeforePeripheralWrite<byte>(peripheral, (valueToWrite, offset) => (byte)runner.WriteHook(valueToWrite, offset), subrange);
        }

        public static void AddWatchpointHook(this IBusController sysbus, ulong address, SysbusAccessWidth width, Access access, string pythonScript)
        {
            var engine = new WatchpointHookPythonEngine(sysbus, pythonScript);
            sysbus.AddWatchpointHook(address, width, access, engine.Hook);
        }
    }
}

