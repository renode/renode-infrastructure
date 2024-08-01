//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure;
using System.Linq;
using Antmicro.Renode.Utilities.Collections;
using Antmicro.Renode.Utilities.Packets;
using Antmicro.Renode.Core.USB;

namespace Antmicro.Renode.Peripherals.USB
{
    public class MPFS_USB : SimpleContainer<IUSBDevice>, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IProvidesRegisterCollection<WordRegisterCollection>, IProvidesRegisterCollection<ByteRegisterCollection>, IKnownSize
    {
        public MPFS_USB(IMachine machine, ControllerMode mode = ControllerMode.Host) : base(machine)
        {
            this.mode = mode;

            addressToDeviceCache = new TwoWayDictionary<byte, IUSBDevice>();

            fifoFromDeviceToHost = new Queue<byte>[NumberOfEndpoints];
            fifoFromHostToDevice = new Queue<byte>[NumberOfEndpoints];
            receiveDeviceAddress = new IValueRegisterField[NumberOfEndpoints];
            transmitDeviceAddress = new IValueRegisterField[NumberOfEndpoints];
            requestInTransaction = new IFlagRegisterField[NumberOfEndpoints];
            transmitTargetEndpointNumber = new IValueRegisterField[NumberOfEndpoints];
            receiveTargetEndpointNumber = new IValueRegisterField[NumberOfEndpoints];

            for(var i = 0; i < NumberOfEndpoints; i++)
            {
                fifoFromDeviceToHost[i] = new Queue<byte>();
                fifoFromHostToDevice[i] = new Queue<byte>();
            }

            MainIRQ = new GPIO();
            DmaIRQ = new GPIO();

            byteRegisters = new ByteRegisterCollection(this);
            wordRegisters = new WordRegisterCollection(this);
            doubleWordRegisters = new DoubleWordRegisterCollection(this);

            var gate = new GPIOGate(MainIRQ);
            usbInterruptsManager = new InterruptManager<UsbInterrupt>(this, gate.GetGPIO(), "main");
            txInterruptsManager = new InterruptManager<TxInterrupt>(this, gate.GetGPIO(), "main");
            rxInterruptsManager = new InterruptManager<RxInterrupt>(this, gate.GetGPIO(), "main");

            DefineCommonRegisters();
            DefineIndexedRegisters();
            DefineFifoRegisters();
            DefineControlAndStatusRegisters();
            DefineNonIndexedEndpointControlAndStatusRegisters();
            DefineMultipointControlAndStatusRegisters();

            ResetInterrupts();
        }

        public override void Reset()
        {
            byteRegisters.Reset();
            wordRegisters.Reset();
            doubleWordRegisters.Reset();
            ResetInterrupts();
        }

        public byte ReadByte(long offset)
        {
            if(!byteRegisters.TryRead(offset, out var res))
            {
                this.LogUnhandledRead(offset);
            }
            return res;
        }

        public void WriteByte(long offset, byte value)
        {
            byteRegisters.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            if(!wordRegisters.TryRead(offset, out var res))
            {
                this.LogUnhandledRead(offset);
            }
            return res;
        }

        public void WriteWord(long offset, ushort value)
        {
            wordRegisters.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            if(!doubleWordRegisters.TryRead(offset, out var res))
            {
                this.LogUnhandledRead(offset);
            }
            return res;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            doubleWordRegisters.Write(offset, value);
        }

        public override void Register(IUSBDevice peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            // when a new device is attached to the controller it is not automatically started, but waits for reset;
            // if there are multiple devices attached they are reset by the controller one at a time -
            // it ensures that there is only one device with address 0 (the default one) at the bus;
            base.Register(peripheral, registrationPoint);
            TryInitializeConnectedDevice();
        }

        public override void Unregister(IUSBDevice peripheral)
        {
            base.Unregister(peripheral);
            if(sessionInProgress.Value)
            {
                usbInterruptsManager.SetInterrupt(UsbInterrupt.DeviceDisconnectedSessionEnded);
            }
        }

        [IrqProvider]
        public GPIO MainIRQ { get; private set; }
        public GPIO DmaIRQ { get; private set; }

        public long Size => 0x1000;

        ByteRegisterCollection IProvidesRegisterCollection<ByteRegisterCollection>.RegistersCollection => byteRegisters;

        WordRegisterCollection IProvidesRegisterCollection<WordRegisterCollection>.RegistersCollection => wordRegisters;

        DoubleWordRegisterCollection IProvidesRegisterCollection<DoubleWordRegisterCollection>.RegistersCollection => doubleWordRegisters;

