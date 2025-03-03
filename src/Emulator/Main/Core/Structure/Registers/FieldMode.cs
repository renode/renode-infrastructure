//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.Structure.Registers
{
    [Flags]
    public enum FieldMode
    {
        Read = 1 << 0,
        Write = 1 << 1,
        Set = 1 << 2,
        Toggle = 1 << 3,
        WriteOneToClear = 1 << 4,
        WriteZeroToClear = 1 << 5,
        ReadToClear = 1 << 6,
        WriteZeroToSet = 1 << 7,
        WriteZeroToToggle = 1 << 8,
        ReadToSet = 1 << 11,
        WriteToClear = 1 << 12
    }

    public static class FieldModeHelper
    {
        public static bool IsFlagSet(this FieldMode mode, FieldMode value)
        {
            return (mode & value) != 0;
        }

        public static bool IsReadable(this FieldMode mode)
        {
            return (mode & (FieldMode.Read | FieldMode.ReadToClear | FieldMode.ReadToSet)) != 0;
        }

        public static bool IsWritable(this FieldMode mode)
        {
            return (mode & (FieldMode.Write | FieldMode.Set | FieldMode.Toggle | FieldMode.WriteOneToClear | FieldMode.WriteZeroToClear |
                            FieldMode.WriteZeroToSet | FieldMode.WriteZeroToToggle | FieldMode.WriteToClear)) != 0;
        }

        public static FieldMode WriteBits(this FieldMode mode)
        {
            return mode & ~(FieldMode.Read | FieldMode.ReadToClear | FieldMode.ReadToSet);
        }

        public static FieldMode ReadBits(this FieldMode mode)
        {
            return mode & (FieldMode.Read | FieldMode.ReadToClear | FieldMode.ReadToSet);
        }

        public static bool IsValid(this FieldMode mode)
        {
            //the assumption that write flags are exclusive is used in BusRegister logic (switch instead of ifs)
            return !(BitHelper.GetSetBits((uint)ReadBits(mode)).Count > 1) && !(BitHelper.GetSetBits((uint)WriteBits(mode)).Count > 1);
        }
    }
}
