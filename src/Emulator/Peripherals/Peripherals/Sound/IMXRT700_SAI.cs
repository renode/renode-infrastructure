//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sound
{
    public partial class IMXRT700_SAI : IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize, IDisposable
    {
        // Clock root frequency is currently hardcoded, but it should be implied by a clock tree configuration.
        public IMXRT700_SAI(IMachine machine, uint clockRootFrequencyHz = 24576000)
        {
            this.clockRootFrequencyHz = clockRootFrequencyHz;
            IRQ = new GPIO();
            DmaReceiveRequest = new GPIO();
            DmaTransmitRequest = new GPIO();

            transmitter = new Transceiver(machine, this, IRQ, DmaTransmitRequest, isTx: true);
            receiver = new Transceiver(machine, this, IRQ, DmaReceiveRequest, isTx: false);

            RegistersCollection = new DoubleWordRegisterCollection(this, BuildRegisterMap());
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            transmitter.Reset();
            receiver.Reset();
        }

        public void Dispose()
        {
            transmitter.Dispose();
            receiver.Dispose();
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset == (long)Registers.ReceiveData)
            {
                return receiver.ReadBits(32);
            }
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset == (long)Registers.TransmitData)
            {
                transmitter.WriteBits(32, value);
            }
            else
            {
                RegistersCollection.Write(offset, value);
            }
        }

        public ushort ReadWord(long offset)
        {
            if(offset == (long)Registers.ReceiveData)
            {
                return (ushort)receiver.ReadBits(16);
            }
            this.WarningLog("Trying to read register at offset 0x{0:X}, but word access is forbidden", offset);
            return 0;
        }

        public void WriteWord(long offset, ushort value)
        {
            if(offset == (long)Registers.TransmitData)
            {
                transmitter.WriteBits(16, value);
            }
            else
            {
                this.WarningLog("Trying to write 0x{0:X} to register at offset 0x{1:X}, but word access is forbidden", value, offset);
            }
        }

        public byte ReadByte(long offset)
        {
            if(offset == (long)Registers.ReceiveData)
            {
                return (byte)receiver.ReadBits(8);
            }
            this.WarningLog("Trying to read register at offset 0x{0:X}, but byte access is forbidden", offset);
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            if(offset == (long)Registers.TransmitData)
            {
                transmitter.WriteBits(8, value);
            }
            else
            {
                this.WarningLog("Trying to write 0x{0:X} to register at offset 0x{1:X}, but byte access is forbidden", value, offset);
            }
        }

        public void SetInputFile(ReadFilePath inputFile, bool littleEndian)
        {
            InputFile = inputFile;
            receiver.SetPcmFile(inputFile, littleEndian);
        }

        public void SetOutputFile(WriteFilePath outputFile, bool littleEndian)
        {
            OutputFile = outputFile;
            transmitter.SetPcmFile(outputFile, littleEndian);
        }

        public string InputFile { get; private set; }

        public string OutputFile { get; private set; }

        [DefaultInterrupt]
        public GPIO IRQ { get; }

        // These signals should be blinked to trigger DMA request.
        public GPIO DmaReceiveRequest { get; }

        public GPIO DmaTransmitRequest { get; }

        public long Size => 0x1000;

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var regs = new Dictionary<long, DoubleWordRegister>()
            {
                {(long)Registers.VersionId, new DoubleWordRegister(this, 0x03020002)
                    .WithValueField(0, 16, FieldMode.Read, name: "FEATURE")
                    .WithValueField(16, 8, FieldMode.Read, name: "MINOR")
                    .WithValueField(24, 8, FieldMode.Read, name: "MAJOR")
                },
                {(long)Registers.Parameter, new DoubleWordRegister(this, 0x00050301)
                    .WithValueField(0, 4, FieldMode.Read, name: "DATALINE")
                    .WithReservedBits(4, 4)
                    .WithValueField(8, 4, FieldMode.Read, name: "FIFO")
                    .WithReservedBits(12, 4)
                    .WithValueField(16, 4, FieldMode.Read, name: "FRAME")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.MCLKControl, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "DIV") // Reserved for this chip, but field is RW. Changes to this bit field have no effect.
                    .WithReservedBits(8, 15)
                    .WithFlag(23, name: "DIVEN") // Reserved for this chip, but field is RW. Changes to this bit field have no effect.
                    .WithValueField(24, 2, name: "MSEL") // Reserved for this chip, but field is RW. Changes to this bit field have no effect.
                    .WithReservedBits(26, 4)
                    .WithTaggedFlag("MOE", 30)
                    .WithReservedBits(31, 1)
                },
            };

            var txRegs = transmitter.GetRegisterMap();
            var rxRegs = receiver.GetRegisterMap();

            foreach(var txReg in txRegs)
            {
                regs.Add(TransmitterRegistersOffset + txReg.Key, txReg.Value);
            }

            foreach(var rxReg in rxRegs)
            {
                regs.Add(ReceiverRegistersOffset + rxReg.Key, rxReg.Value);
            }

            return regs;
        }

        private readonly uint clockRootFrequencyHz;

        private readonly Transceiver transmitter;
        private readonly Transceiver receiver;

        private const int TransmitterRegistersOffset = 0x08;
        private const int ReceiverRegistersOffset = 0x88;

        private enum Registers
        {
            VersionId = 0x00,                   // VERID
            Parameter = 0x04,                   // PARAM
            TransmitControl = 0x08,             // TCSR
            TransmitConfiguration1 = 0x0C,      // TCR1
            TransmitConfiguration2 = 0x10,      // TCR2
            TransmitConfiguration3 = 0x14,      // TCR3
            TransmitConfiguration4 = 0x18,      // TCR4
            TransmitConfiguration5 = 0x1C,      // TCR5
            TransmitData = 0x20,                // TDR0
            TransmitFifo = 0x40,                // TFR0
            TransmitMask = 0x60,                // TMR
            TransmitTimestampControl = 0x70,    // TTCR
            TransmitTimestamp = 0x74,           // TTSR
            TransmitBitCount = 0x78,            // TBCR
            TransmitBitCountTimestamp = 0x7C,   // TBCTR
            ReceiveControl = 0x88,              // RCSR
            ReceiveConfiguration1 = 0x8C,       // RCR1
            ReceiveConfiguration2 = 0x90,       // RCR2
            ReceiveConfiguration3 = 0x94,       // RCR3
            ReceiveConfiguration4 = 0x98,       // RCR4
            ReceiveConfiguration5 = 0x9C,       // RCR5
            ReceiveData = 0xA0,                 // RDR0
            ReceiveFifo = 0xC0,                 // RFR0
            ReceiveMask = 0xE0,                 // RMR
            ReceiveTimestampControl = 0xF0,     // RTCR
            ReceiveTimestamp = 0xF4,            // RTSR
            ReceiveBitCount = 0xF8,             // RBCR
            ReceiveBitCountTimestamp = 0xFC,    // RBCTR
            MCLKControl = 0x100                 // MCR
        }
    }
}