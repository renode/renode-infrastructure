//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.Structure
{
    /// <summary>
    /// Point under which a peripheral can be registered.
    /// <remarks>
    /// Not every registration point type can be used for addressing the peripheral.
    /// Some peripherals allow registering via more than one type of registration point,
    /// which interally get converted to a type used for addressing and retrieval of registered
    /// peripherals.
    /// <remarks>
    /// </summary>
    public interface IRegistrationPoint
    {
        string PrettyString { get; }
    }
}

