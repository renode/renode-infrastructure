//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Debug
{
    public enum FunctionCallParameterType
    {
        Ignore,
        Int64,
        UInt64,
        Int32,
        UInt32,
        Int16,
        UInt16,
        Byte,
        String,
        Int32Array,
        UInt32Array,
        ByteArray
    }
}
