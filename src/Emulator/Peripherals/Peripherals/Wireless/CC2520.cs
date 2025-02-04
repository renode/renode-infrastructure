//
// Copyright (c) 2010-2025 Antmicro
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

namespace Antmicro.Renode.Peripherals.Wireless
{
    public sealed class CC2520: IRadio, ISPIPeripheral, INumberedGPIOOutput, IGPIOReceiver
    {
        public CC2520()
        {
            CreateRegisters();
            RegisterInstructions();
            var dict = new Dictionary<int, IGPIO>();
            for(var i = 0; i < NumberOfGPIOs; ++i)
            {
                dict[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(dict);
            Reset();
        }

        public void Reset()
        {
            isRxEnabled = false;
            inReset = true;
            vregEnabled = false;
            oscillatorRunning = false;
            wasLastFrameSent = false;

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

            currentInstruction = null;

            registers.Reset();
            foreach(var gpio in Connections.Values)
            {
                gpio.Unset();
            }
        }

        public void OnGPIO(int number, bool value)
        {
            const int voltageRegulatorEnable = 0;
            const int notReset = 1;
            if(number == notReset)
            {
                inReset = !value;
                if(!inReset)
                {
                    oscillatorRunning = true;
                }
            }
            else if(number == voltageRegulatorEnable)
            {
                vregEnabled = value;
            }
            else
            {
                //we don't want to accidentally reset on another input
                return;
            }
            if(inReset && !vregEnabled)
            {
                this.Log(LogLevel.Debug, "Resetting radio...");
                Reset();
            }
            UpdateInterrupts();
        }

        public bool[] Irqs()
        {
            return Connections.Values.Select(x => x.IsSet).ToArray();
        }

        public byte Transmit(byte data)
        {
            this.Log(LogLevel.Noisy, "Writing to radio: 0x{0:X}", data);
            if(currentInstruction == null)
            {
                if(!decoderRoot.TryParseOpcode(data, out currentInstruction))
                {
                    this.Log(LogLevel.Error, "Cannot find opcode in value 0b{0} (0x{1:X})".FormatWith(Convert.ToString(data, 2).PadLeft(8,'0'), data));
                    return 0;
                }
                this.Log(LogLevel.Debug, "Setting command to: {0}", currentInstruction.Name);
            }
            var returnValue = currentInstruction.Parse(data);
            if(currentInstruction.IsFinished)
            {
                currentInstruction = null;
            }
            return returnValue;
        }

        public void FinishTransmission()
        {
            currentInstruction = null;
            this.Log(LogLevel.Debug, "Finish transmission");
        }

        public void ReceiveFrame(byte[] bytes, IRadio sender)
        {
            if(isRxEnabled)
            {
                //this allows to have proper CCA values easily.
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
                return ChannelValueFromFrequency((uint)channel.Value);
            }

            set
            {
                this.Log(LogLevel.Info, "Setting channel to {0}", value);
                channel.Value = ChannelNumberToFrequency(value);
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections
        {
            get; private set;
        }

        public event Action<IRadio, byte[]> FrameSent;

        private void HandleFrame(byte[] bytes)
        {
            Frame ackFrame = null;

            SetException(ExceptionFlags.StartOfFrameDelimiter);
            var frame = new Frame(bytes);
            var isCrcOk = frame.CheckCRC();

            var autoPendingResult = false;
            byte sourceMatchingIndex = NoSourceIndex;

            if(frameFilteringEnabled.Value)
            {
                if(!ShouldAcceptFrame(frame))
                {
                    this.Log(LogLevel.Debug, "Not accepting the frame");
                    return;
                }
                SetException(ExceptionFlags.RxFrameAccepted);

                if(sourceMatchingEnabled.Value)
                {
                    //algorithm described in the docs, chapter 20.3.3
                    var sourceMatchingMask = 0u;
                    if(frame.SourceAddressingMode == AddressingMode.ShortAddress)
                    {
                        uint sourceMatchingShortEnabled = (uint)((shortAddressMatchingEnabled[2].Value << 16) | (shortAddressMatchingEnabled[1].Value << 8) | shortAddressMatchingEnabled[0].Value);
                        for(byte i = 0; i < 24; ++i)
                        {
                            var mask = 1u << i;
                            if((sourceMatchingShortEnabled & mask) == 0)
                            {
                                continue;
                            }
                            if(frame.AddressInformation.SourcePan == GetSourceAddressMatchingPanId(i)
                               && frame.AddressInformation.SourceAddress.GetValue() == GetSourceAddressMatchingShortAddress(i))
                            {
                                sourceMatchingMask |= mask;
                                if(sourceMatchingIndex == NoSourceIndex)
                                {
                                    autoPendingResult = VerifyAutoPending(frame.SourceAddressingMode, frame.Type, i);
                                    sourceMatchingIndex = (byte)(i | (autoPendingResult ? 1 << 6 : 0));
                                }
                            }
                        }
                    }
                    else if(frame.SourceAddressingMode == AddressingMode.ExtendedAddress)
                    {
                        uint sourceMatchingExtendedEnabled = (uint)((extendedAddressMatchingEnabled[2].Value << 16) | (extendedAddressMatchingEnabled[1].Value << 8) | extendedAddressMatchingEnabled[0].Value);
                        for(byte i = 0; i < 12; ++i)
                        {
                            var mask = 3u << (2 * i);
                            if((sourceMatchingExtendedEnabled & mask) == 0)
                            {
                                continue;
                            }
                            if(frame.AddressInformation.SourceAddress.GetValue() == GetSourceAddressMatchingExtendedAddress(i))
                            {
                                sourceMatchingMask |= mask;
                                if(sourceMatchingIndex == NoSourceIndex)
                                {
                                    autoPendingResult = VerifyAutoPending(frame.SourceAddressingMode, frame.Type, i);
                                    sourceMatchingIndex = (byte)(i | 0x20 | (autoPendingResult ? 1 << 6 : 0));
                                }
                            }
                        }
                    }
                    BitConverter.GetBytes(sourceMatchingMask).CopyTo(sourceAddressMatchingResult, 0);
                    sourceAddressMatchingResult[3] = sourceMatchingIndex;
                    SetException(ExceptionFlags.SourceMatchingDone);
                    if(sourceMatchingIndex != NoSourceIndex)
                    {
                        SetException(ExceptionFlags.SourceMatchingFound);
                    }
                }
            }
            if(autoCrc.Value)
            {
                byte secondByte = 0;
                if(isCrcOk)
                {
                    secondByte |= 1 << 7;
                }
                if(appendDataMode.Value)
                {
                    //Theoretically sourceMatchingIndex might not be valid here if the matching was not performed.
                    //This set of conditions is defined in the docs, though.
                    secondByte |= sourceMatchingIndex;
                }
                else
                {
                    secondByte |= 100; // correlation value 100 means near maximum quality
                }
                frame.Bytes[frame.Bytes.Length - 2] = 70; //as in CC2538
                frame.Bytes[frame.Bytes.Length - 1] = secondByte;
            }
            rxFifo.Enqueue(frame.Length);
            foreach(var item in frame.Bytes)
            {
                rxFifo.Enqueue(item);
            }
            //Not filtering for length because a full frame was received. This might be an issue in a more general sense, as we do not support partial frames.
            SetException(ExceptionFlags.FifoThresholdReached);

            SetException(ExceptionFlags.RxFrameDone);
            if(isCrcOk && autoAck.Value
                    && frame.AcknowledgeRequest
                    && frame.Type != FrameType.Beacon
                    && frame.Type != FrameType.ACK)
            {
                ackFrame = Frame.CreateACK(frame.DataSequenceNumber, autoPendingResult || pendingAlwaysOn.Value);
            }
            var frameSent = FrameSent;
            if(frameSent != null && ackFrame != null)
            {
                frameSent(this, ackFrame.Bytes);
                SetException(ExceptionFlags.TxAckDone);
            }
        }

        bool VerifyAutoPending(AddressingMode mode, FrameType type, byte i)
        {
            if(!autoPendingFlag.Value)
            {
                return false;
            }
            if(pendingDataRequestOnly.Value && type != FrameType.Data)
            {
                return false;
            }
            var index = i / 8;
            var position = (byte)(i % 8);
            if(mode == AddressingMode.ShortAddress)
            {
                //first three bytes are for extended addresses, then three short addresses
                index += 3;
            }
            return BitHelper.IsBitSet(sourceAddressMatchingControl[index], position);
        }

        private ushort GetSourceAddressMatchingPanId(int i)
        {
            return BitConverter.ToUInt16(sourceAddressTable, 4 * i);
        }

        private ushort GetSourceAddressMatchingShortAddress(int i)
        {
            return BitConverter.ToUInt16(sourceAddressTable, 4 * i + 2);
        }

        private ulong GetSourceAddressMatchingExtendedAddress(int i)
        {
            return BitConverter.ToUInt64(sourceAddressTable, 8 * i);
        }

        private bool ShouldAcceptFrame(Frame frame)
        {
            var frameType = frame.Type;
            switch(modifyFrameTypeFilter.Value)
            {
            case ModifyFieldTypeMode.InvertMSB:
                frameType = (FrameType)((int)frameType ^ 4);
                break;
            case ModifyFieldTypeMode.SetMSB:
                frameType = (FrameType)((int)frameType | 4);
                break;
            case ModifyFieldTypeMode.UnsetMSB:
                frameType = (FrameType)((int)frameType & 3);
                break;
            }

            //1. Check minimum frame length
            //2. Check reserved FCF bits
            if((((frame.FrameControlField >> 7) & 7) & frameControlFieldReservedMask.Value) != 0)
            {
                this.Log(LogLevel.Noisy, "Wrong FCF reserved field. Dropping frame.");
                return false;
            }

            //3. Check frame version
            if(frame.FrameVersion > maxFrameVersion.Value)
            {
                this.Log(LogLevel.Noisy, "Frame version too high. Got 0x{0:X}, expected at most 0x{1:X}. Dropping frame.", frame.FrameVersion, maxFrameVersion.Value);
                return false;
            }

            //4. Check address modes
            if(frame.SourceAddressingMode == AddressingMode.Reserved)
            {
                this.Log(LogLevel.Noisy, "Reserved source addressing mode. Dropping frame.");
                return false;
            }
            if(frame.DestinationAddressingMode == AddressingMode.Reserved)
            {
                this.Log(LogLevel.Noisy, "Reserved destination addressing mode. Dropping frame.");
                return false;
            }

            //5. Check destination address
            if(frame.DestinationAddressingMode != AddressingMode.None)
            {
                //this IntraPAN is kind of a guess. We could use source PAN Id instead, but the documentation says clearly: "If a destination PAN ID is included in the frame"
                if(!frame.IntraPAN && frame.AddressInformation.DestinationPan != GetPanId() && frame.AddressInformation.DestinationPan != BroadcastPanIdentifier)
                {
                    this.Log(LogLevel.Noisy, "Invalid destination PAN: got 0x{0:X}, expected 0x{1:X} or 0x{2:X}. Dropping frame.", frame.AddressInformation.DestinationPan, GetPanId(), BroadcastPanIdentifier);
                    return false;
                }
                if(frame.DestinationAddressingMode == AddressingMode.ShortAddress)
                {
                    if(!frame.AddressInformation.DestinationAddress.IsShortBroadcast && !frame.AddressInformation.DestinationAddress.Equals(localShortAddress))
                    {
                        this.Log(LogLevel.Noisy, "Invalid destination short address (I'm {0}, but the message is directed to {1}). Dropping frame.", localShortAddress.GetValue(), frame.AddressInformation.DestinationAddress.GetValue());
                        return false;
                    }
                }
                // (5.3) check destination extended address
                else if(frame.DestinationAddressingMode == AddressingMode.ExtendedAddress)
                {
                    if(!frame.AddressInformation.DestinationAddress.Equals(localExtendedAddress))
                    {
                        this.Log(LogLevel.Noisy, "Invalid destination extended address (I'm {0}, but the message is directed to {1}). Dropping frame.", localExtendedAddress.GetValue(), frame.AddressInformation.DestinationAddress.GetValue());
                        return false;
                    }
                }
            }
            //6. Check frame type
            switch(frameType)
            {
            case FrameType.Beacon:
                if(!acceptBeaconFrames.Value
                   || frame.Length < 9
                   || frame.DestinationAddressingMode != AddressingMode.None
                   || (frame.SourceAddressingMode != AddressingMode.ShortAddress && frame.SourceAddressingMode != AddressingMode.ExtendedAddress)
                   || (frame.AddressInformation.SourcePan != BroadcastPanIdentifier && frame.AddressInformation.SourcePan != GetPanId()))
                {
                    this.Log(LogLevel.Noisy, "Beacon frame not accepted. Dropping frame.");
                    return false;
                }
                break;
            case FrameType.Data:
                if(!acceptDataFrames.Value
                   || frame.Length < 9
                   || (frame.DestinationAddressingMode == AddressingMode.None && frame.SourceAddressingMode == AddressingMode.None)
                   || (frame.DestinationAddressingMode == AddressingMode.None
                       && (!panCoordinator.Value || frame.AddressInformation.SourcePan != GetPanId())))
                {
                    this.Log(LogLevel.Noisy, "Data frame not accepted. Dropping frame.");
                    return false;
                }
                break;
            case FrameType.ACK:
                if(!acceptAckFrames.Value || frame.Length != 5)
                {
                    this.Log(LogLevel.Noisy, "ACK frame not accepted. Dropping frame.");
                    return false;
                }
                break;
            case FrameType.MACControl:
                if(!acceptMacCommandFrames.Value
                   || frame.Length < 9
                   || (frame.DestinationAddressingMode == AddressingMode.None
                       && (!panCoordinator.Value || frame.AddressInformation.SourcePan != GetPanId())))
                {
                    this.Log(LogLevel.Noisy, "MAC control frame not accepted. Dropping frame.");
                    return false;
                }
                break;
            default:
                if(!acceptReservedFrames.Value || frame.Length < 9)
                {
                    this.Log(LogLevel.Noisy, "Reserved frame not accepted. Dropping frame.");
                    return false;
                }
                break;
            }
            return true;
        }

        private void SendFrame()
        {
            var length = txFifo.Peek();
            var data = txFifo.Skip(1).ToArray();
            Frame frame;
            if(autoCrc.Value)
            {
                var crc = Frame.CalculateCRC(data);
                frame = new Frame(data.Concat(crc).ToArray());
            }
            else
            {
                frame = new Frame(data);
            }
            if(length > frame.Length && !ignoreTxUnderflow.Value)
            {
                SetException(ExceptionFlags.TxUnderflow);
                this.Log(LogLevel.Warning, "Frame dropped because of TX underflow. Expected length: {0}. Frame length: {1}.", length, frame.Length);
                return;
                //ignoreTxUnderflow should probably transmit whatever data is written to the tx fifo after this transfer. This would be difficult to model, as we do not support "wrong" frames yet.
            }
            this.DebugLog("Sending frame {0}.", frame.Bytes.Select(x => "0x{0:X}".FormatWith(x)).Stringify());
            var frameSent = FrameSent;
            if(frameSent != null)
            {
                frameSent(this, frame.Bytes);
            }
            wasLastFrameSent = true;
            SetException(ExceptionFlags.StartOfFrameDelimiter);
            SetException(ExceptionFlags.TxFrameDone);
        }

        private void SetException(ExceptionFlags flag)
        {
            var regNumber = (int)flag / 8;
            var bit = (int)flag % 8;
            this.Log(LogLevel.Noisy, "Setting flag {0}", flag);
            pendingExceptionFlag[regNumber].Value |= 1u << bit;
            UpdateInterrupts();
        }

        private void UnsetException(ExceptionFlags flag)
        {
            var regNumber = (int)flag / 8;
            var bit = (int)flag % 8;
            this.Log(LogLevel.Noisy, "Unsetting flag {0}", flag);
            pendingExceptionFlag[regNumber].Value &= ~(1u << bit);
            UpdateInterrupts();
        }

        private bool IsExceptionSet(ExceptionFlags flag)
        {
            var regNumber = (int)flag / 8;
            var bit = (int)flag % 8;
            return (pendingExceptionFlag[regNumber].Value & (1u << bit)) != 0;
        }

        private void UpdateInterrupts()
        {
            Connections[(int)GPIOs.Cca].Set(currentFrame == null);
            Connections[(int)GPIOs.Sfd].Set(IsExceptionSet(ExceptionFlags.StartOfFrameDelimiter));
            Connections[(int)GPIOs.Fifop].Set(IsExceptionSet(ExceptionFlags.FifoThresholdReached));
            Connections[(int)GPIOs.Fifo].Set(rxFifo.Count > 0 && rxFifo.Count <= RxFifoMemorySize);
        }

        private byte GetStatus()
        {
            return (byte)(((oscillatorRunning ? 1 : 0) << 7)
                          | ((isRxEnabled ? 1 : 0) << 6)
                          | ((currentInstruction != null && currentInstruction.HighPriority.HasValue && currentInstruction.HighPriority.Value ? 1 : 0) << 3)
                          | ((currentInstruction != null && currentInstruction.HighPriority.HasValue && !currentInstruction.HighPriority.Value ? 1 : 0) << 2)
                          | ((isRxEnabled ? 1 : 0) << 0));
            //bits 0 and 1 are responsible for RX/TX states. TX is active after STXON[CCA] command, so it's not interruptible with GetStatus read.
            //RX is active when isRxEnabled == true, but TX is not active. Again, since cpu will not get status on TX, we ignore the second condition.
            //DPU bits are mutually exclusive at the moment, but we should implement priorities someday, so I leave them as independent.
            //EXCEPTION channel A and B bits should be implemented
            //RSSI is always assumed to be valid in RX mode
        }

        private uint GetPanId()
        {
            return (uint)((localAddressInfo[9] << 8) | localAddressInfo[8]);
        }

        private int ChannelValueFromFrequency(uint frequency)
        {
            //According to documentation, chapter 16:
            //"Channels are numbered 11 through 26 and are 5MHz apart"
            return ((int)frequency - 11) / 5 + 11;
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
                {(long)Registers.FrameFiltering0, new ByteRegister(this, 0xD)
                                .WithValueField(4, 3, out frameControlFieldReservedMask, name: "FCF_RESERVED_MASK")
                                .WithValueField(2, 2, out maxFrameVersion, name: "MAX_FRAME_VERSION")
                                .WithFlag(1, out panCoordinator, name: "PAN_COORDINATOR")
                                .WithFlag(0, out frameFilteringEnabled, name: "FRAME_FILTER_EN")
                },
                {(long)Registers.FrameFiltering1, new ByteRegister(this, 0x78)
                                .WithFlag(7, out acceptReservedFrames, name: "ACCEPT_FT_4TO7_RESERVED")
                                .WithFlag(6, out acceptMacCommandFrames, name: "ACCEPT_FT_3_MAC_CMD")
                                .WithFlag(5, out acceptAckFrames, name: "ACCEPT_FT_2_ACK")
                                .WithFlag(4, out acceptDataFrames, name: "ACCEPT_FT_1_DATA")
                                .WithFlag(3, out acceptBeaconFrames, name: "ACCEPT_FT_0_BEACON")
                                .WithEnumField(1, 2, out modifyFrameTypeFilter, name: "MODIFY_FT_FILTER")
                },
                {(long)Registers.SourceMatching, new ByteRegister(this, 0x7)
                                .WithFlag(2, out pendingDataRequestOnly, name: "PEND_DATAREQ_ONLY")
                                .WithFlag(1, out autoPendingFlag, name: "AUTOPEND")
                                .WithFlag(0, out sourceMatchingEnabled, name: "SRC_MATCH_EN")
                },
                {(long)Registers.ShortAddressMatchingEnabled0, new ByteRegister(this)
                                .WithValueField(0, 8, out shortAddressMatchingEnabled[0], name: "SHORT_ADDR_EN[7:0]")
                },
                {(long)Registers.ShortAddressMatchingEnabled1, new ByteRegister(this)
                                .WithValueField(0, 8, out shortAddressMatchingEnabled[1], name: "SHORT_ADDR_EN[15:8]")
                },
                {(long)Registers.ShortAddressMatchingEnabled2, new ByteRegister(this)
                                .WithValueField(0, 8, out shortAddressMatchingEnabled[2], name: "SHORT_ADDR_EN[23:16]")
                },
                {(long)Registers.ExtendedAddressMatchingEnabled0, new ByteRegister(this)
                                .WithValueField(0, 8, out extendedAddressMatchingEnabled[0], name: "EXT_ADDR_EN[7:0]")
                },
                {(long)Registers.ExtendedAddressMatchingEnabled1, new ByteRegister(this)
                                .WithValueField(0, 8, out extendedAddressMatchingEnabled[1], name: "EXT_ADDR_EN[15:8]")
                },
                {(long)Registers.ExtendedAddressMatchingEnabled2, new ByteRegister(this)
                                .WithValueField(0, 8, out extendedAddressMatchingEnabled[2], name: "EXT_ADDR_EN[23:16]")
                },
                {(long)Registers.FrameControl0, new ByteRegister(this, 0x40)
                                .WithFlag(7, out appendDataMode, name: "APPEND_DATA_MODE")
                                .WithFlag(6, out autoCrc, name: "AUTOCRC")
                                .WithFlag(5, out autoAck, name: "AUTOACK")
                                .WithTag("ENERGY_SCAN", 4, 1)
                                .WithTag("RX_MODE", 2, 2) //I doubt these two will be useful, they are test modes
                                .WithTag("TX_MODE", 0, 2)
                },
                {(long)Registers.FrameControl1, new ByteRegister(this, 0x1)
                                .WithFlag(2, out pendingAlwaysOn, name: "PENDING_OR")
                                .WithFlag(1, out ignoreTxUnderflow, name: "IGNORE_TX_UNDERF")
                                .WithTag("SET_RXENMASK_ON_TX", 0, 1)
                },
                {(long)Registers.PendingExceptionFlags0, new ByteRegister(this)
                                .WithValueField(0, 8, out pendingExceptionFlag[0], FieldMode.Read | FieldMode.WriteZeroToClear, name: "EXCFLAG0")
                },
                {(long)Registers.PendingExceptionFlags1, new ByteRegister(this)
                                .WithValueField(0, 8, out pendingExceptionFlag[1], FieldMode.Read | FieldMode.WriteZeroToClear, name: "EXCFLAG1")
                },
                {(long)Registers.PendingExceptionFlags2, new ByteRegister(this)
                                .WithValueField(0, 8, out pendingExceptionFlag[2], FieldMode.Read | FieldMode.WriteZeroToClear, name: "EXCFLAG2")
                },
                {(long)Registers.ExceptionMaskA0, new ByteRegister(this)
                                .WithValueField(0, 8, out pendingExceptionMaskA[0], name: "EXCMASKA0")
                },
                {(long)Registers.ExceptionMaskA1, new ByteRegister(this)
                                .WithValueField(0, 8, out pendingExceptionMaskA[1], name: "EXCMASKA1")
                },
                {(long)Registers.ExceptionMaskA2, new ByteRegister(this)
                                .WithValueField(0, 8, out pendingExceptionMaskA[2], name: "EXCMASKA2")
                },
                {(long)Registers.ExceptionMaskB0, new ByteRegister(this)
                                .WithValueField(0, 8, out pendingExceptionMaskB[0], name: "EXCMASKB0")
                },
                {(long)Registers.ExceptionMaskB1, new ByteRegister(this)
                                .WithValueField(0, 8, out pendingExceptionMaskB[1], name: "EXCMASKB1")
                },
                {(long)Registers.ExceptionMaskB2, new ByteRegister(this)
                                .WithValueField(0, 8, out pendingExceptionMaskB[2], name: "EXCMASKB2")
                },
                {(long)Registers.FrequencyControl, new ByteRegister(this, 0x0B)
                                .WithValueField(0, 7, out channel, changeCallback: (_, chanVal) => this.Log(LogLevel.Info, "Setting channel to {0}", ChannelValueFromFrequency((uint)chanVal)), name: "FREQ")
                },
                {(long)Registers.TxPower, new ByteRegister(this, 0x6)
                                .WithValueField(0, 8, name: "PA_POWER")
                },
                {(long)Registers.FifoPControl, new ByteRegister(this, 0x40)
                                .WithValueField(0, 7, out fifopThreshold, name: "FIFOP_THR")
                },
                {(long)Registers.CCAThreshold, new ByteRegister(this, 0xE0)
                                .WithValueField(0, 8, name: "CCA_THR")
                },
                {(long)Registers.RSSIValidStatus, new ByteRegister(this)
                                .WithFlag(0, FieldMode.Read, valueProviderCallback: (_) => isRxEnabled, name: "RSSISTAT")
                },
                {(long)Registers.RxFirstByte, new ByteRegister(this)
                                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => rxFifo.Count == 0 ? 0u : rxFifo.Peek(), name: "RXFIRST")
                },
                {(long)Registers.RxFifoCount, new ByteRegister(this)
                                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => (byte)rxFifo.Count, name: "RXFIFOCNT")
                },
                {(long)Registers.TxFifoCount, new ByteRegister(this)
                                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => (byte)txFifo.Count, name: "TXFIFOCNT")
                },
                {(long)Registers.ChipId, new ByteRegister(this, 0x84)
                                .WithValueField(0, 8, FieldMode.Read, name: "CHIPID")
                },
                {(long)Registers.ExternalClock, new ByteRegister(this, 0x20)
                                .WithTag("EXTCLOCK_EN", 5, 1) //if this is set and we have a gpio configured for external clock, then it means we need an internal timer implemented here.
                                .WithValueField(0, 5, name: "EXT_FREQ")
                },
                {(long)Registers.ModemControl0, new ByteRegister(this, 0x45)
                                .WithValueField(6, 2, name: "DEM_NUM_ZEROS")
                                .WithTag("DEMOD_AVG_MODE", 5, 1)
                                .WithTag("PREAMBLE_LENGTH", 1, 4)
                                .WithTag("TX_FILTER", 0, 1)
                },
                {(long)Registers.ModemControl1, new ByteRegister(this, 0x2E)
                                .WithFlag(5, name: "CORR_THR_SFD")
                                .WithValueField(0, 4, name: "CORR_THR")
                },
                {(long)Registers.RxModuleTuning, new ByteRegister(this, 0x29)
                                .WithValueField(0, 8)
                },
                {(long)Registers.SynthesizerTuning, new ByteRegister(this, 0x55)
                                .WithValueField(0, 8)
                },
                {(long)Registers.VCOTuning1, new ByteRegister(this, 0x29)
                                .WithValueField(0, 8)
                },
                {(long)Registers.AGCTuning1, new ByteRegister(this, 0xE)
                                .WithValueField(0, 8)
                },
                {(long)Registers.ADCTest0, new ByteRegister(this, 0x66)
                                .WithValueField(0, 8)
                },
                {(long)Registers.ADCTest1, new ByteRegister(this, 0xA)
                                .WithValueField(0, 8)
                },
                {(long)Registers.ADCTest2, new ByteRegister(this, 0x5)
                                .WithValueField(0, 8)
                },
            };
            registers = new ByteRegisterCollection(this, dict);
        }

