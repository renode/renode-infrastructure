//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class FT9001_USART : UARTBase, IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IKnownSize
    {
        public FT9001_USART(IMachine machine) : base(machine)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            IRQ = new GPIO();
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
            base.Reset();
            RegistersCollection.Reset();
            UpdateInterrupts();
        }

        public long Size => 0x100;

        public GPIO IRQ { get; }

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        public ByteRegisterCollection RegistersCollection { get; }

        protected override void CharWritten()
        {
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            UpdateInterrupts();
        }

        private void DefineRegisters()
        {
            Registers.BaudDivisorLow.Define(this)
                .WithTag("BDL", 0, 8);
            Registers.BaudDivisorHigh.Define(this)
                .WithTag("BDH", 0, 8);

            Registers.Control2.Define(this)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("RE", 2)
                .WithFlag(3, out transmitEnabled, name: "TE")
                .WithReservedBits(4, 4);
            Registers.Control1.Define(this)
                .WithTaggedFlag("PT", 0)
                .WithTaggedFlag("PE", 1)
                .WithReservedBits(2, 3)
                .WithTaggedFlag("M", 5)
                .WithReservedBits(6, 2);

            Registers.Data.Define(this)
                .WithValueField(0, 8, name: "SCIDRL",
                        valueProviderCallback: (_) =>
                        {
                            if(!TryGetCharacter(out var character))
                            {
                                this.WarningLog("Tried to read from empty RX FIFO");
                                return 0;
                            }
                            UpdateInterrupts();
                            return character;
                        },
                        writeCallback: (_, value) =>
                        {
                            if(transmitEnabled.Value)
                            {
                                TransmitCharacter((byte)value);
                            }
                            else
                            {
                                this.WarningLog("Tried to transmit while TE is disabled");
                            }
                        });

            Registers.BaudRateFractional.Define(this)
                .WithTag("BRDF", 0, 8);

            Registers.FIFOControl.Define(this)
                .WithTag("RXFLSEL_1_8", 0, 2)
                .WithTag("TXFLSEL_1_8", 2, 2)
                .WithReservedBits(4, 2)
                .WithFlag(6, name: "TFEN ")
                .WithFlag(7, name: "RFEN");
            Registers.FIFOStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "REMPTY", valueProviderCallback: _ => Count == 0)
                .WithReservedBits(1, 1)
                .WithFlag(2, FieldMode.Read, name: "TEMPTY", valueProviderCallback: _ => true) // Transmit queue is always empty
                .WithFlag(3, FieldMode.Read, name: "TFULL", valueProviderCallback: _ => false) // Transmit queue is never full
                .WithReservedBits(4, 1)
                .WithFlag(5, FieldMode.Read, name: "RFTS", valueProviderCallback: _ => Count > 0)
                .WithFlag(6, FieldMode.Read, name: "FTC", valueProviderCallback: _ => true) // Frame transmission completes immediately
                .WithReservedBits(7, 1);

            Registers.FIFOControl2.Define(this)
                .WithFlag(0, FieldMode.Write, name: "RXFCLR",
                        writeCallback: (_, clear) => { if(clear) { ClearBuffer(); } })
                .WithTaggedFlag("TXFCLR", 1)
                .WithTaggedFlag("RXFTOE", 2)
                .WithTaggedFlag("RXFTOIE", 3)
                .WithTaggedFlag("RXORIE", 4)
                .WithFlag(5, out receiveFifoInterruptEnabled, name: "RXFIE")
                .WithReservedBits(6, 1)
                .WithFlag(7, out transmitFifoInterruptEnabled, name: "TXFIE")
                .WithChangeCallback((_, __) => UpdateInterrupts());
            Registers.FIFOStatus2.Define(this)
                 .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "FXPF")
                 .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "FXFE")
                 .WithFlag(2, FieldMode.Read | FieldMode.WriteOneToClear, name: "FXNF")
                 .WithFlag(3, FieldMode.Read | FieldMode.WriteOneToClear, name: "FXOR")
                 .WithReservedBits(4, 4);
        }

        private void UpdateInterrupts()
        {
            bool transmitPending = transmitFifoInterruptEnabled.Value;
            bool receivePending = (Count > 0) && receiveFifoInterruptEnabled.Value;
            bool anyPending = transmitPending || receivePending;
            this.DebugLog("Updating IRQ: TX={0}, RX={1} -> IRQ={2}", transmitPending, receivePending, anyPending);
            IRQ.Set(anyPending);
        }

        private IFlagRegisterField transmitEnabled;
        private IFlagRegisterField receiveFifoInterruptEnabled;
        private IFlagRegisterField transmitFifoInterruptEnabled;

        private enum Registers
        {
            BaudDivisorLow = 0x00,
            BaudDivisorHigh = 0x01,
            Control2 = 0x02,
            Control1 = 0x03,
            Data = 0x06,
            BaudRateFractional = 0x0A,
            FIFOControl = 0x0E,
            FIFOStatus = 0x11,
            FIFOControl2 = 0x13,
            FIFOStatus2 = 0x15
        }
    }
}
