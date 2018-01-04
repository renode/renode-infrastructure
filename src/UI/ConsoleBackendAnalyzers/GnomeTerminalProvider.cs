//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if PLATFORM_LINUX
using System.Diagnostics;

namespace Antmicro.Renode.UI
{
    [ConsoleBackendAnalyzerProvider("GnomeTerminal")]
    public class GnomeTerminalProvider : ProcessBasedProvider
    {
        protected override Process CreateProcess(string consoleName, string command)
        {
            var p = new Process();
            p.EnableRaisingEvents = true;
            var position = WindowPositionProvider.Instance.GetNextPosition();

            var arguments = string.Format("--tab -e \"{3}\" --title '{0}' --geometry=+{1}+{2}", consoleName, (int)position.X, (int)position.Y, command);
            p.StartInfo = new ProcessStartInfo("gnome-terminal", arguments)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true
            };
            p.Exited += (sender, e) =>
            {
                var proc = sender as Process;
                if (proc.ExitCode != 0)
                {
                    LogError("gnome-terminal", arguments, proc.ExitCode);
                }
                // We do not call InnerOnClose here, because gnome-terminal closes immediately after spawning new window.
            };
            return p;
        }
    }
}

#endif
