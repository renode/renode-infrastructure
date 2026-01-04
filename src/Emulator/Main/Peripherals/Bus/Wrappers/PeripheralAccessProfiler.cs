//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU.GuestProfiling.ProtoBuf;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    public static class PeripheralAccessProfilerExtensions
    {
        public static void CreatePeripheralAccessProfiler(this Emulation emulation, string name, string filePath = null)
        {
            DisposePeripheralAccessProfilers();
            var profiler = new PeripheralAccessProfiler(filePath);
            emulation.MasterTimeSource.SyncHook += profiler.FlushBuffer;
            emulation.ExternalsManager.AddExternal(profiler, name);
            profiler.PreDisposeHook += delegate
            {
                try
                {
                    emulation.ExternalsManager.RemoveExternal(profiler);
                }
                catch
                { }
                emulation.MasterTimeSource.SyncHook -= profiler.FlushBuffer;
            };
        }

        public static PeripheralAccessProfiler GetPeripheralAccessProfiler()
        {
            return EmulationManager.Instance.CurrentEmulation.ExternalsManager.GetExternalsOfType<PeripheralAccessProfiler>().SingleOrDefault();
        }

        private static void DisposePeripheralAccessProfilers()
        {
            var emulation = EmulationManager.Instance.CurrentEmulation;
            foreach(var profiler in emulation.ExternalsManager.GetExternalsOfType<PeripheralAccessProfiler>())
            {
                profiler.Dispose();
            }
        }

        private static void FlushBuffer(this PeripheralAccessProfiler profiler, TimeInterval _) => profiler.FlushBuffer();
    }

    public class PeripheralAccessProfiler : IExternal, IDisposable
    {
        public PeripheralAccessProfiler(string filePath = null)
        {
            buses = new HashSet<SystemBus>();
            if(filePath != null)
            {
                Start(filePath);
            }
        }

        public void Start(string filePath)
        {
            lock(writeLock)
            {
                if(started)
                {
                    FinishFile();
                }

                fileStream = new FileStream(new SequencedFilePath(filePath), FileMode.Create, FileAccess.Write);
                writer = new PerfettoTraceWriter();
                ticksAtStart = CustomDateTime.Now.Ticks;
                started = true;

                foreach(var bus in buses)
                {
                    foreach(var cpu in bus.GetCPUs())
                    {
                        var trackId = (ulong)cpu.UniqueObjectId;
                        writer.CreateTrack(cpu.GetName(), trackId);
                        writer.CreateEventBegin(0, InBetweenName, trackId);
                    }
                }
            }
        }

        public void Enable(SystemBus sysbus, string filePath = null)
        {
            lock(writeLock)
            {
                if(filePath != null)
                {
                    Start(filePath);
                }

                if(!buses.Add(sysbus))
                {
                    throw new RecoverableException("Peripheral access profiling is already enabled for this system bus");
                }
                sysbus.EnableAllPeripheralAccessWrappers(typeof(ReadAccessProfilerWrapper<>), typeof(WriteAccessProfilerWrapper<>), enable: true, name: WrappersName);

                if(!started)
                {
                    return;
                }
                foreach(var cpu in sysbus.GetCPUs())
                {
                    var trackId = (ulong)cpu.UniqueObjectId;
                    writer.CreateTrack(cpu.GetName(), trackId);
                    writer.CreateEventBegin(0, InBetweenName, trackId);
                }
            }
        }

        public void Disable(SystemBus sysbus)
        {
            lock(writeLock)
            {
                if(!buses.Remove(sysbus))
                {
                    throw new RecoverableException("Peripheral access profiling is not enabled for this system bus");
                }
                sysbus.EnableAllPeripheralAccessWrappers(typeof(ReadAccessProfilerWrapper<>), typeof(WriteAccessProfilerWrapper<>), enable: false, name: WrappersName);
            }
        }

        public void Dispose()
        {
            lock(writeLock)
            {
                PreDisposeHook?.Invoke();
                foreach(var bus in buses)
                {
                    bus.EnableAllPeripheralAccessWrappers(typeof(ReadAccessProfilerWrapper<>), typeof(WriteAccessProfilerWrapper<>), enable: false, name: WrappersName);
                }
                FinishFile();
                buses.Clear();
            }
        }

        public void Finish()
        {
            lock(writeLock)
            {
                if(!started)
                {
                    throw new RecoverableException("Peripheral access profiling is not started");
                }
                FinishFile();
            }
        }

        public void FlushBuffer()
        {
            lock(writeLock)
            {
                if(!started)
                {
                    return;
                }
                writer.FlushBuffer(fileStream);
            }
        }

        public event Action PreDisposeHook;

        private static IDisposable GetLogger(IAccessProfilerWrapper wrapper, Access access, long offset)
        {
            var accessString = GetAccessString(access);
            var register = wrapper.RegisterMapper.ToString(offset);

            if(wrapper.Bus.TryGetCurrentCPU(out var cpu))
            {
                var trackId = (ulong)cpu.UniqueObjectId;
                var pc = cpu.PC.ToString();

                PeripheralAccessProfilerExtensions.GetPeripheralAccessProfiler()?.LogStart(trackId, accessString, wrapper.Type, wrapper.InstanceName, register, pc);
                return DisposableWrapper.New(() =>
                {
                    // Pop 5 items added in LogStart
                    PeripheralAccessProfilerExtensions.GetPeripheralAccessProfiler()?.LogEnd((ulong)trackId, stackLength: 5);
                });
            }

            PeripheralAccessProfilerExtensions.GetPeripheralAccessProfiler()?.LogStart(userTrackId, accessString, wrapper.Type, wrapper.InstanceName, register);
            return DisposableWrapper.New(() =>
            {
                // Pop 4 items added in LogStart
                PeripheralAccessProfilerExtensions.GetPeripheralAccessProfiler()?.LogEnd(userTrackId, stackLength: 4);
            });
        }

        private static string GetAccessString(Access access)
        {
            switch(access)
            {
            case Access.Read:
                return "read";
            case Access.Write:
                return "write";
            default:
                throw new Exception("unreachable");
            }
        }

        private static string GetPeripheralTypeName(IBusPeripheral peripheral)
        {
            var type = peripheral.GetType();
            var peripheralNamespace = type.Namespace;
            if(peripheralNamespace.StartsWith(DefaultNamespace))
            {
                peripheralNamespace = peripheralNamespace.Substring(DefaultNamespace.Length);
            }
            return $"{peripheralNamespace}.{type.Name}";
        }

        private static string GetPeripheralName(IBusPeripheral peripheral)
        {
            try
            {
                return peripheral.GetName();
            }
            catch
            {
                return FallbackInstanceName;
            }
        }

        private static readonly ulong? userTrackId = null;

        private void FinishFile()
        {
            lock(writeLock)
            {
                if(!started)
                {
                    return;
                }
                started = false;

                var now = CurrentTimestamp;

                // End all in between accesses
                foreach(var bus in buses)
                {
                    foreach(var cpu in bus.GetCPUs())
                    {
                        writer.CreateEventEnd(now, (ulong)cpu.UniqueObjectId);
                    }
                }
                if(userTrackCreated)
                {
                    writer.CreateEventEnd(now, localUserTrackId);
                }
                writer.FlushBuffer(fileStream);

                this.Log(LogLevel.Info, "{0} has been saved", fileStream.Name);
                fileStream.Dispose();
                fileStream = null;
                writer = null;
            }
        }

        private void LogStart(ulong? trackId, params string[] stack)
        {
            lock(writeLock)
            {
                if(!started)
                {
                    return;
                }

                if(!userTrackCreated && trackId == userTrackId)
                {
                    localUserTrackId = (ulong)new IdentifiableObject().UniqueObjectId;
                    writer.CreateTrack(UserTrackName, localUserTrackId);
                    writer.CreateEventBegin(0, InBetweenName, localUserTrackId);
                    userTrackCreated = true;
                }
                var now = CurrentTimestamp;
                writer.CreateEventEnd(now, trackId ?? localUserTrackId); // in between accesses
                foreach(var entry in stack)
                {
                    writer.CreateEventBegin(now, entry, trackId ?? localUserTrackId);
                }
            }
        }

        private void LogEnd(ulong? trackId, int stackLength)
        {
            lock(writeLock)
            {
                if(!started)
                {
                    return;
                }

                var now = CurrentTimestamp;
                for(var i = 0; i < stackLength; ++i)
                {
                    writer.CreateEventEnd(now, trackId ?? localUserTrackId);
                }
                writer.CreateEventBegin(now, InBetweenName, trackId ?? localUserTrackId);
            }
        }

        private ulong CurrentTimestamp => (ulong)(CustomDateTime.Now.Ticks - ticksAtStart) * TicksInNanosecond;

        private long ticksAtStart;
        private FileStream fileStream;
        private PerfettoTraceWriter writer;
        private bool userTrackCreated;
        private bool started;
        private ulong localUserTrackId;

        private readonly HashSet<SystemBus> buses;
        private readonly object writeLock = new object();

        private const string UserTrackName = "User";
        private const string InBetweenName = "-";
        private const string DefaultNamespace = "Antmicro.Renode.Peripherals.";
        private const string WrappersName = "Access profiling";
        private const string FallbackInstanceName = "<unknown>";
        private const ulong TicksInNanosecond = 100;

        private interface IAccessProfilerWrapper
        {
            string Type { get; }

            string InstanceName { get; }

            RegisterMapper RegisterMapper { get; }

            IBusController Bus { get; }
        }

        private class ReadAccessProfilerWrapper<T> : ReadHookWrapper<T>, IAccessProfilerWrapper
        {
            public ReadAccessProfilerWrapper(IBusPeripheral peripheral, Func<long, T> originalMethod)
                : base(peripheral, originalMethod)
            {
                Type = GetPeripheralTypeName(peripheral);
                InstanceName = GetPeripheralName(peripheral);
                RegisterMapper = new RegisterMapper(peripheral.GetType());
                Bus = peripheral.GetMachine().GetSystemBus(peripheral);
            }

            public override T Read(long offset)
            {
                using(GetLogger(this, Access.Read, offset))
                {
                    return OriginalMethod(offset);
                }
            }

            public string Type { get; }

            public string InstanceName { get; }

            public RegisterMapper RegisterMapper { get; }

            public IBusController Bus { get; }
        }

        private class WriteAccessProfilerWrapper<T> : WriteHookWrapper<T>, IAccessProfilerWrapper
        {
            public WriteAccessProfilerWrapper(IBusPeripheral peripheral, Action<long, T> originalMethod)
                : base(peripheral, originalMethod, null, null)
            {
                Type = GetPeripheralTypeName(peripheral);
                InstanceName = GetPeripheralName(peripheral);
                RegisterMapper = new RegisterMapper(peripheral.GetType());
                Bus = peripheral.GetMachine().GetSystemBus(peripheral);
            }

            public override void Write(long offset, T value)
            {
                using(GetLogger(this, Access.Write, offset))
                {
                    OriginalMethod(offset, value);
                }
            }

            public string Type { get; }

            public string InstanceName { get; }

            public RegisterMapper RegisterMapper { get; }

            public IBusController Bus { get; }
        }

        private enum Access
        {
            Read,
            Write,
        }
    }
}