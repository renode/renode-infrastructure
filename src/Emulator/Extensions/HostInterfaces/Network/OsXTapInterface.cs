//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if PLATFORM_OSX
using System;
using Antmicro.Renode.Peripherals.Network;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Core;
using System.IO;
using System.Threading;
using Antmicro.Renode.Logging;
using System.Threading.Tasks;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Network;
using System.Net.NetworkInformation;
using System.Linq;
using Antmicro.Renode.Utilities;
using Mono.Unix;
using Antmicro.Renode.Exceptions;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public sealed class OsXTapInterface : ITapInterface, IHasOwnLife, IDisposable
    {
        public OsXTapInterface(string interfaceNameOrPath)
        {
            originalInterfaceNameOrPath = interfaceNameOrPath;
            Init();
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            if(deviceFile == null)
            {
                return;
            }
            var bytes = frame.Bytes;
            try
            {
                // since the file reader operations are buffered, we have to immediately flush writes
                deviceFile.Write(bytes, 0, bytes.Length);
                deviceFile.Flush();
            }
            catch(IOException)
            {
                if(networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    this.DebugLog("Interface is not up during write, frame dropped.");
                }
                else
                {
                    throw;
                }
            }
            this.NoisyLog("Frame of length {0} sent to host.", frame.Length);
        }

        public void Start()
        {
            Resume();
        }

        public void Pause()
        {
            if(deviceFile == null)
            {
                return;
            }
            cts.Cancel();
            readerTask.Wait();
        }

        public void Resume()
        {
            if(deviceFile == null)
            {
                return;
            }
            cts = new CancellationTokenSource();
            readerTask = Task.Run(ReadPacketAsync);
            readerTask.ContinueWith(x => 
                this.Log(LogLevel.Error, "Exception happened on reader task ({0}). Task stopped.", x.Exception.InnerException.GetType().Name), TaskContinuationOptions.OnlyOnFaulted);
        }

        public void Dispose()
        {
            if(deviceFile != null)
            {
                deviceFile.Close();
            }
        }

        public event Action<EthernetFrame> FrameReady;

        public MACAddress MAC 
        { 
            get 
            { 
                return macAddress; 
            } 

            set 
            { 
                macAddress = value; 
            } 
        }

        public string InterfaceName 
        {
            get
            {
                return networkInterface.Name;
            }
        }

        private async Task ReadPacketAsync()
        {
            var buffer = new byte[Mtu];
            while(!cts.IsCancellationRequested)
            {
                try
                {
                    if(await deviceFile.ReadAsync(buffer, 0, buffer.Length, cts.Token) > 0)
                    {
                        if(!Misc.TryCreateFrameOrLogWarning(this, buffer, out var frame, addCrc: true))
                        {
                            return;
                        }
                        FrameReady?.Invoke(frame);
                        this.NoisyLog("Frame of length {0} received from host.", frame.Bytes.Length);
                    }
                }
                catch(IOException)
                {
                    if(networkInterface.OperationalStatus != OperationalStatus.Up)
                    {
                        this.NoisyLog("I/O exception while interface is not up, waiting {0}s.", Misc.NormalizeDecimal(GracePeriod.TotalSeconds));
                        // probably the interface is not opened yet
                        await Task.Delay(GracePeriod);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        [PostDeserialization]
        private void Init()
        {
            var interfaceNameOrPath = originalInterfaceNameOrPath;
            if(!Directory.Exists("/Library/Extensions/tap.kext/"))
            {
                this.Log(LogLevel.Warning, "No TUNTAP kernel extension found, running in dummy mode.");
                MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
                return;
            }
            if(!File.Exists(interfaceNameOrPath))
            {
                var tapDevicePath = ConfigurationManager.Instance.Get<string>("tap", "tap-device-path", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                interfaceNameOrPath = Path.Combine(tapDevicePath, interfaceNameOrPath);
            }

            deviceFile = File.Open(interfaceNameOrPath, FileMode.Open, FileAccess.ReadWrite);

            // let's find out to what interface the character device file belongs
            var deviceType = new UnixFileInfo(interfaceNameOrPath).DeviceType;
            var majorNumber = deviceType >> 24;
            var minorNumber = deviceType & 0xFFFFFF;
            this.DebugLog($"Opening TAP device with major number: {majorNumber} and minor number: {minorNumber}");
            networkInterface = NetworkInterface.GetAllNetworkInterfaces().Single(x => x.Name == "tap" + minorNumber);
            MAC = (MACAddress)networkInterface.GetPhysicalAddress();
        }

        [Transient]
        private Task readerTask;

        [Transient]
        private CancellationTokenSource cts;

        [Transient]
        private FileStream deviceFile;

        [Transient]
        private NetworkInterface networkInterface;

        [Transient]
        // we need to have this field explicitly to put [Transient] attribute on it
        private MACAddress macAddress;

        private readonly string originalInterfaceNameOrPath;

        private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(1);
        private const int Mtu = 1500;
    }
}
#endif
