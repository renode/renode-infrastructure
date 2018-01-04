//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Utilities.Collections
{
    public sealed class LazyList<T>
    {
        public LazyList()
        {
            funcs = new List<Func<T>>();
        }

        public void Add(Func<T> func)
        {
            funcs.Add(func);
        }

        public List<T> ToList()
        {
            return funcs.Select(x => x()).ToList();
        }

        private readonly List<Func<T>> funcs;
    }
}