        private byte ReadMemory(uint address)
        {
            byte value = 0;
            if(address < RegisterMemorySize)
            {
                if(!registers.TryRead(address, out value))
                {
                    this.Log(LogLevel.Warning, "Failed to read register {0}", (Registers)address);
                    if((address >= 0x64 && address <= 0x79))
                    {
                        //Other addresses do not trigger the exception
                        SetException(ExceptionFlags.MemoryAddressError);
                    }
                    return 0;
                }
                this.Log(LogLevel.Debug, "Successfully read register {0}, value 0x{1:X}", (Registers)address, value);
            }
            else if(address >= TxFifoMemoryStart && address < TxFifoMemoryStart + TxFifoMemorySize)
            {
                this.Log(LogLevel.Error, "Direct access to txFifo is not supported. Trying to access 0x{0:X}", address);
            }
            else if(address >= RxFifoMemoryStart && address < RxFifoMemoryStart + RxFifoMemorySize)
            {
                this.Log(LogLevel.Error, "Direct access to rxFifo is not supported. Trying to access 0x{0:X}", address);
            }
            else if(address >= GeneralMemoryStart && address < GeneralMemoryStart + GeneralMemorySize)
            {
                value = memory[address - GeneralMemoryStart];
                this.Log(LogLevel.Debug, "Read memory 0x{0:X}, value 0x{1:X}", address, value);
            }
            else if(address >= SourceAddressTableStart && address < SourceAddressTableStart + SourceAddressTableSize)
            {
                value = sourceAddressTable[address - SourceAddressTableStart];
                this.Log(LogLevel.Debug, "Read sourceAddressTable 0x{0:X}, value 0x{1:X}", address, value);
            }
            else if(address >= SourceAddressMatchingResultStart && address < SourceAddressMatchingResultStart + SourceAddressMatchingResultSize)
            {
                value = sourceAddressMatchingResult[address - SourceAddressMatchingResultStart];
                this.Log(LogLevel.Debug, "Read sourceAddressMatchingResult 0x{0:X}, value 0x{1:X}", address, value);
            }
            else if(address >= SourceAddressMatchingControlStart && address < SourceAddressMatchingControlStart + SourceAddressMatchingControlSize)
            {
                value = sourceAddressMatchingControl[address - SourceAddressMatchingControlStart];
                this.Log(LogLevel.Debug, "Read sourceAddressMatchingControl 0x{0:X}, value 0x{1:X}", address, value);
            }
            else if(address >= LocalAddressInfoStart && address < LocalAddressInfoStart + LocalAddressInfoSize)
            {
                value = localAddressInfo[address - LocalAddressInfoStart];
                this.Log(LogLevel.Debug, "Read localAddressInfo 0x{0:X}, value 0x{1:X}", address, value);
            }
            else
            {
                SetException(ExceptionFlags.MemoryAddressError);
            }
            return value;
        }

