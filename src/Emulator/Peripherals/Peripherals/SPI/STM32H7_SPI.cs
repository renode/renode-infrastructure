//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class STM32H7_SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral
    {
        public STM32H7_SPI(Machine machine) : base(machine)
        {
            registers = new DoubleWordRegisterCollection(this);
            DMARecieve = new GPIO();

            transmitFifo = new Queue<uint>();
            recieveFifo = new Queue<uint>();

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            DMARecieve.Unset();
            iolockValue = false;
            transmittedPackets = 0;
            transmitFifo.Clear();
            recieveFifo.Clear();
            registers.Reset();
        }

        // We can't use AllowedTranslations because then WriteByte/WriteWord will trigger
        // an additional read (see ReadWriteExtensions:WriteByteUsingDword).
        // We can't have this happenning for the data register.
        public byte ReadByte(long offset)
        {
            return (byte)ReadDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteDoubleWord(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)ReadDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteDoubleWord(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(CanWriteToRegister((Registers)offset, value))
            {
                registers.Write(offset, value);
            }
        }

        public long Size => 0x400;

        public GPIO DMARecieve { get; }

        private void DefineRegisters()
        {
            Registers.Control1.Define(registers)
                .WithFlag(0, out peripheralEnabled, name: "SPE", changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            TryTransmitData();
                        }
                        else
                        {
                            ResetTransmissionState();
                            transmitFifo.Clear();
                            recieveFifo.Clear();
                            transmissionSize.Value = 0;
                        }
                    })
                .WithReservedBits(1, 7)
                .WithTaggedFlag("MASRX", 8)
                .WithFlag(9, out startTransmission, FieldMode.Read | FieldMode.Set, name: "CSTART", changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            endOfTransfer.Value = false;
                            TryTransmitData();
                        }
                    })
                .WithTaggedFlag("CSUSP", 10)
                .WithTaggedFlag("HDDIR", 11)
                .WithTaggedFlag("SSI", 12)
                .WithTaggedFlag("CRC33_17", 13)
                .WithTaggedFlag("RCRCINI", 14)
                .WithTaggedFlag("TCRCINI", 15)
                .WithFlag(16, name: "IOLOCK", valueProviderCallback: _ => iolockValue, changeCallback: (_, value) =>
                    {
                        if(value && !peripheralEnabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Attempted to set IOLOCK while peripheral is enabled");
                            return;
                        }

                        iolockValue = value;
                    })
                .WithReservedBits(17, 15);

            Registers.Control2.Define(registers)
                .WithValueField(0, 16, out transmissionSize, name: "TSIZE")
                .WithTag("TSER", 16, 16);

            Registers.Configuration1.Define(registers)
                .WithValueField(0, 5, out packetSizeBits, name: "DSIZE")
                .WithTag("FTHLV", 5, 4)
                .WithTag("UDRCFG", 9, 2)
                .WithTag("UDRDET", 11, 2)
                .WithReservedBits(13, 1)
                .WithFlag(14, out recieveDMAEnabled, name: "RXDMAEN")
                // Software expects this value to be as it was set. Transmitting with DMA doesn't require any special logic
                .WithFlag(15, name: "TXDMAEN")
                .WithTag("CRCSIZE", 16, 5)
                .WithReservedBits(21, 1)
                .WithTaggedFlag("CRCEN", 22)
                .WithReservedBits(23, 5)
                .WithTag("MBR", 28, 3)
                .WithReservedBits(31, 1);

            Registers.Configuration2.Define(registers)
                .WithTag("MSSI", 0, 4)
                .WithTag("MIDI", 4, 4)
                .WithReservedBits(8, 7)
                .WithTaggedFlag("IOSWP", 15)
                .WithReservedBits(16, 1)
                .WithTag("COMM", 17, 2)
                .WithTag("SP", 19, 3)
                // Only master mode is supported
                .WithFlag(22, name: "MASTER", valueProviderCallback: _ => true, writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            this.Log(LogLevel.Error, "Attempted to set peripheral into SPI slave mode. Only master mode is supported");
                        }
                    })
                .WithFlag(23, out leastSignificantByteFirst, name: "LSBFRST")
                .WithTaggedFlag("CPHA", 24)
                .WithTaggedFlag("CPOL", 25)
                .WithTaggedFlag("SSM", 26)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("SSIOP", 28)
                .WithTaggedFlag("SSOE", 29)
                .WithTaggedFlag("SSOM", 30)
                .WithTaggedFlag("AFCNTR", 31);

            Registers.Status.Define(registers)
                .WithFlag(0, FieldMode.Read, name: "RXP", valueProviderCallback: _ => recieveFifo.Count > 0)
                // We always report that there is space for additional packets
                .WithFlag(1, FieldMode.Read, name: "TXP", valueProviderCallback: _ => true)
                // This flag is equal to RXP && TXP. Since TXP is always true this flag is equal to RXP
                .WithFlag(2, FieldMode.Read, name: "DXP", valueProviderCallback: _ => recieveFifo.Count > 0)
                .WithFlag(3, out endOfTransfer, FieldMode.Read, name: "EOT")
                .WithTaggedFlag("TXTF", 4)
                // Overrun and underrun never occur in this model
                .WithTaggedFlag("UDR", 5)
                .WithTaggedFlag("OVR", 6)
                .WithTaggedFlag("CRCE", 7)
                .WithTaggedFlag("TIFRE", 8)
                .WithTaggedFlag("MODF", 9)
                .WithTaggedFlag("TSERF", 10)
                .WithTaggedFlag("SUSP", 11)
                .WithFlag(12, FieldMode.Read, name: "TXC",
                    valueProviderCallback: _ => transmissionSize.Value == 0 ? transmitFifo.Count == 0 : endOfTransfer.Value)
                .WithTag("RXPLVL", 13, 2)
                .WithTaggedFlag("RXWNE", 15)
                .WithValueField(16, 16, FieldMode.Read, name: "CTSIZE", valueProviderCallback: _ => transmissionSize.Value - transmittedPackets);

            Registers.InterruptStatusFlagsClear.Define(registers)
                .WithReservedBits(0, 3)
                .WithFlag(3, FieldMode.Write, name: "EOTC", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            ResetTransmissionState();
                        }
                    })
                .WithTaggedFlag("TXTFC", 4)
                .WithTaggedFlag("UDRC", 5)
                .WithTaggedFlag("OVRC", 6)
                .WithTaggedFlag("CRCEC", 7)
                .WithTaggedFlag("TIFREC", 8)
                .WithTaggedFlag("MODFC", 9)
                .WithTaggedFlag("TSERFC", 10)
                .WithTaggedFlag("SUSPC", 11)
                .WithReservedBits(12, 20);

            Registers.TransmitData.Define(registers)
                .WithValueField(0, 32, FieldMode.Write, name: "SPI_TXDR", writeCallback: (_, value) =>
                    {
                        transmitFifo.Enqueue((uint)value);
                        TryTransmitData();
                    });

            Registers.RecieveData.Define(registers)
                .WithValueField(0, 32, FieldMode.Read, name: "SPI_RXDR", valueProviderCallback: _ =>
                    {
                        if(!recieveFifo.TryDequeue(out var value))
                        {
                            this.Log(LogLevel.Error, "Recieve data FIFO is empty. Returning 0");
                            return 0;
                        }
                        return value;
                    });

            Registers.I2SConfiguration.Define(registers)
                .WithFlag(0, name: "I2SMOD", valueProviderCallback: _ => false, writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            this.Log(LogLevel.Error, "Attempted to enable I2S. This mode is not supported");
                        }
                    })
                .WithTag("I2SCFG[2:0]", 1, 3)
                .WithTag("I2SSTD[1:0]", 4, 2)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("PCMSYNC", 7)
                .WithTag("DATLEN[1:0]", 8, 2)
                .WithTaggedFlag("CHLEN", 10)
                .WithTaggedFlag("CKPOL", 11)
                .WithTaggedFlag("FIXCH", 12)
                .WithTaggedFlag("WSINV", 13)
                .WithTaggedFlag("DATFMT", 14)
                .WithReservedBits(15, 1)
                .WithTag("I2SDIV[7:0]", 16, 8)
                .WithTaggedFlag("ODD", 24)
                .WithTaggedFlag("MCKOE", 25)
                .WithReservedBits(26, 6);
        }

        private bool CanWriteToRegister(Registers reg, uint value)
        {
            if(peripheralEnabled.Value)
            {
                switch(reg)
                {
                    case Registers.Configuration1:
                    case Registers.Configuration2:
                    case Registers.CRCPolynomial:
                    case Registers.UnderrunData:
                        this.Log(LogLevel.Error, "Attempted to write 0x{0:X} to {0} register while peripheral is enabled", value, reg);
                        return false;
                }
            }

            return true;
        }

        private void TryTransmitData()
        {
            if(!peripheralEnabled.Value || !startTransmission.Value || transmitFifo.Count == 0)
            {
                return;
            }

            // This many bytes are needed to hold all of the packet bits (using ceiling division)
            // The value of the register is one less that the amount of required bits
            var byteCount = (int)packetSizeBits.Value / 8 + 1;
            var bytes = new byte[MaxPacketBytes];
            var reverseBytes = BitConverter.IsLittleEndian && !leastSignificantByteFirst.Value;

            while(transmitFifo.Count != 0)
            {
                var value = transmitFifo.Dequeue();
                BitHelper.GetBytesFromValue(bytes, 0, value, byteCount, reverseBytes);

                for(var i = 0; i < byteCount; i++)
                {
                    bytes[i] = RegisteredPeripheral?.Transmit(bytes[i]) ?? 0;
                }

                recieveFifo.Enqueue(BitHelper.ToUInt32(bytes, 0, byteCount, reverseBytes));

                if(recieveDMAEnabled.Value)
                {
                    // This blink is used to signal the DMA that it should perform the peripheral -> memory transaction now
                    // Without this signal DMA will never move data from the recieve FIFO to memory
                    // See STM32DMA:OnGPIO
                    DMARecieve.Blink();
                }
                transmittedPackets++;
            }

            // In case the transmission size is not specified transmission ends
            // if there are no more packets in the queue
            if(transmittedPackets == transmissionSize.Value || transmissionSize.Value == 0)
            {
                RegisteredPeripheral?.FinishTransmission();
                endOfTransfer.Value = true;
                startTransmission.Value = false;
            }
        }

        private void ResetTransmissionState()
        {
            endOfTransfer.Value = false;
            startTransmission.Value = false;
            transmittedPackets = 0;
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly Queue<uint> transmitFifo;
        private readonly Queue<uint> recieveFifo;

        private bool iolockValue;
        private IFlagRegisterField endOfTransfer;
        private IFlagRegisterField peripheralEnabled;
        private IFlagRegisterField recieveDMAEnabled;
        private IFlagRegisterField startTransmission;
        private IFlagRegisterField leastSignificantByteFirst;

        private IValueRegisterField transmissionSize;
        private IValueRegisterField packetSizeBits;

        private ulong transmittedPackets;

        private const int MaxPacketBytes = 4;

        private enum Registers
        {
            Control1 = 0x00,                    // SPI_CR1
            Control2 = 0x04,                    // SPI_CR2
            Configuration1 = 0x08,              // SPI_CFG1
            Configuration2 = 0x0C,              // SPI_CFG1
            InterruptEnable = 0x10,             // SPI_IER
            Status = 0x14,                      // SPI_SR
            InterruptStatusFlagsClear = 0x18,   // SPI_IFCR
            TransmitData = 0x20,                // SPI_TXDR
            RecieveData = 0x30,                 // SPI_RXDR
            CRCPolynomial = 0x40,               // SPI_CRCPOLY
            TransmitterCRC = 0x44,              // SPI_TXCRC
            ReceiverCRC = 0x48,                 // SPI_RXCRC
            UnderrunData = 0x4C,                // SPI_UDRDR
            I2SConfiguration = 0x50,            // SPI_I2SCFGR
        }
    }
}
