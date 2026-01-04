//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Core.Structure
{
    /// <summary>
    /// Used to acquire an object that will represent this object for JSON serialization
    /// </summary>
    public interface IJsonSerializable
    {
        Object SerializeJson();
    }
}