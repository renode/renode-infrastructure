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
    public class ReadHookWrapper<T> : HookWrapper
    {
        public ReadHookWrapper(IBusPeripheral peripheral, Func<long, T> originalMethod, Func<T, long, T> newMethod = null,
                              Range? subrange = null) : base(peripheral, typeof(T), subrange)
        {
            this.originalMethod = originalMethod;
            this.newMethod = newMethod;
        }

        public Func<long, T> OriginalMethod
        {
            get
            {
                return originalMethod;
            }
        }

        public virtual T Read(long offset)
        {
            if(Subrange != null && !Subrange.Value.Contains(checked((ulong)offset)))
            {
                return originalMethod(offset);
            }
            return newMethod(originalMethod(offset), offset);
        }

        private readonly Func<long, T> originalMethod;
        private readonly Func<T, long, T> newMethod;
    }
}

