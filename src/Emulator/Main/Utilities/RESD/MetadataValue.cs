//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities.RESD
{
    public struct MetadataValue
    {
        public MetadataValue(object value)
        {
            innerValue = value;
        }

        public bool TryAs<T>(out T value)
        {
            value = default(T);
            if(innerValue is T)
            {
                value = As<T>();
                return true;
            }
            return false;
        }

        public T As<T>()
        {
            return (T)innerValue;
        }

        public override string ToString()
        {
            return innerValue.ToString();
        }

        public Type InnerType => innerValue.GetType();

        private object innerValue;
    }

    public enum MetadataValueType
    {
        Reserved = 0x00,
        Int8 = 0x01,
        UInt8 = 0x02,
        Int16 = 0x03,
        UInt16 = 0x04,
        Int32 = 0x05,
        UInt32 = 0x06,
        Int64 = 0x07,
        UInt64 = 0x08,
        Float = 0x09,
        Double = 0x0A,
        String = 0x0B,
        Blob = 0x0C,
    }
}
