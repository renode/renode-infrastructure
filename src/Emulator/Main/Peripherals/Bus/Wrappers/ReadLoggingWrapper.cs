//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    public class ReadLoggingWrapper<T> : ReadHookWrapper<T>
    {
        public ReadLoggingWrapper(IBusPeripheral peripheral, Func<long, T> originalMethod) :
            base(peripheral, originalMethod)
        {
            mapper = new RegisterMapper(peripheral.GetType());
        }

        public override T Read(long offset)
        {
            var originalValue = OriginalMethod(offset);
            Peripheral.DebugLog("Read{0} from 0x{1:X}{3}, returned 0x{2:X}.", Name, offset, originalValue, mapper.ToString(offset, " ({0})"));
            return originalValue;
        }

        private readonly RegisterMapper mapper;
    }
}

