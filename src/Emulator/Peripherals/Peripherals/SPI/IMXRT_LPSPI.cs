//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class IMXRT_LPSPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public IMXRT_LPSPI(IMachine machine, uint fifoSize = 4) : base(machine)
        {
            if(!Misc.IsPowerOfTwo(fifoSize))
            {
                throw new ConstructionException($"Invalid fifoSize! It has to be a power of 2 but the fifoSize provided was: {fifoSize}");
            }
            this.fifoSize = fifoSize;

            dataMatcher = new DataMatcher();
            IRQ = new GPIO();
            receiveFifo = new Queue<uint>();
            transmitFifo = new Queue<TCFifoEntry>();
            transmitFifoRestore = new Queue<TCFifoEntry>();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();

            Reset();
        }

        public override void Reset()
        {
            continuousTransferInProgress = false;
            selectedDevice = null;
            sizeLeft = 0;
            currentCommand = null;
            dataMatcher.Reset();
            receiveFifo.Clear();
            transmitFifo.Clear();
            transmitFifoRestore.Clear();

            RegistersCollection.Reset();
            UpdateInterrupts();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "TDF - Transmit Data Flag", valueProviderCallback: _ => true) // TX fifo is always empty, so this bit is always active
                .WithFlag(1, FieldMode.Read, name: "RDF - Receive Data Flag",
                    valueProviderCallback: _ => receiveFifo.Count > (int)rxWatermark.Value
                )
                .WithReservedBits(2, 6)
                // b8-9, b11-13: Unused but don't warn if they're cleared.
                .WithFlag(8, FieldMode.WriteOneToClear, name: "WCF - Word Complete Flag")
                .WithFlag(9, FieldMode.WriteOneToClear, name: "FCF - Frame Complete Flag")
                .WithFlag(10, out transferComplete, FieldMode.Read | FieldMode.WriteOneToClear, name: "TCF - Transfer Complete Flag")
                .WithFlag(11, FieldMode.WriteOneToClear, name: "TEF - Transmit Error Flag")
                .WithFlag(12, FieldMode.WriteOneToClear, name: "REF - Receive Error Flag")
                .WithFlag(13, out dataMatch, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMF - Data Match Flag")
                .WithReservedBits(14, 10)
                .WithTaggedFlag("MBF - Module Busy Flag", 24)
                .WithReservedBits(25, 7);

            Registers.TransmitCommand.Define(this)
                .WithValueField(0, 12, out frameSize, name: "FRAMESZ - Frame Size")
                .WithReservedBits(12, 4)
                .WithTag("WIDTH - Transfer Width", 16, 2) // enum
                .WithFlag(18, out transmitDataMask, name: "TXMSK - Transmit Data Mask", valueProviderCallback: _ => currentCommand?.TxMask ?? false)
                .WithFlag(19, out receiveDataMask, name: "RXMSK - Receive Data Mask", valueProviderCallback: _ => currentCommand?.RxMask ?? false)
                .WithFlag(20, out continuingCommand, name: "CONTC - Continuing Command", valueProviderCallback: _ => currentCommand?.ContinuingCommand ?? false)
                .WithFlag(21, out continuousTransfer, name: "CONT - Continuous Transfer", valueProviderCallback: _ => currentCommand?.Continuous ?? false)
                .WithTaggedFlag("BYSW - Byte Swap", 22)
                .WithTaggedFlag("LSBF - LSB First", 23)
                .WithValueField(24, 2, name: "PCS - Peripheral Chip Select", writeCallback: (_, value) =>
                {
                    if(!TryGetByAddress((int)value, out selectedDevice))
                    {
                        this.Log(LogLevel.Error, "No device is connected to LPSPI_PCS[{0}]!", value);
                    }
                })
                .WithReservedBits(26, 1)
                .WithTag("PRESCALE - Prescaler Value", 27, 3) // enum
                // Unused but don't warn when it's set.
                .WithFlag(30, name: "CPHA - Clock Phase")
                .WithTaggedFlag("CPOL - Clock Polarity", 31)
                .WithWriteCallback((_, cmd) =>
                {
                    this.Log(LogLevel.Debug, "Pushing a command: 0x{0:X8}", cmd);
                    transmitFifo.Enqueue(new TCFifoCmd
                    {
                        ByteSwap = false,
                        TxMask = transmitDataMask.Value,
                        RxMask = receiveDataMask.Value,
                        Continuous = continuousTransfer.Value,
                        ContinuingCommand = continuingCommand.Value
                    });
                    UpdateTransmitter();
                });

            Registers.TransmitData.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) =>
                {
                    this.Log(LogLevel.Debug, "Pushing data: 0x{0:X8}", val);
                    transmitFifo.Enqueue(new TCFifoData
                    {
                        Data = (uint)val
                    });
                    UpdateTransmitter();
                });

            Registers.FIFOStatus.Define(this)
                .WithValueField(0, 5, FieldMode.Read, name: "TXCOUNT - Transmit FIFO Count", valueProviderCallback: _ => 0)
                .WithReservedBits(5, 11)
                .WithValueField(16, 5, FieldMode.Read, name: "RXCOUNT - Receive FIFO Count", valueProviderCallback: _ => (ulong)receiveFifo.Count)
                .WithReservedBits(21, 11);

            Registers.FIFOControl.Define(this)
                .WithTag("TXWATER - Transmit FIFO Watermark", 0, 2)
                .WithReservedBits(2, 14)
                .WithValueField(16, 2, out rxWatermark, name: "RXWATER - Receive FIFO Watermark")
                .WithReservedBits(18, 14);

            Registers.ReceiveData.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "RDR - Receive Data",
                    valueProviderCallback: _ => receiveFifo.Count != 0 ? receiveFifo.Peek() : 0,
                    readCallback: (_, __) =>
                    {
                        if(!receiveFifo.TryDequeue(out var result))
                        {
                            this.Log(LogLevel.Warning, "Receive FIFO underflow");
                            return;
                        }
                        UpdateInterrupts();
                    }
                );

            Registers.DataMatch0.Define(this)
                .WithValueField(0, 32, out match0, name: "MATCH0 - Data match 0", changeCallback: (_, val) =>
                {
                    if(rxDataMatchOnly.Value)
                    {
                        // This is undefined behaviour. The manual says you should not do that.
                        this.Log(LogLevel.Warning, "Changing MATCH0 while CFGR0[RDMO] == 1 is prohibited");
                    }
                });

            Registers.DataMatch1.Define(this)
                .WithValueField(0, 32, out match1, name: "MATCH1 - Data match 1", changeCallback: (_, val) =>
                {
                    if(rxDataMatchOnly.Value)
                    {
                        // This is undefined behaviour. The manual says you should not do that.
                        this.Log(LogLevel.Warning, "Changing MATCH1 while CFGR0[RDMO] == 1 is prohibited");
                    }
                });

            Registers.Control.Define(this)
                .WithFlag(0, out moduleEnable, name: "MEN - Module Enable", changeCallback: (_, val) => UpdateTransmitter())
                .WithFlag(1, name: "RST - Software Reset", changeCallback: (_, val) =>
                {
                    if(val)
                    {
                        this.Log(LogLevel.Debug, "Software Reset requested by writing RST to the Control Register");
                        // TODO: The Control Register shouldn't be cleared. RST should remain set until cleared by software.
                        Reset();
                    }
                })
                .WithTaggedFlag("DOZEN - Doze mode enable", 2)
                .WithTaggedFlag("DBGEN - Debug Enable", 3)
                .WithReservedBits(4, 4)
                .WithFlag(8, FieldMode.Write, name: "RTF - Reset Transmit FIFO", writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        transmitFifo.Clear();
                        transmitFifoRestore.Clear();
                    }
                })
                .WithFlag(9, FieldMode.Write, name: "RRF - Reset Receive FIFO", writeCallback: (_, val) => { if(val) receiveFifo.Clear(); })
                .WithReservedBits(10, 22)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnable.Define(this)
                // Transmit Data Flag is always set so the Transmit Interrupt is never triggered.
                .WithFlag(0, name: "TDIE - Transmit Data Interrupt Enable")
                .WithFlag(1, out receiveDataInterruptEnable, name: "RDIE - Receive Data Interrupt Enable")
                .WithReservedBits(2, 6)
                .WithTaggedFlag("WCIE - Word Complete Interrupt Enable", 8)
                .WithTaggedFlag("FCIE - Frame Complete Interrupt Enable", 9)
                .WithFlag(10, out transferCompleteInterruptEnable, name: "TCIE - Transfer Complete Interrupt Enable")
                // b11-12: These interrupts are never used but allow them to be enabled.
                .WithFlag(11, name: "TEIE - Transmit Error Interrupt Enable")
                .WithFlag(12, name: "REIE - Receive Error Interrupt Enable")
                .WithFlag(13, out dataMatchInterruptEnable, name: "DMIE - Data Match Interrupt Enable")
                .WithReservedBits(14, 18)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.Parameter.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "TXFIFO - Transmit FIFO Size (in words; size = 2^TXFIFO)",
                    valueProviderCallback: _ => (ulong)Misc.Logarithm2((int)fifoSize)
                )
                .WithValueField(8, 8, FieldMode.Read, name: "RXFIFO - Receive FIFO Size (in words; size = 2^RXFIFO)",
                    valueProviderCallback: _ => (ulong)Misc.Logarithm2((int)fifoSize)
                )
                .WithValueField(16, 8, FieldMode.Read, name: "PCSNUM - PCS Number (Indicates the number of PCS pins supported)",
                    valueProviderCallback: _ => (ulong)ChildCollection.Count
                )
                .WithReservedBits(24, 8);

            Registers.Configuration0.Define(this)
                .WithReservedBits(10, 22)
                .WithFlag(9, out rxDataMatchOnly, name: "RDMO - Receive Data Match Only", writeCallback: (_, val) =>
                {
                    if(dataMatch.Value && val)
                    {
                        this.Log(LogLevel.Warning, "Writing 1 to CFGR0[RDMO] while SR[DMF] is set to 1");
                    }
                })
                .WithFlag(8, out circularFifoEnabled, name: "CIRFIFO - Circular FIFO Enable", changeCallback: (_, val) =>
                {
                    transmitFifoRestore.Clear();
                    if(!val)
                    {
                        return;
                    }

                    bool contXferWithCirFifo = false;
                    this.Log(LogLevel.Debug, "Enabling CIRFIFO");
                    foreach(var word in transmitFifo)
                    {
                        transmitFifoRestore.Enqueue(word);

                        if(word is TCFifoCmd cmd)
                        {
                            contXferWithCirFifo |= cmd.Continuous;
                        }
                    }
                    if(contXferWithCirFifo)
                    {
                        this.Log(LogLevel.Warning, "Continuous transfer command enabled/queued when CFGR0[CIRFIFO] is on, this can cause an infinie transfer loop");
                    }
                })
                .WithReservedBits(4, 4)
                .WithTaggedFlag("HRDIR - HostRequest Direction", 3)
                .WithTaggedFlag("HRSEL - Host Request Select", 2)
                .WithTaggedFlag("HRPOL - Host Request Polarity", 1)
                .WithTaggedFlag("HREN - Host Request Enable", 0);

            Registers.Configuration1.Define(this)
                .WithFlag(0, out masterMode, name: "MASTER - Master Mode", changeCallback: (_, val) =>
                {
                    this.Log(LogLevel.Debug, "Switching to the {0} Mode", val ? "Master" : "Slave");
                    if(moduleEnable.Value)
                    {
                        this.Log(LogLevel.Warning, "The LPSPI module should be disabled when changing the Configuration Mode");
                    }
                })
                // Unused but don't warn when it's set.
                .WithFlag(1, name: "SAMPLE - Sample Point")
                .WithTaggedFlag("AUTOPCS - Automatic PCS", 2)
                .WithTaggedFlag("NOSTALL - No Stall", 3)
                .WithReservedBits(4, 4)
                .WithTag("PCSPOL - Peripheral Chip Select Polarity", 8, 4)
                .WithReservedBits(12, 4)
                .WithValueField(16, 3, out matchConfig, name: "MATCFG - Match Configuration")
                .WithReservedBits(19, 5)
                .WithTag("PINCFG - Pin Configuration", 24, 2)
                .WithTaggedFlag("OUTCFG - Output Config", 26)
                .WithTag("PCSCFG - Peripheral Chip Select Configuration", 27, 2)
                .WithReservedBits(29, 3);

            // Unused but don't warn when it's read from or written to.
            Registers.ClockConfiguration.Define(this)
                .WithValueField(0, 8, name: "SCKDIV - SCK Divider")
                .WithValueField(8, 8, name: "DBT - Delay Between Transfers")
                .WithValueField(16, 8, name: "PCSSCK - PCS-to-SCK Delay")
                .WithValueField(24, 8, name: "SCKPCS - SCK-to-PCS Delay");
        }

        private uint GetFrameSize()
        {
            // frameSize keeps value substracted by 1
            var sizeLeft = (uint)frameSize.Value + 1;
            if(sizeLeft % 8 != 0)
            {
                sizeLeft += 8 - (sizeLeft % 8);
                this.Log(LogLevel.Warning, "Only 8-bit-aligned transfers are currently supported, but frame size is set to {0}, adjusting it to: {1}", frameSize.Value, sizeLeft);
            }

            return sizeLeft;
        }

        private bool CanTransfer(out ISPIPeripheral device)
        {
            device = null;
            if(!moduleEnable.Value)
            {
                this.Log(LogLevel.Warning, "Trying to send data, but the LPSPI module is disabled");
                return false;
            }

            if(!masterMode.Value)
            {
                this.Log(LogLevel.Error, "The Slave Mode is not supported");
                return false;
            }

            return TryGetDevice(out device);
        }

        private void DoDummyTransfer()
        {
            if(!CanTransfer(out var device))
            {
                return;
            }

            // send dummy bytes
            while(!TrySendDataInner(0, device)) { }
        }

        private bool TrySendData(uint value)
        {
            if(!CanTransfer(out var device))
            {
                return false;
            }

            return TrySendDataInner(value, device);
        }

        private bool TrySendDataInner(uint value, ISPIPeripheral device)
        {
            if(sizeLeft == 0)
            {
                // let's assume this is a new transfer
                this.Log(LogLevel.Debug, "Starting a new SPI xfer, frame size: {0} bytes", GetFrameSize() / 8);
                sizeLeft = GetFrameSize();
                dataMatcher.Configure((MatchMode)matchConfig.Value);
                dataMatcher.Match0 = (uint)match0.Value;
                dataMatcher.Match1 = (uint)match1.Value;
            }

            // we can read up to 4 bytes at a time
            var byteIdx = 0;
            uint receivedWord = 0;

            this.Log(LogLevel.Debug, "Sending 0x{0:X} to the device", value);
            while(sizeLeft != 0 && byteIdx < 4)
            {
                var resp = device.Transmit((byte)value);

                receivedWord |= (uint)resp << (byteIdx * 8);

                value >>= 8;
                sizeLeft -= 8;
                byteIdx++;
            }
            this.Log(LogLevel.Debug, "Received response 0x{0:X} from the device", receivedWord);

            if(!receiveDataMask.Value && dataMatcher.MatchAndPush(receiveFifo, receivedWord, byteIdx * 8, rxDataMatchOnly.Value))
            {
                dataMatch.Value = true;
            }

            if(!currentCommand.ContinuingCommand)
            {
                transferComplete.Value = true;
                UpdateInterrupts();
            }

            if(sizeLeft == 0 && !currentCommand.ContinuingCommand)
            {
                if(!continuousTransfer.Value)
                {
                    device.FinishTransmission();
                }
                else
                {
                    continuousTransferInProgress = true;
                }
            }

            return true;
        }

        private bool TryGetDevice(out ISPIPeripheral device)
        {
            device = selectedDevice;
            if(device == null)
            {
                this.Log(LogLevel.Warning, "No device connected!");
                return false;
            }
            return true;
        }

        private void UpdateInterrupts()
        {
            var flag = false;

            flag |= receiveDataInterruptEnable.Value && receiveFifo.Count > (int)rxWatermark.Value;
            flag |= transferComplete.Value && transferCompleteInterruptEnable.Value;
            flag |= dataMatch.Value | dataMatchInterruptEnable.Value;

            this.Log(LogLevel.Debug, "Setting IRQ flag to {0}", flag);
            IRQ.Set(flag);
        }

        private void TryTransmitFifoDequeue(out uint? poppedData)
        {
            bool restored = false;
            poppedData = null;

            if(transmitFifo.TryDequeue(out var tcEntry))
            {
                if(tcEntry is TCFifoCmd cmd)
                {
                    currentCommand = cmd;
                }
                else if(tcEntry is TCFifoData data)
                {
                    poppedData = data.Data;
                }
                return;
            }

            // Restoration procedure for circular FIFO
            foreach(var word in transmitFifoRestore)
            {
                transmitFifo.Enqueue(word);
                restored = true;
            }

            if(restored)
            {
                this.Log(LogLevel.Debug, "Restored Tx FIFO");
                TryTransmitFifoDequeue(out poppedData);
            }
        }

        private void UpdateTransmitter(TCFifoEntry transmitEntry = null)
        {
            if(!moduleEnable.Value)
            {
                return;
            }

            if(transmitEntry != null)
            {
                if(transmitFifo.Count > fifoSize)
                {
                    this.Log(LogLevel.Warning, "Trying to enqueue entry to the transmit FIFO which is full, data ignored");
                    return;
                }
                transmitFifo.Enqueue(transmitEntry);

                if(circularFifoEnabled.Value)
                {
                    if(transmitFifoRestore.Count >= fifoSize)
                    {
                        // Avoid storing more data in the restore FIFO rather than in the regular FIFO
                        transmitFifoRestore.Dequeue();
                    }
                    transmitFifoRestore.Enqueue(transmitEntry);
                }
            }

            do
            {
                TryTransmitFifoDequeue(out uint? poppedData);

                // Note: Section 71.3.1.1.1 describes one of the conditions as "data is written to transmit FIFO". This can be understood as either anything being present in the FIFO
                // or as transmission data being present there.
                // Note: The manual does not mention that a command has to be loaded first, but it makes no sense to process data otherwise. It's likely an undefined behaviour.
                bool initSpiXfer = currentCommand != null && poppedData.HasValue && moduleEnable.Value;
                if(!initSpiXfer)
                {
                    this.Log(LogLevel.Debug, "SPI transfer not initialized");
                    // Nothing more to do
                    break;
                }

                if(!currentCommand.ContinuingCommand && continuousTransferInProgress && (sizeLeft == 0))
                {
                    continuousTransferInProgress = false;
                    // Finish last transmission in case with are not in continuing mode.
                    if(TryGetDevice(out var device))
                    {
                        device.FinishTransmission();
                    }
                }

                // When Transmit Data Mask is set, transmit data is masked (no data is loaded from transmit FIFO and output pin is tristated).
                // In master mode, the Transmit Data Mask bit will initiate a new transfer which cannot be aborted by another command word;
                if(currentCommand.TxMask)
                {
                    DoDummyTransfer();
                    // according to the documentation:
                    // "the Transmit Data Mask bit will be cleared by hardware at the end of the transfer"
                    // It is unclear what should be done in case of continuous transfers, but it's assumed that the TxMask should stay as it was set.
                    if(!currentCommand.Continuous)
                    {
                        currentCommand.TxMask = false;
                    }
                }
                else if(poppedData.HasValue)
                {
                    if(!TrySendData(poppedData.Value))
                    {
                        this.Log(LogLevel.Error, "Couldn't send data");
                    }
                }
                else
                {
                    // We need to wait for more data
                    this.Log(LogLevel.Debug, "No more data on FIFO");
                    break;
                }
            } while(currentCommand.Continuous || sizeLeft != 0);
        }

        private IFlagRegisterField masterMode;
        private IFlagRegisterField moduleEnable;
        private IValueRegisterField frameSize;
        private IFlagRegisterField receiveDataMask;
        private IFlagRegisterField transmitDataMask;
        private IFlagRegisterField transferComplete;
        private IValueRegisterField rxWatermark;
        private IFlagRegisterField continuingCommand;
        private IFlagRegisterField continuousTransfer;
        private IFlagRegisterField circularFifoEnabled;

        private IFlagRegisterField receiveDataInterruptEnable;
        private IFlagRegisterField transferCompleteInterruptEnable;
        private IFlagRegisterField dataMatchInterruptEnable;

        private bool continuousTransferInProgress;
        private readonly uint fifoSize;
        private readonly Queue<uint> receiveFifo;
        private ISPIPeripheral selectedDevice;
        private readonly Queue<TCFifoEntry> transmitFifo;
        private readonly Queue<TCFifoEntry> transmitFifoRestore;
        private TCFifoCmd currentCommand;
        private uint sizeLeft;

        private IValueRegisterField match0;
        private IValueRegisterField match1;
        private IValueRegisterField matchConfig;

        private IFlagRegisterField rxDataMatchOnly;
        private IFlagRegisterField dataMatch;

        private readonly DataMatcher dataMatcher;

        private abstract class TCFifoEntry { }

        private class TCFifoCmd : TCFifoEntry
        {
            public byte XferWidth { get; set; }
            public bool TxMask { get; set; }
            public bool RxMask { get; set; }
            public ISPIPeripheral Pcs { get; set; }
            public bool Continuous { get; set; }
            public bool ContinuingCommand { get; set; }
            public bool ByteSwap { get; set; }
        }

        private class TCFifoData : TCFifoEntry
        {
            public uint Data { get; set; }
        }

        private class DataMatcher
        {
            public void Reset()
            {
                lastWord = null;
                Configure(MatchMode.Disabled);
                Match0 = default;
                Match1 = default;
                Array.Clear(matchBuffer, 0, matchBuffer.Length);
                Array.Clear(compareBuffer, 0, compareBuffer.Length);
            }

            public void Configure(MatchMode mode)
            {
                lastWord = null;
                matchMode = mode;
                Active = mode != MatchMode.Disabled;
                matchFirstOnly = ((int)mode & 0b001) == 0;
                switch(mode)
                {
                    case MatchMode.SeqFirst:
                    case MatchMode.SeqAny:
                        match1Mode = Match1Mode.Seq;
                        break;
                    case MatchMode.FirstCompareMasked:
                    case MatchMode.AnyCompareMasked:
                        match1Mode = Match1Mode.Mask;
                        break;
                    default:
                        match1Mode = Match1Mode.None;
                        break;
                }
            }

            public bool MatchAndPush(Queue<uint> fifo, uint value, int width, bool dataMatchOnly = false)
            {
                if(!Active)
                {
                    if(!dataMatchOnly)
                    {
                        // Pass-through
                        fifo.Enqueue(value);
                    }
                    return false;
                }

                matchBuffer[0] = value;
                int wordCount = 1;

                // In sequential mode we need to read two words
                if(match1Mode == Match1Mode.Seq)
                {
                    if(!lastWord.HasValue)
                    {
                        lastWord = value;
                        if(!dataMatchOnly)
                        {
                            fifo.Enqueue(value);
                        }
                        return false;
                    }
                    matchBuffer[1] = matchBuffer[0];
                    matchBuffer[0] = lastWord.Value;
                    ++wordCount;

                    // In sequential mode we need to wait for at least two words
                    if(matchFirstOnly)
                    {
                        Active = false;
                    }
                }
                else
                {
                    if(matchFirstOnly)
                    {
                        Active = false;
                    }
                }

                // Prepare match buffers
                uint lastWordCmpMask = WordSizeMask(width);
                if(match1Mode == Match1Mode.Mask)
                {
                    lastWordCmpMask &= Match1;
                }

                compareBuffer[1] = Match1;
                // The two "compare" modes just OR MATCH0 and MATCH0 to create the comparison word
                compareBuffer[0] = Match0 | (match1Mode == Match1Mode.None ? Match1 : 0);
                // Zero-out irrelevant bits (masked or not part of the word given its width)
                compareBuffer[wordCount - 1] &= lastWordCmpMask;
                matchBuffer[wordCount - 1] &= lastWordCmpMask;

                // Perform comparisons
                bool match = true;

                if((compareBuffer[0] != matchBuffer[0]) || (wordCount == 2 && compareBuffer[1] != matchBuffer[1]))
                {
                    match = false;
                }

                // Manage the FIFO
                if(!dataMatchOnly || match)
                {
                    if(match1Mode == Match1Mode.Seq && dataMatchOnly)
                    {
                        fifo.Enqueue(lastWord.Value);
                    }
                    fifo.Enqueue(value);
                }

                lastWord = value;
                return match;
            }

            public bool Active { get; set; }
            public MatchMode Mode => matchMode;
            public uint Match0 { get; set; }
            public uint Match1 { get; set; }

            private static uint WordSizeMask(int wordSizeInBits)
            {
                if(wordSizeInBits >= 32)
                {
                    return uint.MaxValue;
                }
                return (1U << wordSizeInBits) - 1;
            }

            private bool matchFirstOnly;
            private MatchMode matchMode;
            private Match1Mode match1Mode;

            private uint? lastWord;

            // Two-word buffer of inputs to compare. The second word is relevant only in sequential mode.
            private readonly uint[] matchBuffer = new uint[2];
            // Two-word buffer of reference words to compare inputs with. The second word is relevant only in sequential mode.
            // Those words are based on the contents of `Match0` and `Match1`.
            private readonly uint[] compareBuffer = new uint[2];

            private enum Match1Mode
            {
                None,
                Seq,
                Mask,
            }
        }

        private enum MatchMode : long
        {
            Disabled = 0b000,
            FirstCompare = 0b010,
            AnyCompare = 0b011,
            SeqFirst = 0b100,
            SeqAny = 0b101,
            FirstCompareMasked = 0b110,
            AnyCompareMasked = 0b111,
        }

        private enum Registers : long
        {
            VersionID = 0x0,
            Parameter = 0x4,
            Control = 0x10,
            Status = 0x14,
            InterruptEnable = 0x18,
            DMAEnable = 0x1C,
            Configuration0 = 0x20,
            Configuration1 = 0x24,
            DataMatch0 = 0x30,
            DataMatch1 = 0x34,
            ClockConfiguration = 0x40,
            FIFOControl = 0x58,
            FIFOStatus = 0x5C,
            TransmitCommand = 0x60,
            TransmitData = 0x64,
            ReceiveStatus = 0x70,
            ReceiveData = 0x74
        }
    }
}