        private void WriteMemory(uint address, byte value)
        {
            if(address < RegisterMemorySize)
            {
                if(!registers.TryWrite(address, value))
                {
                    this.Log(LogLevel.Warning, "Failed to write register {0}, value 0x{1:X}", (Registers)address, value);
                    if((address >= 0x64 && address <= 0x79))
                    {
                        //Other addresses do not trigger the exception
                        SetException(ExceptionFlags.MemoryAddressError);
                    }
                }
                this.Log(LogLevel.Debug, "Successfully written register {0}, value 0x{1:X}", (Registers)address, value);
            }
            else if(address >= TxFifoMemoryStart && address < TxFifoMemoryStart + TxFifoMemorySize)
            {
                this.Log(LogLevel.Error, "Direct access to txFifo is not supported. Trying to access 0x{0:X}, value 0x{1:X}", address, value);
            }
            else if(address >= RxFifoMemoryStart && address < RxFifoMemoryStart + RxFifoMemorySize)
            {
                this.Log(LogLevel.Error, "Direct access to rxFifo is not supported. Trying to access 0x{0:X}, value 0x{1:X}", address, value);
            }
            else if(address >= GeneralMemoryStart && address < GeneralMemoryStart + GeneralMemorySize)
            {
                memory[address - GeneralMemoryStart] = value;
                this.Log(LogLevel.Debug, "Written mem 0x{0:X}, value 0x{1:X}", address, value);
            }
            else if(address >= SourceAddressTableStart && address < SourceAddressTableStart + SourceAddressTableSize)
            {
                sourceAddressTable[address - SourceAddressTableStart] = value;
                this.Log(LogLevel.Debug, "Written sourceAddressTable 0x{0:X}, value 0x{1:X}", address, value);
            }
            else if(address >= SourceAddressMatchingResultStart && address < SourceAddressMatchingResultStart + SourceAddressMatchingResultSize)
            {
                sourceAddressMatchingResult[address - SourceAddressMatchingResultStart] = value;
                this.Log(LogLevel.Debug, "Written sourceAddressMatchingResult 0x{0:X}, value 0x{1:X}", address, value);
            }
            else if(address >= SourceAddressMatchingControlStart && address < SourceAddressMatchingControlStart + SourceAddressMatchingControlSize)
            {
                sourceAddressMatchingControl[address - SourceAddressMatchingControlStart] = value;
                this.Log(LogLevel.Debug, "Written sourceAddressMatchingControl 0x{0:X}, value 0x{1:X}", address, value);
            }
            else if(address >= LocalAddressInfoStart && address < LocalAddressInfoStart + LocalAddressInfoSize)
            {
                localAddressInfo[address - LocalAddressInfoStart] = value;
                this.Log(LogLevel.Debug, "Written localAddressInfo 0x{0:X}, value 0x{1:X}", address, value);
            }
            else
            {
                SetException(ExceptionFlags.MemoryAddressError);
            }
        }

