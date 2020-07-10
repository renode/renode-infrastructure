//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace Antmicro.Renode.Utilities
{
    public class TemporaryFilesManager
    {
        static TemporaryFilesManager()
        {
            Initialize(Path.GetTempPath(), DefaultDirectoryPrefix);
        }

        public static TemporaryFilesManager Instance { get; private set; }

        public static void Initialize(string tempDirectory, string tempDirPrefix)
        {
            Instance = new TemporaryFilesManager(tempDirectory, tempDirPrefix);
        }

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

        public void Cleanup()
        {
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

        public event Action<string> OnFileCreated;

        public string EmulatorTemporaryPath
        {
            get
            { 
                return emulatorTemporaryPath;
            }
        }

        private TemporaryFilesManager(string tempDirectory, string tempDirPrefix)
        {
            if(AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                id = Process.GetCurrentProcess().Id.ToString();
            }
            else
            {
                id = string.Format("{0}-{1}", Process.GetCurrentProcess().Id, AppDomain.CurrentDomain.Id);
            }
            otherEmulatorTempPrefix = Path.Combine(tempDirectory, tempDirPrefix);
            emulatorTemporaryPath = otherEmulatorTempPrefix + id;

            if(!Directory.Exists(emulatorTemporaryPath))
            {
                Directory.CreateDirectory(emulatorTemporaryPath);
            }
            Cleanup();
        }

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

        ~TemporaryFilesManager()
        {
            Cleanup();
            ClearDirectory(emulatorTemporaryPath);           
        }

        private static void ClearDirectory(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch(Exception)
            {
                // we did everything we could
            }
        }

        public const string CrashSuffix = "-crash";

        private readonly string otherEmulatorTempPrefix;
        private readonly string emulatorTemporaryPath; 
        private readonly string id;

        private const string DefaultDirectoryPrefix = "renode-";
    }
}
