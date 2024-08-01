//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;
using System.Threading;
using Antmicro.Renode.Peripherals.Bus.Wrappers;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class MAX3421E : SimpleContainer<IUSBDevice>, IProvidesRegisterCollection<ByteRegisterCollection>, ISPIPeripheral, IDisposable
    {
        public MAX3421E(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            setupQueue = new Queue<byte>();
            receiveQueue = new Queue<byte>();
            sendQueue = new Queue<byte>();
            bumper = machine.ObtainManagedThread(GenerateFrameInterrupt, BumpsPerSecond);

            RegistersCollection = new ByteRegisterCollection(this);

            DefineRegisters();
        }

        public override void Register(IUSBDevice peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            base.Register(peripheral, registrationPoint);

            // indicate the K state - full-speed device attached
            kStatus.Value = true;
            jStatus.Value = false;

            connectDisconnectInterruptRequest.Value = true;

            this.Log(LogLevel.Debug, "USB device connected to port {0}", registrationPoint.Address);
            UpdateInterrupts();

            HandleBumper();
        }

        public override void Unregister(IUSBDevice peripheral)
        {
            base.Unregister(peripheral);

            connectDisconnectInterruptRequest.Value = true;
            UpdateInterrupts();

            HandleBumper();
        }

        public void FinishTransmission()
        {
            this.Log(LogLevel.Noisy, "Transmission finished");
            state = State.Idle;
        }

        public override void Dispose()
        {
            base.Dispose();
            bumper.Dispose();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            UpdateInterrupts();

            lastRegister = 0;
            state = State.Idle;

            setupQueue.Clear();
            receiveQueue.Clear();
            sendQueue.Clear();

            HandleBumper();
        }

        public byte Transmit(byte data)
        {
            this.Log(LogLevel.Noisy, "Received byte: 0x{0:X} in state {1}", data, state);
            byte result = 0;

            switch(state)
            {
            case State.Idle:
                HandleCommandByte(data);
                result = RegistersCollection.Read((long)RegisterType.HostIrqPending);
                break;

            case State.Writing:
                this.Log(LogLevel.Noisy, "Writing value 0x{0:X} to register {1} (0x{1:X})", data, lastRegister);
                RegistersCollection.Write((long)lastRegister, data);
                break;

            case State.Reading:
                this.Log(LogLevel.Noisy, "Reading value from register {0} (0x{0:X})", lastRegister);
                result = RegistersCollection.Read((long)lastRegister);
                break;

            default:
                this.Log(LogLevel.Error, "Received byte 0x{0:X} in an unexpected state: {1}. Ignoring it...", data, state);
                break;
            }

            this.Log(LogLevel.Noisy, "Returning byte: 0x{0:X}", result);
            return result;
        }

        public GPIO IRQ { get; }

        public ByteRegisterCollection RegistersCollection { get; }

        private void HandleBumper()
        {
            if((hostMode.Value || startOfFramePacketsGenerationEnable.Value) && ChildCollection.Any())
            {
                bumper.Start();
            }
            else
            {
                bumper.Stop();
            }
        }

        private void UpdateInterrupts()
        {
            var state = false;

            state |= (connectDisconnectInterruptRequest.Value && connectDisconnectInterruptEnable.Value);
            state |= (busEventInterruptRequest.Value && busEventInterruptEnable.Value);
            state |= (frameGeneratorInterruptRequest.Value && frameGeneratorInterruptEnable.Value);
            state |= (hostTransferDoneInterruptRequest.Value && hostTransferDoneInterruptEnable.Value);
            state |= (receiveDataAvailableInterruptRequest.Value && receiveDataAvailableInterruptEnable.Value);
            state |= (sendDataBufferAvailableInterruptRequest.Value && sendDataBufferAvailableInterruptEnable.Value);

            state |= (oscillatorOKInterruptRequest.Value && oscillatorOKInterruptEnable.Value);

            state &= interruptEnable.Value;

            this.Log(LogLevel.Noisy, "Setting IRQ to {0}", state);
            IRQ.Set(state);
        }

        private void DefineRegisters()
        {
            RegisterType.ReceiveFifo.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "data", valueProviderCallback: _ =>
                {
                    if(receiveQueue.Count == 0)
                    {
                        this.Log(LogLevel.Warning, "Trying to read from an empty receive queue");
                        return 0;
                    }
                    return receiveQueue.Dequeue();
                })
            ;

            RegisterType.SendFifo.Define(this)
                .WithValueField(0, 8, name: "data",
                    valueProviderCallback: _ =>
                    {
                        if(sendQueue.Count == 0)
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty send queue");
                            return 0;
                        }
                        return sendQueue.Dequeue();

                    },
                    writeCallback: (_, val) =>
                    {
                        sendQueue.Enqueue((byte)val);
                        if(sendQueue.Count > FifoSize)
                        {
                            this.Log(LogLevel.Warning, "Too much data put in the send queue. Initial bytes will be dropped");
                            sendQueue.Dequeue();
                        }
                    })
            ;

            RegisterType.SetupFifo.Define(this)
                .WithValueField(0, 8, name: "setup data", valueProviderCallback: _ =>
                {
                    if(setupQueue.Count == 0)
                    {
                        this.Log(LogLevel.Warning, "Trying to read from an empty setup queue");
                        return 0;
                    }
                    return setupQueue.Dequeue();

                },
                writeCallback: (_, val) =>
                {
                    setupQueue.Enqueue((byte)val);
                    if(setupQueue.Count > 8)
                    {
                        this.Log(LogLevel.Warning, "Too much data put in the setup queue. Initial bytes will be dropped");
                        setupQueue.Dequeue();
                    }
                })
            ;

            RegisterType.ReceiveQueueLength.Define(this)
                .WithValueField(0, 7, FieldMode.Read, name: "count", valueProviderCallback: _ => (uint)receiveQueue.Count)
                .WithReservedBits(7, 1)
            ;

            RegisterType.SendQueueLength.Define(this)
                .WithValueField(0, 7, out sendByteCount, name: "count")
                .WithReservedBits(7, 1)
                .WithWriteCallback((_, __) =>
                {
                    sendDataBufferAvailableInterruptRequest.Value = false;
                    UpdateInterrupts();
                })
            ;

            RegisterType.USBIrqPending.Define(this)
                .WithFlag(0, out oscillatorOKInterruptRequest, FieldMode.Read | FieldMode.WriteOneToClear, name: "OSCOKIRQ")
                .WithReservedBits(1, 4)
                .WithTag("NOVBUSIRQ", 5, 1)
                .WithTag("VBUSIRQ", 6, 1)
                .WithReservedBits(7, 1)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            RegisterType.USBIrqEnabled.Define(this)
                .WithFlag(0, out oscillatorOKInterruptEnable, name: "OSCOKIE")
                .WithReservedBits(1, 4)
                .WithTag("NOVBUSIE", 5, 1)
                .WithTag("VBUSIE", 6, 1)
                .WithReservedBits(7, 1)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            RegisterType.USBControl.Define(this)
                .WithReservedBits(0, 4)
                .WithTag("PWRDOWN", 4, 1)
                .WithFlag(5, name: "Chip Reset", changeCallback: (_, v) =>
                {
                    if(!v)
                    {
                        // software should wait for the oscillator and PLLS
                        // to stabilize after setting CHIPRES = 0 which
                        // is indicated by setting oscillator OK IRQ
                        oscillatorOKInterruptRequest.Value = true;
                        UpdateInterrupts();
                    }
                })
                .WithReservedBits(6, 2)
            ;

            RegisterType.CPUControl.Define(this)
                .WithFlag(0, out interruptEnable, name: "IE")
                .WithReservedBits(1, 5)
                .WithTag("PULSEWID", 6, 2)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            RegisterType.PinControl.Define(this)
                .WithTag("GPXA", 0, 1)
                .WithTag("GPXB", 1, 1)
                .WithTag("POSINT", 2, 1)
                .WithTag("INTLEVEL", 3, 1)
                .WithTag("FDUPSPI", 4, 1)
                .WithReservedBits(5, 3)
            ;

            RegisterType.Revision.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => ChipRevision)
            ;

            RegisterType.HostIrqPending.Define(this, 0x8) //sndbavirq is set by default
                .WithFlag(0, out busEventInterruptRequest, FieldMode.Read | FieldMode.WriteOneToClear, name: "BUSEVENTIRQ")
                .WithTag("RWUIRQ - Remote Wakeup Interrupt Request", 1, 1)
                .WithFlag(2, out receiveDataAvailableInterruptRequest, FieldMode.Read | FieldMode.WriteOneToClear, name: "RCVDAVIRQ") // this should not go automatically from 1 to 0 when the fifo is empty, but should be explicitly cleared by the cpu
                .WithFlag(3, out sendDataBufferAvailableInterruptRequest, FieldMode.Read, name: "SNDAVIRQ") // this bit is cleared by writing to SNDBC register
                .WithTag("SUSDNIRQ - Suspend operation Done Interrupt Request", 4, 1)
                .WithFlag(5, out connectDisconnectInterruptRequest, FieldMode.Read | FieldMode.WriteOneToClear, name: "CONDETIRQ")
                .WithFlag(6, out frameGeneratorInterruptRequest, FieldMode.Read | FieldMode.WriteOneToClear, name: "FRAMEIRQ")
                .WithFlag(7, out hostTransferDoneInterruptRequest, FieldMode.Read | FieldMode.WriteOneToClear, name: "HXFRDNIRQ")
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            RegisterType.HostIrqEnabled.Define(this)
                .WithFlag(0, out busEventInterruptEnable, name: "BUSEVENTIE")
                .WithTag("RWUIE - Remote Wakeup Interrupt Enable", 1, 1)
                .WithFlag(2, out receiveDataAvailableInterruptEnable, name: "RCVDAVIE")
                .WithFlag(3, out sendDataBufferAvailableInterruptEnable, name: "SNDAVIE")
                .WithTag("SUSDNIE - Suspend operation Done Interrupt Enable", 4, 1)
                .WithFlag(5, out connectDisconnectInterruptEnable, name: "CONDETIE")
                .WithFlag(6, out frameGeneratorInterruptEnable, name: "FRAMEIE")
                .WithFlag(7, out hostTransferDoneInterruptEnable, name: "HXFRDNIE")
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            RegisterType.Mode.Define(this)
                .WithFlag(0, out hostMode, name: "host mode")
                .WithTag("LOWSPEED", 1, 1)
                .WithTag("HUBPRE - Send the PRE PID to a LS device operating through a USB hub", 2, 1)
                .WithFlag(3, out startOfFramePacketsGenerationEnable, name: "SOFKAENAB")
                .WithTag("SEPIRQ - Provides the GPIN IRQS on a separate pin (GPX)", 4, 1)
                .WithTag("DELAYISO - Delay data transfer to an ISOCHRONOUS endpoint until the next frame", 5, 1)
                .WithTag("DMPULLDN - Connect internal 15k resistor from D- to ground", 6, 1)
                .WithTag("DPPULLDN - Connect internal 15k resistor from D+ to ground", 7, 1)
                .WithWriteCallback((_, __) => HandleBumper())
            ;

            RegisterType.PeripheralAddress.Define(this)
                .WithValueField(0, 7, out deviceAddress, name: "address")
                .WithReservedBits(7, 1)
            ;

            RegisterType.HostControl.Define(this)
                .WithFlag(0, name: "bus reset",
                        valueProviderCallback: _ => false,
                        writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                busEventInterruptRequest.Value = true;
                                UpdateInterrupts();
                            }
                        })
                .WithTag("FRMRST - Reset the SOF frame counter", 1, 1)
                .WithTag("SAMPLEBUS - Sample the state of the USB bus", 2, 1)
                .WithTag("SIGRSM - Signal a bus resume event", 3, 1)
                .WithTag("RCVTOG - Set or clear the data toggle value for a data transfer", 4, 2)
                .WithTag("SNDTOG - Set or clear the data toggle value for a data transfer", 6, 2)
            ;

            RegisterType.HostTransfer.Define(this)
                .WithValueField(0, 4, out var ep, name: "ep")
                .WithFlag(4, out var setup, name: "setup")
                .WithFlag(5, out var outnin, name: "outnin")
                .WithTag("ISO", 6, 1)
                .WithFlag(7, out var hs, name: "hs")
                .WithWriteCallback((_, v) => { HandleHostTransfer((uint)ep.Value, setup.Value, outnin.Value, hs.Value); })
            ;

            RegisterType.HostResult.Define(this)
                .WithTag("HRSLT - Host result", 0, 4)
                .WithTag("RCVTOGRD - Resulting data toggle value for IN transfers", 4, 1)
                .WithTag("SNDTOGRD - Resulting data toggle value for OUT transfers", 5, 1)
                .WithFlag(6, out kStatus, name: "KSTATUS - Sample the state of the USB bus")
                .WithFlag(7, out jStatus, name: "JSTATUS - Sample the state of the USB bus")
            ;
        }

        private void HandleCommandByte(byte data)
        {
            var dir = (CommandDirection)((data >> 1) & 0x1);
            lastRegister = (RegisterType)(data >> 3);

            this.Log(LogLevel.Noisy, "Command byte detected: operation: {0}, register: {1} (0x{1:X})", dir, lastRegister);

            switch(dir)
            {
                case CommandDirection.Write:
                    state = State.Writing;
                    break;

                case CommandDirection.Read:
                    state = State.Reading;
                    break;

                default:
                    throw new ArgumentException("Unsupported command direction");
            }
        }

        private void HandleHostTransfer(uint ep, bool setup, bool outnin, bool hs)
        {
            if(setup && hs)
            {
                this.Log(LogLevel.Error, "Both SETUP and HS bits set for a host transfer - ignoring it!");
                return;
            }

            var device = this.ChildCollection.Values.FirstOrDefault(x => x.USBCore.Address == deviceAddress.Value);
            if(device == null)
            {
                this.Log(LogLevel.Warning, "Tried to send setup packet to a device with address 0x{0:X}, but it's not connected", deviceAddress.Value);

                // setting the IRQ is necessary to allow communication right after the usb device address has changed
                hostTransferDoneInterruptRequest.Value = true;
                UpdateInterrupts();

                return;
            }

            if(setup)
            {
                this.Log(LogLevel.Noisy, "Setup TX");
                if(ep != 0)
                {
                    this.Log(LogLevel.Error, "This model does not support SETUP packets on EP different than 0");
                    return;
                }

                HandleSetup(device);
            }
            else if(hs)
            {
                this.Log(LogLevel.Noisy, "Handshake {0}", outnin ? "out" : "in");

                hostTransferDoneInterruptRequest.Value = true;
                UpdateInterrupts();
            }
            else
            {
                USBEndpoint endpoint = null;
                if(ep != 0)
                {
                    endpoint = device.USBCore.GetEndpoint((int)ep, outnin ? Direction.HostToDevice : Direction.DeviceToHost);
                    if(endpoint == null)
                    {
                        this.Log(LogLevel.Error, "Tried to access a non-existing EP #{0}", ep);

                        hostTransferDoneInterruptRequest.Value = true;
                        UpdateInterrupts();
                        return;
                    }
                }

                if(outnin)
                {
                    this.Log(LogLevel.Noisy, "Bulk out");
                    HandleBulkOut(endpoint);
                }
                else
                {
                    this.Log(LogLevel.Noisy, "Bulk in");
                    HandleBulkIn(endpoint);
                }
            }
        }

        private void GenerateFrameInterrupt()
        {
            this.Log(LogLevel.Noisy, "Generating frame interrupt");

            frameGeneratorInterruptRequest.Value = true;
            UpdateInterrupts();
        }

        private void HandleBulkOut(USBEndpoint endpoint)
        {
            if(endpoint != null)
            {
                if((int)sendByteCount.Value != sendQueue.Count)
                {
                    this.Log(LogLevel.Warning, "Requested to send BULK out {0} bytes of data, but there are {1} bytes in the queue.", sendByteCount.Value, sendQueue.Count);
                }

                var bytesToSend = sendQueue.DequeueRange((int)sendByteCount.Value);
                this.Log(LogLevel.Noisy, "Writing {0} bytes to the device", bytesToSend.Length);
                endpoint.WriteData(bytesToSend);

                sendDataBufferAvailableInterruptRequest.Value = true;
            }

            hostTransferDoneInterruptRequest.Value = true;
            UpdateInterrupts();
        }

        private void HandleBulkIn(USBEndpoint endpoint)
        {
            if(endpoint != null)
            {
                this.Log(LogLevel.Noisy, "Initiated read from the device");
                endpoint.SetDataReadCallbackOneShot((_, data) =>
                {
                    this.Log(LogLevel.Noisy, "Received data from the device");
#if DEBUG_PACKETS
                    this.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(data));
#endif
                    EnqueueReceiveData(data);

                    hostTransferDoneInterruptRequest.Value = true;
                    UpdateInterrupts();
                });
            }
            else
            {
                hostTransferDoneInterruptRequest.Value = true;
                UpdateInterrupts();
            }
        }

        private void HandleSetup(IUSBDevice device)
        {
            var data = setupQueue.DequeueAll();
            if(!Packet.TryDecode<SetupPacket>(data, out var setupPacket))
            {
                this.Log(LogLevel.Error, "Could not decode SETUP packet - some data might be lost! Bytes were: {0}", Misc.PrettyPrintCollectionHex(data));
                return;
            }

            device.USBCore.HandleSetupPacket(setupPacket, response =>
            {
                EnqueueReceiveData(response);

                hostTransferDoneInterruptRequest.Value = true;
                UpdateInterrupts();
            });
        }

        private void EnqueueReceiveData(IEnumerable<byte> data)
        {
            if(receiveQueue.EnqueueRange(data) > 0)
            {
                receiveDataAvailableInterruptRequest.Value = true;
            }
        }

        private RegisterType lastRegister;
        private State state;

        private IFlagRegisterField connectDisconnectInterruptRequest;
        private IFlagRegisterField connectDisconnectInterruptEnable;
        private IFlagRegisterField interruptEnable;
        private IFlagRegisterField kStatus;
        private IFlagRegisterField jStatus;
        private IFlagRegisterField busEventInterruptRequest;
        private IFlagRegisterField busEventInterruptEnable;
        private IFlagRegisterField frameGeneratorInterruptRequest;
        private IFlagRegisterField frameGeneratorInterruptEnable;
        private IFlagRegisterField hostTransferDoneInterruptRequest;
        private IFlagRegisterField hostTransferDoneInterruptEnable;
        private IFlagRegisterField receiveDataAvailableInterruptRequest;
        private IFlagRegisterField receiveDataAvailableInterruptEnable;
        private IValueRegisterField deviceAddress;
        private IValueRegisterField sendByteCount;
        private IFlagRegisterField sendDataBufferAvailableInterruptRequest;
        private IFlagRegisterField sendDataBufferAvailableInterruptEnable;
        private IFlagRegisterField startOfFramePacketsGenerationEnable;
        private IFlagRegisterField hostMode;
        private IFlagRegisterField oscillatorOKInterruptEnable;
        private IFlagRegisterField oscillatorOKInterruptRequest;
        private readonly Queue<byte> setupQueue;
        private readonly Queue<byte> receiveQueue;
        private readonly Queue<byte> sendQueue;
        private readonly IManagedThread bumper;

        private const byte ChipRevision = 0x13;
        private const int BumpsPerSecond = 100;
        private const int FifoSize = 64;

        private enum State
        {
            Idle,
            Writing,
            Reading
        }

        private enum CommandDirection
        {
            Read = 0,
            Write = 1
        }

        [RegisterMapper.RegistersDescription]
        private enum RegisterType
        {
            // R0 is not available in HOST mode
            ReceiveFifo = 1,
            SendFifo = 2,
            // R3 is not available in HOST mode
            SetupFifo = 4,
            // R5 is not available in HOST mode
            ReceiveQueueLength = 6,
            SendQueueLength = 7,
            // R8-R12 are not available in HOST mode
            USBIrqPending = 13,
            USBIrqEnabled = 14,
            USBControl = 15,
            CPUControl = 16,
            PinControl = 17,
            Revision = 18,
            // R19 is not available in HOST mode
            IOPins1 = 20,
            IOPins2 = 21,
            GeneralPurposeInIrqPending = 22,
            GeneralPurposeInIrqEnabled = 23,
            GeneralPurposeInIrqPolarity = 24,
            HostIrqPending = 25,
            HostIrqEnabled = 26,
            Mode = 27,
            PeripheralAddress = 28,
            HostControl = 29,
            HostTransfer = 30,
            HostResult = 31
        }
    }
}
