//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Mocks
{
    public class DummyI2CSlave : II2CPeripheral
    {
        public DummyI2CSlave()
        {
            buffer = new Queue<byte>();
        }

        public void Write(byte[] data)
        {
            this.Log(LogLevel.Debug, "Received {0} bytes: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));
        }

        public byte[] Read(int count = 1)
        {
            var dataToReturn = buffer.DequeueRange(count);
            if(dataToReturn.Length < count)
            {
                this.Log(LogLevel.Debug, "Not enough data in buffer, filling rest with zeros.");
                dataToReturn = dataToReturn.Concat(Enumerable.Repeat(default(byte), count - dataToReturn.Length)).ToArray();
            }
            return dataToReturn;
        }

        public void FinishTransmission()
        {
            // Intentionally left blank.
        }

        public void Reset()
        {
            buffer.Clear();
        }

        private readonly Queue<byte> buffer;
    }
}
