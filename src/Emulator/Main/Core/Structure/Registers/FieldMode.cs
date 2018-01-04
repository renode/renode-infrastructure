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
        ReadToClear = 1 << 6
    }

    public static class FieldModeHelper
    {
        public static bool IsFlagSet(this FieldMode mode, FieldMode value)
        {
            return (mode & value) != 0;
        }

        public static bool IsReadable(this FieldMode mode)
        {
            return (mode & (FieldMode.Read | FieldMode.ReadToClear)) != 0;
        }

        public static bool IsWritable(this FieldMode mode)
        {
            return (mode & (FieldMode.Write | FieldMode.Set | FieldMode.Toggle | FieldMode.WriteOneToClear | FieldMode.WriteZeroToClear)) != 0;
        }

        public static FieldMode WriteBits(this FieldMode mode)
        {
            return mode & ~(FieldMode.Read | FieldMode.ReadToClear);
        }

        public static FieldMode ReadBits(this FieldMode mode)
        {
            return mode & (FieldMode.Read | FieldMode.ReadToClear);
        }

        public static bool IsValid(this FieldMode mode)
        {
            if((mode & (FieldMode.Read | FieldMode.ReadToClear)) == (FieldMode.Read | FieldMode.ReadToClear))
            {
                return false;
            }
            //the assumption that write flags are exclusive is used in BusRegister logic (switch instead of ifs)
            if(BitHelper.GetSetBits((uint)(mode & (FieldMode.Write | FieldMode.Set | FieldMode.Toggle | FieldMode.WriteOneToClear | FieldMode.WriteZeroToClear))).Count > 1)
            {
                return false;
            }
            return true;
        }
    }
}
