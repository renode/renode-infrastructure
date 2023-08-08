//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.SPI
{
    public sealed class Quark_SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public Quark_SPI(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            CreateRegisters();
            Reset();
        }

        public override void Reset()
        {
            registers.Reset();
            RefreshInterrupt();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size
        {
            get { return 0xFF; }
        }

        public GPIO IRQ { get; set; }

        private void CreateRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                /*
                 * Although some registers are locked if ssiEnabled is set, we do not have an abstraction to define it here.
                 */
                {(long)Registers.Control0, new DoubleWordRegister(this, 0x70000)
                    .WithTag("Frame Format FRF", 4, 2) //rw
                    .WithTag("Serial Clock Phase SCPH", 6, 1) //rwl
                    .WithTag("Serial Clock Polarity SCPOL", 7, 1) //rwl
                    .WithEnumField(8, 2, out transferMode, FieldMode.Read | FieldMode.Write, name:"Transfer Mode TMOD") //rwl
                    .WithTag("Slave Output Enable SLV_OE", 10, 1) //rwl
                    .WithTag("Shift Register Loop SRL", 11, 1) //rwl
                    .WithTag("Control Frame Size CFS", 12, 4) //rwl
                    .WithTag("Data Frame Size in 32-bit mode DFS_32", 16, 7) //rwl
                                
                },
                {(long)Registers.Control1, new DoubleWordRegister(this, 0)
                    .WithValueField(0, 16, name: "Number of Data Frames NDF")
                },
                {(long)Registers.SSIEnable, new DoubleWordRegister(this, 0)
                    .WithFlag(0, out ssiEnabled, FieldMode.Read | FieldMode.Write, writeCallback: DisableSSI, name: "SSI Enable SSIENR")
                },
                {(long)Registers.SlaveEnable, new DoubleWordRegister(this, 0)
                    .WithValueField(0, 4, changeCallback: (oldValue, newValue) => {
                            if(newValue != 1 && newValue != 0)
                            {
                                this.Log(LogLevel.Warning, "Unhandled write to slave enable.");
                            }
                        })
                },
                {(long)Registers.BaudRateSelect, new DoubleWordRegister(this, 0)
                    .WithValueField(0, 15, name: "SSI Clock Divider SCKDV")
                },
                {(long)Registers.TransmitFIFOThresholdLevel, new DoubleWordRegister(this, 0)
                    .WithValueField(0, 3, name: "Transmit FIFO Threshold TXFTLR") // it does not matter since our transmission ends immediately
                },
                {(long)Registers.ReceiveFIFOThresholdLevel, new DoubleWordRegister(this, 0)
                    .WithValueField(0, 3, out receiveFifoInterruptThreshold, name: "Receive FIFO Threshold RFT")
                    .WithValueField(3, 29, name: "Reserved") // reserved but written
                },
                {(long)Registers.TransmitFIFOLevel, new DoubleWordRegister(this, 0)
                    .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: (_) => 0, name: "Transmit FIFO Level TXTFL")
                },
                {(long)Registers.ReceiveFIFOLevel, new DoubleWordRegister(this, 0)
                    .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: (_) => (uint)receiveFifo.Count, name: "Receive FIFO Level RXFLR")
                },
                {(long)Registers.Status, new DoubleWordRegister(this, 6)
                    .WithTag("SSI Busy BUSY", 0, 1)//ro, should probably always read 0
                    .WithFlag(1, FieldMode.Read, name: "Transmit FIFO Not Full TFNF")
                    .WithFlag(2, FieldMode.Read, name: "Transmit FIFO Empty TFE") // transmit fifo is always empty
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: x => receiveFifo.Count > 0, name: "Receive FIFO Not Empty RFNE")
                    .WithTag("Receive FIFO Full RFF", 4, 1) //ro
                    .WithTag("Transmission Error TXE", 5, 1) //ro
                },
                {(long)Registers.InterruptMask, new DoubleWordRegister(this, 3)
                    .WithFlag(0, out transmitFifoEmptyInterruptEnabled, name: "Transmit FIFO Empty Interrupt Mask TXEIM")
                    .WithFlag(1, name: "Transmit FIFO Overflow Interrupt Mask TXOIM")
                    .WithFlag(2, name: "Receive FIFO Underflow Interrupt Mask RXUIM")
                    .WithFlag(3, name: "Receive FIFO Overflow Interrupt Mask RXUIM")
                    .WithFlag(4, out receiveFifoFullInterruptEnabled, name: "Receive FIFO Full Interrupt Mask RXFIM")
                    .WithWriteCallback((_, __) => RefreshInterrupt())
                },
                {(long)Registers.InterruptStatus, new DoubleWordRegister(this, 0)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: x => transmitFifoEmptyInterruptEnabled.Value, name: "Transmit FIFO Empty Interrupt Status TXEIS")
                    .WithFlag(1, FieldMode.Read, name: "Transmit FIFO Overflow Interrupt Status TXOIS")
                    .WithFlag(2, FieldMode.Read, name: "Receive FIFO Underflow Interrupt Status RXUIS")
                    .WithFlag(3, FieldMode.Read, name: "Receive FIFO Overflow Interrupt Status RXUIS")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: x => receiveFifo.Count > (int)receiveFifoInterruptThreshold.Value && receiveFifoFullInterruptEnabled.Value, name: "Receive FIFO Full Interrupt Status RXFIS")
                },
                {(long)Registers.RawInterruptStatus, new DoubleWordRegister(this, 0)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: x => true, name: "Transmit FIFO Empty Raw Interrupt Status TXEIR")
                    .WithTag("Transmit FIFO Overflow Raw Interrupt Status TXOIR", 1, 1) //ro
                    .WithTag("Receive FIFO Underflow Raw Interrupt Status RXUIR", 2, 1) //ro
                    .WithTag("Receive FIFO Overflow Raw Interrupt Status RXUIR", 3, 1) //ro
                    .WithFlag(4, out receiveFifoFullRawInterrupt, FieldMode.Read, valueProviderCallback: x => receiveFifo.Count > (int)receiveFifoInterruptThreshold.Value, name: "Receive FIFO Full Raw Interrupt Status RXFIR")
                }
            };
            var dataRegister = new DoubleWordRegister(this, 0).WithValueField(0, 16, writeCallback: (_, val) => WriteData((uint)val), valueProviderCallback: _ => ReadData());
            for(var i = 0; i < 36; i++)
            {
                //the fifo elements are not addressable, so reading/writing to any of these has the same effect -> it can be a single register.
                registersMap.Add((long)Registers.Data + i, dataRegister);
            }

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        private uint ReadData()
        {
            return receiveFifo.Count > 0 ? receiveFifo.Dequeue() : (byte)0x00;
        }

        private void WriteData(uint data)
        {
            if(!ssiEnabled.Value)
            {
                return;
            }
            if(transferMode.Value == TransferMode.ReceiveOnly || transferMode.Value == TransferMode.EEPROMRead)
            {
                // note that number of data frames (NDF field in second control register) is important in this transfer mode
                // see datasheet for details
                this.Log(LogLevel.Error, "Unhandled transfer mode {0}.", transferMode);
            }
            RefreshInterrupt(true); // although we immediately transfer the byte, the interrupt line has to be turned off for the moment
            var result = RegisteredPeripheral.Transmit((byte)data);
            if(transferMode.Value == TransferMode.TransmitAndReceive)
            {
                receiveFifo.Enqueue(result);
            }

            RefreshInterrupt();
        }

        private void DisableSSI(bool oldValue, bool newValue)
        {
            this.Log(LogLevel.Debug, "SSI {0}.", newValue ? "enabled" : "disabled");
            receiveFifo.Clear();
            RefreshInterrupt();
            if(!newValue && oldValue)
            {
                RegisteredPeripheral.FinishTransmission();
            }
        }

        private void RefreshInterrupt(bool transmitFifoEmptySuppressed = false)
        {
            var value = ssiEnabled.Value && ((!transmitFifoEmptySuppressed && transmitFifoEmptyInterruptEnabled.Value) ||
                                             (receiveFifoFullRawInterrupt.Value && receiveFifoFullInterruptEnabled.Value));
            IRQ.Set(value);
        }

        private DoubleWordRegisterCollection registers;

        private IFlagRegisterField ssiEnabled;
        private IEnumRegisterField<TransferMode> transferMode;

        private IFlagRegisterField transmitFifoEmptyInterruptEnabled;
        private IFlagRegisterField receiveFifoFullInterruptEnabled;
        private IFlagRegisterField receiveFifoFullRawInterrupt;
        private IValueRegisterField receiveFifoInterruptThreshold;

        private readonly Queue<byte> receiveFifo = new Queue<byte>();

        private enum TransferMode
        {
            TransmitAndReceive = 0x0,
            TransmitOnly = 0x1,
            ReceiveOnly = 0x2,
            EEPROMRead = 0x3
        }

        private enum Registers
        {
            Control0 = 0x0,
            Control1 = 0x4,
            SSIEnable = 0x8,
            MicrowireControl = 0xC,
            SlaveEnable = 0x10,
            BaudRateSelect = 0x14,
            TransmitFIFOThresholdLevel = 0x18,
            ReceiveFIFOThresholdLevel = 0x1C,
            TransmitFIFOLevel = 0x20,
            ReceiveFIFOLevel = 0x24,
            Status = 0x28,
            InterruptMask = 0x2C,
            InterruptStatus = 0x30,
            RawInterruptStatus = 0x34,
            TransmitFIFOOverflowInterruptClear = 0x38,
            ReceiveFIFOOverflowInterruptClear = 0x3C,
            ReceiveFIFOUnderflowInterruptClear = 0x40,
            MultiMasterInterruptClear = 0x44, //only reserved fields?
            InterruptClear = 0x48,
            DMAControl = 0x4C,
            DMATransmitDataLevel = 0x50,
            DMAReceiveDataLevel = 0x54,
            Identification = 0x58,
            CoreKitVersionID = 0x5C,
            Data = 0x60, //DR0 up to DR35 at 0xEC
            RXSampleDelay = 0xF0
        }
    }
}
