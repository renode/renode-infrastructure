//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    public class RegisterMapper
    {
        public RegisterMapper(Type type, string tag = null)
        {
            tagString = tag is null ? "" : $" in \"{tag}\"";
            if(type.IsEnum)
            {
                RegisterEnumMapping(type);
                return;
            }

            var peripheralType = type;
            var types = peripheralType.GetAllNestedTypes();
            var interestingEnums = types.Where(t => t.GetCustomAttributes(false).Any(x => x is RegistersDescriptionAttribute attr && attr.Contains(tag))).ToList();

            if(interestingEnums.Count == 0)
            {
                interestingEnums = types.Where(t => t.BaseType == typeof(Enum) && t.Name.Contains("register", StringComparison.CurrentCultureIgnoreCase)).ToList();
            }

            foreach(var interestingEnum in interestingEnums)
            {
                RegisterEnumMapping(interestingEnum);
            }
        }

        public void RegisterEnumMapping(Type @enum)
        {
            if(!@enum.IsEnum)
            {
                throw new RecoverableException("@enum parameter must be an enum type");
            }

            foreach(var value in @enum.GetEnumValues())
            {
                var l = Convert.ToInt64(value);
                var s = Enum.GetName(@enum, value);

                if(!map.ContainsKey(l))
                {
                    map.Add(l, s);
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
                    name = $"{map[closest]}+0x{offset - closest:x}{tagString}";
                }
                else
                {
                    name = $"unknown{tagString}";
                }
            }
            else
            {
                name = map[offset];
            }

            return name;
        }

        private readonly string tagString;
        private readonly Dictionary<long, string> map = new Dictionary<long, string>();
    }
}
