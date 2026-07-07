//
// Copyright (c) 2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

namespace Antmicro.Renode.Core.Structure
{
    public sealed class EnumRegistrationPoint<E> : ITheOnlyPossibleRegistrationPoint
    where E : struct, Enum
    {
        public EnumRegistrationPoint(E name)
        {
            Name = name;
        }

        public E Name { get; private set; }

        public string PrettyString => nameof(E);
    }
}
