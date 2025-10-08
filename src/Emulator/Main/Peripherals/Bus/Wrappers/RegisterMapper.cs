//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    public class RegisterMapper
    {
        public RegisterMapper(Type peripheralType)
        {
            var types = peripheralType.GetAllNestedTypes();
            var interestingEnums = new List<Type>();

            var enumsWithAttribute = types.Where(t => t.GetCustomAttributes(false).Any(x => x is RegistersDescriptionAttribute));
            if(enumsWithAttribute != null)
            {
                interestingEnums.AddRange(enumsWithAttribute);
            }
            interestingEnums.AddRange(types.Where(t => t.BaseType == typeof(Enum) && t.Name.IndexOf("register", StringComparison.CurrentCultureIgnoreCase) != -1));

            foreach(var type in interestingEnums)
            {
                foreach(var value in type.GetEnumValues())
                {
                    var l = Convert.ToInt64(value);
                    var s = Enum.GetName(type, value);

                    if(!map.ContainsKey(l))
                    {
                        map.Add(l, s);
                    }
                }
            }
        }

        public string ToString(long offset)
        {
            string name;
            if(!map.ContainsKey(offset))
            {
                var closestCandidates = map.Keys.Where(k => k < offset).ToList();
                if(closestCandidates.Count > 0)
                {
                    var closest = closestCandidates.Max();
                    name = $"{map[closest]}+0x{offset - closest:x}";
                }
                else
                {
                    name = "unknown";
                }
            }
            else
            {
                name = map[offset];
            }

            return name;
        }

        private readonly Dictionary<long, string> map = new Dictionary<long, string>();

        [AttributeUsage(AttributeTargets.Enum)]
        public class RegistersDescriptionAttribute : Attribute { }
    }
}