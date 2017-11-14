//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU.Registers
{
    public class RegistersGroup<T> : IRegisters<T>
    { 
        public RegistersGroup(IEnumerable<int> keys, Func<int, T> getter, Action<int, T> setter)
        {
            this.keys = new HashSet<int>(keys);
            this.getter = getter;
            this.setter = setter;
        }

        public T this[int index]
        {
            get
            {
                if(!keys.Contains(index))
                {
                    throw new ArgumentOutOfRangeException("index");
                }
                return getter(index);
            }

            set
            {
                if(!keys.Contains(index))
                {
                    throw new ArgumentOutOfRangeException("index");
                }
                setter(index, value);
            }
        }

        public IEnumerable<int> Keys { get { return keys; } }

        private readonly HashSet<int> keys;
        private readonly Func<int, T> getter;
        private readonly Action<int, T> setter;
    }
}

