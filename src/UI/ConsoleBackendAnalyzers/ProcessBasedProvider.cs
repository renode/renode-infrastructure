//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if !PLATFORM_WINDOWS
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using AntShell.Terminal;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UI
{
    public abstract class ProcessBasedProvider : IConsoleBackendAnalyzerProvider
    {
        // isMonitorWindows is not used for ProcessBasedProvider
        public bool TryOpen(string consoleName, out IIOSource io, bool isMonitorWindow = false)
        {
            var ptyUnixStream = new PtyUnixStream();
            io = new StreamIOSource(ptyUnixStream);

            if(!CheckScreenTool())
            {
                process = null;
                return false;
            }

            var commandString = $"{ScreenTool} {(ptyUnixStream.SlaveName)}";
            process = CreateProcess(consoleName, commandString);
            if(!RunProcess(process))
            {
                process = null;
                return false;
            }

            // here we give 1s time for screen to start; otherwise some initial data (e.g. banner could be lost)
            Thread.Sleep(1000);
            return true;
        }

        public void Close()
        {
            var p = process;
            if(p == null)
            {
                return;
            }

            try
            {
                p.CloseMainWindow();
            }
            catch(InvalidOperationException e)
            {
                // do not report an exception if the process has already exited
                if(!e.Message.Contains("finished") && !e.Message.Contains("exited"))
                {
                    throw;
                }
            }
            process = null;
        }

        public event Action OnClose;

        protected abstract Process CreateProcess(string consoleName, string command);

        protected void LogError(string source, string arguments, int exitCode)
        {
            Logger.LogAs(this, LogLevel.Error, "There was an error while starting {0} with arguments: {1}. It exited with code: {2}. In order to use different terminal, change preferences in configuration file.", source, arguments, exitCode);
        }

        protected void InnerOnClose()
        {
            OnClose?.Invoke();
        }

        private bool RunProcess(Process p)
        {
            try
            {
                p.Start();
                return true;
            }
            catch(Win32Exception e)
            {
                if(e.NativeErrorCode == 2)
                {
                    Logger.LogAs(this, LogLevel.Warning, "Could not find binary: {0}", p.StartInfo.FileName);
                }
                else
                {
                    Logger.LogAs(this, LogLevel.Error, "There was an error when starting process: {0}", e.Message);
                }
            }

            return false;
        }

        private bool CheckScreenTool()
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo(ScreenTool, "--help")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
            };
            return RunProcess(p);
        }

        private Process process;

        private const string ScreenTool = "screen";
    }
}

#endif
