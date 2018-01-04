//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    public sealed class WriteLoggingWrapper<T> : WriteHookWrapper<T>
    {
        public WriteLoggingWrapper(IBusPeripheral peripheral, Action<long, T> originalMethod) : base(peripheral, originalMethod, null, null)
        {
            mapper = new RegisterMapper(peripheral.GetType());
        }

        public override void Write(long offset, T value)
        {
            Peripheral.DebugLog("Write{0} to 0x{1:X}{3}, value 0x{2:X}.", Name, offset, value, mapper.ToString(offset, " ({0})"));
            OriginalMethod(offset, value);
        }

        private readonly RegisterMapper mapper;
    }
}

