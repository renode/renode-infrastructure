//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class MSP430F2XXX_MPY : BasicWordPeripheral, IBytePeripheral
    {
        public MSP430F2XXX_MPY(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public void WriteByte(long offset, byte value)
        {
            if(offset == (long)Registers.MultiplySignedOperand1 || offset == (long)Registers.MultiplySignedAccumulateOperand1)
            {
                ushort extendedValue = value;
                extendedValue |= (value & 0x80) > 0 ? (ushort)0xFF00 : (ushort)0;
                WriteWord(offset, extendedValue);
                return;
            }

            WriteWord(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return (byte)ReadWord(offset);
        }

        private void SetModeAndOperand(ulong value, Mode mode)
        {
            operand1 = (uint)value;
            currentMode = mode;
        }

        private void StoreResult(ulong result)
        {
            resultLow.Value = (ushort)result;
            resultHigh.Value = (ushort)(result >> 16);
        }

        private void PerformCalculation(ulong operand2)
        {
            switch(currentMode)
            {
                case Mode.Unsigned:
                {
                    var result = operand1 * operand2;

                    StoreResult(result);
                    sumExtend.Value = 0;
                    break;
                }

                case Mode.Signed:
                {
                    var result = (short)operand1 * (short)operand2;
                    StoreResult((ulong)result);
                    sumExtend.Value = result < 0 ? 0xFFFFU : 0U;
                    break;
                }

                case Mode.UnsignedAccumulate:
                {
                    var result = LastResult + (int)(operand1 * operand2);
                    StoreResult((ulong)result);
                    sumExtend.Value = result > ushort.MaxValue ? 1U : 0U;
                    break;
                }

                case Mode.SignedAccumulate:
                {
                    var result = LastResult + (short)operand1 * (short)operand2;
                    StoreResult((ulong)result);
                    sumExtend.Value = result < 0 ? 0xFFFFU : 0U;
                    break;
                }
            }
        }

        private void DefineRegisters()
        {
            Registers.MultiplyUnsignedOperand1.Define(this)
                .WithValueField(0, 16, name: "MPY",
                    writeCallback: (_, value) => SetModeAndOperand(value, Mode.Unsigned))
            ;

            Registers.MultiplySignedOperand1.Define(this)
                .WithValueField(0, 16, name: "MPYS",
                    writeCallback: (_, value) => SetModeAndOperand(value, Mode.Signed))
                ;

            Registers.MultiplyAccumulateOperand1.Define(this)
                .WithValueField(0, 16, name: "MAC",
                    writeCallback: (_, value) => SetModeAndOperand(value, Mode.UnsignedAccumulate))
            ;

            Registers.MultiplySignedAccumulateOperand1.Define(this)
                .WithValueField(0, 16, name: "MACS",
                    writeCallback: (_, value) => SetModeAndOperand(value, Mode.SignedAccumulate))
            ;

            Registers.SecondOperand.Define(this)
                .WithValueField(0, 16, name: "OP2",
                    writeCallback: (_, value) => PerformCalculation(value))
            ;

            Registers.ResultLow.Define(this)
                .WithValueField(0, 16, out resultLow, name: "RESLO")
            ;

            Registers.ResultHigh.Define(this)
                .WithValueField(0, 16, out resultHigh, name: "RESHI")
            ;

            Registers.SumExtend.Define(this)
                .WithValueField(0, 16, out sumExtend, FieldMode.Read, name: "SUMEXT")
            ;
        }

        private int LastResult => (int)resultLow.Value | ((int)resultHigh.Value << 16);

        private Mode currentMode;
        private uint operand1;

        private IValueRegisterField resultLow;
        private IValueRegisterField resultHigh;
        private IValueRegisterField sumExtend;

        private enum Mode
        {
            Unsigned,
            Signed,
            UnsignedAccumulate,
            SignedAccumulate,
        }

        private enum Registers
        {
            MultiplyUnsignedOperand1 = 0x00,
            MultiplySignedOperand1 = 0x02,
            MultiplyAccumulateOperand1 = 0x04,
            MultiplySignedAccumulateOperand1 = 0x06,
            SecondOperand = 0x8,
            ResultLow = 0xA,
            ResultHigh = 0xC,
            SumExtend = 0xE,
        }
    }
}
