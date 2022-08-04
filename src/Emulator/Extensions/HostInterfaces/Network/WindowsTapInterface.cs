//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

#if PLATFORM_WINDOWS
using System;
using System.Net.NetworkInformation;
using System.Linq;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Network;
using Antmicro.Migrant;
using System.Diagnostics;
using Antmicro.Renode.Exceptions;
using System.Security;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public sealed class WindowsTapInterface : ITapInterface, IHasOwnLife, IDisposable
    {
        public WindowsTapInterface(string name)
        {
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            InterfaceName = name ?? "";
            Init();
        }

        public void Dispose()
        {
            if(isInDummyMode)
            {
                return;
            }

            if(handle != null)
            {
                // inform the device that it is disconnected
                ChangeDeviceStatus(false);
            }
            stream?.Close();
            handle?.Close();
        }

        public void Pause()
        {
            lock(lockObject)
            {
                if(thread != null)
                {
                    cancellationTokenSource.Cancel(); //notify the thread to finish its work
                    thread.Join();
                    thread = null;
                }      
            }
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            if(stream == null)
            {
                this.Log(LogLevel.Error, "Stream null on sending the frame to the TAP interface");
                return;
            }
            stream.Write(frame.Bytes, 0, frame.Bytes.Length);
            stream.Flush();
            this.Log(LogLevel.Noisy, "{0} byte frame sent to the TAP interface", frame.Bytes.Length);
        }

        public void Resume()
        {
            lock(lockObject)
            {
                if(thread == null)
                {
                    cancellationTokenSource = new CancellationTokenSource();
                    thread = new Thread(() => TransmitLoop(cancellationTokenSource.Token))
                    {
                        Name = this.GetType().Name,
                        IsBackground = true
                    };
                    thread.Start();
                }
            }
        }

        public void Start()
        {
            Resume();
        }

        public string InterfaceName { get; }

        public MACAddress MAC { get; set; }
        public event Action<EthernetFrame> FrameReady;

        private Guid? GetDeviceGuid(string name)
        {
            var adapters = Registry.LocalMachine.OpenSubKey(AdapterRegistryBranch);
            if(adapters == null)
            {
                return null;
            }
            var connections = Registry.LocalMachine.OpenSubKey(ConnectionRegistryBranch);
            foreach(string subkey in adapters.GetSubKeyNames())
            {
                try
                {
                    var adapter = adapters.OpenSubKey(subkey);

                    if(adapter == null)
                    {
                        return null;
                    }
                    var adapterType = (string)adapter.GetValue("ComponentId", "");
                    //make sure whether the adapter listed in the registry is a tap interface
                    //the tap-Windows6 driver lists its adapter type as "root\tap0901"
                    if(adapterType == AdapterType)
                    {
                        string connectionGuid = (string)adapter.GetValue("NetCfgInstanceId");
                        var connection = connections.OpenSubKey($"{connectionGuid}\\Connection");
                        //check for the interface's name
                        if((string)connection.GetValue("Name") == name)
                        {
                            return new Guid(connectionGuid);
                        }
                    }
                }
                catch(SecurityException)
                {
                    //There is a registry branch ({AdapterRegistryBranch}\Properties) that by default even the Administrator account doesn't have permissions to read.
                    //Branches like these should be skipped.
                }
            }
            return null;
        }

        [PostDeserialization]
        private void Init()
        {
            this.Log(LogLevel.Debug, "Initializing Windows TAP device");
            var tapctlPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\OpenVPN", "", "");
            if(String.IsNullOrEmpty(tapctlPath))
            {
                isInDummyMode = true;
                this.Log(LogLevel.Warning, "tapctl.exe utility not found - running in the dummy mode!");
                return;
            }
            isInDummyMode = false;
            tapctlPath += @"\bin\tapctl.exe";
            //check whether the interface with such name exists
            var deviceGuid = GetDeviceGuid(InterfaceName);
            //create interface if it doesn't exist
            if(deviceGuid == null)
            {
                using(var tapctlProcess = new Process())
                {
                    tapctlProcess.StartInfo.Verb = "runas";
                    tapctlProcess.StartInfo.FileName = tapctlPath;
                    tapctlProcess.StartInfo.Arguments = $"create --name {InterfaceName}";
                    tapctlProcess.StartInfo.UseShellExecute = true;
                    tapctlProcess.Start();
                    tapctlProcess.WaitForExit();
                }
            }
            deviceGuid = GetDeviceGuid(InterfaceName);
            if(deviceGuid == null)
            {
                throw new RecoverableException("Failed to retrieve the GUID of the TAP device. Please use the tapctl.exe tool manually to create the TAP device.");
            }
            this.Log(LogLevel.Debug, "device GUID: {0}", deviceGuid.ToString().ToUpper());
            string deviceFilePath = $"\\\\.\\Global\\{{{deviceGuid.ToString().ToUpper()}}}.tap";
            handle = new SafeFileHandle(
                CreateFile(
                    deviceFilePath,
                    0x2000000, //MAXIMUM_ALLOWED constant 
                    0,
                    IntPtr.Zero,
                    FileMode.Open,
                    (int)(FileAttributes.System) | 0x40000000, //FILE_FLAG_OVERLAPPED constant
                    IntPtr.Zero),
                true);
            int error = Marshal.GetLastWin32Error();
            if(error != 0)
            {
                throw new RecoverableException($"Win32 error when opening the handle to the device file at path: {deviceFilePath} \n error id: {error}");
            }
            stream = new FileStream(handle, FileAccess.ReadWrite, MTU, true);
            // inform the device that it is connected
            ChangeDeviceStatus(true);
        }

        private void TransmitLoop(CancellationToken token)
        {
            while(true)
            {
                if(token.IsCancellationRequested)
                {
                    this.Log(LogLevel.Noisy, "Requested thread cancellation - stopping reading from the TAP device file.");
                    return;
                }

                if(stream == null)
                {
                    this.Log(LogLevel.Error, "Stream null on receiving the frame from the TAP interface - stopping reading from the TAP device file");
                    return;
                }
                try
                {
                    var buffer = new byte[MTU];
                    int bytesRead = stream.Read(buffer, 0, MTU);
                    if(bytesRead > 0)
                    {
                        var packet = new byte[bytesRead];
                        Array.Copy(buffer, packet, bytesRead);
                        this.Log(LogLevel.Noisy, "Received {0} bytes frame", bytesRead);
                        if(Misc.TryCreateFrameOrLogWarning(this, packet, out var frame, addCrc: true))
                        {
                            FrameReady?.Invoke(frame);
                        }
                    }
                }
                catch(ArgumentException e)
                {
                    this.Log(LogLevel.Error, "Stream was most likely closed - stopping reading from the TAP device file. Exception message: {0}", e.Message);
                    return;
                }
                catch(ObjectDisposedException e)
                {
                    this.Log(LogLevel.Error, "Error reading data - stopping reading from the TAP device file. Exception message: {0}", e.Message);
                    return;
                }
            }
        }

        private void ChangeDeviceStatus(bool isOn)
        {
            IntPtr deviceStatus = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(deviceStatus, isOn ? 1 : 0);
            DeviceIoControl(handle.DangerousGetHandle(), TapDriverControlCode(6, 0), deviceStatus, 4, deviceStatus, 4, out int len, IntPtr.Zero);
            Marshal.FreeHGlobal(deviceStatus);
        }

        private static uint CalculateControlCode(uint deviceType, uint function, uint method, uint access)
        {
            return ((deviceType << 16) | (access << 14) | (function << 2) | method);
        }

        private static uint TapDriverControlCode(uint request, uint method)
        {
            return CalculateControlCode(0x22, request, method, 0);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile
        );

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped
        );

        //The following GUIDs are guaranteed by Microsoft
        //Microsoft docs link: https://docs.microsoft.com/en-us/windows-hardware/drivers/install/system-defined-device-setup-classes-available-to-vendors
        private const string AdapterRegistryBranch = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
        private const string ConnectionRegistryBranch = @"SYSTEM\CurrentControlSet\Control\Network\{4D36E972-E325-11CE-BFC1-08002BE10318}";
        private const int MTU = 1522;
        private const string AdapterType = @"root\tap0901";

        private bool isInDummyMode;
        private readonly object lockObject = new object();

        [Transient]
        private CancellationTokenSource cancellationTokenSource;
        [Transient]
        private FileStream stream;
        [Transient]
        private SafeFileHandle handle;
        [Transient]
        private Thread thread;
    }
}
#endif
