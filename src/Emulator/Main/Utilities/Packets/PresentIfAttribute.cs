//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities.Packets
{
    /// <summary>
    /// Indicates that the decorated field or property is conditional and should only be processed
    /// (serialized or deserialized) if a specific boolean condition is met. <br />
    /// The member referenced by <see cref="ConditionPropertyName"/> must be a property or field
    /// of type <see cref="bool"/> within the same class. It must only rely on data
    /// from fields that are located earlier in the binary representation of the struct
    /// </summary>
    /// <remarks>
    /// When the condition evaluates to <c>false</c>, this field is completely omitted from the binary stream. <br />
    /// This causes the byte offset of all subsequent fields to shift if they do not have
    /// explicit fixed offsets (e.g. <c>[Offset(bytes: 16)]</c>).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class PresentIfAttribute : Attribute
    {
        public PresentIfAttribute(string conditionName)
        {
            this.ConditionPropertyName = conditionName;
        }

        public string ConditionPropertyName { get; }
    }
}
