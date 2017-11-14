//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class Place
    {
        public Place(byte[] array, int startIndex)
        {
            Array = array;
            StartIndex = startIndex;
        }

        public Place(long address)
        {
            Address = address;
        }

        public long? Address { get; private set; }
        public byte[] Array { get; private set; }
        public int? StartIndex { get; private set; }

        public static implicit operator Place(long address)
        {
            return new Place(address);
        }
    }
}