        private void RegisterInstructions()
        {
            decoderRoot.AddOpcode(0x00, 8, () => new SNOP { Parent = this });
            decoderRoot.AddOpcode(0x10, 4, () => new MEMRD { Parent = this });
            decoderRoot.AddOpcode(0x20, 4, () => new MEMWR { Parent = this });
            decoderRoot.AddOpcode(0x30, 8, () => new RXBUF { Parent = this });
            decoderRoot.AddOpcode(0x32, 7, () => new RXBUFMOV { Parent = this });
            decoderRoot.AddOpcode(0x3A, 8, () => new TXBUF { Parent = this });
            decoderRoot.AddOpcode(0x40, 8, () => new SXOSCON { Parent = this });
            decoderRoot.AddOpcode(0x42, 8, () => new SRXON { Parent = this });
            decoderRoot.AddOpcode(0x44, 8, () => new STXONCCA { Parent = this });
            decoderRoot.AddOpcode(0x45, 8, () => new SRFOFF { Parent = this });
            decoderRoot.AddOpcode(0x46, 8, () => new SXOSCOFF { Parent = this });
            decoderRoot.AddOpcode(0x47, 8, () => new SFLUSHRX { Parent = this });
            decoderRoot.AddOpcode(0x48, 8, () => new SFLUSHTX { Parent = this });
            decoderRoot.AddOpcode(0x80, 2, () => new REGRD { Parent = this });
            decoderRoot.AddOpcode(0xC0, 2, () => new REGWR { Parent = this });
        }

