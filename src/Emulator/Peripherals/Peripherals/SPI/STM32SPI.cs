//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.SPI
{
    public sealed class STM32SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IWordPeripheral, IDoubleWordPeripheral, IBytePeripheral, IKnownSize
    {
        public STM32SPI(IMachine machine, STM32Series series, int bufferCapacity = DefaultBufferCapacity) : base(machine)
        {
            var supportedSeries = new List<STM32Series>{
                STM32Series.F0,
                STM32Series.F4,
                STM32Series.F7,
                STM32Series.G0,
                STM32Series.L0,
                STM32Series.L1,
                STM32Series.L5,
            };
            if(!supportedSeries.Contains(series))
            {
                throw new ConstructionException($"Unsupported STM32 series value: {series}!");
            }
            this.series = series;

            receiveBuffer = new CircularBuffer<byte>(bufferCapacity);
            IRQ = new GPIO();
            DMARecieve = new GPIO();
            registers = new DoubleWordRegisterCollection(this);
            SetupRegisters();
            Reset();
        }

        // We can't use AllowedTranslations because then WriteByte/WriteWord will trigger
        // an additional read (see ReadWriteExtensions:WriteByteUsingDoubleWord).
        // We can't have this happen for the data register.
        public byte ReadByte(long offset)
        {
            // byte interface is there for DMA
            if(offset % 4 == 0)
            {
                return (byte)ReadDoubleWord(offset);
            }
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            if(offset % 4 == 0)
            {
                WriteDoubleWord(offset, (uint)value);
            }
            else
            {
                this.LogUnhandledWrite(offset, value);
            }
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)ReadDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteDoubleWord(offset, (uint)value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            IRQ.Unset();
            DMARecieve.Unset();
            lock(receiveBuffer)
            {
                receiveBuffer.Clear();
            }
            registers.Reset();
        }

        public long Size
        {
            get
            {
                return 0x400;
            }
        }

        public GPIO IRQ { get; }

        public GPIO DMARecieve { get; }

        private uint HandleDataRead()
        {
            IRQ.Unset();
            lock(receiveBuffer)
            {
                if(receiveBuffer.TryDequeue(out var value))
                {
                    Update();
                    return value;
                }
                // We don't warn when the data register is read while it's empty because the HAL
                // (for example L0, F4) does this intentionally.
                // See https://github.com/STMicroelectronics/STM32CubeL0/blob/bec4e499a74de98ab60784bf2ef1912bee9c1a22/Drivers/STM32L0xx_HAL_Driver/Src/stm32l0xx_hal_spi.c#L1368-L1372
                return 0;
            }
        }

        private void HandleDataWrite(uint value)
        {
            IRQ.Unset();
            lock(receiveBuffer)
            {
                var peripheral = RegisteredPeripheral;
                if(peripheral == null)
                {
                    this.Log(LogLevel.Warning, "SPI transmission while no SPI peripheral is connected.");
                    receiveBuffer.Enqueue(0x0);
                    return;
                }
                var response = peripheral.Transmit((byte)value); // currently byte mode is the only one we support
                if(receiveBuffer.Count == receiveBuffer.Capacity)
                {
                    this.Log(LogLevel.Debug, "Receiving response while RXFIFO is full, dropping it");
                    overrun.Value = true;
                }
                else
                {
                    receiveBuffer.Enqueue(response);
                }
                if(rxDmaEnable.Value)
                {
                    // This blink is used to signal the DMA that it should perform the peripheral -> memory transaction now
                    // Without this signal DMA will never move data from the receive buffer to memory
                    // See STM32DMA:OnGPIO
                    DMARecieve.Blink();
                }
                this.NoisyLog("Transmitted 0x{0:X}, received 0x{1:X}.", value, response);
            }
            Update();
        }

        private void Update()
        {
            var rxBufferNotEmptyInterruptFlag = IsRxBufferNotEmpty() && rxBufferNotEmptyInterruptEnable.Value;

            // Consider only the OVR event flag as the MODF (Master Mode fault event), FRE (TI frame
            // format error) and CRCERR (CRC protocol error) are not supported by this model.
            var errorInterrupt = errorInterruptEnable.Value && overrun.Value;

            IRQ.Set(txBufferEmptyInterruptEnable.Value || rxBufferNotEmptyInterruptFlag || errorInterrupt);
        }

        private bool IsRxBufferNotEmpty()
        {
            if(series == STM32Series.L5 && !fifoReceptionThreshold.Value)
            {
                return receiveBuffer.Count >= 2;
            }

            return receiveBuffer.Count != 0;
        }

        private void SetupRegisters()
        {
            // Some fields relate to the physical layer of the SPI protocol, these are not
            // taken into account in Renode and are defined as flags or value fields without
            // a corresponding RegisterField or read/write callbacks. They are marked with
            // comments.
            Registers.Control1.Define(registers)
                .WithFlag(0, name: "CPHA") // Physical
                .WithFlag(1, name: "CPOL") // Physical
                .WithFlag(2, out masterMode, writeCallback: (old_value, value) =>
                {
                    if(!value && old_value)
                    {
                        this.Log(LogLevel.Error, "Setting slave mode which is not supported.");
                    }
                }, name: "MSTR")
                .WithValueField(3, 3, name: "Baud") // Physical
                .WithFlag(6, changeCallback: (oldValue, newValue) =>
                {
                    if(!newValue)
                    {
                        IRQ.Unset();
                    }
                    else if(!masterMode.Value)
                    {
                        this.Log(LogLevel.Error, "Enabled SPI in slave mode, which is not supported.");
                    }
                }, name: "SpiEnable")
                .WithFlag(7, name: "LSBFIRST") // Physical
                                               // We keep these as flags to preserve written values. SSI flag is used by drivers to select/detect operation mode (Master or Slave)
                .WithFlag(8, name: "SSI") // Internal slave select
                .WithFlag(9, name: "SSM") // Software slave management
                .WithTaggedFlag("RXONLY", 10)
                .WithTaggedFlag("DFF", 11)
                .WithTaggedFlag("CRCNEXT", 12)
                .WithTaggedFlag("CRCEN", 13)
                .WithTaggedFlag("BIDIOE", 14)
                .WithTaggedFlag("BIDIMODE", 15);

            Registers.Control2.Define(registers)
                .WithFlag(0, out rxDmaEnable, name: "RXDMAEN")
                .If(series == STM32Series.L5)
                    .Then(reg => reg
                            // Firmware may read/write this value. There is no special logic for TX DMA as transfers are handled by STM32LDMA model
                            .WithFlag(1, name: "TXDMAEN")
                    )
                    .Else(reg => reg
                            .WithTaggedFlag("TXDMAEN", 1)
                    )
                .WithTaggedFlag("SSOE", 2)
                .If(series == STM32Series.L5)
                    .Then(reg => reg
                          .WithFlag(3, name: "NSSP") // Physical
                    )
                    .Else(reg => reg
                          .WithReservedBits(3, 1)
                    )
                .WithTaggedFlag("FRF", 4)
                .WithFlag(5, out errorInterruptEnable, name: "ERRIE")
                .WithFlag(6, out rxBufferNotEmptyInterruptEnable, name: "RXNEIE")
                .WithFlag(7, out txBufferEmptyInterruptEnable, name: "TXEIE")
                .If(series == STM32Series.L5)
                    .Then(reg => reg
                          .WithValueField(8, 4, writeCallback: (_, dataSizeBits) =>
                          {
                              if(dataSizeBits > 0b111)
                              {
                                  this.Log(LogLevel.Warning, "Data size > 8 bits not supported");
                              }
                          }, name: "DS")
                          .WithFlag(12, out fifoReceptionThreshold, name: "FRXTH")
                    )
                    .Else(reg => reg
                          .WithReservedBits(8, 4)
                          .WithReservedBits(12, 1)
                    )
                .WithReservedBits(13, 19)
                .WithWriteCallback((_, __) => Update());

            Registers.Status.Define(registers, 2)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => IsRxBufferNotEmpty(), name: "RXNE")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "TXE") // transfers are instant
                .WithTaggedFlag("CHSIDE", 2) // r/o
                .WithTaggedFlag("UDR", 3) // r/o
                .WithTaggedFlag("CRCERR", 4) // rc_w0
                .WithTaggedFlag("MODF", 5) // r/o
                .WithFlag(6, out overrun, FieldMode.Read, readCallback: (_, __) =>
                {
                    if(receiveBuffer.Count < receiveBuffer.Capacity)
                    {
                        overrun.Value = false;
                    }
                }, name: "OVR")
                .WithTaggedFlag("BSY", 7) // r/o
                .WithTaggedFlag("FRE", 8) // r/o
                .WithReservedBits(9, 23);

            Registers.Data.Define(registers)
                .WithValueField(0, 16, valueProviderCallback: _ => HandleDataRead(),
                    writeCallback: (_, value) => HandleDataWrite((uint)value), name: "DR")
                .WithReservedBits(16, 16);

            Registers.CRCPolynomial.Define(registers, 7)
                .WithTag("CRCPOLY", 0, 16)
                .WithReservedBits(16, 16);

            Registers.ReceivedCRC.Define(registers)
                .WithTag("RxCRC", 0, 16) // r/o
                .WithReservedBits(16, 16);

            Registers.TransmittedCRC.Define(registers)
                .WithTag("TxCRC", 0, 16) // r/o
                .WithReservedBits(16, 16);

            Registers.I2SConfiguration.Define(registers)
                .WithTaggedFlag("CHLEN", 0)
                .WithTag("DATLEN", 1, 2)
                .WithTaggedFlag("CKPOL", 3)
                .WithTag("I2SSTD", 4, 2)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("PCMSYNC", 7)
                .WithTag("I2SCFG", 8, 2)
                .WithFlag(10, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (oldValue, newValue) =>
                {
                    // write one to clear to keep this bit 0
                    if(newValue)
                    {
                        this.Log(LogLevel.Warning, "Trying to enable not supported I2S mode.");
                    }
                }, name: "I2SE")
                .WithTaggedFlag("I2SMOD", 11)
                .WithReservedBits(12, 20);

            Registers.I2SPrescaler.Define(registers, 2)
                .WithTag("I2SDIV", 0, 8)
                .WithTaggedFlag("ODD", 8)
                .WithTaggedFlag("MCKOE", 9)
                .WithReservedBits(10, 22);
        }

        private IFlagRegisterField txBufferEmptyInterruptEnable, rxBufferNotEmptyInterruptEnable, rxDmaEnable;
        private IFlagRegisterField masterMode;
        private IFlagRegisterField overrun;
        private IFlagRegisterField errorInterruptEnable;
        //STM32L5 specific flags
        private IFlagRegisterField fifoReceptionThreshold;

        private readonly DoubleWordRegisterCollection registers;

        private readonly CircularBuffer<byte> receiveBuffer;

        private readonly STM32Series series;

        private const int DefaultBufferCapacity = 4;

        private enum Registers
        {
            Control1 = 0x0, // SPI_CR1,
            Control2 = 0x4, // SPI_CR2
            Status = 0x8, // SPI_SR
            Data = 0xC, // SPI_DR
            CRCPolynomial = 0x10, // SPI_CRCPR
            ReceivedCRC = 0x14, // SPI_RXCRCR
            TransmittedCRC = 0x18, // SPI_TXCRCR
            I2SConfiguration = 0x1C, // SPI_I2SCFGR
            I2SPrescaler = 0x20, // SPI_I2SPR
        }
    }
}