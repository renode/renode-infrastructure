//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.SPI;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Mocks
{
    /// <summary>
    /// A dummy SPI slave peripheral implementing <see cref="ISPIPeripheral"> for mocking communication and testing SPI controllers.
    /// Model supports queuing data to its buffer, that will be return upon SPI Transmit. Additionally it exposes events that allow mocking more complex models.
    /// Example:
    /// python """
    ///     dummy = monitor.Machine["sysbus.spi.dummy"]
    ///     dummy.DataReceived += lambda data: dummy.EnqueueValue(data)
    /// """
    /// </summary>
    public class DummySPISlave : ISPIPeripheral
    {
        public DummySPISlave()
        {
            buffer = new Queue<byte>();
        }

        public void EnqueueValue(byte val)
        {
            buffer.Enqueue(val);
        }

        public void FinishTransmission()
        {
            TransmissionFinished?.Invoke();
            idx = 0;
        }

        public void Reset()
        {
            buffer.Clear();
            idx = 0;
        }

        /// <summary>
        /// Implements <see cref="ISPIPeripheral">.
        /// Logs the received <paramref name="data"/>.
        /// </summary>
        /// <remarks>
        /// This method invokes <see cref="DataReceived"> event.
        /// </remarks>
        public byte Transmit(byte data)
        {
            DataReceived?.Invoke(data);
            this.Log(LogLevel.Debug, "Data received: 0x{0:X} (idx: {1})", data, idx);
            idx++;
            if(buffer.Count == 0)
            {
                this.Log(LogLevel.Debug, "No data left in buffer, returning 0.");
                return 0;
            }
            return buffer.Dequeue();
        }

        /// <summary>
        /// Informs about the <see cref="Write"> method being called with the first argument being the data written to the model.
        /// </summary>
        public event Action<byte> DataReceived;

        /// <summary>
        /// Informs about the <see cref="FinishTransmission"> method being called.
        /// </summary>
        public event Action TransmissionFinished;

        private int idx;
        private readonly Queue<byte> buffer;
    }
}
