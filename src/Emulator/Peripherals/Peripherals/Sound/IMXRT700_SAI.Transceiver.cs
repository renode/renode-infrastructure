//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Sound;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sound
{
    public partial class IMXRT700_SAI
    {
        private class Transceiver : IDisposable
        {
            public Transceiver(IMachine machine, IMXRT700_SAI parent, GPIO irq, GPIO dmaTrigger, bool isTx)
            {
                this.machine = machine;
                this.parent = parent;
                this.irq = irq;
                this.dmaTrigger = dmaTrigger;
                this.isTx = isTx;
                tr = isTx ? "T" : "R"; // T/R
                id = tr + "X"; // TX/RX
                registers = BuildRegisterMap();
            }

            public void SetPcmFile(string pcmFile, bool littleEndianFileFormat)
            {
                this.pcmFile = pcmFile;
                this.littleEndianFileFormat = littleEndianFileFormat;
            }

            public void Reset()
            {
                ResetFifo();
                // Configuration registers are not affected, except for status fields.
                requestFlag.Value = false;
                warningFlag.Value = false;
                errorFlag.Value = false;
                syncErrorFlag.Value = false;
                wordStartFlag.Value = false;
                irq.Unset();
            }

            public void Dispose()
            {
                encoder?.Dispose();
            }

            public ReadOnlyDictionary<long, DoubleWordRegister> GetRegisterMap()
            {
                return registers;
            }

            public uint ReadBits(int bitWidth)
            {
                if(FifoOccupancy == 0)
                {
                    parent.DebugLog("{0}: FIFO is empty, ignoring {1}-bit read", id, bitWidth);
                    return 0;
                }

                var dataWordSizeInBits = GetFifoPackedDataSizeInBits();
                if(currentFifoReadWordBitOffset == 0)
                {
                    currentFifoReadWord = queue.Dequeue();
                }
                var value = BitHelper.GetValue(currentFifoReadWord, currentFifoReadWordBitOffset, dataWordSizeInBits);
                currentFifoReadWordBitOffset += dataWordSizeInBits;
                if(currentFifoReadWordBitOffset >= FifoWordLengthInBits)
                {
                    readPointer.Value++;
                    currentFifoReadWordBitOffset = 0;
                }

                readPointer.Value = readPointer.Value % FifoCapacity;

                if(FifoOccupancy < FifoCapacity)
                {
                    warningFlag.Value = false;
                    if(continueOnError.Value)
                    {
                        errorFlag.Value = false;
                    }
                }

                UpdateInterrupts();

                return value;
            }

            public void WriteBits(int bitWidth, uint value)
            {
                if(FifoOccupancy == FifoCapacity)
                {
                    parent.DebugLog("{0}: FIFO is full, ignoring {1}-bit write of {2:X}", id, bitWidth, value);
                    return;
                }

                var dataWordSizeInBits = GetFifoPackedDataSizeInBits();
                BitHelper.SetMaskedValue(ref currentFifoWriteWord, value, currentFifoWriteWordBitOffset, dataWordSizeInBits);
                currentFifoWriteWordBitOffset += dataWordSizeInBits;
                if(currentFifoWriteWordBitOffset >= FifoWordLengthInBits)
                {
                    queue.Enqueue(currentFifoWriteWord);
                    writePointer.Value++;
                    currentFifoWriteWordBitOffset = 0;
                }

                writePointer.Value = writePointer.Value % FifoCapacity;

                if(FifoOccupancy > 0)
                {
                    warningFlag.Value = false;
                    if(continueOnError.Value)
                    {
                        errorFlag.Value = false;
                    }
                }

                UpdateInterrupts();
            }

            public override string ToString()
            {
                return ""
                    + $"{tr}FW={watermark.Value},"
                    + $"MF={msbFirst.Value},"
                    + $"FRSZ={frameSize.Value},"
                    + $"FPACK={packingMode.Value},"
                    + $"FBT={firstBitShifted.Value},"
                    + $"W0W={word0Width.Value},"
                    + $"WNW={wordNWidth.Value},"
                    + $"RFP={readPointer.Value},"
                    + $"WFP={writePointer.Value},"
                    + $"{tr}WM={wordMask.Value}";
            }

            private bool TryDequeueFrame(out uint[] frame)
            {
                frame = new uint[NumberOfChannels];
                var dataWordSizeInBits = GetFifoPackedDataSizeInBits();
                var sampleSize = Word0BitDepth;
                for(byte i = 0; i < frame.Length; i++)
                {
                    var isMasked = BitHelper.IsBitSet((uint)wordMask.Value, i);
                    if(isMasked)
                    {
                        // The transmit FIFO is not read for masked words.
                        // The transmit data pin is in the high-impedance state for the length of the masked word.
                        frame[i] = 0;
                        continue;
                    }
                    if(currentFifoReadWordBitOffset == 0)
                    {
                        if(FifoOccupancy == 0)
                        {
                            // underrun error
                            errorFlag.Value = true;
                            return false;
                        }
                        currentFifoReadWord = queue.Dequeue();
                    }

                    var firstBitOffset = 0;

                    if(msbFirst.Value)
                    {
                        firstBitOffset = (int)(firstBitShifted.Value - (sampleSize - 1));
                    }
                    else
                    {
                        firstBitOffset = (int)firstBitShifted.Value;
                    }

                    var value = BitHelper.GetValue(currentFifoReadWord, currentFifoReadWordBitOffset + firstBitOffset, (int)sampleSize);
                    currentFifoReadWordBitOffset += dataWordSizeInBits;
                    if(currentFifoReadWordBitOffset >= FifoWordLengthInBits)
                    {
                        readPointer.Value++;
                        currentFifoReadWordBitOffset = 0;
                    }

                    frame[i] = value;
                    readPointer.Value = readPointer.Value % FifoCapacity;
                    sampleSize = WordNBitDepth;
                }

                return true;
            }

            private bool TryEnqueueFrame(uint[] frame)
            {
                var dataWordSizeInBits = GetFifoPackedDataSizeInBits();
                // Process samples and put them in fifo.
                var sampleSize = Word0BitDepth;
                for(byte i = 0; i < frame.Length; i++)
                {
                    var isMasked = BitHelper.IsBitSet((uint)wordMask.Value, i);
                    if(isMasked)
                    {
                        // The received data is discarded and not written to the receive FIFO.
                        continue;
                    }
                    if(FifoOccupancy >= FifoCapacity)
                    {
                        // overrun error
                        errorFlag.Value = true;
                        return false;
                    }
                    if(currentFifoWriteWordBitOffset == 0)
                    {
                        currentFifoWriteWord = 0;
                    }
                    var value = frame[i];
                    var firstBitOffset = 0;
                    if(msbFirst.Value)
                    {
                        firstBitOffset = (int)(firstBitShifted.Value - (sampleSize - 1));
                    }
                    else
                    {
                        firstBitOffset = (int)firstBitShifted.Value;
                    }

                    BitHelper.SetMaskedValue(ref currentFifoWriteWord, value, currentFifoWriteWordBitOffset + firstBitOffset, (int)sampleSize);
                    currentFifoWriteWordBitOffset += dataWordSizeInBits;
                    if(currentFifoWriteWordBitOffset >= FifoWordLengthInBits)
                    {
                        queue.Enqueue(currentFifoWriteWord);
                        writePointer.Value++;
                        currentFifoWriteWordBitOffset = 0;
                    }

                    writePointer.Value = writePointer.Value % FifoCapacity;
                    sampleSize = WordNBitDepth;
                }
                return true;
            }

            // For transmitter.
            private void OutputFrame()
            {
                if(!channelEnable.Value)
                {
                    return;
                }
                var frame = new uint[NumberOfChannels];
                if(errorFlag.Value)
                {
                    // Transmit zero data until TCSR[FEF] is cleared.
                }
                else
                {
                    // Each frame (sample) consists of a fixed number of words (channels),
                    // with each word consisting of a fixed number of bits (bit depth).
                    // Words are loaded from 32-bit FIFO registers.
                    if(!TryDequeueFrame(out frame))
                    {
                        parent.DebugLog("{0}: Fifo underrun - no samples in fifo", id);
                    }
                }

                foreach(var word in frame)
                {
                    encoder?.AcceptSample(word);
                }

                UpdateInterrupts();
            }

            // For receiver.
            private void InputFrame()
            {
                if(!channelEnable.Value)
                {
                    return;
                }
                var frame = new uint[NumberOfChannels];
                for(int i = 0; i < frame.Length; i++)
                {
                    frame[i] = decoder.GetSingleSample();
                }
                if(errorFlag.Value)
                {
                    // Discard received data until RCSR[FEF] is cleared and the next frame is received.
                    return;
                }
                if(!TryEnqueueFrame(frame))
                {
                    parent.DebugLog("{0}: Fifo overrun - fifo is full", id);
                }

                UpdateInterrupts();
            }

            private int GetFifoPackedDataSizeInBits()
            {
                switch(packingMode.Value)
                {
                case FifoPacking.Disabled:
                    return 32;
                case FifoPacking.Bits8:
                    return 8;
                case FifoPacking.Bits16:
                    return 16;
                default:
                    return 0;
                }
            }

            private void Start()
            {
                if(isTx)
                {
                    StartTransmitter();
                }
                else
                {
                    StartReceiver();
                }
            }

            private void Stop()
            {
                if(isTx)
                {
                    StopTransmitter();
                }
                else
                {
                    StopReceiver();
                }
            }

            private void StartTransmitter()
            {
                var sampleRate = CalculateSampleRate();
                parent.InfoLog("{0}: Starting transmitter for {1} audio channels with bit depth {2} sampled at rate {3} Hz", id, NumberOfChannels, BitDepth, sampleRate);
                parent.DebugLog("{0}: Fifo configuration: {1}", id, this);
                encoder?.Dispose();
                if(pcmFile == null)
                {
                    parent.WarningLog("{0}: Starting transmitter without an output file", id);
                }
                else
                {
                    encoder = new PCMEncoder(BitDepth, sampleRate, NumberOfChannels, false);
                    encoder.SetOutputFile(pcmFile, littleEndianFileFormat);
                }

                StartThread(OutputFrame);
            }

            private void StartReceiver()
            {
                var sampleRate = CalculateSampleRate();
                parent.InfoLog("{0}: Starting receiver for {1} audio channels with bit depth {2} sampled at rate {3} Hz", id, NumberOfChannels, BitDepth, sampleRate);
                parent.DebugLog("{0}: Fifo configuration: {1}", id, this);
                decoder = new PCMDecoder(BitDepth, sampleRate, NumberOfChannels, false, parent);
                if(pcmFile == null)
                {
                    parent.WarningLog("{0}: Starting receiver without an input file", id);
                }
                else
                {
                    decoder.LoadFile(pcmFile, littleEndianFileFormat);
                }
                StartThread(InputFrame);
            }

            private void StopTransmitter()
            {
                encoder?.FlushBuffer();
                StopThread();
            }

            private void StopReceiver()
            {
                StopThread();
            }

            private void StartThread(Action action)
            {
                var sampleRate = CalculateSampleRate();
                sampleThread = machine.ObtainManagedThread(action, sampleRate, name: $"sai_{id}_sampling");
                sampleThread.Start();
            }

            private void StopThread()
            {
                if(sampleThread == null)
                {
                    parent.DebugLog("{0}: Trying to stop sampling when it is not active", id);
                    return;
                }
                sampleThread.Stop();
                sampleThread = null;
            }

            private bool CheckDmaTrigger()
            {
                if(isTx)
                {
                    requestFlag.Value = FifoOccupancy <= (int)watermark.Value;
                    warningFlag.Value = FifoOccupancy == 0;
                }
                else
                {
                    requestFlag.Value = FifoOccupancy > (int)watermark.Value;
                    warningFlag.Value = FifoOccupancy == FifoCapacity;
                }

                var fifoRequestDma = fifoRequestDmaEnable.Value && requestFlag.Value;
                var fifoWarningDma = fifoWarningDmaEnable.Value && warningFlag.Value;
                return fifoRequestDma || fifoWarningDma;
            }

            private void UpdateDmaRequest()
            {
                while(CheckDmaTrigger())
                {
                    var fifoCountWatch = FifoOccupancy;
                    var fifoWordBitWatch = GetCurrentFifoWordBitOffset();
                    parent.DebugLog("{0}: Triggering DMA request at FIFO level {1}", id, fifoCountWatch);
                    dmaRequestInProgress = true;
                    // Blink is used to represent both DMA request and DMA done signals at once, because during emulation DMA transfers finish immediately.
                    dmaTrigger.Blink();
                    dmaRequestInProgress = false;
                    if(fifoCountWatch == FifoOccupancy && fifoWordBitWatch == GetCurrentFifoWordBitOffset())
                    {
                        // There are a few cases when this condition is fulfilled:
                        // 1. DMA request was not accepted due to a programmed eDMA channel configuration.
                        // 2. Transfer was rejected due to errors detected by eDMA in Transfer Control Descriptor (TCD) - such errors are reported by eDMA and visible to the software.
                        // 3. TCD was correct, but it didn't target data register as source or destination address. It may be correct depending on developer intent.
                        // 4. DMA requests are not used at all by sofwtare. We don't know it at the model level, as it depends on a dynamic eDMA configuration.
                        // In either case there is no progress at SAI level, so break DMA trigger loop and try at the next opportunity.
                        parent.DebugLog("{0}: DMA request hasn't generated access to data register - it may be an intended behavior or eDMA channel misconfiguration", id);
                        break;
                    }
                }
            }

            private int GetCurrentFifoWordBitOffset()
            {
                return isTx ? currentFifoWriteWordBitOffset : currentFifoReadWordBitOffset;
            }

            private void UpdateInterrupts()
            {
                if(dmaRequestInProgress)
                {
                    return;
                }

                if(!enable.Value)
                {
                    return;
                }

                UpdateDmaRequest();

                var fifoRequest = fifoRequestInterruptEnable.Value && requestFlag.Value;
                var fifoWarning = fifoWarningInterruptEnable.Value && warningFlag.Value;
                var fifoError = fifoErrorInterruptEnable.Value && errorFlag.Value;
                var syncError = syncErrorInterruptEnable.Value && syncErrorFlag.Value;
                var wordStart = wordStartInterruptEnable.Value && wordStartFlag.Value;
                irq.Set(fifoRequest || fifoWarning || fifoError || syncError || wordStart);
            }

            private void ResetFifo()
            {
                queue.Clear();
                readPointer.Value = 0;
                writePointer.Value = 0;
                currentFifoReadWord = 0;
                currentFifoWriteWord = 0;
                currentFifoReadWordBitOffset = 0;
                currentFifoWriteWordBitOffset = 0;
            }

            private uint CalculateSampleRate()
            {
                var sampleRate = parent.clockRootFrequencyHz;
                if(bitClockDirection.Value)
                {
                    // Bit clock generated internally.
                    var divider = 1u;
                    if(!bitClockBypass.Value)
                    {
                        divider = ((uint)bitClockDivide.Value + 1) * 2;
                    }
                    sampleRate /= divider;
                }
                else
                {
                    // Bit clock generated externally.
                    // The value specified in the constructor's parameter is taken directly as the frequency of an external clock used for sampling.
                }
                return sampleRate / CalculateNumberOfBitsPerFrame();
            }

            // It always returns non-zero value.
            private uint CalculateNumberOfBitsPerFrame()
            {
                var bitsPerFrame = Word0BitDepth + (WordNBitDepth * (NumberOfChannels - 1));

                switch(packingMode.Value)
                {
                case FifoPacking.Bits8:
                    return NumberOfChannels * 8;
                case FifoPacking.Bits16:
                    return NumberOfChannels * 16;
                case FifoPacking.Disabled:
                default:
                    return bitsPerFrame;
                }
            }

            private ReadOnlyDictionary<long, DoubleWordRegister> BuildRegisterMap()
            {
                var registersMap = new Dictionary<long, DoubleWordRegister>
                {
                    {(long)TransceiverRegisters.Control, new DoubleWordRegister(parent)
                        .WithFlag(0, out fifoRequestDmaEnable, name: "FRDE")
                        .WithFlag(1, out fifoWarningDmaEnable, name: "FWDE")
                        .WithReservedBits(2, 3)
                        .WithReservedBits(5, 3)
                        .WithFlag(8, out fifoRequestInterruptEnable, name: "FRIE")
                        .WithFlag(9, out fifoWarningInterruptEnable, name: "FWIE")
                        .WithFlag(10, out fifoErrorInterruptEnable, name: "FEIE")
                        .WithFlag(11, out syncErrorInterruptEnable, name: "SEIE")
                        .WithFlag(12, out wordStartInterruptEnable, name: "WSIE")
                        .WithReservedBits(13, 3)
                        .WithFlag(16, out requestFlag, FieldMode.Read, name: "FRF")
                        .WithFlag(17, out warningFlag, FieldMode.Read, name: "FWF")
                        .WithFlag(18, out errorFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "FEF")
                        .WithFlag(19, out syncErrorFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "SEF")
                        .WithFlag(20, out wordStartFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "WSF")
                        .WithReservedBits(21, 3)
                        .WithFlag(24, writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                Reset();
                            }
                        }, name: "SR")
                        .WithFlag(25, valueProviderCallback: _ => false, writeCallback: (_, value) =>
                        {
                            // FIFO reset
                            if(value)
                            {
                                ResetFifo();
                            }
                        }, name: "FR")
                        .WithReservedBits(26, 2)
                        .WithFlag(28, out bitClockEnable, changeCallback: (_, value) =>
                        {
                            if(value && enable.Value)
                            {
                                Start();
                            }
                            else
                            {
                                Stop();
                            }
                        }, name: "BCE")
                        .WithTaggedFlag("DBGE", 29)
                        .WithTaggedFlag("STOPE", 30)
                        .WithFlag(31, out enable, changeCallback: (_, value) =>
                        {
                            bitClockEnable.Value = value;
                            if(value)
                            {
                                Start();
                            }
                            else
                            {
                                Stop();
                            }
                        }, name: $"{tr}E" /* TE/RE */)
                        .WithWriteCallback((_, __) => UpdateInterrupts())
                    },
                    {(long)TransceiverRegisters.Configuration1, new DoubleWordRegister(parent)
                        .WithValueField(0, 3, out watermark, name: $"{tr}FW" /* TFW/RFW */)
                        .WithReservedBits(3, 29)
                        .WithWriteCallback((_, __) => UpdateInterrupts())
                    },
                    {(long)TransceiverRegisters.Configuration2, new DoubleWordRegister(parent)
                        .WithValueField(0, 8, out bitClockDivide, name: "DIV")
                        .WithReservedBits(8, 15)
                        .WithFlag(23, out bitClockBypass, name: "BYP")
                        .WithFlag(24, out bitClockDirection, name: "BCD")
                        .WithTaggedFlag("BCP", 25)
                        .WithEnumField(26, 2, out msel, name: "MSEL")
                        .WithTaggedFlag("BCI", 28)
                        .WithTaggedFlag("BCS", 29)
                        .WithTag("SYNC", 30, 2)
                    },
                    {(long)TransceiverRegisters.Configuration3, new DoubleWordRegister(parent)
                        .WithTag("WDFL", 0, 5)
                        .WithReservedBits(5, 11)
                        .WithFlag(16, out channelEnable, name: $"{tr}CE" /* TCE/RCE */)
                        .WithReservedBits(17, 7)
                        .WithReservedBits(24, 1)
                        .WithReservedBits(25, 7)
                    },
                    {(long)TransceiverRegisters.Configuration4, new DoubleWordRegister(parent)
                        .WithTaggedFlag("FSD", 0)
                        .WithTaggedFlag("FSP", 1)
                        .WithTaggedFlag("ONDEM", 2)
                        .WithTaggedFlag("FSE", 3)
                        .WithFlag(4, out msbFirst, name: "MF")
                        .WithTaggedFlag("CHMOD", 5)
                        .WithReservedBits(6, 2)
                        .WithTag("SYWD", 8, 5)
                        .WithReservedBits(13, 3)
                        .WithValueField(16, 5, out frameSize, name: "FRSZ")
                        .WithReservedBits(21, 3)
                        .WithEnumField(24, 2, out packingMode, writeCallback: (_, value) =>
                        {
                            if(value == FifoPacking.Reserved)
                            {
                                parent.WarningLog("{0}: Selected reserved value for FIFO Packing Mode - a correct operation won't be possible", id);
                            }
                        }, name: "FPACK")
                        .WithReservedBits(26, 2)
                        .WithFlag(28, out continueOnError, name: "FCONT")
                        .WithReservedBits(29, 3)
                    },
                    {(long)TransceiverRegisters.Configuration5, new DoubleWordRegister(parent)
                        .WithReservedBits(0, 8)
                        .WithValueField(8, 5, out firstBitShifted, name: "FBT")
                        .WithReservedBits(13, 3)
                        .WithValueField(16, 5, out word0Width, writeCallback: (_, value) =>
                        {
                            // The written value is one less than the number of bits in the word.
                            // Do not log when default 0 value is written as it would generate spurious warnings.
                            if(word0Width.Value != 0 && Word0BitDepth % 8 != 0)
                            {
                                // Peripheral model in Renode supports only widths that are multiples of bytes.
                                parent.WarningLog("{0}: Only word 0 widths that are multiples of bytes are supported during emulation, but got {1}", id, Word0BitDepth);
                            }
                        }, name: "W0W")
                        .WithReservedBits(21, 3)
                        .WithValueField(24, 5, out wordNWidth, writeCallback: (_, value) =>
                        {
                            // The written value is one less than the number of bits in the word.
                            // Do not log when default 0 value is written as it would generate spurious warnings.
                            if(wordNWidth.Value != 0 && WordNBitDepth % 8 != 0)
                            {
                                // Peripheral model in Renode supports only widths that are multiples of bytes.
                                parent.WarningLog("{0}: Only word N widths that are multiples of bytes are supported during emulation, but got {1}", id, WordNBitDepth);
                            }
                        }, name: "WNW")
                        .WithReservedBits(29, 3)
                    },
                    // FIFO transmit/receive logic on write/read to/from this offset is implemented directly and here only a placeholder is placed.
                    {(long)TransceiverRegisters.Data, new DoubleWordRegister(parent)
                        .WithValueField(0, 32, isTx ? FieldMode.Write : FieldMode.Read, name: $"{tr}DR" /* TDR/RDR */)
                    },
                    {(long)TransceiverRegisters.Fifo, new DoubleWordRegister(parent)
                        .WithValueField(0, 4, out readPointer, FieldMode.Read, name: "RFP")
                        .WithReservedBits(4, 12)
                        .WithValueField(16, 4, out writePointer, FieldMode.Read, name: "WFP")
                        .WithReservedBits(20, 11)
                        .WithReservedBits(31, 1)
                    },
                    {(long)TransceiverRegisters.Mask, new DoubleWordRegister(parent)
                        .WithValueField(0, 32, out wordMask, name: $"{tr}WM" /* TWM/RWM */)
                    },
                    {(long)TransceiverRegisters.TimestampControl, new DoubleWordRegister(parent)
                        .WithTaggedFlag("TSEN", 0)
                        .WithTaggedFlag("TSINC", 1)
                        .WithTag("TSSEL", 2, 2)
                        .WithReservedBits(4, 4)
                        .WithTaggedFlag("RTSC", 8) // FieldMode.WriteOneToClear
                        .WithTaggedFlag("RBC", 9) // FieldMode.WriteOneToClear
                        .WithReservedBits(10, 22)
                    },
                    {(long)TransceiverRegisters.Timestamp, new DoubleWordRegister(parent)
                        .WithTag("TSC", 0, 32) // FieldMode.Read
                    },
                    {(long)TransceiverRegisters.BitCount, new DoubleWordRegister(parent)
                        .WithTag("BCNT", 0, 32) // FieldMode.Read
                    },
                    {(long)TransceiverRegisters.BitCountTimestamp, new DoubleWordRegister(parent)
                        .WithTag("BCTS", 0, 32) // FieldMode.Read
                    },
                };

                return new ReadOnlyDictionary<long, DoubleWordRegister>(registersMap);
            }

            private uint NumberOfChannels => (uint)(frameSize.Value + 1);

            private uint Word0BitDepth => (uint)(word0Width.Value + 1);

            private uint WordNBitDepth => (uint)(wordNWidth.Value + 1);

            private uint BitDepth => NumberOfChannels == 1 ? Word0BitDepth : WordNBitDepth;

            private int FifoOccupancy => queue.Count;

            private IFlagRegisterField requestFlag;
            private IFlagRegisterField warningFlag;
            private IFlagRegisterField errorFlag;
            private IValueRegisterField watermark;
            private IEnumRegisterField<FifoPacking> packingMode;
            private IFlagRegisterField continueOnError;
            private IValueRegisterField readPointer;
            private IValueRegisterField writePointer;
            private IFlagRegisterField fifoRequestDmaEnable;
            private IFlagRegisterField fifoWarningDmaEnable;
            private IFlagRegisterField fifoRequestInterruptEnable;
            private IFlagRegisterField fifoWarningInterruptEnable;
            private IFlagRegisterField fifoErrorInterruptEnable;
            private IFlagRegisterField syncErrorInterruptEnable;
            private IFlagRegisterField wordStartInterruptEnable;
            private IFlagRegisterField syncErrorFlag;
            private IFlagRegisterField wordStartFlag;
            private IFlagRegisterField bitClockEnable;
            private IFlagRegisterField enable;
            private IValueRegisterField bitClockDivide;
            private IFlagRegisterField bitClockBypass;
            private IFlagRegisterField bitClockDirection;
            private IEnumRegisterField<ClockOption> msel;
            private IFlagRegisterField channelEnable;
            private IFlagRegisterField msbFirst;
            private IValueRegisterField frameSize;
            private IValueRegisterField firstBitShifted;
            private IValueRegisterField word0Width;
            private IValueRegisterField wordNWidth;
            private IValueRegisterField wordMask;

            private string pcmFile;
            private bool littleEndianFileFormat;
            private PCMDecoder decoder;
            private PCMEncoder encoder;
            private IManagedThread sampleThread;
            private uint currentFifoReadWord;
            private uint currentFifoWriteWord;
            private int currentFifoReadWordBitOffset;
            private int currentFifoWriteWordBitOffset;
            private bool dmaRequestInProgress;
            private readonly IMXRT700_SAI parent;
            private readonly IMachine machine;
            private readonly GPIO irq;
            private readonly GPIO dmaTrigger;
            private readonly bool isTx;
            private readonly string tr;
            private readonly string id;

            private readonly ReadOnlyDictionary<long, DoubleWordRegister> registers;
            private readonly Queue<uint> queue = new Queue<uint>(FifoCapacity);
            private const int FifoCapacity = 8;
            private const int FifoWordLengthInBits = 32;

            private enum TransceiverRegisters
            {
                Control = 0x00,
                Configuration1 = 0x04,
                Configuration2 = 0x08,
                Configuration3 = 0x0C,
                Configuration4 = 0x10,
                Configuration5 = 0x14,
                Data = 0x18,
                Fifo = 0x38,
                Mask = 0x58,
                TimestampControl = 0x68,
                Timestamp = 0x6C,
                BitCount = 0x70,
                BitCountTimestamp = 0x74,
            }
        }

        private enum FifoPacking
        {
            Disabled = 0b00,
            Reserved = 0b01,
            Bits8 = 0b10,
            Bits16 = 0b11
        }

        private enum ClockOption
        {
            BusClock = 0b00, // compute_main_clk (SAI0-SAi2) or sense_main_clk (SAI3) depending on domain
            MClockOrMux = 0b01, // MCLK if MCR.MOE == 0, otherwise clock configured by SAI012FCLKSEL/SAI012CLKDIV or SAI3FCLKSEL/SAI3CLKDIV
            Reserved2 = 0b10,
            Reserved3 = 0b11
        }
    }
}