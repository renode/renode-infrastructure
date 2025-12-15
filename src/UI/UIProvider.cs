//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Diagnostics;
using System.IO;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UI
{
    class UIProvider : IDisposable
    {
        public UIProvider(int port)
        {
            if(!FindUIExecutable(out var path))
            {
                Logger.Log(LogLevel.Error, "Couldn't find the {0} executable", UIExecutableName);
                return;
            }
            process = new Process
            {
                StartInfo = new ProcessStartInfo { FileName = path, ArgumentList = { "--renode-port=" + port.ToString() } },
                EnableRaisingEvents = true
            };
            process.Exited += (_, __) => WindowClosed?.Invoke();
            process.Start();
        }

        public void Dispose()
        {
            if(process == null)
            {
                return;
            }
            process.Kill();
            process.Dispose();
            process = null;
        }

        public event Action WindowClosed;

        private static bool TryUIPath(string prefix, out string path)
        {
            path = Path.Join(prefix, UIExecutableName);
            Logger.Log(LogLevel.Info, "Looking for {0} at {1}", UIExecutableName, path);
            return File.Exists(path);
        }

        private static bool FindUIExecutable(out string path)
        {
            // Try to find binary next to assembly
            // This is needed for non-portable builds since the executable may actually be the `dotnet` binary and not Renode-specific
            if(TryUIPath(AppDomain.CurrentDomain.BaseDirectory, out path))
            {
                return true;
            }
            // Fallback for portable - look for binary near executable directory
            if(TryUIPath(Misc.ExecutableDirectory, out path))
            {
                return true;
            }
            return false;
        }

        private Process process;

#if PLATFORM_WINDOWS
        private const string UIExecutableName = "renode-ui.exe";
#else
        private const string UIExecutableName = "renode-ui";
#endif
    }
}
