//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Peripherals.Wireless.IEEE802_15_4;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public sealed class CC1200: IRadio, ISPIPeripheral, INumberedGPIOOutput, IGPIOReceiver
    {
        public CC1200(Machine machine)
        {
            this.machine = machine;
            CreateRegisters();
            var dict = new Dictionary<int, IGPIO>();
            for(var i = 0; i < NumberOfGPIOs; ++i)
            {
                dict[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(dict);
            gpioConfigurations = new [] {gpio0Selection, gpio1Selection, gpio2Selection, gpio3Selection};
            Reset();
        }

        public void Reset()
        {
            wasSyncTransfered = false;
            stateMachineMode = StateMachineMode.Off;
            currentFrame = null;

            txFifo.Clear();
            rxFifo.Clear();
            memory = new byte[GeneralMemorySize];
            sourceAddressTable = new byte[SourceAddressTableSize];
            sourceAddressMatchingResult = new byte[SourceAddressMatchingResultSize];
            sourceAddressMatchingControl = new byte[SourceAddressMatchingControlSize];
            localAddressInfo = new byte[LocalAddressInfoSize];

            localExtendedAddress = new Address(new ArraySegment<byte>(localAddressInfo, 0, 8));
            localShortAddress = new Address(new ArraySegment<byte>(localAddressInfo, 10, 2));


            registers.Reset();
            extendedRegisters.Reset();
            frequency = 0;
            foreach(var gpio in Connections.Values)
            {
                gpio.Unset();
            }
        }

        private bool isChipSelect;
        private bool initialized;
        private IEnumRegisterField<GPIOSignal>[] gpioConfigurations;
        public void OnGPIO(int number, bool value)
        {
            if(!initialized && !value)
            {
                // our gpio system uses `false` as defaults. Here it's a meaningful value. Let's ignore gpios until first `true` is received
                return;
            }
            initialized = true;
            this.Log(LogLevel.Noisy, "Received a GPIO! Number {0}, value {1}", number, value);
            if(number == 0)
            {
                isChipSelect = !value;
                if(!isChipSelect)
                {
                    FinishTransmission();
                }
                else if(stateMachineMode == StateMachineMode.Off)
                {
                    stateMachineMode = StateMachineMode.Idle;
                }
                UpdateGPIOs();
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled GPIO {0}!", number);
            }
        }

        public void UpdateGPIOs()
        {
            for(var i = 0; i < gpioConfigurations.Length; ++i)
            {
                if(i == 1)
                {
                    //MOSI line, we fake the behavior here - the controller is configured to pull up
                    Connections[i].Set(!isChipSelect);
                    continue;
                }
                var gpio = gpioConfigurations[i];
                switch(gpio.Value)
                {
                    case GPIOSignal.RxFifoThreshold:
                        Connections[i].Set(fifoThreshold.Value < rxFifo.Count);
                        this.Log(LogLevel.Error, $"RX: threshold {fifoThreshold.Value}, fifo {rxFifo.Count}");
                        break;
                    case GPIOSignal.TxFifoThreshold:
                        Connections[i].Set(127 - fifoThreshold.Value <= txFifo.Count);
                        break;
                    case GPIOSignal.TxFifoFull:
                        if(!Connections[i].IsSet)
                        {
                            Connections[i].Set(txFifo.Count == 128);
                        }
                        else
                        {
                            Connections[i].Set(txFifo.Count > 127 - fifoThreshold.Value);
                        }
                        break;
                    case GPIOSignal.PacketSync:
                        if(wasSyncTransfered)
                        {
                            Connections[i].Blink();
                        }
                        wasSyncTransfered = false;
                        break;
                    case GPIOSignal.MARCStateStatus1:
                        Connections[i].Set(
                            stateMachineMode == StateMachineMode.ReceiveMode ||
                            stateMachineMode == StateMachineMode.Idle);
                        break;
                    case GPIOSignal.MARCStateStatus0:
                        Connections[i].Set(
                            stateMachineMode == StateMachineMode.ReceiveMode ||
                            stateMachineMode == StateMachineMode.TransmitMode);
                        break;
                    case GPIOSignal.ChipReadyN:
                        Connections[i].Set(stateMachineMode == StateMachineMode.Off);
                        break;
                    default:
                        this.Log(LogLevel.Warning, "Unsupported GPIO mode on pin {0}: {1}", i, gpio.Value);
                        continue; //continue not to log
                }
                this.Log(LogLevel.Noisy, "Setting up GPIO{0} ({1}) to {2}", i, gpio.Value, Connections[i].IsSet);
            }
        }

        private enum State
        {
            WaitingForHeader,
            WaitingForAddress,
            WaitingForData,
            Readout
        }

        private const byte ExtendedRegisterAccessCommand = 0x2F;
        private const byte CommandStrobeLow = 0x30;
        private const byte CommandStrobeHigh = 0x3d;
        private const byte BuffersOrFECOrFreeArea = 0x3e;
        private const byte StandardFIFOAccess = 0x3f;
        private struct AccessDescriptor
        {
            public byte Address;
            public bool IsRead;
            public bool IsBurst; //todo: when does it end?
            public Target Target;

            public State NextState(State state)
            {
                switch(state)
                {
                case State.WaitingForHeader:
                    if(Target == Target.Registers || Target == Target.StandardFIFO)
                    {
                        return IsRead ? State.Readout : State.WaitingForData;
                    }
                    else if(Target == Target.ExtendedRegisters || Target == Target.DirectFIFO || Target == Target.FECWorkspaceOrFreeArea)
                    {
                        return State.WaitingForAddress;
                    }
                    else if(Target == Target.CommandStrobe)
                    {
                        return State.WaitingForHeader;
                    }
                    //should not reach
                    break;
                case State.WaitingForAddress:
                    return IsRead ? State.Readout : State.WaitingForData;
                case State.WaitingForData:
                case State.Readout:
                    return IsBurst ? state : State.WaitingForHeader;
                }
                //should not reach
                return State.WaitingForHeader;
            }
        }
        private enum Target
        {
            Registers,
            ExtendedRegisters,
            StandardFIFO,
            DirectFIFO,
            FECWorkspaceOrFreeArea,
            CommandStrobe
        }
        private AccessDescriptor access;
        private State state;
        private byte[] freeArea = new byte[0xFF];
        private void RunCommand(Command command)
        {
            this.Log(LogLevel.Noisy, "Running command: {0}", command);
            switch(command)
            {
                case Command.ResetChip:
                    Reset(); //maybe should do less
                    break;
                case Command.EnableAndCalibrateFrequencySynthesizer:
                    stateMachineMode = StateMachineMode.FastTxReady;
                    break;
                case Command.EnableRx:
                    stateMachineMode = StateMachineMode.ReceiveMode;
                    if(currentFrame != null)
                    {
                        HandleFrame(currentFrame);
                        currentFrame = null;
                    }
                    break;
                case Command.EnableTx:
                    stateMachineMode = StateMachineMode.TransmitMode;
                    SendFrame();
                    break;
                case Command.Idle:
                    stateMachineMode = StateMachineMode.Idle;
                    break;
                case Command.Sleep: //sleep should only be called in idle, and has no special state code. This is a warning hush
                    stateMachineMode = StateMachineMode.Off;
                    break;
                case Command.FlushRX:
                    rxFifo.Clear();
                    break;
                case Command.FlushTX:
                    txFifo.Clear();
                    break;
                case Command.NoOperation:
                    //intentionally left blank
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unsupported command {0} ({1})", command, (Registers)command);
                    break;
            }
        }
        public byte Transmit(byte data)
        {
            var status = GetStatus();
            if(!isChipSelect || stateMachineMode == StateMachineMode.Off)
            {
                this.Log(LogLevel.Error, "Trying to communicate with Chip Select disabled or in OFF state.");
            }
            if(state == State.WaitingForHeader)
            {
                access = new AccessDescriptor
                {
                    IsRead = (data & 0x80) != 0,
                    IsBurst = (data & 0x40) != 0
                };
                var command = (byte)(data & 0x3F);
                this.Log(LogLevel.Noisy, "{2} radio: 0x{0:X}, {1} (raw 0x{3:X})", command, (Registers)command, (access.IsBurst ? "(Burst) " : String.Empty) + (access.IsRead ? "Read from" : "Write to"), data);

                if(command < ExtendedRegisterAccessCommand)
                {
                    access.Target = Target.Registers;
                    access.Address = command;
                }
                else if(command == ExtendedRegisterAccessCommand)
                {
                    access.Target = Target.ExtendedRegisters;
                }
                else if(command >= CommandStrobeLow && command <= CommandStrobeHigh)
                {
                    access.Target = Target.CommandStrobe;
                    RunCommand((Command)command);
                }
                else if(command == StandardFIFOAccess)
                {
                    access.Target = Target.StandardFIFO;
                }
                else if(command == BuffersOrFECOrFreeArea) // && spi_direct_access_cfg == 0
                {
                    access.Target = Target.DirectFIFO;
                }
                else if(command == BuffersOrFECOrFreeArea) // && spi_direct_access_cfg == 0
                {
                    access.Target = Target.FECWorkspaceOrFreeArea;
                }
                state = access.NextState(state);
                this.Log(LogLevel.Noisy, "Access target: {0}, next state {1}", access.Target, state);

                return status;
            }
            else if(state == State.WaitingForAddress)
            {
                access.Address = data;
                state = access.NextState(state);
                return 0;
            }
            else if(state == State.WaitingForData)
            {
                switch(access.Target)
                {
                    case Target.Registers:
                        this.Log(LogLevel.Debug, "Writing to Register 0x{0:X} ({1}), value 0x{2:X}", access.Address, (Registers)access.Address, data);
                        registers.Write(access.Address, data);
                        break;
                    case Target.ExtendedRegisters:
                        this.Log(LogLevel.Debug, "Writing to ExtendedRegister 0x{0:X} ({1}), value 0x{2:X}", access.Address, (ExtendedRegisters)access.Address, data);
                        extendedRegisters.Write(access.Address, data);
                        break;
                    case Target.StandardFIFO:
                        this.Log(LogLevel.Debug, "Writing to txFifo value 0x{0:X}", data);
                        txFifo.Enqueue(data);
                        break;
                    case Target.DirectFIFO:
                        //txFifo[access.Address] = data;
                        break;
                    case Target.FECWorkspaceOrFreeArea:
                        freeArea[access.Address] = data;
                        break;
                }
                state = access.NextState(state);
                return status;
            }
            else //State.Readout
            {
                byte value;
                state = access.NextState(state);
                switch(access.Target)
                {
                    case Target.Registers:
                        value = registers.Read(access.Address);
                        this.Log(LogLevel.Debug, "Reading from Register 0x{0:X} ({1}), value 0x{2:X}", access.Address, (Registers)access.Address, value);
                        return value;
                    case Target.ExtendedRegisters:
                        value = extendedRegisters.Read(access.Address);
                        this.Log(LogLevel.Debug, "Reading from ExtendedRegister 0x{0:X} ({1}), value 0x{2:X}", access.Address, (ExtendedRegisters)access.Address, value);
                        return value;
                    case Target.StandardFIFO:
                        rxFifo.TryDequeue(out value);
                        //underflow?
                        this.Log(LogLevel.Debug, "Reading from rx fifo, value 0x{0:X}, {1} bytes left", value, rxFifo.Count);
                        return value;
                    case Target.DirectFIFO:
                        return rxFifo.ElementAt(access.Address);
                    case Target.FECWorkspaceOrFreeArea:
                    default: // no other options
                        return freeArea[access.Address];
                }
            }
        }

        public void FinishTransmission()
        {
            this.Log(LogLevel.Noisy, "Finish transmission");
            state = State.WaitingForHeader;
        }

        public void ReceiveFrame(byte[] bytes)
        {
            if(stateMachineMode == StateMachineMode.ReceiveMode)
            {
                //this allows to have proper CCA values easily.
                this.DebugLog("Received frame {0}.", bytes.Select(x => "0x{0:X}".FormatWith(x)).Stringify());
                currentFrame = bytes;
                HandleFrame(bytes);
                currentFrame = null;
            }
            else
            {
                currentFrame = bytes;
                this.DebugLog("Radio is not listening right now - this frame is being deffered.");
            }
        }

        public int Channel
        {
            get
            {
                return ChannelValueFromFrequency(frequency);
            }

            set
            {
                this.Log(LogLevel.Info, "Setting channel to {0}", value);
                frequency = ChannelNumberToFrequency(value);
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections
        {
            get; private set;
        }

#region frames
        public event Action<IRadio, byte[]> FrameSent;

        private void HandleFrame(byte[] bytes)
        {
            // var frame = new Frame(bytes);
            // rxFifo.Enqueue(frame.Length);
            var header = new PacketHeader802154(bytes[0], bytes[1]);
            var crcLength = header.FCS2Byte ? 2 : 4;
            foreach(var data in bytes.Take(bytes.Length - crcLength))
            {
                rxFifo.Enqueue(data);
            }
            if(appendStatus.Value)
            {
                rxFifo.Enqueue(0xFF); //RSSI. Use sane value here TODO!
                rxFifo.Enqueue((byte)((crcEnabled.Value ? (1 << 7) : 0) | 0x7F)); //LQI = use sane value
            }
            wasSyncTransfered = true;
            UpdateGPIOs();
        }

        private void SendFrame()
        {
            uint length;
            int crcLength = 0;
            IEnumerable<byte> data;
            if(packetMode802_15_4g.Value)
            {
                txFifo.TryDequeue(out var packetHeaderA);
                txFifo.TryDequeue(out var packetHeaderB);
                var packetHeader = new PacketHeader802154(packetHeaderA, packetHeaderB);
                if(packetHeader.ModeSwitch)
                {
                    this.Log(LogLevel.Error, "Unsupported packet with Mode Switch on, dropping");
                    txFifo.Clear();
                    return;
                }
                if(!packetHeader.FCS2Byte)
                {
                    this.Log(LogLevel.Error, "Unsupported packet with 4-byte FCS, falling back to 32 and padding with 0s.");
                    crcLength = 4;
                    //txFifo.Clear();
                }
                else
                {
                    crcLength = 2;
                }
                length = packetHeader.Length;
                data = new byte[] { packetHeaderA, packetHeaderB };
            }
            else
            {
                txFifo.TryDequeue(out var lengthByte);
                length = lengthByte;
                data = new byte[] { lengthByte };
            }
            data = data.Concat(txFifo.DequeueAll()).ToArray();

            IEnumerable<byte> crc = new byte[0];
            if(crcEnabled.Value)
            {
                if(crcLength == 2)
                {
                    crc = Frame.CalculateCRC(data);
                }
                else if(crcLength == 4)
                {
                    crc = Frame.CalculateCRC(data).Concat(new byte[]{0, 0});//hack
                }
                data = (data.Concat(crc).ToArray());
            }
            this.DebugLog("Sending frame {0}.", data.Select(x => "0x{0:X}".FormatWith(x)).Stringify());
            FrameSent?.Invoke(this, data.ToArray());
            stateMachineMode = txOffMode;
            this.Log(LogLevel.Noisy, "Setting state to {0}", stateMachineMode);
            wasSyncTransfered = true;
            UpdateGPIOs();
        }

        private struct PacketHeader802154
        {
            public PacketHeader802154(byte byteA, byte byteB)
            {
                this.byteA = byteA;
                this.byteB = byteB;
            }

            public uint Length => byteB + ((byteA & 0x7u) << 8);
            public bool DataWhitening => BitHelper.IsBitSet(byteA, 3);
            public bool FCS2Byte => BitHelper.IsBitSet(byteA, 4);
            public bool ModeSwitch => BitHelper.IsBitSet(byteA, 7);

            private byte byteA;
            private byte byteB;
        }

#endregion
        private byte GetStatus()
        {
            var status = (byte)((byte)stateMachineMode << 4);
            this.Log(LogLevel.Noisy, "Returning status 0x{0:X}", status);
            return status;
            // bits 0:3 are reserved, 7 is CHIP_RDYn, should always be zero
        }

        private int ChannelValueFromFrequency(uint frequency)
        {
            var actualFreq = frequency * 625 / 4096; // should be calculated from f_xosc, freqoff (equal to 0) and LO_Divider.
            return (int)(actualFreq - 863125) * 1000 / 25000; // 902200 - center, 200000 - spacing, 1000 - Hz to kHz
         //   return ((int)frequency - 11) / 5 + 11;
        }

        private uint ChannelNumberToFrequency(int channelNumber)
        {
            //According to documentation, chapter 16:
            //"Channels are numbered 11 through 26 and are 5MHz apart"
            return (uint)(11 + 5 * (channelNumber - 11));
        }

        private void CreateRegisters()
        {
            var dict = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.GPIO3Pin, new ByteRegister(this, 0x6)
                    .WithTaggedFlag("GPIO3_ATRAN", 7)
                    .WithTaggedFlag("GPIO3_INV", 6)
                    .WithEnumField(0, 6, out gpio3Selection)
                    .WithWriteCallback((_, __) => UpdateGPIOs())
                },
                {(long)Registers.GPIO2Pin, new ByteRegister(this, 0x7)
                    .WithTaggedFlag("GPIO2_ATRAN", 7)
                    .WithTaggedFlag("GPIO2_INV", 6)
                    .WithEnumField(0, 6, out gpio2Selection)
                    .WithWriteCallback((_, __) => UpdateGPIOs())
                },
                {(long)Registers.GPIO1Pin, new ByteRegister(this, 0x30)
                    .WithTaggedFlag("GPIO1_ATRAN", 7)
                    .WithTaggedFlag("GPIO1_INV", 6)
                    .WithEnumField(0, 6, out gpio1Selection)
                    .WithWriteCallback((_, __) => UpdateGPIOs())
                },
                {(long)Registers.GPIO0Pin, new ByteRegister(this, 0x3c)
                    .WithTaggedFlag("GPIO0_ATRAN", 7)
                    .WithTaggedFlag("GPIO0_INV", 6)
                    .WithEnumField(0, 6, out gpio0Selection)
                    .WithWriteCallback((_, __) => UpdateGPIOs())
                },
                {(long)Registers.Sync3Word, new ByteRegister(this, 0x93).WithValueField(0, 8)},
                {(long)Registers.Sync2Word, new ByteRegister(this, 0x0B).WithValueField(0, 8)},
                {(long)Registers.Sync1Word, new ByteRegister(this, 0x51).WithValueField(0, 8)},
                {(long)Registers.Sync0Word, new ByteRegister(this, 0xDE).WithValueField(0, 8)},
                {(long)Registers.FrequencyDeviation, new ByteRegister(this, 0x06).WithValueField(0, 8)},
                {(long)Registers.ModulationFormatAndFrequencyDeviation, new ByteRegister(this, 0x03)
                    .WithValueField(0, 3, name: "DEV_E")
                    .WithValueField(3, 3, name: "MOD_FORMAT")
                    .WithValueField(6, 2, name: "MODEM_MODE")
                },
                {(long)Registers.DigitalDCRemoval, new ByteRegister(this, 0x4C)
                    .WithValueField(0, 3, name: "DCFILT_BW")
                    .WithValueField(3, 3, name: "DCFILT_BW_SETTLE")
                    .WithFlag(6, name: "DCFILT_FREEZE_COEFF")
                    .WithReservedBits(7, 1)
                },
                {(long)Registers.Preamble1, new ByteRegister(this, 0x14)
                    .WithValueField(0, 2, name: "PREAMBLE_WORD")
                    .WithValueField(2, 4, name: "NUM_PREAMBLE")
                    .WithReservedBits(6, 2)
                },
                {(long)Registers.Preamble0, new ByteRegister(this, 0xDA)
                    .WithValueField(0, 4, name: "PQT")
                    .WithValueField(4, 3, name: "PQT_VALID_TIMEOUT")
                    .WithFlag(7, name: "PQT_EN")
                },
                {(long)Registers.DigitalImageChannelCompensation, new ByteRegister(this, 0xC4)
                    .WithValueField(0, 2, name: "IQIC_IMGCH_LEVEL_THR")
                    .WithValueField(2, 2, name: "IQIC_BLEN")
                    .WithValueField(4, 2, name: "IQIC_BLEN_SETTLE")
                    .WithFlag(6, name: "IQIC_UPDATE_COEFF_EN")
                    .WithFlag(7, name: "IQIC_EN")
                },
                {(long)Registers.FIFOConfiguration, new ByteRegister(this, 0x80)
                    .WithValueField(0, 7, out fifoThreshold, name: "FIFO_THR")
                    .WithTaggedFlag("CRC_AUTOFLUSH", 7)
                },
                {(long)Registers.PacketConfiguration2, new ByteRegister(this, 0x4)
                    .WithTag("PKT_FORMAT", 0, 2)
                    .WithTag("CCA_MODE", 2, 3)
                    .WithFlag(5, out packetMode802_15_4g, name: "FG_MODE_EN")
                    .WithTaggedFlag("BYTE_SWAP_EN", 6)
                    .WithReservedBits(7, 1)
                },
                {(long)Registers.PacketConfiguration1, new ByteRegister(this, 0x3)
                    .WithFlag(0, out appendStatus)
                    .WithFlag(1, out crcEnabled)
                    .WithTag("ADDR_CHECK_CFG", 3, 2) //TODO!!!
                    .WithTaggedFlag("PN9_SWAP_EN", 5)
                    .WithTaggedFlag("WHITE_DATA", 6)
                    .WithTaggedFlag("FEC_EN", 7)
                },
                //PKT_CFG0 - length_config
                {(long)Registers.PacketConfiguration0, new ByteRegister(this)
                    .WithTaggedFlag("UART_SWAP_EN", 0)
                    .WithTaggedFlag("UART_MODE_EN", 1)
                    .WithTag("PKT_BIT_LEN", 2, 3)
                    .WithEnumField(5, 2, out packetLengthConfig, name: "LENGTH_CONFIG")
                },
                {(long)Registers.RFENDConfiguration0, new ByteRegister(this)
                    .WithTag("ANT_DIV_RX_TERM_CFG", 0, 3)
                    .WithTaggedFlag("TERM_ON_BAD_PACKET_EN", 3)
                    .WithValueField(4, 2, writeCallback: (_, value) =>
                    {
                        switch(value)
                        {
                            case 0:
                                txOffMode = StateMachineMode.Idle;
                                break;
                            case 1:
                                txOffMode = StateMachineMode.FastTxReady;
                                break;
                            case 2:
                                txOffMode = StateMachineMode.TransmitMode;
                                break;
                            case 3:
                                txOffMode = StateMachineMode.ReceiveMode;
                                break;
                        }
                    }, valueProviderCallback: _ =>
                    {
                        switch(txOffMode)
                        {
                            case StateMachineMode.Idle:
                                return 0;
                            case StateMachineMode.FastTxReady:
                                return 1;
                            case StateMachineMode.TransmitMode:
                                return 2;
                            case StateMachineMode.ReceiveMode:
                            default: //it is 2bit anyway
                                return 3;
                        }
                    }, name: "TXOFF_MODE")
                },

                {(long)Registers.PacketLength, new ByteRegister(this)
                    .WithValueField(0, 8, out packetLength, name: "PACKET_LENGTH")
                },
            };
            registers = new ByteRegisterCollection(this, dict);

            var extDict = new Dictionary<long, ByteRegister>
            {
                {(long)ExtendedRegisters.Frequency2, new ByteRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, value) => BitHelper.ReplaceBits(ref frequency, value, 8, 16))
                },
                {(long)ExtendedRegisters.Frequency1, new ByteRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, value) => BitHelper.ReplaceBits(ref frequency, value, 8, 8))
                },
                {(long)ExtendedRegisters.Frequency0, new ByteRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, value) => BitHelper.ReplaceBits(ref frequency, value, 8))
                },
                {(long)ExtendedRegisters.ReceivedSignalStrengthIndicator0, new ByteRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => true, name: "RSSI_VALID")
                    .WithFlag(1, valueProviderCallback: _ => true, name: "CARRIER_SENSE_VALID")
                    .WithFlag(2, valueProviderCallback: _ => false, name: "CARRIER_SENSE") // 0 means "channel clear"
                },
                {(long)ExtendedRegisters.PartNumber, new ByteRegister(this)
                    .WithValueField(0, 8, valueProviderCallback: _ => 0x20) //CC1200. 0x21 for CC1201
                },
                {(long)ExtendedRegisters.TxFIFONumberOfBytes, new ByteRegister(this)
                    .WithValueField(0, 8, valueProviderCallback: _ => (uint)txFifo.Count)
                },
                {(long)ExtendedRegisters.RxFIFONumberOfBytes, new ByteRegister(this)
                    .WithValueField(0, 8, valueProviderCallback: _ => (uint)rxFifo.Count)
                },
            };
            extendedRegisters = new ByteRegisterCollection(this, extDict);
        }

        uint frequency;
        IValueRegisterField fifoThreshold;
        IFlagRegisterField packetMode802_15_4g;
        IFlagRegisterField appendStatus;
        IFlagRegisterField crcEnabled;
        IValueRegisterField packetLength;
        IEnumRegisterField<PacketLengthConfig> packetLengthConfig;
