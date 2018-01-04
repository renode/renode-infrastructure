//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Plugins.TracePlugin
{
    [Plugin(Name = "tracer", Description = "Tracing plugin", Version = "0.1", Vendor = "Antmicro")]
    public class TracePlugin : IDisposable
    {
        public TracePlugin(Monitor monitor)
        {
            this.monitor = monitor;           
            traceCommand = new TraceCommand(monitor);
            monitor.RegisterCommand(traceCommand);
        }

        public void Dispose()
        {
            monitor.UnregisterCommand(traceCommand);
        }

        private readonly TraceCommand traceCommand;
        private readonly Monitor monitor;
    }
}

