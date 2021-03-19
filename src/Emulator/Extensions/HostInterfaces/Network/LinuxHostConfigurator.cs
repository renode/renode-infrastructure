//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public class LinuxHostConfigurator : IHostMachineElement, IDisposable
    {
        public LinuxHostConfigurator()
        {
            configs = new List<ILinuxHostConfig>();
        }

        public void ApplyHostConfiguration()
        {
            this.Log(LogLevel.Info, "Applying host network configuration");

            var generatedFileName = TemporaryFilesManager.Instance.GetTemporaryFile();
            this.Log(LogLevel.Info, "Shell file {0}", generatedFileName);

            using(StreamWriter configFile = new StreamWriter(generatedFileName))
            {
                foreach(var cfg in configs)
                {
                    cfg.Apply(configFile);
                }
            }
            RunShellScript(generatedFileName);
        }

        public void Register(ILinuxHostConfig config)
        {
            configs.Add(config);
        }

        public void Dispose()
        {
            if(configs.Count == 0) return;
            this.Log(LogLevel.Info, "Rewoking host network configuration");
            configs.Reverse();

            var generatedFileName = TemporaryFilesManager.Instance.GetTemporaryFile();
            this.Log(LogLevel.Debug, "Shell file {0}", generatedFileName);

            using(StreamWriter configFile = new StreamWriter(generatedFileName))
            {
                foreach(var cfg in configs)
                {
                    cfg.Rewoke(configFile);
                }
            }

            RunShellScript(generatedFileName);
            configs.Clear();
        }

        private void RunShellScript(string generatedFileName)
        {
            var process = new Process();
            var output = string.Empty;
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = string.Format("-e {0}", generatedFileName);

            try
            {
                SudoTools.EnsureSudoProcess(process, "Renode Linux IPv4 Network");
            }
            catch(Exception ex)
            {
                throw new RecoverableException("Process elevation failed: " + ex.Message);
            }

            process.EnableRaisingEvents = true;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            var started = process.Start();
            if(started)
            {
                output = process.StandardError.ReadToEnd();
                process.WaitForExit();
            }

            if(!started || process.ExitCode != 0)
            {
                this.Log(LogLevel.Error, "Could not apply configuration file.");
                this.Log(LogLevel.Warning, "Exit code {0} Encountered error {1}", process.ExitCode, output);
                return;
            }
        }

        private List<ILinuxHostConfig> configs;
    }
}