#region vars
        private StateMachineMode stateMachineMode;

        private IEnumRegisterField<GPIOSignal> gpio3Selection;
        private IEnumRegisterField<GPIOSignal> gpio2Selection;
        private IEnumRegisterField<GPIOSignal> gpio1Selection;
        private IEnumRegisterField<GPIOSignal> gpio0Selection;


        private bool wasSyncTransfered;

        private byte[] currentFrame;

        private readonly CircularBuffer<byte> txFifo = new CircularBuffer<byte>(0x80);
        private readonly CircularBuffer<byte> rxFifo = new CircularBuffer<byte>(0x80);
        private byte[] memory;
        private byte[] sourceAddressTable;
        private byte[] sourceAddressMatchingResult;
        private byte[] sourceAddressMatchingControl;
        private byte[] localAddressInfo;
        private Address localShortAddress;
        private Address localExtendedAddress;

        private ByteRegisterCollection registers;
        private ByteRegisterCollection extendedRegisters;


        private readonly Machine machine;

        private const int GeneralMemorySize = 0x180;
        private const int SourceAddressTableSize = 0x60;
        private const int SourceAddressMatchingResultSize = 0x4;
        private const int SourceAddressMatchingControlSize = 0x6;
        private const int LocalAddressInfoSize = 0xC;
        private const int NumberOfGPIOs = 4;

