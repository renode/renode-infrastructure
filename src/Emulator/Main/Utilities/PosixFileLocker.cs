//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;

namespace Antmicro.Renode.Utilities
{
    public class PosixFileLocker : IDisposable
    {
        public PosixFileLocker(string fileToLock)
        {
            file = fileToLock;
            fd = LibCWrapper.Creat(fileToLock, LibCWrapper.DEFFILEMODE);
            if(!TryDoFileLocking(fd, true))
            {
                throw new InvalidOperationException("File {0} not locked.".FormatWith(file));
            }
        }

        public void Dispose()
        {
            if(!TryDoFileLocking(fd, false))
            {
                throw new InvalidOperationException("File {0} not unlocked.".FormatWith(file));
            }
            LibCWrapper.Close(fd);
        }

        [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
        private static extern int Flock(int fd, FlockOperation operation);

        private static bool TryDoFileLocking(int fd, bool lockFile, FlockOperation? specificFlag = null)
        {
            if(fd < 0)
            {
                return false;
            }
            int res;
            do
            {
                res = Flock(fd, specificFlag ?? (lockFile ? FlockOperation.LOCK_EX : FlockOperation.LOCK_UN));
            }
            while(LibCWrapper.ShouldRetrySyscall(res));
            // if can't get lock ...
            return res == 0;
        }

        private readonly int fd;
        private readonly string file;

        [Flags]
        private enum FlockOperation
        {
            LOCK_SH = 1,
            LOCK_EX = 2,
            LOCK_NB = 4,
            LOCK_UN = 8
        }
    }
}
