//
// Copyright (c) 2010-2018 Antmicro
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

        public Place(ulong address)
        {
            Address = address;
        }

        public ulong? Address { get; private set; }
        public byte[] Array { get; private set; }
        public int? StartIndex { get; private set; }

        public static implicit operator Place(ulong address)
        {
            return new Place(address);
        }
    }
}

