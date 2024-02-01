//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.DMA
{
    public enum TransferType
    {
        Byte = 1,
        Word = 2,
        DoubleWord = 4,
        QuadWord = 8,
    }
}
