//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    public class WriteHookWrapper<T> : HookWrapper
    {
        public WriteHookWrapper(IBusPeripheral peripheral, Action<long, T> originalMethod, Func<T, long, T> newMethod = null,
                               Range? subrange = null) : base(peripheral, typeof(T), subrange)
        {
            this.originalMethod = originalMethod;
            this.newMethod = newMethod;
        }

        public Action<long, T> OriginalMethod
        {
            get
            {
                return originalMethod;
            }
        }

        public virtual void Write(long offset, T value)
        {
            if(Subrange != null && !Subrange.Value.Contains(checked((ulong)offset)))
            {
                originalMethod(offset, value);
                return;
            }
            var modifiedValue = newMethod(value, offset);
            originalMethod(offset, modifiedValue);
        }

        private readonly Action<long, T> originalMethod;
        private readonly Func<T, long, T> newMethod;
    }
}

