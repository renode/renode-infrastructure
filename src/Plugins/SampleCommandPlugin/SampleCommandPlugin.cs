//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Plugins;
using Antmicro.Renode;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Plugins.SampleCommandPlugin
{
    [Plugin(Name = "Sample command plugin", Version = "1.0", Description = "Sample plugin providing \"hello\" command.", Vendor = "Antmicro")]
    public sealed class SampleCommandPlugin : IDisposable
    {
        public SampleCommandPlugin(Monitor monitor)
        {
            this.monitor = monitor;            
            helloCommand = new HelloCommand(monitor);
            monitor.RegisterCommand(helloCommand);
        }

        public void Dispose()
        {
            monitor.UnregisterCommand(helloCommand);
        }

        private readonly HelloCommand helloCommand;
        private readonly Monitor monitor;
    }
}
