//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if PLATFORM_LINUX
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Network;
using System.Net.NetworkInformation;
using System.Linq;
using Antmicro.Renode.TAPHelper;
using Antmicro.Renode.Peripherals;
using System.Threading;
using Antmicro.Migrant.Hooks;
using System.IO;
using Antmicro.Renode.Logging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;
using Mono.Unix;
using Antmicro.Renode.Network;
using Antmicro.Migrant;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public sealed class LinuxTapInterface : ITapInterface, IHasOwnLife, IDisposable
    {
        public LinuxTapInterface(string name, bool persistent)
        {
            backupMAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            mac = backupMAC;
            deviceName = name ?? "";
            this.persistent = persistent;
            Init();
        }

        public void Dispose()
        {
            if(stream != null)
            {
                stream.Close();
            }

            if(tapFileDescriptor != -1)
            {
                LibCWrapper.Close(tapFileDescriptor);
                tapFileDescriptor = -1;
            }
        }

        public void Pause()
        {
            if(!active)
            {
                return;
            }

            lock(lockObject)
            {
                var token = cts;
                token.Cancel();
                IsPaused = true;
                // we're not joining the read thread as it's canceled and will return after Read times out;
                // we might end up with multiple TransmitLoop threads running at the same time as a result of a quick Pause/Resume actions,
                // but only the last one will process data - the rest will terminate unconditionally after leaving the Read function call
            }
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            // TODO: non blocking
            if(stream == null)
            {
                return;
            }
            var handle = GCHandle.Alloc(frame.Bytes, GCHandleType.Pinned);
            try
            {
                var result = LibCWrapper.Write(stream.Handle, handle.AddrOfPinnedObject(), frame.Bytes.Length);
                if(!result)
                {
                    this.Log(LogLevel.Error,
                        "Error while writing to TUN interface: {0}.", result);
                }
            }
            finally
            {
                handle.Free();
            }
        }

        public void Resume()
        {
            if(!active)
            {
                return;
            }

            lock(lockObject)
            {
                cts = new CancellationTokenSource();
                thread = new Thread(() => TransmitLoop(cts.Token))
                {
                    Name = this.GetType().Name,
                    IsBackground = true
                };
                thread.Start();
                IsPaused = false;
            }
        }

        public void Start()
        {
            Resume();
        }

        public string InterfaceName { get; private set; }
        public event Action<EthernetFrame> FrameReady;

        public bool IsPaused { get; private set; } = true;

        public MACAddress MAC
        {
            get
            {
                return mac;
            }
            set
            {
                throw new NotSupportedException("Cannot change the MAC of the host machine.");
            }
        }

        [PostDeserialization]
        private void Init()
        {
            active = false;
            // if there is no /dev/net/tun, run in a "dummy" mode
            if(!File.Exists("/dev/net/tun"))
            {
                this.Log(LogLevel.Warning, "No TUN device found, running in dummy mode.");
                return;
            }

            IntPtr devName;
            if(deviceName != "")
            {
                // non-anonymous mapping
                devName = Marshal.StringToHGlobalAnsi(deviceName);
            }
            else
            {
                devName = Marshal.AllocHGlobal(DeviceNameBufferSize);
                Marshal.WriteByte(devName, 0); // null termination
            }
            try
            {
                // If we don't have rw access to /dev/net/tun we will loop here indefinitely
                // because we won't be able to open tap
                // Fortunately /dev/net/tun has rw by default
                tapFileDescriptor = TAPTools.OpenTAP(devName, persistent);
                if(tapFileDescriptor < 0)
                {
                    var process = new Process();
                    var output = string.Empty;
                #if NET
                    process.StartInfo.FileName = "dotnet";
                #else
                    process.StartInfo.FileName = "mono";
                #endif
                    process.StartInfo.Arguments = string.Format("{0} {1} true", DynamicModuleSpawner.GetTAPHelper(), deviceName);

                    try
                    {
                        SudoTools.EnsureSudoProcess(process, "TAP creator");
                    }
                    catch(Exception ex)
                    {
                        throw new RecoverableException("Process elevation failed: " + ex.Message);
                    }

                    process.EnableRaisingEvents = true;
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;

                    var started = process.Start();
                    if(started)
                    {
                        output = process.StandardError.ReadToEnd();
                        process.WaitForExit();
                    }
                    if(!started || process.ExitCode != 0)
                    {
                        this.Log(LogLevel.Warning, "Could not create TUN/TAP interface, running in dummy mode.");
                        this.Log(LogLevel.Debug, "Error {0} while opening tun device '{1}'. {2}", process.ExitCode, deviceName, output);
                        return;
                    }
                    Init();
                    return;
                }
                stream = new UnixStream(tapFileDescriptor, true);
                InterfaceName = Marshal.PtrToStringAnsi(devName);
                this.Log(LogLevel.Info,
                    "Opened interface {0}.", InterfaceName);
            }
            finally
            {
                Marshal.FreeHGlobal(devName);
            }
            active = true;
            var ourInterface = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(x => x.Name == InterfaceName);
            if(ourInterface != null)
            {
                mac = (MACAddress)ourInterface.GetPhysicalAddress();
            }
        }

        private void TransmitLoop(CancellationToken token)
        {
            while(true)
            {
                byte[] buffer = null;
                if(stream == null)
                {
                    return;
                }
                try
                {
                    buffer = LibCWrapper.Read(stream.Handle, MTU, ReadTimeout, () => token.IsCancellationRequested);
                }
                catch(ArgumentException)
                {
                    // stream was closed
                    return;
                }
                catch(ObjectDisposedException)
                {
                    return;
                }

                if(token.IsCancellationRequested)
                {
                    return;
                }
                if(buffer == null || buffer.Length == 0)
                {
                    continue;
                }
                if(Misc.TryCreateFrameOrLogWarning(this, buffer, out var frame, addCrc: true))
                {
                    FrameReady?.Invoke(frame);
                }
            }
        }

        private const int DeviceNameBufferSize = 8192;
        private const int MTU = 1522;
        private const int ReadTimeout = 100; // in milliseconds

        [Transient]
        private bool active;
        [Transient]
        private MACAddress mac;
        private MACAddress backupMAC;
        [Transient]
        private CancellationTokenSource cts;
        private readonly string deviceName;
        private readonly object lockObject = new object();
        private readonly bool persistent;
        [Transient]
        private UnixStream stream;
        [Transient]
        private int tapFileDescriptor;
        [Transient]
        private Thread thread;
    }
}
#endif