        private IValueRegisterField frameControlFieldReservedMask;
        private IValueRegisterField maxFrameVersion;
        private IFlagRegisterField panCoordinator;
        private IFlagRegisterField frameFilteringEnabled;

        private IFlagRegisterField acceptBeaconFrames;
        private IFlagRegisterField acceptDataFrames;
        private IFlagRegisterField acceptAckFrames;
        private IFlagRegisterField acceptMacCommandFrames;
        private IFlagRegisterField acceptReservedFrames;

        private IEnumRegisterField<ModifyFieldTypeMode> modifyFrameTypeFilter;

        private IFlagRegisterField pendingDataRequestOnly;
        private IFlagRegisterField autoPendingFlag;
        private IFlagRegisterField sourceMatchingEnabled;

        private readonly IValueRegisterField[] shortAddressMatchingEnabled = new IValueRegisterField[3];
        private readonly IValueRegisterField[] extendedAddressMatchingEnabled = new IValueRegisterField[3];

        private IFlagRegisterField autoCrc;
        private IFlagRegisterField autoAck;
        private IFlagRegisterField appendDataMode;

        private IFlagRegisterField ignoreTxUnderflow;
        private IFlagRegisterField pendingAlwaysOn;

        private IValueRegisterField channel;

        private IValueRegisterField fifopThreshold;

