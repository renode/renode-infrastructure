//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;

namespace Antmicro.Renode.Core.Structure.Registers
{
    /// <summary>
    /// Register field that provides a value of type T, where T is an enumeration.
    /// The maximum value of T must not exceed the field's width.
    /// </summary>
    public interface IEnumRegisterField<T> : IRegisterField<T> where T : struct, IConvertible
    {
    }
}
