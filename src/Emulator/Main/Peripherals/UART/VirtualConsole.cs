//
// Copyright (c) 2010-2023 Antmicro
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
        public static void CreateVirtualConsole(this IMachine machine, string name)
        {
            var virtConsole = new VirtualConsole(machine);
            machine.RegisterAsAChildOf(machine.SystemBus, virtConsole, NullRegistrationPoint.Instance);
            machine.SetLocalName(virtConsole, name);
        }
    }

    public class VirtualConsole : IExternal, IUART
    {
        public VirtualConsole(IMachine machine)
        {
            bus = machine.SystemBus;
            fifo = new Queue<byte>();
            locker = new object();
        }

        public void Reset()
        {
            Clear();
        }

        public void Clear()
        {
            lock(locker)
            {
                fifo.Clear();
            }
        }

        public bool IsEmpty()
        {
            lock(locker)
            {
                return fifo.Count == 0;
            }
        }

        public void WriteChar(byte value)
        {
            lock(locker)
            {
                fifo.Enqueue(value);
            }
        }

        public byte[] ReadBuffer(int maxCount = 1)
        {
            lock(locker)
            {
                return GetFifoIterator().Take(maxCount).ToArray();
            }
        }

        public long WriteBufferToMemory(ulong address, int maxCount, ICPU context = null)
        {
            var buffer = ReadBuffer(maxCount);
            bus.WriteBytes(buffer, address, true, context);
            return buffer.Length;
        }

        public void DisplayChar(byte value)
        {
            CharReceived?.Invoke(value);
        }

        public uint BaudRate { get; set; }
        public Bits StopBits { get; set; }
        public Parity ParityBit { get; set; }

        [field: Transient]
        public event Action<byte> CharReceived;

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
