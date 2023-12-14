//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Diagnostics;

namespace Antmicro.Renode.Utilities
{
    /// <summary>
    /// Sudo tools. Set of methods related to the process elevation.
    /// </summary>
    public static class SudoTools
    {
        /// <summary>
        /// Wraps the command into a sudo-tool call if necessary, creates a new Process object and exectues it.
        /// </summary>
        /// <description>
        /// Checks whether user is root. If it is exectues command, id not tries to elevate priviledged and run the command.
        /// </description>
        /// <returns>The sudo execute.</returns>
        /// <param name="command">Command.</param>
        /// <param name = "arguments">Command's arguments.</param>
        /// <param name="description">Optional description.</param>
        public static Process EnsureSudoExecute(string command, string arguments = "", string description = "")
        {
            Process process;
            process = Misc.IsRoot ? Process.Start(command, arguments) : SudoExecute(command + " " + arguments, description);
            return process;
        }

        /// <summary>
        /// Tries to wrap existing Process, with supported sudo tool. It switched the command's filename and arguments, and wraps them
        /// into a call to the supported sudo tool, if it's found.
        /// </summary>
        /// <param name="process">Process to be elevated.</param>
        /// <param name="description">Process description.</param>
        public static void EnsureSudoProcess(Process process, string description = "")
        {
            if(Misc.IsRoot)
            {
                return;
            }
            Process sudoProcess = process;
            string sudoName = FindSudoToolName();
            if(string.IsNullOrWhiteSpace(sudoProcess.StartInfo.FileName))
            {
                throw new ArgumentException("EnsureSudoProcess needs to work on a process with initliazed StartInfo.FileName.");
            }
            var command = sudoProcess.StartInfo.FileName + " " + sudoProcess.StartInfo.Arguments;
            sudoProcess.StartInfo.Arguments = SudoDecorateCommand(sudoName, command, description);
            sudoProcess.StartInfo.FileName = sudoName;
        }

        /// <summary>
        /// Finds the name of the sudo tool.
        /// </summary>
        /// <returns><c>true</c>, if sudo tool name was found, <c>false</c> otherwise.</returns>
        /// <param name="name">Sudo tool name, if found.</param>
        private static bool TryFindSudoToolName(out string name)
        {
            name = default(string);
            foreach(var nameToCheck in knownToolNames)
            {
                if(Misc.IsCommandAvaialble(nameToCheck))
                {
                    name = nameToCheck;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Finds the name of the sudo tool. Throwing version of <see cref="TryFindSudoToolName"/>.
        /// </summary>
        /// <returns>The sudo tool name.</returns>
        private static string FindSudoToolName()
        {
            string name;
            if(!TryFindSudoToolName(out name))
            {
                throw new PlatformNotSupportedException(
                    String.Format("Error: No supported sudo tool found. Supported tools are {0}.", string.Join(", ", knownToolNames))
                );
            }
            return name;
        }

        /// <summary>
        /// Tries to find sudo tool, and execute the command with elevated rigths.
        /// </summary>
        /// <returns>The Process object, after the execution.</returns>
        /// <param name="command">Command.</param>
        /// <param name="description">Description.</param>
        private static Process SudoExecute(string command, string description = "")
        {
            string sudoName = FindSudoToolName();
            Process process;
            command = SudoDecorateCommand(sudoName, command, description);
            process = Process.Start(sudoName, command);
            return process;
        }

        /// <summary>
        /// Decorates the command for a specific sudo tool.
        /// </summary>
        /// <returns>The decorated command.</returns>
        /// <param name="sudoName">Sudo tool name.</param>
        /// <param name="command">Command to be decorated.</param>
        /// <param name="description">Description.</param>
        private static string SudoDecorateCommand(string sudoName, string command, string description = "")
        {
            string result;

            switch(sudoName)
            {
                // Tool specific adjustments.
                case "gksudo":
                    result = string.Format(@"-D ""{0}"" ""{1}""", description, command);
                    break;
                case "kdesudo":
                    result = string.Format(@"-c ""{0}"" --comment ""{1}""", command, description);
                    break;
                case "pkexec":
                    // We do nothing, because 'pkexec' (version 0.105) doesn't accept description as a parameter.
                default:
                    result = command;
                    break;
            }

            return result;
        }

        /// <summary>
        /// List of supported tool names.
        /// </summary>
        private static string[] knownToolNames = { "gksudo", "kdesudo", "pkexec" };
    }
}