        private bool ReadAndClearInterrupt<T>(InterruptManager<T> irqManager, T irq) where T : struct, IConvertible
        {
            var result = irqManager.IsSet(irq);
            if(result)
            {
                irqManager.SetInterrupt(irq, false);
            }
            return result;
        }

        private void DefineCommonRegisters()
        {
            Registers.FunctionAddress.Tag8(this, name: "FADDR_REG")
            ;

            Registers.Power.Define8(this, 0x20, name: "POWER_REG")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => true, name: "HS Mode")
            ;

            Registers.TransmitInterruptsStatus
                .Bind(this, txInterruptsManager.GetRegister<WordRegister>((irq, _) => ReadAndClearInterrupt(txInterruptsManager, irq)), name: "TX_IRQ_REG")
            ;

            Registers.ReceiveInterruptsStatus
                .Bind(this, rxInterruptsManager.GetRegister<WordRegister>((irq, _) => ReadAndClearInterrupt(rxInterruptsManager, irq)), name: "RX_IRQ_REG")
            ;

            Registers.TransmitInterruptsEnable
                .Bind(this, txInterruptsManager.GetInterruptEnableRegister<WordRegister>(), name: "TX_IRQ_EN_REG")
            ;

            Registers.ReceiveInterrptsEnable
                .Bind(this, rxInterruptsManager.GetInterruptEnableRegister<WordRegister>(), name: "RX_IRQ_EN_REG")
            ;

            Registers.UsbInterruptsStatus
                .Bind(this, usbInterruptsManager.GetRegister<ByteRegister>((irq, _) => ReadAndClearInterrupt(usbInterruptsManager, irq)), name: "USB_IRQ_REG")
            ;

            Registers.UsbInterruptsEnable
                .Bind(this, usbInterruptsManager.GetInterruptEnableRegister<ByteRegister>(), name: "USB_IRQ_EN_REG")
            ;

            Registers.Frame.Tag16(this, name: "FRAME_REG")
            ;

            Registers.Index.Define8(this, name: "INDEX_REG")
                .WithValueField(0, 4, out index, name: "Selected Endpoint")
            ;

            Registers.TestMode.Tag8(this, name: "TEST_MODE_REG")
            ;
        }

        private void DefineIndexedRegisters()
        {
            Registers.TransmitMaximumPacketSize.Tag16(this, name: "TX_MAX_P_REG")
            ;

            // !WARNING driver treats those Low/High as one short register, docs splits it into two byte ones
            if(mode == ControllerMode.Host)
            {
                // !WARNING those two registers are mutually exclusive and their accessability depends on INDEX_REG value
                HostRegisters.Endpoint0ControlStatusLow.Tag8(this, name: "CSR0L_REG")
                ;
                HostRegisters.EndpointNTransmitControlStatusLow.Tag8(this, name: "TX_CSRL_REG")
                ;

                // !WARNING those two registers are mutually exclusive and their accessability depends on INDEX_REG value
                HostRegisters.Endpoint0ControlStatusHigh.Tag8(this, name: "CSR0H_REG")
                ;
                HostRegisters.EndpointNTransmitControlStatusHigh.Tag8(this, name: "TX_CSRH_REG")
                ;
            }
            else
            {
                // !WARNING those two registers are mutually exclusive and their accessability depends on INDEX_REG value
                DeviceRegisters.Endpoint0ControlStatusLow.Tag8(this, name: "CSR0L_REG")
                ;
                DeviceRegisters.EndpointNTransmitControlStatusLow.Tag8(this, name: "TX_CSRL_REG")
                ;

                // !WARNING those two registers are mutually exclusive and their accessability depends on INDEX_REG value
                DeviceRegisters.Endpoint0ControlStatusHigh.Tag8(this, name: "CSR0H_REG")
                ;
                DeviceRegisters.EndpointNTransmitControlStatusHigh.Tag8(this, name: "TX_CSRH_REG")
                ;
            }

            Registers.ReceiveMaximumPacketSize.Tag16(this, name: "RX_MAX_P_REG")
            ;

            // !WARNING driver treats those Low/High as one short register, docs splits it into two byte ones
            if(mode == ControllerMode.Host)
            {
                HostRegisters.ReceiveControlStatusLow.Tag8(this, name: "RX_CSRL_REG")
                ;

                HostRegisters.ReceiveControlStatusHigh.Tag8(this, name: "RX_CSRH_REG")
                ;
            }
            else
            {
                DeviceRegisters.ReceiveControlStatusLow.Tag8(this, name: "RX_CSRL_REG")
                ;

                DeviceRegisters.ReceiveControlStatusHigh.Tag8(this, name: "RX_CSRH_REG")
                ;
            }

            // !SIMPLIFICATION those are two different registers in the documentation, but I implement them the same way for now
            Registers.Endpoint0FifoCount.Tag8(this, name: "COUNT0_REG")
            ;
            Registers.EndpointNReceiveFifoCount.Define16(this)
                .WithValueField(0, 14, FieldMode.Read, valueProviderCallback: _ => (byte)fifoFromDeviceToHost[index.Value].Count);

            Registers.Endpoint0Type.Tag8(this, name: "TYPE0_REG")
            ;
            Registers.EndpointNTransmitType.Tag8(this, name: "TX_TYPE_REG")
            ;

            Registers.Endpoint0NAKLimit.Tag8(this, name: "NAK_LIMIT0_REG")
            ;
            Registers.EndpointNTransmitInterval.Tag8(this, name: "TX_INTERVAL_REG")
            ;

            Registers.EndpointNReceiveType.Tag8(this, name: "RX_TYPE_REG")
            ;

            Registers.EndpointNReceiveInterval.Tag8(this, name: "RX_INTERVAL_REG")
            ;

            Registers.Endpoint0ConfigData.Tag8(this, name: "CONFIG_DATA_REG")
            ;
            Registers.EndpointNFifoSize.Tag8(this, name: "FIFO_SIZE_REG")
            ;
        }

