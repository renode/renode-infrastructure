//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿namespace Antmicro.Renode.Core.Structure.Registers
{
    /// <summary>
    /// Register field that provides a boolean value. It's width is always equal to 1.
    /// </summary>
    public interface IFlagRegisterField : IRegisterField<bool>
    {
    }
}

