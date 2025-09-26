//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.PCI;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    [Icon("usb")]
    public class ISP1761 : IDoubleWordPeripheral, IPeripheralRegister<IUSBHub, USBRegistrationPoint>, IPeripheralContainer<IUSBPeripheral, USBRegistrationPoint>, IPCIPeripheral
    {
        public ISP1761(IMachine machine)
        {
            // pci-specific info.
            pci_info = new PCIInfo(0x5406, 0x10b5, 0x9054, 0x10b5, 0x680);
            pci_info.BarLen[0] = 0x10000;
            pci_info.BarLen[3] = 0x10000;

            for(int i = 0; i < 32; i++)
            {
                ptd[i] = new PTD();
                ptdi[i] = new PTD();
            }
            intDoneMap = 0x00000000;
            intSkipMap = 0xFFFFFFFF;
            atlSkipMap = 0xFFFFFFFF;
            atlDoneMap = 0x00000000;
            atlIRQMaskOR = 0x0000000;
            swReset = 0x00000000;
            memoryReg = 0x0000000;
            this.machine = machine;
            interr = 0;
            IRQ = new GPIO();
            this.machine = machine;
            registeredDevices = new Dictionary<byte, IUSBPeripheral>();
            adressedDevices = new Dictionary<byte, IUSBPeripheral>();
            registeredHubs = new Dictionary<byte, IUSBHub>();

            PortSc = new PortStatusAndControlRegister[1]; //port status control
            for(int i = 0; i < PortSc.Length; i++)
            {
                PortSc[i] = new PortStatusAndControlRegister();
            }
            SetupData = new USBSetupPacket();

            SoftReset();//soft reset must be done before attaching devices
        }

        public void DoneInt(int p)
        {
            intDoneMap |= (uint)(1 << p);
            intIRQMaskOR |= (uint)(1 << p);
        }

        public void ProcessPacket(uint addr)
        {
            lock(thisLock)
            {
                PTDheader ptdh = new PTDheader();

                for(int p = 0; p < 32; p++)
                    if((atlSkipMap & (1 << p)) == 0)
                    {
                        if((1 << p) == atlLastPTD)
                        {
                            break;
                        }
                        ptdh.V = (ptd[p].DW0) & 0x1;
                        ptdh.NrBytesToTransfer = (ptd[p].DW0 >> 3) & 0x7fff;
                        ptdh.MaxPacketLength = (ptd[p].DW0 >> 18) & 0x7ff;
                        ptdh.Mult = (ptd[p].DW0 >> 29) & 0x2;
                        ptdh.EndPt = (((ptd[p].DW1) & 0x7) << 1) | (ptd[p].DW0 >> 31);
                        ptdh.DeviceAddress = (byte)((ptd[p].DW1 >> 3) & 0x7f);
                        ptdh.Token = (ptd[p].DW1 >> 10) & 0x3;
                        ptdh.EPType = (ptd[p].DW1 >> 12) & 0x3;
                        ptdh.S = (ptd[p].DW1 >> 14) & 0x1;
                        ptdh.SE = (ptd[p].DW1 >> 16) & 0x3;
                        ptdh.PortNumber = (byte)((ptd[p].DW1 >> 18) & 0x7f);
                        ptdh.HubAddress = (byte)((ptd[p].DW1 >> 25) & 0x7f);
                        ptdh.DataStartAddress = (((ptd[p].DW2 >> 8) & 0xffff) << 3) + 0x400;
                        ptdh.RL = (ptd[p].DW2 >> 25) & 0xf;
                        ptdh.NrBytesTransferred = (ptd[p].DW3) & 0x7fff;
                        ptdh.NakCnt = (ptd[p].DW3 >> 19) & 0xf;
                        ptdh.Cerr = (ptd[p].DW3 >> 23) & 0x3;
                        ptdh.DT = (ptd[p].DW3 >> 25) & 0x1;
                        ptdh.SC = (ptd[p].DW3 >> 27) & 0x1;
                        ptdh.X = (ptd[p].DW3 >> 28) & 0x1;
                        ptdh.B = (ptd[p].DW3 >> 29) & 0x1;
                        ptdh.H = (ptd[p].DW3 >> 30) & 0x1;
                        ptdh.A = (ptd[p].DW3 >> 31) & 0x1;
                        ptdh.NextPTDPointer = (ptd[p].DW4) & 0x1F;
                        ptdh.J = (ptd[p].DW4 >> 5) & 0x1;
                        if(ptdh.V != 0)
                        {
                            /* Process Packet */
                            ProcessPacket(ptdh);
                            /* Set packet done bits */
                            if(ptdh.A == 0)
                            {
                                ptd[p].DW3 = (uint)(((ptd[p].DW3 | ((((0 >> 3) & 0x7fff) + ptdh.NrBytesTransferred) & 0x7fff) << 0) & 0x7fffffff));
                                ptd[p].DW3 = ptd[p].DW3 | (ptdh.B << 29);
                                ptd[p].DW3 = ptd[p].DW3 | (ptdh.H << 30);
                                ptd[p].DW0 = ptd[p].DW0 & 0xfffffffe;
                                //  ptd[p].DW3 = (ptd[p].DW3&0xff87ffff) | (PTDh.NakCnt<<19);
                                Done(p);
                            }
                        }
                    }
                if(atlDoneMap != 0)
                {
                    if((InterruptEnableRegister.OnAsyncAdvanceEnable == true) & (InterruptEnableRegister.Enable == true))
                    {
                        UsbSts |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.InterruptOnAsyncAdvance; //raise flags in status register
                        interr |= 1 << 8;
                        IRQ.Set(true); //raise interrupt
                    }
                }
            }
        }

        public void ProcessPacket(PTDheader ptdh)
        {
            IUSBPeripheral targetDevice;
            if(ptdh.DeviceAddress != 0)
            {
                targetDevice = this.FindDeviceInternal(ptdh.DeviceAddress);
            }
            else
            {
                targetDevice = this.FindDeviceByPortInternal(ptdh.PortNumber);
                targetDevice = ActiveDevice;
            }
            if(targetDevice == null)
            {
                return;
            }
            if(ptdh.V != 0)//if transfer descriptor active
            {
                USBPacket packet;
                packet.BytesToTransfer = ptdh.NrBytesToTransfer;
                switch((PIDCode)ptdh.Token)
                {
                case PIDCode.Setup://if setup command
                    this.Log(LogLevel.Noisy, "Setup");
                    this.Log(LogLevel.Noisy, "Device {0:d}", ptdh.DeviceAddress);

                    SetupData.RequestType = payLoad[ptdh.DataStartAddress];
                    SetupData.Request = payLoad[ptdh.DataStartAddress + 1];
                    SetupData.Value = BitConverter.ToUInt16(payLoad, (int)(ptdh.DataStartAddress + 2));
                    SetupData.Index = BitConverter.ToUInt16(payLoad, (int)(ptdh.DataStartAddress + 4));
                    SetupData.Length = BitConverter.ToUInt16(payLoad, (int)(ptdh.DataStartAddress + 6));
                    packet.Ep = (byte)ptdh.EndPt;
                    packet.Data = null;

                    if(((SetupData.RequestType & 0x80u) >> 7) == (uint)DataDirection.DeviceToHost)//if device to host transfer
                    {
                        if(((SetupData.RequestType & 0x60u) >> 5) == (uint)USBRequestType.Standard)
                        {
                            if(ptdh.DeviceAddress == 3)
                            {
                                this.Log(LogLevel.Warning, "Setup");
                            }

                            switch((DeviceRequestType)SetupData.Request)
                            {
                            case DeviceRequestType.GetDescriptor:
                                targetDevice.GetDescriptor(packet, SetupData);
                                break;
                            case DeviceRequestType.GetConfiguration:
                                targetDevice.GetConfiguration();
                                break;
                            case DeviceRequestType.GetInterface:
                                targetDevice.GetInterface(packet, SetupData);
                                break;
                            case DeviceRequestType.GetStatus:
                                targetDevice.GetStatus(packet, SetupData);
                                break;
                            default:
                                targetDevice.GetDescriptor(packet, SetupData);
                                this.Log(LogLevel.Warning, "Unsupported device request1");
                                break;
                            }//end of switch request
                        }
                        else if(((SetupData.RequestType & 0x60u) >> 5) == (uint)USBRequestType.Class)
                        {
                            targetDevice.ProcessClassGet(packet, SetupData);
                        }
                        else if(((SetupData.RequestType & 0x60u) >> 5) == (uint)USBRequestType.Vendor)
                        {
                            targetDevice.ProcessVendorGet(packet, SetupData);
                        }
                    }
                    else//if host to device transfer
                        if(((SetupData.RequestType & 0x60) >> 5) == (uint)USBRequestType.Standard)
                    {
                        switch((DeviceRequestType)SetupData.Request)
                        {
                        case DeviceRequestType.SetAddress:
                            targetDevice.SetAddress(SetupData.Value);
                            targetDevice.SendInterrupt += ProcessINT;
                            targetDevice.SendPacket += ProcessPacket;
                            this.AddressDevice(targetDevice, (byte)SetupData.Value);
                            counter++;
                            break;
                        case DeviceRequestType.SetDescriptor:
                            targetDevice.GetDescriptor(packet, SetupData);

                            break;
                        case DeviceRequestType.SetFeature:
                            targetDevice.GetDescriptor(packet, SetupData);

                            break;
                        case DeviceRequestType.SetInterFace:
                            targetDevice.SetInterface(packet, SetupData);

                            break;
                        case DeviceRequestType.SetConfiguration:

                            targetDevice.SetConfiguration(packet, SetupData);

                            break;

                        default:
                            this.Log(LogLevel.Warning, "Unsupported device request2");
                            break;
                        }//end of switch request
                    }//end of request type.standard
                    else

                    if((SetupData.RequestType >> 5) == (uint)USBRequestType.Class)
                    {
                        targetDevice.ProcessClassSet(packet, SetupData);
                    }
                    else if((SetupData.RequestType >> 5) == (uint)USBRequestType.Vendor)
                    {
                        targetDevice.ProcessVendorSet(packet, SetupData);
                    }

                    ptdh.Transferred((uint)ptdh.NrBytesToTransfer);
                    ptdh.Done();

                    break;

                case PIDCode.Out://data transfer from host to device
                {
                    uint dataAmount;

                    dataAmount = ptdh.NrBytesToTransfer;

                    if(dataAmount > 0)
                    {
                        byte[] tdData = new byte[dataAmount];
                        Array.Copy(payLoad, ptdh.DataStartAddress, tdData, 0, dataAmount);

                        if(ptdh.EPType == 0)
                        {
                            packet.Ep = (byte)ptdh.EndPt;
                            packet.Data = tdData;
                            targetDevice.GetDescriptor(packet, SetupData);
                        }
                        else
                        {
                            packet.Data = tdData;
                            packet.Ep = (byte)ptdh.EndPt;
                            targetDevice.WriteDataBulk(packet);
                        }
                    }
                    else
                    {
                        packet.Data = null;
                        packet.Ep = (byte)ptdh.EndPt;

                        targetDevice.WriteDataBulk(packet);
                    }

                    ptdh.Transferred(dataAmount);
                    ptdh.Done();
                }
                break;
                case PIDCode.In://data transfer from device to host
                {
                    if(ptdh.NrBytesToTransfer > 0)
                    {
                        byte[] inData = null;

                        byte[] buff = new byte[ptdh.NrBytesToTransfer];

                        if(ptdh.EPType == 0)
                        {
                            packet.Data = buff;
                            packet.Ep = (byte)ptdh.EndPt;
                            inData = targetDevice.GetDataControl(packet);
                        }
                        else
                        {
                            packet.Data = buff;
                            packet.Ep = (byte)ptdh.EndPt;
                            inData = targetDevice.GetDataBulk(packet);
                            if(inData == null)
                                return;
                        }

                        if(inData != null)
                        {
                            if(ptdh.NrBytesToTransfer > 0)
                            {
                                Array.Copy(inData, 0, payLoad, ptdh.DataStartAddress, inData.Length);
                                ptdh.Transferred((uint)inData.Length);
                            }
                        }
                    }
                    else
                    {
                        if(ptdh.EPType == 0)
                        {
                            packet.Data = null;
                            packet.Ep = (byte)ptdh.EndPt;
                            targetDevice.GetDataControl(packet);
                        }
                        else
                        {
                            packet.Data = null;
                            packet.Ep = (byte)ptdh.EndPt;
                            targetDevice.GetDataBulk(packet);
                        }
                    }

                    ptdh.Done();
                }
                break;

                default:
                    this.Log(LogLevel.Warning, "Unkonwn PID");
                    break;
                }
            }
            else
            {
                this.Log(LogLevel.Info, "Inactive transfed descriptor not processing at this point");
            }
        }

        public void ProcessPacketInt(PTDheader ptdh)
        {
            USBPacket packet;
            packet.BytesToTransfer = ptdh.NrBytesToTransfer;
            IUSBPeripheral targetDevice;

            if(ptdh.DeviceAddress != 0)
            {
                targetDevice = this.FindDeviceInternal(ptdh.DeviceAddress);
            }
            else
            {
                targetDevice = this.FindDeviceByPortInternal(ptdh.PortNumber);
                targetDevice = ActiveDevice;
            }
            if(targetDevice == null)
                return;

            if(ptdh.V != 0)//if transfer descriptor active
            {
                switch((PIDCode)ptdh.Token)
                {
                case PIDCode.In://data transfer from device to host
                {
                    if(ptdh.NrBytesToTransfer > 0)
                    {
                        byte[] inData = null;

                        byte[] buff = new byte[ptdh.NrBytesToTransfer];

                        if(ptdh.EPType == 3)
                        {
                            Array.Copy(payLoad, ptdh.DataStartAddress, buff, 0, ptdh.NrBytesToTransfer);
                            packet.Data = buff;
                            packet.Ep = (byte)ptdh.EndPt;
                            inData = targetDevice.WriteInterrupt(packet);//targetDevice.WriteInterrupt(packet);
                        }

                        if(inData != null)
                        {
                            Array.Copy(inData, 0, payLoad, ptdh.DataStartAddress, inData.Length);
                            ptdh.Transferred((uint)inData.Length);
                            ptdh.Done();
                        }
                    }
                    else
                    {
                        packet.Data = null;
                        packet.Ep = (byte)ptdh.EndPt;

                        if(ptdh.EPType == 0)
                        {
                            targetDevice.GetDataControl(packet);
                        }
                        else
                        {
                            targetDevice.GetDataBulk(packet);
                        }
                    }
                    if(targetDevice.GetTransferStatus() == 6)
                    {
                        ptdh.Bubble();
                        ptdh.Done();
                    }
                    if(targetDevice.GetTransferStatus() == 4)
                    {
                        ptdh.Stalled();
                        ptdh.Done();
                    }
                }
                break;

                default:
                    this.Log(LogLevel.Warning, "Unkonwn PID");
                    break;
                }
            }
        }

        public void WriteDoubleWord(long address, uint value)
        {
            if(address >= (uint)0x64 && address < (uint)0x130)
            {
                uint portNumber = (uint)(address - (uint)Offset.PortStatusControl) / 4;

                PortStatusAndControlRegisterChanges change = PortSc[portNumber].SetValue(value);
                if(change.ConnectChange == true)
                {
                    UsbSts |= (uint)InterruptMask.PortChange;
                }

                if((InterruptEnableRegister.Enable == true) && (InterruptEnableRegister.PortChangeEnable == true))
                {
                    UsbSts |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.PortChange; //raise flags in status register
                    interr |= 1 << 8;
                    IRQ.Set(true); //raise interrupt
                }
            }
            else if(address >= (uint)0x800 && address < (uint)0xbff)
            {
                if(((address - 0x800) / 4) % 8 == 0)
                {
                    ptdi[(address - 0x800) / 32].DW0 = value;

                    PTDheader ptdh = new PTDheader();
                    ptdh.V = (ptdi[(address - 0x800) / 32].DW0) & 0x1;
                    ptdh.NrBytesToTransfer = (ptdi[(address - 0x800) / 32].DW0 >> 3) & 0x7fff;
                    ptdh.MaxPacketLength = (ptdi[(address - 0x800) / 32].DW0 >> 18) & 0x7ff;
                    ptdh.Mult = (ptdi[(address - 0x800) / 32].DW0 >> 29) & 0x2;
                    ptdh.EndPt = (((ptdi[(address - 0x800) / 32].DW1) & 0x7) << 1) | (ptdi[(address - 0x800) / 32].DW0 >> 31);
                    ptdh.DeviceAddress = (byte)((ptdi[(address - 0x800) / 32].DW1 >> 3) & 0x7f);
                    ptdh.Token = (ptdi[(address - 0x800) / 32].DW1 >> 10) & 0x3;
                    ptdh.EPType = (ptdi[(address - 0x800) / 32].DW1 >> 12) & 0x3;
                    ptdh.S = (ptdi[(address - 0x800) / 32].DW1 >> 14) & 0x1;
                    ptdh.SE = (ptdi[(address - 0x800) / 32].DW1 >> 16) & 0x3;
                    ptdh.PortNumber = (byte)((ptdi[(address - 0x800) / 32].DW1 >> 18) & 0x7f);
                    ptdh.HubAddress = (byte)((ptdi[(address - 0x800) / 32].DW1 >> 25) & 0x7f);
                    ptdh.DataStartAddress = (ptdi[(address - 0x800) / 32].DW2 >> 8) & 0xffff;
                    ptdh.RL = (ptdi[(address - 0x800) / 32].DW2 >> 25) & 0xf;
                    ptdh.NrBytesTransferred = (ptdi[(address - 0x800) / 32].DW3) & 0x7fff;
                    ptdh.NakCnt = (ptdi[(address - 0x800) / 32].DW3 >> 19) & 0xf;
                    ptdh.Cerr = (ptdi[(address - 0x800) / 32].DW3 >> 23) & 0x3;
                    ptdh.DT = (ptdi[(address - 0x800) / 32].DW3 >> 25) & 0x1;
                    ptdh.SC = (ptdi[(address - 0x800) / 32].DW3 >> 27) & 0x1;
                    ptdh.X = (ptdi[(address - 0x800) / 32].DW3 >> 28) & 0x1;
                    ptdh.B = (ptdi[(address - 0x800) / 32].DW3 >> 29) & 0x1;
                    ptdh.H = (ptdi[(address - 0x800) / 32].DW3 >> 30) & 0x1;
                    ptdh.A = (ptdi[(address - 0x800) / 32].DW3 >> 31) & 0x1;
                    ptdh.NextPTDPointer = (ptdi[(address - 0x800) / 32].DW4) & 0x1F;
                    ptdh.J = (ptdi[(address - 0x800) / 32].DW4 >> 5) & 0x1;
                    if(ptdh.V == 1)
                    {
                        {
                            this.Log(LogLevel.Noisy, "REG---------------------------");
                            this.Log(LogLevel.Noisy, "V=0{0:X}", ptdh.V);
                            this.Log(LogLevel.Noisy, "NrBytesToTransfer=0{0:X}", ptdh.NrBytesToTransfer);
                            this.Log(LogLevel.Noisy, "MaxPacketLength=0{0:X}", ptdh.MaxPacketLength);
                            this.Log(LogLevel.Noisy, "Mult=0{0:X}", ptdh.Mult);
                            this.Log(LogLevel.Noisy, "EndPt=0{0:X}", ptdh.EndPt);
                            this.Log(LogLevel.Noisy, "DeviceAddress=0{0:X}", ptdh.DeviceAddress);
                            this.Log(LogLevel.Noisy, "Token=0{0:X}", ptdh.Token);
                            this.Log(LogLevel.Noisy, "EPType=0{0:X}", ptdh.EPType);
                            this.Log(LogLevel.Noisy, "S=0{0:X}", ptdh.S);
                            this.Log(LogLevel.Noisy, "SE=0{0:X}", ptdh.SE);
                            this.Log(LogLevel.Noisy, "PortNumber=0{0:X}", ptdh.PortNumber);
                            this.Log(LogLevel.Noisy, "HubAddress =0{0:X}", ptdh.HubAddress);
                            this.Log(LogLevel.Noisy, "DataStartAddress=0{0:X}", ptdh.DataStartAddress);
                            this.Log(LogLevel.Noisy, "RL=0{0:X}", ptdh.RL);
                            this.Log(LogLevel.Noisy, "NrBytesTransferred=0{0:X}", ptdh.NrBytesTransferred);
                            this.Log(LogLevel.Noisy, "NakCnt=0{0:X}", ptdh.NakCnt);
                            this.Log(LogLevel.Noisy, "Cerr =0{0:X}", ptdh.Cerr);
                            this.Log(LogLevel.Noisy, "DT=0{0:X}", ptdh.DT);
                            this.Log(LogLevel.Noisy, "SC=0{0:X}", ptdh.SC);
                            this.Log(LogLevel.Noisy, "X=0{0:X}", ptdh.X);
                            this.Log(LogLevel.Noisy, "B=0{0:X}", ptdh.B);
                            this.Log(LogLevel.Noisy, "H =0{0:X}", ptdh.H);
                            this.Log(LogLevel.Noisy, "A=0{0:X}", ptdh.A);
                            this.Log(LogLevel.Noisy, "NextPTDPointer =0{0:X}", ptdh.NextPTDPointer);
                            this.Log(LogLevel.Noisy, "J=0{0:X}", ptdh.J);
                        }
                        ProcessINT(ptdh.DeviceAddress);
                    }
                }

                if(((address - 0x800) / 4) % 8 == 1)
                {
                    ptdi[(address - 0x800) / 32].DW1 = value;
                }
                if(((address - 0x800) / 4) % 8 == 2)
                {
                    ptdi[(address - 0x800) / 32].DW2 = value;
                }
                if(((address - 0x800) / 4) % 8 == 3)
                {
                    ptdi[(address - 0x800) / 32].DW3 = value;
                }
                if(((address - 0x800) / 4) % 8 == 4)
                {
                    ptdi[(address - 0x800) / 32].DW4 = value;
                }
                if(((address - 0x800) / 4) % 8 == 5)
                {
                    ptdi[(address - 0x800) / 32].DW5 = value;
                }
                if(((address - 0x800) / 4) % 8 == 6)
                {
                    ptdi[(address - 0x800) / 32].DW6 = value;
                }
                if(((address - 0x800) / 4) % 8 == 7)
                {
                    ptdi[(address - 0x800) / 32].DW7 = value;
                }
            }
            else if(address >= (uint)0xc00 && address < (uint)0x1000)
            {
                if(((address - 0xc00) / 4) % 8 == 0)
                {
                    ptd[(address - 0xc00) / 32].DW0 = value;

                    PTDheader ptdh = new PTDheader();
                    ptdh.V = (ptd[(address - 0xc00) / 32].DW0) & 0x1;
                    ptdh.NrBytesToTransfer = (ptd[(address - 0xc00) / 32].DW0 >> 3) & 0x7fff;
                    ptdh.MaxPacketLength = (ptd[(address - 0xc00) / 32].DW0 >> 18) & 0x7ff;
                    ptdh.Mult = (ptd[(address - 0xc00) / 32].DW0 >> 29) & 0x2;
                    ptdh.EndPt = (((ptd[(address - 0xc00) / 32].DW1) & 0x7) << 1) | (ptd[(address - 0xc00) / 32].DW0 >> 31);
                    ptdh.DeviceAddress = (byte)((ptd[(address - 0xc00) / 32].DW1 >> 3) & 0x7f);
                    ptdh.Token = (ptd[(address - 0xc00) / 32].DW1 >> 10) & 0x3;
                    ptdh.EPType = (ptd[(address - 0xc00) / 32].DW1 >> 12) & 0x3;
                    ptdh.S = (ptd[(address - 0xc00) / 32].DW1 >> 14) & 0x1;
                    ptdh.SE = (ptd[(address - 0xc00) / 32].DW1 >> 16) & 0x3;
                    ptdh.PortNumber = (byte)((ptd[(address - 0xc00) / 32].DW1 >> 18) & 0x7f);
                    ptdh.HubAddress = (byte)((ptd[(address - 0xc00) / 32].DW1 >> 25) & 0x7f);
                    ptdh.DataStartAddress = (ptd[(address - 0xc00) / 32].DW2 >> 8) & 0xffff;
                    ptdh.RL = (ptd[(address - 0xc00) / 32].DW2 >> 25) & 0xf;
                    ptdh.NrBytesTransferred = (ptd[(address - 0xc00) / 32].DW3) & 0x7fff;
                    ptdh.NakCnt = (ptd[(address - 0xc00) / 32].DW3 >> 19) & 0xf;
                    ptdh.Cerr = (ptd[(address - 0xc00) / 32].DW3 >> 23) & 0x3;
                    ptdh.DT = (ptd[(address - 0xc00) / 32].DW3 >> 25) & 0x1;
                    ptdh.SC = (ptd[(address - 0xc00) / 32].DW3 >> 27) & 0x1;
                    ptdh.X = (ptd[(address - 0xc00) / 32].DW3 >> 28) & 0x1;
                    ptdh.B = (ptd[(address - 0xc00) / 32].DW3 >> 29) & 0x1;
                    ptdh.H = (ptd[(address - 0xc00) / 32].DW3 >> 30) & 0x1;
                    ptdh.A = (ptd[(address - 0xc00) / 32].DW3 >> 31) & 0x1;
                    ptdh.NextPTDPointer = (ptd[(address - 0xc00) / 32].DW4) & 0x1F;
                    ptdh.J = (ptd[(address - 0xc00) / 32].DW4 >> 5) & 0x1;
                    if(ptdh.V == 1)
                    {
                        {
                            this.Log(LogLevel.Noisy, "REG---------------------------");
                            this.Log(LogLevel.Noisy, "V=0{0:X}", ptdh.V);
                            this.Log(LogLevel.Noisy, "NrBytesToTransfer=0{0:X}", ptdh.NrBytesToTransfer);
                            this.Log(LogLevel.Noisy, "MaxPacketLength=0{0:X}", ptdh.MaxPacketLength);
                            this.Log(LogLevel.Noisy, "Mult=0{0:X}", ptdh.Mult);
                            this.Log(LogLevel.Noisy, "EndPt=0{0:X}", ptdh.EndPt);
                            this.Log(LogLevel.Noisy, "DeviceAddress=0{0:X}", ptdh.DeviceAddress);
                            this.Log(LogLevel.Noisy, "Token=0{0:X}", ptdh.Token);
                            this.Log(LogLevel.Noisy, "EPType=0{0:X}", ptdh.EPType);
                            this.Log(LogLevel.Noisy, "S=0{0:X}", ptdh.S);
                            this.Log(LogLevel.Noisy, "SE=0{0:X}", ptdh.SE);
                            this.Log(LogLevel.Noisy, "PortNumber=0{0:X}", ptdh.PortNumber);
                            this.Log(LogLevel.Noisy, "HubAddress =0{0:X}", ptdh.HubAddress);
                            this.Log(LogLevel.Noisy, "DataStartAddress=0{0:X}", ptdh.DataStartAddress);
                            this.Log(LogLevel.Noisy, "RL=0{0:X}", ptdh.RL);
                            this.Log(LogLevel.Noisy, "NrBytesTransferred=0{0:X}", ptdh.NrBytesTransferred);
                            this.Log(LogLevel.Noisy, "NakCnt=0{0:X}", ptdh.NakCnt);
                            this.Log(LogLevel.Noisy, "Cerr =0{0:X}", ptdh.Cerr);
                            this.Log(LogLevel.Noisy, "DT=0{0:X}", ptdh.DT);
                            this.Log(LogLevel.Noisy, "SC=0{0:X}", ptdh.SC);
                            this.Log(LogLevel.Noisy, "X=0{0:X}", ptdh.X);
                            this.Log(LogLevel.Noisy, "B=0{0:X}", ptdh.B);
                            this.Log(LogLevel.Noisy, "H =0{0:X}", ptdh.H);
                            this.Log(LogLevel.Noisy, "A=0{0:X}", ptdh.A);
                            this.Log(LogLevel.Noisy, "NextPTDPointer =0{0:X}", ptdh.NextPTDPointer);
                            this.Log(LogLevel.Noisy, "J=0{0:X}", ptdh.J);
                        }
                    }
                }

                if(((address - 0xc00) / 4) % 8 == 1)
                {
                    ptd[(address - 0xc00) / 32].DW1 = value;
                }
                if(((address - 0xc00) / 4) % 8 == 2)
                {
                    ptd[(address - 0xc00) / 32].DW2 = value;
                }
                if(((address - 0xc00) / 4) % 8 == 3)
                {
                    ptd[(address - 0xc00) / 32].DW3 = value;
                }
                if(((address - 0xc00) / 4) % 8 == 4)
                {
                    ptd[(address - 0xc00) / 32].DW4 = value;
                }
                if(((address - 0xc00) / 4) % 8 == 5)
                {
                    ptd[(address - 0xc00) / 32].DW5 = value;
                }
                if(((address - 0xc00) / 4) % 8 == 6)
                {
                    ptd[(address - 0xc00) / 32].DW6 = value;
                }
                if(((address - 0xc00) / 4) % 8 == 7)
                {
                    ptd[(address - 0xc00) / 32].DW7 = value;
                }
            }
            else if(address >= (uint)0x1000 && address <= (uint)0xffff)
            {
                Array.Copy(BitConverter.GetBytes(value), 0, payLoad, ((address)), 4);
            }
            else
            {
                PTDheader ptdh = new PTDheader();
                switch((Offset)address)
                {
                case Offset.UsbCommand:
                    UsbCmd = value;
                    if((value & 0x2) != 0)
                    {
                        UsbCmd &= ~0x2u;
                        this.SoftReset();
                        break;
                    }

                    if((value & 0x1) != 0)
                    {
                        UsbSts &= ~(uint)(1 << 12);
                        for(int i = 0; i < PortSc.Length; i++)
                        {
                            PortSc[i].Enable();
                        }
                    }

                    break;
                case Offset.CompanionPortRouting1:
                    hscpPortRoute[0] = value;
                    break;
                case Offset.CompanionPortRouting2:
                    hscpPortRoute[1] = value;
                    break;
                case Offset.AsyncListAddress:
                    AsyncListAddress = value;
                    break;
                case Offset.FrameListBaseAddress:
                    break;
                case Offset.INTPTDLastPTD:
                    intLastPTD = value;
                    break;
                case Offset.UsbInterruptEnable:
                    InterruptEnableRegister.OnAsyncAdvanceEnable = ((value & (uint)InterruptMask.InterruptOnAsyncAdvance) != 0) ? true : false;
                    InterruptEnableRegister.HostSystemErrorEnable = ((value & (uint)InterruptMask.HostSystemError) != 0) ? true : false;
                    InterruptEnableRegister.FrameListRolloverEnable = ((value & (uint)InterruptMask.FrameListRollover) != 0) ? true : false;
                    InterruptEnableRegister.PortChangeEnable = ((value & (uint)InterruptMask.PortChange) != 0) ? true : false;
                    InterruptEnableRegister.USBErrorEnable = ((value & (uint)InterruptMask.USBError) != 0) ? true : false;
                    InterruptEnableRegister.Enable = ((value & (uint)InterruptMask.USBInterrupt) != 0) ? true : false;
                    if(InterruptEnableRegister.Enable && InterruptEnableRegister.PortChangeEnable)
                    {
                        for(int i = 0; i < PortSc.Length; i++)
                        {
                            if((PortSc[i].GetValue() & PortStatusAndControlRegister.ConnectStatusChange) != 0)
                            {
                                IRQ.Set(false);
                                UsbSts |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.PortChange; //raise flags in status register
                                interr |= 1 << 8;
                                IRQ.Set(true); //raise interrupt
                                break;
                            }
                        }
                    }
                    break;

                case Offset.UsbStatus:
                    if((value & (uint)InterruptMask.FrameListRollover) != 0)
                    {
                        UsbSts &= ~(uint)(InterruptMask.FrameListRollover);
                    }
                    if((value & (uint)InterruptMask.HostSystemError) != 0)
                    {
                        UsbSts &= ~(uint)(InterruptMask.HostSystemError);
                    }
                    if((value & (uint)InterruptMask.InterruptOnAsyncAdvance) != 0)
                    {
                        UsbSts &= ~(uint)(InterruptMask.InterruptOnAsyncAdvance);
                    }
                    if((value & (uint)InterruptMask.PortChange) != 0)
                    {
                        UsbSts &= ~(uint)(InterruptMask.PortChange);
                    }
                    if((value & (uint)InterruptMask.USBError) != 0)
                    {
                        UsbSts &= ~(uint)(InterruptMask.USBError);
                    }
                    if((value & (uint)InterruptMask.USBInterrupt) != 0)
                    {
                        UsbSts &= ~(uint)(InterruptMask.USBInterrupt);
                        IRQ.Set(false); //clear interrupt
                    }
                    break;
                case Offset.ConfiguredFlag:
                    ConfigFlag = value;
                    break;
                case (Offset)0x330:
                    atlIRQMaskOR = value;
                    break;
                case Offset.ATLPTDLastPTD:
                    atlLastPTD = value;
                    break;
                case (Offset)Offset.INTPTDSkipMap:
                    intSkipMap = value;
                    break;
                case (Offset)Offset.ATLPTDSkipMap:
                    atlSkipMap = value;
                    lock(thisLock)
                    {
                        for(int p = 0; p < 32; p++)
                            if((atlSkipMap & (1 << p)) == 0)
                            {
                                if((1 << p) == atlLastPTD)
                                {
                                    break;
                                }
                                ptdh.V = (ptd[p].DW0) & 0x1;
                                ptdh.NrBytesToTransfer = (ptd[p].DW0 >> 3) & 0x7fff;
                                ptdh.MaxPacketLength = (ptd[p].DW0 >> 18) & 0x7ff;
                                ptdh.Mult = (ptd[p].DW0 >> 29) & 0x2;
                                ptdh.EndPt = (((ptd[p].DW1) & 0x7) << 1) | (ptd[p].DW0 >> 31);
                                ptdh.DeviceAddress = (byte)((ptd[p].DW1 >> 3) & 0x7f);
                                ptdh.Token = (ptd[p].DW1 >> 10) & 0x3;
                                ptdh.EPType = (ptd[p].DW1 >> 12) & 0x3;
                                ptdh.S = (ptd[p].DW1 >> 14) & 0x1;
                                ptdh.SE = (ptd[p].DW1 >> 16) & 0x3;
                                ptdh.PortNumber = (byte)((ptd[p].DW1 >> 18) & 0x7f);
                                ptdh.HubAddress = (byte)((ptd[p].DW1 >> 25) & 0x7f);
                                ptdh.DataStartAddress = (((ptd[p].DW2 >> 8) & 0xffff) << 3) + 0x400;
                                ptdh.RL = (ptd[p].DW2 >> 25) & 0xf;
                                ptdh.NrBytesTransferred = (ptd[p].DW3) & 0x7fff;
                                ptdh.NakCnt = (ptd[p].DW3 >> 19) & 0xf;
                                ptdh.Cerr = (ptd[p].DW3 >> 23) & 0x3;
                                ptdh.DT = (ptd[p].DW3 >> 25) & 0x1;
                                ptdh.SC = (ptd[p].DW3 >> 27) & 0x1;
                                ptdh.X = (ptd[p].DW3 >> 28) & 0x1;
                                ptdh.B = (ptd[p].DW3 >> 29) & 0x1;
                                ptdh.H = (ptd[p].DW3 >> 30) & 0x1;
                                ptdh.A = (ptd[p].DW3 >> 31) & 0x1;
                                ptdh.NextPTDPointer = (ptd[p].DW4) & 0x1F;
                                ptdh.J = (ptd[p].DW4 >> 5) & 0x1;
                                if(ptdh.V != 0)
                                {
                                    /* Process Packet */
                                    ProcessPacket(ptdh);
                                    /* Set packet done bits */
                                    if(ptdh.A == 0)
                                    {
                                        ptd[p].DW3 = (uint)(((ptd[p].DW3 | ((((0 >> 3) & 0x7fff) + ptdh.NrBytesTransferred) & 0x7fff) << 0) & 0x7fffffff));
                                        ptd[p].DW3 = ptd[p].DW3 | (ptdh.B << 29);
                                        ptd[p].DW3 = ptd[p].DW3 | (ptdh.H << 30);
                                        ptd[p].DW0 = ptd[p].DW0 & 0xfffffffe;
                                        //  ptd[p].DW3 = (ptd[p].DW3&0xff87ffff) | (PTDh.NakCnt<<19);
                                        Done(p);
                                    }
                                }
                            }
                        if(atlDoneMap != 0)
                        {
                            if((InterruptEnableRegister.OnAsyncAdvanceEnable == true) & (InterruptEnableRegister.Enable == true))
                            {
                                UsbSts |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.InterruptOnAsyncAdvance; //raise flags in status register
                                interr |= 1 << 8;
                                IRQ.Set(true); //raise interrupt
                            }
                        }
                    }
                    ProcessINT();
                    break;
                case (Offset)Offset.Interrupt:
                    IRQ.Set(false);
                    interr = (int)value;
                    break;
                case (Offset)Offset.Memory:
                    this.Log(LogLevel.Noisy, "Memory Banks: {0:x}", value);
                    memoryReg = value;
                    break;
                case (Offset)Offset.InterruptEnable:
                    InterruptEnableRegister.Enable = true;
                    InterruptEnableRegister.OnAsyncAdvanceEnable = true;
                    InterruptEnableRegister.PortChangeEnable = true;
                    break;
                case (Offset)Offset.SWReset:
                    swReset = value;
                    if((swReset & (1 << 0)) > 0)
                    {
                        intDoneMap = 0x00000000;
                        intSkipMap = 0xFFFFFFFF;
                        atlSkipMap = 0xFFFFFFFF;
                        atlDoneMap = 0x00000000;
                        atlIRQMaskOR = 0x0000000;
                        intIRQMaskOR = 0x0000000;
                        swReset = 0x00000000;
                        memoryReg = 0x0000000;
                        SoftReset();
                    }
                    if((swReset & (1 << 0)) > 0)
                    {
                        intDoneMap = 0x00000000;
                        intSkipMap = 0xFFFFFFFF;
                        atlSkipMap = 0xFFFFFFFF;
                        atlDoneMap = 0x00000000;
                        atlIRQMaskOR = 0x0000000;
                        intIRQMaskOR = 0x0000000;
                        swReset = 0x00000000;
                        memoryReg = 0x0000000;
                    }
                    break;
                case (Offset)Offset.Scratch:
                    scratch = value;
                    break;

                default:
                    this.LogUnhandledWrite(address, value);
                    break;
                }
            }
        }

        public void SoftReset()
        {
            adressedDevices = new Dictionary<byte, IUSBPeripheral>();

            hCSParams = (NCC & 0x0f) << 12 | (NPCC & 0x0f) << 8 | ((uint)PortSc.Length & 0xf) << 0;
            //TODO: manage variable amount of ports
            for(int i = 0; i < PortSc.Length; i++)
            {
                PortSc[i].SetValue(0x00001000);
                PortSc[i].PowerUp();

                if(firstReset)
                {
                    PortStatusAndControlRegisterChanges change = PortSc[i].Attach();
                    firstReset = false;
                    //}

                    if(change.ConnectChange == true)
                    {
                        UsbSts |= (uint)InterruptMask.PortChange;
                    }

                    if((InterruptEnableRegister.Enable == true) && (InterruptEnableRegister.PortChangeEnable == true))
                    {
                        UsbSts |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.PortChange; //raise flags in status register
                        //  interr|=1<<8;
                        interr |= 1 << 8;
                        IRQ.Set(true);  //raise interrupt
                    }
                }
            }
            //interrupts
        }

        public void AttachHUBDevice(IUSBPeripheral device, byte port)
        {
            registeredDevices.Add(port, device);

            PortStatusAndControlRegisterChanges change = PortSc[port - 1].Attach();

            if(change.ConnectChange == true)
            {
                UsbSts |= (uint)InterruptMask.PortChange;
            }

            if((InterruptEnableRegister.Enable == true) && (InterruptEnableRegister.PortChangeEnable == true))
            {
                UsbSts |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.PortChange; //raise flags in status register
                interr |= 1 << 7;
                IRQ.Set(true); //raise interrupt
            }
        }

        public void DetachDevice(byte port)
        {
            registeredDevices.Remove(port);
        }

        public void AttachHUBDevice(uint addr)
        {
            PTDheader ptdh = new PTDheader();
            for(int p = 0; p < 32; p++)
                // int p=0;
                //int p=0;
                //if(atlSkipMap == 0xFFFFFFFE)
                if(((1 << p) ^ (intSkipMap & (1 << p))) != 0)
                {
                    if((1 << p) == intLastPTD)
                    {
                        break;
                    }
                    ptdh.V = (ptdi[p].DW0) & 0x1;
                    ptdh.NrBytesToTransfer = (ptdi[p].DW0 >> 3) & 0x7fff;
                    ptdh.MaxPacketLength = (ptdi[p].DW0 >> 18) & 0x7ff;
                    ptdh.Mult = (ptdi[p].DW0 >> 29) & 0x2;
                    ptdh.EndPt = (((ptdi[p].DW1) & 0x7) << 1) | (ptdi[p].DW0 >> 31);
                    ptdh.DeviceAddress = (byte)((ptdi[p].DW1 >> 3) & 0x7f);
                    ptdh.Token = (ptdi[p].DW1 >> 10) & 0x3;
                    ptdh.EPType = (ptdi[p].DW1 >> 12) & 0x3;
                    ptdh.S = (ptdi[p].DW1 >> 14) & 0x1;
                    ptdh.SE = (ptdi[p].DW1 >> 16) & 0x3;
                    ptdh.PortNumber = (byte)((ptdi[p].DW1 >> 18) & 0x7f);
                    ptdh.HubAddress = (byte)((ptdi[p].DW1 >> 25) & 0x7f);
                    ptdh.DataStartAddress = (((ptdi[p].DW2 >> 8) & 0xffff) << 3) + 0x400;
                    ptdh.RL = (ptdi[p].DW2 >> 25) & 0xf;
                    ptdh.NrBytesTransferred = (ptdi[p].DW3) & 0x7fff;
                    ptdh.NakCnt = (ptdi[p].DW3 >> 19) & 0xf;
                    ptdh.Cerr = (ptdi[p].DW3 >> 23) & 0x3;
                    ptdh.DT = (ptdi[p].DW3 >> 25) & 0x1;
                    ptdh.SC = (ptdi[p].DW3 >> 27) & 0x1;
                    ptdh.X = (ptdi[p].DW3 >> 28) & 0x1;
                    ptdh.B = (ptdi[p].DW3 >> 29) & 0x1;
                    ptdh.H = (ptdi[p].DW3 >> 30) & 0x1;
                    ptdh.A = (ptdi[p].DW3 >> 31) & 0x1;
                    ptdh.NextPTDPointer = (ptdi[p].DW4) & 0x1F;
                    ptdh.J = (ptdi[p].DW4 >> 5) & 0x1;
                    if(addr == ptdh.DeviceAddress)
                    {
                        if(ptdh.V != 0)
                        {
                            /* Process Packet */
                            ProcessPacketInt(ptdh);
                            /* Set packet done bits */
                            //if (PTDh.V==0)
                            {
                                //PTDh.H=1;
                                ptdi[p].DW0 = ptdi[p].DW0 & 0xfffffffe;
                                ptdi[p].DW3 = (uint)(((ptdi[p].DW3 | ((((0 >> 3) & 0x7fff) + ptdh.NrBytesTransferred) & 0x7fff) << 0) & 0x7fffffff));
                                ptdi[p].DW3 = ptdi[p].DW3 | (ptdh.B << 29);
                                ptdi[p].DW3 = ptdi[p].DW3 | (ptdh.H << 30);
                                ptdi[p].DW4 = ptdi[p].DW4 & 0xfffffffe;
                                DoneInt(p);

                                if((InterruptEnableRegister.OnAsyncAdvanceEnable == true) & (InterruptEnableRegister.Enable == true))
                                {
                                    UsbSts |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.InterruptOnAsyncAdvance; //raise flags in status register
                                    interr |= 1 << 7;
                                    IRQ.Set(true); //raise interrupt
                                }
                            }
                        }
                    }
                }
        }

        public void RegisterHub(IUSBHub hub)
        {
            RegHub(hub);
            ActiveDevice = hub;
            hub.RegisterHub += new Action<IUSBHub>(RegHub);
            hub.Connected += new Action<uint>(AttachHUBDevice);
            hub.Disconnected += new Action<uint, uint>(DetachHUBDevice);
            hub.ActiveDevice += new Action<IUSBPeripheral>(Active);
        }

        public void RegHub(IUSBHub hub)
        {
            registeredHubs.Add((byte)(registeredHubs.Count() + 1), hub);
        }

        public void DetachHUBDevice(uint addr, uint port)
        {
            PTDheader ptdh = new PTDheader();
            for(int p = 0; p < 32; p++)

                if(((1 << p) ^ (intSkipMap & (1 << p))) != 0)
                {
                    if((1 << p) == intLastPTD)
                    {
                        break;
                    }
                    ptdh.V = (ptdi[p].DW0) & 0x1;
                    ptdh.NrBytesToTransfer = (ptdi[p].DW0 >> 3) & 0x7fff;
                    ptdh.MaxPacketLength = (ptdi[p].DW0 >> 18) & 0x7ff;
                    ptdh.Mult = (ptdi[p].DW0 >> 29) & 0x2;
                    ptdh.EndPt = (((ptdi[p].DW1) & 0x7) << 1) | (ptdi[p].DW0 >> 31);
                    ptdh.DeviceAddress = (byte)((ptdi[p].DW1 >> 3) & 0x7f);
                    ptdh.Token = (ptdi[p].DW1 >> 10) & 0x3;
                    ptdh.EPType = (ptdi[p].DW1 >> 12) & 0x3;
                    ptdh.S = (ptdi[p].DW1 >> 14) & 0x1;
                    ptdh.SE = (ptdi[p].DW1 >> 16) & 0x3;
                    ptdh.PortNumber = (byte)((ptdi[p].DW1 >> 18) & 0x7f);
                    ptdh.HubAddress = (byte)((ptdi[p].DW1 >> 25) & 0x7f);
                    ptdh.DataStartAddress = (((ptdi[p].DW2 >> 8) & 0xffff) << 3) + 0x400;
                    ptdh.RL = (ptdi[p].DW2 >> 25) & 0xf;
                    ptdh.NrBytesTransferred = (ptdi[p].DW3) & 0x7fff;
                    ptdh.NakCnt = (ptdi[p].DW3 >> 19) & 0xf;
                    ptdh.Cerr = (ptdi[p].DW3 >> 23) & 0x3;
                    ptdh.DT = (ptdi[p].DW3 >> 25) & 0x1;
                    ptdh.SC = (ptdi[p].DW3 >> 27) & 0x1;
                    ptdh.X = (ptdi[p].DW3 >> 28) & 0x1;
                    ptdh.B = (ptdi[p].DW3 >> 29) & 0x1;
                    ptdh.H = (ptdi[p].DW3 >> 30) & 0x1;
                    ptdh.A = (ptdi[p].DW3 >> 31) & 0x1;
                    ptdh.NextPTDPointer = (ptdi[p].DW4) & 0x1F;
                    ptdh.J = (ptdi[p].DW4 >> 5) & 0x1;
                    if(addr == ptdh.DeviceAddress)
                    {
                        if(ptdh.V != 0)
                        {
                            /* Process Packet */
                            ProcessPacketInt(ptdh);
                            /* Set packet done bits */
                            {
                                ptdi[p].DW0 = ptdi[p].DW0 & 0xfffffffe;
                                ptdi[p].DW3 = (uint)(((ptdi[p].DW3 | ((((0 >> 3) & 0x7fff) + ptdh.NrBytesTransferred) & 0x7fff) << 0) & 0x7fffffff));
                                ptdi[p].DW3 = ptdi[p].DW3 | (ptdh.B << 29);
                                ptdi[p].DW3 = ptdi[p].DW3 | (ptdh.H << 30);
                                ptdi[p].DW4 = ptdi[p].DW4 & 0xfffffffe;
                                DoneInt(p);

                                if((InterruptEnableRegister.OnAsyncAdvanceEnable == true) & (InterruptEnableRegister.Enable == true))
                                {
                                    UsbSts |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.InterruptOnAsyncAdvance; //raise flags in status register
                                    interr |= 1 << 7;
                                    IRQ.Set(true); //raise interrupt
                                }
                            }
                        }

                        IUSBHub hub;
                        IUSBPeripheral device;

                        for(byte x = 1; x <= registeredHubs.Count; x++)
                        {
                            hub = registeredHubs[x];
                            if(hub.GetAddress() == port)
                            {
                                for(byte i = 1; i <= (byte)hub.NumberOfPorts; i++)
                                    if((device = hub.GetDevice(i)) != null)
                                        if(device.GetAddress() != 0)

                                            RemoveFromHub(device);

                                registeredHubs.Remove((byte)x);
                            }
                        }
                        adressedDevices.Remove((byte)port);
                    }
                }
        }

        public void RemoveFromHub(IUSBPeripheral dev)
        {
            IUSBHub hub;
            IUSBPeripheral device;
            for(byte x = 1; x <= registeredHubs.Count; x++)
            {
                hub = registeredHubs[x];
                if(hub.GetAddress() == dev.GetAddress())
                {
                    for(byte i = 1; i <= (byte)hub.NumberOfPorts; i++)
                        if((device = hub.GetDevice(i)) != null)
                            if(device.GetAddress() != 0)
                                RemoveFromHub(device);
                            else
                                adressedDevices.Remove((byte)device.GetAddress());
                    registeredHubs.Remove((byte)x);
                }
            }
            adressedDevices.Remove((byte)dev.GetAddress());
        }

        public void ProcessINT()
        {
            PTDheader ptdh = new PTDheader();
            for(int p = 0; p < 32; p++)
                if(((1 << p) ^ (intSkipMap & (1 << p))) != 0)
                {
                    if((1 << p) == intLastPTD)
                    {
                        break;
                    }
                    ptdh.V = (ptdi[p].DW0) & 0x1;
                    ptdh.NrBytesToTransfer = (ptdi[p].DW0 >> 3) & 0x7fff;
                    ptdh.MaxPacketLength = (ptdi[p].DW0 >> 18) & 0x7ff;
                    ptdh.Mult = (ptdi[p].DW0 >> 29) & 0x2;
                    ptdh.EndPt = (((ptdi[p].DW1) & 0x7) << 1) | (ptdi[p].DW0 >> 31);
                    ptdh.DeviceAddress = (byte)((ptdi[p].DW1 >> 3) & 0x7f);
                    ptdh.Token = (ptdi[p].DW1 >> 10) & 0x3;
                    ptdh.EPType = (ptdi[p].DW1 >> 12) & 0x3;
                    ptdh.S = (ptdi[p].DW1 >> 14) & 0x1;
                    ptdh.SE = (ptdi[p].DW1 >> 16) & 0x3;
                    ptdh.PortNumber = (byte)((ptdi[p].DW1 >> 18) & 0x7f);
                    ptdh.HubAddress = (byte)((ptdi[p].DW1 >> 25) & 0x7f);
                    ptdh.DataStartAddress = (((ptdi[p].DW2 >> 8) & 0xffff) << 3) + 0x400;
                    ptdh.RL = (ptdi[p].DW2 >> 25) & 0xf;
                    ptdh.NrBytesTransferred = (ptdi[p].DW3) & 0x7fff;
                    ptdh.NakCnt = (ptdi[p].DW3 >> 19) & 0xf;
                    ptdh.Cerr = (ptdi[p].DW3 >> 23) & 0x3;
                    ptdh.DT = (ptdi[p].DW3 >> 25) & 0x1;
                    ptdh.SC = (ptdi[p].DW3 >> 27) & 0x1;
                    ptdh.X = (ptdi[p].DW3 >> 28) & 0x1;
                    ptdh.B = (ptdi[p].DW3 >> 29) & 0x1;
                    ptdh.H = (ptdi[p].DW3 >> 30) & 0x1;
                    ptdh.A = (ptdi[p].DW3 >> 31) & 0x1;
                    ptdh.NextPTDPointer = (ptdi[p].DW4) & 0x1F;
                    ptdh.J = (ptdi[p].DW4 >> 5) & 0x1;
                    //if(addr == PTDh.DeviceAddress)
                    {
                        if(ptdh.V != 0)
                        {
                            /* Process Packet */
                            ProcessPacketInt(ptdh);
                            if(ptdh.V == 0)
                            {
                                /* Set packet done bits */

                                ptdi[p].DW0 = ptdi[p].DW0 & 0xfffffffe;
                                ptdi[p].DW3 = (uint)(((ptdi[p].DW3 | ((((0 >> 3) & 0x7fff) + ptdh.NrBytesTransferred) & 0x7fff) << 0) & 0x7fffffff));
                                ptdi[p].DW3 = ptdi[p].DW3 | (ptdh.B << 29);
                                ptdi[p].DW3 = ptdi[p].DW3 | (ptdh.H << 30);
                                ptdi[p].DW4 = ptdi[p].DW4 & 0xfffffffe;
                                DoneInt(p);
                                if((InterruptEnableRegister.OnAsyncAdvanceEnable == true) & (InterruptEnableRegister.Enable == true))
                                {
                                    UsbSts |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.InterruptOnAsyncAdvance; //raise flags in status register
                                    interr |= 1 << 7;
                                    IRQ.Set(true); //raise interrupt
                                }
                            }
                        }
                    }
                }
        }

        public uint ReadDoubleWord(long address)
        {
            //this.Log(LogType.Warning, "Read from offset 0x{0:X}", address);

            if(address >= (uint)0x64 && address < (uint)0x130)
            {
                uint portNumber = (uint)(address - (uint)Offset.PortStatusControl) / 4u;

                return PortSc[portNumber].GetValue();
            }
            else if(address >= (uint)0x800 && address < (uint)0xbff)
            {
                //this.Log(LogType.Warning, "READ PTD {0} reg {1}", (address - 0x800) / 32, ((address - 0x800) / 4) % 8);
                if(((address - 0x800) / 4) % 8 == 0)
                {
                    // this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0x800) / 32].DW0);
                    return ptdi[(address - 0x800) / 32].DW0;
                }
                if(((address - 0x800) / 4) % 8 == 1)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0x800) / 32].DW1);
                    return ptdi[(address - 0x800) / 32].DW1;
                }
                if(((address - 0x800) / 4) % 8 == 2)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0x800) / 32].DW2);
                    return ptdi[(address - 0x800) / 32].DW2;
                }
                if(((address - 0x800) / 4) % 8 == 3)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0x800) / 32].DW3);
                    return ptdi[(address - 0x800) / 32].DW3;
                }
                if(((address - 0x800) / 4) % 8 == 4)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0x800) / 32].DW4);
                    return ptdi[(address - 0x800) / 32].DW4;
                }
                if(((address - 0x800) / 4) % 8 == 5)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0x800) / 32].DW5);
                    return ptdi[(address - 0x800) / 32].DW5;
                }
                if(((address - 0x800) / 4) % 8 == 6)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0x800) / 32].DW6);
                    return ptdi[(address - 0x800) / 32].DW6;
                }
                if(((address - 0x800) / 4) % 8 == 7)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0x800) / 32].DW7);
                    return ptdi[(address - 0x800) / 32].DW7;
                }
            }
            else
            /* Read QH registers */ if(address >= (uint)0xc00 && address < (uint)0x1000)
            {
                //this.Log(LogType.Warning, "READ PTD {0} reg {1}", (address - 0xc00) / 32, ((address - 0xc00) / 4) % 8);
                if(((address - 0xc00) / 4) % 8 == 0)
                {
                    // this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0xc00) / 32].DW0);
                    return ptd[(address - 0xc00) / 32].DW0;
                }
                if(((address - 0xc00) / 4) % 8 == 1)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0xc00) / 32].DW1);
                    return ptd[(address - 0xc00) / 32].DW1;
                }
                if(((address - 0xc00) / 4) % 8 == 2)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0xc00) / 32].DW2);
                    return ptd[(address - 0xc00) / 32].DW2;
                }
                if(((address - 0xc00) / 4) % 8 == 3)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0xc00) / 32].DW3);
                    return ptd[(address - 0xc00) / 32].DW3;
                }
                if(((address - 0xc00) / 4) % 8 == 4)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0xc00) / 32].DW4);
                    return ptd[(address - 0xc00) / 32].DW4;
                }
                if(((address - 0xc00) / 4) % 8 == 5)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0xc00) / 32].DW5);
                    return ptd[(address - 0xc00) / 32].DW5;
                }
                if(((address - 0xc00) / 4) % 8 == 6)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0xc00) / 32].DW6);
                    return ptd[(address - 0xc00) / 32].DW6;
                }
                if(((address - 0xc00) / 4) % 8 == 7)
                { //this.Log(LogType.Warning, "READ VAL 0{0:X}",ptd[(address - 0xc00) / 32].DW7);
                    return ptd[(address - 0xc00) / 32].DW7;
                }
            }
            else
            /* Read from memory area*/ if(address >= (uint)0x1000 && address <= (uint)0xffff)
            {
                //this.Log(LogType.Warning, "Read payLoad 0{0:X}", address);
                return (uint)BitConverter.ToUInt32(payLoad, (int)((address)));
            }
            else
            {
                uint tDM = 0;
                switch((Offset)address)
                {
                case Offset.CapabilityLength:
                    return CapBase;

                case Offset.StructuralParameters:
                    return hCSParams;

                case Offset.CapabilityParameters:
                    return hCCParams;

                case Offset.CompanionPortRouting1:
                    return hscpPortRoute[0];

                case Offset.CompanionPortRouting2:
                    return hscpPortRoute[1];

                case Offset.UsbCommand:
                    return UsbCmd;

                case Offset.UsbStatus:
                    return UsbSts;

                case Offset.UsbFrameIndex:
                    return UsbFrIndex;

                case Offset.AsyncListAddress:
                    return AsyncListAddress;

                case Offset.ConfiguredFlag:
                    return ConfigFlag;

                case Offset.UsbInterruptEnable:
                    return InterruptEnableRegister.GetRegister();

                case (Offset)Offset.INTPTDDoneMap:
                    tDM = intDoneMap;
                    intDoneMap = 0x0;
                    return tDM;
                case (Offset)Offset.INTPTDSkipMap:
                    return intSkipMap;
                case Offset.INTPTDLastPTD:
                    return intLastPTD;

                case (Offset)0x330:
                    return atlIRQMaskOR;
                case (Offset)Offset.Memory:
                    return memoryReg;
                case (Offset)Offset.ATLPTDDoneMap:
                    tDM = atlDoneMap;
                    atlDoneMap = 0x0;
                    return tDM;
                case (Offset)Offset.ATLPTDSkipMap:
                    return atlSkipMap;
                case (Offset)Offset.ChipID:
                    return 0x00011761;
                case (Offset)Offset.Scratch:
                    return scratch;
                case Offset.ATLPTDLastPTD:
                    return atlLastPTD;
                case (Offset)Offset.Interrupt:
                    return (uint)interr;//0xffffffff;
                case (Offset)Offset.SWReset:
                    return swReset;
                default:
                    this.LogUnhandledRead(address);
                    break;
                }
            }
            return 0;
        }

        public void ProcessINT(uint addr)
        {
            PTDheader ptdh = new PTDheader();
            for(int p = 0; p < 32; p++)
                if(((1 << p) ^ (intSkipMap & (1 << p))) != 0)
                {
                    if((1 << p) == intLastPTD)
                    {
                        break;
                    }
                    ptdh.V = (ptdi[p].DW0) & 0x1;
                    ptdh.NrBytesToTransfer = (ptdi[p].DW0 >> 3) & 0x7fff;
                    ptdh.MaxPacketLength = (ptdi[p].DW0 >> 18) & 0x7ff;
                    ptdh.Mult = (ptdi[p].DW0 >> 29) & 0x2;
                    ptdh.EndPt = (((ptdi[p].DW1) & 0x7) << 1) | (ptdi[p].DW0 >> 31);
                    ptdh.DeviceAddress = (byte)((ptdi[p].DW1 >> 3) & 0x7f);
                    ptdh.Token = (ptdi[p].DW1 >> 10) & 0x3;
                    ptdh.EPType = (ptdi[p].DW1 >> 12) & 0x3;
                    ptdh.S = (ptdi[p].DW1 >> 14) & 0x1;
                    ptdh.SE = (ptdi[p].DW1 >> 16) & 0x3;
                    ptdh.PortNumber = (byte)((ptdi[p].DW1 >> 18) & 0x7f);
                    ptdh.HubAddress = (byte)((ptdi[p].DW1 >> 25) & 0x7f);
                    ptdh.DataStartAddress = (((ptdi[p].DW2 >> 8) & 0xffff) << 3) + 0x400;
                    ptdh.RL = (ptdi[p].DW2 >> 25) & 0xf;
                    ptdh.NrBytesTransferred = (ptdi[p].DW3) & 0x7fff;
                    ptdh.NakCnt = (ptdi[p].DW3 >> 19) & 0xf;
                    ptdh.Cerr = (ptdi[p].DW3 >> 23) & 0x3;
                    ptdh.DT = (ptdi[p].DW3 >> 25) & 0x1;
                    ptdh.SC = (ptdi[p].DW3 >> 27) & 0x1;
                    ptdh.X = (ptdi[p].DW3 >> 28) & 0x1;
                    ptdh.B = (ptdi[p].DW3 >> 29) & 0x1;
                    ptdh.H = (ptdi[p].DW3 >> 30) & 0x1;
                    ptdh.A = (ptdi[p].DW3 >> 31) & 0x1;
                    ptdh.NextPTDPointer = (ptdi[p].DW4) & 0x1F;
                    ptdh.J = (ptdi[p].DW4 >> 5) & 0x1;
                    if(addr == ptdh.DeviceAddress)
                    {
                        if(ptdh.V != 0)
                        {
                            /* Process Packet */
                            ProcessPacketInt(ptdh);
                            if(ptdh.V == 0)
                            {
                                /* Set packet done bits */

                                ptdi[p].DW0 = ptdi[p].DW0 & 0xfffffffe;
                                ptdi[p].DW3 = (uint)(((ptdi[p].DW3 | ((((0 >> 3) & 0x7fff) + ptdh.NrBytesTransferred) & 0x7fff) << 0) & 0x7fffffff));
                                ptdi[p].DW3 = ptdi[p].DW3 | (ptdh.B << 29);
                                ptdi[p].DW3 = ptdi[p].DW3 | (ptdh.H << 30);
                                ptdi[p].DW4 = ptdi[p].DW4 & 0xfffffffe;
                                DoneInt(p);
                                if((InterruptEnableRegister.OnAsyncAdvanceEnable == true) & (InterruptEnableRegister.Enable == true))
                                {
                                    UsbSts |= (uint)InterruptMask.USBInterrupt | (uint)InterruptMask.InterruptOnAsyncAdvance; //raise flags in status register
                                    interr |= 1 << 7;
                                    IRQ.Set(true); //raise interrupt
                                }
                            }
                        }
                    }
                }
        }

        public void WriteDoubleWordPCI(uint bar, long offset, uint value)
        {
            if(bar == 3)
                WriteDoubleWord(offset, value);
            return;
        }

        public PCIInfo GetPCIInfo()
        {
            return pci_info;
        }

        public void Register(IUSBHub peripheral, USBRegistrationPoint registrationInfo)
        {
            AttachHUBDevice(peripheral, registrationInfo.Address.Value);
            RegisterHub(peripheral);
            machine.RegisterAsAChildOf(this, peripheral, registrationInfo);
            defaultDevice = peripheral;
            return;
        }

        public void Unregister(IUSBHub peripheral)
        {
            byte port = registeredDevices.FirstOrDefault(x => x.Value == peripheral).Key;
            DetachDevice(port);
            machine.UnregisterAsAChildOf(this, peripheral);
            registeredDevices.Remove(port);
            registeredHubs.Remove(port);
        }

        public void Active(IUSBPeripheral periph)
        {
            ActiveDevice = periph;
        }

        public uint ReadDoubleWordPCI(uint bar, long offset)
        {
            if(bar == 3)
                return ReadDoubleWord(offset);
            return 0;
        }

        public void Register(IUSBPeripheral peripheral, USBRegistrationPoint registrationInfo)
        {
            AttachHUBDevice(peripheral, registrationInfo.Address.Value);
            machine.RegisterAsAChildOf(this, peripheral, registrationInfo);
            defaultDevice = peripheral;
        }

        public void Reset()
        {
            SoftReset();
            ActiveDevice = defaultDevice;
        }

        public IEnumerable<USBRegistrationPoint> GetRegistrationPoints(IUSBPeripheral peripheral)
        {
            throw new System.NotImplementedException();
        }

        public IUSBPeripheral FindDevice(byte port)
        {
            throw new NotImplementedException();
        }

        public void Done(int p)
        {
            atlDoneMap |= (uint)(1 << p);
            atlIRQMaskOR |= (uint)(1 << p);
        }

        public void Unregister(IUSBPeripheral peripheral)
        {
            byte port = registeredDevices.FirstOrDefault(x => x.Value == peripheral).Key;
            DetachDevice(port);
            machine.UnregisterAsAChildOf(this, peripheral);
            registeredDevices.Remove(port);
            registeredHubs.Remove(port);
        }

        public GPIO IRQ { get; private set; }

        public IEnumerable<IRegistered<IUSBPeripheral, USBRegistrationPoint>> Children
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }

        public IUSBHub Parent
        {
            get;
            set;
        }

        public Dictionary<byte, IUSBPeripheral> DeviceList
        {
            get;
            set;
        }

        public uint UsbFrIndex = 0; //usb frame index
        public InterruptEnable InterruptEnableRegister = new InterruptEnable();
        public uint UsbCmd = 0x00080000; //usb command
        public PortStatusAndControlRegister[] PortSc; //port status control
        public uint UsbSts = 0x0000000; //usb status
        public  USBSetupPacket SetupData ;

        public int XPort = 0;
        public bool OutBool = false;
        public uint AsyncListAddress; //next async addres
        public uint ConfigFlag; // configured flag registers
        public const uint OpBase = 0x20;  //operational registers base addr
        protected IUSBPeripheral ActiveDevice;

        protected Object thisLock = new Object();
        protected IUSBPeripheral defaultDevice;

        private void AddressDevice(IUSBPeripheral device, byte address)
        {
            if(!adressedDevices.ContainsKey(address))//XXX: Linux hack
            {
                adressedDevices.Add(address, device);
            }
        }

        private IUSBPeripheral FindDeviceInternal(byte deviceAddress)
        {
            if(registeredHubs.Count() > 0)
            {
                if(adressedDevices.ContainsKey(deviceAddress))
                {
                    IUSBPeripheral device = adressedDevices[deviceAddress];
                    return device;
                }
                else
                {
                    return null;
                }
            }
            return null;
        }

        private IUSBPeripheral FindDeviceByPortInternal(byte portNumber)
        {
            if(registeredHubs.Count() > 0)
            {
                if(portNumber != 0)
                {
                    IUSBHub hub;
                    IUSBPeripheral device;

                    for(byte x = 1; x <= registeredHubs.Count; x++)
                    {
                        hub = registeredHubs[x];
                        for(byte i = 1; i <= (byte)hub.NumberOfPorts; i++)
                            if((device = hub.GetDevice(i)) != null)
                                if(device.GetAddress() == 0)
                                    return device;
                    }
                    return null;
                }
                else
                {
                    IUSBPeripheral device = registeredDevices[(byte)(portNumber + (byte)1)];
                    return device;
                }
            }
            return null;
        }

        private uint intDoneMap;
        private uint intSkipMap;
        private uint intLastPTD;
        private uint atlSkipMap;
        private uint atlLastPTD;
        private uint atlDoneMap;
        private uint atlIRQMaskOR;
        private uint scratch = 0xdeadbabe;
        private uint intIRQMaskOR;
        private uint swReset;
        private uint memoryReg;
        private PCIInfo pci_info;

        private bool firstReset = true;
        private int interr;
        private Dictionary <byte,IUSBPeripheral> adressedDevices;
        private uint hCSParams = 0; //structural parameters (addr 0x04) (RO)

        private readonly uint[] hscpPortRoute = new uint [2];
        private readonly PTD[] ptdi = new PTD[32];
        private readonly PTD[] ptd = new PTD[32];

        private readonly Dictionary <byte,IUSBPeripheral> registeredDevices;
        private readonly Dictionary <byte,IUSBHub> registeredHubs;
        private readonly uint hCCParams = 0; //capability parameters (addr 0x08) (RO)
        private readonly byte[] payLoad = new byte[0x10000];
        private readonly IMachine machine;

        uint counter = 0;
        private const uint NPCC = 0; //number of ports per companion controller
        private const uint NCC = 0; //number of companion controllers

        private const uint HciVersion = 0x0100;//hci version (16 bit BCD)
        private const uint CapBase = (HciVersion & 0xffff) << 16 | ((OpBase) & 0xff) << 0;//lenght + version (0x00) (RO)

        public class InterruptEnable
        {
            public uint GetRegister()
            {
                uint regValue = 0;
                regValue |= (uint)(OnAsyncAdvanceEnable ? 1 : 0) << 5;
                regValue |= (uint)(HostSystemErrorEnable ? 1 : 0) << 4;
                regValue |= (uint)(FrameListRolloverEnable ? 1 : 0) << 3;
                regValue |= (uint)(PortChangeEnable ? 1 : 0) << 2;
                regValue |= (uint)(USBErrorEnable ? 1 : 0) << 1;
                regValue |= (uint)(Enable ? 1 : 0) << 0;
                return regValue;
            }

            public bool OnAsyncAdvanceEnable = false;
            public bool HostSystemErrorEnable = false;
            public bool FrameListRolloverEnable = false;
            public bool PortChangeEnable = false;
            public bool USBErrorEnable = false;
            public bool Enable = false;
        }

        public class PTDheader
        {
            public
            PTDheader()
            {
                V = 0;
                NrBytesToTransfer = 0;
                MaxPacketLength = 0;
                Mult = 0;
                EndPt = 0;
                DeviceAddress = 0;
                Token = 0;
                EPType = 0;
                S = 0;
                SE = 0;
                PortNumber = 0;
                HubAddress = 0;
                DataStartAddress = 0;
                RL = 0;
                NrBytesTransferred = 0;
                NakCnt = 0;
                Cerr = 0;
                DT = 0;
                SC = 0;
                X = 0;
                B = 0;
                H = 0;
                A = 0;
                NextPTDPointer = 0;
                J = 0;
            }

            public void Transferred(uint amount)
            {
                NrBytesTransferred += amount;
            }

            public void Bubble()
            {
                B = 1;
                V = 0;
                A = 0;
            }

            public void Stalled()
            {
                H = 1;
                V = 0;
                A = 0;
            }

            public void Done()
            {
                V = 0;
                A = 0;
            }

            public void Nak()
            {
                V = 0;
                A = 0;
                NakCnt = 0;
            }

            public uint V ;
            public uint NrBytesToTransfer ;
            public uint MaxPacketLength ;
            public uint Mult ;
            public uint EndPt ;
            public byte DeviceAddress;
            public uint Token ;
            public uint EPType ;
            public uint S ;
            public uint SE;
            public byte PortNumber ;
            public byte HubAddress ;
            public uint DataStartAddress ;
            public uint RL ;
            public uint NrBytesTransferred ;
            public uint NakCnt ;
            public uint Cerr ;
            public uint DT ;
            public uint SC ;
            public uint X ;
            public uint B ;
            public uint H ;
            public uint A ;
            public uint NextPTDPointer ;
            public uint J ;
        }

        public class PTD
        {
            public uint DW0
            {
                get;
                set;
            }

            public uint DW1
            {
                get;
                set;
            }

            public uint DW2
            {
                get;
                set;
            }

            public uint DW3
            {
                get;
                set;
            }

            public uint DW4
            {
                get;
                set;
            }

            public uint DW5
            {
                get;
                set;
            }

            public uint DW6
            {
                get;
                set;
            }

            public uint DW7
            {
                get;
                set;
            }
        };

        public enum InterruptMask : uint //same mask for Int Enable ane USB Status registers
        {
            InterruptOnAsyncAdvance = (uint)(1 << 5),
            HostSystemError = (uint)(1 << 4),
            FrameListRollover = (uint)(1 << 3),
            PortChange = (uint)(1 << 2),
            USBError = (uint)(1 << 1),
            USBInterrupt = (uint)(1 << 0)
        }

        protected enum PIDCode
        {
            Out = 0,
            In = 1,
            Setup = 2
        }

        private enum Offset : uint
        {
            /* capability registers ... */
            CapabilityLength = 0x00,
            StructuralParameters = 0x04,
            CapabilityParameters = 0x08,
            CompanionPortRouting1 = 0x0C,
            CompanionPortRouting2 = 0x10,

            /* operational registers ... */
            UsbCommand = OpBase,
            UsbStatus = OpBase + 0x04,
            UsbInterruptEnable = OpBase + 0x08,
            UsbFrameIndex = OpBase + 0x0C,
            ControlSegment = OpBase + 0x10,
            FrameListBaseAddress = OpBase + 0x14,
            AsyncListAddress = OpBase + 0x18,
            ConfiguredFlag = OpBase + 0x40,
            PortStatusControl = OpBase + 0x44,

            /* Additional EHCI operational registers */
            ISOPTDDoneMap = 0x0130,
            ISOPTDSkipMap = 0x0134,
            ISOPTDLastPTD = 0x0138,
            INTPTDDoneMap = 0x0140,
            INTPTDSkipMap = 0x0144,
            INTPTDLastPTD = 0x0148,
            ATLPTDDoneMap = 0x0150,
            ATLPTDSkipMap = 0x0154,
            ATLPTDLastPTD = 0x0158,

            /* Configuration registers */
            HWModeControl = 0x0300,
            ChipID = 0x0304,
            Scratch = 0x0308,
            SWReset = 0x030C,
            DMAConfiguration = 0x0330,
            BufferStatus = 0x0334,
            ATLDoneTimeout = 0x0338,
            Memory = 0x033C,
            EdgeInterruptCount = 0x0340,
            DMAStartAddress = 0x0344,
            PowerDownControl = 0x0354,
            Port1Control = 0x0374,

            /* Interrupt registers */
            Interrupt = 0x0310,
            InterruptEnable = 0x0314,
            ISOIRQMaskOR = 0x0318,
            INTIRQMaskOR = 0x031C,
            ATLIRQMaskOR = 0x0320,
            ISOIRQMaskAND = 0x0324,
            INTIRQMaskAND = 0x0328,
            ATLIRQMaskAND = 0x032C
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
}