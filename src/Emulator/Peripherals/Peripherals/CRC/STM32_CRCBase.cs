//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2022 Pieter Agten
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CRC
{
    public class STM32_CRCBase : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public STM32_CRCBase(bool configurablePoly, IndependentDataWidth independentDataWidth)
        {
            this.configurablePoly = configurablePoly;
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "CRC_DR",
                        writeCallback: (_, value) =>
                        {
                            UpdateCRC((uint)value, 4);
                            // Equivalent for byte and word implemented directly in writeByte and writeWord methods
                        },
                        valueProviderCallback: _ => CRC.Value
                    )
                },
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear,
                        valueProviderCallback: _ => false,
                        name: "RESET")
                    .WithReservedBits(1, 2)
                    .WithEnumField<DoubleWordRegister, PolySize>(3, 2, out polySize, name: "POLYSIZE")
                    .WithEnumField<DoubleWordRegister, BitReversal>(5, 2, out reverseInputData, name: "REV_IN")
                    .WithFlag(7, out reverseOutputData, name: "REV_OUT")
                    .WithReservedBits(8, 24)
                    .WithWriteCallback((_, __) => { crcConfigDirty = true; })
                },
                {(long)Registers.InitialValue, new DoubleWordRegister(this, DefaultInitialValue)
                    .WithValueField(0, 32, out initialValue, name: "CRC_INIT")
                },
                {(long)Registers.Polynomial, new DoubleWordRegister(this, DefaultPolymonial)
                    .WithValueField(0, 32, out polynomial,
                        FieldMode.Read | (configurablePoly ? FieldMode.Write : 0),
                        name: "CRC_POL"
                    )
                    .WithWriteCallback((_, __) => { crcConfigDirty = true; })
                }
            };

            if(independentDataWidth == IndependentDataWidth.Bits8)
            {
                registersMap.Add((long)Registers.IndependentData,
                    new DoubleWordRegister(this)
                        .WithTag("CRC_IDR", 0, 8)
                        .WithReservedBits(8, 24));
            }
            else if(independentDataWidth == IndependentDataWidth.Bits32)
            {
                registersMap.Add((long)Registers.IndependentData,
                    new DoubleWordRegister(this)
                        .WithTag("CRC_IDR", 0, 32));
            }

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public byte ReadByte(long offset)
        {
            // only properly aligned reads will be handled correctly here
            return (byte)registers.Read(offset);
        }

        public ushort ReadWord(long offset)
        {
            // only properly aligned reads will be handled correctly here
            return (ushort)registers.Read(offset);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            if((Registers)offset == Registers.Data)
            {
                UpdateCRC(value, 1);
            }
            else
            {
                this.LogUnhandledWrite(offset, value);
            }
        }

        public void WriteWord(long offset, ushort value)
        {
            if((Registers)offset == Registers.Data)
            {
                UpdateCRC(value, 2);
            }
            else
            {
                this.LogUnhandledWrite(offset, value);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void Reset()
        {
            registers.Reset();
            ReloadCRCConfig();
        }

        public long Size => 0x400;

        public enum IndependentDataWidth
        {
            Bits8 = 0,
            Bits32 = 1
        }

        private static int PolySizeToCRCWidth(PolySize poly)
        {
            switch(poly)
            {
                case PolySize.CRC32:
                    return 32;
                case PolySize.CRC16:
                    return 16;
                case PolySize.CRC8:
                    return 8;
                case PolySize.CRC7:
                    return 7;
                default:
                    throw new ArgumentException($"Unknown PolySize value: {poly}!");
            }
        }

        private void UpdateCRC(uint value, int bytesCount)
        {
            if(reverseInputData.Value == BitReversal.ByByte)
            {
                value = BitHelper.ReverseBitsByByte(value);
            }
            else if(reverseInputData.Value == BitReversal.ByWord)
            {
                switch(bytesCount)
                {
                    case 1:
                        value = BitHelper.ReverseBitsByByte(value);
                        break;
                    case 2:
                    case 4:
                        value = BitHelper.ReverseBitsByWord(value);
                        break;
                }
            }
            else if(reverseInputData.Value == BitReversal.ByDoubleWord)
            {
                switch(bytesCount)
                {
                    case 1:
                        value = BitHelper.ReverseBitsByByte(value);
                        break;
                    case 2:
                        value = BitHelper.ReverseBitsByWord(value);
                        break;
                    case 4:
                        value = BitHelper.ReverseBits(value);
                        break;
                }
            }
            CRC.Update(BitHelper.GetBytesFromValue(value, bytesCount));
        }

        private void ReloadCRCConfig()
        {
            var config = new CRCConfig((uint)polynomial.Value, PolySizeToCRCWidth(polySize.Value), reflectInput: false, reflectOutput: reverseOutputData.Value, init: (uint)initialValue.Value, xorOutput: 0x0);
            if(crc == null || !config.Equals(crc.Config))
            {
                crc = new CRCEngine(config);
            }
            else
            {
                crc.Reset();
            }
            crcConfigDirty = false;
        }
        
        private CRCEngine CRC
        {
            get
            {
                if(crc == null || crcConfigDirty)
                {
                    ReloadCRCConfig();
                }
                return crc;
            }
        }

        private IFlagRegisterField reverseOutputData;
        private IEnumRegisterField<BitReversal> reverseInputData;
        private IEnumRegisterField<PolySize> polySize;
        private IValueRegisterField initialValue;
        private IValueRegisterField polynomial;

        private bool crcConfigDirty;
        private CRCEngine crc;

        private readonly bool configurablePoly;
        private readonly DoubleWordRegisterCollection registers;

        private const uint DefaultInitialValue = 0xFFFFFFFF;
        private const uint DefaultPolymonial = 0x04C11DB7;

        private enum BitReversal
        {
            Disabled,
            ByByte,
            ByWord,
            ByDoubleWord
        }

        private enum Registers : long
        {
            Data = 0x0,
            IndependentData = 0x4,
            Control = 0x8,
            InitialValue = 0x10,
            Polynomial = 0x14
        }

        private enum PolySize
        {
            CRC32,
            CRC16,
            CRC8,
            CRC7
        }
    }
}
