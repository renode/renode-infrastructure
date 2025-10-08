//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.IO;

namespace Antmicro.Renode.Utilities
{
    public class SimpleFileCache
    {
        public SimpleFileCache(string location, bool enabled = true)
        {
            Enabled = enabled;
            cacheLocation = Path.Combine(Emulator.UserDirectoryPath, location);

            internalCache = new HashSet<string>();
            Populate();
        }

        public bool ContainsEntryWithSha(string sha)
        {
            return Enabled && internalCache.Contains(sha);
        }

        public bool TryGetEntryWithSha(string sha, out string filename)
        {
            if(!Enabled || !ContainsEntryWithSha(sha))
            {
                filename = null;
                return false;
            }

            filename = Path.Combine(cacheLocation, sha);
            return true;
        }

        public void StoreEntryWithSha(string sha, string filename)
        {
            if(!Enabled || ContainsEntryWithSha(sha))
            {
                return;
            }

            EnsureCacheDirectory();
            using(var locker = new FileLocker(Path.Combine(cacheLocation, LockFileName)))
            {
                FileCopier.Copy(filename, Path.Combine(cacheLocation, sha), true);
                internalCache.Add(sha);
            }
        }

        public bool Enabled { get; set; }

        private void Populate()
        {
            if(!Enabled)
            {
                return;
            }
            EnsureCacheDirectory();
            using(var locker = new FileLocker(Path.Combine(cacheLocation, LockFileName)))
            {
                var dinfo = new DirectoryInfo(cacheLocation);
                foreach(var file in dinfo.EnumerateFiles())
                {
                    internalCache.Add(file.Name);
                }
            }
        }

        private void EnsureCacheDirectory()
        {
            Directory.CreateDirectory(cacheLocation);
        }

        private readonly HashSet<string> internalCache;
        private readonly string cacheLocation;

        private const string LockFileName = ".lock";
    }
}