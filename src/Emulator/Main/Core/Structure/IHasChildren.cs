//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

namespace Antmicro.Renode.Core.Structure
{
    public interface IHasChildren<out T>
    {
        IEnumerable<string> GetNames();
        T TryGetByName(string name, out bool success);
    }

    public static class IHasChildrenHelper
    {
        public static bool TryGetByName<T>(this IHasChildren<T> @this, string name, out T child)
        {
            bool success;
            child = @this.TryGetByName(name, out success);
            return success;
        }
    }
}

