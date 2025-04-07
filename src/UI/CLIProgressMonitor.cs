//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UI
{
    public class CLIProgressMonitor : IProgressMonitorHandler
    {
        public CLIProgressMonitor()
        {
            updateTime = TimeSpan.FromSeconds(3);
        }

        public void Finish(int id)
        {
        }

        public void Update(int id, string description, int? progress)
        {
            var now = CustomDateTime.Now;
            if(now - lastUpdate > updateTime)
            {
                Logger.Log(LogLevel.Info, description);
                lastUpdate = now;
            }
        }

        private DateTime lastUpdate;

        private readonly TimeSpan updateTime;
    }
}