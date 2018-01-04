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
    [ConsoleBackendAnalyzerProvider("Putty")]
    public class PuttyProvider : ProcessBasedProvider
    {
        protected override Process CreateProcess(string consoleName, string command)
        {
            var p = new Process();
            p.EnableRaisingEvents = true;
            var arguments = string.Format("{0} -serial -title '{0}'", consoleName);
            p.StartInfo = new ProcessStartInfo("putty", arguments)
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
                    LogError("Putty", arguments, proc.ExitCode);
                }
                InnerOnClose();
            };

            return p;
        }
    }
}

#endif
