//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class IMXRT_LPSPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public IMXRT_LPSPI(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
            outputFifo = new Queue<byte>();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public override void Reset()
        {
            sizeLeft = 0;
            continuousTransferInProgress = false;

            outputFifo.Clear();
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

        public long Size => 0x4000;

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "TDF - Transmit Data Flag", valueProviderCallback: _ => true) // TX fifo is always empty, so this bit is always active
                .WithFlag(1, FieldMode.Read, name: "RDF - Receive Data Flag", valueProviderCallback: _ => GetWordsCount() > rxWatermark.Value)
                .WithReservedBits(2, 6)
                .WithTaggedFlag("WCF - Word Complete Flag", 8)
                .WithTaggedFlag("FCF - Frame Complete Flag", 9)
                .WithFlag(10, out transferComplete, FieldMode.Read | FieldMode.WriteOneToClear, name: "TCF - Transfer Complete Flag")
                .WithTaggedFlag("TEF - Transmit Error Flag", 11)
                .WithTaggedFlag("REF - Receive Error Flag", 12)
                .WithTaggedFlag("DMF - Data Match Flag", 13)
                .WithReservedBits(14, 10)
                .WithTaggedFlag("MBF - Module Busy Flag", 24)
                .WithReservedBits(25, 7);

            Registers.TransmitCommand.Define(this)
                .WithValueField(0, 12, out frameSize, name: "FRAMESZ - Frame Size")
                .WithReservedBits(12, 4)
                .WithTag("WIDTH - Transfer Width", 16, 2) // enum
                .WithFlag(18, out transmitDataMask, name: "TXMSK - Transmit Data Mask")
                .WithFlag(19, out receiveDataMask, name: "RXMSK - Receive Data Mask")
                .WithFlag(20, out continuingCommand, name: "CONTC - Continuing Command")
                .WithFlag(21, out continuousTransfer, name: "CONT - Continuous Command")
                .WithTaggedFlag("BYSW - Byte Swap", 22)
                .WithTaggedFlag("LSBF - LSB First", 23)
                .WithTag("PCS - Peripheral Chip Select", 24, 2) // enum
                .WithReservedBits(26, 1)
                .WithTag("PRESCALE - Prescaler Value", 27, 3) // enum
                .WithTaggedFlag("CPHA - Clock Phase", 30)
                .WithTaggedFlag("CPOL - Clock Polarity", 31)
                .WithWriteCallback((_, __) =>
                {
                    if(!continuingCommand.Value && continuousTransferInProgress)
                    {
                        continuousTransferInProgress = false;
                        if(TryGetDevice(out var device))
                        {
                            device.FinishTransmission();
                        }
                    }

                    // When Transmit Data Mask is set, transmit data is masked (no data is loaded from transmit FIFO and output pin is tristated). In
                    // master mode, the Transmit Data Mask bit will initiate a new transfer which cannot be aborted by another command word;
                    if(transmitDataMask.Value)
                    {
                        DoDummyTransfer();
                        // according to the documentation:
                        // "the Transmit Data Mask bit will be cleared by hardware at the end of the transfer"
                        transmitDataMask.Value = false;
                    }
                });

            Registers.TransmitData.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => 
                {
                    if(!TrySendData(val))
                    {
                        this.Log(LogLevel.Warning, "Couldn't send data");
                    }
                });

            Registers.FIFOStatus.Define(this)
                .WithValueField(0, 5, FieldMode.Read, name: "TXCOUNT - Transmit FIFO Count", valueProviderCallback: _ => 0)
                .WithReservedBits(5, 11)
                .WithValueField(16, 5, FieldMode.Read, name: "RXCOUNT - Receive FIFO Count", valueProviderCallback: _ => GetWordsCount())
                .WithReservedBits(21, 11);

            Registers.FIFOControl.Define(this)
                .WithTag("TXWATER - Transmit FIFO Watermark", 0, 4)
                .WithReservedBits(5, 11)
                .WithValueField(16, 5, out rxWatermark, name: "RXWATER - Receive FIFO Watermark")
                .WithReservedBits(21, 11);

            Registers.ReceiveData.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                {
                    var result = 0u;

                    var issueFifoUnderflowWarning = false;
                    var bytesPerWord = GetWordSizeInBits() / 8;
                    for(var i = 0; i < bytesPerWord; i++)
                    {
                        result <<= 8;

                        if(outputFifo.TryDequeue(out var val))
                        {
                            result += val;
                        }
                        else
                        {
                            issueFifoUnderflowWarning = true;
                        }
                    }

                    if(issueFifoUnderflowWarning)
                    {
                        this.Log(LogLevel.Warning, "Receive FIFO underflow");
                    }

                    return result;
                });

            Registers.Control.Define(this)
                .WithFlag(0, out moduleEnable, name: "MEN - Module Enable")
                .WithTaggedFlag("RST - Software Reset", 1)
                .WithTaggedFlag("DOZEN - Doze mode enable", 2)
                .WithTaggedFlag("DBGEN - Debug Enable", 3)
                .WithReservedBits(4, 4)
                .WithFlag(8, FieldMode.Write, name: "RTF - Reset Transmit FIFO") // since TX fifo is always empty, there is nothing to do here
                .WithFlag(9, FieldMode.Write, name: "RRF - Reset Receive FIFO", writeCallback: (_, val) => { if(val) outputFifo.Clear(); })
                .WithReservedBits(10, 22);

            Registers.InterruptEnable.Define(this)
                .WithTaggedFlag("TDIE - Transmit Data Interrupt Enable", 0)
                .WithTaggedFlag("RDIE - Receive Data Interrupt Enable", 1)
                .WithReservedBits(2, 6)
                .WithTaggedFlag("WCIE - Word Complete Interrupt Enable", 8)
                .WithTaggedFlag("FCIE - Frame Complete Interrupt Enable", 9)
                .WithFlag(10, out transferCompleteInterruptEnable, name: "TCIE - Transfer Complete Interrupt Enable")
                .WithTaggedFlag("TEIE - Transmit Error Interrupt Enable", 11)
                .WithTaggedFlag("REIE - Receive Error Interrupt Enable", 12)
                .WithTaggedFlag("DMIE - Data Match Interrupt Enable", 13)
                .WithReservedBits(14, 18)
                .WithWriteCallback((_, __) => UpdateInterrupts());
        }

        private uint GetWordSizeInBits()
        {
            return Math.Min(32, frameSize.Value + 1);
        }

        private uint GetWordsCount()
        {
            var wordSize = GetWordSizeInBits();
            return (uint)(((outputFifo.Count * 8) + 1) / wordSize);
        }

        private uint GetFrameSize()
        {
            // frameSize keeps value substructed by 1
            var sizeLeft = frameSize.Value + 1;
            if(sizeLeft % 8 != 0)
            {
                sizeLeft += (8 - (sizeLeft % 8));
                this.Log(LogLevel.Warning, "Only 8-bit-aligned transfers are currently supported, but frame size is set to {0}. Adjusting it to: {1}", frameSize.Value, sizeLeft);
            }

            return sizeLeft;
        }

        private bool CanTransfer(out ISPIPeripheral device)
        {
            if(!moduleEnable.Value)
            {
                this.Log(LogLevel.Warning, "Trying to send data, but the LPSPI module is disabled");
                device = null;
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
            while(!TrySendDataInner(0, device)) {}
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
                sizeLeft = GetFrameSize();
            }

            var result = new List<byte>();

            // we can read up to 4 bytes at a time
            var ctr = 0;
            while(sizeLeft != 0 && ctr < 4)
            {
                this.Log(LogLevel.Debug, "Sending 0x{0:X} to the device", (byte)value);
                var resp = device.Transmit((byte)value);

                this.Log(LogLevel.Debug, "Received response 0x{0:X} from the device", resp);
                if(!receiveDataMask.Value)
                {
                    outputFifo.Enqueue(resp);
                }

                value >>= 8;
                sizeLeft -= 8;
                ctr++;
            }

            transferComplete.Value = true;
            UpdateInterrupts();

            if(sizeLeft == 0)
            {
                if(!continuousTransfer.Value)
                {
                    device.FinishTransmission();
                }
                else
                {
                    continuousTransferInProgress = true;
                }

                return true;
            }

            return false;
        }

        private bool TryGetDevice(out ISPIPeripheral device)
        {
            device = RegisteredPeripheral;
            if(device == null)
            {
                this.Log(LogLevel.Warning, "No device connected");
                return false;
            }

            return true;
        }

        private void UpdateInterrupts()
        {
            var flag = false;

            flag |= transferComplete.Value && transferCompleteInterruptEnable.Value;

            this.Log(LogLevel.Debug, "Setting IRQ flag to {0}", flag);
            IRQ.Set(flag);
        }

        private IFlagRegisterField moduleEnable;
        private IValueRegisterField frameSize;
        private IFlagRegisterField receiveDataMask;
        private IFlagRegisterField transmitDataMask;
        private IFlagRegisterField transferComplete;
        private IValueRegisterField rxWatermark;
        private IFlagRegisterField continuingCommand;
        private IFlagRegisterField continuousTransfer;

        private IFlagRegisterField transferCompleteInterruptEnable;

        private uint sizeLeft;
        private bool continuousTransferInProgress;

        private readonly Queue<byte> outputFifo;

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
