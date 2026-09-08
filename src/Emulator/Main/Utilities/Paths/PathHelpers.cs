//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;

namespace Antmicro.Renode.Utilities
{
    public static class PathHelpers
    {
        public static string StripPathPrefix(string path, string prefix)
        {
            if(String.IsNullOrEmpty(prefix))
            {
                return path;
            }
            return path.StartsWith(prefix, StringComparison.Ordinal) ? path.Substring(prefix.Length + (prefix.EndsWith(Path.DirectorySeparatorChar) ? 0 : 1)) : path;
        }

        public static string SanitizePathSeparator(string baseString)
        {
            var sanitizedFile = baseString.Replace("\\", "/");
            if(sanitizedFile.Contains("/ "))
            {
                sanitizedFile = sanitizedFile.Replace("/ ", "\\ ");
            }
            return sanitizedFile;
        }

        public static bool TryGetFilenameFromAvailablePaths(string fileName, IEnumerable<string> paths, out string fullPath)
        {
            fullPath = String.Empty;
            foreach(var pathElement in paths.Prepend(String.Empty))
            {
                var currentPath = Path.Combine(pathElement, fileName);
                if(File.Exists(currentPath) || Directory.Exists(currentPath))
                {
                    fullPath = Path.GetFullPath(currentPath);
                    return true;
                }
            }
            return false;
        }
    }
}
