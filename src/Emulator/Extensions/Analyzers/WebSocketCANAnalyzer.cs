//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text.Json;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CAN;
using Antmicro.Renode.Time;
using Antmicro.Renode.Tools.Network;
using Antmicro.Renode.Utilities;

#nullable enable

namespace Antmicro.Renode.Analyzers;

[Transient]
public class WebSocketCANAnalyzer : BasicPeripheralBackendAnalyzer<CANBackend>, IExternal, IDisposable
{
    public override void AttachTo(CANBackend backend)
    {
        base.AttachTo(backend);
        StartServer();
    }

    public override void Clear()
    {
        // Intentionally empty, not relevant for CAN
    }

    public void Dispose()
    {
        server?.Dispose();
    }

    public override void Hide()
    {
        // Intentionally empty
    }

    public override void Show()
    {
        // Intentionally empty
    }

    /// <summary>
    /// Encodes a can message frame as json and sends it over the websocket connection
    /// </summary>
    private void SendFrameToWebsocket(IExternal canHubExternal, ICAN sender, CANMessageFrame message)
    {
        if(server is null)
        {
            this.WarningLog("Attempted to send a frame with no active websocket server");
            return;
        }
        var canHub = (CANHub)canHubExternal;
        DBCDecoder.DecodedMessage? decodedMessage = null;
        canHub.DbcDecoder?.TryDecodeCANMessage(message, out decodedMessage);
        var serializedFrame = JsonSerializer.SerializeToUtf8Bytes(new WrappedCANMessage(canHub, sender, message, decodedMessage));
        server.Send(serializedFrame);
    }

    private void StartServer()
    {
        var canHub = ((CANBackend)Backend).CANHub;
        if(!EmulationManager.Instance.CurrentEmulation.ExternalsManager.TryGetName(canHub, out var hubName))
        {
            this.WarningLog("Cannot open CAN endpoint for an unnamed CAN hub");
            return;
        }
        this.DebugLog("Opening CAN endpoint at /can/{0}", hubName);
        server = new WebSocketBufferedServer($"/can/{hubName}", true);
        server.Start();
        canHub.FrameReceived += SendFrameToWebsocket;
    }

    private WebSocketBufferedServer? server;

    private class WrappedCANMessage
    {
        public WrappedCANMessage(CANHub canHub, ICAN sender, CANMessageFrame message, DBCDecoder.DecodedMessage? decodedMessage)
        {
            CanHub = EmulationManager.Instance.CurrentEmulation.ExternalsManager.TryGetName(canHub, out var name) ? name : "";
            TimeStamp = TimeDomainsManager.Instance.GetEffectiveVirtualTimeStamp().TimeElapsed.ToString();
            Sender = sender.GetName();
            Message = message;
            DecodedMessage = decodedMessage;
        }

        public string CanHub { get; }

        public string Sender { get; }

        public string TimeStamp { get; }

        public CANMessageFrame Message { get; }

        public DBCDecoder.DecodedMessage? DecodedMessage { get; }
    }
}
