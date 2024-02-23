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
    /// <summary>
    /// A dummy I2C slave peripheral implementing <see cref="II2CPeripheral"> for mocking communication and testing I2C controllers.
    /// Model supports queuing data to its buffer, that will be return upon I2C reads. Additionally it exposes events that allow mocking more complex models.
    /// </summary>
    public class DummyI2CSlave : II2CPeripheral
    {
        /// <summary>
        /// Creates a model instance.
        /// </summary>
        public DummyI2CSlave()
        {
            buffer = new Queue<byte>();
        }

        /// <summary>
        /// Implements <see cref="II2CPeripheral">.
        /// Logs the received <paramref name="data"/>.
        /// </summary>
        /// <remarks>
        /// This method invokes <see cref="DataReceived"> event.
        /// </remarks>
        public void Write(byte[] data)
        {
            this.Log(LogLevel.Debug, "Received {0} bytes: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));
            DataReceived?.Invoke(data);
        }

        /// <summary>
        /// Implements <see cref="II2CPeripheral">.
        /// Read will access the buffer first, upto <paramref name="count"/> bytes, then fill any remaining space with zeros.
        /// </summary>
        /// <remarks>
        /// This method invokes <see cref="ReadRequested"> event before attempting to return data from the buffer.
        /// </remarks>
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

        /// <summary>
        /// Implements <see cref="II2CPeripheral">.
        /// </summary>
        /// <remarks>
        /// This method invokes <see cref="TransmissionFinished"> event.
        /// </remarks>
        public void FinishTransmission()
        {
            this.Log(LogLevel.Noisy, "Transmission finished");
            TransmissionFinished?.Invoke();
        }

        /// <summary>
        /// Implements <see cref="IPeripheral">.
        /// Will clear the internal buffer.
        /// </summary>
        public void Reset()
        {
            buffer.Clear();
        }

        /// <summary>
        /// Enqueues a byte to the internal buffer.
        /// </summary>
        public void EnqueueResponseByte(byte b)
        {
            buffer.Enqueue(b);
        }

        /// <summary>
        /// Enqueues bytes to the internal buffer.
        /// </summary>
        public void EnqueueResponseBytes(IEnumerable<byte> bs)
        {
            buffer.EnqueueRange(bs);
        }

        /// <summary>
        /// Informs about the <see cref="Write"> method being called with the first argument being the data written to the model.
        /// </summary>
        public event Action<byte[]> DataReceived;

        /// <summary>
        /// Informs about the <see cref="Read"> method being called with the first argument being the number of bytes requested to read from the model.
        /// The event is called before any access to the internal buffer allowing to enqeueue a response with <see cref="EnqueueResponseBytes"> or <see cref="EnqueueResponseByte"> methods.
        /// </summary>
        public event Action<int> ReadRequested;

        /// <summary>
        /// Informs about the <see cref="FinishTransmission"> method being called.
        /// </summary>
        public event Action TransmissionFinished;

        private readonly Queue<byte> buffer;
    }
}
