//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using System.Linq;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities
{
    public class FilePath
    {
        public FilePath(string path, FileAccess fileAccess, bool validate = true)
        {
            this.path = path;
            this.fileAccess = fileAccess;

            if(validate)
            {
                Validate();
            }
        }

        public virtual void Validate()
        {
            if(!File.Exists(path))
            {
                throw new RecoverableException($"File does not exist: {path}");
            }

            try
            {
                using(var fs = File.Open(path, FileMode.Open, fileAccess, FileShare.ReadWrite))
                {
                    if(!fs.CanRead && fileAccess == FileAccess.Read)
                    {
                        throw new RecoverableException($"File is not readable: {path}");
                    }
                    if(!fs.CanWrite && fileAccess == FileAccess.Write)
                    {
                        throw new RecoverableException($"File is not writable: {path}");
                    }
                }
            }
            catch(UnauthorizedAccessException e)
            {
                throw new RecoverableException($"Error while accessing {path}: {e.Message}");
            }
        }

        public bool IsValid()
        {
            try
            {
                Validate();
            }
            catch(Exception)
            {
                return false;
            }

            return true;
        }

        public override string ToString()
        {
            return path;
        }

        public static implicit operator string(FilePath fp)
        {
            return fp.path;
        }

        protected bool CanBeCreated()
        {
            if(File.Exists(path))
            {
                return false;
            }

            try
            {
                using(File.Create(path))
                {
                }
                File.Delete(path);
            }
            catch
            {
                return false;
            }

            return true;
        }

        protected readonly string path;
        protected readonly FileAccess fileAccess;
    }

    public class ReadFilePath : FilePath
    {
        public ReadFilePath(string path, bool validate = true) : base(path, FileAccess.Read, validate) { }

        public static implicit operator ReadFilePath(string path)
        {
            return new ReadFilePath(path);
        }
    }

    public class OptionalReadFilePath : FilePath
    {
        public OptionalReadFilePath(string path, bool validate = true) : base(path, FileAccess.Read, false)
        {
            if(path != null && validate)
            {
                Validate();
            }
        }

        public static implicit operator string(OptionalReadFilePath fp)
        {
            return fp?.path;
        }

        public static implicit operator OptionalReadFilePath(string path)
        {
            return new OptionalReadFilePath(path);
        }

        public static implicit operator OptionalReadFilePath(ReadFilePath fp)
        {
            return new OptionalReadFilePath(fp);
        }
    }

    public class AppendFilePath : FilePath
    {
        public AppendFilePath(string path, bool validate = true) : base(path, FileAccess.Write, validate) { }

        public static implicit operator AppendFilePath(string path)
        {
            return new AppendFilePath(path);
        }
    }

    public class WriteFilePath : FilePath
    {
        public WriteFilePath(string path, bool validate = true) : base(path, FileAccess.Write, validate) { }

        public override void Validate()
        {
            if(!File.Exists(path))
            {
                if(!CanBeCreated())
                {
                    throw new RecoverableException($"File {path} could not be created");
                }
                return;
            }

            base.Validate();
        }

        public static implicit operator WriteFilePath(string path)
        {
            return new WriteFilePath(path);
        }
    }

    public class SequencedFilePath : WriteFilePath
    {
        public static SequencedFilePath Create(string path, out string movedFilePath)
        {
            var sequencedFilePath = new SequencedFilePath(path, false);
            sequencedFilePath.Validate(out movedFilePath);
            return sequencedFilePath;
        }

        public SequencedFilePath(string path, bool validate = true) : base(path, validate) { }

        public override void Validate() => Validate(out _);

        public static implicit operator SequencedFilePath(string path)
        {
            return new SequencedFilePath(path);
        }

        private bool Validate(out string movedFilePath)
        {
            if(!File.Exists(path))
            {
                if(!CanBeCreated())
                {
                    throw new RecoverableException($"File {path} could not be created");
                }
                movedFilePath = null;
                return false;
            }

            var lastSplit = path.LastIndexOf(Path.DirectorySeparatorChar);
            if(lastSplit == -1)
            {
                throw new RecoverableException($"{path} is an invalid path");
            }

            var dirPath = path.Substring(0, lastSplit);
            var fileName = path.Substring(lastSplit + 1);
            var pathGlob = string.Concat(fileName, ".*");

            var lastIndex = Directory.EnumerateFiles(dirPath, pathGlob)
                .Select(path => path.Substring(path.LastIndexOf('.') + 1))
                .Where(suffix => suffix.All(char.IsDigit))
                .Select(suffix => int.Parse(suffix))
                .Concat(new [] { 0 })
                .Max();

            movedFilePath = $"{path}.{lastIndex + 1}";

            try
            {
                File.Move(path, movedFilePath);
            }
            catch(Exception e)
            {
                throw new RecoverableException($"Error occured while moving old file to {movedFilePath}: {e.Message}");
            }

            Logger.Log(LogLevel.Info, "Old file {0} moved to {1}", path, movedFilePath);

            if(!CanBeCreated())
            {
                throw new RecoverableException($"File {movedFilePath} could not be created");
            }

            return true;
        }
    }
}
