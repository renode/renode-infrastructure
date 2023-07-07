//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Migrant;
using System;

namespace Antmicro.Renode.Utilities.Binding
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ImportAttribute : TransientAttribute
    {
        public string Name { get; set; }
        public bool UseExceptionWrapper { get; set; } = true;
        // By default all [Import]s are required
        public bool Optional { get; set; }
    }
}

