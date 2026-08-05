//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Mocks
{
    public class EchoI2CDevice : II2CPeripheral
    {
        public EchoI2CDevice()
        {
        }

        public void Write(byte[] data)
        {
            this.DebugLog("Written {0} bytes of data: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));
            if(lastAccessType != AccessType.Write)
            {
                internalBuffer.Clear();
            }
            internalBuffer.EnqueueRange(data);
            lastAccessType = AccessType.Write;
        }

        public void Reset()
        {
            lastAccessType = null;
            readBuffer.Clear();
            internalBuffer.Clear();
        }

        public byte[] Read(int count = 1)
        {
            if(lastAccessType != AccessType.Read)
            {
                readBuffer.Clear();
                readBuffer.EnqueueRange(internalBuffer);
            }
            var result = readBuffer.DequeueRange(count).Concat(Misc.Iterate(() => (byte)0)).Take(count).ToArray();
            this.DebugLog("Read {0} bytes: {1}", count, Misc.PrettyPrintCollectionHex(result));
            lastAccessType = AccessType.Read;
            return result;
        }

        public void FinishTransmission()
        {
            this.DebugLog("Finishing transmission");
            lastAccessType = null;
        }

        private AccessType? lastAccessType;

        private readonly Queue<byte> internalBuffer = new();
        private readonly Queue<byte> readBuffer = new();

        public enum AccessType
        {
            Read,
            Write
        }
    }
}
