//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.UnitTests.Utilities
{
    public sealed class LazyPool<T>
    {
        public LazyPool(Func<T> factory)
        {
            this.factory = factory;
            list = new List<T>();
        }

        public T this[int index]
        {
            get
            {
                CheckIndex(index);
                return list[index];
            }
            set
            {
                CheckIndex(index);
                list[index] = value;
            }
        }

        private void CheckIndex(int index)
        {
            while(index >= list.Count)
            {
                list.Add(factory());
            }
        }

        private readonly List<T> list;
        private readonly Func<T> factory;
    }
}