        private readonly IValueRegisterField[] pendingExceptionFlag = new IValueRegisterField[3];
        private readonly IValueRegisterField[] pendingExceptionMaskA = new IValueRegisterField[3];
        private readonly IValueRegisterField[] pendingExceptionMaskB = new IValueRegisterField[3];

        private bool isRxEnabled;
        private bool inReset;
        private bool vregEnabled;
        private bool oscillatorRunning;
        private bool wasLastFrameSent;

        private byte[] currentFrame;

        private readonly Queue<byte> txFifo = new Queue<byte>();
        private readonly Queue<byte> rxFifo = new Queue<byte>();
        private byte[] memory;
        private byte[] sourceAddressTable;
        private byte[] sourceAddressMatchingResult;
        private byte[] sourceAddressMatchingControl;
        private byte[] localAddressInfo;
        private Address localShortAddress;
        private Address localExtendedAddress;

        private Instruction currentInstruction;
        private ByteRegisterCollection registers;

        private SimpleInstructionDecoder<Instruction> decoderRoot = new SimpleInstructionDecoder<Instruction>();

        private const int RegisterMemorySize = 0x80;
        private const uint TxFifoMemoryStart = 0x100;
        private const int TxFifoMemorySize = 0x80;
        private const uint RxFifoMemoryStart = 0x180;
        private const int RxFifoMemorySize = 0x80;
        private const uint GeneralMemoryStart = 0x200;
        private const int GeneralMemorySize = 0x180;
        private const uint SourceAddressTableStart = 0x380;
        private const int SourceAddressTableSize = 0x60;
        private const uint SourceAddressMatchingResultStart = 0x3E0;
        private const int SourceAddressMatchingResultSize = 0x4;
        private const uint SourceAddressMatchingControlStart = 0x3E4;
        private const int SourceAddressMatchingControlSize = 0x6;
        private const uint LocalAddressInfoStart = 0x3EA;
        private const int LocalAddressInfoSize = 0xC;
        private const int NumberOfGPIOs = 6;

        private const int BroadcastPanIdentifier = 0xFFFF;
        private const byte NoSourceIndex = 0x3F;

        private abstract class Instruction
        {
            public byte Parse(byte value)
            {
                CurrentByteCount++;
                return ParseInner(value);
            }

            public CC2520 Parent { protected get; set; }
            public string Name { get; private set; }
            public bool IsFinished
            {
                get
                {
                    return CurrentByteCount == Length;
                }
            }

            public bool? HighPriority { get; protected set; } //tristate, because non-null value sets DPUx_ACTIVE in status

