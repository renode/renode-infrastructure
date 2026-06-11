//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Diagnostics;
using System.IO;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UI
{
    class UIProvider : IDisposable
    {
        public UIProvider(int port)
        {
            uiPort = port;
            if(!FindUIExecutable(out uiPath))
            {
                Logger.Log(LogLevel.Error, "Couldn't find the {0} executable", UIExecutableName);
                return;
            }
            if(RuntimeInfo.IsMacOS())
            {
                SignRenodeUI(uiPath);
            }
            process = new Process
            {
                StartInfo = StartInfo,
                EnableRaisingEvents = true
            };
            process.Exited += ProcessExited;
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

        private static void SignRenodeUI(string path)
        {
            using(var proc = new Process
            {
                StartInfo = new ProcessStartInfo { FileName = "codesign", ArgumentList = { "--sign", "-", "--force", path } },
                EnableRaisingEvents = true
            })
            {
                proc.Start();
                proc.WaitForExit();
            }
        }

        private static bool TryUIPath(string prefix, out string path)
        {
            path = Path.Join(prefix, UIExecutableName);
            if(RuntimeInfo.IsWindows())
            {
                path += ".exe";
            }
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

        private static void ShowXwtError(string primary, string secondary)
        {
            // Try to quickly spin up Xwt to show the message
            var provider = XwtProvider.Create(null);
            if(provider == null)
            {
                // Failed to open Xwt, at least print to console
                Logger.Log(LogLevel.Error, "{0}\n{1}", primary, secondary);
                return;
            }
            using(provider)
            {
                if(RuntimeInfo.IsMacOS())
                {
                    CrashHandler.ShowErrorWindow($"{primary}\n{secondary}");
                }
                else
                {
                    Xwt.MessageDialog.ShowError(primary, secondary);
                }
            }
        }

        private void ProcessExited(object _, EventArgs __)
        {
            // webkit2gtk (which is what renode-ui uses on Linux) silently crashes on some Nvidia GPUs, so try forcing software rendering if we crashed
            if(RuntimeInfo.IsLinux() && !triedSettingSoftwareGL)
            {
                Logger.Log(LogLevel.Info, "Failed to launch renode-ui, trying again with software-only libgl");
                triedSettingSoftwareGL = true;
                var startInfo = StartInfo;
                startInfo.Environment["LIBGL_ALWAYS_SOFTWARE"] = "true";
                process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };
                process.Exited += ProcessExited;
                process.Start();
                return;
            }
            if(process.ExitCode != 0)
            {
                // No advice for macOS since it should have Safari pre-installed
                var advice = "";
                if(RuntimeInfo.IsWindows())
                {
                    advice = "Make sure you have WebView2 installed";
                }
                else if(RuntimeInfo.IsLinux())
                {
                    advice = "Make sure you have libwebkit2gtk 4.0 or 4.1 installed";
                }
                else
                {
                    advice = "";
                }
                ShowXwtError("Failed to launch renode-ui", advice);
            }
            WindowClosed?.Invoke();
        }

        private ProcessStartInfo StartInfo => new ProcessStartInfo { FileName = uiPath, ArgumentList = { "--renode-port=" + uiPort.ToString() } };

        private Process process;

        private bool triedSettingSoftwareGL = false;

        private readonly string uiPath;
        private readonly int uiPort;

        private const string UIExecutableName = "renode-ui";
    }
}
