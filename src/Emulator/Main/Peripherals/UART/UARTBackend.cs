//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities.Collections;

using AntShell.Terminal;

namespace Antmicro.Renode.Peripherals.UART
{
    public class UARTBackend : IAnalyzableBackend<IUART>
    {
        public UARTBackend()
        {
            history = new CircularBuffer<byte>(BUFFER_SIZE);
            actionsDictionary = new Dictionary<IOProvider, Action<byte>>();
        }

        public void RepeatHistory(Action beforeRepeatingHistory = null)
        {
            lock(lockObject)
            {
                if(beforeRepeatingHistory != null)
                {
                    beforeRepeatingHistory();
                }

                foreach(var b in history)
                {
                    io.Write(b);
                }
            }
        }

        public void Attach(IUART uart)
        {
            UART = uart;
            UART.CharReceived += EnqueueHistory;
        }

        public void Detach()
        {
            UART.CharReceived -= EnqueueHistory;
            UART = null;
        }

        public void BindAnalyzer(IOProvider io)
        {
            this.io = io;
            io.ByteRead += ByteRead;

            Action<byte> writeAction = (b =>
            {
                lock(lockObject)
                {
                    io.Write(b);
                }
            });

            var mre = new ManualResetEventSlim();
            Task.Run(() =>
            {
                lock(lockObject)
                {
                    mre.Set();
                    RepeatHistory();
                    UART.CharReceived += writeAction;
                    actionsDictionary.Add(io, writeAction);
                }
            });
            mre.Wait();
        }

        public void UnbindAnalyzer(IOProvider io)
        {
            lock(lockObject)
            {
                io.ByteRead -= ByteRead;
                UART.CharReceived -= actionsDictionary[io];
                actionsDictionary.Remove(io);
            }
        }

        public string DumpHistoryBuffer(int limit = 0)
        {
            var result = new StringBuilder();
            var hasLimit = limit > 0;
            lock(lockObject)
            {
                foreach(var b in history)
                {
                    if(hasLimit)
                    {
                        if(limit == 0)
                        {
                            break;
                        }
                        limit--;
                    }

                    result.Append((char)b);
                }
            }
            return result.ToString();
        }

        public IUART UART { get; private set; }

        public IAnalyzable AnalyzableElement { get { return UART; } }

        private void EnqueueHistory(byte b)
        {
            lock(lockObject)
            {
                history.Enqueue(b);
            }
        }

        private void ByteRead(int b)
        {
            if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
            {
                // it happens when writing from uart analyzer
                vts = new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
            }

            UART.GetMachine().HandleTimeDomainEvent(UART.WriteChar, (byte)b, vts);
        }

        [PostDeserializationAttribute]
        private void ReAttach()
        {
            if(UART != null)
            {
                Attach(UART);
            }
        }

        [Transient]
        private IOProvider io;
        private readonly object lockObject = new object();
        [Constructor]
        private readonly Dictionary<IOProvider, Action<byte>> actionsDictionary;
        private readonly CircularBuffer<byte> history;

        private const int BUFFER_SIZE = 100000;
    }
}