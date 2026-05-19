//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class SemihostingHandler : IDisposable
    {
        public SemihostingHandler(Arm cpu)
        {
            this.cpu = cpu;
        }

        public void Dispose()
        {
            ClearDescriptors();
        }

        public uint DoSemihosting(uint operation, uint argumentsAddress)
        {
            switch((Operation)operation)
            {
            case Operation.SYS_OPEN:
            {
                var pathPointer = GetArgumentField(argumentsAddress, 0);
                var mode = GetArgumentField(argumentsAddress, 1);
                var pathLength = (int)GetArgumentField(argumentsAddress, 2);
                LogReceived("SYS_OPEN", $"args: 0x{pathPointer:X} / {mode} / {pathLength}");

                if(pathLength <= 0)
                {
                    cpu.Log(LogLevel.Warning, "Semihosting: SYS_OPEN: Invalid path length: {0}; it has to be a positive number.", pathLength);
                    return unchecked((uint)-1);
                }

                // Check presence of the required null character
                var byteAfterPath = cpu.Bus.ReadByte((ulong)(pathPointer + pathLength), cpu);
                if(byteAfterPath != 0)
                {
                    cpu.Log(LogLevel.Warning, "Semihosting: SYS_OPEN: No null character after the path. Found: '{0}'", (char)byteAfterPath);
                    return unchecked((uint)-1);
                }

                var pathBytes = cpu.Bus.ReadBytes(pathPointer, pathLength, cpu);
                var path = Encoding.ASCII.GetString(pathBytes, 0, pathLength);
                return AddDescriptor("SYS_OPEN", path, mode);
            }
            case Operation.SYS_CLOSE:
            {
                var handle = GetArgumentField(argumentsAddress, 0);
                LogReceived("SYS_CLOSE", $"args: {handle}");
                if(RemoveDescriptor("SYS_CLOSE", handle))
                {
                    return 0;
                }
                else
                {
                    return unchecked((uint)-1);
                }
            }
            case Operation.SYS_WRITE:
            {
                var handle = GetArgumentField(argumentsAddress, 0);
                var sourcePointer = GetArgumentField(argumentsAddress, 1);
                var length = GetArgumentField(argumentsAddress, 2);
                LogReceived("SYS_WRITE", $"args: {handle} / 0x{sourcePointer:X} / {length}");

                if(TryGetSemihostingDescriptor("SYS_WRITE", handle, out var descriptor))
                {
                    var bytes = cpu.Bus.ReadBytes(sourcePointer, (int)length, cpu);
                    if(descriptor.TryWrite("SYS_WRITE", bytes, ref semihostingErrno))
                    {
                        return 0;
                    }
                }
                // Return value: "the number of bytes that are not written, if there is an error".
                return length;
            }
            case Operation.SYS_READ:
            {
                var handle = GetArgumentField(argumentsAddress, 0);
                var destinationPointer = GetArgumentField(argumentsAddress, 1);
                var length = GetArgumentField(argumentsAddress, 2);
                LogReceived("SYS_READ", $"args: {handle} / 0x{destinationPointer:X} / {length}");

                if(TryGetSemihostingDescriptor("SYS_READ", handle, out var descriptor))
                {
                    if(descriptor.TryRead("SYS_READ", (int)length, out var bytesCount, out var bytesRead, ref semihostingErrno))
                    {
                        cpu.Bus.WriteBytes(bytesRead, destinationPointer, bytesCount, context: cpu);
                        // Success is indicated by returning any x such that 0 <= x < length.
                        return length - (uint)bytesCount;
                    }
                }
                // Return value: "the number of bytes not filled in the buffer".
                return length;
            }
            case Operation.SYS_ISTTY:
            {
                var handle = GetArgumentField(argumentsAddress, 0);
                LogReceived("SYS_ISTTY", $"args: {handle}");

                if(TryGetSemihostingDescriptor("SYS_ISTTY", handle, out var descriptor))
                {
                    return descriptor.IsConsole ? 1u : 0u;
                }
                else
                {
                    return unchecked((uint)-1);
                }
            }
            case Operation.SYS_SEEK:
            {
                var handle = GetArgumentField(argumentsAddress, 0);
                var offset = GetArgumentField(argumentsAddress, 1);
                LogReceived("SYS_SEEK", $"args: {handle} / {offset}");

                if(TryGetSemihostingDescriptor("SYS_SEEK", handle, out var descriptor))
                {
                    if(descriptor.TrySeek(offset, ref semihostingErrno))
                    {
                        return 0;
                    }
                }
                return unchecked((uint)-1);
            }
            case Operation.SYS_FLEN:
            {
                var handle = GetArgumentField(argumentsAddress, 0);
                LogReceived("SYS_FLEN", $"args: {handle}");

                if(TryGetSemihostingDescriptor("SYS_FLEN", handle, out var descriptor))
                {
                    if(descriptor.TryGetLength(out var length, ref semihostingErrno))
                    {
                        return length;
                    }
                }
                return unchecked((uint)-1);
            }
            case Operation.SYS_ERRNO:
            {
                LogReceived("SYS_ERRNO");
                if(argumentsAddress != 0)
                {
                    LogFailure("SYS_ERRNO", "The parameter register must be 0");
                }

                return (uint)semihostingErrno;
            }
            // SYS_READC, SYS_WRITEC and SYS_WRITE0 don't use descriptors. They always use the "debug channel".
            case Operation.SYS_READC:
            {
                LogReceived("SYS_READC");
                if(argumentsAddress != 0)
                {
                    LogFailure("SYS_READC", "The parameter register must be 0");
                    return unchecked((uint)-1);
                }

                if(!TryGetSemihostingUart("SYS_READC", out var uart))
                {
                    semihostingErrno = Errno.EINVAL;
                    return unchecked((uint)-1);
                }
                return uart.SemihostingReadByte();
            }
            case Operation.SYS_WRITEC:
            {
                var character = (byte)GetArgumentField(argumentsAddress, 0);
                LogReceived("SYS_WRITEC", $"args: {character}");

                if(!TryGetSemihostingUart("SYS_WRITEC", out var uart))
                {
                    semihostingErrno = Errno.EINVAL;
                    return unchecked((uint)-1);
                }
                uart.SemihostingWriteByte(character);

                // Return value: "None. Return register is corrupted", so we decide to set it to 0
                return 0;
            }
            case Operation.SYS_WRITE0:
            {
                var sourcePointer = cpu.TranslateAddress(argumentsAddress, MpuAccess.Read);
                LogReceived("SYS_WRITE0", $"args: 0x{sourcePointer:X}");

                if(!TryGetSemihostingUart("SYS_WRITE0", out var uart))
                {
                    semihostingErrno = Errno.EINVAL;
                    return unchecked((uint)-1);
                }

                // SYS_WRITE0 writes characters up to the null character.
                for(byte sourceByte; (sourceByte = cpu.Bus.ReadByte(sourcePointer, cpu)) != '\0'; sourcePointer++)
                {
                    uart.SemihostingWriteByte(sourceByte);
                }
                // Return value: "None. Return register is corrupted", so we decide to set it to 0
                return 0;
            }
            default:
            {
                cpu.Log(LogLevel.Warning, "Semihosting: Unhandled 0x{0:X} operation ({1})", operation, (Operation)operation);
                return unchecked((uint)-1);
            }
            }
        }

        public void LogFailure(string operationName, string reason)
        {
            cpu.Log(LogLevel.Warning, "Semihosting: {0} failed; {1}", operationName, reason);
        }

        public void Reset()
        {
            ClearDescriptors();
            semihostingErrno = Errno.NoError;
        }

        public SemihostingUart SemihostingUart
        {
            get
            {
                if(cpu.SemihostingUart == null)
                {
                    throw new SemihostingException("Cannot open Semihosting console; set SemihostingUart first.");
                }
                return cpu.SemihostingUart;
            }
        }

        public string SemihostingDirectory
        {
            get => this.fullSemihostingDirectory;
            set
            {
                cpu.Log(LogLevel.Debug, "Trying to set SemihostingDirectory to: {0}", value);
                try
                {
                    if(!String.IsNullOrEmpty(value))
                    {
                        var fullDirectoryPath = Path.GetFullPath(value);
                        if(Directory.Exists(fullDirectoryPath))
                        {
                            // Make sure that the path ends with directory separator for the check later
                            fullDirectoryPath = fullDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                            cpu.Log(LogLevel.Noisy, "Full SemihostingDirectory: {0}", fullDirectoryPath);
                            fullSemihostingDirectory = fullDirectoryPath;
                            return;
                        }
                        else
                        {
                            throw new ConstructionException($"It's not a directory.");
                        }
                    }
                    else
                    {
                        throw new ConstructionException("String is empty.");
                    }
                }
                catch(Exception e)
                {
                    throw new ConstructionException($"Incorrect SemihostingDirectory: {value}. {e.Message}");
                }
            }
        }

        public const string ConsolePath = ":tt";
        public const string FeaturesPath = ":semihosting-features";

        private uint GetArgumentField(ulong baseAddress, ulong field)
        {
            var address = baseAddress + field * 4;
            // TODO: Add address translation to semihosting handler
            return cpu.Bus.ReadDoubleWord(address, cpu);
        }

        private void LogReceived(string operationName, string message = "")
        {
            cpu.Log(LogLevel.Debug, "Semihosting: {0} received; {1}", operationName, message);
        }

        private bool TryGetSemihostingUart(string operationName, out SemihostingUart semihostingUart)
        {
            if(cpu.SemihostingUart != null)
            {
                semihostingUart = cpu.SemihostingUart;
                return true;
            }
            else
            {
                LogFailure(operationName, "Cannot open Semihosting console; set SemihostingUart first.");
                semihostingUart = null;
                return false;
            }
        }

        private bool TryGetSemihostingDescriptor(string operationName, uint handle, out SemihostingDescriptor semihostingDescriptor)
        {
            if(semihostingDescriptors.TryGetValue(handle, out semihostingDescriptor))
            {
                return true;
            }
            else
            {
                LogFailure(operationName, "Incorrect handle");
                semihostingErrno = Errno.EBADF;
                return false;
            }
        }

        private uint AddDescriptor(string operationName, string path, uint mode)
        {
            SemihostingDescriptor descriptor = null;
            try
            {
                descriptor = new SemihostingDescriptor(this, path, (ISOCMode)mode);
            }
            catch(SemihostingException e)
            {
                LogFailure(operationName, e.Message);
                semihostingErrno = Errno.EINVAL;
                return unchecked((uint)-1);
            }
            catch(Exception e)
            {
                LogFailure(operationName, $"Unhandled {e.GetType().Name}: {e.Message}");
                semihostingErrno = Errno.EINVAL;
                return unchecked((uint)-1);
            }

            var descriptorNumber = 0u;
            if(freeDescriptors.Count == 0)
            {
                highestFileDescriptor += 1;
                descriptorNumber = highestFileDescriptor;
            }
            else
            {
                descriptorNumber = freeDescriptors.Dequeue();
            }
            semihostingDescriptors.Add(descriptorNumber, descriptor);

            LogReceived(operationName, $"'{path}' opened as {descriptorNumber}");
            return descriptorNumber;
        }

        private bool RemoveDescriptor(string operationName, uint handle)
        {
            if(TryGetSemihostingDescriptor(operationName, handle, out var descriptor))
            {
                descriptor.Close();
                semihostingDescriptors.Remove(handle);
                freeDescriptors.Enqueue(handle, handle);
                return true;
            }
            else
            {
                // Errno got already set by TryGetSemihostingDescriptor
                return false;
            }
        }

        private void ClearDescriptors()
        {
            foreach(var descriptor in semihostingDescriptors)
            {
                descriptor.Value.Close();
            }
            semihostingDescriptors.Clear();
            freeDescriptors.Clear();
            highestFileDescriptor = 0;
        }

        private string fullSemihostingDirectory;
        private Errno semihostingErrno = Errno.NoError;
        private uint highestFileDescriptor = 0;
        private readonly Arm cpu;
        private readonly PriorityQueue<uint, uint> freeDescriptors = new PriorityQueue<uint, uint>();
        private readonly Dictionary<uint, SemihostingDescriptor> semihostingDescriptors = new Dictionary<uint, SemihostingDescriptor>();

        private class SemihostingDescriptor
        {
            public SemihostingDescriptor(SemihostingHandler handler, string path, ISOCMode mode)
            {
                this.handler = handler;
                if(string.IsNullOrWhiteSpace(path))
                {
                    throw new SemihostingException($"Invalid path: {path}");
                }

                if(path == SemihostingHandler.ConsolePath)
                {
                    backingStream = new UartStream(handler.SemihostingUart, mode);
                    isConsole = true;
                }
                else if(path == SemihostingHandler.FeaturesPath)
                {
                    if(mode != ISOCMode.Read && mode != ISOCMode.ReadBinary)
                    {
                        throw new SemihostingException($"Invalid mode for :semihosting-features: {mode}");
                    }

                    backingStream = new MemoryStream(
                        new byte[] {
                        // SHFB_MAGIC_0-3
                        0x53, 0x48, 0x46, 0x42,

                        // Feature byte 0:
                        //  bit 0: SH_EXT_EXIT_EXTENDED (not supported)
                        //  bit 1: SH_EXT_STDOUT_STDERR (supported)
                        0x2},
                        writable: false);
                    isConsole = false;
                }
                else
                {
                    if(IsPathValid(path, out var errorMessage))
                    {
                        try
                        {
                            var fullPath = Path.GetFullPath(Path.Combine(handler.SemihostingDirectory, path));
                            backingStream = OpenFileWithISOCMode(fullPath, mode);
                        }
                        catch(Exception e)
                        {
                            throw new SemihostingException(e.Message);
                        }
                        isConsole = false;
                    }
                    else
                    {
                        throw new SemihostingException(errorMessage);
                    }
                }
            }

            public void Close()
            {
                backingStream?.Close();
            }

            public bool TryGetLength(out uint length, ref Errno errno)
            {
                length = 0;
                // ARM docs: SYS_FLEN's arg is a "handle for a (...) seekable file object."
                if(backingStream?.CanSeek ?? false)
                {
                    if(backingStream.Length > uint.MaxValue)
                    {
                        handler.LogFailure("SYS_FLEN", $"File size {backingStream.Length} is too big for 32-bit value");
                        errno = Errno.EOVERFLOW;
                        return false;
                    }

                    length = (uint)backingStream.Length;

                    return true;
                }
                else
                {
                    handler.LogFailure("SYS_FLEN", "Descriptor isn't seekable");
                    errno = Errno.EINVAL;
                    return false;
                }
            }

            public bool TryRead(string operationName, int length, out int bytesCount, out byte[] bytesRead, ref Errno errno)
            {
                bytesRead = new byte[length];
                bytesCount = 0;
                if(backingStream?.CanRead ?? false)
                {
                    try
                    {
                        bytesCount = backingStream.Read(bytesRead, 0, length);
                        return true;
                    }
                    catch(Exception e)
                    {
                        handler.LogFailure(operationName, e.Message);
                        errno = Errno.EINVAL;
                    }
                }
                else
                {
                    handler.LogFailure(operationName, "Descriptor isn't readable");
                    errno = Errno.EBADF;
                }
                return false;
            }

            public bool TrySeek(long offset, ref Errno errno)
            {
                if(backingStream?.CanRead ?? false)
                {
                    try
                    {
                        backingStream.Seek(offset, SeekOrigin.Begin);
                        return true;
                    }
                    catch(Exception e)
                    {
                        handler.LogFailure("SYS_SEEK", e.Message);
                    }
                }
                else
                {
                    handler.LogFailure("SYS_SEEK", "Descriptor isn't seekable");
                }
                errno = Errno.EINVAL;
                return false;
            }

            public bool TryWrite(string operationName, byte[] bytes, ref Errno errno)
            {
                if(backingStream?.CanWrite ?? false)
                {
                    try
                    {
                        backingStream.Write(bytes, 0, bytes.Length);
                        return true;
                    }
                    catch(Exception e)
                    {
                        handler.LogFailure(operationName, e.Message);
                        errno = Errno.EINVAL;
                    }
                }
                else
                {
                    handler.LogFailure(operationName, "Descriptor isn't writable");
                    errno = Errno.EBADF;
                }
                return false;
            }

            public bool IsConsole => isConsole;

            private bool IsPathValid(string filePath, out string errorMessage)
            {
                errorMessage = "";
                var fullPath = "";

                if(String.IsNullOrEmpty(handler.SemihostingDirectory))
                {
                    errorMessage = $"'{filePath}' can't be opened; SemihostingDirectory isn't set";
                    return false;
                }

                fullPath = Path.GetFullPath(filePath, handler.SemihostingDirectory);
                if(fullPath.StartsWith(handler.SemihostingDirectory))
                {
                    return true;
                }

                errorMessage = $"'{fullPath}' can't be opened; it's outside the SemihostingDirectory ({handler.SemihostingDirectory})";
                return false;
            }

            private FileStream OpenFileWithISOCMode(string path, ISOCMode mode)
            {
                // The same for the binary versions of these modes.
                FileMode fileMode;      // Append: a, Create: w/w+, Open: r/r+
                FileAccess fileAccess;  // Read: r, Write: w/a, ReadWrite: r+/w+

                switch(mode)
                {
                case ISOCMode.Read:
                case ISOCMode.ReadBinary:
                    fileAccess = FileAccess.Read;
                    fileMode = FileMode.Open;
                    break;
                case ISOCMode.ReadWrite:
                case ISOCMode.ReadWriteBinary:
                    fileAccess = FileAccess.ReadWrite;
                    fileMode = FileMode.Open;
                    break;
                case ISOCMode.Write:
                case ISOCMode.WriteBinary:
                    fileAccess = FileAccess.Write;
                    fileMode = FileMode.Create;
                    break;
                case ISOCMode.WriteRead:
                case ISOCMode.WriteReadBinary:
                    fileAccess = FileAccess.ReadWrite;
                    fileMode = FileMode.Create;
                    break;
                case ISOCMode.Append:
                case ISOCMode.AppendBinary:
                    fileAccess = FileAccess.Write;
                    fileMode = FileMode.Append;
                    break;
                case ISOCMode.AppendRead:
                case ISOCMode.AppendReadBinary:
                    // There is no FileMode to allow seeking for read and writing always at the end.
                    throw new SemihostingException("Modes 'a+' and 'a+b' aren't supported");
                default:
                    throw new SemihostingException($"Invalid ISO C mode: {mode}");
                }
                return File.Open(path, fileMode, fileAccess);
            }

            private readonly bool isConsole;
            private readonly Stream backingStream;
            private readonly SemihostingHandler handler;
        }

        private class UartStream : Stream
        {
            public UartStream(SemihostingUart uart, ISOCMode mode)
            {
                this.uart = uart;
                switch(mode)
                {
                case ISOCMode.Read:
                case ISOCMode.ReadBinary:
                case ISOCMode.ReadWrite:
                case ISOCMode.ReadWriteBinary:
                    canRead = true;
                    break;
                case ISOCMode.Write:
                case ISOCMode.WriteBinary:
                case ISOCMode.WriteRead:
                case ISOCMode.WriteReadBinary:
                case ISOCMode.Append:
                case ISOCMode.AppendBinary:
                case ISOCMode.AppendRead:
                case ISOCMode.AppendReadBinary:
                    canWrite = true;
                    break;
                default:
                    throw new SemihostingException($"Invalid mode for Semihosting console: {mode}");
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if(!CanRead)
                {
                    throw new NotSupportedException();
                }
                return uart.SemihostingReadBytes(buffer, offset, count);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if(!CanWrite)
                {
                    throw new NotSupportedException();
                }
                uart.SemihostingWriteBytes(buffer, offset, count);
            }

            public override int ReadByte()
            {
                if(!CanRead)
                {
                    throw new NotSupportedException();
                }
                return uart.SemihostingReadByte();
            }

            public override void WriteByte(byte value)
            {
                if(!CanWrite)
                {
                    throw new NotSupportedException();
                }
                uart.SemihostingWriteByte(value);
            }

            public override void Flush()
            {
                // Does nothing as characters are always immediately flushed
                return;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override bool CanRead => canRead;

            public override bool CanSeek => false;

            public override bool CanWrite => canWrite;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            private readonly SemihostingUart uart;
            private readonly bool canRead;
            private readonly bool canWrite;
        }

        private class SemihostingException : Exception
        {
            public SemihostingException(string errorMessage) : base(errorMessage) { }
        }

        private enum Operation
        {
            SYS_OPEN = 0x01,
            SYS_CLOSE = 0x02,
            SYS_WRITEC = 0x03,
            SYS_WRITE0 = 0x04,
            SYS_WRITE = 0x05,
            SYS_READ = 0x06,
            SYS_READC = 0x07,
            SYS_ISERROR = 0x08,
            SYS_ISTTY = 0x09,
            SYS_SEEK = 0x0A,
            SYS_FLEN = 0x0C,
            SYS_TMPNAM = 0x0D,
            SYS_REMOVE = 0x0E,
            SYS_RENAME = 0x0F,
            SYS_CLOCK = 0x10,
            SYS_TIME = 0x11,
            SYS_SYSTEM = 0x12,
            SYS_ERRNO = 0x13,
            SYS_GET_CMDLINE = 0x15,
            SYS_HEAPINFO = 0x16,
            angel_SWIreason_EnterSVC = 0x17,
            angel_SWIreason_ReportException = 0x18,
            SYS_ELAPSED = 0x30,
            SYS_TICKFREQ = 0x31,
        }

        // TODO: Add more Errno and use proper one for each error
        private enum Errno : uint
        {
            NoError = 0,
            EBADF = 9, // Bad file number
            EINVAL = 22, // Invalid Argument
            EOVERFLOW = 75
        }

        /*
         * Semihosting modes are based on ISO C fopen modes:
         *
         * mode                 0   1   2   3   4   5   6   7   8   9   10  11
         * ISO C fopen mode     r   rb  r+  r+b w   wb  w+  w+b a   ab  a+  a+b
         */
        private enum ISOCMode
        {
            Read,
            ReadBinary,
            ReadWrite,
            ReadWriteBinary,
            Write,
            WriteBinary,
            WriteRead,
            WriteReadBinary,
            Append,
            AppendBinary,
            AppendRead,
            AppendReadBinary,
        }
    }
}
