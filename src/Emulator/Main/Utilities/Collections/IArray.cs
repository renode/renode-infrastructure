//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities.Collections
{
    public interface IArray<T> : IEnumerable<T>
    {
        int Length { get; }
        T this[int index] { get; set; }
    }
}
