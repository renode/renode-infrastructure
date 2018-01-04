//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities
{
    public static class TypeResolver
    {
        public static Type ResolveType(string name)
        {
            var isNullable = false;
            if (name.StartsWith("System.Nullable`1<"))
            {
                isNullable = true;
                name = name.Substring(18);
                name = name.Substring(0, name.Length - 1);
            }

            Type result;
            if (!typesMap.TryGetValue(name, out result))
            {
                return null;
            }

            return isNullable ? typeof(Nullable<>).MakeGenericType(result) : result;

        }

        private static readonly Dictionary<string, Type> typesMap = new Dictionary<string, Type>()
        {
            { "System.Int32", typeof(Int32) }
        };
    }
}

