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

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class SemihostingHandler : IDisposable, IPeripheral, IRegisterablePeripheral<SemihostingUart, NumberRegistrationPoint<SemihostingHandler.Stdio>>, IRegisterablePeripheral<SemihostingUart, NullRegistrationPoint>
    {
        public SemihostingHandler(IMachine machine)
        {
            this.machine = machine;
        }

        public void AttachCpu(Arm cpu)
        {
            this.cpu = cpu;
        }

        public void Dispose()
        {
            ClearDescriptors();
            RemoveTemporaryFilesDirectory();
        }

        public void Register(SemihostingUart peripheral, NullRegistrationPoint registrationPoint)
        {
            /* If registration point was not given assume given uart peripheral will process all console streams */
            Register(peripheral, new NumberRegistrationPoint<Stdio>(Stdio.InputOutputError));
        }

        public void Register(SemihostingUart peripheral, NumberRegistrationPoint<Stdio> registrationPoint)
        {
            if(registrationPoint.Address.HasFlag(Stdio.InputOutput))
            {
                if(stdInOut != null)
                {
                    throw new RegistrationException("Uart for semihosting input/output stream is already registered.");
                }
                stdInOut = peripheral;
            }

            if(registrationPoint.Address.HasFlag(Stdio.Error))
            {
                if(stdErr != null)
                {
                    throw new RegistrationException("Uart for semihosting error stream is already registered.");
                }
                stdErr = peripheral;
            }

            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(SemihostingUart peripheral)
        {
            if(peripheral == stdInOut)
            {
                stdInOut = null;
            }

            if(peripheral == stdErr)
            {
                stdErr = null;
            }
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public uint DoSemihosting(uint operationNumber, uint argumentsAddress)
        {
            var operation = (Operation)operationNumber;
            switch(operation)
            {
            case Operation.SYS_OPEN:
            {
                var pathPointer = GetArgumentField(argumentsAddress, 0);
                var mode = GetArgumentField(argumentsAddress, 1);
                var pathLength = (int)GetArgumentField(argumentsAddress, 2);
                LogReceived("SYS_OPEN", "args: 0x{0:X} / {1} / {2}", pathPointer, mode, pathLength);

                if(pathLength <= 0)
                {
                    this.Log(LogLevel.Warning, "SYS_OPEN: Invalid path length: {0}; it has to be a positive number.", pathLength);
                    return unchecked((uint)-1);
                }

                // Check presence of the required null character
                var byteAfterPath = cpu.Bus.ReadByte((ulong)(pathPointer + pathLength), cpu);
                if(byteAfterPath != 0)
                {
                    this.Log(LogLevel.Warning, "SYS_OPEN: No null character after the path. Found: '{0}'", (char)byteAfterPath);
                    return unchecked((uint)-1);
                }

                var pathBytes = cpu.Bus.ReadBytes(pathPointer, pathLength, cpu);
                var path = Encoding.UTF8.GetString(pathBytes, 0, pathLength);
                return AddDescriptor("SYS_OPEN", path, mode);
            }
            case Operation.SYS_CLOSE:
            {
                var handle = GetArgumentField(argumentsAddress, 0);
                LogReceived("SYS_CLOSE", "args: {0}", handle);
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
                LogReceived("SYS_WRITE", "args: {0} / 0x{1:X} / {2}", handle, sourcePointer, length);

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
                LogReceived("SYS_READ", "args: {0} / 0x{1:X} / {2}", handle, destinationPointer, length);

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
                LogReceived("SYS_ISTTY", "args: {0}", handle);

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
                LogReceived("SYS_SEEK", "args: {0} / {1}", handle, offset);

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
                LogReceived("SYS_FLEN", "args: {0}", handle);

                if(TryGetSemihostingDescriptor("SYS_FLEN", handle, out var descriptor))
                {
                    if(descriptor.TryGetLength(out var length, ref semihostingErrno))
                    {
                        return length;
                    }
                }
                return unchecked((uint)-1);
            }
            case Operation.SYS_TMPNAM:
            {
                var bufferPointer = GetArgumentField(argumentsAddress, 0);
                var tempId = (int)GetArgumentField(argumentsAddress, 1);
                var bufferLength = GetArgumentField(argumentsAddress, 2);

                LogReceived("SYS_TMPNAM", "args: 0x{0:X} / {1} / {2}", bufferPointer, tempId, bufferLength);

                if(tempId < 0 || MaxNumberOfTemporaryFiles <= tempId)
                {
                    this.Log(LogLevel.Warning, "SYS_TMPNAM: File id: {0} out of (0,{1}) range", tempId, MaxNumberOfTemporaryFiles);
                    semihostingErrno = Errno.EINVAL;
                    return unchecked((uint)-1);
                }

                if(TemporaryFilesDirectory == null)
                {
                    if(!GenerateTemporaryFilesDirectory())
                    {
                        semihostingErrno = Errno.EINVAL;
                        return unchecked((uint)-1);
                    }
                }

                if(!TryAccessPath(TemporaryFilesDirectory, out var fullDirectoryPath, out var errorMessage))
                {
                    this.Log(LogLevel.Warning, "SYS_TMPNAM: Can't access temporary files directory: {0}", errorMessage);
                    semihostingErrno = Errno.EACCES;
                    return unchecked((uint)-1);
                }

                if(!Directory.Exists(fullDirectoryPath))
                {
                    try
                    {
                        Directory.CreateDirectory(fullDirectoryPath);
                        this.Log(LogLevel.Info, "SYS_TMPNAM: Created temporary files directory at: {0}", fullDirectoryPath);
                    }
                    catch(Exception e)
                    {
                        this.Log(LogLevel.Warning, "SYS_TMPNAM: Can't create temporary files directory {0}", e.Message);
                        semihostingErrno = Errno.EINVAL;
                        return unchecked((uint)-1);
                    }
                }

                var filePath = Path.Join(TemporaryFilesDirectory, tempId.ToString());
                var filenameBytes = Encoding.UTF8.GetBytes(filePath);
                if(filenameBytes.Length > bufferLength)
                {
                    this.Log(LogLevel.Warning, "SYS_TMPNAM: Path length is too long.");
                    semihostingErrno = Errno.EOVERFLOW;
                    return unchecked((uint)-1);
                }
                cpu.Bus.WriteBytes(filenameBytes, bufferPointer, filenameBytes.Length, context: cpu);

                return 0;
            }
            case Operation.SYS_REMOVE:
            {
                var pathPointer = GetArgumentField(argumentsAddress, 0);
                var pathLength = (int)GetArgumentField(argumentsAddress, 1);
                LogReceived("SYS_REMOVE", "args: 0x{0:X} / {1}", pathPointer, pathLength);

                if(pathLength <= 0)
                {
                    this.Log(LogLevel.Warning, "SYS_REMOVE: Invalid path length: {0}; it has to be a positive number.", pathLength);
                    return unchecked((uint)-1);
                }

                // Check presence of the required null character
                var byteAfterPath = cpu.Bus.ReadByte((ulong)(pathPointer + pathLength), cpu);
                if(byteAfterPath != 0)
                {
                    this.Log(LogLevel.Warning, "SYS_REMOVE: No null character after the path. Found: '{0}'", (char)byteAfterPath);
                    return unchecked((uint)-1);
                }

                var pathBytes = cpu.Bus.ReadBytes(pathPointer, pathLength, cpu);
                var path = Encoding.UTF8.GetString(pathBytes, 0, pathLength);

                if(!TryAccessPath(path, out var fullPath, out var errorMessage))
                {
                    this.Log(LogLevel.Warning, "SYS_REMOVE: {0}", errorMessage);
                    semihostingErrno = Errno.EACCES;
                    return unchecked((uint)-1);
                }

                if(!File.Exists(fullPath))
                {
                    semihostingErrno = Errno.ENOENT;
                    return unchecked((uint)-1);
                }

                try
                {
                    File.Delete(fullPath);
                    return 0;
                }
                catch(PathTooLongException)
                {
                    semihostingErrno = Errno.ENAMETOOLONG;
                }
                catch(IOException)
                {
                    semihostingErrno = Errno.EIO;
                }
                catch(UnauthorizedAccessException)
                {
                    semihostingErrno = Errno.EACCES;
                }
                catch(Exception e)
                {
                    this.Log(LogLevel.Warning, "Unhandled {0}: {1}", e.GetType().Name, e.Message);
                    semihostingErrno = Errno.EINVAL;
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
            case Operation.SYS_GET_CMDLINE:
            {
                var bufferPointer = GetArgumentField(argumentsAddress, 0);
                var bufferLength = GetArgumentField(argumentsAddress, 1);

                LogReceived("SYS_GET_CMDLINE", "args: 0x{0:X} / {1}", bufferPointer, bufferLength);

                if(ProgramArguments == null)
                {
                    cpu.Bus.WriteDoubleWord(argumentsAddress + 4, 0, context: cpu); /* write 0 to signify there are no arguments */
                    return 0;
                }

                var argumentBytes = Encoding.UTF8.GetBytes(ProgramArguments + '\0');
                if(argumentBytes.Length > bufferLength)
                {
                    semihostingErrno = Errno.EOVERFLOW;
                    return unchecked((uint)-1);
                }

                cpu.Bus.WriteBytes(argumentBytes, bufferPointer, argumentBytes.Length, context: cpu);
                cpu.Bus.WriteDoubleWord(argumentsAddress + 4, (uint)argumentBytes.Length, context: cpu); /* write back the length of arguments string */

                return 0;
            }
            case Operation.SYS_EXIT:
            {
                exitReason = (int)cpu.TranslateAddress(argumentsAddress, MpuAccess.Read);

                LogReceived("SYS_EXIT", "args: 0x{0:X}", exitReason);

                this.Log(LogLevel.Info, "Program exited with reason {0}(0x{1:X})", (ExitReasonCode)exitReason, exitReason);

                if(HaltOnExit)
                {
                    cpu.IsHalted = true;
                }
                // Program doesn't expect return from this call, but in case we decide to continue the execution, registers should be left as they were.
                return (uint)operation;
            }
            case Operation.SYS_EXIT_EXTENDED:
            {
                exitReason = (int)GetArgumentField(argumentsAddress, 0);
                exitCode = (int)GetArgumentField(argumentsAddress, 1);

                LogReceived("SYS_EXIT_EXTENDED", "args: 0x{0:X} / {1}", exitReason, exitCode);

                this.Log(LogLevel.Info, "Program exited with reason {0}(0x{1:X}) and return code {2}", (ExitReasonCode)exitReason, exitReason, exitCode);

                if(HaltOnExit)
                {
                    cpu.IsHalted = true;
                }
                // Program doesn't expect return from this call, but in case we decide to continue the execution, registers should be left as they were.
                return (uint)operation;
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

                if(!TryGetConsole("SYS_READC", out var console))
                {
                    semihostingErrno = Errno.EINVAL;
                    return unchecked((uint)-1);
                }
                return console.SemihostingReadByte();
            }
            case Operation.SYS_WRITEC:
            {
                var character = (byte)GetArgumentField(argumentsAddress, 0);
                LogReceived("SYS_WRITEC", "args: {0}", character);

                if(!TryGetConsole("SYS_WRITEC", out var console))
                {
                    semihostingErrno = Errno.EINVAL;
                    return unchecked((uint)-1);
                }
                console.SemihostingWriteByte(character);

                // Return value: "None. Return register is corrupted", so we decide to set it to 0
                return 0;
            }
            case Operation.SYS_WRITE0:
            {
                var sourcePointer = cpu.TranslateAddress(argumentsAddress, MpuAccess.Read);
                LogReceived("SYS_WRITE0", "args: 0x{0:X}", sourcePointer);

                if(!TryGetConsole("SYS_WRITE0", out var console))
                {
                    semihostingErrno = Errno.EINVAL;
                    return unchecked((uint)-1);
                }

                // SYS_WRITE0 writes characters up to the null character.
                for(byte sourceByte; (sourceByte = cpu.Bus.ReadByte(sourcePointer, cpu)) != '\0'; sourcePointer++)
                {
                    console.SemihostingWriteByte(sourceByte);
                }
                // Return value: "None. Return register is corrupted", so we decide to set it to 0
                return 0;
            }
            case Operation.angel_SWIreason_EnterSVC:
            case Operation.angelSWI_Reason_SyncCacheRange:
            {
                this.Log(LogLevel.Warning, "{0} is a reserved operation, usage of which got deprecated by ARM", operation);
                return unchecked((uint)-1);
            }
            default:
            {
                this.Log(LogLevel.Warning, "Unhandled 0x{0:X} operation ({1})", (uint)operation, operation);
                return unchecked((uint)-1);
            }
            }
        }

        public void LogFailure(string operationName, string reason)
        {
            this.Log(LogLevel.Warning, "{0} failed; {1}", operationName, reason);
        }

        public void Reset()
        {
            Dispose();
            semihostingErrno = Errno.NoError;
            exitReason = null;
            exitCode = null;
        }

        public bool Exited
        {
            get
            {
                return exitReason != null;
            }
        }

        public bool ExitedWithCode
        {
            get
            {
                return exitCode != null;
            }
        }

        public int? ExitReason
        {
            get
            {
                if(exitReason == null)
                {
                    this.Log(LogLevel.Warning, "Program didn't exit yet");
                }
                return exitReason;
            }
        }

        public int? ExitCode
        {
            get
            {
                if(exitCode == null)
                {
                    if(ExitReason != null)
                    {
                        this.Log(LogLevel.Warning, "Program exited but didn't specify an exit code");
                    }
                }
                return exitCode;
            }
        }

        public string SemihostingDirectory
        {
            get => this.fullSemihostingDirectory;
            set
            {
                this.Log(LogLevel.Debug, "Trying to set SemihostingDirectory to: {0}", value);
                try
                {
                    if(String.IsNullOrEmpty(value))
                    {
                        throw new ConstructionException("String is empty.");
                    }
                    var fullDirectoryPath = Path.GetFullPath(value);
                    if(!Directory.Exists(fullDirectoryPath))
                    {
                        throw new ConstructionException("It's not a directory.");
                    }
                    // Make sure that the path ends with directory separator for the check later
                    fullDirectoryPath = fullDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    this.Log(LogLevel.Noisy, "Full SemihostingDirectory: {0}", fullDirectoryPath);
                    fullSemihostingDirectory = fullDirectoryPath;
                }
                catch(Exception e)
                {
                    throw new ConstructionException($"Incorrect SemihostingDirectory: {value}. {e.Message}");
                }
            }
        }

        public bool HaltOnExit { get; set; } = true;

        public string ProgramArguments { get; set; }

        public string TemporaryFilesDirectory { get; set; }

        public const uint MaxNumberOfTemporaryFiles = 256;
        public const string ConsolePath = ":tt";
        public const string FeaturesPath = ":semihosting-features";

        private uint GetArgumentField(ulong baseAddress, ulong field)
        {
            var address = baseAddress + field * 4;
            // TODO: Add address translation to semihosting handler
            return cpu.Bus.ReadDoubleWord(address, cpu);
        }

        private void LogReceived(string operationName, string message = null, params object[] args)
        {
            this.Log(LogLevel.Debug, $"{operationName} received; " + message, args);
        }

        private bool GenerateTemporaryFilesDirectory()
        {
            string directoryPath;
            string fullDirectoryPath;
            do
            {
                directoryPath = $"temp-{Guid.NewGuid()}";
                if(!TryAccessPath(directoryPath, out fullDirectoryPath, out var errorMessage))
                {
                    this.Log(LogLevel.Error, "Temporary files directory generation failed: {0}", errorMessage);
                    return false;
                }
            }
            while(File.Exists(fullDirectoryPath));
            this.Log(LogLevel.Debug, "Generated temporary files directory: {0}", directoryPath);
            TemporaryFilesDirectory = directoryPath; /* We're setting TemporaryFilesDirectory to path relative to WorkingDirectory */
            return true;
        }

        private bool TryGetConsole(string operationName, out SemihostingUart console)
        {
            if(stdInOut != null)
            {
                console = stdInOut;
                return true;
            }
            else
            {
                LogFailure(operationName, "Cannot open Semihosting console; set SemihostingUart for input/output first.");
                console = null;
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

        private bool TryAccessPath(string filePath, out string fullPath, out string errorMessage)
        {
            errorMessage = "";
            fullPath = "";

            if(String.IsNullOrEmpty(SemihostingDirectory))
            {
                errorMessage = $"'{filePath}' can't be accessed; SemihostingDirectory isn't set";
                return false;
            }

            fullPath = Path.GetFullPath(filePath, SemihostingDirectory);
            if(fullPath.StartsWith(SemihostingDirectory))
            {
                return true;
            }

            errorMessage = $"'{fullPath}' can't be accessed; it's outside the SemihostingDirectory ({SemihostingDirectory})";
            return false;
        }

        private void RemoveTemporaryFilesDirectory()
        {
            if(TemporaryFilesDirectory == null)
            {
                return;
            }

            try
            {
                if(!TryAccessPath(TemporaryFilesDirectory, out var fullPath, out var errorMessage))
                {
                    throw new SemihostingException(errorMessage);
                }

                if(Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath);
                }
            }
            catch(Exception e)
            {
                this.Log(LogLevel.Error, "Temporary files directory disposal failed: {0}", e.Message);
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

            this.Log(LogLevel.Debug, "{0}: '{1}' opened as {2}", operationName, path, descriptorNumber);
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

        private SemihostingUart StdInOut
        {
            get
            {
                if(stdInOut == null)
                {
                    throw new SemihostingException("Cannot open Semihosting console; set standard input/output stream first.");
                }
                return stdInOut;
            }
        }

        private SemihostingUart StdErr
        {
            get
            {
                if(stdErr == null)
                {
                    throw new SemihostingException("Cannot open Semihosting error console; set standard error stream first.");
                }
                return stdErr;
            }
        }

        private Arm cpu;
        private Errno semihostingErrno = Errno.NoError;
        private SemihostingUart stdInOut;
        private SemihostingUart stdErr;
        private string fullSemihostingDirectory;
        private uint highestFileDescriptor = 0;
        private int? exitReason = null;
        private int? exitCode = null;
        private readonly IMachine machine;
        private readonly PriorityQueue<uint, uint> freeDescriptors = new PriorityQueue<uint, uint>();
        private readonly Dictionary<uint, SemihostingDescriptor> semihostingDescriptors = new Dictionary<uint, SemihostingDescriptor>();

        [Flags]
        public enum Stdio
        {
            None = 0,
            InputOutput = 1,
            Error = 1 << 1,
            InputOutputError = InputOutput | Error,
        }

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
                    backingStream = OpenUartWithISOCMode(mode);
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
                        //  bit 0: SH_EXT_EXIT_EXTENDED (supported)
                        //  bit 1: SH_EXT_STDOUT_STDERR (supported)
                        0x3},
                        writable: false);
                    isConsole = false;
                }
                else
                {
                    if(handler.TryAccessPath(path, out var fullPath, out var errorMessage))
                    {
                        try
                        {
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

            private UartStream OpenUartWithISOCMode(ISOCMode mode)
            {
                SemihostingUart uart;
                var canRead = false;
                var canWrite = false;
                switch(mode)
                {
                case ISOCMode.Read:
                case ISOCMode.ReadBinary:
                    canRead = true;
                    uart = handler.StdInOut;
                    break;
                case ISOCMode.ReadWrite:
                case ISOCMode.ReadWriteBinary:
                    uart = handler.StdInOut;
                    canRead = true;
                    canWrite = true;
                    break;
                case ISOCMode.Write:
                case ISOCMode.WriteBinary:
                    uart = handler.StdInOut;
                    canWrite = true;
                    break;
                case ISOCMode.WriteRead:
                case ISOCMode.WriteReadBinary:
                    uart = handler.StdInOut;
                    canRead = true;
                    canWrite = true;
                    break;
                case ISOCMode.Append:
                case ISOCMode.AppendBinary:
                case ISOCMode.AppendRead:
                case ISOCMode.AppendReadBinary:
                    uart = handler.StdErr;
                    canWrite = true;
                    break;
                default:
                    throw new SemihostingException($"Invalid mode for Semihosting console: {mode}");
                }
                return new UartStream(uart, canRead, canWrite);
            }

            private readonly bool isConsole;
            private readonly Stream backingStream;
            private readonly SemihostingHandler handler;
        }

        private class UartStream : Stream
        {
            public UartStream(SemihostingUart uart, bool canRead, bool canWrite)
            {
                this.uart = uart;
                this.canRead = canRead;
                this.canWrite = canWrite;
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
            SYS_EXIT = 0x18, // angel_SWIreason_ReportException
            angelSWI_Reason_SyncCacheRange = 0x19,
            SYS_EXIT_EXTENDED = 0x20,
            SYS_ELAPSED = 0x30,
            SYS_TICKFREQ = 0x31,
        }

        // TODO: Add more Errno and use proper one for each error
        private enum Errno : uint
        {
            NoError = 0,
            ENOENT = 2, // No such file or directory
            EIO = 5,  // I/O error
            EBADF = 9, // Bad file number
            EACCES = 13, // Permission denied
            EINVAL = 22, // Invalid Argument
            EOVERFLOW = 75, // Value too large for defined data type
            ENAMETOOLONG = 91 // File or path name too long
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

        private enum ExitReasonCode
        {
            ADP_Stopped_BranchThroughZero = 0x20000,
            ADP_Stopped_UndefinedInstr = 0x20001,
            ADP_Stopped_SoftwareInterrupt = 0x20002,
            ADP_Stopped_PrefetchAbort = 0x20003,
            ADP_Stopped_DataAbort = 0x20004,
            ADP_Stopped_AddressException = 0x20005,
            ADP_Stopped_IRQ = 0x20006,
            ADP_Stopped_FIQ = 0x20007,
            ADP_Stopped_BreakPoint = 0x20020,
            ADP_Stopped_WatchPoint = 0x20021,
            ADP_Stopped_StepComplete = 0x20022,
            ADP_Stopped_RunTimeErrorUnknown = 0x20023,
            ADP_Stopped_InternalError = 0x20024,
            ADP_Stopped_UserInterruption = 0x20025,
            ADP_Stopped_ApplicationExit = 0x20026,
            ADP_Stopped_StackOverflow = 0x20027,
            ADP_Stopped_DivisionByZero = 0x20028,
            ADP_Stopped_OSSpecific = 0x20029,
        }
    }
}
