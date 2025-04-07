//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    public sealed class WriteLoggingWrapper<T> : WriteHookWrapper<T>
    {
        public WriteLoggingWrapper(IBusPeripheral peripheral, Action<long, T> originalMethod) : base(peripheral, originalMethod, null, null)
        {
            mapper = new RegisterMapper(peripheral.GetType());
            machine = peripheral.GetMachine();
        }

        public override void Write(long offset, T value)
        {
            Peripheral.Log(LogLevel.Info, machine.SystemBus.DecorateWithCPUNameAndPC($"Write{Name} to 0x{offset:X} ({mapper.ToString(offset)}), value 0x{value:X}"));
            OriginalMethod(offset, value);
        }

        private readonly IMachine machine;
        private readonly RegisterMapper mapper;
    }
}