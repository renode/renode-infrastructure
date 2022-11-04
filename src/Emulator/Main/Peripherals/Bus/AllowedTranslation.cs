//
// Copyright (c) 2010-2022 Antmicro
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
        ByteToQuadWord = 1 << 2,
        WordToByte = 1 << 3,
        WordToDoubleWord = 1 << 4,
        WordToQuadWord = 1 << 5,
        DoubleWordToByte = 1 << 6,
        DoubleWordToWord = 1 << 7,
        DoubleWordToQuadWord = 1 << 8,
        QuadWordToByte = 1 << 9,
        QuadWordToWord = 1 << 10,
        QuadWordToDoubleWord = 1 << 11,
    }
}