            protected Instruction()
            {
                Name = GetType().Name;
                Length = 1;
            }

            protected virtual byte ParseInner(byte value)
            {
                return Parent.GetStatus();
            }

            protected virtual bool IsCommandStrobe
            {
                get { return Length == 1; }
            }

            protected int Length;
            protected int CurrentByteCount;
            protected uint AddressA;
            protected uint CountC;
        }

        private sealed class SNOP : Instruction
        {
            protected override bool IsCommandStrobe
            {
                get { return false; }
            }
        }

        private sealed class SXOSCON : Instruction
        {
            protected override byte ParseInner(byte value)
            {
                Parent.oscillatorRunning = true;
                return base.ParseInner(value);
            }

            protected override bool IsCommandStrobe
            {
                get { return false; }
            }
        }

        private sealed class SRXON : Instruction
        {
            protected override byte ParseInner(byte value)
            {
                Parent.isRxEnabled = true;
                if(Parent.currentFrame != null)
                {
                    Parent.HandleFrame(Parent.currentFrame);
                    Parent.currentFrame = null;
                }
                return base.ParseInner(value);
            }
        }

        private sealed class STXONCCA : Instruction
        {
            protected override byte ParseInner(byte value)
            {
                Parent.SendFrame();
                return base.ParseInner(value);
            }
        }

        private sealed class SRFOFF : Instruction
        {
            protected override byte ParseInner(byte value)
            {
                if(Parent.isRxEnabled && Parent.currentFrame != null)
                {
                    Parent.SetException(ExceptionFlags.RxFrameAborted);
                    Parent.currentFrame = null;
                }
                Parent.isRxEnabled = false;
                return base.ParseInner(value);
            }
        }

        private sealed class SXOSCOFF : Instruction
        {
            protected override byte ParseInner(byte value)
            {
                if(Parent.isRxEnabled)
                {
                    Parent.SetException(ExceptionFlags.UsageError);
                    if(Parent.currentFrame != null)
                    {
                        Parent.SetException(ExceptionFlags.RxFrameAborted);
                        Parent.currentFrame = null;
                    }
                }
                Parent.oscillatorRunning = false;
                return base.ParseInner(value);
            }
        }

        private sealed class MEMRD : Instruction
        {
            public MEMRD()
            {
                Length = 0;
            }

            protected override byte ParseInner(byte value)
            {
                switch(CurrentByteCount)
                {
                case 1:
                    AddressA = (uint)(value & 0xF) << 8;
                    return Parent.GetStatus();
                case 2:
                    AddressA |= value;
                    return Parent.GetStatus();
                default:
                    var registerValue = Parent.ReadMemory(AddressA);
                    AddressA = (AddressA + 1) % 0x3FF; //0x3FF is the highest RAM address. It is not explicitly stated that MEMWR/MEMRD should wrap
                    return registerValue;
                }
            }
        }

        private sealed class MEMWR : Instruction
        {
            public MEMWR()
            {
                Length = 0;
            }

            protected override byte ParseInner(byte value)
            {
                switch(CurrentByteCount)
                {
                case 1:
                    AddressA = (uint)(value & 0xF) << 8;
                    return Parent.GetStatus();
                case 2:
                    AddressA |= value;
                    return Parent.GetStatus();
                default:
                    var registerValue = Parent.ReadMemory(AddressA);
                    Parent.WriteMemory(AddressA, value);
                    AddressA = (AddressA + 1) % 0x3FF; //0x3FF is the highest RAM address. It is not explicitly stated that MEMWR/MEMRD should wrap
                    return registerValue;
                }
            }
        }

        private sealed class RXBUF : Instruction
        {
            public RXBUF()
            {
                Length = 0;
            }

            protected override byte ParseInner(byte value)
            {
                switch(CurrentByteCount)
                {
                case 1:
                    return base.ParseInner(value);
                default:
                    if(Parent.rxFifo.Count > 0)
                    {
                        var data = Parent.rxFifo.Dequeue();
                        if(Parent.rxFifo.Count <= (int)Parent.fifopThreshold.Value)
                        {
                            Parent.UnsetException(ExceptionFlags.FifoThresholdReached);
                        }
                        Parent.UpdateInterrupts();
                        return data;
                    }
                    Parent.SetException(ExceptionFlags.RxUnderflow);
                    return 0;
                }
            }
        }

        private sealed class TXBUF : Instruction
        {
            public TXBUF()
            {
                Length = 0;
            }

            protected override byte ParseInner(byte value)
            {
                switch(CurrentByteCount)
                {
                case 1:
                    return base.ParseInner(value);
                default:
                    if(Parent.wasLastFrameSent)
                    {
                        Parent.txFifo.Clear();
                        Parent.wasLastFrameSent = false;
                    }
                    var count = Parent.txFifo.Count;
                    if(count <= TxFifoMemorySize)
                    {
                        Parent.txFifo.Enqueue(value);
                    }
                    else
                    {
                        Parent.SetException(ExceptionFlags.TxOverflow);
                    }
                    return (byte)count;
                }
            }
        }

        private sealed class SFLUSHRX : Instruction
        {
            protected override byte ParseInner(byte value)
            {
                Parent.rxFifo.Clear();
                Parent.UnsetException(ExceptionFlags.FifoThresholdReached);
                Parent.UpdateInterrupts();
                return base.ParseInner(value);
            }
        }

        private sealed class SFLUSHTX : Instruction
        {
            protected override byte ParseInner(byte value)
            {
                Parent.txFifo.Clear();
                Parent.UpdateInterrupts();
                return base.ParseInner(value);
            }
        }

        private sealed class REGRD : Instruction
        {
            public REGRD()
            {
                Length = 0;
            }

            protected override byte ParseInner(byte value)
            {
                if(CurrentByteCount == 1)
                {
                    AddressA = BitHelper.GetValue(value, 0, 6);
                    return base.ParseInner(value);
                }
                var registerValue = Parent.ReadMemory(AddressA);
                AddressA = (AddressA + 1) % 0x7F; //the operation wraps on 0x7F
                return registerValue;
            }
        }

        private sealed class REGWR : Instruction
        {
            public REGWR()
            {
                Length = 0;
            }

            protected override byte ParseInner(byte value)
            {
                if(CurrentByteCount == 1)
                {
                    AddressA = BitHelper.GetValue(value, 0, 6);
                    return Parent.GetStatus();
                }
                var registerValue = Parent.ReadMemory(AddressA);
                Parent.WriteMemory(AddressA, value);
                AddressA = (AddressA + 1) % 0x7F; //the operation wraps on 0x7F
                return registerValue;
            }
        }

