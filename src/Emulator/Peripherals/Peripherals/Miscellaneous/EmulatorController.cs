//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
#if !PLATFORM_WINDOWS
using Mono.Unix;
using Mono.Unix.Native;
#endif
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [Icon("controller")]
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class EmulatorController : IDoubleWordPeripheral, IKnownSize
    {
        public EmulatorController(IMachine machine)
        {
            this.machine = machine;
            stopwatch = new Stopwatch();
            stringRegister = new byte[StringRegisterSize];
            fileRegister = new byte[FileRegisterSize];
            dictionary = new ConcurrentDictionary<string, string>();
        }

        public uint ReadDoubleWord(long offset)
        {
            if((Register)offset == Register.Activate)
            {
                return Magic + Version;
            }
            if(activated)
            {
                return HandleRead(offset);
            }
            this.Log(LogLevel.Warning, "Trying to read without prior activation, offset {0}.", offset);
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if((Register)offset == Register.Activate)
            {
                TryActivate(value);
                return;
            }
            if(activated)
            {
                HandleWrite(offset, value);
            }
            else
            {
                this.Log(LogLevel.Warning, "Trying to write without prior activation, value {0} @ 0x{1:X}.", value, offset);
            }
        }

        public void Reset()
        {
            activated = false;
            state = State.Usual;
        }

        public long Size
        {
            get
            {
                return FileRegisterEnd;
            }
        }

        public string this[string key]
        {
            get
            {
                string value;
                if(!dictionary.TryGetValue(key, out value))
                {
                    throw new KeyNotFoundException();
                }
                return value;
            }
            set
            {
                dictionary.AddOrUpdate(key, x => value, (x, y) => value);
            }
        }

        private void TryActivate(uint value)
        {
            if(value == Magic + Version)
            {
                activated = true;
                this.Log(LogLevel.Info, "Activated.");
            }
            else
            {
                this.Log(LogLevel.Warning,
                    "Write at activate register, but incorrent MAGIC value 0x{0:X}, should be 0x{1:X}.",
                    value, Magic + Version);
            }
        }

        private uint HandleRead(long offset)
        {
            switch((Register)offset)
            {
#if !PLATFORM_WINDOWS
            case Register.ReceiveFileFromEmulator:
                return HandleReceiveFile();
#endif
            case Register.SendFileToEmulator:
                return HandleSendFile();
            case Register.SendReceiveController:
                return HandleReadPacket();
            case Register.GetOrSet:
                return HandleGet();
            case Register.List:
                return HandleList();
            default:
                if(offset >= StringRegisterStart && offset < StringRegisterEnd)
                {
                    return HandleArrayRead(offset - StringRegisterStart, stringRegister);
                }
                if(offset >= FileRegisterStart && offset < FileRegisterEnd)
                {
                    return HandleArrayRead(offset - FileRegisterStart, fileRegister);
                }
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        private void HandleWrite(long offset, uint value)
        {
            switch((Register)offset)
            {
            case Register.Save:
                HandleSave(value);
                break;
            case Register.Load:
                HandleLoad(value);
                break;
            case Register.SendReceiveController:
                HandleWritePacket(value);
                break;
            case Register.Date:
                HandleDate();
                break;
            case Register.MeasureTime:
                HandleTimeMeasure((TimeMeasurementOperation)value);
                break;
            case Register.GetOrSet:
                HandleSet();
                break;
            default:
                if(offset >= StringRegisterStart && offset < StringRegisterEnd)
                {                    
                    HandleArrayWrite(offset - StringRegisterStart, value, stringRegister);
                    return;
                }
                if(offset >= FileRegisterStart && offset < FileRegisterEnd)
                {
                    HandleArrayWrite(offset - FileRegisterStart, value, fileRegister);
                    return;
                }
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        private uint HandleList()
        {
            if(!keyListPosition.HasValue)
            {
                keyListPosition = 0;
                keys = dictionary.Keys.ToArray();
            }
            if(keyListPosition.Value >= keys.Length)
            {
                keyListPosition = null;
                keys = null;
                return 0;
            }
            SetCurrentStringRegister(keys[keyListPosition.Value]);
            keyListPosition++;
            return 1;
        }

        private uint HandleGet()
        {
            var key = GetCurrentStringRegister();
            string value;
            if(!dictionary.TryGetValue(key, out value))
            {
                return 0;
            }
            SetCurrentStringRegister(value);
            return 1;
        }

        private void HandleSet()
        {
            switch(state)
            {
            case State.Usual:
                currentKeyToSet = GetCurrentStringRegister();
                state = State.SetValue;
                break;
            case State.SetValue:
                this[currentKeyToSet] = GetCurrentStringRegister();
                state = State.Usual;
                break;
            default:
                throw new InvalidOperationException("Improper state while setting a value.");
            }
        }

        private void HandleDate()
        {
            SetCurrentStringRegister(string.Format("{0:yyyy.MM.dd-HH:mm:ss}", machine.RealTimeClockDateTime));
        }

        private void HandleTimeMeasure(TimeMeasurementOperation operation)
        {
            switch(operation)
            {
            case TimeMeasurementOperation.Start:
                stopwatch.Start();
                this.Log(LogLevel.Info, "Time measurement started.");
                break;
            case TimeMeasurementOperation.Stop:
                stopwatch.Stop();
                this.Log(LogLevel.Info, "Time measurement finished. Elapsed {0}s = {1}", Misc.NormalizeDecimal(stopwatch.Elapsed.TotalSeconds), stopwatch.Elapsed);
                break;
            case TimeMeasurementOperation.Reset:
                if(stopwatch.IsRunning)
                {
                    stopwatch.Restart();
                }
                else
                {
                    stopwatch.Reset();
                }
                this.Log(LogLevel.Info, "Time measurement reseted.");
                break;
            default:
                this.Log(LogLevel.Warning, "Invalid value written to time measurement register, ignoring.");
                break;
            }
        }
#if !PLATFORM_WINDOWS
        private uint HandleReceiveFile()
        {
            var transferFileName = GetCurrentStringRegister();
            try
            {
                OpenStreamForReading(transferFileName);
                state = State.FileReceive;
                this.Log(LogLevel.Info, "Sending file {0} to emulation started.", transferStream.Name);
                var info = new UnixFileInfo(transferStream.Name).FileAccessPermissions;
                return (uint)info;
            }
            catch(IOException e)
            {
                HandleException(e);
                return 0;
            }
        }
#endif

        private uint HandleSendFile()
        {
            var transferFileName = GetCurrentStringRegister();
            try
            {
                OpenStreamForWriting(transferFileName);
                state = State.FileSend;
                this.Log(LogLevel.Info, "Receiving file {0} from emulation started.", transferStream.Name);
                return 0;
            }
            catch(IOException e)
            {
                HandleException(e);
                return 1;
            }
        }

        private uint HandleReadPacket()
        {
            try
            {
                if(state != State.FileReceive)
                {
                    this.Log(LogLevel.Error, "HandleReadPacket called in an improper state.", state);
                    return 0;
                }
                var retValue = (uint)transferStream.Read(fileRegister, 0, (int)(FileRegisterEnd - FileRegisterStart));
                if(retValue == 0)
                {
                    state = State.Usual;
                    this.Log(LogLevel.Info,
                        "Sending file {0} to emulation ended, {1}B transmitted.",
                        transferStream.Name, Misc.NormalizeBinary(transferStream.Position));
                    transferStream.Close();
                }
                this.NoisyLog("Prepared packet of data to read by guest of size {0}B.",
                    Misc.NormalizeBinary(retValue));
                return retValue;
             
            }
            catch(IOException e)
            {
                HandleException(e);
                return 0;
            }
        }

        private void HandleWritePacket(uint value)
        {
            try
            {
                if(state == State.ReceivePermisions)
                {
#if !PLATFORM_WINDOWS
                    Syscall.chmod(transferStream.Name, (FilePermissions)value);
#else
                    this.Log(LogLevel.Warning, "Setting file permissions in not supported in Windows.");
#endif
                    state = State.Usual;
                    return;
                }
                if(state != State.FileSend)
                {
                    this.Log(LogLevel.Error, "HandleWritePacket called in improper state.",
                        state);
                    return;
                }
                if(value == 0)
                {
                    state = State.ReceivePermisions;
                    this.Log(LogLevel.Info,
                        "Receiving file {0} from emulation ended, {1}B transmitted.",
                        transferStream.Name, Misc.NormalizeBinary(transferStream.Position));
                    transferStream.Close();
                    return;
                }
                this.NoisyLog("Received packet of data to write of size {0}B.", Misc.NormalizeBinary(value));
                transferStream.Write(fileRegister, 0, (int)value);
            }
            catch(IOException e)
            {
                HandleException(e);
            }
        }

        private string GetCurrentStringRegister()
        {
            var count = stringRegister.IndexOf(x => x == 0);
            if(count == -1)
            {
                count = stringRegister.Length;
            }
            return Encoding.ASCII.GetString(stringRegister, 0, count);
        }

        private void SetCurrentStringRegister(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            if(bytes.Length - 1 > StringRegisterEnd - StringRegisterStart)
            {
                throw new ArgumentException(string.Format("String size cannot exceed {0} bytes.", StringRegisterEnd - StringRegisterStart - 1));
            }
            bytes.CopyTo(stringRegister, 0);
            stringRegister[bytes.Length] = 0;
        }

        private void HandleException(IOException e)
        {
            this.Log(LogLevel.Error, "IOException: {0}.", e.Message);
        }

        private void OpenStreamForReading(string transferFileName)
        {
            transferStream = new FileStream(transferFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private void OpenStreamForWriting(string transferFileName)
        {
            transferStream = new FileStream(transferFileName, FileMode.Create, FileAccess.ReadWrite);
        }

        private static void HandleArrayWrite(long offset, uint value, byte[] array)
        {
            var index = (int)(offset);
            var bytes = BitConverter.GetBytes(value);
            for(var i = 0; i < 4; i++)
            {
                array[index + i] = bytes[i];
            }
        }

        private static uint HandleArrayRead(long offset, byte[] array)
        {
            var index = (int)(offset);
            var bytes = new byte[4];
            for(var i = 0; i < 4; i++)
            {
                bytes[i] = array[index + i];
            }
            return BitConverter.ToUInt32(bytes, 0);
        }

        private static void HandleLoad(uint value)
        {
            ThreadPool.QueueUserWorkItem(delegate { EmulationManager.Instance.Load(string.Format(SavepointName, value)); });
        }

        private static void HandleSave(uint value)
        {
            ThreadPool.QueueUserWorkItem(delegate { EmulationManager.Instance.Save(string.Format(SavepointName, value)); });
        }

        [PostDeserialization]
        private void AfterDeserialization()
        {
            if(state == State.FileSend || state == State.FileReceive)
            {
                // we don't know whether the file is still available etc.
                // therefore we go back to the usual state
                state = State.Usual;
            }
        }

        private bool activated;
        private State state;

        [Transient]
        private FileStream transferStream;
        private string[] keys;
        private string currentKeyToSet;
        private int? keyListPosition;

        private readonly IMachine machine;
        private readonly Stopwatch stopwatch;
        private readonly byte[] stringRegister;
        private readonly byte[] fileRegister;
        private readonly ConcurrentDictionary<string, string> dictionary;

        private const uint Magic = 0xDEADBEEF;
        private const uint Version = 3;
        private const string SavepointName = "ckpt{0}.dat";

        private enum State
        {
            Usual,
            FileReceive,
            FileSend,
            ReceivePermisions,
            SetValue
        }

        private enum Register : uint
        {
            Activate = 0x00,
            Save = 0x04,
            Load = 0x08,
            ReceiveFileFromEmulator = 0x14,
            SendFileToEmulator = 0x18,
            SendReceiveController = 0x1C,
            GetOrSet = 0x20,
            List = 0x24,
            Date = 0x28,
            MeasureTime = 0x2C
        }

        private enum TimeMeasurementOperation : uint
        {
            Start = 0,
            Stop = 1,
            Reset = 2
        }

        private const uint StringRegisterStart = 0x100;
        private const uint StringRegisterSize = 0x100;
        private const uint StringRegisterEnd = StringRegisterStart + StringRegisterSize;
        private const uint FileRegisterStart = StringRegisterEnd;
        private const uint FileRegisterSize = 0x10000;
        private const uint FileRegisterEnd = FileRegisterStart + FileRegisterSize;
    }
}

