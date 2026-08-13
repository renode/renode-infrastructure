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

namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    [AttributeUsage(AttributeTargets.Enum)]
    public class RegistersDescriptionAttribute : Attribute
    {
        public RegistersDescriptionAttribute(params string[] tags) : this(tags.Length == 0, tags)
        {
        }

        public RegistersDescriptionAttribute(bool isDefault, params string[] tags)
        {
            if(isDefault)
            {
                Array.Resize(ref tags, tags.Length + 1);
                tags[^1] = null;
            }
            this.tags = tags;
        }

        public bool Contains(string tag)
        {
            return tags.Contains(tag);
        }

        public IEnumerable<string> Tag => tags;

        private readonly string[] tags;
    }
}
