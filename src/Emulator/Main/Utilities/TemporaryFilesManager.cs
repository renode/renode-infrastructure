//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities
{
    public class TemporaryFilesManager
    {
        static TemporaryFilesManager()
        {
            Initialize(Path.GetTempPath(), DefaultDirectoryPrefix, !EmulationManager.DisableEmulationFilesCleanup);
        }

        public static void Initialize(string tempDirectory, string tempDirPrefix, bool cleanFiles)
        {
            Instance = new TemporaryFilesManager(tempDirectory, tempDirPrefix, cleanFiles);
        }

        public static TemporaryFilesManager Instance { get; private set; }

        public string GetTemporaryFile(string fileNameSuffix = null)
        {
            lock(emulatorTemporaryPath)
            {
                string path;
                do
                {
                    var fileName = string.Format(fileNameSuffix != null ? $"{Guid.NewGuid()}-{fileNameSuffix}" : $"{Guid.NewGuid()}.tmp");
                    path = Path.Combine(emulatorTemporaryPath, fileName);
                    // this is guid, collision is very unlikely
                }
                while(File.Exists(path));

                using(File.Create(path))
                {
                    //that's the simplest way to create and NOT have the file open
                }

                var ofc = OnFileCreated;
                if(ofc != null)
                {
                    ofc(path);
                }

                return path;
            }
        }

        public bool TryCreateFile(string fileName, out string path)
        {
            path = Path.Combine(emulatorTemporaryPath, fileName);

            // check if the file exists, since File.Create would override the file
            if(File.Exists(path))
            {
                path = null;
                return false;
            }

            try
            {
                using(File.Create(path))
                {
                    //that's the simplest way to create and NOT have the file open
                }
            }
            catch(Exception)
            {
                path = null;
                return false;
            }

            return true;
        }

        public bool TryCreateDirectory(string dirName, out string path)
        {
            path = Path.Combine(emulatorTemporaryPath, dirName);

            if(Directory.Exists(path))
            {
                return true;
            }

            try
            {
                Directory.CreateDirectory(path);
            }
            catch(Exception)
            {
                path = null;
                return false;
            }

            return true;
        }

        public void Cleanup()
        {
            if(!shouldCleanFiles)
            {
                return;
            }

            foreach(var entry in Directory.GetDirectories(Directory.GetParent(emulatorTemporaryPath).FullName)
                .Where(x => x != emulatorTemporaryPath && x.StartsWith(otherEmulatorTempPrefix, StringComparison.Ordinal)
                    && !x.EndsWith(CrashSuffix, StringComparison.Ordinal)))
            {
                var pid = entry.Substring(otherEmulatorTempPrefix.Length);
                int processId;
                if(pid != null && int.TryParse(pid, out processId) && IsProcessAlive(processId))
                {
                    continue;
                }
                ClearDirectory(entry);
            }
        }

        public string EmulatorTemporaryPath
        {
            get
            {
                return emulatorTemporaryPath;
            }
        }

        public event Action<string> OnFileCreated;

        public const string CrashSuffix = "-crash";

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                return proc != null && !proc.HasExited;
            }
            catch(ArgumentException)
            {
                return false;
            }
        }

        private TemporaryFilesManager(string tempDirectory, string tempDirPrefix, bool cleanFiles)
        {
            shouldCleanFiles = cleanFiles;
            var pid = Process.GetCurrentProcess().Id;
            if(AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                id = pid.ToString();
            }
            else
            {
                id = string.Format("{0}-{1}", pid, AppDomain.CurrentDomain.Id);
            }
            otherEmulatorTempPrefix = Path.Combine(tempDirectory, tempDirPrefix);
            emulatorTemporaryPath = otherEmulatorTempPrefix + id;

            // Remove leftover directory from a previous run.
            // This can happen with persistent temp directory (e.g., in Docker)
            if(Directory.Exists(emulatorTemporaryPath))
            {
                if(!shouldCleanFiles)
                {
                    Logger.Log(LogLevel.Warning, "Clearing temporary directory with PID matching the currently running Renode instance ({0}) even though `--keep-temporary-files` is set, to ensure that Renode runs correctly", pid);
                }
                Directory.Delete(emulatorTemporaryPath, true);
            }
            Directory.CreateDirectory(emulatorTemporaryPath);

            Cleanup();
        }

        ~TemporaryFilesManager()
        {
            Cleanup();
            ClearDirectory(emulatorTemporaryPath);
        }

        private void ClearDirectory(string path)
        {
            if(!shouldCleanFiles)
            {
                return;
            }
            try
            {
                Directory.Delete(path, true);
            }
            catch(Exception)
            {
                // we did everything we could
            }
        }

        private readonly string otherEmulatorTempPrefix;
        private readonly string emulatorTemporaryPath;
        private readonly string id;
        private readonly bool shouldCleanFiles;

        private const string DefaultDirectoryPrefix = "renode-";
    }
}