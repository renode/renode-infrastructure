//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
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
            DataReceived?.Invoke(data);
        }

        public byte[] Read(int count = 1)
        {
            ReadRequested?.Invoke(count);
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
            this.Log(LogLevel.Noisy, "Transmission finished");
            TransmissionFinished?.Invoke();
        }

        public void Reset()
        {
            buffer.Clear();
        }

        public void EnqueueResponseByte(byte b)
        {
            buffer.Enqueue(b);
        }

        public void EnqueueResponseBytes(IEnumerable<byte> bs)
        {
            buffer.EnqueueRange(bs);
        }

        public event Action<byte[]> DataReceived;
        public event Action<int> ReadRequested;
        public event Action TransmissionFinished;

        private readonly Queue<byte> buffer;
    }
}
