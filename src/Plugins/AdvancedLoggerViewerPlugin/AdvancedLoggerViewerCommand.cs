//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using Antmicro.Renode.UserInterface.Commands;
using Antmicro.Renode.UserInterface;
using Xwt;
using Antmicro.Renode.Logging.Backends;
using Antmicro.Renode.UI;

namespace Antmicro.Renode.Plugins.AdvancedLoggerViewer
{
    public class AdvancedLoggerViewerCommand : Antmicro.Renode.UserInterface.Commands.Command
    {
        public AdvancedLoggerViewerCommand(Monitor monitor) : base(monitor, "showLogger", "Advanced logger viewer")
        {
        }

        [Runnable]
        public void Run()
        {
            ApplicationExtensions.InvokeInUIThread(() => {
                var window = new Window();
                window.Width = 800;
                window.Height = 600;
                window.Content = new LogViewer(LuceneLoggerBackend.Instance);
                window.Show();
            });
        }
    }
}

