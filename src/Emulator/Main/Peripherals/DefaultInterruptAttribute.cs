//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals
{
    /// <summary>
    /// This attribute indicates that an interrupt source (GPIO)
    /// can be used in REPL without name when connecting to an interrupt destination
    /// even if there are several interrupt sources within the peripheral.
    /// </summary>
    /// <remarks>
    /// Only one GPIO property within a peripheral should be marked with this attribute.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public class DefaultInterruptAttribute : Attribute
    {
    }
}

