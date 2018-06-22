//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU.Registers
{
    public class RegistersGroup : IRegisters
    {
        public RegistersGroup(IEnumerable<int> keys, Func<int, RegisterValue> getter, Action<int, ulong> setter)
        {
            this.keys = new HashSet<int>(keys);
            this.getter = getter;
            this.setter = setter;
        }

        public RegisterValue this[int index]
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
        private readonly Func<int, RegisterValue> getter;
        private readonly Action<int, ulong> setter;
    }
}

