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
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class SAM_SPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>, ISamPdcBytePeripheral, ISamPdcWordPeripheral, ISamPdcDoubleWordPeripheral
    {
        public SAM_SPI(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            irqManager = new InterruptManager<Interrupts>(this, IRQ, nameof(IRQ));
            transmitBuffer = new Queue<byte>();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            pdc = new SAM_PDC(machine, this, (long)Registers.PdcReceivePointer, HandlePDCInterrupts);
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            SWReset();
            irqManager.Reset();
            irqManager.SetInterrupt(Interrupts.TransmissionRegistersEmpty);
            pdc.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(writeProtection && writeProtected.Contains(offset))
            {
                writeProtectionViolationSource.Value = (ulong)offset;
                writeProtectionViolationStatus.Value = true;
                this.WarningLog("Writing to Write Protected register at offset: 0x{0:X}", offset);
            }
            else
            {
                RegistersCollection.Write(offset, value);
            }
        }

        public byte? DmaByteRead() => (byte?)DmaWordRead();

        public void DmaByteWrite(byte data) => DmaDoubleWordWrite(data);

        public ushort? DmaWordRead()
        {
            if(!irqManager.IsSet(Interrupts.ReceiveDataRegisterFull))
            {
                return null;
            }
            var rval = RegistersCollection.Read((long)Registers.ReceiveData);
            return (ushort)(rval & 0xFFFF);
        }

        public void DmaWordWrite(ushort data) => DmaDoubleWordWrite(data);

        public uint? DmaDoubleWordRead() => null;

        public void DmaDoubleWordWrite(uint data)
        {
            RegistersCollection.Write((long)Registers.TransmitData, data);
            pdc.TriggerReceiver();
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }
        public GPIO IRQ { get; }
        public long Size => 0x128;

        public TransferType DmaWriteAccessWidth { get; private set; }
        public TransferType DmaReadAccessWidth { get; private set; }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        isEnabled.Value = true;
                        irqManager.SetInterrupt(Interrupts.TransmitDataRegisterEmpty);
                        if(!waitDataReadBeforeTransfer.Value || !irqManager.IsSet(Interrupts.ReceiveDataRegisterFull))
                        {
                            TryTransfer();
                        }
                    },
                    name: "SPIEN")
                .WithFlag(1, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            isEnabled.Value = false;
                            irqManager.ClearInterrupt(Interrupts.TransmitDataRegisterEmpty);
                        }
                    },
                    name: "SPIDIS")
                .WithReservedBits(2, 5)
                .WithFlag(7, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if (val)
                        {
                            SWReset();
                        }
                    },
                    name: "SWRST")
                .WithReservedBits(8, 16)
                .WithTaggedFlag("LASTXFER", 24)
                .WithReservedBits(25, 7)
            ;

            // Protected by WPEN
            Registers.Mode.Define(this)
                .WithEnumField(0, 1, out mode, name: "MSTR")
                .WithEnumField(1, 1, out peripheralSelectMode, name: "PS")
                .WithFlag(2, out useDecoder, name: "PCSDEC")
                .WithReservedBits(3, 1)
                .WithTaggedFlag("MODFDIS", 4)
                .WithFlag(5, out waitDataReadBeforeTransfer, name: "WDRBT")
                .WithReservedBits(6, 1)
                .WithFlag(7, out localLoopback, name: "LLB")
                .WithReservedBits(8, 8)
                .WithValueField(16, 4,
                    writeCallback: (_, val) => ChipSelect(val),
                    name: "PCS")
                .WithReservedBits(20, 4)
                .WithTag("DLYBCS", 24, 8)
                .WithWriteCallback((_, __) => SetDmaAccessWidth())
            ;

            Registers.ReceiveData.Define(this)
                .WithValueField(0, 16, out receiveBuffer, FieldMode.Read, name: "RD")
                .WithValueField(16, 4, FieldMode.Read,
                    valueProviderCallback: _ => mode.Value == SPIMode.Master ? rawChipSelect : 0,
                    name: "PCS")
                .WithReservedBits(20, 12)
                .WithReadCallback((_, __) =>
                {
                    irqManager.ClearInterrupt(Interrupts.ReceiveDataRegisterFull);
                    if(isEnabled.Value && mode.Value == SPIMode.Master && waitDataReadBeforeTransfer.Value)
                    {
                        TryTransfer();
                    }
                })
            ;

            Registers.TransmitData.Define(this)
                .WithValueField(0, 16, out transmitData, FieldMode.Write, name: "TD")
                .WithValueField(16, 4, out var chipSelect, FieldMode.Write, name: "PCS")
                .WithReservedBits(20, 4)
                .WithTaggedFlag("LASTXFER", 24)
                .WithReservedBits(25, 7)
                .WithWriteCallback((_, __) =>
                {
                    transmitBuffer.Clear();
                    if(peripheralSelectMode.Value == PeripheralSelectMode.Variable)
                    {
                        ChipSelect(chipSelect.Value);
                    }
                    EnqueueTx(transmitData.Value);
                    irqManager.ClearInterrupt(Interrupts.TransmissionRegistersEmpty);
                    if(!isEnabled.Value || mode.Value != SPIMode.Master)
                    {
                        return;
                    }
                    if(!(waitDataReadBeforeTransfer.Value && irqManager.IsSet(Interrupts.ReceiveDataRegisterFull)))
                    {
                        TryTransfer();
                    }
                })
            ;

            RegistersCollection.AddRegister((long)Registers.Status,
                irqManager.GetRawInterruptFlagRegister<DoubleWordRegister>()
                    .WithReservedBits(11, 5)
                    .WithFlag(16, out isEnabled, FieldMode.Read, name: "SPIENS")
                    .WithReservedBits(17, 15)
                    .WithReadCallback((_, __) =>
                    {
                        // These interrupts are cleared on read
                        irqManager.ClearInterrupt(Interrupts.ModeFaultError);
                        irqManager.ClearInterrupt(Interrupts.OverrunError);
                        irqManager.ClearInterrupt(Interrupts.NSSRising);
                        irqManager.ClearInterrupt(Interrupts.UnderrunError);
                    })
            );

            RegistersCollection.AddRegister((long)Registers.InterruptEnable,
                irqManager.GetInterruptEnableSetRegister<DoubleWordRegister>()
                    .WithReservedBits(11, 21)
            );

            RegistersCollection.AddRegister((long)Registers.InterruptDisable,
                irqManager.GetInterruptEnableClearRegister<DoubleWordRegister>()
                    .WithReservedBits(11, 21)
            );

            RegistersCollection.AddRegister((long)Registers.InterruptMask,
                irqManager.GetInterruptEnableRegister<DoubleWordRegister>()
                    .WithReservedBits(11, 21)
            );

            // Protected by WPEN
            Registers.ChipSelect0.DefineMany(this, NumberOfChipSelectRegisters, setup: (register, registerIndex) =>
                {
                    register
                        .WithTaggedFlag("CPOL", 0)
                        .WithTaggedFlag("NCPHA", 1)
                        .WithTaggedFlag("CSNAAT", 2)
                        .WithTaggedFlag("CSAAT", 3)
                        .WithValueField(4, 4,
                            writeCallback: (_, val) =>
                            {
                                txLengths[registerIndex] = val;
                                SetDmaAccessWidth();
                            },
                            valueProviderCallback: _ =>
                            {
                                return txLengths[registerIndex];
                            },
                            name: "BITS")
                        .WithTag("SCBR", 8, 8)
                        .WithTag("DLYBS", 16, 8)
                        .WithTag("DLYBCT", 24, 8);
                },
                stepInBytes: 4
            );

            Registers.WriteProtectionMode.Define(this)
                .WithFlag(0, out var enableProtection, name: "WPEN")
                .WithReservedBits(1, 7)
                .WithValueField(8, 24, out var passkey, name: "WPKEY")
                .WithWriteCallback((_, val) =>
                {
                    if(passkey.Value != WriteProtectionPasswd)
                    {
                        this.WarningLog("Wrong Write Protection Key! WPVS bit remains unchanged.");
                        return;
                    }
                    writeProtection = enableProtection.Value;
                })
            ;

            Registers.WriteProtectionStatus.Define(this)
                .WithFlag(0, out writeProtectionViolationStatus, FieldMode.ReadToClear, name: "WPVS")
                .WithReservedBits(1, 7)
                .WithValueField(8, 8, out writeProtectionViolationSource, FieldMode.Read, name: "WPVSRC")
                .WithReservedBits(16, 16)
            ;
        }

        private void HandlePDCInterrupts()
        {
            if(pdc == null)
            {
                return;
            }
            irqManager.SetInterrupt(Interrupts.EndOfReceiveBuffer, pdc.EndOfRxBuffer);
            irqManager.SetInterrupt(Interrupts.EndOfTransmitBuffer, pdc.EndOfTxBuffer);
            irqManager.SetInterrupt(Interrupts.TransmitBufferEmpty, pdc.TxBufferEmpty);
            irqManager.SetInterrupt(Interrupts.ReceiveBufferFull, pdc.RxBufferFull);
        }

        private void EnqueueTx(ulong val)
        {
            var byte0 = val & 0xFF;
            transmitBuffer.Enqueue((byte)byte0);
            if(SelectedSlaveByteMask > 0)
            {
                var byte1 = ((val & 0xFF00) >> 8) & SelectedSlaveByteMask;
                transmitBuffer.Enqueue((byte)byte1);
            }
        }

        private void TryTransfer()
        {
            if(transmitBuffer.Count == 0 || !(TryGetByAddress(selectedSlaveAddr, out var slavePeripheral) || localLoopback.Value))
            {
                return;
            }
            if(irqManager.IsSet(Interrupts.ReceiveDataRegisterFull))
            {
                irqManager.SetInterrupt(Interrupts.OverrunError);
            }
            DmaReadAccessWidth = (TransferType)transmitBuffer.Count;

            var transmit = localLoopback.Value ? (Func<byte, byte>)(b => b) : slavePeripheral.Transmit;

            var transmitted = (ulong)0;
            receiveBuffer.Value = 0;

            for(var i = 0; i < transmitBuffer.Count; ++i)
            {
                var b = transmitBuffer.Dequeue();
                transmitted |= (ulong)b << (8 * i);
                receiveBuffer.Value |= (ulong)transmit(b) << (8 * i);
            }

            if(localLoopback.Value)
            {
                this.NoisyLog("Transmitted 0x{0:X} in loopback", transmitted);
            }
            else
            {
                slavePeripheral.NoisyLog("Received 0x{0:X}, transmitted 0x{1:X}", transmitted, receiveBuffer.Value);
            }

            irqManager.SetInterrupt(Interrupts.ReceiveDataRegisterFull);
            irqManager.SetInterrupt(Interrupts.TransmissionRegistersEmpty);
        }

        private void ChipSelect(ulong val)
        {
            rawChipSelect = val;
            if(!useDecoder.Value)
            {
                // first zero from the least significant side describes the selected slave
                selectedSlaveAddr = Misc.Logarithm2((int)BitHelper.GetLeastSignificantZero(val));
            }
            else
            {
                selectedSlaveAddr = (int)val;
            }
        }

        private void SetDmaAccessWidth()
        {
            if(peripheralSelectMode.Value == PeripheralSelectMode.Variable)
            {
                DmaWriteAccessWidth = TransferType.DoubleWord;
            }
            else
            {
                DmaWriteAccessWidth = txLengths[SelectedSlaveRegisterNumber] > 0 ? TransferType.Word : TransferType.Byte;
            }
        }

        private void SWReset()
        {
            // Software reset does not affect the PDC
            isEnabled.Value = false;
            useDecoder.Value = false;
            waitDataReadBeforeTransfer.Value = false;
            writeProtection = false;
            localLoopback.Value = false;
            selectedSlaveAddr = 0;
            rawChipSelect = 0;
            transmitBuffer.Clear();
            irqManager.SetInterrupt(Interrupts.TransmissionRegistersEmpty);
            mode.Value = SPIMode.Slave;
            peripheralSelectMode.Value = PeripheralSelectMode.Fixed;
            DmaWriteAccessWidth = TransferType.Byte;
            DmaReadAccessWidth = TransferType.Byte;
            Array.Clear(txLengths, 0, txLengths.Length);
        }

        private int SelectedSlaveRegisterNumber => useDecoder.Value ? (selectedSlaveAddr / 4) : selectedSlaveAddr;

        private ulong SelectedSlaveByteMask => (ulong)((1 << (int)txLengths[SelectedSlaveRegisterNumber]) - 1);

        private bool writeProtection;
        private int selectedSlaveAddr;
        private ulong rawChipSelect;
        private ulong[] txLengths = {0, 0, 0, 0};
        private Queue<byte> transmitBuffer;
        private IValueRegisterField receiveBuffer;
        private IValueRegisterField transmitData;
        private IValueRegisterField writeProtectionViolationSource;
        private IFlagRegisterField writeProtectionViolationStatus;
        private IFlagRegisterField isEnabled;
        private IFlagRegisterField useDecoder;
        private IFlagRegisterField waitDataReadBeforeTransfer;
        private IFlagRegisterField localLoopback;
        private IEnumRegisterField<SPIMode> mode;
        private IEnumRegisterField<PeripheralSelectMode> peripheralSelectMode;
        private readonly InterruptManager<Interrupts> irqManager;
        private readonly SAM_PDC pdc;
        private readonly List<long> writeProtected = new List<long>
        {
            (long)Registers.Mode,
            (long)Registers.ChipSelect0,
            (long)Registers.ChipSelect1,
            (long)Registers.ChipSelect2,
            (long)Registers.ChipSelect3,
        };
        private const int NumberOfChipSelectRegisters = 4;
        private const int WriteProtectionPasswd = 0x535049; // ASCII: "SPI"

        public enum Registers
        {
            Control                 = 0x00,
            Mode                    = 0x04,
            ReceiveData             = 0x08,
            TransmitData            = 0x0C,
            Status                  = 0x10,
            InterruptEnable         = 0x14,
            InterruptDisable        = 0x18,
            InterruptMask           = 0x1C,
            ChipSelect0             = 0x30,
            ChipSelect1             = 0x34,
            ChipSelect2             = 0x38,
            ChipSelect3             = 0x3C,
            WriteProtectionMode     = 0xE4,
            WriteProtectionStatus   = 0xE8,
            // PDC
            PdcReceivePointer       = 0x100,
            PdcReceiveCounter       = 0x104,
            PdcTransmitPointer      = 0x108,
            PdcTransmitCounter      = 0x10C,
            PdcReceiveNextPointer   = 0x110,
            PdcReceiveNextCounter   = 0x114,
            PdcTransmitNextPointer  = 0x118,
            PdcTransmitNextCounter  = 0x11C,
            PdcTransferControl      = 0x120,
            PdcTransferStatus       = 0x124,
        }

        private enum SPIMode : ulong
        {
            Slave = 0x0,
            Master = 0x1,
        }

        private enum PeripheralSelectMode : ulong
        {
            Fixed = 0x0,
            Variable = 0x1,
        }

        private enum Interrupts
        {
            ReceiveDataRegisterFull = 0,
            TransmitDataRegisterEmpty = 1,
            ModeFaultError = 2,
            OverrunError = 3,
            EndOfReceiveBuffer = 4,
            EndOfTransmitBuffer = 5,
            ReceiveBufferFull = 6,
            TransmitBufferEmpty = 7,
            NSSRising = 8,
            TransmissionRegistersEmpty = 9,
            UnderrunError = 10,
        }
    }
}