        private sealed class RXBUFMOV : Instruction
        {
            public RXBUFMOV()
            {
                Length = 4;
            }

            protected override byte ParseInner(byte value)
            {
                switch(CurrentByteCount)
                {
                case 1:
                    HighPriority = (value & 0x1u) != 0;
                    return Parent.GetStatus();
                case 2:
                    CountC = value;
                    return (byte)Parent.rxFifo.Count;
                case 3:
                    AddressA = (uint)(value & 0xF) << 8;
                    return Parent.GetStatus();
                default:
                    AddressA |= value;
                    if(Parent.rxFifo.Count < CountC)
                    {
                        Parent.SetException(ExceptionFlags.RxBufferMoveTimeout);
                        CountC = (uint)Parent.rxFifo.Count;
                        Parent.Log(LogLevel.Warning, "Rx buffer underflow during RXBUFMOV instruction. A status register should be set, but it's not well specified which one.");
                    }
                    for(var i = 0; i < CountC; ++i)
                    {
                        var data = Parent.rxFifo.Dequeue();
                        Parent.WriteMemory(AddressA, data);
                        AddressA++;
                        Parent.SetException(HighPriority.Value ? ExceptionFlags.DPUDoneHigh : ExceptionFlags.DPUDoneLow);
                    }
                    if(Parent.rxFifo.Count <= (int)Parent.fifopThreshold.Value)
                    {
                        Parent.UnsetException(ExceptionFlags.FifoThresholdReached);
                    }
                    Parent.UpdateInterrupts();
                    return Parent.GetStatus();
                }
            }
        }



        private enum Registers
        {
            //FREG registers
            FrameFiltering0 = 0x00, //FRMFILT0
            FrameFiltering1 = 0x01,
            SourceMatching = 0x02,
            ShortAddressMatchingEnabled0 = 0x04,
            ShortAddressMatchingEnabled1 = 0x05,
            ShortAddressMatchingEnabled2 = 0x06,
            ExtendedAddressMatchingEnabled0 = 0x08,
            ExtendedAddressMatchingEnabled1 = 0x09,
            ExtendedAddressMatchingEnabled2 = 0x0A,
            FrameControl0 = 0x0C,
            FrameControl1 = 0x0D,
            RXENABLE0 = 0x0E,
            RXENABLE1 = 0x0F,
            PendingExceptionFlags0 = 0x10,
            PendingExceptionFlags1 = 0x11,
            PendingExceptionFlags2 = 0x12,
            ExceptionMaskA0 = 0x14,
            ExceptionMaskA1 = 0x15,
            ExceptionMaskA2 = 0x16,
            ExceptionMaskB0 = 0x18,
            ExceptionMaskB1 = 0x19,
            ExceptionMaskB2 = 0x1A,
            EXCBINDX0 = 0x1C,
            EXCBINDX1 = 0x1D,
            EXCBINDY0 = 0x1E,
            EXCBINDY1 = 0x1F,
            GPIOCTRL0 = 0x20,
            GPIOCTRL1 = 0x21,
            GPIOCTRL2 = 0x22,
            GPIOCTRL3 = 0x23,
            GPIOCTRL4 = 0x24,
            GPIOCTRL5 = 0x25,
            GPIOPOLARITY = 0x26,
            GPIOCTRL = 0x28,
            DPUCON = 0x2A,
            DPUSTAT = 0x2C,
            FrequencyControl = 0x2E,
            FREQTUNE = 0x2F,
            TxPower = 0x30,
            TXCTRL = 0x31,
            FSMSTAT0 = 0x32,
            FSMSTAT1 = 0x33,
            FifoPControl = 0x34,
            FSMCTRL = 0x35,
            CCAThreshold = 0x36,
            CCACTRL1 = 0x37,
            RSSI = 0x38,
            RSSIValidStatus = 0x39,
            RxFirstByte = 0x3C,
            RxFifoCount = 0x3E,
            TxFifoCount = 0x3F,
            //SREG registers
            ChipId = 0x40,
            Version = 0x42,
            ExternalClock = 0x44,
            ModemControl0 = 0x46,
            ModemControl1 = 0x47,
            FREQEST = 0x48,
            RxModuleTuning = 0x4A,
            SynthesizerTuning = 0x4C,
            FSCAL0 = 0x4E,
            VCOTuning1 = 0x4F,
            FSCAL2 = 0x50,
            FSCAL3 = 0x51,
            AGCCTRL0 = 0x52,
            AGCTuning1 = 0x53,
            AGCCTRL2 = 0x54,
            AGCCTRL3 = 0x55,
            ADCTest0 = 0x56,
            ADCTest1 = 0x57,
            ADCTest2 = 0x58,
            MDMTEST0 = 0x5A,
            MDMTEST1 = 0x5B,
            DACTEST0 = 0x5C,
            DACTEST1 = 0x5D,
            ATEST = 0x5E,
            DACTEST2 = 0x5F,
            PTEST0 = 0x60,
            PTEST1 = 0x61,
            RESERVED = 0x62,
            DPUBIST = 0x7A,
            ACTBIST = 0x7C,
            RAMBIST = 0x7E
        }

        private enum ExceptionFlags
        {
            RFIdle = 0,
            TxFrameDone = 1,
            TxAckDone = 2,
            TxUnderflow = 3,
            TxOverflow = 4,
            RxUnderflow = 5,
            RxOverflow = 7,
            RxEnableZero = 8,
            RxFrameDone = 9,
            RxFrameAccepted = 10,
            SourceMatchingDone = 11,
            SourceMatchingFound = 12,
            FifoThresholdReached = 13,
            StartOfFrameDelimiter = 14,
            DPUDoneLow = 15,
            DPUDoneHigh = 16,
            MemoryAddressError = 17,
            UsageError = 18,
            OperandError = 19,
            SPIError = 20,
            RFNoLock = 21,
            RxFrameAborted = 22,
            RxBufferMoveTimeout = 23
        }

        private enum GPIOs
        {
            //these are the default functions of reconfigurable gpios. This is not robust.
            Clock = 0,
            Fifo = 1,
            Fifop = 2,
            Cca = 3,
            Sfd = 4,
            In = 5
        }

        private enum ModifyFieldTypeMode
        {
            Leave,
            InvertMSB,
            SetMSB,
            UnsetMSB
        }
    }
}
