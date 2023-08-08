//
// Copyright (c) 2010-2023 Antmicro
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
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class DesignWare_SPI: SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public DesignWare_SPI(IMachine machine, uint transmitDepth, uint receiveDepth) : base(machine)
        {
            transmitBuffer = new Queue<ushort>();
            receiveBuffer = new Queue<ushort>();

            this.transmitDepth = transmitDepth;

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control0, new DoubleWordRegister(this, 0x7)
                    .WithReservedBits(20, 12)
                    .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => dataFrameSize.Value, name: "DFS_32")
                    .WithTag("CFS", 12, 4)
                    .WithTaggedFlag("SRL", 11)
                    .WithTaggedFlag("SLV_OE", 10)
                    .WithEnumField<DoubleWordRegister, TransferMode>(8, 2, out transferMode, name: "TMOD")
                    .WithTaggedFlag("SCPOL", 7)
                    .WithTaggedFlag("SCPH", 6)
                    .WithTag("FRF", 4, 2)
                    .WithValueField(0, 4, out dataFrameSize, name: "DFS", writeCallback: (_, val) =>
                    {
                        if(val == 7)
                        {
                            FrameSize = TransferSize.SingleByte;
                        }
                        else if(val == 15)
                        {
                            FrameSize = TransferSize.DoubleByte;
                        }
                        else
                        {
                            this.Log(LogLevel.Error, "Only 8/16-bit transfers are supported. Falling back to the default 8-bit mode");
                            dataFrameSize.Value = 7;
                            FrameSize = TransferSize.SingleByte;
                        }
                    })
                },

                {(long)Registers.Control1, new DoubleWordRegister(this)
                    .WithReservedBits(16, 16)
                    .WithValueField(0, 16, out numberOfFrames, name: "NDF")
                },

                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithReservedBits(1, 31)
                    .WithFlag(0, out enabled, changeCallback: (_, val) =>
                    {
                        if(val == false)
                        {
                            ClearBuffers();
                        }
                    }, name: "SSI_EN")
                },

                {(long)Registers.SlaveSelect, new DoubleWordRegister(this)
                    .WithReservedBits(3, 29)
                    .WithEnumField<DoubleWordRegister, SlaveSelect>(0, 3, name: "SER", writeCallback: (previousVal, val) =>
                    {
                        if(!TryDecodeSlaveId(val, out var newId))
                        {
                            return;
                        }

                        // no slave selected
                        if(newId == 0)
                        {
                            if(!TryDecodeSlaveId(previousVal, out var oldId))
                            {
                                return;
                            }

                            if(!TryGetByAddress(oldId, out var slave))
                            {
                                this.Log(LogLevel.Warning, "Trying to de-select slave #{0} that is not connected", oldId);
                                return;
                            }

                            slave.FinishTransmission();
                        }
                        else
                        {
                            TrySendData(newId);
                        }
                    })
                },

                {(long)Registers.ClockDivider, new DoubleWordRegister(this)
                    .WithValueField(1, 16, name: "SCKDV_15_1")
                    .WithFlag(0, FieldMode.Read, name: "SCKDV_0") // it's always 0 to ensure that the divider is even
                },

                {(long)Registers.TransmitTreshold, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithValueField(0, 8, writeCallback: (_, val) =>
                    {
                        if(val <= transmitDepth)
                        {
                            transmitThreshold = (uint)val;
                        }
                        else
                        {
                            this.Log(LogLevel.Warning, "Ignored setting transmit threshold to a value (0x{0:X}) greater than the fifo depth (0x{1:X})", val, transmitDepth);
                        }
                    }, valueProviderCallback: _ => transmitThreshold, name: "TFT")
                },

                {(long)Registers.ReceiveTreshold, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithValueField(0, 8, writeCallback: (_, val) =>
                    {
                        if(val <= receiveDepth)
                        {
                            receiveThreshold = (uint)val;
                        }
                        else
                        {
                            this.Log(LogLevel.Warning, "Ignored setting receive threshold to a value (0x{0:X}) greater than the fifo depth (0x{1:X})", val, receiveDepth);
                        }
                    }, valueProviderCallback: _ => receiveThreshold, name: "RFT")
                },

                {(long)Registers.TransmitLevel, new DoubleWordRegister(this)
                    .WithReservedBits(3, 29)
                    .WithValueField(0, 3, FieldMode.Read, valueProviderCallback: _ => (uint)transmitBuffer.Count, name: "TXFLR")
                },

                {(long)Registers.ReceiveLevel, new DoubleWordRegister(this)
                    .WithReservedBits(3, 29)
                    .WithValueField(0, 3, FieldMode.Read, valueProviderCallback: _ => (uint)receiveBuffer.Count, name: "RXFLR")
                },

                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithReservedBits(7, 25)
                    .WithTag("DCOL", 6, 1) // read-to-clear
                    .WithTag("TXE", 5, 1) // read-to-clear, available in slave mode only
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => receiveBuffer.Count == receiveDepth, name: "RFF")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => receiveBuffer.Count != 0, name: "RFNE")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => transmitBuffer.Count == 0, name: "TFE")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => transmitBuffer.Count < transmitDepth, name: "TFNF")
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "BUSY") // in Renode transfers are instant, so BUSY is always 'false'
                },

                {(long)Registers.InterruptMask, new DoubleWordRegister(this)
                    .WithReservedBits(6, 26)
                    .WithFlag(5, out multiMasterContentionMask, name: "MSTIM")
                    .WithFlag(4, out receiveFullMask, name: "RXFIM")
                    .WithFlag(3, out receiveOverflowMask, name: "RXFOIM")
                    .WithFlag(2, out receiveUnderflowMask, name: "RXUIM")
                    .WithFlag(1, out transmitOverflowMask, name: "TXOIM")
                    .WithFlag(0, out transmitEmptyMask, name: "TXEIM")
                    .WithWriteCallback((_, __) => UpdateInterrupt())
                },

                {(long)Registers.InterruptStatus, new DoubleWordRegister(this)
                    .WithReservedBits(6, 26)
                    .WithFlag(5, mode: FieldMode.Read, valueProviderCallback: _ => multiMasterContention.Value && multiMasterContentionMask.Value, name: "MSTIS")
                    .WithFlag(4, mode: FieldMode.Read, valueProviderCallback: _ => receiveFull.Value && receiveFullMask.Value, name: "RXFIS")
                    .WithFlag(3, mode: FieldMode.Read, valueProviderCallback: _ => receiveOverflow.Value && receiveOverflowMask.Value, name: "RXFOIS")
                    .WithFlag(2, mode: FieldMode.Read, valueProviderCallback: _ => receiveUnderflow.Value && receiveUnderflowMask.Value, name: "RXUIS")
                    .WithFlag(1, mode: FieldMode.Read, valueProviderCallback: _ => transmitOverflow.Value && transmitOverflowMask.Value, name: "TXOIS")
                    .WithFlag(0, mode: FieldMode.Read, valueProviderCallback: _ => transmitEmpty.Value && transmitEmptyMask.Value, name: "TXEIS")
                },

                {(long)Registers.InterruptRawStatus, new DoubleWordRegister(this)
                    .WithReservedBits(6, 26)
                    .WithFlag(5, mode: FieldMode.Read, flagField: out multiMasterContention, name: "MSTIR")
                    .WithFlag(4, mode: FieldMode.Read, flagField: out receiveFull, name: "RXFIR")
                    .WithFlag(3, mode: FieldMode.Read, flagField: out receiveOverflow, name: "RXFOIR")
                    .WithFlag(2, mode: FieldMode.Read, flagField: out receiveUnderflow, name: "RXUIR")
                    .WithFlag(1, mode: FieldMode.Read, flagField: out transmitOverflow, name: "TXOIR")
                    .WithFlag(0, mode: FieldMode.Read, flagField: out transmitEmpty, name: "TXEIR")
                },

                {(long)Registers.TransmitOverflowInterruptClear, new DoubleWordRegister(this)
                    .WithReservedBits(1, 31)
                    .WithFlag(0, mode: FieldMode.Read, name: "TXOICR", readCallback: (_, __) => { transmitOverflow.Value = false; UpdateInterrupt(); })
                },

                {(long)Registers.ReceiveOverflowInterruptClear, new DoubleWordRegister(this)
                    .WithReservedBits(1, 31)
                    .WithFlag(0, mode: FieldMode.Read, name: "RXOICR", readCallback: (_, __) => { receiveOverflow.Value = false; UpdateInterrupt(); })
                },

                {(long)Registers.ReceiveUnderflowInterruptClear, new DoubleWordRegister(this)
                    .WithReservedBits(1, 31)
                    .WithFlag(0, mode: FieldMode.Read, name: "RXUICR", readCallback: (_, __) => { receiveUnderflow.Value = false; UpdateInterrupt(); })
                },

                {(long)Registers.MultiMasterContentionInterruptClear, new DoubleWordRegister(this)
                    .WithReservedBits(1, 31)
                    .WithFlag(0, mode: FieldMode.Read, name: "MSTICR", readCallback: (_, __) => { multiMasterContention.Value = false; UpdateInterrupt(); })
                },

                {(long)Registers.InterruptClear, new DoubleWordRegister(this)
                    .WithReservedBits(1, 31)
                    .WithFlag(0, mode: FieldMode.Read, readCallback: (_, __) =>
                    {
                        transmitOverflow.Value = false;
                        receiveOverflow.Value = false;
                        receiveUnderflow.Value = false;
                        multiMasterContention.Value = false;

                        UpdateInterrupt();
                    }, name: "ICR")
                },

                {(long)Registers.DeviceIdentificationCode, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0xFFFFFFFF, name: "IDCODE")
                },

                {(long)Registers.SynopsisComponentVersion, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0x3332332A, name: "SSI_COMP_VERSION")
                },

                {(long)Registers.DataRegister, new DoubleWordRegister(this)
                    .WithReservedBits(16, 16)
                    .WithValueField(0, 16, valueProviderCallback: _ =>
                    {
                        if(!enabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Trying to read value from a disabled SPI");
                            return 0;
                        }

                        if(!TryDequeueFromReceiveBuffer(out var data))
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty FIFO");
                            return 0;
                        }

                        return data;
                    },
                    writeCallback: (_, val) =>
                    {
                        if(!enabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Cannot write to SPI buffer while disabled");
                            return;
                        }

                        EnqueueToTransmitBuffer((ushort)val);
                    }, name: "DR")
                },
            };

            registersCollection = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Register(ISPIPeripheral peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            if(registrationPoint.Address < 1 || registrationPoint.Address > 3)
            {
                throw new RegistrationException("EOS S3 SPI Master supports 3 slaves at addresses 1, 2, 3");
            }

            base.Register(peripheral, registrationPoint);
        }

        public override void Reset()
        {
            FrameSize = TransferSize.SingleByte;

            transmitThreshold = 0;
            receiveThreshold = 0;

            ClearBuffers();

            registersCollection.Reset();
            UpdateInterrupt();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registersCollection.Write(offset, value);
        }

        public bool TryDequeueFromReceiveBuffer(out ushort data)
        {
            if(!receiveBuffer.TryDequeue(out data))
            {
                receiveUnderflow.Value = true;
                UpdateInterrupt();

                data = 0;
                return false;
            }

            if(receiveBuffer.Count <= receiveThreshold)
            {
                receiveFull.Value = false;
                UpdateInterrupt();
            }

            return true;
        }

        private void UpdateInterrupt()
        {
            var value = false;

            value |= multiMasterContention.Value && multiMasterContentionMask.Value;
            value |= receiveFull.Value && receiveFullMask.Value;
            value |= receiveOverflow.Value && receiveOverflowMask.Value;
            value |= receiveUnderflow.Value && receiveUnderflowMask.Value;
            value |= transmitOverflow.Value && transmitOverflowMask.Value;
            value |= transmitEmpty.Value && transmitEmptyMask.Value;

            this.Log(LogLevel.Noisy, "Setting IRQ to {0}", value);
            IRQ.Set(value);
        }

        private bool TrySendData(int slaveAddress)
        {
            if(!enabled.Value)
            {
                this.Log(LogLevel.Warning, "Cannot transmit data while SPI is disabled");
                return false;
            }

            if(!this.TryGetByAddress(slaveAddress, out var peripheral))
            {
                this.Log(LogLevel.Warning, "Trying to send data to a not attached slave #{0}", slaveAddress);
                return false;
            }

            this.Log(LogLevel.Noisy, "Transmit mode {0} selected", transferMode.Value);

            if(transmitBuffer.Count == 0 && transferMode.Value != TransferMode.Receive)
            {
                this.Log(LogLevel.Warning, "No data to transmit");
                return false;
            }

            var bytesFromFrames = ((int)numberOfFrames.Value + 1) * (FrameSize == TransferSize.SingleByte ? 1 : 2 /* TransferSize.DoubleByte */);
            switch(transferMode.Value)
            {
                case TransferMode.TransmitReceive:
                    DoTransfer(peripheral, transmitBuffer.Count, readFromFifo: true, writeToFifo: true);
                    break;
                case TransferMode.Transmit:
                    DoTransfer(peripheral, transmitBuffer.Count, readFromFifo: true, writeToFifo: false);
                    break;
                case TransferMode.Receive:
                    DoTransfer(peripheral, bytesFromFrames, readFromFifo: false, writeToFifo: true);
                    break;
                case TransferMode.EEPROM:
                    // control bytes
                    DoTransfer(peripheral, transmitBuffer.Count, readFromFifo: true, writeToFifo: false);
                    // data bytes
                    DoTransfer(peripheral, bytesFromFrames, readFromFifo: false, writeToFifo: true);
                    break;
            }

            return true;
        }

        private void DoTransfer(ISPIPeripheral peripheral, int size, bool readFromFifo, bool writeToFifo)
        {
            this.Log(LogLevel.Noisy, "Doing an SPI transfer of size {0} bytes (reading from fifo: {1}, writing to fifo: {2})", size, readFromFifo, writeToFifo);
            for(var i = 0; i < size; i++)
            {
                ushort dataFromSlave = 0;
                var dataToSlave = readFromFifo ? transmitBuffer.Dequeue() : (ushort)0;
                switch(FrameSize)
                {
                    case TransferSize.SingleByte:
                        dataFromSlave = peripheral.Transmit((byte)dataToSlave);
                        break;

                    case TransferSize.DoubleByte:
                    {
                        var responseHigh = peripheral.Transmit((byte)(dataToSlave >> 8));
                        var responseLow = peripheral.Transmit((byte)dataToSlave);
                        dataFromSlave = (ushort)((responseHigh << 8) | responseLow);
                        break;
                    }

                    default:
                        throw new ArgumentException($"Unexpected transfer size {FrameSize}");
                }

                this.Log(LogLevel.Noisy, "Sent 0x{0:X}, received 0x{1:X}", dataToSlave, dataFromSlave);

                if(!writeToFifo)
                {
                    continue;
                }

                lock(innerLock)
                {
                    receiveBuffer.Enqueue(dataFromSlave);
                    if(receiveBuffer.Count > receiveThreshold)
                    {
                        receiveFull.Value = true;
                        UpdateInterrupt();
                    }
                }
            }
        }

        private void EnqueueToTransmitBuffer(ushort val)
        {
            if(transmitBuffer.Count == transmitDepth)
            {
                this.Log(LogLevel.Warning, "Trying to write to a full FIFO. Dropping the data");
                transmitOverflow.Value = true;
                UpdateInterrupt();
                return;
            }

            transmitBuffer.Enqueue(val);

            if(transmitBuffer.Count <= transmitThreshold)
            {
                transmitEmpty.Value = true;
                UpdateInterrupt();
            }
        }

        private bool TryDecodeSlaveId(SlaveSelect val, out int id)
        {
            switch(val)
            {
                case SlaveSelect.Slave1:
                    id = 1;
                    return true;
                case SlaveSelect.Slave2:
                    id = 2;
                    return true;
                case SlaveSelect.Slave3:
                    id = 3;
                    return true;
                case SlaveSelect.None:
                    id = 0;
                    return true;
                default:
                    this.Log(LogLevel.Warning, "Unexpected Slave Select (SER) value: 0x{0:X}", val);
                    id = -1;
                    return false;
            }
        }

        private void ClearBuffers()
        {
            lock(innerLock)
            {
                receiveBuffer.DequeueAll();
                transmitBuffer.DequeueAll();

                transmitEmpty.Value = true;
                receiveFull.Value = false;
                UpdateInterrupt();
            }
        }

        public long Size => 0x400;

        public GPIO IRQ { get; private set; } = new GPIO();

        public TransferSize FrameSize { get; private set; }

        private uint transmitThreshold;
        private uint receiveThreshold;

        private IValueRegisterField dataFrameSize;
        private IEnumRegisterField<TransferMode> transferMode;
        private IValueRegisterField numberOfFrames;
        private IFlagRegisterField enabled;

        private IFlagRegisterField multiMasterContentionMask;
        private IFlagRegisterField receiveFullMask;
        private IFlagRegisterField receiveOverflowMask;
        private IFlagRegisterField receiveUnderflowMask;
        private IFlagRegisterField transmitOverflowMask;
        private IFlagRegisterField transmitEmptyMask;

        private IFlagRegisterField multiMasterContention; // this IRQ is never set in the current implementation
        private IFlagRegisterField receiveFull;
        private IFlagRegisterField receiveOverflow;
        private IFlagRegisterField receiveUnderflow;
        private IFlagRegisterField transmitOverflow;
        private IFlagRegisterField transmitEmpty;

        private readonly uint transmitDepth;

        // a single frame can have up to 16-bits
        private readonly Queue<ushort> receiveBuffer;
        private readonly Queue<ushort> transmitBuffer;

        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly object innerLock = new object();

        public enum TransferSize
        {
            SingleByte = 0,
            DoubleByte = 1
        }

        private enum TransferMode
        {
            TransmitReceive = 0x0,
            Transmit = 0x1,
            Receive = 0x2,
            EEPROM = 0x3,
        }

        private enum SlaveSelect
        {
            None = 0,
            Slave1 = 1,
            Slave2 = 2,
            Slave3 = 4
        }

        private enum Registers
        {
            Control0 = 0x0,
            Control1 = 0x4,
            Enable = 0x8,
            SlaveSelect = 0x10,
            ClockDivider = 0x14,
            TransmitTreshold = 0x18,
            ReceiveTreshold = 0x1C,
            TransmitLevel = 0x20,
            ReceiveLevel = 0x24,
            Status = 0x28,
            InterruptMask = 0x2C,
            InterruptStatus = 0x30,
            InterruptRawStatus = 0x34,
            TransmitOverflowInterruptClear = 0x38,
            ReceiveOverflowInterruptClear = 0x3C,
            ReceiveUnderflowInterruptClear = 0x40,
            MultiMasterContentionInterruptClear = 0x44,
            InterruptClear = 0x48,
            DeviceIdentificationCode = 0x58,
            SynopsisComponentVersion = 0x5C,
            DataRegister = 0x60,
        }
    }
}
