//
// Copyright (c) 2010-2026 Antmicro
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

namespace Antmicro.Renode.Peripherals.I2C
{
    [AllowedTranslations(AllowedTranslation.DoubleWordToByte | AllowedTranslation.WordToByte)]
    public sealed class NXP_IMX_I2C : SimpleContainer<II2CPeripheral>, IProvidesRegisterCollection<ByteRegisterCollection>, IBytePeripheral, IKnownSize
    {
        public NXP_IMX_I2C(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            writeBuffer = new List<byte>();
            readBuffer = new Queue<byte>();

            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            selectedDevice = null;
            state = TransferState.Idle;
            writeBuffer.Clear();
            readBuffer.Clear();
            RegistersCollection.Reset();
            IRQ.Unset();
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; }

        public long Size => 0x10000;

        private void DefineRegisters()
        {
            Registers.Address.Define(this)
                .WithReservedBits(0, 1)
                .WithTag("ADR", 1, 7);

            Registers.FrequencyDivider.Define(this)
                .WithTag("IC", 0, 6)
                .WithReservedBits(6, 2);

            Registers.Control.Define(this)
                .WithReservedBits(0, 2)
                .WithFlag(2, FieldMode.Write, name: "RSTA",
                    writeCallback: (_, value) =>
                    {
                        // Repeated START: flush whatever was written so far to the slave and expect a new address byte
                        if(value && masterMode.Value)
                        {
                            BeginAddressing();
                        }
                    })
                .WithTaggedFlag("TXAK", 3)
                .WithTaggedFlag("MTX", 4)
                .WithFlag(5, out masterMode, name: "MSTA",
                    changeCallback: (_, value) =>
                    {
                        busBusy.Value = value;
                        if(value)
                        {
                            BeginAddressing();
                        }
                        else
                        {
                            FinishTransfer();
                        }
                    })
                .WithFlag(6, out interruptEnable, name: "IIEN")
                // Stored only so the driver observes the controller as enabled on read-back
                .WithFlag(7, name: "IEN");

            Registers.Status.Define(this, 0x81)
                .WithFlag(0, out receivedNoAcknowledge, FieldMode.Read, name: "RXAK")
                .WithFlag(1, out interruptFlag, FieldMode.Read | FieldMode.WriteZeroToClear, name: "IIF",
                    writeCallback: (_, _) => UpdateInterrupt())
                .WithTaggedFlag("SRW", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("IAL", 4)
                .WithFlag(5, out busBusy, FieldMode.Read, name: "IBB")
                .WithTaggedFlag("IAAS", 6)
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => true, name: "ICF");

            Registers.Data.Define(this)
                .WithValueField(0, 8, name: "DATA",
                    valueProviderCallback: _ => ReadData(),
                    writeCallback: (_, value) => WriteData((byte)value));
        }

        private void WriteData(byte value)
        {
            if(!masterMode.Value)
            {
                this.Log(LogLevel.Warning, "Data register written outside of master mode; slave mode is not supported.");
                return;
            }

            if(state == TransferState.Addressing)
            {
                var deviceAddress = value >> 1;
                var isRead = (value & 0x1) != 0;

                if(!TryGetByAddress(deviceAddress, out selectedDevice))
                {
                    this.Log(LogLevel.Debug, "No device registered at address 0x{0:X}, NAKing.", deviceAddress);
                }
                receivedNoAcknowledge.Value = selectedDevice == null;

                if(isRead)
                {
                    readBuffer.Clear();
                    // The driver issues a dummy read right after addressing to kick off the
                    // reception of the first byte; that read must not consume real data
                    state = TransferState.ReceivingFirstByte;
                }
                else
                {
                    state = TransferState.Transmitting;
                }

                TransferComplete();
                return;
            }

            writeBuffer.Add(value);
            receivedNoAcknowledge.Value = selectedDevice == null;
            TransferComplete();
        }

        private byte ReadData()
        {
            if(!masterMode.Value)
            {
                this.Log(LogLevel.Warning, "Data register read outside of master mode; slave mode is not supported.");
                return 0;
            }

            switch(state)
            {
            case TransferState.ReceivingFirstByte:
                state = TransferState.Receiving;
                TransferComplete();
                return 0;

            case TransferState.Receiving:
                if(readBuffer.Count == 0 && selectedDevice != null)
                {
                    foreach(var b in selectedDevice.Read())
                    {
                        readBuffer.Enqueue(b);
                    }
                }
                readBuffer.TryDequeue(out var result);
                TransferComplete();
                return result;

            default:
                return 0;
            }
        }

        private void BeginAddressing()
        {
            FlushWrite();
            state = TransferState.Addressing;
        }

        private void FlushWrite()
        {
            if(writeBuffer.Count == 0)
            {
                return;
            }
            selectedDevice?.Write(writeBuffer.ToArray());
            writeBuffer.Clear();
        }

        private void FinishTransfer()
        {
            FlushWrite();
            selectedDevice?.FinishTransmission();
            state = TransferState.Idle;
        }

        private void TransferComplete()
        {
            interruptFlag.Value = true;
            UpdateInterrupt();
        }

        private void UpdateInterrupt()
        {
            IRQ.Set(interruptEnable.Value && interruptFlag.Value);
        }

        private II2CPeripheral selectedDevice;
        private TransferState state;

        private IFlagRegisterField masterMode;
        private IFlagRegisterField interruptEnable;
        private IFlagRegisterField receivedNoAcknowledge;
        private IFlagRegisterField interruptFlag;
        private IFlagRegisterField busBusy;

        private readonly List<byte> writeBuffer;
        private readonly Queue<byte> readBuffer;

        private enum TransferState
        {
            Idle,
            Addressing,
            Transmitting,
            ReceivingFirstByte,
            Receiving,
        }

        private enum Registers
        {
            Address = 0x0,           // IADR
            FrequencyDivider = 0x4,  // IFDR
            Control = 0x8,           // I2CR
            Status = 0xC,            // I2SR
            Data = 0x10,             // I2DR
        }
    }
}
