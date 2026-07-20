//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Diagnostics;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class DesignWare_SPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public DesignWare_SPI(IMachine machine, uint transmitDepth, uint receiveDepth, uint idCode = 0xFFFFFFFF, uint componentVersion = 0x3332332A, int maxTransferSize = 16) : base(machine)
        {
            if(maxTransferSize != 16 && maxTransferSize != 32)
            {
                throw new ConstructionException($"Unsupported '{nameof(maxTransferSize)}' value ({maxTransferSize}), legal values: 16, 32");
            }
            if(transmitDepth >= MaxFifoDepth)
            {
                throw new ConstructionException($"Unsupported '{nameof(transmitDepth)}' value ({transmitDepth}), must be less than {MaxFifoDepth}");
            }
            if(receiveDepth >= MaxFifoDepth)
            {
                throw new ConstructionException($"Unsupported '{nameof(receiveDepth)}' value ({receiveDepth}), must be less than {MaxFifoDepth}");
            }
            transmitBuffer = new Queue<uint>();
            receiveBuffer = new Queue<uint>();

            this.transmitDepth = transmitDepth;
            this.receiveDepth = receiveDepth;
            this.idCode = idCode;
            this.componentVersion = componentVersion;
            this.maxTransferSize = maxTransferSize;
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
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

            RegistersCollection.Reset();
            UpdateInterrupt();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public bool TryDequeueFromReceiveBuffer(out uint data)
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

        public long Size => 0x400;

        public GPIO IRQ { get; } = new GPIO();

        public TransferSize FrameSize { get; private set; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.Control0.Define(this, 0x80004000U | (maxTransferSize == 16 ? 0x7U : 0x70000U))
                .If(maxTransferSize == 16)
                    .Then(reg => reg
                        .WithValueField(0, 4, out dataFrameSize, name: "DFS",
                            writeCallback: HandleDataFrameSizeChange
                        )
                    )
                    .Else(reg => reg
                        .WithReservedBits(0, 4)
                    )
                .WithTag("FRF", 4, 2)
                .WithTaggedFlag("SCPH", 6)
                .WithTaggedFlag("SCPOL", 7)
                .WithEnumField(8, 2, out transferMode, name: "TMOD")
                .WithTaggedFlag("SLV_OE", 10)
                .WithTaggedFlag("SRL", 11)
                .WithTag("CFS", 12, 4)
                .If(maxTransferSize == 32)
                    .Then(reg => reg
                        .WithValueField(16, 5, out dataFrameSize, name: "DFS_32",
                            writeCallback: HandleDataFrameSizeChange
                        )
                    )
                    .Else(reg => reg
                        .WithReservedBits(16, 5)
                    )
                .WithTag("SPI_FRF", 21, 2)
                .WithReservedBits(23, 1)
                .WithTaggedFlag("SSTE", 24)
                .WithTaggedFlag("SECONV", 25)
                .WithReservedBits(26, 5)
                .WithTaggedFlag("SPI_IS_MST", 31)
            ;

            Registers.Control1.Define(this)
                .WithValueField(0, 16, out numberOfFrames, name: "NDF")
                .WithReservedBits(16, 16)
            ;

            Registers.Enable.Define(this)
                .WithFlag(0, out enabled, name: "SSI_EN",
                    changeCallback: (_, __) =>
                    {
                        if(!enabled.Value)
                        {
                            ClearBuffers();
                        }
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.MicrowireControl.Define(this)
                .WithTaggedFlag("MWMOD", 0)
                .WithTaggedFlag("MDD", 1)
                .WithTaggedFlag("MHS", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.SlaveSelect.Define(this)
                .WithEnumField<DoubleWordRegister, SlaveSelect>(0, 3, name: "SER",
                    writeCallback: (previousVal, val) =>
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
                    }
                )
                .WithReservedBits(3, 29)
            ;

            Registers.ClockDivider.Define(this)
                .WithFlag(0, FieldMode.Read, name: "SCKDV_0") // it's always 0 to ensure that the divider is even
                .WithValueField(1, 15, name: "SCKDV_15_1")
                .WithReservedBits(16, 16)
            ;

            var transmitThresholdBits = Misc.Logarithm2(Misc.NextPowerOfTwo((int)transmitDepth));
            Registers.TransmitThreshold.Define(this)
                .WithValueField(0, transmitThresholdBits, name: "TFT",
                    writeCallback: (_, val) =>
                    {
                        if(val <= transmitDepth)
                        {
                            transmitThreshold = (uint)val;
                        }
                        else
                        {
                            this.Log(LogLevel.Warning, "Ignored setting transmit threshold to a value (0x{0:X}) greater than the fifo depth (0x{1:X})", val, transmitDepth);
                        }
                    },
                    valueProviderCallback: _ => transmitThreshold
                )
                .WithReservedBits(transmitTresholdBits, 32 - transmitTresholdBits)
            ;

            var receiveThresholdBits = Misc.Logarithm2(Misc.NextPowerOfTwo((int)receiveDepth));
            Registers.ReceiveThreshold.Define(this)
                .WithValueField(0, receiveThresholdBits, name: "RFT",
                    writeCallback: (_, val) =>
                    {
                        if(val <= receiveDepth)
                        {
                            receiveThreshold = (uint)val;
                        }
                        else
                        {
                            this.Log(LogLevel.Warning, "Ignored setting receive threshold to a value (0x{0:X}) greater than the fifo depth (0x{1:X})", val, receiveDepth);
                        }
                    },
                    valueProviderCallback: _ => receiveThreshold
                )
                .WithReservedBits(receiveThresholdBits, 32 - receiveThresholdBits)
            ;

            var transmitLeveldBits = Misc.Logarithm2(Misc.NextPowerOfTwo((int)transmitDepth + 1));
            Registers.TransmitLevel.Define(this)
                .WithValueField(0, transmitLeveldBits, FieldMode.Read, valueProviderCallback: _ => (uint)transmitBuffer.Count, name: "TXFLR")
                .WithReservedBits(transmitLeveldBits, 32 - transmitLeveldBits)
            ;

            var receiveLevelBits = Misc.Logarithm2(Misc.NextPowerOfTwo((int)receiveDepth + 1));
            Registers.ReceiveLevel.Define(this)
                .WithValueField(0, receiveLevelBits, FieldMode.Read, valueProviderCallback: _ => (uint)receiveBuffer.Count, name: "RXFLR")
                .WithReservedBits(receiveLevelBits, 32 - receiveLevelBits)
            ;

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "BUSY") // in Renode transfers are instant, so BUSY is always 'false'
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => transmitBuffer.Count < transmitDepth, name: "TFNF")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => transmitBuffer.Count == 0, name: "TFE")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => receiveBuffer.Count != 0, name: "RFNE")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => receiveBuffer.Count == receiveDepth, name: "RFF")
                .WithTag("TXE", 5, 1) // read-to-clear, available in slave mode only
                .WithTag("DCOL", 6, 1) // read-to-clear
                .WithReservedBits(7, 25)
            ;

            Registers.InterruptMask.Define(this)
                .WithFlag(0, out transmitEmptyMask, name: "TXEIM")
                .WithFlag(1, out transmitOverflowMask, name: "TXOIM")
                .WithFlag(2, out receiveUnderflowMask, name: "RXUIM")
                .WithFlag(3, out receiveOverflowMask, name: "RXFOIM")
                .WithFlag(4, out receiveFullMask, name: "RXFIM")
                .WithFlag(5, out multiMasterContentionMask, name: "MSTIM")
                .WithReservedBits(6, 26)
                .WithWriteCallback((_, __) => UpdateInterrupt())
            ;

            Registers.InterruptStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => transmitEmpty.Value && transmitEmptyMask.Value, name: "TXEIS")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => transmitOverflow.Value && transmitOverflowMask.Value, name: "TXOIS")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => receiveUnderflow.Value && receiveUnderflowMask.Value, name: "RXUIS")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => receiveOverflow.Value && receiveOverflowMask.Value, name: "RXFOIS")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => receiveFull.Value && receiveFullMask.Value, name: "RXFIS")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => multiMasterContention.Value && multiMasterContentionMask.Value, name: "MSTIS")
                .WithReservedBits(6, 26)
            ;

            Registers.InterruptRawStatus.Define(this)
                .WithFlag(0, out transmitEmpty, FieldMode.Read, name: "TXEIR")
                .WithFlag(1, out transmitOverflow, FieldMode.Read, name: "TXOIR")
                .WithFlag(2, out receiveUnderflow, FieldMode.Read, name: "RXUIR")
                .WithFlag(3, out receiveOverflow, FieldMode.Read, name: "RXOIR")
                .WithFlag(4, out receiveFull, FieldMode.Read, name: "RXFIR")
                .WithFlag(5, out multiMasterContention, FieldMode.Read, name: "MSTIR")
                .WithReservedBits(6, 26)
            ;

            Registers.TransmitOverflowInterruptClear.Define(this)
                .WithFlag(0, FieldMode.Read, name: "TXOICR",
                    readCallback: (_, __) =>
                    {
                        transmitOverflow.Value = false;
                        UpdateInterrupt();
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.ReceiveOverflowInterruptClear.Define(this)
                .WithFlag(0, FieldMode.Read, name: "RXOICR",
                    readCallback: (_, __) =>
                    {
                        receiveOverflow.Value = false;
                        UpdateInterrupt();
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.ReceiveUnderflowInterruptClear.Define(this)
                .WithFlag(0, FieldMode.Read, name: "RXUICR",
                    readCallback: (_, __) =>
                    {
                        receiveUnderflow.Value = false;
                        UpdateInterrupt();
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.MultiMasterContentionInterruptClear.Define(this)
                .WithFlag(0, FieldMode.Read, name: "MSTICR",
                    readCallback: (_, __) =>
                    {
                        multiMasterContention.Value = false;
                        UpdateInterrupt();
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.InterruptClear.Define(this)
                .WithFlag(0, FieldMode.Read, name: "ICR",
                    readCallback: (_, __) =>
                    {
                        transmitOverflow.Value = false;
                        receiveOverflow.Value = false;
                        receiveUnderflow.Value = false;
                        multiMasterContention.Value = false;

                        UpdateInterrupt();
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.DmaControl.Define(this)
                .WithTaggedFlag("RDMAE", 0)
                .WithTaggedFlag("TDMAE", 1)
                .WithReservedBits(2, 30)
            ;

            Registers.DmaTransmitData.Define(this)
                .WithTag("DMATDL", 0, receiveThresholdBits)
                .WithReservedBits(receiveThresholdBits, 32 - receiveThresholdBits)
            ;

            Registers.DmaReceiveData.Define(this)
                .WithTag("DMARDL", 0, receiveThresholdBits)
                .WithReservedBits(receiveThresholdBits, 32 - receiveThresholdBits)
            ;

            Registers.DeviceIdentificationCode.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => idCode, name: "IDCODE")
            ;

            Registers.SynopsysComponentVersion.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => componentVersion, name: "SSI_COMP_VERSION")
            ;

            Registers.Data.DefineMany(this, NumberOfDataRegisters, (reg, i) => reg
                .WithValueField(0, maxTransferSize, name: "DR",
                    valueProviderCallback: _ =>
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

                        EnqueueToTransmitBuffer((uint)val);
                    }
                )
                .WithReservedBits(maxTransferSize, 32 - maxTransferSize)
            );

            Registers.ReceiveSampleDelay.Define(this)
                .WithTag("RSD", 0, 8)
                .WithReservedBits(8, 24)
            ;
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

        private void HandleDataFrameSizeChange(ulong previousValue, ulong newValue)
        {
            switch(newValue)
            {
            case 0x0:
            case 0x1:
            case 0x2:
                this.WarningLog("Attempted write with illegal value (0x{0:X}), ignoring", newValue);
                dataFrameSize.Value = previousValue;
                break;
            case 0x7:
            case 0xf:
            case 0x1f:
                FrameSize = (TransferSize)((newValue + 1) >> 3);
                break;
            default:
                this.Log(LogLevel.Error, "Only 8/16/32-bit transfers are supported, falling back to the default 8-bit mode");
                FrameSize = TransferSize.SingleByte;
                dataFrameSize.Value = 7;
                break;
            }
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
            default:
                throw new UnreachableException();
            }

            return true;
        }

        private void DoTransfer(ISPIPeripheral peripheral, int size, bool readFromFifo, bool writeToFifo)
        {
            this.Log(LogLevel.Noisy, "Doing an SPI transfer of size {0} bytes (reading from fifo: {1}, writing to fifo: {2})", size, readFromFifo, writeToFifo);
            for(var i = 0; i < size; i++)
            {
                var dataFromSlave = 0U;
                var dataToSlave = readFromFifo ? transmitBuffer.Dequeue() : 0U;

                var bytes = (int)FrameSize;
                var bytesToSlave = new byte[bytes];
                BitHelper.GetBytesFromValue(bytesToSlave, 0, dataToSlave, bytes);
                for(var n = bytes - 1; n >= 0; --n)
                {
                    dataFromSlave <<= 8;
                    dataFromSlave |= peripheral.Transmit(bytesToSlave[n]);
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

        private void EnqueueToTransmitBuffer(uint val)
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
        private readonly uint receiveDepth;
        private readonly uint idCode;
        private readonly uint componentVersion;
        private readonly int maxTransferSize;

        // a single frame can have up to 32-bits
        private readonly Queue<uint> receiveBuffer;
        private readonly Queue<uint> transmitBuffer;
        private readonly object innerLock = new object();

        private const int NumberOfDataRegisters = 36;
        private const int MaxFifoDepth = (1 << 16) - 1;

        public enum TransferSize
        {
            SingleByte = 1,
            DoubleByte = 2,
            QuadByte = 4,
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
            MicrowireControl = 0xC,
            SlaveSelect = 0x10,
            ClockDivider = 0x14,
            TransmitThreshold = 0x18,
            ReceiveThreshold = 0x1C,
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
            DmaControl = 0x4C,
            DmaTransmitData = 0x50,
            DmaReceiveData = 0x54,
            DeviceIdentificationCode = 0x58,
            SynopsysComponentVersion = 0x5C,
            Data = 0x60,
            Data1 = 0x64,
            Data2 = 0x68,
            Data3 = 0x6C,
            Data4 = 0x70,
            Data5 = 0x74,
            Data6 = 0x78,
            Data7 = 0x7C,
            Data8 = 0x80,
            Data9 = 0x84,
            Data10 = 0x88,
            Data11 = 0x8C,
            Data12 = 0x90,
            Data13 = 0x94,
            Data14 = 0x98,
            Data15 = 0x9C,
            Data16 = 0xA0,
            Data17 = 0xA4,
            Data18 = 0xA8,
            Data19 = 0xAC,
            Data20 = 0xB0,
            Data21 = 0xB4,
            Data22 = 0xB8,
            Data23 = 0xBC,
            Data24 = 0xC0,
            Data25 = 0xC4,
            Data26 = 0xC8,
            Data27 = 0xCC,
            Data28 = 0xD0,
            Data29 = 0xD4,
            Data30 = 0xD8,
            Data31 = 0xDC,
            Data32 = 0xE0,
            Data33 = 0xE4,
            Data34 = 0xE8,
            Data35 = 0xEC,
            ReceiveSampleDelay = 0xF0,
        }
    }
}
