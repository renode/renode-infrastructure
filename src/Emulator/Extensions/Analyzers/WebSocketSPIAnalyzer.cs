//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Analyzers;

[Transient]
public class WebSocketSPIAnalyzer : BasicPeripheralBackendAnalyzer<SPIBackend>, IExternal, IDisposable
{
    public override void AttachTo(SPIBackend backend)
    {
        base.AttachTo(backend);
        machine = backend.SPIController.GetMachine();
        StartServer();
        aggregatorTimer = new Timer(_ => Send(), null, MaxAggregateTimeMs, MaxAggregateTimeMs);
    }

    public override void Show()
    {
        SPIController.BytesExchanged += ExchangedBytesToWebSocket;
    }

    public override void Hide()
    {
        SPIController.BytesExchanged -= ExchangedBytesToWebSocket;
        lock(messagesLock)
        {
            messages.Clear();
        }
    }

    public override void Clear()
    {
        // Nothing to do
    }

    public void Dispose()
    {
        aggregatorTimer.Dispose();
        server.Dispose();
    }

    public ISPIController SPIController => (Backend as SPIBackend)?.SPIController;

    public int SPINumber { get; private set; }

    private static int spiCount;

    private void StartServer()
    {
        SPINumber = Interlocked.Increment(ref spiCount);
        server = new WebSocketBufferedServer($"/spiAnalyzer/{SPINumber}", true);
        server.Start();
    }

    private void ExchangedBytesToWebSocket((byte pico, byte poci) bytes, IPeripheral slave)
    {
        var data = new SPIMessage
        {
            TimeStamp = machine.ElapsedVirtualTime.TimeElapsed.ToString(),
            PICO = bytes.pico,
            POCI = bytes.poci,
            PeripheralName = machine.TryGetLocalName(slave, out var name) ? name : "",
        };
        lock(messagesLock)
        {
            messages.Enqueue(data);
            if(messages.Count >= MaxAggregateCount)
            {
                Send();
            }
        }
    }

    private void Send()
    {
        byte[] serialized;
        lock(messagesLock)
        {
            if(messages.Count > 0)
            {
                serialized = JsonSerializer.SerializeToUtf8Bytes(messages);
                messages.Clear();
            }
            else
            {
                serialized = null;
            }
        }
        if(serialized != null)
        {
            server.Send(serialized);
        }
    }

    private WebSocketBufferedServer server;
    private IMachine machine;
    private Timer aggregatorTimer;
    private readonly Queue<SPIMessage> messages = new Queue<SPIMessage>();
    private readonly object messagesLock = new object();

    private const uint MaxAggregateCount = 60; // empirical value
    private const uint MaxAggregateTimeMs = 250;

    private class SPIMessage
    {
        public string TimeStamp { get; init; }

        public byte PICO { get; init; }

        public byte POCI { get; init; }

        public string PeripheralName { get; init; }
    }
}
