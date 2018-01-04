//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Bus
{
    [Flags]
    public enum AllowedTranslation
    {
        ByteToWord = 1 << 0,
        ByteToDoubleWord = 1 << 1,
        WordToByte = 1 << 2,
        WordToDoubleWord = 1 << 3,
        DoubleWordToByte = 1 << 4,
        DoubleWordToWord = 1 << 5,
    }
}

