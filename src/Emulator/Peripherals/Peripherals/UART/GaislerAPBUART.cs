//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.UART
{
    public class GaislerAPBUART: BasicDoubleWordPeripheral, IUART, IGaislerAPB
    {
        public GaislerAPBUART(IMachine machine, uint fifoDepth = 8, uint frequency = 25000000) : base(machine)
        {
            this.fifoDepth = fifoDepth;
            this.frequency = frequency;
            DefineRegisters();
        }

        public void WriteChar(byte value)
        {
            if(!receiverEnable.Value)
            {
                this.Log(LogLevel.Warning, "Received byte 0x{0:X}, but the receiver is not enabled, dropping.", value);
                return;
            }
            receiveFifo.Enqueue(value);
            UpdateInterrupt();
        }

        public override void Reset()
        {
            base.Reset();
            receiveFifo.Clear();
        }

        public uint BaudRate
        {
            get
            {
                var divisor = scaler.Value * 8;
                return divisor == 0 ? 0 : frequency / (uint) divisor;
            }
        }

        public Bits StopBits => Bits.One;

        public Parity ParityBit => parityEnable.Value ?
                                    (paritySelect.Value == ParitySelect.Even ?
                                        Parity.Even :
                                        Parity.Odd) :
                                    Parity.None;

        public GPIO IRQ { get; } = new GPIO();

        [field: Transient]
        public event Action<byte> CharReceived;

        public uint GetVendorID() => vendorID;

        public uint GetDeviceID() => deviceID;

        public GaislerAPBPlugAndPlayRecord.SpaceType GetSpaceType() => GaislerAPBPlugAndPlayRecord.SpaceType.APBIOSpace;

        public uint GetInterruptNumber() => this.GetCpuInterruptNumber(IRQ);

        private void DefineRegisters()
        {
            Registers.Data.Define(this, name: "DATA")
                .WithValueField(0, 8, valueProviderCallback: _ =>
                    {
                        if(!receiveFifo.TryDequeue(out byte value))
                        {
                            this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                        }
                        UpdateInterrupt();
                        return value;
                    }, writeCallback: (_, value) =>
                    {
                        if(!transmitterEnable.Value)
                        {
                            this.Log(LogLevel.Warning, "Tried to transmit byte 0x{0:X}, but the transmitter is not enabled. dropping.", value);
                            return;
                        }
                        CharReceived?.Invoke((byte)value);
                        UpdateInterrupt();
                    }, name: "DATA"
                )
                .WithReservedBits(8, 24)
            ;

            Registers.Status.Define(this, 0x86, name: "STATUS")
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => receiveFifo.Count > 0, name: "DR")
                .WithTaggedFlag("TS", 1)
                .WithTaggedFlag("TE", 2)
                .WithTaggedFlag("BR", 3)
                .WithTaggedFlag("OV", 4)
                .WithTaggedFlag("PE", 5)
                .WithTaggedFlag("FE", 6)
                .WithTaggedFlag("TH", 7)
                .WithTaggedFlag("RH", 8)
                .WithTaggedFlag("TF", 9)
                .WithTaggedFlag("RF", 10)
                .WithReservedBits(11, 9)
                .WithTag("TCNT", 20, 6)
                .WithValueField(26, 6, FieldMode.Read, valueProviderCallback: _ => (ulong) Math.Min(receiveFifo.Count, fifoDepth), name: "RCNT")
            ;

            Registers.Control.Define(this, name: "CONTROL")
                .WithFlag(0, out receiverEnable, name: "RE")
                .WithFlag(1, out transmitterEnable, name: "TE")
                .WithFlag(2, out receiverInterruptEnable, name: "RI", softResettable: false)
                .WithFlag(3, out transmitterInterruptEnable, name: "TI", softResettable: false)
                .WithEnumField(4, 1, out paritySelect, name: "PS", softResettable: false)
                .WithFlag(5, out parityEnable, name: "PE", softResettable: false)
                .WithTaggedFlag("FL", 6)
                .WithFlag(7, name: "LB", softResettable: false)
                .WithFlag(8, name: "EC", valueProviderCallback: _ => false, writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                this.Log(LogLevel.Error, "Attempted to set peripheral into external clock mode, which is unsupported!");
                            }
                        })
                .WithFlag(9,  out transmitterFifoInterruptEnable, name: "TF", softResettable: false)
                .WithFlag(10, out receiverFifoInterruptEnable, name: "RF", softResettable: false)
                .WithFlag(11, name: "DB", softResettable: false)
                .WithFlag(12, name: "BI", softResettable: false)
                .WithFlag(13, name: "DI", softResettable: false)
                .WithFlag(14, out transmitterShiftRegisterEmptyInterruptEnable, name: "SI", softResettable: false)
                .WithReservedBits(15, 16)
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => true, name: "FA")
                .WithChangeCallback((_, __) => UpdateInterrupt())
            ;

            Registers.Scaler.Define(this, name: "SCALER")
                .WithValueField(0, 12, out scaler, name: "SCALER")
                .WithReservedBits(12, 20)
            ;

            Registers.FifoDebug.Define(this, name: "FIFO_DEBUG")
                .WithTag("SOFT_RESET", 0, 8)
                .WithReservedBits(8, 24)
            ;
        }

        private void UpdateInterrupt()
        {
            if(receiveFifo.Count > 0 && receiverInterruptEnable.Value || transmitterInterruptEnable.Value)
            {
                IRQ.Blink();
            }
            else
            {
                IRQ.Set(transmitterFifoInterruptEnable.Value && transmitterEnable.Value);
            }
        }

        private IFlagRegisterField transmitterEnable;
        private IFlagRegisterField receiverEnable;
        private IFlagRegisterField transmitterFifoInterruptEnable;
        private IFlagRegisterField receiverFifoInterruptEnable;
        private IFlagRegisterField transmitterShiftRegisterEmptyInterruptEnable;
        private IFlagRegisterField transmitterInterruptEnable;
        private IFlagRegisterField receiverInterruptEnable;
        private IValueRegisterField scaler;
        private IFlagRegisterField parityEnable;
        private IEnumRegisterField<ParitySelect> paritySelect;

        private readonly uint fifoDepth;
        private readonly uint frequency;

        private readonly Queue<byte> receiveFifo = new Queue<byte>();

        private const uint vendorID = 0x01; // Aeroflex Gaisler
        private const uint deviceID = 0x0c; // GRLIB APBUART

        private enum ParitySelect
        {
            Even = 0,
            Odd = 1
        }

        private enum Registers : long
        {
            Data = 0x00,
            Status = 0x04,
            Control = 0x08,
            Scaler = 0x0c,
            FifoDebug = 0x10
        }
    }
}
