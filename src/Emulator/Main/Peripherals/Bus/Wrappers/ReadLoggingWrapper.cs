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
    public class ReadLoggingWrapper<T> : ReadHookWrapper<T>
    {
        public ReadLoggingWrapper(IBusPeripheral peripheral, Func<long, T> originalMethod) :
            base(peripheral, originalMethod)
        {
            mapper = new RegisterMapper(peripheral.GetType());
            machine = peripheral.GetMachine();
        }

        public override T Read(long offset)
        {
            var value = OriginalMethod(offset);
            Peripheral.Log(LogLevel.Info, machine.SystemBus.DecorateWithCPUNameAndPC($"Read{Name} from 0x{offset:X} ({mapper.ToString(offset)}), returned 0x{value:X}"));
            return value;
        }

        private readonly IMachine machine;
        private readonly RegisterMapper mapper;
    }
}