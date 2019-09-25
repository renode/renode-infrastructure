//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class EFR32_GPCRC : IDoubleWordPeripheral, IKnownSize
    {
        public EFR32_GPCRC()
        {
            gpcrc = new CRCEngine(DefaultPolynomial);
            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, changeCallback: (_, enable) => { isEnabled = enable; }, name: "EN")
                    .WithReservedBits(1, 3)
                    .WithFlag(4, changeCallback: (_, polySel) =>
                    {
                        if(polySel)
                        {
                            this.Log(LogLevel.Warning, "16-bit polynomial is not supported");
                        }
                    }, name: "POLYSEL")
                    .WithReservedBits(5, 3)
                    .WithTaggedFlag("BYTEMODE", 8)
                    .WithTag("BITEREVERSE", 9, 1)
                    .WithTaggedFlag("BYTEREVERSE", 10)
                    .WithReservedBits(11, 2)
                    .WithTaggedFlag("AUTOINIT", 13)
                    .WithReservedBits(14, 18)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, changeCallback: (_, value) => { if(value) { UpdateInitVal(); } }, name: "INIT")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.Init, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out initDataField, name: "INIT")
                },
                {(long)Registers.InputData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        crc.Value = gpcrc.CalculateCrc32(BitConverter.GetBytes(value), crc.Value);
                    }, name: "INPUTDATA")
                },
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out crc, FieldMode.Read, name: "DATA")
                },
            };
            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        public void Reset()
        {
            registers.Reset();
            isEnabled = false;
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x400;

        private void UpdateInitVal()
        {
            if(isEnabled)
            {
                crc.Value = initDataField.Value;
            }
        }

        private CRCEngine gpcrc;
        private readonly DoubleWordRegisterCollection registers;
        private bool isEnabled;
        private IValueRegisterField initDataField;
        private IValueRegisterField crc;

        private const CRCType DefaultPolynomial = CRCType.CRC32CCITTPolynomial;

        private enum Registers
        {
            Control = 0x00,
            Command = 0x04,
            Init = 0x08,
            Polynomial = 0x0C,
            InputData = 0x10,
            InputDataHWord = 0x14,
            InputDataByte = 0x18,
            Data = 0x1C,
            DataReverse = 0x20,
            DataByteReverse = 0x24,
        }
    }
}
