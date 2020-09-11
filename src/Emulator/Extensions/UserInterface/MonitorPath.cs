//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UserInterface
{
    public class MonitorPath
    {
        private List<string> pathEntries = new List<string>();
        private List<string> defaultPath = new List<string>();
        private readonly string startingWorkingDirectory;
        private readonly char[] pathSeparator = new []{';'};
        private Stack<string> workingDirectory = new Stack<string>();

        public String CurrentWorkingDirectory
        {
            get{ return workingDirectory.Peek();}
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

        public IEnumerable<string> PathElements
        {
            get{ return pathEntries;}
        }

        private List<string> GetDirEntries(string path)
        {
            var split = path.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries).Distinct();
            var current = new List<string>();
            foreach (string entry in split)
            {
                var curentry = entry;//System.IO.Path.Combine(Environment.CurrentDirectory, entry);
                if (curentry.StartsWith("./"))
                {
                    curentry = curentry.Length > 2 ? curentry.Substring(2) : ".";
                }
                if (!Directory.Exists(curentry))
                {
                    throw new RecoverableException(String.Format("Entry {0} does not exist or is not a directory.", curentry));
                }
                current.Add(curentry);
            }
            return current;
        }

        public String Path
        {
            get
            {
                return pathEntries.Aggregate((x,y) => x + ';' + y);
            }
            set
            {
                pathEntries = GetDirEntries(value);
            }
        }

        public String DefaultPath
        {
            get{ return defaultPath.Aggregate((x,y) => x + ';' + y);}
            private set
            {
                defaultPath = GetDirEntries(value);
            }
        }

        public void Append(string path)
        {
            pathEntries.AddRange(GetDirEntries(path));
        }

        public void Reset()
        {
            Path = DefaultPath;
            workingDirectory.Push(startingWorkingDirectory);
            Append(CurrentWorkingDirectory);
        }

        public MonitorPath(string currentWorkingDirectory)
        {
            startingWorkingDirectory = currentWorkingDirectory;
            if(Misc.TryGetRootDirectory(out var rootDirectory))
            {
                defaultPath = new List<string> { rootDirectory };
            }
            else
            {
                DefaultPath = ".";
            }
            Reset();
        }
    }
}

