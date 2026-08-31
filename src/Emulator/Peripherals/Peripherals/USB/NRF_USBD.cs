//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.USB
{
    public class NRF_USBD : IUSBDevice, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize, INRFEventProvider
    {
        public NRF_USBD(IMachine machine, short maximumPacketSize = 64)
        {
            this.machine = machine;
            USBCore = new USBDeviceCore(this, customSetupPacketHandler: HandleSetupPacket);
            registers = new DoubleWordRegisterCollection(this);
            IRQ = new GPIO();
            interruptManager = new InterruptManager<Events>(this, IRQ, "UsbIrq");
            events = new IFlagRegisterField[(int)Events.EpData + 1];
            for(var i = 0; i < EndpointCount; i++)
            {
                epOutBuffers[i] = new Queue<byte[]>();
            }
            this.maximumPacketSize = maximumPacketSize;
            InitiateUSBCore();
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void Reset()
        {
            interruptManager.Reset();
            registers.Reset();
            eventCauseReady = false;
            eventCauseSuspend = false;
            ep0Buffer.Clear();
            for(var i = 0; i < EndpointCount; i++)
            {
                lock(epOutBuffers[i])
                {
                    epOutBuffers[i].Clear();
                }
                epInDataStatus[i] = false;
                epInStatus[i] = false;
                epOutDataStatus[i] = false;
                epOutStatus[i] = false;
            }
        }

        public USBDeviceCore USBCore { get; }

        [IrqProvider]
        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public event Action<uint> EventTriggered;

        private void HandleSetupPacket(SetupPacket packet, byte[] arg2, Action<byte[]> action)
        {
            this.Log(LogLevel.Noisy, "Received SetupPacket. Request: 0x{0:X2}, Value: 0x{1:X4}, Index: 0x{2:X4}, Length: 0x{3:X4}", packet.Request, packet.Value, packet.Index, packet.Count);
            setupPacket = packet;
            setupPacketAdditionalData = arg2;
            epOutSize[0] = (uint)(arg2?.Length ?? 0);
            ep0Buffer.Clear();
            setupPacketResultCallback = action;
            SetEvent(Events.Ep0Setup);

            switch(packet.Request)
            {
            case (byte)StandardRequest.SetAddress:
                USBCore.Address = (byte)setupPacket.Value;
                break;
            }
        }

        private void GetData(ushort epNumber)
        {
            this.Log(LogLevel.Noisy, "Reading data from EP IN number: {0}", epNumber);
            uint endpointIn = epInPtr[epNumber];
            uint endpointInCount = epInMaxCnt[epNumber];
            var usbPacket = machine.GetSystemBus(this).ReadBytes(endpointIn, (int)endpointInCount);
            epInAmount[epNumber] = endpointInCount;

            if(epNumber == 0)
            {
                ep0Buffer.AddRange(usbPacket);
                if(usbPacket.Length < maximumPacketSize || ep0Buffer.Count >= setupPacket.Count)
                {
                    var fullData = ep0Buffer.ToArray();
                    ep0Buffer.Clear();
                    setupPacketAdditionalData = null;
                    var cb = setupPacketResultCallback;
                    setupPacketResultCallback = null;
                    cb?.Invoke(fullData);
                }
            }
            else if(usbPacket.Length != 0)
            {
                this.Log(LogLevel.Noisy, "Sending {0} bytes from EP IN {1} to host", usbPacket.Length, epNumber);
                deviceToHostEndpoints[epNumber]?.HandlePacket(usbPacket);
            }
            DataAcknowledged(epNumber);
        }

        private void OnTaskEp0RcvOut(ushort epNumber)
        {
            this.Log(LogLevel.Noisy, "TASKS_EP0RCVOUT called");
            if(setupPacketAdditionalData != null && setupPacketAdditionalData.Length > 0)
            {
                SetEvent(Events.Ep0DataDone);
            }
        }

        private void OnTaskStartEpOut0(ushort epNumber)
        {
            this.Log(LogLevel.Noisy, "TASKS_STARTEPOUT0 called, epOutPtr[0]=0x{0:X}, len={1}", epOutPtr[0], epOutSize[0]);
            if(setupPacketAdditionalData != null && setupPacketAdditionalData.Length > 0 && epOutPtr[0] != 0)
            {
                uint endpointOut = epOutPtr[0];
                machine.GetSystemBus(this).WriteBytes(setupPacketAdditionalData, endpointOut);
                epOutAmount[0] = (uint)setupPacketAdditionalData.Length;
            }
            SetEvent(Events.Started);
            SetEvent(Events.EndEpOut0);
        }

        private void HandleEpOut(ushort epNumber)
        {
            this.Log(LogLevel.Noisy, "HandleEpOut task called on EP {0}, epOutPtr=0x{1:X}", epNumber, epOutPtr[epNumber]);
            lock(epOutBuffers[epNumber])
            {
                if(epOutBuffers[epNumber].Count > 0)
                {
                    var data = epOutBuffers[epNumber].Dequeue();
                    uint maxCnt = epOutMaxCnt[epNumber];
                    int toCopy = Math.Min((int)maxCnt, data.Length);
                    machine.GetSystemBus(this).WriteBytes(data, epOutPtr[epNumber], startingIndex: 0, count: toCopy);
                    epOutAmount[epNumber] = (uint)toCopy;
                    epOutSize[epNumber] = (uint)(data.Length - toCopy);
                }
                else
                {
                    epOutAmount[epNumber] = 0;
                    epOutSize[epNumber] = 0;
                }
            }
            epOutDataStatus[epNumber] = false;
            SetEvent(Events.Started);
            SetEvent(Events.EndEpOut0 + epNumber);
            SetEvent(Events.EpData);
        }

        private void OnDataWritten(ushort epNumber, byte[] data)
        {
            this.Log(LogLevel.Noisy, "Host wrote {0} bytes to EP {1}", data.Length, epNumber);
            lock(epOutBuffers[epNumber])
            {
                epOutBuffers[epNumber].Enqueue(data);
            }
            epOutDataStatus[epNumber] = true;
            epOutSize[epNumber] = (uint)data.Length;
            SetEvent(Events.EpData);
        }

        private void DataAcknowledged(ushort epNumber)
        {
            SetEvent(Events.Started);
            SetEvent(Events.EndEpIn0 + epNumber);

            if(epNumber == 0)
            {
                SetEvent(Events.Ep0DataDone);
            }
            else
            {
                epInDataStatus[epNumber] = true;
                SetEvent(Events.EpData);
            }
        }

        private void SetEvent(Events @event)
        {
            interruptManager.SetInterrupt(@event);
            events[(int)@event].Value = true;
            // Events registers start at 0x100, they are apart of each other by 4 bytes.
            EventTriggered?.Invoke((uint)@event * 4 + 0x100);
        }

        private void DefineTask(Registers register, Action<ushort> callback, ushort epNumber, string name)
        {
            register.Define(this, name: name)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) callback(epNumber); })
                .WithReservedBits(1, 31);
        }

        private void DefineEvent(Registers register, Events @event, string name)
        {
            register.Define(this, name: name)
                .WithFlag(0, out events[(int)@event], writeCallback: (_, value) =>
                {
                    if(!value)
                    {
                        interruptManager.SetInterrupt(@event, false);
                    }
                })
                .WithReservedBits(1, 31);
        }

        private void DefineRegisters()
        {
            for(ushort i = 0; i < EndpointCount; i++)
            {
                DefineTask(Registers.TasksStartEpIn0 + i, GetData, i, $"TASKS_STARTEPIN{i}");
            }
            DefineTask(Registers.TasksEp0Status, _ =>
            {
                var cb = setupPacketResultCallback;
                setupPacketResultCallback = null;
                cb?.Invoke(Array.Empty<byte>());
            }, 0, "TASKS_EP0STATUS");
            DefineTask(Registers.TasksEp0Stall, _ =>
            {
                var cb = setupPacketResultCallback;
                setupPacketResultCallback = null;
                cb?.Invoke(null);
            }, 0, "TASKS_EP0STALL");
            DefineTask(Registers.TasksEp0RcvOut, OnTaskEp0RcvOut, 0, "TASKS_EP0RCVOUT");
            DefineTask(Registers.TasksStartEpOut0, OnTaskStartEpOut0, 0, "TASKS_STARTEPOUT0");
            for(ushort i = 1; i < EndpointCount; i++)
            {
                DefineTask(Registers.TasksStartEpOut0 + i, HandleEpOut, i, $"TASKS_STARTEPOUT{i}");
            }

            DefineEvent(Registers.EventsUsbReset, Events.UsbReset, "EVENTS_USBRESET");
            DefineEvent(Registers.EventsEp0Setup, Events.Ep0Setup, "EVENTS_EP0SETUP");
            DefineEvent(Registers.EventsStarted, Events.Started, "EVENTS_STARTED");
            for(var i = 0; i < EndpointCount; i++)
            {
                DefineEvent(Registers.EventsEndEpIn0 + i, Events.EndEpIn0 + i, $"EVENTS_ENDEPIN{i}");
                DefineEvent(Registers.EventsEndEpOut0 + i, Events.EndEpOut0 + i, $"EVENTS_ENDEPOUT{i}");
            }
            DefineEvent(Registers.EventsStartOfFrame, Events.StartOfFrame, "EVENTS_SOF");
            DefineEvent(Registers.EventsUsbEvent, Events.UsbEvent, "EVENTS_USBEVENT");
            DefineEvent(Registers.EventsEp0DataDone, Events.Ep0DataDone, "EVENTS_EP0DATADONE");
            DefineEvent(Registers.EventsEpData, Events.EpData, "EVENTS_EPDATA");

            registers.AddRegister((long)Registers.InterruptEnable,
                interruptManager.GetInterruptEnableSetRegister<DoubleWordRegister>());
            registers.AddRegister((long)Registers.InterruptEnableSet,
                interruptManager.GetInterruptEnableSetRegister<DoubleWordRegister>());
            registers.AddRegister((long)Registers.InterruptEnableClear,
                interruptManager.GetInterruptEnableClearRegister<DoubleWordRegister>());

            Registers.EventCause.Define(this)
                .WithTaggedFlag("EVENT_ISOOUTCRC", 0)
                .WithFlag(8, writeCallback: (_, val) => { if(val) eventCauseSuspend = false; }, valueProviderCallback: _ => eventCauseSuspend, name: "EVENT_SUSPEND")
                .WithTaggedFlag("EVENT_RESUME", 9)
                .WithTaggedFlag("EVENT_USBWUALLOWED", 10)
                .WithFlag(11, writeCallback: (_, val) => { if(val) eventCauseReady = false; }, valueProviderCallback: _ => eventCauseReady, name: "EVENT_READY")
                .WithReservedBits(12, 20);

            var epStatusReg = Registers.EndpointStatus.Define(this);
            var epDataStatusReg = Registers.EndpointDataStatus.Define(this);
            for(var i = 0; i < EndpointCount; i++)
            {
                var epIndex = i;
                epStatusReg
                    .WithFlag(i, writeCallback: (_, val) => { if(val) epInStatus[epIndex] = false; }, valueProviderCallback: _ => epInStatus[epIndex], name: $"EPIN{i}")
                    .WithFlag(16 + i, writeCallback: (_, val) => { if(val) epOutStatus[epIndex] = false; }, valueProviderCallback: _ => epOutStatus[epIndex], name: $"EPOUT{i}");
                epDataStatusReg
                    .WithFlag(i, writeCallback: (_, val) => { if(val) epInDataStatus[epIndex] = false; }, valueProviderCallback: _ => epInDataStatus[epIndex], name: $"EPIN{i}")
                    .WithFlag(16 + i, writeCallback: (_, val) => { if(val) epOutDataStatus[epIndex] = false; }, valueProviderCallback: _ => epOutDataStatus[epIndex], name: $"EPOUT{i}");
            }
            epStatusReg.WithReservedBits(8, 8).WithReservedBits(24, 8);
            epDataStatusReg.WithReservedBits(8, 8).WithReservedBits(24, 8);

            Registers.UsbAddress.Define(this)
                .WithValueField(0, 7, name: "ADDR", valueField: out usbAddress)
                .WithReservedBits(7, 25);

            Registers.bmRequestType.Define(this)
                .WithValueField(0, 8, name: "BMREQUESTTYPE", valueProviderCallback: _ => ((setupPacket.Direction == Direction.DeviceToHost ? 1u : 0u) << 7) | (((uint)setupPacket.Type & 0x3) << 5) | ((uint)setupPacket.Recipient & 0x1F))
                .WithReservedBits(8, 24);

            Registers.bRequest.Define(this)
                .WithValueField(0, 8, name: "BREQUEST", valueProviderCallback: _ => (uint)setupPacket.Request)
                .WithReservedBits(8, 24);

            void DefineLowHigh(Registers lowReg, Registers highReg, string prefix, Func<uint> valGetter)
            {
                lowReg.Define(this).WithValueField(0, 8, name: $"{prefix}LOW", valueProviderCallback: _ => valGetter() & 0xFF).WithReservedBits(8, 24);
                highReg.Define(this).WithValueField(0, 8, name: $"{prefix}HIGH", valueProviderCallback: _ => (valGetter() >> 8) & 0xFF).WithReservedBits(8, 24);
            }

            DefineLowHigh(Registers.wValueLow, Registers.wValueHigh, "WVALUE", () => setupPacket.Value);
            DefineLowHigh(Registers.wIndexLow, Registers.wIndexHigh, "WINDEX", () => setupPacket.Index);
            DefineLowHigh(Registers.wLengthLow, Registers.wLengthHigh, "WLENGTH", () => setupPacket.Count);

            Registers.Enable.Define(this)
                .WithFlag(0, out usbEnable, writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        eventCauseReady = true;
                        SetEvent(Events.UsbEvent);
                    }
                }, name: "ENABLE")
                .WithReservedBits(1, 31);

            Registers.UsbPullup.Define(this)
                .WithFlag(0, out usbPullup, name: "CONNECT")
                .WithReservedBits(1, 31);

            Registers.DataToggle.Define(this)
                .WithValueField(0, 3, out dataToggleEndpoint, name: "EP")
                .WithReservedBits(3, 4)
                .WithFlag(7, out dataToggleInputOutput, name: "IO")
                .WithValueField(8, 2, valueField: out dataToggleValue, name: "VALUE")
                .WithReservedBits(10, 22)
                .WithWriteCallback((_, __) => HandleToggle());

            Registers.EndpointInEnable.Define(this)
                .WithValueField(0, 9, name: "EPINEN")
                .WithReservedBits(9, 23);

            Registers.EndpointOutEnable.Define(this)
                .WithValueField(0, 9, name: "EPOUTEN")
                .WithReservedBits(9, 23);

            Registers.EndpointStall.Define(this)
                .WithValueField(0, 3, out epstallEndpoint, name: "EP")
                .WithReservedBits(3, 4)
                .WithFlag(7, out epstallIO, name: "IO")
                .WithFlag(8, out epstallStall, name: "STALL")
                .WithReservedBits(9, 23)
                .WithWriteCallback((_, __) => HandleStalling());

            // SIZE.EPOUT registers (0x4A0..0x4BC) and SIZE.ISOOUT (0x4C0)
            for(var i = 0; i < EndpointCount; i++)
            {
                var epIndex = i;
                registers.AddRegister(0x4A0 + (i * 4), new DoubleWordRegister(this).WithValueField(0, 16, name: $"SIZE_EPOUT{i}",
                    valueProviderCallback: _ => epOutSize[epIndex]));
            }
            registers.AddRegister(0x4C0, new DoubleWordRegister(this).WithValueField(0, 16, name: "SIZE_ISOOUT"));

            for(var i = 0; i < EndpointCount; i++)
            {
                var epOffset = (long)Registers.HaltedEndpointIn0 + (i * 4);
                registers.AddRegister(epOffset, new DoubleWordRegister(this).WithValueField(0, 1, name: $"HALTED_EPIN{i}").WithReservedBits(1, 31));
                var epOutHaltedOffset = (long)Registers.HaltedEndpointOut0 + (i * 4);
                registers.AddRegister(epOutHaltedOffset, new DoubleWordRegister(this).WithValueField(0, 1, name: $"HALTED_EPOUT{i}").WithReservedBits(1, 31));
            }

            Registers.IsoSplit.Define(this)
                .WithValueField(0, 16, name: "SPLIT")
                .WithReservedBits(16, 16);

            Registers.IsoInConfig.Define(this)
                .WithTaggedFlag("RESPONSE", 0)
                .WithReservedBits(1, 31);

            for(var i = 0; i < EndpointCount; i++)
            {
                var epIndex = i;
                registers.AddRegister((long)Registers.Endpoint0In + (0x14 * i),
                    new DoubleWordRegister(this).WithValueField(0, 32, name: $"EPIN{i}_PTR",
                        writeCallback: (_, val) => epInPtr[epIndex] = (uint)val,
                        valueProviderCallback: _ => epInPtr[epIndex]));
                registers.AddRegister((long)Registers.Endpoint0InCount + (0x14 * i),
                    new DoubleWordRegister(this).WithValueField(0, 8, name: $"EPIN{i}_MAXCNT",
                        writeCallback: (_, val) => epInMaxCnt[epIndex] = (uint)val,
                        valueProviderCallback: _ => epInMaxCnt[epIndex]).WithReservedBits(8, 24));
                registers.AddRegister((long)Registers.Endpoint0InAmount + (0x14 * i),
                    new DoubleWordRegister(this).WithValueField(0, 8, FieldMode.Read, name: $"EPIN{i}_AMOUNT",
                        valueProviderCallback: _ => epInAmount[epIndex]).WithReservedBits(8, 24));

                registers.AddRegister((long)Registers.Endpoint0Out + (0x14 * i),
                    new DoubleWordRegister(this).WithValueField(0, 32, name: $"EPOUT{i}_PTR",
                        writeCallback: (_, val) => {
                            epOutPtr[epIndex] = (uint)val;
                            if(epIndex == 0 && setupPacketAdditionalData != null && setupPacketAdditionalData.Length > 0 && (uint)val != 0)
                            {
                                machine.GetSystemBus(this).WriteBytes(setupPacketAdditionalData, (uint)val);
                                epOutAmount[0] = (uint)setupPacketAdditionalData.Length;
                            }
                        },
                        valueProviderCallback: _ => epOutPtr[epIndex]));
                registers.AddRegister((long)Registers.Endpoint0OutCount + (0x14 * i),
                    new DoubleWordRegister(this).WithValueField(0, 8, name: $"EPOUT{i}_MAXCNT",
                        writeCallback: (_, val) => epOutMaxCnt[epIndex] = (uint)val,
                        valueProviderCallback: _ => epOutMaxCnt[epIndex]).WithReservedBits(8, 24));
                registers.AddRegister((long)Registers.Endpoint0OutAmount + (0x14 * i),
                    new DoubleWordRegister(this).WithValueField(0, 8, FieldMode.Read, name: $"EPOUT{i}_AMOUNT",
                        valueProviderCallback: _ => epOutAmount[epIndex]).WithReservedBits(8, 24));
            }
        }

        private void HandleToggle()
        {
            if(dataToggleValue.Value == 0)
            {
                this.Log(LogLevel.Noisy, "Selecting EP #{0}, {1}", dataToggleEndpoint.Value, dataToggleInputOutput.Value ? "in" : "out");
                return;
            }
            this.Log(LogLevel.Noisy, "Accessing EP #{0}, {1}; DATA{2}", dataToggleEndpoint.Value, dataToggleInputOutput.Value == false ? "out" : "in", dataToggleValue.Value == 1 ? "0" : "1");
        }

        private void HandleStalling()
        {
            this.Log(LogLevel.Noisy, "{0} EP #{1}, {2}", epstallStall.Value == true ? "Stalling" : "Unstalling", epstallEndpoint.Value, epstallIO.Value == false ? "out" : "in");
        }

        private void InitiateUSBCore()
        {
            var config = new USBConfiguration(this, 0, "").WithInterface(
                configure: x =>
                {
                    for(byte i = 0; i < EndpointCount; i++)
                    {
                        var epIndex = i;
                        var transferType = i == 0 ? EndpointTransferType.Control : EndpointTransferType.Bulk;
                        x.WithEndpoint(
                            Direction.DeviceToHost,
                            transferType,
                            maximumPacketSize,
                            0x10,
                            out deviceToHostEndpoints[epIndex],
                            id: i);
                        x.WithEndpoint(
                            Direction.HostToDevice,
                            transferType,
                            maximumPacketSize,
                            0x10,
                            out hostToDeviceEndpoints[epIndex],
                            id: i);
                        if(i > 0)
                        {
                            hostToDeviceEndpoints[epIndex].DataWritten += data => OnDataWritten(epIndex, data);
                        }
                    }
                });
            USBCore.SelectedConfiguration = config;
        }

        DoubleWordRegisterCollection IProvidesRegisterCollection<DoubleWordRegisterCollection>.RegistersCollection => registers;

        private SetupPacket setupPacket;
        private byte[] setupPacketAdditionalData;

        private IValueRegisterField dataToggleEndpoint;
        private IFlagRegisterField dataToggleInputOutput;
        private IValueRegisterField dataToggleValue;

        private IValueRegisterField usbAddress;
        private IValueRegisterField epstallEndpoint;
        private IFlagRegisterField epstallIO;
        private IFlagRegisterField epstallStall;

        private IFlagRegisterField usbPullup;
        private IFlagRegisterField usbEnable;
        private bool eventCauseReady;
        private bool eventCauseSuspend;

        private readonly USBEndpoint[] deviceToHostEndpoints = new USBEndpoint[EndpointCount];
        private readonly USBEndpoint[] hostToDeviceEndpoints = new USBEndpoint[EndpointCount];
        private readonly Queue<byte[]>[] epOutBuffers = new Queue<byte[]>[EndpointCount];
        private Action<byte[]> setupPacketResultCallback;
        private readonly IMachine machine;
        private readonly bool[] epInDataStatus = new bool[EndpointCount];
        private readonly bool[] epInStatus = new bool[EndpointCount];
        private readonly bool[] epOutDataStatus = new bool[EndpointCount];
        private readonly bool[] epOutStatus = new bool[EndpointCount];
        private readonly List<byte> ep0Buffer = new List<byte>();

        private readonly uint[] epInPtr = new uint[EndpointCount];
        private readonly uint[] epInMaxCnt = new uint[EndpointCount];
        private readonly uint[] epInAmount = new uint[EndpointCount];
        private readonly uint[] epOutPtr = new uint[EndpointCount];
        private readonly uint[] epOutMaxCnt = new uint[EndpointCount];
        private readonly uint[] epOutAmount = new uint[EndpointCount];
        private readonly uint[] epOutSize = new uint[EndpointCount];

        private readonly InterruptManager<Events> interruptManager;
        private readonly IFlagRegisterField[] events;

        private readonly short maximumPacketSize;
        private readonly DoubleWordRegisterCollection registers;

        private const ushort EndpointCount = 8;

        private enum Events
        {
            UsbReset = 0,
            Started = 1,
            EndEpIn0 = 2,
            EndEpIn1 = 3,
            EndEpIn2 = 4,
            EndEpIn3 = 5,
            EndEpIn4 = 6,
            EndEpIn5 = 7,
            EndEpIn6 = 8,
            EndEpIn7 = 9,
            Ep0DataDone = 10,
            EndIsoIn = 11,
            EndEpOut0 = 12,
            EndEpOut1 = 13,
            EndEpOut2 = 14,
            EndEpOut3 = 15,
            EndEpOut4 = 16,
            EndEpOut5 = 17,
            EndEpOut6 = 18,
            EndEpOut7 = 19,
            EndIsoOut = 20,
            StartOfFrame = 21,
            UsbEvent = 22,
            Ep0Setup = 23,
            EpData = 24
        }

        private enum Registers : long
        {
            TasksStartEpIn0 = 0x004,
            TasksStartEpIn1 = 0x008,
            TasksStartEpIn2 = 0x00C,
            TasksStartEpIn3 = 0x010,
            TasksStartEpIn4 = 0x014,
            TasksStartEpIn5 = 0x018,
            TasksStartEpIn6 = 0x01C,
            TasksStartEpIn7 = 0x020,
            TasksStartIsoIn = 0x024,
            TasksStartEpOut0 = 0x028,
            TasksStartEpOut1 = 0x02C,
            TasksStartEpOut2 = 0x030,
            TasksStartEpOut3 = 0x034,
            TasksStartEpOut4 = 0x038,
            TasksStartEpOut5 = 0x03C,
            TasksStartEpOut6 = 0x040,
            TasksStartEpOut7 = 0x044,
            TasksStartIsoOut = 0x048,
            TasksEp0RcvOut = 0x04C,
            TasksEp0Status = 0x050,
            TasksEp0Stall = 0x054,
            TasksDPDMDrive = 0x058,
            TasksDPDMNODrive = 0x05C,
            EventsUsbReset = 0x100,
            EventsStarted = 0x104,
            EventsEndEpIn0 = 0x108,
            EventsEndEpIn1 = 0x10C,
            EventsEndEpIn2 = 0x110,
            EventsEndEpIn3 = 0x114,
            EventsEndEpIn4 = 0x118,
            EventsEndEpIn5 = 0x11C,
            EventsEndEpIn6 = 0x120,
            EventsEndEpIn7 = 0x124,
            EventsEp0DataDone = 0x128,
            EventsEndIsoIn = 0x12C,
            EventsEndEpOut0 = 0x130,
            EventsEndEpOut1 = 0x134,
            EventsEndEpOut2 = 0x138,
            EventsEndEpOut3 = 0x13C,
            EventsEndEpOut4 = 0x140,
            EventsEndEpOut5 = 0x144,
            EventsEndEpOut6 = 0x148,
            EventsEndEpOut7 = 0x14C,
            EventsEndIsoOut = 0x150,
            EventsStartOfFrame = 0x154,
            EventsUsbEvent = 0x158,
            EventsEp0Setup = 0x15C,
            EventsEpData = 0x160,
            InterruptEnable = 0x300,
            InterruptEnableSet = 0x304,
            InterruptEnableClear = 0x308,
            UsbPullup = 0x504,
            DataToggle = 0x50C,
            IsoSplit = 0x51C,
            IsoInConfig = 0x530,
            HaltedEndpointOut0 = 0x420,
            HaltedEndpointIn0 = 0x440,
            EndpointStall = 0x518,
            EventCause = 0x400,
            EndpointStatus = 0x468,
            EndpointDataStatus = 0x46c,
            UsbAddress = 0x470,
            bmRequestType = 0x480,
            bRequest = 0x484,
            wValueLow = 0x488,
            wValueHigh = 0x48C,
            wIndexLow = 0x490,
            wIndexHigh = 0x494,
            wLengthLow = 0x498,
            wLengthHigh = 0x49C,
            Enable = 0x500,
            EndpointInEnable = 0x510,
            EndpointOutEnable = 0x514,
            Endpoint0In = 0x600,
            Endpoint0InCount = 0x604,
            Endpoint0InAmount = 0x608,
            Endpoint0Out = 0x700,
            Endpoint0OutCount = 0x704,
            Endpoint0OutAmount = 0x708,
        }
    }
}