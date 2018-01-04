//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using System.Linq;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class PrimeCellIDHelper
    {
        public PrimeCellIDHelper(int peripheralSize, byte[] data, IPeripheral parent)
        {
            this.peripheralSize = peripheralSize;
            this.data = data.ToArray(); // to obtain a copy
            this.parent = parent;
            if(data.Length != 8)
            {
                throw new RecoverableException("You have to provide full peripheral id and prime cell id (8 bytes).");
            }
        }

        public byte Read(long offset)
        {
            if(offset >= peripheralSize || offset < peripheralSize - 8 * 4)
            {
                parent.LogUnhandledRead(offset);
                return 0;
            }
            return data[8 - (peripheralSize - offset)/4];
        }

        private readonly int peripheralSize;
        private readonly IPeripheral parent;
        private readonly byte[] data;
    }
}

