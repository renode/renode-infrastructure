//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities.Collections;
using System.Collections.Generic;
using AntShell.Terminal;
using System.Threading.Tasks;
using Antmicro.Migrant;
using System.Threading;
using Antmicro.Renode.Time;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.UART
{
    public class UARTBackend : IAnalyzableBackend<IUART>
    {
        public UARTBackend()
        {
            history = new CircularBuffer<byte>(BUFFER_SIZE);
            actionsDictionary = new Dictionary<IOProvider, Action<byte>>();
        }

        public void Attach(IUART uart)
        {
            UART = uart;
            UART.CharReceived += b =>
            {
                lock(lockObject)
                {
                    history.Enqueue(b);
                }
            };
        }

        public void BindAnalyzer(IOProvider io)
        {
            this.io = io;
            io.ByteRead += b =>
            {
                if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
                {
                    // it happens when writing from uart analyzer
                    vts = new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
                }

                UART.GetMachine().HandleTimeDomainEvent(UART.WriteChar, (byte)b, vts);
            };

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
                UART.CharReceived -= actionsDictionary[io];
                actionsDictionary.Remove(io);
            }
        }

        public IUART UART { get; private set; }

        public IAnalyzable AnalyzableElement { get { return UART; } }

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

        [Transient]
        private IOProvider io;
        private Dictionary<IOProvider, Action<byte>> actionsDictionary;
        private readonly CircularBuffer<byte> history;
        private object lockObject= new object();

        private const int BUFFER_SIZE = 100000;
    }
}

