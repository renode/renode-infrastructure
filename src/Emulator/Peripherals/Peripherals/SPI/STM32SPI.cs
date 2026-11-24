//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.SPI
{
    public sealed class STM32SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IWordPeripheral, IDoubleWordPeripheral, IBytePeripheral, IKnownSize, ISPIController
    {
        public STM32SPI(IMachine machine, STM32Series series, int bufferCapacity = DefaultBufferCapacity) : base(machine)
        {
            // SPI v1.3.x adds FIFO data packing and CR2.NSSP/DS/FRXTH, and replaces CR1.DFF with CR1.CRCL.
            // See ST AN5543, Table 1, and the SPI control registers in RM0091 (F0), RM0385 (F7), RM0444 (G0), and RM0438 (L5).
            var usesFifoRegisterLayout = series switch
            {
                STM32Series.F0 or STM32Series.F7 or STM32Series.G0 or STM32Series.L5 => true,
                STM32Series.F1 or STM32Series.F4 or STM32Series.L0 or STM32Series.L1 => false,
                _ => throw new ConstructionException($"Unsupported STM32 series value: {series}!"),
            };
            // The same peripheral revision also adds packed DR read/write support.
            hasDataPacking = usesFifoRegisterLayout;

            receiveBuffer = new CircularBuffer<byte>(bufferCapacity);
            IRQ = new GPIO();
            DMAReceive = new GPIO();
            DMASend = new GPIO();
            registers = new DoubleWordRegisterCollection(this);
            SetupRegisters(usesFifoRegisterLayout);
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
            if(offset == (long)Registers.Data)
            {
                // DR only forwards the value to the transmitter.
                HandleDataWrite(value);
            }
            else if(offset % 4 == 0)
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
            return IsPackedDataAccess(offset) ? (ushort)HandleDataRead(packed: true) : (ushort)ReadDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            if(IsPackedDataAccess(offset))
            {
                HandleDataWrite(value, packed: true);
            }
            else
            {
                WriteDoubleWord(offset, value);
            }
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
            DMAReceive.Unset();
            DMASend.Unset();
            lock(receiveBuffer)
            {
                receiveBuffer.Clear();
            }
            registers.Reset();
            dataSize = 8;
            frameMask = byte.MaxValue;
        }

        public long Size
        {
            get
            {
                return 0x400;
            }
        }

        public GPIO IRQ { get; }

        public GPIO DMAReceive { get; }

        public GPIO DMASend { get; }

        public event Action<(byte pico, byte poci), IPeripheral> BytesExchanged;

        // A halfword access to DR transfers two frames, low byte first, when each frame fits in a byte.
        // See "Data packing" in RM0091 (F0), RM0385 (F7), RM0444 (G0), and RM0438 (L5).
        private bool IsPackedDataAccess(long offset)
        {
            return offset == (long)Registers.Data && hasDataPacking && dataSize <= 8;
        }

        private uint HandleDataRead(bool packed = false)
        {
            IRQ.Unset();
            lock(receiveBuffer)
            {
                if(receiveBuffer.TryDequeue(out var value))
                {
                    uint result = value;
                    if(packed && receiveBuffer.TryDequeue(out var high))
                    {
                        result |= (uint)high << 8;
                    }
                    Update();
                    return result;
                }
                // We don't warn when the data register is read while it's empty because the HAL
                // (for example L0, F4) does this intentionally.
                // See https://github.com/STMicroelectronics/STM32CubeL0/blob/bec4e499a74de98ab60784bf2ef1912bee9c1a22/Drivers/STM32L0xx_HAL_Driver/Src/stm32l0xx_hal_spi.c#L1368-L1372
                return 0;
            }
        }

        private void HandleDataWrite(uint value, bool packed = false)
        {
            DMASend.Set(false);
            lock(receiveBuffer)
            {
                var peripheral = RegisteredPeripheral;
                if(peripheral == null)
                {
                    this.Log(LogLevel.Warning, "SPI transmission while no SPI peripheral is connected.");
                    receiveBuffer.Enqueue(0x0);
                    if(packed)
                    {
                        receiveBuffer.Enqueue(0x0);
                    }
                    return;
                }
                TransmitByte(peripheral, (byte)value);
                if(packed)
                {
                    TransmitByte(peripheral, (byte)(value >> 8));
                }
            }
            if(txDmaEnable.Value)
            {
                DMASend.Set(true);
            }
            Update();
        }

        private void TransmitByte(ISPIPeripheral peripheral, byte value)
        {
            value &= frameMask;
            var response = (byte)(peripheral.Transmit(value) & frameMask);
            BytesExchanged?.Invoke((value, response), peripheral);
            if(receiveBuffer.Count == receiveBuffer.Capacity)
            {
                if(!overrun.Value)
                {
                    this.DebugLog("Receiving response while RXFIFO is full, dropping it");
                    overrun.Value = true;
                }
            }
            else
            {
                receiveBuffer.Enqueue(response);
            }
            if(rxDmaEnable.Value && IsRxBufferNotEmpty())
            {
                // This blink is used to signal the DMA that it should perform the peripheral -> memory transaction now
                // Without this signal DMA will never move data from the receive buffer to memory
                // See STM32DMA:OnGPIO
                DMAReceive.Blink();
            }
            this.NoisyLog("Transmitted 0x{0:X}, received 0x{1:X}.", value, response);
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
            if(fifoReceptionThreshold != null && !fifoReceptionThreshold.Value)
            {
                return receiveBuffer.Count >= 2;
            }

            return receiveBuffer.Count != 0;
        }

        private void SetupRegisters(bool usesFifoRegisterLayout)
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
                    else
                    {
                        if(!masterMode.Value)
                        {
                            this.Log(LogLevel.Error, "Enabled SPI in slave mode, which is not supported.");
                        }
                        DMASend.Set(txDmaEnable.Value);
                    }
                }, name: "SpiEnable")
                .WithFlag(7, name: "LSBFIRST") // Physical
                                               // We keep these as flags to preserve written values. SSI flag is used by drivers to select/detect operation mode (Master or Slave)
                .WithFlag(8, name: "SSI") // Internal slave select
                .WithFlag(9, name: "SSM") // Software slave management
                .WithTaggedFlag("RXONLY", 10)
                .WithTaggedFlag(usesFifoRegisterLayout ? "CRCL" : "DFF", 11)
                .WithTaggedFlag("CRCNEXT", 12)
                .WithTaggedFlag("CRCEN", 13)
                .WithTaggedFlag("BIDIOE", 14)
                .WithTaggedFlag("BIDIMODE", 15);

            Registers.Control2.Define(registers, usesFifoRegisterLayout ? 0x0700u : 0u)
                .WithFlag(0, out rxDmaEnable, name: "RXDMAEN")
                .WithFlag(1, out txDmaEnable, writeCallback: (_, value) =>
                {
                    DMASend.Set(value);
                }, name: "TXDMAEN")
                .WithTaggedFlag("SSOE", 2)
                .If(usesFifoRegisterLayout)
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
                .If(usesFifoRegisterLayout)
                    .Then(reg => reg
                          .WithValueField(8, 4, valueProviderCallback: _ => dataSize - 1, writeCallback: (_, dataSizeM1) =>
                          {
                              dataSize = dataSizeM1 + 1;
                              if(dataSize < 4)
                              {
                                  // The reserved encodings read back as the reset value (8 bits).
                                  dataSize = 8;
                              }
                              else if(dataSize > 8)
                              {
                                  this.Log(LogLevel.Warning, "Data size > 8 bits not supported");
                              }
                              frameMask = (byte)((1u << (int)dataSize) - 1);
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
        private IFlagRegisterField txDmaEnable;
        private IFlagRegisterField masterMode;
        private IFlagRegisterField overrun;
        private IFlagRegisterField errorInterruptEnable;
        private IFlagRegisterField fifoReceptionThreshold;
        private ulong dataSize;
        private byte frameMask;

        private readonly DoubleWordRegisterCollection registers;

        private readonly CircularBuffer<byte> receiveBuffer;

        private readonly bool hasDataPacking;

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