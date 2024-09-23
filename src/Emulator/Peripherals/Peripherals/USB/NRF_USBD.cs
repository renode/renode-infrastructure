//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Collections.Generic;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.USB;
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
            epInDataStatus = new bool[EndpointCount];
            epInStatus = new bool[EndpointCount];
            this.maximumPacketSize = maximumPacketSize;
            InitiateUSBCore();
            DefineRegisters();
        }
        
        public void Reset()
        {
            interruptManager.Reset();
            registers.Reset();
        }

        public USBDeviceCore USBCore { get; }

        public uint ReadDoubleWord(long offset) 
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        [IrqProvider]
        public GPIO IRQ { get; }
        public long Size => 0x1000;

        public event Action<uint> EventTriggered;

        private void HandleSetupPacket(SetupPacket packet, byte[] arg2, Action<byte[]> action)
        {
            // Note: this method handled some setup packets in the model instead of relying them to the simulated software
            // This is a simplification that needs to be resolved in the future
            this.Log(LogLevel.Noisy, "Received SetupPacket. Request: {0}", packet.Request);
            setupPacket = packet;
            SetEvent(Events.Ep0Setup);
            setupPacketResultCallback = action;

            switch(packet.Request)
            {
                case (byte)StandardRequest.SetAddress:
                    USBCore.Address = (byte)setupPacket.Value;
                    setupPacketResultCallback(Array.Empty<byte>());
                    break;
                case (byte)StandardRequest.SetConfiguration:
                    setupPacketResultCallback(Array.Empty<byte>());
                    break;
            }
        }

        private void GetData(ushort epNumber) 
        {
            this.Log(LogLevel.Noisy, "Reading data from EP number: {0}", epNumber);
            // Every pointer to endpoint data and endpoint count is n * 0x14 away from endpoint's 0, where n is number of endpoint. 
            // E.g: pointer to second endpoint data would be: (2 * 0x14 + address of endpoint 0) 
            uint endpointIn = registers.Read((0x14 * epNumber) + (long)Registers.Endpoint0In);
            uint endpointInCount = registers.Read((0x14 * epNumber) + (long)Registers.Endpoint0InCount);
            var usbPacket = machine.GetSystemBus(this).ReadBytes(endpointIn, (int)endpointInCount);

            if(epNumber == 0)
            {
                setupPacketResultCallback(usbPacket);
                endpoint0InCount.Value = endpointInCount;
            }
            else if(usbPacket.Length != 0)
            {
                deviceToHostEndpoint.HandlePacket(usbPacket);
            }
            DataAcknowledged(epNumber);
        }

        private void DataAcknowledged(ushort epNumber)
        {
            epInDataStatus[epNumber] = true;

            SetEvent(Events.Started);
            SetEvent(Events.EndEpIn0 + epNumber);
            SetEvent(Events.EpData);

            // Special event for control endpoint
            if(epNumber == 0) 
            {
                SetEvent(Events.Ep0DataDone);
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
            DefineTask(Registers.TasksStartEpIn0, GetData, 0, "TASKS_STARTEPIN0");
            DefineTask(Registers.TasksStartEpIn1, GetData, 1, "TASKS_STARTEPIN1");
            DefineTask(Registers.TasksStartEpIn2, GetData, 2, "TASKS_STARTEPIN2");
            DefineTask(Registers.TasksStartEpIn3, GetData, 3, "TASKS_STARTEPIN3");
            DefineTask(Registers.TasksStartEpIn4, GetData, 4, "TASKS_STARTEPIN4");
            DefineTask(Registers.TasksStartEpIn5, GetData, 5, "TASKS_STARTEPIN5");
            DefineTask(Registers.TasksStartEpIn6, GetData, 6, "TASKS_STARTEPIN6");
            DefineTask(Registers.TasksStartEpIn7, GetData, 7, "TASKS_STARTEPIN7");
            DefineTask(Registers.TasksEp0Status, (_) => {}, 0, "TASKS_EP0STATUS");
            DefineEvent(Registers.EventsUsbReset, Events.UsbReset, "EVENTS_USBRESET");
            DefineEvent(Registers.EventsEp0Setup, Events.Ep0Setup, "EVENTS_EP0SETUP");
            DefineEvent(Registers.EventsStarted, Events.Started, "EVENTS_STARTED");
            DefineEvent(Registers.EventsEndEpIn0, Events.EndEpIn0, "EVENTS_ENDEPIN0");
            DefineEvent(Registers.EventsEndEpIn1, Events.EndEpIn1, "EVENTS_ENDEPIN1");
            DefineEvent(Registers.EventsEndEpIn2, Events.EndEpIn2, "EVENTS_ENDEPIN2");
            DefineEvent(Registers.EventsEndEpIn3, Events.EndEpIn3, "EVENTS_ENDEPIN3");
            DefineEvent(Registers.EventsEndEpIn4, Events.EndEpIn4, "EVENTS_ENDEPIN4");
            DefineEvent(Registers.EventsEndEpIn5, Events.EndEpIn5, "EVENTS_ENDEPIN5");
            DefineEvent(Registers.EventsEndEpIn6, Events.EndEpIn6, "EVENTS_ENDEPIN6");
            DefineEvent(Registers.EventsEndEpIn7, Events.EndEpIn7, "EVENTS_ENDEPIN7");
            DefineEvent(Registers.EventsEp0DataDone, Events.Ep0DataDone, "EVENTS_EP0DATADONE");
            DefineEvent(Registers.EventsEpData, Events.EpData, "EVENTS_EPDATA");

            registers.AddRegister((long)Registers.InterruptEnable, 
                interruptManager.GetInterruptEnableSetRegister<DoubleWordRegister>());

            Registers.EventCause.Define(this)
                .WithTaggedFlag("EVENT_ISOOUTCRC", 0)
                .WithTaggedFlag("EVENT_SUSPEND", 8)
                .WithTaggedFlag("EVENT_RESUME", 9)
                .WithTaggedFlag("EVENT_USBWUALLOWED", 10)
                .WithFlag(11, name: "EVENT_READY")
                .WithReservedBits(12, 20);

            Registers.EndpointStatus.Define(this)
                .WithFlag(0, writeCallback: (_, val) => {epInStatus[0] = val;}, valueProviderCallback: _ => epInStatus[0], name: "EPIN1")
                .WithFlag(1, writeCallback: (_, val) => {epInStatus[1] = val;}, valueProviderCallback: _ => epInStatus[1], name: "EPIN1")
                .WithFlag(2, writeCallback: (_, val) => {epInStatus[2] = val;}, valueProviderCallback: _ => epInStatus[2], name: "EPIN2")
                .WithFlag(3, writeCallback: (_, val) => {epInStatus[3] = val;}, valueProviderCallback: _ => epInStatus[3], name: "EPIN3")
                .WithFlag(4, writeCallback: (_, val) => {epInStatus[4] = val;}, valueProviderCallback: _ => epInStatus[4], name: "EPIN4")
                .WithFlag(5, writeCallback: (_, val) => {epInStatus[5] = val;}, valueProviderCallback: _ => epInStatus[5], name: "EPIN5")
                .WithFlag(6, writeCallback: (_, val) => {epInStatus[6] = val;}, valueProviderCallback: _ => epInStatus[6], name: "EPIN6")
                .WithFlag(7, writeCallback: (_, val) => {epInStatus[7] = val;}, valueProviderCallback: _ => epInStatus[7], name: "EPIN7")
                .WithReservedBits(8, 8)
                .WithTaggedFlag("EPOUT0", 16)
                .WithTaggedFlag("EPOUT1", 17)
                .WithTaggedFlag("EPOUT2", 18)
                .WithTaggedFlag("EPOUT3", 19)
                .WithTaggedFlag("EPOUT4", 20)
                .WithTaggedFlag("EPOUT5", 21)
                .WithTaggedFlag("EPOUT6", 22)
                .WithTaggedFlag("EPOUT7", 23)
                .WithTaggedFlag("EPOUT8", 24)
                .WithReservedBits(25, 7);

            Registers.EndpointDataStatus.Define(this)
                .WithReservedBits(0, 1) // Ep0 has no data status
                .WithFlag(1, writeCallback: (_, val) => {epInDataStatus[1] = val;}, valueProviderCallback: _ => epInDataStatus[1], name: "EPIN1")
                .WithFlag(2, writeCallback: (_, val) => {epInDataStatus[2] = val;}, valueProviderCallback: _ => epInDataStatus[2], name: "EPIN2")
                .WithFlag(3, writeCallback: (_, val) => {epInDataStatus[3] = val;}, valueProviderCallback: _ => epInDataStatus[3], name: "EPIN3")
                .WithFlag(4, writeCallback: (_, val) => {epInDataStatus[4] = val;}, valueProviderCallback: _ => epInDataStatus[4], name: "EPIN4")
                .WithFlag(5, writeCallback: (_, val) => {epInDataStatus[5] = val;}, valueProviderCallback: _ => epInDataStatus[5], name: "EPIN5")
                .WithFlag(6, writeCallback: (_, val) => {epInDataStatus[6] = val;}, valueProviderCallback: _ => epInDataStatus[6], name: "EPIN6")
                .WithFlag(7, writeCallback: (_, val) => {epInDataStatus[7] = val;}, valueProviderCallback: _ => epInDataStatus[7], name: "EPIN7")
                .WithReservedBits(8, 9)
                .WithTaggedFlag("EPOUT1", 17)
                .WithTaggedFlag("EPOUT2", 18)
                .WithTaggedFlag("EPOUT3", 19)
                .WithTaggedFlag("EPOUT4", 20)
                .WithTaggedFlag("EPOUT5", 21)
                .WithTaggedFlag("EPOUT6", 22)
                .WithTaggedFlag("EPOUT7", 23)
                .WithReservedBits(24, 8);

            Registers.UsbAddress.Define(this)
                .WithValueField(0, 7, out usbAddress, FieldMode.Read)
                .WithReservedBits(7, 24);

            Registers.bmRequestType.Define(this)
                .WithTag("RECIPIENT", 0, 5)
                .WithValueField(5, 2, FieldMode.Read, valueProviderCallback: _ => 0, name: "TYPE")
                .WithValueField(7, 1, FieldMode.Read, name: "DIRECTION",
                    valueProviderCallback: _ => (ulong)setupPacket.Direction)
                .WithReservedBits(8, 24);

            Registers.bRequest.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => setupPacket.Request)
                .WithReservedBits(8, 24);

            Registers.wValueLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (byte)(setupPacket.Value & 0xFF))
                .WithReservedBits(8, 24);

            Registers.wValueHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (byte)(setupPacket.Value >> 8 & 0xFF))
                .WithReservedBits(8, 24);

            Registers.wIndexLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => setupPacket.Index)
                .WithReservedBits(8, 24);

            Registers.wLengthLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => setupPacket.Count)
                .WithReservedBits(8, 24);

            Registers.Enable.Define(this)
                .WithFlag(0, out usbEnable, name: "ENABLE")
                .WithReservedBits(1, 31);

            Registers.UsbPullup.Define(this)
                .WithFlag(0, out usbPullup, name: "CONNECT")
                .WithReservedBits(1, 31);

            Registers.DataToggle.Define(this)
                .WithValueField(0, 3, valueField: out dataToggleEndpoint, name: "EP")
                .WithFlag(7, out dataToggleInputOutput, name: "IO")
                .WithValueField(8, 2, valueField: out dataToggleValue, name: "VALUE")
                .WithReservedBits(10, 22)
                .WithWriteCallback((_, __) => HandleToggle());

            Registers.EndpointInEnable.Define(this)
                .WithFlag(0, out ep0InEnabled, name: "IN0")
                .WithTaggedFlag("IN1", 1)
                .WithTaggedFlag("IN2", 2)
                .WithTaggedFlag("IN3", 3)
                .WithTaggedFlag("IN4", 4)
                .WithTaggedFlag("IN5", 5)
                .WithTaggedFlag("IN6", 6)
                .WithTaggedFlag("IN7", 7)
                .WithTaggedFlag("ISOIN", 8)
                .WithReservedBits(9, 23);

            Registers.EndpointOutEnable.Define(this)
                .WithFlag(0, out ep0OutEnabled, name: "OUT0")
                .WithTaggedFlag("OUT1", 1)
                .WithTaggedFlag("OUT2", 2)
                .WithTaggedFlag("OUT3", 3)
                .WithTaggedFlag("OUT4", 4)
                .WithTaggedFlag("OUT5", 5)
                .WithTaggedFlag("OUT6", 6)
                .WithTaggedFlag("OUT7", 7)
                .WithTaggedFlag("ISOOUT", 8)
                .WithReservedBits(9, 23);

            Registers.EndpointStall.Define(this)
                .WithValueField(0, 3, out epstallEndpoint, name: "EP")
                .WithReservedBits(3, 4)
                .WithFlag(7, out epstallIO, name: "IO")
                .WithFlag(8, out epstallStall, name: "STALL")
                .WithReservedBits(9, 23)
                .WithWriteCallback((_, __) => HandleStalling());

            Registers.IsoInConfig.Define(this) // This is last thing happening in nRF5340, after this the enumeration should start
                .WithTaggedFlag("RESPONSE", 0)
                .WithReservedBits(1, 31);
                

            Registers.Endpoint0In.Define(this)
                .WithValueField(0, 32, name: "EPIN0", valueField: out endpoint0In);
            Registers.Endpoint0InCount.Define(this)
                .WithValueField(0, 8, name: "EPIN0_MAXCNT", valueField: out endpoint0InCount)
                .WithReservedBits(8, 24);

            Registers.Endpoint1In.Define(this)
                .WithValueField(0, 32, name: "EPIN1");
            Registers.Endpoint1InCount.Define(this)
                .WithValueField(0, 8, name: "EPIN1_MAXCNT")
                .WithReservedBits(8, 24);

            Registers.Endpoint2In.Define(this)
                .WithValueField(0, 32, name: "EPIN2");
            Registers.Endpoint2InCount.Define(this)
                .WithValueField(0, 8, name: "EPIN2_MAXCNT")
                .WithReservedBits(8, 24);

            Registers.Endpoint3In.Define(this)
                .WithValueField(0, 32, name: "EPIN3");
            Registers.Endpoint3InCount.Define(this)
                .WithValueField(0, 8, name: "EPIN3_MAXCNT")
                .WithReservedBits(8, 24);

            Registers.Endpoint4In.Define(this)
                .WithValueField(0, 32, name: "EPIN4");
            Registers.Endpoint4InCount.Define(this)
                .WithValueField(0, 8, name: "EPIN4_MAXCNT")
                .WithReservedBits(8, 24);

            Registers.Endpoint5In.Define(this)
                .WithValueField(0, 32, name: "EPIN5");
            Registers.Endpoint5InCount.Define(this)
                .WithValueField(0, 8, name: "EPIN5_MAXCNT")
                .WithReservedBits(8, 24);

            Registers.Endpoint6In.Define(this)
                .WithValueField(0, 32, name: "EPIN6");
            Registers.Endpoint6InCount.Define(this)
                .WithValueField(0, 8, name: "EPIN6_MAXCNT")
                .WithReservedBits(8, 24);

            Registers.Endpoint7In.Define(this)
                .WithValueField(0, 32, name: "EPIN7");
            Registers.Endpoint7InCount.Define(this)
                .WithValueField(0, 8, name: "EPIN7_MAXCNT")
                .WithReservedBits(8, 24);
        }

        private void HandleToggle() 
        {
            if(dataToggleValue.Value == 0) 
            {
                this.Log(LogLevel.Noisy, "Selecting EP #{0}, {1}", dataToggleEndpoint.Value, dataToggleInputOutput.Value ? "in" : "out");
                return;
            }
            this.Log(LogLevel.Noisy, "Accessing EP #{0}, {1}; DATA{2}", dataToggleEndpoint.Value, dataToggleInputOutput.Value==false ? "out" : "in", dataToggleValue.Value == 1 ? "0" : "1");
        }

        private void HandleStalling() 
        {
            // This is useful for debugging, as software may stall endpoint
            // on wrong/unsupported tokens
            this.Log(LogLevel.Noisy, "{0} EP #{1}, {2}", epstallStall.Value == true ? "Stalling" : "Unstalling", epstallEndpoint.Value, epstallIO.Value==false ? "out" : "in");
        }
        
        private void InitiateUSBCore()
        {
            // Define all possible endpoints as available right away
            // This is to be improved in the future and should reflect what software returns as a result of enumaration, but will require support from `USB` subsystem in Renode
            USBConfiguration config = new USBConfiguration(this, 0, "").WithInterface(
                    configure: x =>
                        x.WithEndpoint(
                            Direction.DeviceToHost,
                            EndpointTransferType.Control,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.DeviceToHost,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out deviceToHostEndpoint)
                        .WithEndpoint(
                            Direction.DeviceToHost,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.DeviceToHost,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.DeviceToHost,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.DeviceToHost,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.DeviceToHost,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.DeviceToHost,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.HostToDevice,
                            EndpointTransferType.Control,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.HostToDevice,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.HostToDevice,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.HostToDevice,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.HostToDevice,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.HostToDevice,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.HostToDevice,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _)
                        .WithEndpoint(
                            Direction.HostToDevice,
                            EndpointTransferType.Bulk,
                            maximumPacketSize,
                            0x10,
                            out _));
            USBCore.SelectedConfiguration = config;
        }

        DoubleWordRegisterCollection IProvidesRegisterCollection<DoubleWordRegisterCollection>.RegistersCollection => registers;

        private USBEndpoint deviceToHostEndpoint;
        private Action<byte[]> setupPacketResultCallback;
        private readonly IMachine machine;
        private readonly bool[] epInDataStatus;
        private readonly bool[] epInStatus;

        private readonly InterruptManager<Events> interruptManager;
        private readonly IFlagRegisterField[] events;

        private SetupPacket setupPacket;

        private IValueRegisterField endpoint0In;
        private IValueRegisterField endpoint0InCount;
        private IValueRegisterField dataToggleEndpoint;
        private IFlagRegisterField dataToggleInputOutput;
        private IValueRegisterField dataToggleValue;

        private IValueRegisterField usbAddress;
        private IValueRegisterField epstallEndpoint;
        private IFlagRegisterField epstallIO;
        private IFlagRegisterField epstallStall;

        private IFlagRegisterField usbPullup;
        private IFlagRegisterField usbEnable;
        private IFlagRegisterField ep0InEnabled;
        private IFlagRegisterField ep0OutEnabled;

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
            UsbPullup = 0x504,
            DataToggle = 0x50C,
            IsoSplit = 0x51C,
            IsoInConfig = 0x530,
            HaltedEndpointOut0 = 0x444,
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
            Endpoint1In = 0x614,
            Endpoint1InCount = 0x618,
            Endpoint2In = 0x628,
            Endpoint2InCount = 0x62C,
            Endpoint2Amount = 0x630,
            Endpoint3In = 0x63C,
            Endpoint3InCount = 0x640,
            Endpoint4In = 0x650,
            Endpoint4InCount = 0x654,
            Endpoint5In = 0x664,
            Endpoint5InCount = 0x668,
            Endpoint6In = 0x678,
            Endpoint6InCount = 0x67C,
            Endpoint7In = 0x68C,
            Endpoint7InCount = 0x690,

            Endpoint0Out = 0x700,
            Endpoint0OutCount = 0x704,
            Endpoint0OutAmount = 0x708,
            Endpoint1Out = 0x714,
            Endpoint1OutCount = 0x718,
            Endpoint2Out = 0x728,
            Endpoint2OutCount = 0x72C,
        }
    }
}
