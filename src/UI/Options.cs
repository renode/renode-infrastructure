//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.OptionsParser;

namespace Antmicro.Renode.UI
{
    public class Options : IValidatedOptions
    {
        public bool Validate(out string error)
        {
            if(HideMonitor && Console)
            {
                error = "--hide-monitor and --console cannot be set at the same time";
                return false;
            }

            if(DisableXwt)
            {
                HideMonitor = true;
            }

#if NET
            if(ServerMode)
            {
                DisableXwt = true;
            }
            else if(ServerModePort != 21234 || !string.Equals(ServerModeWorkDir, "working-dir"))
            {
                error = "--server-mode-port and --server-mode-work-dir options are allowed only if --server-mode is enabled";
                return false;
            }
#else
            if(ServerMode)
            {
                error = "--server-mode is allowed only in .NET build";
                return false;
            }
#endif

            error = null;
            return true;
        }

        [Name('p', "plain"), DefaultValue(false), Description("Remove steering codes (e.g., colours) from output.")]
        public bool Plain { get; set; }

        [Name('P', "port"), DefaultValue(-1), Description("Instead of opening a window, listen for Monitor commands on the specified port.")]
        public int Port { get; set; }

        [Name('e', "execute"), Description("Execute command on startup (executed after the optional script). May be used many times.")]
        public string[] Execute { get; set; }

        [Name("config"), Description("Use the configuration file from the provided path, or create one if it does not exist")]
        public string ConfigFile { get; set; }

        [Name("disable-xwt"), Alias("disable-gui"), DefaultValue(false), Description("Disable XWT GUI support. It automatically sets HideMonitor.")]
        public bool DisableXwt { get; set; }

        [Name("file-to-include / snapshot"), PositionalArgument(0)]
        public string FilePath { get; set; }

        [Name("hide-monitor"), DefaultValue(false), Description("Do not show the Monitor window.")]
        public bool HideMonitor { get; set; }

        [Name("hide-log"), DefaultValue(false), Description("Do not show log messages in a console.")]
        public bool HideLog { get; set; }

        [Name("hide-analyzers"), DefaultValue(false), Description("Do not show analyzers.")]
        public bool HideAnalyzers { get; set; }

        [Name("pid-file"), Description("Write PID of the Renode instance to the provided file.")]
        public string PidFile { get; set; }

        [Name("robot-server-port"), DefaultValue(-1), Description("Start robot framework remote server on the specified port.")]
        public int RobotFrameworkRemoteServerPort { get; set; }

        [Name("robot-debug-on-error"), DefaultValue(false), Description("Initialize GUI for Robot tests debugging")]
        public bool RobotDebug { get; set; }

        [Name('v', "version"), DefaultValue(false), Description("Print version and exit.")]
        public bool Version { get; set; }

        [Name("console"), Description("Run the Monitor in the console instead of a separate window")]
        public bool Console { get; set; }

        [Name("keep-temporary-files"), Description("Don't clean temporary files on exit")]
        public bool KeepTemporaryFiles { get; set; }

        [Name("server-mode"), DefaultValue(false), Description("Insted of opening a window. Expose localhost:port/proxy endpoint (Works only in .NET build)")]
        public bool ServerMode { get; set; }

        [Name("server-mode-port"), DefaultValue(21234), Description("Set port for server mode")]
        public int ServerModePort { get; set; }

        [Name("server-mode-work-dir"), DefaultValue("working-dir"), Description("Set working directory for server mode (relative for renode)")]
        public string ServerModeWorkDir { get; set; }
    }
}