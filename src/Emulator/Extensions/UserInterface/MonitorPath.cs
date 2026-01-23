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
using System.Linq;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UserInterface
{
    public class MonitorPath
    {
        public MonitorPath(string currentWorkingDirectory)
        {
            startingWorkingDirectory = currentWorkingDirectory;
            if(Misc.TryGetRootDirectory(out var rootDirectory))
            {
                defaultPath = new Stack<string>();
                defaultPath.Push(rootDirectory);
            }
            else
            {
                DefaultPath = ".";
            }
            Reset();
        }

        public void PushWorkingDirectory(string path)
        {
            Environment.CurrentDirectory = System.IO.Path.Combine(CurrentWorkingDirectory, path);
            workingDirectory.Push(Environment.CurrentDirectory);
        }

        public string PopWorkingDirectory()
        {
            Environment.CurrentDirectory = workingDirectory.Pop();
            return Environment.CurrentDirectory;
        }

        public void Prepend(string path)
        {
            foreach(var entry in GetDirEntries(path))
            {
                PushDirectory(path);
            }
        }

        public void PushDirectory(string path)
        {
            pathEntries.Push(path);
        }

        public string PopDirectory()
        {
            return pathEntries.Pop();
        }

        public void Reset()
        {
            Path = DefaultPath;
            workingDirectory.Push(startingWorkingDirectory);
            Prepend(CurrentWorkingDirectory);
        }

        public String CurrentWorkingDirectory
        {
            get { return workingDirectory.Peek(); }
        }

        public IEnumerable<string> PathElements
        {
            get { return pathEntries; }
        }

        public String Path
        {
            get
            {
                return pathEntries.Aggregate((x, y) => x + ';' + y);
            }

            set
            {
                pathEntries = GetDirEntries(value);
            }
        }

        public String DefaultPath
        {
            get { return defaultPath.Aggregate((x, y) => x + ';' + y); }

            private set
            {
                defaultPath = GetDirEntries(value);
            }
        }

        private Stack<string> GetDirEntries(string path)
        {
            var split = path.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries).Distinct().Reverse();
            var current = new Stack<string>();
            foreach(string entry in split)
            {
                var curentry = entry;//System.IO.Path.Combine(Environment.CurrentDirectory, entry);
                if(curentry.StartsWith("./"))
                {
                    curentry = curentry.Length > 2 ? curentry.Substring(2) : ".";
                }
                if(!Directory.Exists(curentry))
                {
                    throw new RecoverableException(String.Format("Entry {0} does not exist or is not a directory.", curentry));
                }
                current.Push(curentry);
            }
            return current;
        }

        private Stack<string> pathEntries = new Stack<string>();
        private Stack<string> defaultPath = new Stack<string>();
        private readonly Stack<string> workingDirectory = new Stack<string>();
        private readonly string startingWorkingDirectory;
        private readonly char[] pathSeparator = new []{';'};
    }
}
