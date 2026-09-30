//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class DesignWare_SPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public DesignWare_SPI(IMachine machine, uint transmitDepth, uint receiveDepth, uint idCode = 0xFFFFFFFF, uint componentVersion = 0x3332332A, int maxTransferSize = 16, bool levelTrigger = true, int numberOfTargets = 3) : base(machine)
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
            if(numberOfTargets <= 0)
            {
                throw new ConstructionException($"'{nameof(numberOfTargets)}' has to be greater than zero");
            }
            if(numberOfTargets > 32)
            {
                throw new ConstructionException($"'{nameof(numberOfTargets)}' has to be less or equal to 32");
            }
            transmitFifo = new Queue<uint>();
            receiveFifo = new Queue<uint>();

            this.transmitDepth = transmitDepth;
            this.receiveDepth = receiveDepth;
            this.idCode = idCode;
            this.componentVersion = componentVersion;
            this.maxTransferSize = maxTransferSize;
            this.levelTrigger = levelTrigger;
            this.numberOfTargets = numberOfTargets;
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public override void Register(ISPIPeripheral peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            if(registrationPoint.Address < 1 || registrationPoint.Address > numberOfTargets)
            {
                var adresses = numberOfTargets == 1 ? "address 1" : $"addresses from 1 to {numberOfTargets}";
                var targets = numberOfTargets == 1 ? "1 slave" : $"{numberOfTargets} slaves";
                throw new RegistrationException($"{nameof(DesignWare_SPI)} Master supports {targets} at {adresses}");
            }

            base.Register(peripheral, registrationPoint);
        }

        public override void Reset()
        {
            FrameSize = TransferSize.SingleByte;
            slaveId = 0;
            target = null;
            transferStartFifoLevel = 1;

            ClearBuffers();

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

        public bool TryDequeueFromReceiveBuffer(out uint data)
        {
            if(!receiveFifo.TryDequeue(out data))
            {
                receiveUnderflow.Value = true;
                UpdateInterrupts();

                data = 0;
                return false;
            }

            UpdateInterrupts();
            return true;
        }

        public long Size => 0x400;

        public GPIO IRQ { get; } = new GPIO();

        public TransferSize FrameSize { get; private set; }

        public TransferSize MaxTransferSize => maxTransferSize == 32 ? TransferSize.QuadByte : TransferSize.DoubleByte;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private static int GetByteSize(int bits)
        {
            return Misc.RoundUpToPowerOfTwo(bits.AlignUpToMultipleOf(8) / 8);
        }

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
                .WithEnumField(8, 2, out transferMode, name: "TMOD",
                    changeCallback: (_, __) => this.NoisyLog("Transmit mode {0} selected", transferMode.Value)
                )
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
                            this.DebugLog("Finishing transmission");
                            target?.FinishTransmission();
                        }
                        else
                        {
                            AttemptDataTransfer();
                        }
                        UpdateInterrupts();
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
                .WithValueField(0, numberOfTargets, name: "SER",
                    changeCallback: (_, value) =>
                    {
                        if(TryChangeTarget(value))
                        {
                            AttemptDataTransfer();
                        }
                    }
                )
                .WithReservedBits(numberOfTargets, 32 - numberOfTargets)
            ;

            Registers.ClockDivider.Define(this)
                .WithFlag(0, FieldMode.Read, name: "SCKDV_0") // it's always 0 to ensure that the divider is even
                .WithValueField(1, 15, name: "SCKDV_15_1")
                .WithReservedBits(16, 16)
            ;

            var transmitThresholdBits = Misc.Logarithm2(Misc.RoundUpToPowerOfTwo((int)transmitDepth));
            Registers.TransmitThreshold.Define(this)
                .WithValueField(0, transmitThresholdBits, out transmitThreshold, name: "TFT",
                    changeCallback: (previousValue, value) =>
                    {
                        if(value > transmitDepth)
                        {
                            transmitThreshold.Value = previousValue;
                            this.Log(LogLevel.Warning, "Ignored setting transmit threshold to a value (0x{0:X}) greater than the fifo depth (0x{1:X})", value, transmitDepth);
                        }
                    }
                )
                .If(levelTrigger)
                    .Then(reg => reg
                        .WithReservedBits(transmitThresholdBits, 32 - transmitThresholdBits)
                    )
                    .Else(reg => reg
                        .WithReservedBits(transmitThresholdBits, 16 - transmitThresholdBits)
                        .WithValueField(16, transmitThresholdBits, name: "TXFTHR",
                            changeCallback: (previousValue, value) =>
                            {
                                if(value > transmitDepth)
                                {
                                    this.WarningLog("Ignored setting transmit threshold to a value (0x{0:X}) greater than the fifo depth (0x{1:X})", value, transmitDepth);
                                    return;
                                }

                                transferStartFifoLevel = value + 1;
                                AttemptDataTransfer();
                            }
                        )
                        .WithReservedBits(16 + transmitThresholdBits, 16 - transmitThresholdBits)
                    )
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            var receiveThresholdBits = Misc.Logarithm2(Misc.RoundUpToPowerOfTwo((int)receiveDepth));
            Registers.ReceiveThreshold.Define(this)
                .WithValueField(0, receiveThresholdBits, out receiveThreshold, name: "RFT",
                    changeCallback: (previousValue, value) =>
                    {
                        if(value > receiveDepth)
                        {
                            receiveThreshold.Value = previousValue;
                            this.Log(LogLevel.Warning, "Ignored setting receive threshold to a value (0x{0:X}) greater than the fifo depth (0x{1:X})", value, receiveDepth);
                            return;
                        }
                        UpdateInterrupts();
                    }
                )
                .WithReservedBits(receiveThresholdBits, 32 - receiveThresholdBits)
            ;

            var transmitLeveldBits = Misc.Logarithm2(Misc.RoundUpToPowerOfTwo((int)transmitDepth + 1));
            Registers.TransmitLevel.Define(this)
                .WithValueField(0, transmitLeveldBits, FieldMode.Read, valueProviderCallback: _ => (uint)transmitFifo.Count, name: "TXFLR")
                .WithReservedBits(transmitLeveldBits, 32 - transmitLeveldBits)
            ;

            var receiveLevelBits = Misc.Logarithm2(Misc.RoundUpToPowerOfTwo((int)receiveDepth + 1));
            Registers.ReceiveLevel.Define(this)
                .WithValueField(0, receiveLevelBits, FieldMode.Read, valueProviderCallback: _ => (uint)receiveFifo.Count, name: "RXFLR")
                .WithReservedBits(receiveLevelBits, 32 - receiveLevelBits)
            ;

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "BUSY") // in Renode transfers are instant, so BUSY is always 'false'
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => transmitFifo.Count < transmitDepth, name: "TFNF")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => transmitFifo.Count == 0, name: "TFE")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => receiveFifo.Count != 0, name: "RFNE")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => receiveFifo.Count == receiveDepth, name: "RFF")
                .WithTag("TXE", 5, 1) // read-to-clear, available in slave mode only
                .WithTag("DCOL", 6, 1) // read-to-clear
                .WithReservedBits(7, 25)
            ;

            Registers.InterruptMask.Define(this, 0x3F)
                .WithFlag(0, out transmitEmptyMask, name: "TXEIM")
                .WithFlag(1, out transmitOverflowMask, name: "TXOIM")
                .WithFlag(2, out receiveUnderflowMask, name: "RXUIM")
                .WithFlag(3, out receiveOverrunMask, name: "RXFOIM")
                .WithFlag(4, out receiveFullMask, name: "RXFIM")
                .WithFlag(5, out multiMasterContentionMask, name: "MSTIM")
                .WithReservedBits(6, 26)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => TransmitEmpty && transmitEmptyMask.Value, name: "TXEIS")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => transmitOverflow.Value && transmitOverflowMask.Value, name: "TXOIS")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => receiveUnderflow.Value && receiveUnderflowMask.Value, name: "RXUIS")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => receiveOverrun.Value && receiveOverrunMask.Value, name: "RXFOIS")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => ReceiveFull && receiveFullMask.Value, name: "RXFIS")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => multiMasterContention.Value && multiMasterContentionMask.Value, name: "MSTIS")
                .WithReservedBits(6, 26)
            ;

            Registers.InterruptRawStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => TransmitEmpty, name: "TXEIR")
                .WithFlag(1, out transmitOverflow, FieldMode.Read, name: "TXOIR")
                .WithFlag(2, out receiveUnderflow, FieldMode.Read, name: "RXUIR")
                .WithFlag(3, out receiveOverrun, FieldMode.Read, name: "RXOIR")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => ReceiveFull, name: "RXFIR")
                .WithFlag(5, out multiMasterContention, FieldMode.Read, name: "MSTIR")
                .WithReservedBits(6, 26)
            ;

            Registers.TransmitOverflowInterruptClear.Define(this)
                .WithFlag(0, FieldMode.Read, name: "TXOICR",
                    readCallback: (_, __) =>
                    {
                        transmitOverflow.Value = false;
                        UpdateInterrupts();
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.ReceiveOverrunInterruptClear.Define(this)
                .WithFlag(0, FieldMode.Read, name: "RXOICR",
                    readCallback: (_, __) =>
                    {
                        receiveOverrun.Value = false;
                        UpdateInterrupts();
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.ReceiveUnderflowInterruptClear.Define(this)
                .WithFlag(0, FieldMode.Read, name: "RXUICR",
                    readCallback: (_, __) =>
                    {
                        receiveUnderflow.Value = false;
                        UpdateInterrupts();
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.MultiMasterContentionInterruptClear.Define(this)
                .WithFlag(0, FieldMode.Read, name: "MSTICR",
                    readCallback: (_, __) =>
                    {
                        multiMasterContention.Value = false;
                        UpdateInterrupts();
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.InterruptClear.Define(this)
                .WithFlag(0, FieldMode.Read, name: "ICR",
                    readCallback: (_, __) =>
                    {
                        transmitOverflow.Value = false;
                        receiveOverrun.Value = false;
                        receiveUnderflow.Value = false;
                        multiMasterContention.Value = false;

                        UpdateInterrupts();
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
                            this.WarningLog("Trying to read value from a disabled SPI");
                            return 0;
                        }

                        if(!TryDequeueFromReceiveBuffer(out var data))
                        {
                            this.WarningLog("Trying to read from an empty FIFO");
                            return 0;
                        }

                        return data;
                    },
                    writeCallback: (_, value) =>
                    {
                        if(!enabled.Value)
                        {
                            this.WarningLog("Cannot write to SPI buffer while disabled");
                            return;
                        }

                        EnqueueToTransmitBuffer((uint)value);
                        AttemptDataTransfer();
                    }
                )
                .WithReservedBits(maxTransferSize, 32 - maxTransferSize)
            );

            Registers.ReceiveSampleDelay.Define(this)
                .WithTag("RSD", 0, 8)
                .WithReservedBits(8, 24)
            ;
        }

        private void UpdateInterrupts()
        {
            var irqs = new (bool IsSet, string Name)[]
            {
                (TransmitEmpty && transmitEmptyMask.Value, "TXE"),
                (transmitOverflow.Value && transmitOverflowMask.Value, "TXO"),
                (receiveUnderflow.Value && receiveUnderflowMask.Value, "RXU"),
                (receiveOverrun.Value && receiveOverrunMask.Value, "RXO"),
                (ReceiveFull && receiveFullMask.Value, "RXF"),
                (multiMasterContention.Value && multiMasterContentionMask.Value, "MST")
            }.Where(x => x.IsSet).Select(x => x.Name).ToArray();

            var value = irqs.Length > 0;
            if(IRQ.IsSet != value)
            {
                this.NoisyLog("Setting IRQ to {0} {1}", value, Misc.PrettyPrintCollection(irqs));
                IRQ.Set(value);
            }
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
                this.DebugLog("Frame size set to {0}", FrameSize);
                break;
            default:
                var newSize = GetByteSize((int)newValue + 1);
                if(newSize > (int)MaxTransferSize)
                {
                    this.ErrorLog("Only 8/16{0}-bit transfers are supported, attempted to set a value ({1:X}-bit mode) greater than max transfer size, falling back to the default 8-bit mode", maxTransferSize == 32 ? "/32" : "", newValue + 1);
                    FrameSize = TransferSize.SingleByte;
                    dataFrameSize.Value = 7;
                    break;
                }

                this.ErrorLog("Only 8/16{0}-bit transfers are supported, falling back to next supported mode ({1}-bit mode)", maxTransferSize == 32 ? "/32" : "", newSize << 3);
                FrameSize = (TransferSize)newSize;
                dataFrameSize.Value = ((ulong)FrameSize << 3) - 1;
                break;
            }
        }

        private void AttemptDataTransfer()
        {
            if(!enabled.Value || slaveId == 0 || transmitFifo.Count < TransferStartFifoLevel)
            {
                return;
            }

            switch(transferMode.Value)
            {
            case TransferMode.TransmitReceive:
                DoTransfer(transmitFifo.Count, readFromFifo: true, writeToFifo: true);
                break;
            case TransferMode.Transmit:
                DoTransfer(transmitFifo.Count, readFromFifo: true, writeToFifo: false);
                break;
            case TransferMode.Receive:
                DoTransfer(NumberOfFrames, readFromFifo: false, writeToFifo: true);
                var dummy = transmitFifo.Dequeue();
                if(transmitFifo.Count > 0)
                {
                    this.WarningLog("In receive mode Rx FIFO doesn't contain a single dummy word (dummy: 0x{0:X}, FIFO: {1})", dummy, Misc.PrettyPrintCollectionHex(transmitFifo));
                }
                break;
            case TransferMode.EEPROM:
                // control bytes
                DoTransfer(transmitFifo.Count, readFromFifo: true, writeToFifo: false);
                // data bytes
                DoTransfer(NumberOfFrames, readFromFifo: false, writeToFifo: true);
                break;
            default:
                throw new UnreachableException();
            }

            UpdateInterrupts();
        }

        private void DoTransfer(int size, bool readFromFifo, bool writeToFifo)
        {
            this.NoisyLog("Doing an SPI transfer of size {0} bytes (reading from fifo: {1}, writing to fifo: {2})", size, readFromFifo, writeToFifo);

            for(var i = 0; i < size; i++)
            {
                var dataToSlave = readFromFifo ? transmitFifo.Dequeue() : 0U;
                var bytesToSlave = BitHelper.GetBytesFromValue(dataToSlave, (int)FrameSize, reverse: true);
                var receivedBytes = bytesToSlave.Select(byteToSlave => target?.Transmit(byteToSlave) ?? 0x0).ToArray();
                var dataFromSlave = BitHelper.ToUInt32(receivedBytes, 0, (int)FrameSize, reverse: true);

                this.NoisyLog("Sent 0x{0:X}, received 0x{1:X}", dataToSlave, dataFromSlave);

                if(writeToFifo)
                {
                    if(receiveFifo.Count < receiveDepth)
                    {
                        receiveFifo.Enqueue(dataFromSlave);
                    }
                    else
                    {
                        receiveOverrun.Value = true;
                    }
                }
            }
        }

        private void EnqueueToTransmitBuffer(uint val)
        {
            if(transmitFifo.Count == transmitDepth)
            {
                this.WarningLog("Trying to write to a full FIFO. Dropping the data");
                transmitOverflow.Value = true;
                UpdateInterrupts();
                return;
            }

            transmitFifo.Enqueue(val);
            UpdateInterrupts();
        }

        private bool TryChangeTarget(ulong slaveSelect)
        {
            this.DebugLog("Finishing transmission");
            target?.FinishTransmission();
            target = null;

            if(slaveSelect == 0)
            {
                return false;
            }

            if(!Misc.IsPowerOfTwo(slaveSelect))
            {
                this.WarningLog("Unexpected Slave Select (SER) value: 0x{0:X}", slaveSelect);
                slaveId = 0;
                return false;
            }

            slaveId = BitHelper.GetMostSignificantSetBitIndex(slaveSelect) + 1;

            if(!this.TryGetByAddress(slaveId, out target))
            {
                target = null;
                this.WarningLog("Trying to send data to a not attached slave #{0}", slaveId);
            }

            return true;
        }

        private void ClearBuffers()
        {
            receiveFifo.DequeueAll();
            transmitFifo.DequeueAll();
            UpdateInterrupts();
        }

        private bool TransmitEmpty => enabled.Value && transmitFifo.Count <= (int)transmitThreshold.Value;

        private bool ReceiveFull => enabled.Value && receiveFifo.Count > (int)receiveThreshold.Value;

        private int NumberOfFrames => (int)numberOfFrames.Value + 1;

        private int TransferStartFifoLevel => transferMode.Value != TransferMode.Receive ? (int)transferStartFifoLevel : 1;

        private int slaveId;
        private ISPIPeripheral target;
        private ulong transferStartFifoLevel;

        private IValueRegisterField dataFrameSize;
        private IEnumRegisterField<TransferMode> transferMode;
        private IValueRegisterField numberOfFrames;
        private IFlagRegisterField enabled;
        private IValueRegisterField transmitThreshold;
        private IValueRegisterField receiveThreshold;

        private IFlagRegisterField multiMasterContentionMask;
        private IFlagRegisterField receiveFullMask;
        private IFlagRegisterField receiveOverrunMask;
        private IFlagRegisterField receiveUnderflowMask;
        private IFlagRegisterField transmitOverflowMask;
        private IFlagRegisterField transmitEmptyMask;

        private IFlagRegisterField multiMasterContention; // this IRQ is never set in the current implementation
        private IFlagRegisterField receiveOverrun;
        private IFlagRegisterField receiveUnderflow;
        private IFlagRegisterField transmitOverflow;

        private readonly uint transmitDepth;
        private readonly uint receiveDepth;
        private readonly uint idCode;
        private readonly uint componentVersion;
        private readonly int maxTransferSize;
        private readonly bool levelTrigger;
        private readonly int numberOfTargets;

        // a single frame can have up to 32-bits
        private readonly Queue<uint> receiveFifo;
        private readonly Queue<uint> transmitFifo;

        private const int NumberOfDataRegisters = 36;
        private const int MaxFifoDepth = (1 << 16) - 1;

        public enum TransferSize
        {
            SingleByte = 1,
            DoubleByte = 2,
            QuadByte = 4,
        }

        [RegistersDescription]
        public enum Registers
        {
            Control0                            = 0x00, // CTRLR0
            Control1                            = 0x04, // CTRLR1
            Enable                              = 0x08, // ENR
            MicrowireControl                    = 0x0C, // MWCR
            SlaveSelect                         = 0x10, // SER
            ClockDivider                        = 0x14, // BAUDR
            TransmitThreshold                   = 0x18, // TXFTLR
            ReceiveThreshold                    = 0x1C, // RXFTLR
            TransmitLevel                       = 0x20, // TXFLR
            ReceiveLevel                        = 0x24, // RXFLR
            Status                              = 0x28, // SR
            InterruptMask                       = 0x2C, // IMR
            InterruptStatus                     = 0x30, // ISR
            InterruptRawStatus                  = 0x34, // RISR
            TransmitOverflowInterruptClear      = 0x38, // TXEICR
            ReceiveOverrunInterruptClear        = 0x3C, // RXOICR
            ReceiveUnderflowInterruptClear      = 0x40, // RXUICR
            MultiMasterContentionInterruptClear = 0x44, // MSTICR
            InterruptClear                      = 0x48, // ICR
            DmaControl                          = 0x4C, // DMACR
            DmaTransmitData                     = 0x50, // DMATDLR
            DmaReceiveData                      = 0x54, // DMARDLR
            DeviceIdentificationCode            = 0x58, // IDR
            SynopsysComponentVersion            = 0x5C, // VERSION_ID
            Data                                = 0x60, // DRn
            Data1                               = 0x64,
            Data2                               = 0x68,
            Data3                               = 0x6C,
            Data4                               = 0x70,
            Data5                               = 0x74,
            Data6                               = 0x78,
            Data7                               = 0x7C,
            Data8                               = 0x80,
            Data9                               = 0x84,
            Data10                              = 0x88,
            Data11                              = 0x8C,
            Data12                              = 0x90,
            Data13                              = 0x94,
            Data14                              = 0x98,
            Data15                              = 0x9C,
            Data16                              = 0xA0,
            Data17                              = 0xA4,
            Data18                              = 0xA8,
            Data19                              = 0xAC,
            Data20                              = 0xB0,
            Data21                              = 0xB4,
            Data22                              = 0xB8,
            Data23                              = 0xBC,
            Data24                              = 0xC0,
            Data25                              = 0xC4,
            Data26                              = 0xC8,
            Data27                              = 0xCC,
            Data28                              = 0xD0,
            Data29                              = 0xD4,
            Data30                              = 0xD8,
            Data31                              = 0xDC,
            Data32                              = 0xE0,
            Data33                              = 0xE4,
            Data34                              = 0xE8,
            Data35                              = 0xEC,
            ReceiveSampleDelay                  = 0xF0, // RX_SAMPLE_DELAY
        }

        private enum TransferMode
        {
            TransmitReceive = 0x0,
            Transmit = 0x1,
            Receive = 0x2,
            EEPROM = 0x3,
        }
    }
}
