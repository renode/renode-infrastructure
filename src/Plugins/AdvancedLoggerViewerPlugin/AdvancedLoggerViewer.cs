//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Plugins;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Logging.Backends;

namespace Antmicro.Renode.Plugins.AdvancedLoggerViewer
{
    [Plugin(Name = "AdvancedLoggerViewer", Version = "0.1", Description = "Viewer for advanced logger", Vendor = "Antmicro")]
    public class AdvancedLoggerViewer : IDisposable
    {
        public AdvancedLoggerViewer(Monitor monitor)
        {
            this.monitor = monitor;
            command = new AdvancedLoggerViewerCommand(monitor);
            monitor.RegisterCommand(command);
            
            // start lucene backend as soon as possible
            // in order to gather more log entries
            LuceneLoggerBackend.EnsureBackend();
        }
        
        public void Dispose()
        {
            monitor.UnregisterCommand(command);
        }
        
        private readonly Monitor monitor;
        private readonly AdvancedLoggerViewerCommand command;
    }
}

