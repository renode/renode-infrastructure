//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if !PLATFORM_WINDOWS
using System;
using Mono.Unix.Native;
using Mono.Unix;
using System.Runtime.InteropServices;
using System.IO;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using System.Threading;
using System.Threading.Tasks;

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
            Stream.Flush();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return Stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            Stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Stream.Write(buffer, offset, count);
        }

        public override bool CanRead { get { return Stream.CanRead; } }

        public override bool CanSeek { get { return Stream.CanSeek; } }

        public override bool CanWrite { get { return Stream.CanWrite; } }

        public override long Length { get { return Stream.Length; } }

        public override bool CanTimeout { get { return true; } }

        public override int ReadTimeout { get; set; }

        public override long Position 
        {
            get { return Stream.Position; }
            set { Stream.Position = value; }
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
            Syscall.close(slaveFd);
            disposed = true;
        }

        private static string OpenNewSlavePty(out int masterFd, out int slaveFd)
        {
            var amaster = Marshal.AllocHGlobal(4);
            var aslave = Marshal.AllocHGlobal(4);
            var name = Marshal.AllocHGlobal(1024);

            IntPtr termios = Marshal.AllocHGlobal(128); // termios struct is 60-bytes, but we allocate more just to make sure
            Tcgetattr(0, termios);
            Cfmakeraw(termios);

            int result = Openpty(amaster, aslave, name, termios, IntPtr.Zero);
            UnixMarshal.ThrowExceptionForLastErrorIf(result);

            masterFd = Marshal.ReadInt32(amaster);
            slaveFd = Marshal.ReadInt32(aslave);
            var slaveName = Marshal.PtrToStringAnsi(name);

            Marshal.FreeHGlobal(amaster);
            Marshal.FreeHGlobal(aslave);
            Marshal.FreeHGlobal(name);
            Marshal.FreeHGlobal(termios);

            var gptResult = Grantpt(masterFd);
            UnixMarshal.ThrowExceptionForLastErrorIf(gptResult);
            var uptResult = Unlockpt(masterFd);
            UnixMarshal.ThrowExceptionForLastErrorIf(uptResult);

            return slaveName;
        }

        private bool WaitUntilDataIsAvailable()
        {
            int pollResult;
            bool retry;
            var pollData = new[] { new Pollfd { fd = masterFd, events = PollEvents.POLLIN } };
            do
            {
                retry = false;
                pollResult = Syscall.poll(pollData, -1);
                // here we compare flag using == operator as we want only POLLHUP to
                // activate the condition
                if(pollResult == 1 && pollData[0].revents == PollEvents.POLLHUP)
                {
                    // this is necessary as poll will result with PollHup when
                    // client disconnects from slave tty; we want to allow to
                    // connect again 
                    System.Threading.Thread.Sleep(HangUpCheckPeriod);
                    retry = true;
                }
            }
            while(!disposed && (retry || UnixMarshal.ShouldRetrySyscall(pollResult)));
            // here we don't use simple == operator to detect POLLIN, as it turns out
            // that POLLHUP is quite sticky - once it is reported it stays forever
            return pollResult == 1 && (pollData[0].revents & PollEvents.POLLIN) != 0;
        }

        private bool IsDataAvailable(int timeout, out int pollResult)
        {
            var pollData = new[] { new Pollfd { fd = masterFd, events = PollEvents.POLLIN } };
            do
            {
                pollResult = Syscall.poll(pollData, timeout);
            }
            while(!disposed && UnixMarshal.ShouldRetrySyscall(pollResult));
            return pollResult > 0;
        }

        private bool IsDataAvailable(int timeout, bool throwOnError = true)
        {
            int pollResult;
            IsDataAvailable(timeout, out pollResult);
            if(throwOnError && pollResult < 0)
            {
                UnixMarshal.ThrowExceptionForLastError();
            }
            return pollResult > 0;
        }

        [PostDeserialization]
        private void Init()
        {
            SlaveName = OpenNewSlavePty(out masterFd, out slaveFd);
        }

        private UnixStream Stream 
        {
            get 
            {
                if(stream == null) 
                {
                    stream = new UnixStream(masterFd, true);
                }
                return stream;
            }
        }

        [DllImport("libc", EntryPoint = "getpt")]
        private extern static int Getpt();
     
        [DllImport("libc", EntryPoint = "grantpt")]
        private extern static int Grantpt(int fd);

        [DllImport("libc", EntryPoint = "unlockpt")]
        private extern static int Unlockpt(int fd);

        [DllImport("libc", EntryPoint="ptsname")]
        extern static IntPtr Ptsname(int fd);

        [DllImport("libc", EntryPoint = "cfmakeraw")]
        private extern static void Cfmakeraw(IntPtr termios); // TODO: this is non-posix, but should work on BSD

        [DllImport("libc", EntryPoint = "tcgetattr")]
        private extern static void Tcgetattr(int fd, IntPtr termios);

        [DllImport("libc", EntryPoint = "tcsetattr")]
        private extern static void Tcsetattr(int fd, int attr, IntPtr termios);

#if PLATFORM_LINUX
        [DllImport("libutil.so.1", EntryPoint = "openpty")]
#else
        [DllImport("util", EntryPoint = "openpty")]
#endif
        private extern static int Openpty(IntPtr amaster, IntPtr aslave, IntPtr name, IntPtr termp, IntPtr winp);

        [Transient]
        private UnixStream stream;

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
#endif
