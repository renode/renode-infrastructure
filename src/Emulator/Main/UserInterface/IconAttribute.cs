//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.UserInterface
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
    public class IconAttribute : Attribute
    {
        public IconAttribute(string resource)
        {
            ResourceName = resource;
        }

        public string ResourceName { get; private set; }
    }
}