        private ulong ReadFromFifo(int fifoId, int numberOfBytes)
        {
            var result = 0ul;
            for(var i = 0; i < numberOfBytes; i++)
            {
                result |= (ulong)((long)fifoFromDeviceToHost[fifoId].Dequeue() << (8 * i));
            }
            return result;
        }

        private void WriteToFifo(int fifoId, int numberOfBytes, ulong value)
        {
            for(var i = 0; i < numberOfBytes; i++)
            {
                fifoFromHostToDevice[fifoId].Enqueue((byte)(value >> (8 * i)));
            }
        }

        private void DefineFifoRegisters()
        {

            for(var i = 0; i < NumberOfEndpoints; i++)
            {
                var fifoId = i;

                ((Registers)(Registers.Endpoint0Fifo + fifoId * 4)).Define32(this, name: $"EP{fifoId}_FIFO_REG")
                    .WithValueField(0, 32,
                        writeCallback: (_, val) => WriteToFifo(fifoId, 4, val),
                        valueProviderCallback: _ => (uint)ReadFromFifo(fifoId, 4))
                ;

                ((Registers)(Registers.Endpoint0Fifo + fifoId * 4)).Define16(this, name: $"EP{fifoId}_FIFO_REG")
                    .WithValueField(0, 16,
                        writeCallback: (_, val) => WriteToFifo(fifoId, 2, val),
                        valueProviderCallback: _ => (ushort)ReadFromFifo(fifoId, 2))
                ;

                ((Registers)(Registers.Endpoint0Fifo + fifoId * 4)).Define8(this, name: $"EP{fifoId}_FIFO_REG")
                    .WithValueField(0, 8,
                        writeCallback: (_, val) => WriteToFifo(fifoId, 1, val),
                        valueProviderCallback: _ => (byte)ReadFromFifo(fifoId, 1))
                ;
            }
        }

        private void HandleSessionStart()
        {
            lock(addressToDeviceCache)
            {
                addressToDeviceCache.Clear();
                if(sessionInProgress.Value)
                {
                    TryInitializeConnectedDevice();
                }
            }
        }

