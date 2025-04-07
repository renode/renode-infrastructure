//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Mono.Cecil;

namespace Antmicro.Renode.Utilities
{
    internal static class MemberReferenceExtensions
    {
        public static String GetFullNameOfMember(this MemberReference definition)
        {
            return definition.FullName.Replace('/', '+');
        }
    }
}