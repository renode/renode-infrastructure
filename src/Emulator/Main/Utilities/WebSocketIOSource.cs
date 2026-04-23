//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using AntShell.Helpers;
using AntShell.Terminal;

namespace Antmicro.Renode.Utilities
{
    public class WebSocketIOSource : IActiveIOSource, ISizeSource
    {
        public WebSocketIOSource(string endpoint)
        {
            server = new WebSocketSingleConnectionServer(endpoint, true);
            server.DataBlockReceived += (sender, bytes) =>
            {
                foreach(var b in bytes)
                {
                    ByteRead(b);
                }
            };
            server.Start();
            server.Resized += OnResize;
        }

        public void Dispose()
        {
            server.Resized -= OnResize;
            server.Dispose();
        }

        public void Flush()
        {
        }

        public void Pause()
        {
            // Required by IActiveIOSource interface
        }

        public void Resume()
        {
            // Required by IActiveIOSource interface
        }

        public void Write(byte b)
        {
            server.SendByte(b);
        }

        public bool IsAnythingAttached { get { return server.IsAnythingReceiving; } }

        public Position Size { get; private set; }

        public event Action<int> ByteRead;

        public event Action Resized;

        private void OnResize(int width, int height)
        {
            Size = new Position(width, height);
            Resized?.Invoke();
        }

        private readonly WebSocketSingleConnectionServer server;
    }
}