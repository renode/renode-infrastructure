//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Migrant;
using Antmicro.Renode.Peripherals.Wireless.IEEE802_15_4;
using Antmicro.Renode.Peripherals.Wireless.CC2538;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public class CC2538RF : IDoubleWordPeripheral, IBytePeripheral, IKnownSize, IRadio
    {
        public CC2538RF()
        {
            rxLock = new object();
            rxQueue = new Queue<Frame>();
            txQueue = new Queue<byte>();

            shortAddress = new Address(AddressingMode.ShortAddress);
            extendedAddress = new Address(AddressingMode.ExtendedAddress);
            random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            IRQ = new GPIO();

            srcShortEnabled = new bool[24];
            srcExtendedEnabled = new bool[12];
            matchedSourceAddresses = new bool[24];
            srcShortPendEnabled = new bool[24];
            srcExtendedPendEnabled = new bool[12];
            ffsmMemory = new uint[96];

            irqHandler = new InterruptHandler<InterruptRegister, InterruptSource>(IRQ);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag0, InterruptSource.StartOfFrameDelimiter, 1);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag0, InterruptSource.FifoP, 2);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag0, InterruptSource.SrcMatchDone, 3);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag0, InterruptSource.SrcMatchFound, 4);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag0, InterruptSource.FrameAccepted, 5);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag0, InterruptSource.RxPktDone, 6);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag0, InterruptSource.RxMaskZero, 7);

            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag1, InterruptSource.TxAckDone, 0);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag1, InterruptSource.TxDone, 1);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag1, InterruptSource.RfIdle, 2);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag1, InterruptSource.CommandStrobeProcessorManualInterrupt, 3);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag1, InterruptSource.CommandStrobeProcessorStop, 4);
            irqHandler.RegisterInterrupt(InterruptRegister.IrqFlag1, InterruptSource.CommandStrobeProcessorWait, 5);

            var matchedSourceIndex = new DoubleWordRegister(this);
            matchedSourceIndexField = matchedSourceIndex.DefineValueField(0, 8, FieldMode.Read | FieldMode.Write);

            var srcResMask = CreateRegistersGroup(3, this, 0, 8,
                                valueProviderCallback: ReadSrcResMaskRegister, writeCallback: WriteSrcResMaskRegister);
            var srcExtendedAddressPendingEnabled = CreateRegistersGroup(3, this, 0, 8,
                                valueProviderCallback: ReadSrcExtendedAddressPendingEnabledRegister, writeCallback: WriteSrcExtendedAddressPendingEnabledRegister);
            var srcShortAddressPendingEnabled = CreateRegistersGroup(3, this, 0, 8,
                                name: "SrcShortAddressPendingEnabled", valueProviderCallback: ReadSrcShortAddressPendingEnabledRegister, writeCallback: WriteSrcShortAddressPendingEnabledRegister);
            var extAddress = CreateRegistersGroup(8, this, 0, 8,
                                valueProviderCallback: i => extendedAddress.Bytes[i], writeCallback: (i, @new) => extendedAddress.SetByte((byte)@new, i));
            panId = CreateRegistersGroup(2, this, 0, 8);
            var shortAddressRegister = CreateRegistersGroup(2, this, 0, 8,
                                valueProviderCallback: i => shortAddress.Bytes[i], writeCallback: (i, @new) => shortAddress.SetByte((byte)@new, i));

            var sourceExtendedAddressEnable = CreateRegistersGroup(3, this, 0, 8,
                                valueProviderCallback: ReadSourceExtendedAddressEnableRegister, writeCallback: WriteSourceExtendedAddressEnableRegister);
            var sourceShortAddressEnable = CreateRegistersGroup(3, this, 0, 8,
                                valueProviderCallback: ReadSourceShortAddressEnableRegister, writeCallback: WriteSourceShortAddressEnableRegister);

            var frameHandling0 = new DoubleWordRegister(this, 0x40);
            autoAck = frameHandling0.DefineFlagField(5);
            autoCrc = frameHandling0.DefineFlagField(6);
            appendDataMode = frameHandling0.DefineFlagField(7);

            var frameHandling1 = new DoubleWordRegister(this, 0x1);
            pendingOr = frameHandling1.DefineFlagField(2);

            var sourceAddressMatching = new DoubleWordRegister(this, 0x7);
            sourceAddressMatchingEnabled = sourceAddressMatching.DefineFlagField(0);
            autoPendEnabled = sourceAddressMatching.DefineFlagField(1);
            pendDataRequestOnly = sourceAddressMatching.DefineFlagField(2);

            var radioStatus0 = new DoubleWordRegister(this, 0).WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ => (uint)fsmState)
                                                              .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => true);
            var radioStatus1 = new DoubleWordRegister(this, 0).WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => ReadRadioStatus1Register());
            var rssiValidStatus = new DoubleWordRegister(this, 0x1).WithFlag(0, FieldMode.Read);

            var interruptMask = CreateRegistersGroup(2, this, 0, 8,
                                 valueProviderCallback: i => irqHandler.GetRegisterMask(InterruptRegisterHelper.GetMaskRegister(i)),
                                 writeCallback: (i, @new) => { irqHandler.SetRegisterMask(InterruptRegisterHelper.GetMaskRegister(i), (uint)@new); });
            var randomData = new DoubleWordRegister(this, 0).WithValueField(0, 2, FieldMode.Read, valueProviderCallback: _ => (uint)(random.Next() & 3));

            var frameFiltering0 = new DoubleWordRegister(this, 0xD);
            frameFilterEnabled = frameFiltering0.DefineFlagField(0);
            isPanCoordinator = frameFiltering0.DefineFlagField(1);
            maxFrameVersion = frameFiltering0.DefineValueField(2, 2);

            var frameFiltering1 = new DoubleWordRegister(this, 0x78);
            acceptBeaconFrames = frameFiltering1.DefineFlagField(3);
            acceptDataFrames = frameFiltering1.DefineFlagField(4);
            acceptAckFrames = frameFiltering1.DefineFlagField(5);
            acceptMacCmdFrames = frameFiltering1.DefineFlagField(6);
            //Reset value set according to the documentation, but register value is caluculated from Channel value.
            var frequencyControl = new DoubleWordRegister(this, 0xB).WithValueField(0, 7,
                                changeCallback: (_, value) => Channel = (int)(((value > 113 ? 113 : value) - 11) / 5 + 11), //FREQ = 11 + 5(channel - 11), maximum value is 113.
                                valueProviderCallback: (value) => 11 + 5 * ((uint)Channel - 11));

            var rfData = new DoubleWordRegister(this, 0).WithValueField(0, 8,
                                valueProviderCallback: _ => DequeueData(), writeCallback: (_, @new) => { EnqueueData((byte)@new); });
            var interruptFlag = CreateRegistersGroup(2, this, 0, 8,
                                valueProviderCallback: i => irqHandler.GetRegisterValue(InterruptRegisterHelper.GetValueRegister(i)),
                                writeCallback: (i, @new) => { irqHandler.SetRegisterValue(InterruptRegisterHelper.GetValueRegister(i), (uint)@new); });
            var commandStrobeProcessor = new DoubleWordRegister(this, 0).WithValueField(0, 8, FieldMode.Write, writeCallback: (_, @new) => { HandleSFRInstruction((uint)@new); });

            var rxFifoBytesCount = new DoubleWordRegister(this).WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => GetRxFifoBytesCount());

            var addresses = new Dictionary<long, DoubleWordRegister>
            {
                { (uint)Register.RfData, rfData },
                { (uint)Register.CommandStrobeProcessor, commandStrobeProcessor },
                { (uint)Register.FrameFiltering0, frameFiltering0 },
                { (uint)Register.FrameFiltering1, frameFiltering1 },
                { (uint)Register.SourceAddressMatching, sourceAddressMatching },
                { (uint)Register.FrameHandling0, frameHandling0 },
                { (uint)Register.FrameHandling1, frameHandling1 },
                { (uint)Register.RadioStatus0, radioStatus0 },
                { (uint)Register.RadioStatus1, radioStatus1 },
                { (uint)Register.RssiValidStatus, rssiValidStatus },
                { (uint)Register.RandomData, randomData },
                { (uint)Register.SourceAddressMatchingResult, matchedSourceIndex },
                { (uint)Register.FrequencyControl, frequencyControl },
                { (uint)Register.RxFifoBytesCount, rxFifoBytesCount }
            };

            RegisterGroup(addresses, (uint)Register.InterruptFlag, interruptFlag);
            RegisterGroup(addresses, (uint)Register.SourceExtendedAdressEnable, sourceExtendedAddressEnable);
            RegisterGroup(addresses, (uint)Register.SourceShortAddressEnable, sourceShortAddressEnable);
            RegisterGroup(addresses, (uint)Register.InterruptMask, interruptMask);
            RegisterGroup(addresses, (uint)Register.SourceAddressMatchingResultMask, srcResMask);
            RegisterGroup(addresses, (uint)Register.SourceExtendedAddressPendingEnabled, srcExtendedAddressPendingEnabled);
            RegisterGroup(addresses, (uint)Register.SourceShortAddressPendingEnabled, srcShortAddressPendingEnabled);
            RegisterGroup(addresses, (uint)Register.ExtendedAddress, extAddress);
            RegisterGroup(addresses, (uint)Register.PanId, panId);
            RegisterGroup(addresses, (uint)Register.ShortAddressRegister, shortAddressRegister);

            registers = new DoubleWordRegisterCollection(this, addresses);

            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            uint result = 0u;
            if(offset >= FfsmMemoryStart && offset <= FfsmMemoryEnd)
            {
                result = ffsmMemory[offset - FfsmMemoryStart];
            }
            else if(offset < RxFifoMemorySize)
            {
                lock(rxLock)
                {
                    // dividedOffset represents the position in the packet queue, assuming that the "Length" byte is also stored there.
                    // Since we do not store the lenght along with the packet, we handle it with increasedFrameIndex variable.
                    // The offset is shifted, as it seems that the driver accesses each byte as it was a double word value.
                    var dividedOffset = offset >> 2;
                    var increasedFrameIndex = 1;
                    do
                    {
                        if(rxQueue.Count < increasedFrameIndex)
                        {
                            break;
                        }

                        if(dividedOffset == increasedFrameIndex - 1)
                        {
                            //return size of the current frame
                            result = (uint)rxQueue.ElementAt(increasedFrameIndex - 1).Bytes.Count();
                            break;
                        }
                        if(increasedFrameIndex + rxQueue.ElementAt(increasedFrameIndex - 1).Bytes.Count() > dividedOffset)
                        {
                            //return specific byte from the current frame
                            result = rxQueue.ElementAt(increasedFrameIndex - 1).Bytes[dividedOffset - increasedFrameIndex];
                            break;
                        }

                        dividedOffset -= rxQueue.ElementAt(increasedFrameIndex - 1).Bytes.Count();
                        increasedFrameIndex++;
                    } while(dividedOffset > 0);
                }
            }
            else
            {
                result = registers.Read(offset);
            }

            return result;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset >= 0x400 && offset <= 0x57C)
            {
                ffsmMemory[offset - 0x400] = value;
                return;
            }

            registers.Write(offset, value);
            return;
        }

        //used by uDMA
        public byte ReadByte(long offset)
        {
            if(offset == (long)Register.RfData)
            {
                return DequeueData();
            }
            this.Log(LogLevel.Warning, "{0} does not implement byte reads apart from {1} (0x{2:X}).", GetType().Name, "RFData", (long)Register.RfData);
            this.LogUnhandledRead(offset);
            return 0;
        }

        //used by uDMA
        public void WriteByte(long offset, byte value)
        {
            if(offset == (long)Register.RfData) // RF data
            {
                EnqueueData(value);
                return;
            }
            this.Log(LogLevel.Warning, "{0} does not implement byte writes apart from {1} (0x{2:X}).", GetType().Name, "RFData", (long)Register.RfData);
            this.LogUnhandledWrite(offset, value);
        }

        public void Reset()
        {
            lock(rxLock)
            {
                registers.Reset();

                currentFrameOffset = -1;
                txPendingCounter = 0;
                fsmState = FSMStates.Idle;
                rxQueue.Clear();

                Array.Clear(srcShortEnabled, 0, srcShortEnabled.Length);
                Array.Clear(srcShortPendEnabled, 0, srcShortPendEnabled.Length);
                Array.Clear(matchedSourceAddresses, 0, matchedSourceAddresses.Length);
                Array.Clear(srcExtendedEnabled, 0, srcExtendedEnabled.Length);
                Array.Clear(srcExtendedPendEnabled, 0, srcExtendedPendEnabled.Length);
                Array.Clear(ffsmMemory, 0, ffsmMemory.Length);

                irqHandler.Reset();
                txQueue.Clear();

                Channel = ChannelResetValue;
            }
        }

        public void ReceiveFrame(byte[] bytes)
        {
            irqHandler.RequestInterrupt(InterruptSource.StartOfFrameDelimiter);

            Frame ackFrame = null;
            var frame = new Frame(bytes);

            var crcOK = frame.CheckCRC();
            if(autoCrc.Value && !crcOK)
            {
                this.Log(LogLevel.Warning, "Received frame with wrong CRC");
            }

            if(frameFilterEnabled.Value)
            {
                if(!ShouldWeAcceptThisFrame(frame))
                {
                    this.DebugLog("Not accepting a frame");
                    return;
                }

                irqHandler.RequestInterrupt(InterruptSource.FrameAccepted);
            }

            lock(rxLock)
            {
                var autoPendingBit = false;
                var index = NoSourceIndex;
                if(sourceAddressMatchingEnabled.Value && crcOK) //todo: verify if this should not only run when frameFilterEnabled, as in CC2520
                {
                    switch(frame.SourceAddressingMode)
                    {
                        case AddressingMode.ShortAddress:
                            for(var i = 0u; i < srcShortEnabled.Length; i++)
                            {
                                if(!srcShortEnabled[i])
                                {
                                    continue;
                                }
                                if(frame.AddressInformation.SourcePan == GetShortPanIdFromRamTable(i)
                                   && frame.AddressInformation.SourceAddress.GetValue() == GetShortSourceAddressFromRamTable(i))
                                {
                                    matchedSourceAddresses[i] = true;
                                    autoPendingBit |= srcShortPendEnabled[i];
                                    if(index == NoSourceIndex)
                                    {
                                        index = i;
                                    }
                                }
                            }
                            break;
                        case AddressingMode.ExtendedAddress:
                            for(var i = 0u; i < srcExtendedEnabled.Length; i++)
                            {
                                if(!srcExtendedEnabled[i])
                                {
                                    continue;
                                }
                                if(frame.AddressInformation.SourceAddress.GetValue() == GetExtendedSourceAddressFromRamTable(i))
                                {
                                    matchedSourceAddresses[2 * i] = true;
                                    matchedSourceAddresses[2 * i + 1] = true;
                                    autoPendingBit |= srcExtendedPendEnabled[i];
                                    if(index == NoSourceIndex)
                                    {
                                        index = i | 0x20;
                                    }
                                }
                            }
                            break;
                    }

                    matchedSourceIndexField.Value = index;

                    autoPendingBit &= autoPendEnabled.Value
                                        && frameFilterEnabled.Value
                                        && (!pendDataRequestOnly.Value
                                            || (frame.Type == FrameType.MACControl
                                                && frame.Payload.Count > 0
                                                && frame.Payload[0] == 0x4));

                    BitHelper.SetBit(ref index, 6, autoPendingBit);
                    if(index != NoSourceIndex)
                    {
                        irqHandler.RequestInterrupt(InterruptSource.SrcMatchFound);
                    }
                    irqHandler.RequestInterrupt(InterruptSource.SrcMatchDone);
                }

                if(autoCrc.Value)
                {
                    var rssi = 70; // why 70?
                    var secondByte = crcOK ? (1u << 7) : 0;
                    if(appendDataMode.Value)
                    {
                        secondByte |= index & 0x7F;
                    }
                    else
                    {
                        secondByte |= 100; // correlation value 100 means near maximum quality
                    }
                    frame.Bytes[frame.Bytes.Length - 2] = (byte)rssi;
                    frame.Bytes[frame.Bytes.Length - 1] = (byte)secondByte;
                }

                rxQueue.Enqueue(frame);
                irqHandler.RequestInterrupt(InterruptSource.FifoP);
                if(crcOK && autoAck.Value
                    && frame.AcknowledgeRequest
                    && frame.Type != FrameType.Beacon
                    && frame.Type != FrameType.ACK)
                {
                    ackFrame = Frame.CreateACK(frame.DataSequenceNumber, pendingOr.Value | autoPendingBit);
                }
            }

            var frameSent = FrameSent;
            if(frameSent != null && ackFrame != null)
            {
                frameSent(this, ackFrame.Bytes);
                irqHandler.RequestInterrupt(InterruptSource.TxAckDone);
            }

            irqHandler.RequestInterrupt(InterruptSource.RxPktDone);
        }

        public int Channel { get; set; }
        public event Action<IRadio, byte[]> FrameSent;
        public GPIO IRQ { get; private set; }
        public long Size { get { return 0x1000; } }
        
	private uint GetRxFifoBytesCount()
        {
            lock(rxLock)
            {
                //takes only first packet into account plus 1 byte that indicates its size
                return rxQueue.Count > 0 ? (uint)rxQueue.Peek().Bytes.Length + 1 : 0u;
	    }
	}
        
	private static DoubleWordRegister[] CreateRegistersGroup(int size, IPeripheral parent, int position, int width,
            FieldMode mode = FieldMode.Read | FieldMode.Write, Action<int, uint> writeCallback = null, Func<int, uint> valueProviderCallback = null, string name = null)
        {
            var result = new DoubleWordRegister[size];
            for(var i = 0; i < size; i++)
            {
                var j = i;
                result[i] = new DoubleWordRegister(parent)
                    .WithValueField(position, width, mode, name: name + j,
                        valueProviderCallback: _ =>
                        {
                            if(valueProviderCallback != null)
                            {
                                return valueProviderCallback(j);
                            }
                            return 0;
                        },
                        writeCallback: (_, @new) =>
                        {
                            if(writeCallback != null)
                            {
                                writeCallback(j, (uint)@new);
                            }
                        });
            }
            return result;
        }

        private static void RegisterGroup(Dictionary<long, DoubleWordRegister> collection, long initialAddress, DoubleWordRegister[] group)
        {
            for(var i = 0; i < group.Length; i++)
            {
                collection.Add(initialAddress + 0x4 * i, group[i]);
            }
        }

        private uint ReadRadioStatus1Register()
        {
            if(txPendingCounter > 0)
            {
                txPendingCounter--;
            }

            lock(rxLock)
            {
                return (rxQueue.Count == 0 ? 0u : 3 << 6) // FIFO, FIFOP
                    | 1u << 4 // clear channel assessment
                    | (txPendingCounter > 0 ? 2u : 0);
            }
        }

        private void WriteSourceShortAddressEnableRegister(int index, uint value)
        {
            for(byte i = 0; i < 8; i++)
            {
                srcShortEnabled[i + 8 * index] = BitHelper.IsBitSet(value, i);
            }
        }

        private uint ReadSourceShortAddressEnableRegister(int index)
        {
            var result = 0u;
            for(byte i = 0; i < 8; i++)
            {
                if(srcShortEnabled[i + 8 * index])
                {
                    BitHelper.SetBit(ref result, i, true);
                }
            }
            return result;
        }

        private void WriteSourceExtendedAddressEnableRegister(int index, uint value)
        {
            for(var i = 0; i < 4; i++)
            {
                srcExtendedEnabled[i + 4 * index] = BitHelper.IsBitSet(value, (byte)(2 * i));
            }
        }

        private uint ReadSourceExtendedAddressEnableRegister(int index)
        {
            var result = 0u;
            for(var i = 0; i < 4; i++)
            {
                if(srcExtendedEnabled[i + 4 * index])
                {
                    BitHelper.SetBit(ref result, (byte)(2 * i), true);
                }
            }
            return result;
        }

        private uint ReadSrcResMaskRegister(int id)
        {
            var result = 0u;
            for(byte i = 0; i < 8; i++)
            {
                BitHelper.SetBit(ref result, i, matchedSourceAddresses[i + 8 * id]);
            }
            return result;
        }

        private void WriteSrcResMaskRegister(int index, uint value)
        {
            for(byte i = 0; i < 8; i++)
            {
                matchedSourceAddresses[i + 8 * index] = BitHelper.IsBitSet(value, i);
            }
        }

        private uint ReadSrcExtendedAddressPendingEnabledRegister(int index)
        {
            var result = 0u;
            for(var i = 0; i < 4; i++)
            {
                if(srcExtendedPendEnabled[i + 4 * index])
                {
                    BitHelper.SetBit(ref result, (byte)(2 * i), true);
                }
            }
            return result;
        }

        private void WriteSrcExtendedAddressPendingEnabledRegister(int index, uint value)
        {
            for(var i = 0; i < 4; i++)
            {
                srcExtendedPendEnabled[i + 4 * index] = BitHelper.IsBitSet(value, (byte)(2 * i));
            }
        }

        private uint ReadSrcShortAddressPendingEnabledRegister(int index)
        {
            var result = 0u;
            for(byte i = 0; i < 8; i++)
            {
                BitHelper.SetBit(ref result, i, srcShortPendEnabled[i + 8 * index]);
            }
            return result;
        }

        private void WriteSrcShortAddressPendingEnabledRegister(int index, uint value)
        {
            for(byte i = 0; i < 8; i++)
            {
                srcShortPendEnabled[i + 8 * index] = BitHelper.IsBitSet(value, i);
            }
        }

        private void HandleSFRInstruction(uint value)
        {
            switch((CSPInstructions)value)
            {
                case CSPInstructions.TxOn:
                    fsmState = FSMStates.Tx;
                    txPendingCounter = TxPendingCounterInitialValue;
                    SendData();
                    break;
                case CSPInstructions.RxOn:
                    fsmState = FSMStates.Rx;
                    break;
                case CSPInstructions.RfOff:
                    fsmState = FSMStates.Idle;
                    break;
                case CSPInstructions.RxFifoFlush:
                    lock(rxLock)
                    {
                        if(rxQueue.Count != 0)
                        {
                            this.Log(LogLevel.Warning, "Dropping unreceived frame.");
                            currentFrameOffset = -1;
                            rxQueue.Clear();
                        }
                    }
                    break;
                case CSPInstructions.TxFifoFlush:
                    txQueue.Clear();
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unsupported CSP instruction {0}.", value);
                    break;
            }
        }

        private byte DequeueData()
        {
            lock(rxLock)
            {
                if(rxQueue.Count == 0)
                {
                    this.Log(LogLevel.Warning, "Trying to dequeue data from empty RX FIFO.");
                    return 0x0;
                }

                var currentFrame = rxQueue.Peek();
                if(currentFrameOffset == -1)
                {
                    // we need to send packet length first
                    currentFrameOffset++;
                    return currentFrame.Length;
                }
                var result = currentFrame.Bytes[currentFrameOffset++];
                if(currentFrameOffset == currentFrame.Bytes.Length)
                {
                    rxQueue.Dequeue();
                    currentFrameOffset = -1;

                    if(rxQueue.Count > 0)
                    {
                        irqHandler.RequestInterrupt(InterruptSource.FifoP);
                    }
                }

                return result;
            }
        }

        private void EnqueueData(byte value)
        {
            this.NoisyLog("Enqueuing data: 0x{0:X}.", value);
            txQueue.Enqueue((byte)(value & 0xFF));
        }

        private void SendData()
        {
            if(txQueue.Count == 0)
            {
                this.Log(LogLevel.Warning, "Attempted to transmit an empty frame.");
                return;
            }

            irqHandler.RequestInterrupt(InterruptSource.StartOfFrameDelimiter);

            //ignore the first byte, it's length. Don't drop it though, as the same packet might get resent.
            var crc = Frame.CalculateCRC(txQueue.Skip(1));
            var frame = new Frame(txQueue.Skip(1).Concat(crc).ToArray());

            this.DebugLog("Sending frame {0}.", frame.Bytes.Select(x => "0x{0:X}".FormatWith(x)).Stringify());
            var fs = FrameSent;
            if(fs != null)
            {
                fs.Invoke(this, frame.Bytes);
            }
            else
            {
                this.Log(LogLevel.Warning, "FrameSent is not initialized. Am I connected to medium?");
            }

            irqHandler.RequestInterrupt(InterruptSource.TxDone);
        }

        private bool ShouldWeAcceptThisFrame(Frame frame)
        {
            // (1) check if length is ok
            // (2) check reserved FCF bits
            // for now we assume it is fine - let's be optimistic. Note - it is implemented in CC2520.

            // (3) check FCF version
            if(frame.FrameVersion > maxFrameVersion.Value)
            {
                this.Log(LogLevel.Noisy, "Wrong frame version.");
                return false;
            }

            // (4) check source/destination addressing mode
            if(frame.SourceAddressingMode == AddressingMode.Reserved || frame.DestinationAddressingMode == AddressingMode.Reserved)
            {
                this.Log(LogLevel.Noisy, "Wrong addressing mode.");
                return false;
            }

            // (5) check destination address
            if(frame.DestinationAddressingMode != AddressingMode.None)
            {
                // (5.1) check destination PAN
                if(frame.AddressInformation.DestinationPan != BroadcastPanIdentifier && frame.AddressInformation.DestinationPan != GetPanId())
                {
                    this.Log(LogLevel.Noisy, "Wrong destination PAN.");
                    return false;
                }
                // (5.2) check destination short address
                if(frame.DestinationAddressingMode == AddressingMode.ShortAddress)
                {
                    if(!frame.AddressInformation.DestinationAddress.IsShortBroadcast && !frame.AddressInformation.DestinationAddress.Equals(shortAddress))
                    {
                        this.Log(LogLevel.Noisy, "Wrong destination short address.");
                        return false;
                    }
                }
                // (5.3) check destination extended address
                else if(frame.DestinationAddressingMode == AddressingMode.ExtendedAddress)
                {
                    if(!frame.AddressInformation.DestinationAddress.Equals(extendedAddress))
                    {
                        this.Log(LogLevel.Noisy, "Wrong destination extended address (i'm {0}, but the message is directed to {1}.", extendedAddress.GetValue(), frame.AddressInformation.DestinationAddress.GetValue());
                        return false;
                    }
                }
            }

            // (6) check frame type
            //todo: not implemented reserved types (implemented in cc2520)
            switch(frame.Type)
            {
                case FrameType.Beacon:
                    if(!acceptBeaconFrames.Value
                        || frame.Length < 9
                        || frame.DestinationAddressingMode != AddressingMode.None
                        || (frame.SourceAddressingMode != AddressingMode.ShortAddress && frame.SourceAddressingMode != AddressingMode.ExtendedAddress)
                        || (frame.AddressInformation.SourcePan != BroadcastPanIdentifier && frame.AddressInformation.SourcePan != GetPanId()))
                    {
                        this.Log(LogLevel.Noisy, "Wrong beacon frame.");
                        return false;
                    }
                    break;
                case FrameType.Data:
                    if(!acceptDataFrames.Value
                        || frame.Length < 9
                        || (frame.DestinationAddressingMode == AddressingMode.None
                            && (!isPanCoordinator.Value || frame.AddressInformation.SourcePan != GetPanId())))
                    {
                        this.Log(LogLevel.Noisy, "Wrong data frame.");
                        return false;
                    }
                    break;
                case FrameType.ACK:
                    if(!acceptAckFrames.Value || frame.Length != 5)
                    {
                        this.Log(LogLevel.Noisy, "Wrong ACK frame.");
                        return false;
                    }
                    break;
                case FrameType.MACControl:
                    if(!acceptMacCmdFrames.Value
                        || frame.Length < 9
                        || (frame.DestinationAddressingMode == AddressingMode.None
                            && (!isPanCoordinator.Value || frame.AddressInformation.SourcePan != GetPanId())))
                    {
                        this.Log(LogLevel.Noisy, "Wrong MAC control frame.");
                        return false;
                    }
                    break;
                default:
                    return false;
            }

            return true;
        }

        private uint GetPanId()
        {
            return (panId[1].Value << 8) | panId[0].Value;
        }

        private ushort GetShortPanIdFromRamTable(uint id)
        {
            return (ushort)((ffsmMemory[16 * id] << 8) | (ffsmMemory[16 * id + 1]));
        }

        private ushort GetShortSourceAddressFromRamTable(uint id)
        {
            return (ushort)((ffsmMemory[16 * id + 2] << 8) | (ffsmMemory[16 * id + 3]));
        }

        private ulong GetExtendedSourceAddressFromRamTable(uint id)
        {
            return ((ffsmMemory[32 * id] << 7 * 8) | (ffsmMemory[32 * id + 1] << 6 * 8)
                           | (ffsmMemory[32 * id + 2] << 5 * 8) | (ffsmMemory[32 * id + 3] << 4 * 8)
                           | (ffsmMemory[32 * id + 4] << 3 * 8) | (ffsmMemory[32 * id + 5] << 2 * 8)
                           | (ffsmMemory[32 * id + 6] << 8) | ffsmMemory[32 * id + 7]);
        }

        private int txPendingCounter;
        private int currentFrameOffset;
        private FSMStates fsmState;

        private readonly DoubleWordRegisterCollection registers;
        private readonly IFlagRegisterField autoAck;
        private readonly IFlagRegisterField autoCrc;
        private readonly IFlagRegisterField frameFilterEnabled;
        private readonly IFlagRegisterField sourceAddressMatchingEnabled;
        private readonly IFlagRegisterField autoPendEnabled;
        private readonly IFlagRegisterField pendDataRequestOnly;
        private readonly IFlagRegisterField appendDataMode;
        private readonly IFlagRegisterField pendingOr;
        private readonly IValueRegisterField matchedSourceIndexField;
        private readonly IFlagRegisterField acceptBeaconFrames;
        private readonly IFlagRegisterField acceptAckFrames;
        private readonly IFlagRegisterField acceptDataFrames;
        private readonly IFlagRegisterField acceptMacCmdFrames;
        private readonly IFlagRegisterField isPanCoordinator;
        private readonly IValueRegisterField maxFrameVersion;
        private readonly DoubleWordRegister[] panId;
        private readonly bool[] srcShortEnabled;
        private readonly bool[] srcShortPendEnabled;
        private readonly bool[] srcExtendedEnabled;
        private readonly bool[] srcExtendedPendEnabled;
        private readonly bool[] matchedSourceAddresses;
        private readonly uint[] ffsmMemory;
        private readonly object rxLock;
        private readonly InterruptHandler<InterruptRegister, InterruptSource> irqHandler;
        private readonly Address shortAddress;
        private readonly Address extendedAddress;
        private readonly Queue<Frame> rxQueue;
        private readonly Queue<byte> txQueue;
        [Constructor]
        private readonly PseudorandomNumberGenerator random;

        private const uint NoSourceIndex = 0x3F;
        private const int BroadcastPanIdentifier = 0xFFFF;
        private const int RamTableBaseAddress = 0x40088400;
        //HACK! TX_ACTIVE is required to be set as 1 few times in a row for contiki
        private const int TxPendingCounterInitialValue = 4;
        private const int ChannelResetValue = 11;
        private const uint FfsmMemoryStart = 0x400;
        private const uint FfsmMemoryEnd = 0x57C;
        private const uint RxFifoMemorySize = 0x200;

        private enum CSPInstructions
        {
            RfOff = 0xDF,
            RxOn = 0xE3,
            TxOn = 0xE9,
            RxFifoFlush = 0xED,
            TxFifoFlush = 0xEE
        }

        private enum FSMStates
        {
            Idle = 0x00,
            Rx = 0x07,
            Tx = 0x22
        }

        private enum Register
        {
            //not groupped
            SourceAddressMatchingResult = 0x58C,
            FrameFiltering0 = 0x600,
            FrameFiltering1 = 0x604,
            SourceAddressMatching = 0x608,
            FrameHandling0 = 0x624,
            FrameHandling1 = 0x628,
            FrequencyControl = 0x63C,
            RadioStatus0 = 0x648,
            RadioStatus1 = 0x64C,
            RssiValidStatus = 0x664,
            RandomData = 0x69C,
            RfData = 0x828,
            CommandStrobeProcessor = 0x838,
            RxFifoBytesCount = 0x66C,

            //register groups
            SourceAddressMatchingResultMask = 0x580,
            SourceExtendedAddressPendingEnabled = 0x590,
            SourceShortAddressPendingEnabled = 0x59C,
            ExtendedAddress = 0x5A8,
            PanId = 0x5C8,
            ShortAddressRegister = 0x5D0,
            SourceShortAddressEnable = 0x60C,
            SourceExtendedAdressEnable = 0x618,
            InterruptMask = 0x68C,
            InterruptFlag = 0x830,
        }
    }
}

