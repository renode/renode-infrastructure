//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.IO;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities
{
    public static class PlatformFileLoader
    {
        public static string FindPlatformFile(string filename)
        {
            if(!TryFindPlatformFile(filename, out var fullPath))
            {
                throw new ConstructionException($"Cannot find platform file {filename}");
            }

            return fullPath;
        }

        public static string CopyPlatformFile(string filename)
        {
            var baseFile = FindPlatformFile(filename);
            var file = TemporaryFilesManager.Instance.GetTemporaryFile();

            File.Copy(baseFile, file, true);
            Logger.Log(LogLevel.Noisy, $"Copying platform file {filename} to {file}");

            return file;
        }

        public static bool TryFindPlatformFile(string filename, out string path)
        {
            var filePath = Path.Combine("platform-lib", RuntimeInfo.RID, filename);

            // Try to find binary next to assembly
            // This is needed for non-portable builds since the executable may actually be the `dotnet` binary and not Renode-specific
            var assemblyPath = typeof(PlatformFileLoader).Assembly.Location;
            if(TryPlatformPath(Path.GetDirectoryName(assemblyPath), filePath, out path))
            {
                return true;
            }
            // Fallback for portable - look for binary near executable directory
            if(TryPlatformPath(Misc.ExecutableDirectory, filePath, out path))
            {
                return true;
            }
            return false;
        }

        private static bool TryPlatformPath(string prefix, string relativePath, out string path)
        {
            path = Path.Combine(prefix, relativePath);
            Logger.Log(LogLevel.Debug, $"Looking for {relativePath} at {path}");
            return File.Exists(path);
        }
    }
}
