//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Runtime.InteropServices;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Utilities
{
    public class PtyUnixStream : Stream
    {
        public PtyUnixStream()
        {
            Init();
        }

        public override int ReadByte()
        {
            try
            {
                if(ReadTimeout > 0)
                {
                    var result = IsDataAvailable(ReadTimeout) ? base.ReadByte() : -2;
                    ReadTimeout = -1;
                    return result;
                }
                else
                {
                    return WaitUntilDataIsAvailable() ? base.ReadByte() : -1;
                }
            }
            catch(IOException)
            {
                return -1;
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var data = LibCWrapper.Read(masterFd, count);
            if(data == null)
            {
                throw new IOException("Failed to read pty");
            }
            Array.Copy(data, 0, buffer, offset, data.Length);
            return data.Length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            bool res;
            unsafe
            {
                fixed(byte* bufferPtr = &buffer[offset])
                {
                    res = LibCWrapper.Write(masterFd, (IntPtr)bufferPtr, count);
                }
            }
            if(!res)
            {
                throw new IOException("Failed to write pty");
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override bool CanTimeout => true;

        public override int ReadTimeout { get; set; }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public string SlaveName
        {
            get { return slaveName; }
            private set { slaveName = value; }
        }

        public int SlaveFd { get { return slaveFd; } }

        protected override void Dispose(bool disposing)
        {
            // masterFd will be closed by disposing the base
            base.Dispose(disposing);
            LibCWrapper.Close(slaveFd);
            disposed = true;
        }

        [DllImport("libc", EntryPoint = "getpt")]
        private static extern int Getpt();

        [DllImport("libc", EntryPoint = "grantpt")]
        private static extern int Grantpt(int fd);

        [DllImport("libc", EntryPoint = "unlockpt")]
        private static extern int Unlockpt(int fd);

        [DllImport("libc", EntryPoint = "cfmakeraw")]
        private static extern void Cfmakeraw(IntPtr termios); // TODO: this is non-posix, but should work on BSD

        [DllImport("libc", EntryPoint = "tcgetattr")]
        private static extern void Tcgetattr(int fd, IntPtr termios);

        [DllImport("libc", EntryPoint = "tcsetattr")]
        private static extern void Tcsetattr(int fd, int attr, IntPtr termios);

        [DllImport("libutil.so.1", EntryPoint = "openpty")]
        private static extern int OpenptyLinux(IntPtr amaster, IntPtr aslave, IntPtr name, IntPtr termp, IntPtr winp);

        [DllImport("util", EntryPoint = "openpty")]
        private static extern int OpenptyMacOS(IntPtr amaster, IntPtr aslave, IntPtr name, IntPtr termp, IntPtr winp);

        private static string OpenNewSlavePty(out int masterFd, out int slaveFd)
        {
            var amaster = Marshal.AllocHGlobal(4);
            var aslave = Marshal.AllocHGlobal(4);
            var name = Marshal.AllocHGlobal(1024);

            IntPtr termios = Marshal.AllocHGlobal(128); // termios struct is 60-bytes, but we allocate more just to make sure
            Tcgetattr(0, termios);
            Cfmakeraw(termios);

            int result;
            if(RuntimeInfo.IsLinux())
            {
                result = OpenptyLinux(amaster, aslave, name, termios, IntPtr.Zero);
            }
            else
            {
                result = OpenptyMacOS(amaster, aslave, name, termios, IntPtr.Zero);
            }
            if(result == -1)
            {
                throw new IOException("Failed to open pty");
            }

            masterFd = Marshal.ReadInt32(amaster);
            slaveFd = Marshal.ReadInt32(aslave);
            var slaveName = Marshal.PtrToStringAnsi(name);

            Marshal.FreeHGlobal(amaster);
            Marshal.FreeHGlobal(aslave);
            Marshal.FreeHGlobal(name);
            Marshal.FreeHGlobal(termios);

            var gptResult = Grantpt(masterFd);
            if(gptResult == -1)
            {
                throw new IOException("Failed to grant access to pty");
            }
            var uptResult = Unlockpt(masterFd);
            if(uptResult == -1)
            {
                throw new IOException("Failed to unlock pty");
            }

            return slaveName;
        }

        private bool WaitUntilDataIsAvailable()
        {
            int pollResult;
            bool retry;
            var pollData = new[] { new Pollfd { Fd = masterFd, Events = PollEvents.POLLIN } };
            do
            {
                retry = false;
                pollResult = LibCWrapper.Poll(pollData, -1);
                // here we compare flag using == operator as we want only POLLHUP to
                // activate the condition
                if(pollResult == 1 && pollData[0].Revents == PollEvents.POLLHUP)
                {
                    // this is necessary as poll will result with PollHup when
                    // client disconnects from slave tty; we want to allow to
                    // connect again
                    System.Threading.Thread.Sleep(HangUpCheckPeriod);
                    retry = true;
                }
            }
            while(!disposed && (retry || LibCWrapper.ShouldRetrySyscall(pollResult)));
            // here we don't use simple == operator to detect POLLIN, as it turns out
            // that POLLHUP is quite sticky - once it is reported it stays forever
            return pollResult == 1 && (pollData[0].Revents & PollEvents.POLLIN) != 0;
        }

        private bool IsDataAvailable(int timeout, out int pollResult)
        {
            var pollData = new[] { new Pollfd { Fd = masterFd, Events = PollEvents.POLLIN } };
            do
            {
                pollResult = LibCWrapper.Poll(pollData, timeout);
            }
            while(!disposed && LibCWrapper.ShouldRetrySyscall(pollResult));
            return pollResult > 0;
        }

        private bool IsDataAvailable(int timeout, bool throwOnError = true)
        {
            int pollResult;
            IsDataAvailable(timeout, out pollResult);
            if(throwOnError && pollResult == -1)
            {
                throw new IOException("Failed to poll for data");
            }
            return pollResult > 0;
        }

        [PostDeserialization]
        private void Init()
        {
            SlaveName = OpenNewSlavePty(out masterFd, out slaveFd);
        }

        [DllImport("libc", EntryPoint = "ptsname")]
        static extern IntPtr Ptsname(int fd);

        [Transient]
        private string slaveName;

        [Transient]
        private int masterFd;

        [Transient]
        private int slaveFd;

        private bool disposed;

        private const int HangUpCheckPeriod = 500;
    }
}
