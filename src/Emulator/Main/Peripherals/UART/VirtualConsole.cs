//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.UART
{
    public static class VirtualConsoleExtensions
    {
        /// <summary>
        /// Creates a model instance and registers it in the machine's peripheral tree.
        /// </summary>
        public static void CreateVirtualConsole(this IMachine machine, string name)
        {
            var virtConsole = new VirtualConsole(machine);
            machine.RegisterAsAChildOf(machine.SystemBus, virtConsole, NullRegistrationPoint.Instance);
            machine.SetLocalName(virtConsole, name);
        }
    }

    /// <summary>
    /// A virtual console peripheral implementing <see cref="IUART"> for mocking, testing and scripting UART communication.
    /// Model supports queuing input data to its buffer, that is made available for the user. Additionally it exposes events
    /// that allow mocking or scripting more complex behaviours.
    /// </summary>
    public class VirtualConsole : IUART
    {
        /// <summary>
        /// Creates a model instance.
        /// </summary>
        public VirtualConsole(IMachine machine)
        {
            bus = machine.SystemBus;
            fifo = new Queue<byte>();
            locker = new object();
        }

        /// <summary>
        /// Implements <see cref="IPeripheral">.
        /// Will clear the internal buffer.
        /// </summary>
        public virtual void Reset()
        {
            Clear();
        }

        /// <summary>
        /// Clears the internal buffer.
        /// </summary>
        public void Clear()
        {
            lock(locker)
            {
                fifo.Clear();
            }
        }

        /// <summary>
        /// Checks whether the internal buffer is empty.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> if internal buffer is empty, <c>false</c> otherwise.
        /// </returns>
        public bool IsEmpty()
        {
            lock(locker)
            {
                return fifo.Count == 0;
            }
        }

        /// <summary>
        /// Checks whether the internal buffer contains <paramref name="value">.
        /// </summary>
        /// <param name="value">
        /// Byte value that is checked to be contained in the internal buffer.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> if internal buffer contains <paramref name="value">, <c>false</c> otherwise.
        /// </returns>
        public bool Contains(byte value)
        {
            lock(locker)
            {
                return fifo.Contains(value);
            }
        }

        /// <summary>
        /// Receives data.
        /// </summary>
        /// <param name="value">
        /// Byte value that is written to the internal buffer.
        /// </param>
        public void WriteChar(byte value)
        {
            lock(locker)
            {
                fifo.Enqueue(value);
                CharWritten?.Invoke(value);
            }
            if(Echo)
            {
                DisplayChar(value);
            }
        }

        /// <summary>
        /// Reads and removes data from the internal buffer.
        /// </summary>
        /// <param name="maxCount">
        /// Upper limit for the number of byte to be returned from the internal buffer.
        /// </param>
        /// <returns>
        /// Returns byte array of the oldest <paramref name="maxCount"> bytes or the whole internal buffer.
        /// </returns>
        public byte[] ReadBuffer(int maxCount = 1)
        {
            lock(locker)
            {
                return GetFifoIterator().Take(maxCount).ToArray();
            }
        }

        /// <summary>
        /// Reads all data from the internal buffer.
        /// </summary>
        /// <returns>
        /// Returns byte array with data from the internal buffer.
        /// </returns>
        public byte[] GetBuffer()
        {
            lock(locker)
            {
                return fifo.ToArray();
            }
        }


        /// <summary>
        /// Reads data from the internal buffer and stores it in the simulated memory.
        /// </summary>
        /// <param name="address">
        /// Address on system bus to be used to store the data.
        /// </param>
        /// <param name="maxCount">
        /// Upper limit for number of bytes to write to system bus.
        /// </param>
        /// <param name="context">
        /// The CPU that the write is performed as.
        /// </param>
        /// <returns>
        /// Returns number of bytes written to the system bus.
        /// </returns>
        public long WriteBufferToMemory(ulong address, int maxCount, ICPU context = null)
        {
            var buffer = ReadBuffer(maxCount);
            bus.WriteBytes(buffer, address, true, context);
            return buffer.Length;
        }

        /// <summary>
        /// Transmits byte <paramref name="value"/>.
        /// </summary>
        /// <param name="value">
        /// Byte value to be transmitted.
        /// </param>
        public virtual void DisplayChar(byte value)
        {
            CharReceived?.Invoke(value);
        }

        /// <summary>
        /// Controls automatic transmission of received data.
        /// </summary>
        public virtual bool Echo { get; set; } = true;

        public uint BaudRate { get; set; }
        public Bits StopBits { get; set; }
        public Parity ParityBit { get; set; }

        /// <summary>
        /// Called when a byte data is transmitted.
        /// </summary>
        [field: Transient]
        public event Action<byte> CharReceived;

        /// <summary>
        /// Called when a byte data is received.
        /// </summary>
        [field: Transient]
        public event Action<byte> CharWritten;

        private IEnumerable<byte> GetFifoIterator()
        {
            while(fifo.Count > 0)
            {
                yield return fifo.Dequeue();
            }
        }

        private readonly IBusController bus;
        private readonly object locker;
        private readonly Queue<byte> fifo;
    }
}
