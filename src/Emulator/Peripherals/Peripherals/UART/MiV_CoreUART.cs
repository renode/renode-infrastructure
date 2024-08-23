//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.UART
{
    // This peripheral only has byte-wide registers, but we define it as a double-word peripheral and
    // translate byte->dword instead of dword->byte to avoid unhandled read warnings on dword access.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class MiV_CoreUART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public MiV_CoreUART(IMachine machine, ulong clockFrequency) : base(machine)
        {
            this.clockFrequency = clockFrequency;
            var registersMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.TransmitData, new ByteRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, b) => {
                        this.TransmitCharacter((byte)b);
                    })},

                {(long)Registers.ReceiveData, new ByteRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => {
                        this.TryGetCharacter(out var character);
                        return character;
                    })},

                {(long)Registers.Control1, new ByteRegister(this)
                    .WithValueField(0, 8, out baudValue0to7, name: "BAUD_VALUE_0_7")},

                {(long)Registers.Control2, new ByteRegister(this)
                    .WithFlag(0, name: "BIT8") // The register only provides a read-back function
                    .WithFlag(1, out parityFlagField, name: "PARITY_EN")
                    .WithFlag(2, out oddNEventFlagField, name: "ODD_N_EVEN")
                    .WithValueField(3, 5, out baudValue8to12, name: "BAUD_VALUE_8_12")},

                {(long)Registers.Control3, new ByteRegister(this)
                    .WithValueField(0, 3, out baudValueFractionField, name: "BAUD_VAL_FRACTION")
                    // bits 7:3 not mentioned in the documentation
                },

                {(long)Registers.Status, new ByteRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "TXRDY")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => Count > 0, name: "RXRDY")
                    .WithFlag(2, FieldMode.Read, name: "PARITY_ERR")
                    .WithFlag(3, FieldMode.Read, name: "OVERFLOW")
                    .WithFlag(4, FieldMode.Read, name: "FRAMING_ERROR")}
                    // bits 7:5 not used according to the documentation
            };

            registers = new ByteRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, (byte)value);
        }

        public long Size => 0x18;

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => parityFlagField.Value ? (oddNEventFlagField.Value ? Parity.Odd : Parity.Even) : Parity.None;

        public override uint BaudRate => (uint)((clockFrequency / (16 * ((baudValue8to12.Value << 8) + baudValue0to7.Value + 1))) + baudValueFractionField.Value * 0.125);

        protected override void CharWritten()
        {
            // intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private readonly IValueRegisterField baudValue0to7;
        private readonly IFlagRegisterField parityFlagField;
        private readonly IFlagRegisterField oddNEventFlagField;
        private readonly IValueRegisterField baudValue8to12;
        private readonly IValueRegisterField baudValueFractionField;
        private readonly ByteRegisterCollection registers;
        private readonly ulong clockFrequency;

        private enum Registers : long
        {
            TransmitData = 0x0,
            ReceiveData = 0x04,
            Control1 = 0x08,
            Control2 = 0x0C,
            Status = 0x10,
            Control3 = 0x14
        }
    }
}