#endregion

        private StateMachineMode txOffMode;
        private enum PacketLengthConfig
        {
            Fixed,
            VariableFirstByte,
            Infinite,
            Variable5LSB
        }

        private enum GPIOSignal : byte
        {
            RxFifoThreshold = 0,
            RxFifoThresholdOrPacketEnd = 1,
            TxFifoThreshold = 2,
            TxFifoFull = 3,
            RxFifoOverflow = 4,
            TxFifoUnderflow = 5,
            PacketSync = 6,
            CRCOk = 7,
            SerialClock = 8,
            SerialRxData = 9,
        //    Reserved,
            PreambleQualityReached = 11,
            PreambleQualityValid = 12,
            RSSIValid = 13,
            RSSISignal = 14,
            ClearChannelAssessment = 15,
            CarrierSenseValid = 16,
            CarrierSense = 17,
            DSSSSignals = 18,
            PacketCRCOk = 19,
            MCUWakeup = 20,
            DualSyncDetect = 21,
            AESCommandActive = 22,
            CommonLNAAndPARegulatorControl = 23,
            ControlExternalLNA = 24,
            ControlExternalPA = 25,
            IsNotIdle = 26,
      //      Reserved,
            ImageFound = 28,
            DataClockForDemodulator = 29,
            DataClockForModulator = 30,
            //reserved
            RSSIStepFound = 33,
            AESRunOrRSSIStepDetected = 34,
            Lock = 35,
            AntennaSelect = 36,
            MARCStateStatus1 = 37,
            MARCStateStatus0 = 38,
            TxOverflowOrRxUnderflow = 39,
            ChannelFilterSettled = 40,
            CollisionEvent = 41,
            RampingStarted = 42,
            PacketError = 43,
            AGCStableGain = 44,
            AGCUpdate = 45,
            ADC = 46,
            //reserved
            HighImpedance = 48,
            ExternalClock = 49,
            ChipReadyN = 50,
            HW0 = 51,
            //reserved
            Clock40K = 54,
            WOREvent0 = 55,
            WOREvent1 = 56,
            WOREvent2 = 57,
            //reserverd
            OscillatorStable = 59,
            ExternalOscillatorEnable = 60
        }
        private enum Command
        {
            ResetChip = 0x30,
            EnableAndCalibrateFrequencySynthesizer,
            EnterXOff,
            CalibrateAndDisableSynthesizer,
            EnableRx,
            EnableTx,
            Idle,
            AutomaticFrequencyCompensation,
            StartRXPollingSequence,
            Sleep,
            FlushRX,
            FlushTX,
            ResetEWORTimer,
            NoOperation
        }
        //only the states relevant for GetStatus are implemented
        private enum StateMachineMode
        {
            Idle = 0,
            ReceiveMode = 1,
            TransmitMode = 2,
            FastTxReady = 3,
            Calibrate = 4,
            Settling = 5,
            RxFIFOError = 6,
            TxFIFOError = 7,
            Off, //this is not a part of the docs, used for flow control
        }
        private enum Registers
        {
            GPIO3Pin = 0x0,
            GPIO2Pin = 0x1,
            GPIO1Pin = 0x2,
            GPIO0Pin = 0x3,
            Sync3Word = 0x4,
            Sync2Word = 0x5,
            Sync1Word = 0x6,
            Sync0Word = 0x7,
            SyncWord1 = 0x8,
            SyncWord0 = 0x9,
            FrequencyDeviation = 0xa,
            ModulationFormatAndFrequencyDeviation = 0xb,
            DigitalDCRemoval = 0xc,
            Preamble1 = 0xd,
            Preamble0 = 0xe,
            DigitalImageChannelCompensation = 0xf,
            ChannelFilter = 0x10,
            GeneralModemParameter1 = 0x11,
            GeneralModemParameter0 = 0x12,
            SymbolRate2 = 0x13,
            SymbolRate1 = 0x14,
            SymbolRate0 = 0x15,
            AGCReferenceLevel = 0x16,
            CarrierSenseThreshold = 0x17,
            RSSIOffset = 0x18,
            AutomaticGainControl3 = 0x19,
            AutomaticGainControl2 = 0x1a,
            AutomaticGainControl1 = 0x1b,
            AutomaticGainControl0 = 0x1c,
            FIFOConfiguration = 0x1d,
            DeviceAddress = 0x1e,
            FrequencySynthesizerCalibrationAndSettling = 0x1f,
            FrequencySynthesizerConfiguration = 0x20,
            EWORConfiguration1 = 0x21,
            EWORConfiguration0 = 0x22,
            Event0MSB = 0x23,
            Event0LSB = 0x24,
            RXDutyCycleMode = 0x25,
            PacketConfiguration2 = 0x26,
            PacketConfiguration1 = 0x27,
            PacketConfiguration0 = 0x28,
            RFENDConfiguration1 = 0x29,
            RFENDConfiguration0 = 0x2a,
            PowerAmplifier1 = 0x2b,
            PowerAmplifier0 = 0x2c,
            ASKConfiguration = 0x2d,
            PacketLength = 0x2e,
            ExtendedAddress = 0x2f,

            //command strobes
            SRES = 0x30,
            SFSTXON = 0x31,
            SXOFF = 0x32,
            SCAL = 0x33,
            SRX = 0x34,
            STX = 0x35,
            SIDLE = 0x36,
            SAFC = 0x37,
            SWOR = 0x38,
            SPWD = 0x39,
            SFRX = 0x3a,
            SFTX = 0x3b,
            SWORRST = 0x3c,
            SNOP = 0x3d,
        }
        private enum ExtendedRegisters
        {
            IFMixConfiguration = 0x0,
            FrequencyOffsetCorrection = 0x1,
            TimingOffsetCorrection = 0x2,
            MARCSpare = 0x3,
            ExternalClockFrequency = 0x4,
            GeneralModemParameter2 = 0x5,
            ExternalControl = 0x6,
            RCOscillatorCalibrationFine = 0x7,
            RCOscillatorCalibrationCourse = 0x8,
            RCOscillatorCalibrationClockOffset = 0x9,
            FrequencyOffsetMSB = 0xa,
            FrequencyOffsetLSB = 0xb,
            Frequency2 = 0xc,
            Frequency1 = 0xd,
            Frequency0 = 0xe,
            ADCConfiguration2 = 0xf,
            ADCConfiguration1 = 0x10,
            ADCConfiguration0 = 0x11,
            FrequencySynthesizerDigital1 = 0x12,
            FrequencySynthesizerDigital0 = 0x13,
            FrequencySynthesizerCalibration3 = 0x14,
            FrequencySynthesizerCalibration2 = 0x15,
            FrequencySynthesizerCalibration1 = 0x16,
            FrequencySynthesizerCalibration0 = 0x17,
            FrequencySynthesizerChargePump = 0x18,
            FrequencySynthesizerDivideByTwo = 0x19,
            DigitalSynthesizerModule1 = 0x1a,
            DigitalSynthesizerModule0 = 0x1b,
            FrequencySynthesizerDividerChain1 = 0x1c,
            FrequencySynthesizerDividerChain0 = 0x1d,
            FrequencySynthesizerLocalBias = 0x1e,
            FrequencySynthesizerPhaseFrequencyDetector = 0x1f,
            FrequencySynthesizerPrescaler = 0x20,
            FrequencySynthesizerDividerRegulator = 0x21,
            FrequencySynthesizerSpare = 0x22,
            FrequencySynthesizerVoltageControlOscillator4 = 0x23,
            FrequencySynthesizerVoltageControlOscillator3 = 0x24,
            FrequencySynthesizerVoltageControlOscillator2 = 0x25,
            FrequencySynthesizerVoltageControlOscillator1 = 0x26,
            FrequencySynthesizerVoltageControlOscillator0 = 0x27,
            GlobalBias6 = 0x28,
            GlobalBias5 = 0x29,
            GlobalBias4 = 0x2a,
            GlobalBias3 = 0x2b,
            GlobalBias2 = 0x2c,
            GlobalBias1 = 0x2d,
            GlobalBias0 = 0x2e,
            IntermediateFrequencyAmplifier = 0x2f,
            LowNoiseAmplifier = 0x30,
            RXMixer = 0x31,
            CrystalOscillator5 = 0x32,
            CrystalOscillator4 = 0x33,
            CrystalOscillator3 = 0x34,
            CrystalOscillator2 = 0x35,
            CrystalOscillator1 = 0x36,
            CrystalOscillator0 = 0x37,
            AnalogSpare = 0x38,
            PowerAmplifier3 = 0x39,
            //0x3A-0x3E not used, 0x3F-0x40 reserved, 0x41-0x63 not used
            EWORTimerCounterValueMSB = 0x64,
            EWORTimerCounterValueLSB = 0x65,
            EWORTimerCaptureValueMSB = 0x66,
            EWORTimerCaptureValueLSB = 0x67,
            MARCBuiltInSelfTest = 0x68,
            DCFilterOffsetIMSB = 0x69,
            DCFilterOffsetILSB = 0x6a,
            DCFilterOffsetQMSB = 0x6b,
            DCFilterOffsetQLSB = 0x6c,
            IQImbalanceValueIMSB = 0x6d,
            IQImbalanceValueILSB = 0x6e,
            IQImbalanceValueQMSB = 0x6f,
            IQImbalanceValueQLSB = 0x70,
            ReceivedSignalStrengthIndicator1 = 0x71,
            ReceivedSignalStrengthIndicator0 = 0x72,
            MARCState = 0x73,
            LinkQualityIndicator = 0x74,
            PreambleAndSyncWordError = 0x75,
            DemodulatorStatus = 0x76,
            FrequencyOffsetEstimateMSB = 0x77,
            FrequencyOffsetEstimateLSB = 0x78,
            AutomaticGainControl3 = 0x79,
            AutomaticGainControl2 = 0x7a,
            AutomaticGainControl1 = 0x7b,
            AutomaticGainControl0 = 0x7c,
            CustomFrequencyModulationRxData = 0x7d,
            CustomFrequencyModulationTxData = 0x7e,
            ASKSoftDecisionOutput = 0x7f,
            RandomNumberGeneratorValue = 0x80,
            SignalMagnitudeAfterCORDIC2 = 0x81,
            SignalMagnitudeAfterCORDIC1 = 0x82,
            SignalMagnitudeAfterCORDIC0 = 0x83,
            SignalAngularAfterCORDIC1 = 0x84,
            SignalAngularAfterCORDIC0 = 0x85,
            ChannelFilterDataI2 = 0x86,
            ChannelFilterDataI1 = 0x87,
            ChannelFilterDataI0 = 0x88,
            ChannelFilterDataQ2 = 0x89,
            ChannelFilterDataQ1 = 0x8a,
            ChannelFilterDataQ0 = 0x8b,
            GPIOStatus = 0x8c,
            FrequencySynthesizerCalibration = 0x8d,
            FrequencySynthesizerPhaseAdjust = 0x8e,
            PartNumber = 0x8f,
            PartVersion = 0x90,
            SerialStatus = 0x91,
            ModemStatus1 = 0x92,
            ModemStatus0 = 0x93,
            MARCStatus1 = 0x94,
            MARCStatus0 = 0x95,
            PowerAmplifierIntermediateFrequencyAmplifierTest = 0x96,
            FrequencySynthesizerTest = 0x97,
            FrequencySynthesizerPrescalerTest = 0x98,
            FrequencySynthesizerPrescalerOverride = 0x99,
            AnalogToDigitalConverterTest = 0x9a,
            DigitalDividerChainTest = 0x9b,
            AnalogTest = 0x9c,
            AnalogTestLVDS = 0x9d,
            AnalogTestMode = 0x9e,
            CrystalOscillatorTest1 = 0x9f,
            CrystalOscillatorTest0 = 0xa0,
            AdvancedEncryptionStandardStatus = 0xa1,
            ModemTest = 0xa2,
            //0xA3-0xD1 not used
            RxFIFOPointerFirstEntry = 0xd2,
            TxFIFOPointerFirstEntry = 0xd3,
            RxFIFOPointerLastEntry = 0xd4,
            TxFIFOPointerLastEntry = 0xd5,
            TxFIFONumberOfBytes = 0xd6,
            RxFIFONumberOfBytes = 0xd7,
            TxFIFONumberOfFreeEntries = 0xd8,
            RxFIFONumberOfFreeEntries = 0xd9,
            RxFIFOFirstByteWhenEmpty = 0xda,
            //0xDB-0xDF not used
            AdvancedEncryptionStandardKey15 = 0xe0,
            AdvancedEncryptionStandardKey14 = 0xe1,
            AdvancedEncryptionStandardKey13 = 0xe2,
            AdvancedEncryptionStandardKey12 = 0xe3,
            AdvancedEncryptionStandardKey11 = 0xe4,
            AdvancedEncryptionStandardKey10 = 0xe5,
            AdvancedEncryptionStandardKey9 = 0xe6,
            AdvancedEncryptionStandardKey8 = 0xe7,
            AdvancedEncryptionStandardKey7 = 0xe8,
            AdvancedEncryptionStandardKey6 = 0xe9,
            AdvancedEncryptionStandardKey5 = 0xea,
            AdvancedEncryptionStandardKey4 = 0xeb,
            AdvancedEncryptionStandardKey3 = 0xec,
            AdvancedEncryptionStandardKey2 = 0xed,
            AdvancedEncryptionStandardKey1 = 0xee,
            AdvancedEncryptionStandardKey0 = 0xef,
            AdvancedEncryptionStandardBuffer15 = 0xf0,
            AdvancedEncryptionStandardBuffer14 = 0xf1,
            AdvancedEncryptionStandardBuffer13 = 0xf2,
            AdvancedEncryptionStandardBuffer12 = 0xf3,
            AdvancedEncryptionStandardBuffer11 = 0xf4,
            AdvancedEncryptionStandardBuffer10 = 0xf5,
            AdvancedEncryptionStandardBuffer9 = 0xf6,
            AdvancedEncryptionStandardBuffer8 = 0xf7,
            AdvancedEncryptionStandardBuffer7 = 0xf8,
            AdvancedEncryptionStandardBuffer6 = 0xf9,
            AdvancedEncryptionStandardBuffer5 = 0xfa,
            AdvancedEncryptionStandardBuffer4 = 0xfb,
            AdvancedEncryptionStandardBuffer3 = 0xfc,
            AdvancedEncryptionStandardBuffer2 = 0xfd,
            AdvancedEncryptionStandardBuffer1 = 0xfe,
            AdvancedEncryptionStandardBuffer0 = 0xff,
        }

    }
}

