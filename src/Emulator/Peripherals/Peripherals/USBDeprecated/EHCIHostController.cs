//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class EHCIHostController : IDoubleWordPeripheral, IPeripheralRegister<IUSBHub, USBRegistrationPoint>, IPeripheralContainer<IUSBPeripheral, USBRegistrationPoint>, IDisposable
    {
        public EHCIHostController(IMachine machine, uint ehciBaseAddress = 0x100, uint capabilityRegistersLength = 0x40, uint numberOfPorts = 1, uint? ulpiBaseAddress = 0x170)
        {
            this.machine = machine;
            sysbus = machine.GetSystemBus(this);
            this.numberOfPorts = numberOfPorts;

            this.ehciBaseAddress = ehciBaseAddress;
            if(ulpiBaseAddress.HasValue)
            {
                ulpiChip = new Ulpi(ulpiBaseAddress.Value);
            }
            capabilitiesLength = capabilityRegistersLength;

            IRQ = new GPIO();
            registeredDevices = new Dictionary<byte, IUSBPeripheral>();
            addressedDevices = new Dictionary<byte, IUSBPeripheral>();
            registeredHubs = new ReusableIdentifiedList<IUSBHub>();
            interruptEnableRegister = new InterruptEnable();
            thisLock = new Object();

            portStatusControl = new PortStatusAndControlRegister[numberOfPorts]; //port status control
            for(int i = 0; i< portStatusControl.Length; i++)
            {
                portStatusControl[i] = new PortStatusAndControlRegister();
            }

            asyncThread = machine.ObtainManagedThread(AsyncListScheduleThread, 100);
            periodicThread = machine.ObtainManagedThread(PeriodicListScheduleThread, 100);

            SoftReset(); //soft reset must be done before attaching devices

            periodic_qh = new QueueHead(sysbus);
            async_qh = new QueueHead(sysbus);
        }

        public void Dispose()
        {
            asyncThread.Dispose();
            periodicThread.Dispose();
        }

        public void Register(IUSBPeripheral peripheral, USBRegistrationPoint registrationPoint)
        {
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            AttachDevice(peripheral, registrationPoint.Address.Value);
        }

        public void Register(IUSBHub peripheral, USBRegistrationPoint registrationPoint)
        {
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            AttachDevice(peripheral, registrationPoint.Address.Value);
            RegisterHub(peripheral);
            return;
        }

        public void Unregister(IUSBHub peripheral)
        {
            byte port = registeredDevices.FirstOrDefault(x => x.Value == peripheral).Key;
            DetachDevice(port);
            machine.UnregisterAsAChildOf(this, peripheral);
            registeredDevices.Remove(port);
            registeredHubs.Remove(peripheral);
        }

        public void Unregister(IUSBPeripheral peripheral)
        {
            byte port = registeredDevices.FirstOrDefault(x => x.Value == peripheral).Key;
            DetachDevice(port);
            machine.UnregisterAsAChildOf(this, peripheral);
            registeredDevices.Remove(port);
            // TODO: why do we remove from hubs here?
            registeredHubs.RemoveAt(port);
        }

        public void AttachDevice(IUSBPeripheral device, byte port)
        {
            registeredDevices.Add(port, device);
            PortStatusAndControlRegisterChanges change = portStatusControl[port - 1].Attach(device);

            if(change.ConnectChange)
            {
                usbStatus |= (uint)InterruptMask.PortChange;
            }

            if(interruptEnableRegister.Enable && interruptEnableRegister.PortChangeEnable)
            {
                usbStatus |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.PortChange;
                IRQ.Set(true);
            }
            defaultDevice = device;
            activeDevice = device;
        }

        public void DetachDevice(byte port)
        {
            registeredDevices.Remove(port);
            var change = portStatusControl[port - 1].Detach();

            if(change.ConnectChange)
            {
                usbStatus |= (uint)InterruptMask.PortChange;
            }

            if(interruptEnableRegister.Enable && interruptEnableRegister.PortChangeEnable)
            {
                usbStatus |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.PortChange;
                IRQ.Set(true);
            }
        }

        public IEnumerable<USBRegistrationPoint> GetRegistrationPoints(IUSBPeripheral peripheral)
        {
            throw new NotImplementedException();
        }

        public GPIO IRQ { get; private set; }

        public IEnumerable<IRegistered<IUSBPeripheral, USBRegistrationPoint>> Children
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public virtual uint ReadDoubleWord(long address)
        {
            long shift;
            if(address.InRange(ehciBaseAddress, capabilitiesLength, out shift))
            {
                switch((CapabilityRegisters)shift)
                {
                case CapabilityRegisters.CapabilityRegistersLength:
                    return (hciVersion & EhciHciVersionMask) << EhciHciVersionOffset | (capabilitiesLength & EhciCapabilityRegistersLengthMask);
                case CapabilityRegisters.StructuralParameters:
                    return ((uint)(portStatusControl.Length) & hcsparamsNPortsMask);
                }
            }
            else if(address.InRange(ehciBaseAddress + capabilitiesLength, EhciPortStatusControlRegisterOffset + numberOfPorts * EhciPortStatusControlRegisterWidth, out shift))
            {
                switch((OperationalRegisters)shift)
                {
                case OperationalRegisters.UsbCommand:
                    return usbCommand & (~0x04u);
                case OperationalRegisters.UsbStatus:
                    //TODO: check locking
                    lock(thisLock)
                    {
                        return usbStatus;
                    }
                case OperationalRegisters.UsbInterruptEnable:
                    return interruptEnableRegister.Value;
                case OperationalRegisters.UsbFrameIndex:
                    usbFrameIndex = usbFrameIndex + 4;
                    usbFrameIndex &= 0x1f;
                    return usbFrameIndex & ~0x7u;
                case OperationalRegisters.PeriodicListBaseAddress:
                    return periodicAddress;
                case OperationalRegisters.AsyncListAddress:
                    return asyncAddress;
                case OperationalRegisters.ConfiguredFlag:
                    return configFlag;
                default:
                    if(shift >= EhciPortStatusControlRegisterOffset)
                    {
                        var portNumber = (shift - EhciPortStatusControlRegisterOffset) / EhciPortStatusControlRegisterWidth;
                        return portStatusControl[portNumber].getValue();
                    }
                    break;
                }
            }

            switch((OtherRegisters)address)
            {
            case OtherRegisters.UsbMode:
                return (uint)mode;
            case OtherRegisters.UsbDCCParams:
                return EhciNumberOfEndpoints;
            default:
                if(ulpiChip != null && address == ulpiChip.BaseAddress)
                {
                    return (uint)(ulpiChip.LastReadValue << UlpiDataReadOffset) | UlpiSyncStateMask;
                }
                break;
            }

            this.LogUnhandledRead(address);
            return 0;
        }

        public virtual void WriteDoubleWord(long address, uint value)
        {
            long shift;
            if(address.InRange(ehciBaseAddress + capabilitiesLength, EhciPortStatusControlRegisterOffset + numberOfPorts * EhciPortStatusControlRegisterWidth, out shift))
            {
                switch((OperationalRegisters)shift)
                {
                case OperationalRegisters.UsbCommand:
                    lock(thisLock)
                    {
                        usbCommand = value;
                        if((value & (uint)EhciUsbCommandMask.HostControllerReset) != 0)
                        {
                            usbCommand &= ~(uint)EhciUsbCommandMask.HostControllerReset; // clear reset bit
                            SoftReset();
                            return;
                        }
                    }
                    if((value & (uint)EhciUsbCommandMask.AsynchronousScheduleEnable) == 0) //if disable async schedule
                    {
                        if ((usbStatus & (uint)EhciUsbStatusMask.AsynchronousScheduleStatus) != 0)
                        {
                            lock(thisLock)
                            {
                                usbStatus &= ~(uint)EhciUsbStatusMask.AsynchronousScheduleStatus;
                            }
                            asyncThread.Stop();
                        }
                    }
                    else
                    {
                        lock(thisLock)
                        {
                            usbStatus |= (uint)EhciUsbStatusMask.Reclamation; //raise reclamation
                        }
                        if((usbStatus & (uint)EhciUsbStatusMask.AsynchronousScheduleStatus) == 0)
                        {
                            lock(thisLock)
                            {
                                usbStatus |= (uint)EhciUsbStatusMask.AsynchronousScheduleStatus; //confirm async schedule enable
                            }
                            asyncThread.Start();
                        }
                    }
                    if((value & (uint)EhciUsbCommandMask.PeriodicScheduleEnable) != 0)
                    {
                        lock(thisLock)
                        {
                            usbStatus |= (uint)EhciUsbStatusMask.PeriodicScheduleStatus;
                        }
                        periodicThread.Start();
                    }
                    else
                    {
                        periodicThread.Stop();
                        lock(thisLock)
                        {
                            usbStatus &= ~(uint)EhciUsbStatusMask.PeriodicScheduleStatus;
                        }
                    }
                    if((value & (uint)EhciUsbCommandMask.RunStop) != 0)
                    {
                        lock(thisLock)
                        {
                            usbStatus &= ~(uint)EhciUsbStatusMask.HCHalted; //clear HCHalted bit in USB status reg
                        }
                        foreach(var port in portStatusControl)
                        {
                            port.Enable();
                        }
                    }
                    else
                    {
                        lock(thisLock)
                        {
                            usbStatus |= (uint)EhciUsbStatusMask.HCHalted; //set HCHalted bit in USB status reg
                        }
                    }
                    if((value & (uint)EhciUsbCommandMask.InterruptOnAsyncAdvanceDoorbell) != 0)
                    {
                        lock(thisLock)
                        {
                            usbCommand &= ~(uint)EhciUsbCommandMask.InterruptOnAsyncAdvanceDoorbell;
                            usbStatus |= (uint)EhciUsbStatusMask.InterruptOnAsyncAdvance;
                        }
                    }
                    return;
                case OperationalRegisters.AsyncListAddress:
                    lock(thisLock)
                    {
                        asyncAddress = value;
                    }
                    return;
                case OperationalRegisters.PeriodicListBaseAddress:
                    lock(thisLock)
                    {
                        periodicAddress = value;
                    }
                    return;
                case OperationalRegisters.UsbInterruptEnable:
                    interruptEnableRegister.OnAsyncAdvanceEnable = ((value & (uint)InterruptMask.InterruptOnAsyncAdvance) != 0);
                    interruptEnableRegister.HostSystemErrorEnable = ((value & (uint)InterruptMask.HostSystemError) != 0);
                    interruptEnableRegister.FrameListRolloverEnable = ((value & (uint)InterruptMask.FrameListRollover) != 0);
                    interruptEnableRegister.PortChangeEnable = ((value & (uint)InterruptMask.PortChange) != 0);
                    interruptEnableRegister.USBErrorEnable = ((value & (uint)InterruptMask.USBError) != 0);
                    interruptEnableRegister.Enable = ((value & (uint)InterruptMask.USBInterrupt) != 0);

                    if(interruptEnableRegister.Enable && interruptEnableRegister.PortChangeEnable)
                    {
                        foreach(var register in portStatusControl)
                        {
                            if((register.getValue() & PortStatusAndControlRegister.ConnectStatusChange) != 0)
                            {
                                IRQ.Set(false);
                                lock(thisLock)
                                {
                                    usbStatus |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.PortChange;
                                }
                                IRQ.Set(true);
                                return;
                            }
                        }
                    }
                    return;
                case OperationalRegisters.UsbStatus:
                    lock(thisLock)
                    {
                        usbStatus &= ~value;
                        if((usbStatus & (uint)InterruptMask.USBInterrupt) == 0)
                        {
                            IRQ.Set(false);
                        }
                    }
                    return;
                case OperationalRegisters.ConfiguredFlag:
                    configFlag = value;
                    return;
                default:
                    if(shift >= EhciPortStatusControlRegisterOffset)
                    {
                        var portNumber = (shift - EhciPortStatusControlRegisterOffset) / EhciPortStatusControlRegisterWidth;

                        PortStatusAndControlRegisterChanges change;
                        portStatusControl[portNumber].setValue(portStatusControl[portNumber].getValue() & (~(value & 0x0000002a)));
                        portStatusControl[portNumber].setValue(portStatusControl[portNumber].getValue() & ((value) | (~(1u << 2))));

                        value &= 0x007011c0;

                        change = portStatusControl[portNumber].setValue(value & (~(1u << 2)));
                        if((portStatusControl[portNumber].getValue() & (1u << 8)) != 0 && (value & (1u << 8)) == 0)
                        {
                            portStatusControl[portNumber].setValue((portStatusControl[portNumber].getValue() & (~(1u << 1))));
                            value |= 1 << 2;
                            change = portStatusControl[portNumber].setValue((portStatusControl[portNumber].getValue() & 0x007011c0) | value);
                        }

                        if(change.ConnectChange)
                        {
                            lock(thisLock)
                            {
                                usbStatus |= (uint)InterruptMask.PortChange;
                            }
                        }

                        if((interruptEnableRegister.Enable) && (interruptEnableRegister.PortChangeEnable))
                        {
                            lock(thisLock)
                            {
                                usbStatus |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.PortChange;
                            }
                            IRQ.Set(true);
                        }
                        return;
                    }
                    break;
                }
            }

            switch((OtherRegisters)address)
            {
            case OtherRegisters.UsbMode:
                mode = (ControllerMode)(value & 0x3);
                break;
            default:
                if(ulpiChip != null && address == ulpiChip.BaseAddress)
                {
                    var isReadOperation = (value & UlpiRdWrMask) == 0;
                    var ulpiRegister = (byte)((value & UlpiRegAddrMask) >> UlpiRegAddrOffset);
                    if(isReadOperation)
                    {
                        // we don't need read value here, as it will be stored in `LastReadValue` property of `ulpiChip`
                        ulpiChip.Read(ulpiRegister);
                    }
                    else
                    {
                        var valueToWrite = (byte)(value & UlpiDataWriteMask);
                        ulpiChip.Write(ulpiRegister, valueToWrite);
                    }
                }
                else
                {
                    this.LogUnhandledWrite(address, value);
                }
                break;
            }
        }

        public virtual void Reset()
        {
            SoftReset();
            activeDevice = defaultDevice; // TODO: why ?
        }

        private void SoftReset()
        {
            addressedDevices.Clear();
            foreach(var port in portStatusControl)
            {
                port.setValue(0x00001000);
                port.powerUp();
            }

            asyncThread.Stop();
            periodicThread.Stop();

            usbCommand = 0x80000; //usb command
            usbStatus = 0x1000; //usb status
            usbFrameIndex = 0; //usb frame index
            asyncAddress = 0;
            periodicAddress = 0; //next async addres
            configFlag = 0; // configured flag registers

            mode = ControllerMode.Idle;
            interruptEnableRegister.Clear();
            periodic_qh = new QueueHead(sysbus);
            async_qh = new QueueHead(sysbus);
        }

        private uint GetFrs()
        {
            switch((usbCommand >> 2) & 0x3)
            {
            case 1:
                return 512;
            case 2:
                return 256;
            default:
                return 1024;
            }
        }

        private void PeriodicListScheduleThread()
        {
            for(uint counter = 0; counter < GetFrs(); counter += 4)
            {
                ProcessList(false, counter);
            }
        }

        private void AsyncListScheduleThread()
        {
            ProcessList();
        }

        private void RegisterHub(IUSBHub hub)
        {
            registeredHubs.Add(hub);
            hub.RegisterHub += h =>
            {
                registeredHubs.Add(h);
            };
            hub.ActiveDevice += d =>
            {
                activeDevice = d;
            };
        }

        private IUSBPeripheral GetTargetDevice(QueueHead qh)
        {
            if((qh.Overlay.Status & StatusActive) == 0)
            {
                return null;
            }
            IUSBPeripheral targetDevice;

            if(qh.DeviceAddress != 0)
            {
                targetDevice = FindDevice(qh.DeviceAddress);
            }
            else
            {
                if(qh.HubAddress == 0 && qh.PortNumber == 0)
                {
                    targetDevice = activeDevice;
                }
                else if(qh.HubAddress != 0)
                {
                    targetDevice = FindDevice(qh.HubAddress, qh.PortNumber);
                }
                else
                {
                    targetDevice = activeDevice;
                }
            }
            return targetDevice;
        }

        private void ProcessList(bool async = true, uint counter = 0)
        {
            QueueHead qh;
            lock(thisLock)
            {
                if(async)
                {
                    qh = async_qh;
                    qh.Address = asyncAddress;
                }
                else
                {
                    qh = periodic_qh;
                    qh.Address = (periodicAddress & 0xfffff000u) + (counter & 0xFFCu);
                }
                if(!qh.IsValid || (qh.ElementName != 0x01))
                {
                    return;
                }

                if(qh.GoToNextLink())
                {
                    qh.Fetch();
                    qh.Advance();
                    if((qh.TransferDescriptor.Status & StatusActive) == 0)
                    {
                        return;
                    }
                }

                IUSBPeripheral targetDevice = GetTargetDevice(qh);
                if(targetDevice == null)
                {
                    return;
                }
                USBPacket packet;
                packet.bytesToTransfer = qh.Overlay.TotalBytesToTransfer;
                packet.ep = qh.EndpointNumber;
                packet.data = null;
                uint dataAmount;
                switch(qh.Overlay.PID)
                {
                case PIDCode.In://data transfer from device to host
                    this.NoisyLog("[process_{0}_list] IN {1:d} [{2}]", async ? "async" : "periodic", qh.Overlay.TotalBytesToTransfer, targetDevice);
                    if(qh.Overlay.TotalBytesToTransfer == 0)
                    {
                        break;
                    }
                    byte[] inData = null;
                    this.NoisyLog("[process_{0}_list] EP {1:d}", async ? "async" : "periodic", qh.EndpointNumber);
                    if(async)
                    {
                        if(qh.EndpointNumber == 0)
                        {
                            inData = targetDevice.GetDataControl(packet);
                            this.NoisyLog("[process_list] CONT");
                        }
                        else
                        {
                            inData = targetDevice.GetDataBulk(packet);
                            this.NoisyLog("[process_list] BULK");
                        }
                    }
                    else
                    {
                        inData = targetDevice.WriteInterrupt(packet);
                    }
                    uint inputSourceArray = 0;
                    uint bytesToTransfer = (inData == null) ? 0 : (uint)inData.Length;
                    while(bytesToTransfer > 0)
                    {
                        for(int i = 0; i < qh.Overlay.BufferPointer.Length; i++)
                        {
                            if((qh.Overlay.BufferPointer[i] == 0) || (bytesToTransfer == 0))
                            {
                                break;
                            }
                            dataAmount = Math.Min(bytesToTransfer, 4096);
                            var dataToSend = new byte[dataAmount];
                            Array.Copy(inData, inputSourceArray, dataToSend, 0, dataAmount);
                            bytesToTransfer -= dataAmount;
                            inputSourceArray += dataAmount;
                            sysbus.WriteBytes(dataToSend, qh.Overlay.BufferPointer[i] | qh.Overlay.CurrentOffset, (int)dataAmount);
                            qh.UpdateTotalBytesToTransfer(Math.Min(dataAmount, qh.Overlay.TotalBytesToTransfer));
                        }
                    }
                    break;
                case PIDCode.Setup://if setup command
                    this.NoisyLog("[async] SETUP {0:d}", qh.Overlay.TotalBytesToTransfer);
                    this.NoisyLog("[async] Device {0:d} [{1}]", qh.DeviceAddress, targetDevice);

                    setupData = new USBSetupPacket();
                    setupData.requestType = sysbus.ReadByte(qh.Overlay.BufferPointer[0] | qh.Overlay.CurrentOffset);
                    setupData.request = sysbus.ReadByte(qh.Overlay.BufferPointer[0] | qh.Overlay.CurrentOffset + 1);
                    setupData.value = sysbus.ReadWord(qh.Overlay.BufferPointer[0] | qh.Overlay.CurrentOffset + 2);
                    setupData.index = sysbus.ReadWord(qh.Overlay.BufferPointer[0] | qh.Overlay.CurrentOffset + 4);
                    setupData.length = sysbus.ReadWord(qh.Overlay.BufferPointer[0] | qh.Overlay.CurrentOffset + 6);

                    if(((setupData.requestType & 0x80u) >> 7) == (uint)DataDirection.DeviceToHost)//if device to host transfer
                    {
                        if(((setupData.requestType & 0x60u) >> 5) == (uint)USBRequestType.Standard)
                        {
                            switch((DeviceRequestType)setupData.request)
                            {
                            case DeviceRequestType.GetDescriptor:
                                targetDevice.GetDescriptor(packet, setupData);
                                break;
                            case DeviceRequestType.GetConfiguration:
                                targetDevice.GetConfiguration();
                                break;
                            case DeviceRequestType.GetInterface:
                                targetDevice.GetInterface(packet, setupData);
                                break;
                            case DeviceRequestType.GetStatus:
                                targetDevice.GetStatus(packet, setupData);
                                break;
                            default:
                                targetDevice.GetDescriptor(packet, setupData);
                                this.Log(LogLevel.Warning, "[async] Unsupported device request");
                                break;
                            }//end of switch request
                        }
                        else if(((setupData.requestType & 0x60u) >> 5) == (uint)USBRequestType.Class)
                        {
                            targetDevice.ProcessClassGet(packet, setupData);
                        }
                        else if(((setupData.requestType & 0x60u) >> 5) == (uint)USBRequestType.Vendor)
                        {
                            targetDevice.ProcessVendorGet(packet, setupData);
                        }
                    }
                    else//if host to device transfer
                        if(((setupData.requestType & 0x60) >> 5) == (uint)USBRequestType.Standard)
                    {
                        switch((DeviceRequestType)setupData.request)
                        {
                        case DeviceRequestType.SetAddress:
                            targetDevice.SetAddress(setupData.value);
                            AddressDevice(targetDevice, (byte)setupData.value);
                            break;
                        case DeviceRequestType.SetDescriptor:
                            targetDevice.GetDescriptor(packet, setupData);
                            break;
                        case DeviceRequestType.SetFeature:
                            targetDevice.GetDescriptor(packet, setupData);
                            break;
                        case DeviceRequestType.SetInterFace:
                            targetDevice.SetInterface(packet, setupData);
                            break;
                        case DeviceRequestType.SetConfiguration:
                            targetDevice.SetConfiguration(packet, setupData);
                            break;
                        default:
                            this.Log(LogLevel.Warning, "[async] Unsupported device request [ {0:X} ]", setupData.request);
                            break;
                        }//end of switch request
                    }//end of request type.standard
                    else if((setupData.requestType >> 5) == (uint)USBRequestType.Class)
                    {
                        targetDevice.ProcessClassSet(packet, setupData);
                    }
                    else if((setupData.requestType >> 5) == (uint)USBRequestType.Vendor)
                    {
                        targetDevice.ProcessVendorSet(packet, setupData);
                    }
                    qh.UpdateTotalBytesToTransfer(qh.Overlay.TotalBytesToTransfer);
                    break;

                case PIDCode.Out://data transfer from host to device
                    this.NoisyLog("[async] OUT {0:d}", qh.Overlay.TotalBytesToTransfer);
                    dataAmount = qh.Overlay.TotalBytesToTransfer;

                    var data = new byte[qh.Overlay.TotalBytesToTransfer];

                    var bytesTransferred = 0;
                    while(qh.Overlay.TotalBytesToTransfer > 0)
                    {
                        for(var i = 0; i < qh.Overlay.BufferPointer.Length; i++)
                        {
                            if(qh.Overlay.BufferPointer[i] == 0)
                            {
                                break;
                            }
                            if(qh.Overlay.TotalBytesToTransfer == 0)
                            {
                                break;
                            }
                            var transferredThisTurn = Math.Min(4096, (int)qh.Overlay.TotalBytesToTransfer);

                            //get data
                            sysbus.ReadBytes(qh.Overlay.BufferPointer[i] | qh.Overlay.CurrentOffset, transferredThisTurn, data, bytesTransferred);
                            bytesTransferred += transferredThisTurn;
                            qh.UpdateTotalBytesToTransfer((uint)transferredThisTurn);
                        }

                        if(bytesTransferred > 0)
                        {
                            packet.data = data;
                            if(qh.EndpointNumber == 0)
                            {
                                if(((setupData.requestType & 0x80u) >> 7) == (uint)DataDirection.DeviceToHost)//if device to host transfer
                                {
                                    if(((setupData.requestType & 0x60u) >> 5) == (uint)USBRequestType.Standard)
                                    {
                                        switch((DeviceRequestType)setupData.request)
                                        {
                                        case DeviceRequestType.GetDescriptor:
                                            targetDevice.GetDescriptor(packet, setupData);
                                            break;
                                        case DeviceRequestType.GetConfiguration:
                                            targetDevice.GetConfiguration();
                                            break;
                                        case DeviceRequestType.GetInterface:
                                            targetDevice.GetInterface(packet, setupData);
                                            break;
                                        case DeviceRequestType.GetStatus:
                                            targetDevice.GetStatus(packet, setupData);
                                            break;
                                        default:
                                            targetDevice.GetDescriptor(packet, setupData);
                                            this.Log(LogLevel.Warning, "[process_{0}_list] Unsupported device request1", async ? "async" : "periodic");
                                            break;
                                        }//end of switch request
                                    }
                                    else if(((setupData.requestType & 0x60u) >> 5) == (uint)USBRequestType.Class)
                                    {
                                        targetDevice.ProcessClassGet(packet, setupData);
                                    }
                                    else if(((setupData.requestType & 0x60u) >> 5) == (uint)USBRequestType.Vendor)
                                    {
                                        targetDevice.ProcessVendorGet(packet, setupData);
                                    }
                                }
                                else//if host to device transfer
                                    if(((setupData.requestType & 0x60) >> 5) == (uint)USBRequestType.Standard)
                                {
                                    switch((DeviceRequestType)setupData.request)
                                    {
                                    case DeviceRequestType.SetAddress:
                                        targetDevice.SetAddress(setupData.value);
                                        AddressDevice(targetDevice, (byte)setupData.value);
                                        break;
                                    case DeviceRequestType.SetDescriptor:
                                        targetDevice.GetDescriptor(packet, setupData);
                                        break;
                                    case DeviceRequestType.SetFeature:
                                        targetDevice.GetDescriptor(packet, setupData);
                                        break;
                                    case DeviceRequestType.SetInterFace:
                                        targetDevice.SetInterface(packet, setupData);
                                        break;
                                    case DeviceRequestType.SetConfiguration:
                                        targetDevice.SetConfiguration(packet, setupData);
                                        break;
                                    default:
                                        this.Log(LogLevel.Warning, "[process_{0}_list] Unsupported device request [ {1:X} ]", async ? "async" : "periodic", setupData.request);
                                        break;
                                    }//end of switch request
                                }//end of request type.standard
                                else if((setupData.requestType >> 5) == (uint)USBRequestType.Class)
                                {
                                    targetDevice.ProcessClassSet(packet, setupData);
                                }
                                else if((setupData.requestType >> 5) == (uint)USBRequestType.Vendor)
                                {
                                    targetDevice.ProcessVendorSet(packet, setupData);
                                }
                                break;
                            }
                        }
                        targetDevice.WriteDataBulk(packet);
                    }
                    break;
                default:
                    this.Log(LogLevel.Warning, "[process_list] Unknown PID");
                    return;
                }

                qh.Processed();
                qh.StaticMemoryUpdate();
                qh.Overlay.UpdateMemory();
                qh.WriteBackTransferDescriptor();
                qh.StaticMemoryUpdate();

                if(interruptEnableRegister.Enable && (async || interruptEnableRegister.FrameListRolloverEnable) && (!async || interruptEnableRegister.OnAsyncAdvanceEnable))
                {
                    if((async && qh.Overlay.InterruptOnComplete) || (!async && (usbFrameIndex > GetFrs())))
                    {
                        usbStatus |= (uint)InterruptMask.USBInterrupt;
                        if(async)
                        {
                            usbStatus |= (uint)InterruptMask.PortChange;
                        }
                        else
                        {
                            usbStatus |= (uint)InterruptMask.FrameListRollover;
                            usbFrameIndex -= GetFrs();
                        }
                        this.NoisyLog("[process_{0}_list] INT IN", async ? "async" : "periodic");
                        IRQ.Set(true);
                    }
                }
            }
        }

        private void AddressDevice(IUSBPeripheral device, byte address)
        {
            if(!addressedDevices.ContainsKey(address))//XXX: Linux hack
            {
                addressedDevices.Add(address, device);
            }
        }

        private IUSBPeripheral FindDevice(byte hubNumber, byte portNumber)
        {
            return addressedDevices.ContainsKey(hubNumber)
                ? ((IUSBHub)addressedDevices[hubNumber]).GetDevice(portNumber)
                : registeredDevices[portNumber];
        }

        private IUSBPeripheral FindDevice(byte deviceAddress)
        {
            return !addressedDevices.ContainsKey(deviceAddress)
                ? null
                : addressedDevices[deviceAddress];
        }

        private readonly Dictionary<byte,IUSBPeripheral> registeredDevices;
        private readonly ReusableIdentifiedList<IUSBHub> registeredHubs;
        private readonly Dictionary<byte,IUSBPeripheral> addressedDevices;
        private readonly InterruptEnable interruptEnableRegister;
        private readonly PortStatusAndControlRegister[] portStatusControl;
        private readonly Object thisLock;
        private readonly IManagedThread asyncThread;
        private readonly IManagedThread periodicThread;
        private readonly IMachine machine;
        private readonly IBusController sysbus;

        private uint usbCommand;
        private uint usbStatus;
        private uint usbFrameIndex;
        private uint periodicAddress;
        private uint asyncAddress;
        private uint configFlag;
        private uint ehciBaseAddress;

        private ControllerMode mode;
        private Ulpi ulpiChip;

        private IUSBPeripheral activeDevice;
        private IUSBPeripheral defaultDevice;

        private uint capabilitiesLength;
        private uint numberOfPorts;

        private USBSetupPacket setupData;
        private QueueHead periodic_qh;
        private QueueHead async_qh;

        private const int UlpiRdWrMask = (1 << 29);
        private const int UlpiRegAddrOffset = 16;
        private const int UlpiRegAddrMask = 0xFF << UlpiRegAddrOffset;
        private const int UlpiDataWriteMask = 0xFF;
        private const int UlpiDataReadOffset = 8;
        private const int UlpiSyncStateMask = (1 << 27);
        private const uint StatusActive = 1u << 7;
        private const uint hcsparamsNPortsMask = 0xf;
        private const uint hciVersion = 0x0100; //hci version (16 bit BCD)

        private const int EhciHciVersionMask = 0xffff;
        private const int EhciHciVersionOffset = 16;
        private const int EhciCapabilityRegistersLengthMask = 0xff;
        private const int EhciPortStatusControlRegisterOffset = 0x44;
        private const int EhciPortStatusControlRegisterWidth = 0x4;

        private const uint EhciNumberOfEndpoints = 0x10;

        private class ReusableIdentifiedList<T> where T: class
        {
            public ReusableIdentifiedList()
            {
                internalList = new List<T>();
            }

            public int Add(T element)
            {
                var firstEmpty = internalList.IndexOf(null);
                if(firstEmpty != -1)
                {
                    internalList[firstEmpty] = element;
                    return firstEmpty;
                }

                internalList.Add(element);
                return internalList.Count;
            }

            public void Remove(T element)
            {
                var index = internalList.IndexOf(element);
                if(index == -1)
                {
                    throw new ArgumentException();
                }

                internalList[index] = null;
            }

            public void RemoveAt(int index)
            {
                internalList[index] = null;
            }

            private readonly List<T> internalList;
        }

        private class QueueTransferDescriptor
        {
            public QueueTransferDescriptor(IBusController bus)
            {
                systemBus = bus;
                Buffer = new uint[5];
            }

            //future 64bit data structures support
            public void Fetch(ulong address)
            {
                //store address
                memoryAddress = address;

                //get data from memory
                PhysNext = systemBus.ReadDoubleWord(address);
                PhysAlternativeNext = systemBus.ReadDoubleWord(address + 0x04);
                Token = systemBus.ReadDoubleWord(address + 0x08);

                for(var i = 0u; i < Buffer.Length; i++)
                {
                    Buffer[i] = systemBus.ReadDoubleWord(address + 0x0C + i * 4);
                }

                //extract fields
                Next = PhysNext & ~(0x1fu);
                NextTerminate = ((PhysNext & 0x01) != 0);
                AlternativeNext = PhysAlternativeNext & ~(0x1fu);
                AlternativeNextTerminate = ((PhysAlternativeNext & 0x01) != 0);
                DataToggle = ((Token & (1u << 31)) != 0);
                TotalBytesToTransfer = (Token >> 16) & 0x7fffu;
                InterruptOnComplete = ((Token & (1u << 15)) != 0);
                CurrentPage = (byte)((Token >> 12) & 0x07u);
                ErrorCount = (byte)((Token >> 10) & 0x03u);
                PID = (PIDCode)((Token >> 8) & 0x03u);
                Status = (byte)(Token & 0xffu);
                CurrentOffset = Buffer[0] & 0x0fffu;
                Buffer[0] &= ~(0x0fffu);
            }

            public ulong FetchNext()
            {
                if(!NextTerminate) //if next one is normal td
                {
                    memoryAddress = Next;
                    Fetch(Next);
                    return memoryAddress;
                }
                if(!AlternativeNextTerminate) //if next one is short packet
                {
                    memoryAddress = AlternativeNext;
                    Fetch(AlternativeNext);
                    return memoryAddress;
                }
                return 0;
            }

            public void UpdateMemory()
            {
                //consolidate fields
                PhysNext = Next | (NextTerminate ? 0x01u : 0x00u);
                PhysAlternativeNext = AlternativeNext | (AlternativeNextTerminate ? 0x01u : 0x00u);
                Token = (DataToggle ? 0 : (1u << 31)) | (TotalBytesToTransfer << 16)
                    | (InterruptOnComplete ? (1u << 15) : 0) | (uint)(CurrentPage << 12)
                    | (uint)(ErrorCount << 10) | ((uint)PID << 8) | (uint)Status;

                //write memory
                systemBus.WriteDoubleWord(memoryAddress, PhysNext);
                systemBus.WriteDoubleWord(memoryAddress + 0x04, PhysAlternativeNext | 0x08);
                systemBus.WriteDoubleWord(memoryAddress + 0x08, Token);
                systemBus.WriteDoubleWord(memoryAddress + 0x0C, Buffer[0] | CurrentOffset);
                for(var i = 1u; i < Buffer.Length; i++)
                {
                    systemBus.WriteDoubleWord(memoryAddress + 0x0C + i * 4, Buffer[i]);
                }
            }

            public void UpdateFromOverlay(QueueHeadOverlay overlay)
            {
                Next = overlay.NextPointer;
                NextTerminate = overlay.NextPointerTerminate;
                AlternativeNext = overlay.AlternateNextPointer;
                AlternativeNextTerminate = overlay.AlternateNextPointerTerminate;
                TotalBytesToTransfer = overlay.TotalBytesToTransfer;
                InterruptOnComplete = overlay.InterruptOnComplete;
                CurrentPage = overlay.CurrentPage;
                PID = overlay.PID;
                Status = overlay.Status;
                CurrentOffset = overlay.CurrentOffset;
                for(int i = 0; i < Buffer.Length; i++)
                {
                    Buffer[i] = overlay.BufferPointer[i];
                }
            }

            public void Processed()
            {
                Status &= 0x7f;
            }

            public uint PhysAlternativeNext { get; private set; }
            public byte ErrorCount { get; private set; }
            public byte Status { get; private set; }
            public uint CurrentOffset { get; private set; }
            public uint PhysNext { get; private set; }
            public bool AlternativeNextTerminate { get; private set; }
            public uint AlternativeNext { get; private set; }
            public bool NextTerminate { get; private set; }
            public uint Token { get; private set; }
            public uint Next { get; private set; }
            public bool DataToggle { get; private set; }
            public bool InterruptOnComplete { get; private set; }
            public byte CurrentPage { get; private set; }
            public PIDCode PID { get; private set; }
            public uint[] Buffer { get; private set; }

            public uint TotalBytesToTransfer { get; set; }

            private IBusController systemBus;
            private ulong memoryAddress;
        }

        private class InterruptEnable
        {
            public void Clear()
            {
                OnAsyncAdvanceEnable = false;
                HostSystemErrorEnable = false;
                FrameListRolloverEnable = false;
                PortChangeEnable = false;
                USBErrorEnable = false;
                Enable = false;
            }

            public bool OnAsyncAdvanceEnable { get; set; }
            public bool HostSystemErrorEnable { get; set; }
            public bool FrameListRolloverEnable { get; set; }
            public bool PortChangeEnable { get; set; }
            public bool USBErrorEnable { get; set; }
            public bool Enable { get; set; }

            public uint Value
            {
                get
                {
                    return (uint)(OnAsyncAdvanceEnable ? 1 : 0) << 5
                        | (uint)(HostSystemErrorEnable ? 1 : 0) << 4
                        | (uint)(FrameListRolloverEnable ? 1 : 0) << 3
                        | (uint)(PortChangeEnable ? 1 : 0) << 2
                        | (uint)(USBErrorEnable ? 1 : 0) << 1
                        | (uint)(Enable ? 1 : 0) << 0;
                }
            }
        }

        //
        // TODO:
        // qh is spread over < address , address + 0x30 )
        // all the check stuff should be redone not to read @ address multiple times
        //
        private class QueueHead
        {
            public QueueHead(IBusController bus)
            {
                Overlay = new QueueHeadOverlay(bus);
                TransferDescriptor = new QueueTransferDescriptor(bus);
                systemBus = bus;
            }

            public void Fetch()
            {
                link = systemBus.ReadDoubleWord(Address);
                staticEndpointState1 = systemBus.ReadDoubleWord(Address + 0x04);
                staticEndpointState2 = systemBus.ReadDoubleWord(Address + 0x08);
                CurrentTransferDescriptor = (uint)Address + 0x10u;

                linkPointer = (uint)(link & ~(0x1f));
                type = (byte)((link & 0x6) >> 1);
                terminate = ((link & 0x1) != 0);

                DataToggleControl = ((staticEndpointState1 & 0x4000) != 0);
                EndpointSpeed = (EndpointSpeed)((staticEndpointState1 & 0x3000) >> 12);
                EndpointNumber = (byte)((staticEndpointState1 & 0xf00) >> 8);
                DeviceAddress = (byte)(staticEndpointState1 & 0x7f);

                PortNumber = (byte)((staticEndpointState2 & 0x3F800000) >> 23);
                HubAddress = (byte)((staticEndpointState2 & 0x7F0000) >> 16);
                TransferDescriptor.Fetch(CurrentTransferDescriptor);
                Overlay.FillWithTransferDescriptor(TransferDescriptor, this);
            }

            public bool GoToNextLink()
            {
                var val = (uint)(systemBus.ReadDoubleWord(Address) & ~(0x1f));
                if((val != Address) && (val != 0))
                {
                    Address = val;
                    return true;
                }
                return false;
            }

            public void Advance()
            {
                CurrentTransferDescriptor = (uint)TransferDescriptor.FetchNext(); // get next transfer descriptor
                Overlay.FillWithTransferDescriptor(TransferDescriptor, this);   // fill transfer descriptor with data from overlay
            }

            public void WriteBackTransferDescriptor()
            {
                TransferDescriptor.UpdateFromOverlay(Overlay);
                TransferDescriptor.UpdateMemory();
            }

            public void Processed()
            {
                Overlay.Processed();
                TransferDescriptor.Processed();
            }

            public void StaticMemoryUpdate()
            {
                link = linkPointer | ((uint)type << 1) | (terminate ? 1u : 0u);

                //update RAM
                systemBus.WriteDoubleWord(Address + 0x0C, CurrentTransferDescriptor);
                systemBus.WriteDoubleWord(Address + 0x10, TransferDescriptor.PhysNext);
                systemBus.WriteDoubleWord(Address + 0x14, TransferDescriptor.PhysAlternativeNext | 0x08);
                systemBus.WriteDoubleWord(Address + 0x18, TransferDescriptor.Token);
                systemBus.WriteDoubleWord(Address + 0x1c, TransferDescriptor.Buffer[0] | TransferDescriptor.CurrentOffset);
                systemBus.WriteDoubleWord(Address + 0x20, TransferDescriptor.Buffer[1]);
                systemBus.WriteDoubleWord(Address + 0x24, TransferDescriptor.Buffer[2]);
                systemBus.WriteDoubleWord(Address + 0x28, TransferDescriptor.Buffer[3]);
                systemBus.WriteDoubleWord(Address + 0x2c, TransferDescriptor.Buffer[4]);
            }

            public void UpdateTotalBytesToTransfer(uint trasferredBytes)
            {
                Overlay.TotalBytesToTransfer -= trasferredBytes;
                TransferDescriptor.TotalBytesToTransfer -= trasferredBytes;
            }

            public ulong Address { get; set; }

            public bool IsValid { get { return ((systemBus.ReadDoubleWord(Address) & 0x1) == 0); } }

            public byte ElementName { get { return (byte)((systemBus.ReadDoubleWord(Address) & 0x6) >> 1); } }

            public QueueHeadOverlay Overlay { get; private set; }

            public QueueTransferDescriptor TransferDescriptor { get; private set; }

            public bool DataToggleControl { get; private set; }

            public EndpointSpeed EndpointSpeed { get; private set; }

            public byte DeviceAddress { get; private set; }

            public byte HubAddress { get; private set; }

            public byte PortNumber { get; private set; }

            public byte EndpointNumber { get; private set; }

            public uint CurrentTransferDescriptor { get; private set; }

            private readonly IBusController systemBus;
            private uint linkPointer;
            private byte type;
            private bool terminate;
            private uint link;
            private uint staticEndpointState1;
            private uint staticEndpointState2;
        }

        private class QueueHeadOverlay
        {
            public QueueHeadOverlay(IBusController bus)
            {
                systemBus = bus;
                BufferPointer = new uint[5];
            }

            public void FillWithTransferDescriptor(QueueTransferDescriptor td, QueueHead qh)
            {
                memoryAddress = qh.CurrentTransferDescriptor;
                NextPointer = td.Next;
                NextPointerTerminate = td.NextTerminate;
                AlternateNextPointer = td.AlternativeNext;
                AlternateNextPointerTerminate = td.AlternativeNextTerminate;
                TotalBytesToTransfer = td.TotalBytesToTransfer;
                InterruptOnComplete = td.InterruptOnComplete;
                CurrentPage = td.CurrentPage;
                errorCount = td.ErrorCount;
                PID = td.PID;
                token = td.Token;

                CurrentOffset = td.CurrentOffset;
                Array.Copy(td.Buffer, BufferPointer, BufferPointer.Length);

                if(qh.DataToggleControl)
                {
                    dataToggle = td.DataToggle;
                }

                Status = (qh.EndpointSpeed == EndpointSpeed.HighSpeed) ?
                    (byte)((td.Status & (byte)0xFE) | (Status & (byte)0x01)) //preserve old ping bit
                    : td.Status;
            }

            public void UpdateMemory()
            {
                //consolidate fields
                physNext = NextPointer | (NextPointerTerminate ? 0x01u : 0x00u);
                physAlternatNext = AlternateNextPointer | (AlternateNextPointerTerminate ? 0x01u : 0x00u);
                token = (dataToggle ? (1u << 31) : 0) | (TotalBytesToTransfer << 16)
                    | (InterruptOnComplete ? (1u << 15) : 0) | (uint)(CurrentPage << 12)
                    | (uint)(errorCount << 10) | ((uint)PID << 8) | (uint)Status;
                CurrentOffset += TotalBytesToTransfer;

                //write memory
                systemBus.WriteDoubleWord(memoryAddress, physNext);
                systemBus.WriteDoubleWord(memoryAddress + 0x04, physAlternatNext);
                systemBus.WriteDoubleWord(memoryAddress + 0x08, token);
                systemBus.WriteDoubleWord(memoryAddress + 0x0C, BufferPointer[0] | CurrentOffset);
                systemBus.WriteDoubleWord(memoryAddress + 0x10, BufferPointer[1]);
                systemBus.WriteDoubleWord(memoryAddress + 0x14, BufferPointer[2]);
                for(var i = 3u; i < BufferPointer.Length; i++)
                {
                    systemBus.WriteDoubleWord(memoryAddress + 0x0C + i * 4, BufferPointer[i]);
                }
            }

            public void Processed()
            {
                Status &= 0x7f;
            }

            public uint NextPointer { get; private set; }
            public bool NextPointerTerminate { get; private set; }
            public uint AlternateNextPointer { get; private set; }
            public bool AlternateNextPointerTerminate { get; private set; }
            public uint TotalBytesToTransfer { get; set; }
            public bool InterruptOnComplete { get; private set; }
            public byte CurrentPage { get; private set; }
            public PIDCode PID { get; private set; }
            public byte Status { get; private set; }
            public uint[] BufferPointer { get; private set; }
            public uint CurrentOffset { get; private set; }

            private readonly IBusController systemBus;
            private ulong memoryAddress;
            private bool dataToggle;
            private byte errorCount;
            private uint physNext;
            private uint physAlternatNext;
            private uint token;
        }

        private enum EndpointSpeed
        {
            FullSpeed = 0, // 12 Mbs
            LowSpeed = 1,  // 1.5 Mbs
            HighSpeed = 2, // 480 Mbs
            Reserved = 3
        }

        private enum InterruptMask : uint
        {
            InterruptOnAsyncAdvance = (uint)(1 << 5),
            HostSystemError = (uint)(1 << 4),
            FrameListRollover = (uint)(1 << 3),
            PortChange = (uint)(1 << 2),
            USBError = (uint)(1 << 1),
            USBInterrupt = (uint)(1 << 0)
        }

        private enum EhciUsbCommandMask : uint
        {
            InterruptOnAsyncAdvanceDoorbell = (1 << 6),
            AsynchronousScheduleEnable = (1 << 5),
            PeriodicScheduleEnable = (1 << 4),
            HostControllerReset = (1 << 1),
            RunStop = 1
        }

        private enum EhciUsbStatusMask : uint
        {
            AsynchronousScheduleStatus = (1 << 15),
            PeriodicScheduleStatus = (1 << 14),
            Reclamation = (1 << 13),
            HCHalted = (1 << 12),
            InterruptOnAsyncAdvance = (1 << 5)
        }

        private enum ControllerMode
        {
            Idle,
            Reserved,
            DeviceMode,
            HostMode
        }

        private enum CapabilityRegisters : uint
        {
            CapabilityRegistersLength = 0x0,
            HCIVersionNumber = 0x2,
            StructuralParameters = 0x04,
            CompanionPortRouteDescription = 0x0C,
        }

        private enum OperationalRegisters : uint
        {
            UsbCommand = 0x0,
            UsbStatus = 0x04,
            UsbInterruptEnable = 0x08,
            UsbFrameIndex = 0x0C,
            PeriodicListBaseAddress = 0x14, /* Frame List Base Address */
            AsyncListAddress = 0x18,
            ConfiguredFlag = 0x40,
        }

        //TODO: this looks like Tegra stuff, not generic EHCI
        private enum OtherRegisters : uint
        {
            UsbDCCParams = 0x124,
            UsbMode = 0x1A8,
        }

        private enum PIDCode
        {
            Out = 0,
            In = 1,
            Setup = 2
        }

        private enum DataDirection
        {
            HostToDevice = 0,
            DeviceToHost = 1
        }

        private enum DeviceRequestType
        {
            GetStatus = 0,
            ClearFeature = 1,
            SetFeature = 3,
            SetAddress = 5,
            GetDescriptor = 6,
            SetDescriptor = 7,
            GetConfiguration = 8,
            SetConfiguration = 9,
            GetInterface = 10,
            SetInterFace = 11,
            SynchFrame = 12
        }
    }

    public static class LongExtensions
    {
        public static bool InRange(this long value, long baseValue, uint length, out long shift)
        {
            shift = value - baseValue;
            return (shift >= 0 && shift < length);
        }
    }
}