        private void DefineControlAndStatusRegisters()
        {

            Registers.DeviceControl.Define8(this, 0x80, name: "DEV_CTRL_REG")
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => true, name: "B-Device")
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => true, name: "FSDev")
                .WithEnumField<ByteRegister, VBusLevel>(3, 2, FieldMode.Read, valueProviderCallback: _ => VBusLevel.AboveVBusValid, name: "VBus")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => mode == ControllerMode.Host, name: "Host Mode")
                .WithFlag(0, out sessionInProgress, changeCallback: (_, val) => HandleSessionStart(), name: "Session")
            ;
        }

        private void DefineNonIndexedEndpointControlAndStatusRegisters()
        {
            Registers.Endpoint0TransmitControlStatus.Define16(this, name: $"EP0_TX_CSR_REG")
                .WithFlag(0, out var receivedPacketReady, name: "RxPktRdy")
                .WithFlag(1, out var transmitPacketReady,  name: "TxPktRdy")
                .WithFlag(3, out var setupPacket, name: "SetupPkt")
                .WithFlag(5, out var requestPacket, name: "ReqPkt")
                .WithFlag(6, out var statusPacket, name: "StatusPkt")
                .WithWriteCallback((_, __) =>
                {
                    if(transmitPacketReady.Value)
                    {
                        transmitPacketReady.Value = false;
                        if(!TryGetDeviceForEndpoint(0, Direction.HostToDevice, out var peripheral))
                        {
                            this.Log(LogLevel.Warning, "There is no peripheral configured for endpoint 0 in host to device direction");
                            return;
                        }

                        if(setupPacket.Value)
                        {
                            setupPacket.Value = false;
                            var data = fifoFromHostToDevice[0].DequeueAll();
                            if(data.Length != 8)
                            {
                                this.Log(LogLevel.Warning, "Setup packet must be composed of 8 bytes, but there are currently {0} in the buffer. Refusing to send packet and dropping buffered data.", data.Length);
                                return;
                            }

                            var packet = Packet.Decode<SetupPacket>(data);
                            peripheral.USBCore.HandleSetupPacket(packet, receivedBytes =>
                            {
                                fifoFromDeviceToHost[0].EnqueueRange(receivedBytes);
                                txInterruptsManager.SetInterrupt(TxInterrupt.Endpoint0);
                            });
                        }

                        if(statusPacket.Value)
                        {
                            statusPacket.Value = false;
                            // nothing happens here - just setting the interrupt
                            txInterruptsManager.SetInterrupt(TxInterrupt.Endpoint0);
                        }
                    }

                    if(requestPacket.Value)
                    {
                        requestPacket.Value = false;

                        // since the communication with the device is instantenous the data should already wait in the buffer
                        receivedPacketReady.Value = true;
                        txInterruptsManager.SetInterrupt(TxInterrupt.Endpoint0);
                    }
                })
            ;

            ((Registers)Registers.Endpoint0ReceivePacketSize).Define8(this, name: $"EP0_RX_COUNT_REG")
                .WithValueField(0, 8, FieldMode.Read, name: $"EP0_RX_COUNT_REG", valueProviderCallback: _ =>
                {
                    return checked((byte)fifoFromDeviceToHost[0].Count);
                })
            ;

            for(var i = 1; i < NumberOfEndpoints; i++)
            {
                var endpointId = i;
                IFlagRegisterField localReceivedPacketReady = null;

                ((Registers)(Registers.Endpoint0ReceiveControlStatus + endpointId * 0x10)).Define16(this, name: $"EP{endpointId}_RX_CSR_REG")
                    .WithFlag(5, out requestInTransaction[endpointId], name: "ReqPkt",
                        writeCallback: (_, val) =>
                        {
                            if(!val)
                            {
                                return;
                            }

                            if(!TryGetDeviceForEndpoint(endpointId, Direction.DeviceToHost, out var peripheral))
                            {
                                this.Log(LogLevel.Warning, "There is no peripheral configured for endpoint {0} in device to host direction", endpointId);
                                return;
                            }

                            var endpoint = peripheral.USBCore.GetEndpoint((int)receiveTargetEndpointNumber[endpointId].Value, Direction.DeviceToHost);
                            if(endpoint == null)
                            {
                                this.Log(LogLevel.Warning, "Trying to read from a non-existing endpoint #{0}", receiveTargetEndpointNumber[endpointId].Value);
                                return;
                            }

                            endpoint.SetDataReadCallbackOneShot((e, bytes) =>
                            {
                                fifoFromDeviceToHost[endpointId].EnqueueRange(bytes);
                                requestInTransaction[endpointId].Value = false;
                                localReceivedPacketReady.Value = true;
                                rxInterruptsManager.SetInterrupt((RxInterrupt)endpointId);
                            });
                        })
                    .WithFlag(4, FieldMode.WriteOneToClear, name: "FlushFIFO", writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }

                        fifoFromDeviceToHost[endpointId].Clear();
                        localReceivedPacketReady.Value = false;
                    })
                    .WithFlag(0, out localReceivedPacketReady, name: "RxPktRdy")
                ;

                ((Registers)(Registers.Endpoint0ReceivePacketSize + endpointId * 0x10)).Define16(this, name: $"EP{endpointId}_RX_COUNT_REG")
                    .WithValueField(0, 14, FieldMode.Read, name: $"EP{endpointId}_RX_COUNT_REG", valueProviderCallback: _ =>
                    {
                        return checked((uint)fifoFromDeviceToHost[endpointId].Count);
                    })
                ;

                ((Registers)(Registers.Endpoint0TransmitControlStatus + endpointId * 0x10)).Define16(this, name: $"EP{endpointId}_TX_CSR_REG")
                    .WithTag("NAK Timeout/IncompTx", 7, 1)
                    .WithTag("ClrDataTog", 6, 1)
                    .WithTag("RxStall", 5, 1)
                    .WithFlag(4, out var setupPkt, FieldMode.Read | FieldMode.Set, name: "SetupPkt")
                    .WithTag("FlushFIFO", 3, 1)
                    .WithTag("Error", 2, 1)
                    .WithFlag(1, FieldMode.Read, name: "FIFONotEmpty", valueProviderCallback: _ => fifoFromHostToDevice[endpointId].Count > 0)
                    .WithFlag(0, out var txPktRdy, FieldMode.Read | FieldMode.Set, name: "TxPktRdy")
                    .WithWriteCallback((_, val) =>
                    {
                        if(!txPktRdy.Value)
                        {
                            return;
                        }

                        if(setupPkt.Value)
                        {
                            throw new ArgumentException("Setup packets on Endpoint 1-4 are not supported");
                        }
                        else
                        {
                            // standard OUT packet
                            if(!TryGetDeviceForEndpoint(endpointId, Direction.HostToDevice, out var peripheral))
                            {
                                this.Log(LogLevel.Warning, "There is no peripheral configured for endpoint {0} in host to device direction", endpointId);
                                return;
                            }

                            var mappedEndpointId = (int)transmitTargetEndpointNumber[endpointId].Value;
                            var endpoint = peripheral.USBCore.GetEndpoint(mappedEndpointId, Direction.HostToDevice);
                            if(endpoint == null)
                            {
                                this.Log(LogLevel.Warning, "Trying to write to a non-existing endpoint #{0}", mappedEndpointId);
                            }

                            var data = fifoFromHostToDevice[endpointId].DequeueAll();
                            endpoint.WriteData(data);

                            txPktRdy.Value = false;
                            txInterruptsManager.SetInterrupt((TxInterrupt)endpointId);
                        }
                    })
                ;

                ((Registers)Registers.Endpoint0TransmitType + endpointId * 0x10).Define8(this, name: $"EP{endpointId}_TX_TYPE_REG")
                    .WithTag("Speed", 6, 2)
                    .WithTag("Protocol", 4, 2)
                    .WithValueField(0, 4, out transmitTargetEndpointNumber[endpointId], name: "Target Endpoint Number")
                ;

                ((Registers)Registers.Endpoint0ReceiveType + endpointId * 0x10).Define8(this, name: $"EP{endpointId}_RX_TYPE_REG")
                    .WithTag("Speed", 6, 2)
                    .WithTag("Protocol", 4, 2)
                    .WithValueField(0, 4, out receiveTargetEndpointNumber[endpointId], name: "Target Endpoint Number")
                ;
            }
        }

        private void DefineMultipointControlAndStatusRegisters()
        {
            for(var i = 0; i < NumberOfEndpoints; i++)
            {
                var endpointId = i;

                ((Registers)(Registers.Endpoint0TransmitFunctionAddress + endpointId * 0x8)).Define8(this, name: $"EP{endpointId}_TX_FUNC_ADDR_REG")
                    .WithValueField(0, 7, out transmitDeviceAddress[endpointId], name: "TxFuncAddr")
                ;

                ((Registers)(Registers.Endpoint0ReceiveFunctionAddress + endpointId * 0x8)).Define8(this, name: $"EP{endpointId}_RX_FUNC_ADDR_REG")
                    .WithValueField(0, 7, out receiveDeviceAddress[endpointId], name: "RxFuncAddr")
                ;
            }
        }

        private void ResetInterrupts()
        {
            usbInterruptsManager.Reset();
            txInterruptsManager.Reset();
            rxInterruptsManager.Reset();
        }

        private bool TryGetDeviceForEndpoint(int endpointId, Direction direction, out IUSBDevice device)
        {
            IValueRegisterField addressField = null;
            switch(direction)
            {
                case Direction.DeviceToHost:
                    addressField = receiveDeviceAddress[endpointId];
                    break;
                case Direction.HostToDevice:
                    addressField = transmitDeviceAddress[endpointId];
                    break;
                default:
                    throw new ArgumentException($"Unexpected direction: {direction}");
            }

            lock(addressToDeviceCache)
            {
                var address = (byte)addressField.Value;
                if(!addressToDeviceCache.TryGetValue(address, out device))
                {
                    // it will happen at the first access to the device after it has been granted an address
                    device = this.ChildCollection.Select(x => x.Value).FirstOrDefault(x => x.USBCore.Address == address);
                    if(device != null)
                    {
                        if(!addressToDeviceCache.TryExchange(device, address, out var oldAddress) || oldAddress != 0)
                        {
                            this.Log(LogLevel.Error, "USB device address change detected: previous address 0x{0:X}, current address 0x{1:X}. This might lead to problems", oldAddress, address);
                        }

                        TryInitializeConnectedDevice();
                    }
                }

                if(device != null && device.USBCore.Address != address)
                {
                    this.Log(LogLevel.Error, "USB device address change detected: previous address 0x{0:X}, current address 0x{1:X}. This might lead to problems", address, device.USBCore.Address);
                }
                return device != null;
            }
        }

        private bool TryInitializeConnectedDevice()
        {
            if(!sessionInProgress.Value)
            {
                // we don't initialize devices when session is inactive
                return false;
            }

            lock(addressToDeviceCache)
            {
                if(addressToDeviceCache.Exists(0))
                {
                    // there is an enumeration in progress, the next device will be picked automatically later
                    return false;
                }

                var peripheral = ChildCollection.Values.FirstOrDefault(x => !addressToDeviceCache.Exists(x));
                if(peripheral == null)
                {
                    // no more devices to initialize
                    return false;
                }

                addressToDeviceCache.Add(0, peripheral);

                usbInterruptsManager.SetInterrupt(UsbInterrupt.DeviceConnected);
                return true;
            }
        }

        private readonly TwoWayDictionary<byte, IUSBDevice> addressToDeviceCache;

        private IFlagRegisterField[] requestInTransaction = new IFlagRegisterField[NumberOfEndpoints];
        private IValueRegisterField[] transmitDeviceAddress;
        private IValueRegisterField[] receiveDeviceAddress;
        private IValueRegisterField[] transmitTargetEndpointNumber;
        private IValueRegisterField[] receiveTargetEndpointNumber;
        private IValueRegisterField index;
        private IFlagRegisterField sessionInProgress;
        private readonly Queue<byte>[] fifoFromHostToDevice;
        private readonly Queue<byte>[] fifoFromDeviceToHost;

        private readonly InterruptManager<UsbInterrupt> usbInterruptsManager;
        private readonly InterruptManager<TxInterrupt> txInterruptsManager;
        private readonly InterruptManager<RxInterrupt> rxInterruptsManager;

        private readonly ByteRegisterCollection byteRegisters;
        private readonly WordRegisterCollection wordRegisters;
        private readonly DoubleWordRegisterCollection doubleWordRegisters;

        // I couldn't find any mention of how to change mode by the software in the documentation.
        private readonly ControllerMode mode;

        private const int NumberOfEndpoints = 5;

        public enum ControllerMode
        {
            Host,
            Device
        }

        private enum UsbInterrupt
        {
            Suspend = 0,
            [EnabledOnReset] Resume = 1,
            [EnabledOnReset] ResetBabble = 2,
            StartOfFrame = 3,
            DeviceConnected = 4,
            DeviceDisconnectedSessionEnded = 5,
            SessionRequest = 6,
            VBusError = 7
        }

        private enum TxInterrupt
        {
            [EnabledOnReset] Endpoint0 = 0,
            [EnabledOnReset] Endpoint1 = 1,
            [EnabledOnReset] Endpoint2 = 2,
            [EnabledOnReset] Endpoint3 = 3,
            [EnabledOnReset] Endpoint4 = 4
        }

        private enum RxInterrupt
        {
            [EnabledOnReset] Endpoint1 = 1,
            [EnabledOnReset] Endpoint2 = 2,
            [EnabledOnReset] Endpoint3 = 3,
            [EnabledOnReset] Endpoint4 = 4
        }

        private enum Registers
        {
            FunctionAddress = 0x0,
            Power = 0x1,
            TransmitInterruptsStatus = 0x2,
            ReceiveInterruptsStatus = 0x4,
            TransmitInterruptsEnable = 0x6,
            ReceiveInterrptsEnable = 0x8,
            UsbInterruptsStatus = 0xA,
            UsbInterruptsEnable = 0xB,
            Frame = 0xC,
            Index = 0xE,
            TestMode = 0xF,

            TransmitMaximumPacketSize = 0x10,
            // defined by the rest of registers
            ReceiveMaximumPacketSize = 0x14,
            // defined by the rest of registers

            Endpoint0FifoCount = 0x18,
            EndpointNReceiveFifoCount = 0x18,

            Endpoint0Type = 0x1A,
            EndpointNTransmitType = 0x1A,

            Endpoint0NAKLimit = 0x1B,
            EndpointNTransmitInterval = 0x1B,

            EndpointNReceiveType = 0x1C,

            EndpointNReceiveInterval = 0x1D,

            Endpoint0ConfigData = 0x1F,
            EndpointNFifoSize = 0x1F,

            // FIFO registers
            Endpoint0Fifo = 0x20,
            Endpoint1Fifo = 0x24,
            Endpoint2Fifo = 0x28,
            Endpoint3Fifo = 0x2C,
            Endpoint4Fifo = 0x30,
            //driver suggests that there are more fifo registers, but documentation says nothing about it

            DeviceControl = 0x60,
            Misc = 0x61,
            TransmitFifoSize = 0x62,
            ReceiveFifoSize = 0x63,
            TransmitFifoAddress = 0x64,
            ReceiveFifoAddress = 0x66,
            VbusControl = 0x68,
            HwVersion = 0x6C,
            // 2-bytes gap, intentionally

            UlpiVbysControl = 0x70,
            UlpiCarKitControl = 0x71,
            UlpiInterruptMask = 0x72,
            UlpiInterruptSources = 0x73,
            UlpiData = 0x74,
            UlpiAddress = 0x75,
            UlpiControl = 0x76,
            UlpiRawData = 0x77,
            EndpointInfo = 0x78,
            RamInfo = 0x79,
            LinkInfo = 0x7A,
            VbusPulseLength = 0x7B,
            HighSpeedEOF1 = 0x7C,
            FullSpeedEOF1 = 0x7D,
            LowSpeedEOF1 = 0x7E,
            SoftReset = 0x7F,

            // Multipoint Control And Status Registers
            Endpoint0TransmitFunctionAddress = 0x80,
            // 1-byte gap, intentionall
            Endpoint0TransmitHubAddress = 0x82,
            Endpoint0TransmitHubPort = 0x83,
            Endpoint0ReceiveFunctionAddress = 0x84,

            // 5-byte gap, intentionall

            Endpoint1TransmitFunctionAddress = 0x88,
            // 1-byte gap, intentionall
            Endpoint1TransmitHubAddress = 0x8A,
            Endpoint1TransmitHubPort = 0x8B,
            Endpoint1ReceiveHubAddress = 0x8E,
            Endpoint1ReceiveHubPort = 0x8F,

            Endpoint2TransmitFunctionAddress = 0x90,
            // 1-byte gap, intentionall
            Endpoint2TransmitHubAddress = 0x92,
            Endpoint2TransmitHubPort = 0x93,
            Endpoint2ReceiveFunctionAddress = 0x94,
            Endpoint2ReceiveHubAddress = 0x96,
            Endpoint2ReceiveHubPort = 0x97,

            Endpoint3TransmitFunctionAddress = 0x98,
            // 1-byte gap, intentionall
            Endpoint3TransmitHubAddress = 0x9A,
            Endpoint3TransmitHubPort = 0x9B,
            Endpoint3ReceiveFunctionAddress = 0x9C,
            Endpoint3ReceiveHubAddress = 0x9E,
            Endpoint3ReceiveHubPort = 0x9F,

            Endpoint4TransmitFunctionAddress = 0xA0,
            // 1-byte gap, intentionall
            Endpoint4TransmitHubAddress = 0xA2,
            Endpoint4TransmitHubPort = 0xA3,
            Endpoint4ReceiveFunctionAddress = 0xA4,
            Endpoint4ReceiveHubAddress = 0xA6,
            Endpoint4ReceiveHubPort = 0xA7,

            Endpoint0TransmitMaximumPacketSize = 0x100,
            Endpoint0TransmitControlStatus = 0x102,
            Endpoint0ReceiveMaximumPacketSize = 0x104,
            Endpoint0ReceiveControlStatus = 0x106,
            Endpoint0ReceivePacketSize = 0x108,
            Endpoint0TransmitType = 0x10A,
            Endpoint0TransmitPollingInterval = 0x10B,
            Endpoint0ReceiveType = 0x10C,
            Endpoint0ReceivePollingInterval = 0x10D,
            // there is an inconsistency between driver and documentation; the latter says FifoSize register should be placed at 0x10F
            Endpoint0FifoSize = 0x10E,

            Endpoint1TransmitMaximumPacketSize = 0x110,
            Endpoint1TransmitControlStatus = 0x112,
            Endpoint1ReceiveMaximumPacketSize = 0x114,
            Endpoint1ReceiveControlStatus = 0x116,
            Endpoint1ReceivePacketSize = 0x118,
            Endpoint1TransmitType = 0x11A,
            Endpoint1TransmitPollingInterval = 0x11B,
            Endpoint1ReceiveType = 0x11C,
            Endpoint1ReceivePollingInterval = 0x11D,
            // there is an inconsistency between driver and documentation; the latter says FifoSize register should be placed at 0x11F
            Endpoint1FifoSize = 0x11E,

            Endpoint2TransmitMaximumPacketSize = 0x120,
            Endpoint2TransmitControlStatus = 0x122,
            Endpoint2ReceiveMaximumPacketSize = 0x124,
            Endpoint2ReceiveControlStatus = 0x126,
            Endpoint2ReceivePacketSize = 0x128,
            Endpoint2TransmitType = 0x12A,
            Endpoint2TransmitPollingInterval = 0x12B,
            Endpoint2ReceiveType = 0x12C,
            Endpoint2ReceivePollingInterval = 0x12D,
            // there is an inconsistency between driver and documentation; the latter says FifoSize register should be placed at 0x12F
            Endpoint2FifoSize = 0x12E,

            Endpoint3TransmitMaximumPacketSize = 0x130,
            Endpoint3TransmitControlStatus = 0x132,
            Endpoint3ReceiveMaximumPacketSize = 0x134,
            Endpoint3ReceiveControlStatus = 0x136,
            Endpoint3ReceivePacketSize = 0x138,
            Endpoint3TransmitType = 0x13A,
            Endpoint3TransmitPollingInterval = 0x13B,
            Endpoint3ReceiveType = 0x13C,
            Endpoint3ReceivePollingInterval = 0x13D,
            // there is an inconsistency between driver and documentation; the latter says FifoSize register should be placed at 0x13F
            Endpoint3FifoSize = 0x13E,

            Endpoint4TransmitMaximumPacketSize = 0x140,
            Endpoint4TransmitControlStatus = 0x142,
            Endpoint4ReceiveMaximumPacketSize = 0x144,
            Endpoint4ReceiveControlStatus = 0x146,
            Endpoint4ReceivePacketSize = 0x148,
            Endpoint4TransmitType = 0x14A,
            Endpoint4TransmitPollingInterval = 0x14B,
            Endpoint4ReceiveType = 0x14C,
            Endpoint4ReceivePollingInterval = 0x14D,
            // there is an inconsistency between driver and documentation; the latter says FifoSize register should be placed at 0x14F
            Endpoint4FifoSize = 0x14E,

            // DMA registers
            DmaInterrupt = 0x200,
            DmaChannel1Control = 0x204,
            DmaChannel1Address = 0x208,
            DmaChannel1Count = 0x20C,
            DmaChannel2Control = 0x214,
            DmaChannel2Address = 0x218,
            DmaChannel2Count = 0x21C,
            DmaChannel3Control = 0x224,
            DmaChannel3Address = 0x228,
            DmaChannel3Count = 0x22C,
            DmaChannel4Control = 0x234,
            DmaChannel4Address = 0x238,
            DmaChannel4Count = 0x23C,

        }

        private enum HostRegisters
        {
            Endpoint0ControlStatusLow = 0x12,
            EndpointNTransmitControlStatusLow = 0x12,

            Endpoint0ControlStatusHigh = 0x13,
            EndpointNTransmitControlStatusHigh = 0x13,

            ReceiveControlStatusLow = 0x16,
            ReceiveControlStatusHigh = 0x17
        }

        private enum DeviceRegisters
        {
            Endpoint0ControlStatusLow = 0x12,
            EndpointNTransmitControlStatusLow = 0x12,

            Endpoint0ControlStatusHigh = 0x13,
            EndpointNTransmitControlStatusHigh = 0x13,

            ReceiveControlStatusLow = 0x16,
            ReceiveControlStatusHigh = 0x17
        }

        private enum VBusLevel
        {
            BelowSessionEnd = 0,
            AboveSessionEndBelowAvalid = 1,
            AboveAvalidBelowVBusValid = 2,
            AboveVBusValid = 3
        }
    }
}
