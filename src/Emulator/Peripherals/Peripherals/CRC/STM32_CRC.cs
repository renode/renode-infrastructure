//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2022 Pieter Agten
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CRC
{
    public class STM32_CRC : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public STM32_CRC(STM32Series series, bool configurablePoly=false)
        {
            STM32Config conf;
            if(!this.setupConfig.TryGetValue(series, out conf))
            {
                throw new ConstructionException($"Unknown STM32 series value: {series}!");
            }
            
            this.configurablePoly = configurablePoly;
            this.configurableInitialValue = conf.configurableInitialValue;

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
                    .If(conf.hasPolySizeBits)
                        .Then(r => r.WithEnumField<DoubleWordRegister, PolySize>(3, 2, out polySize, name: "POLYSIZE"))
                        .Else(r => r.WithReservedBits(3, 2))
                    .If(conf.reversibleIO)
                        .Then(r => { r.WithEnumField<DoubleWordRegister, BitReversal>(5, 2, out reverseInputData, name: "REV_IN");
                                     r.WithFlag(7, out reverseOutputData, name: "REV_OUT"); })
                        .Else(r => r.WithReservedBits(5, 3))
                    .WithReservedBits(8, 24)
                    .WithWriteCallback((_, __) => { crcConfigDirty = true; })
                },
                {(long)Registers.InitialValue, new DoubleWordRegister(this, DefaultInitialValue)
                    .WithValueField(0, 32, out initialValue,
                        FieldMode.Read | (configurableInitialValue ? FieldMode.Write : 0),
                        name: "CRC_INIT")
                },
                {(long)Registers.Polynomial, new DoubleWordRegister(this, DefaultPolymonial)
                    .WithValueField(0, 32, out polynomial,
                        FieldMode.Read | (configurablePoly ? FieldMode.Write : 0),
                        name: "CRC_POL"
                    )
                    .WithWriteCallback((_, __) => { crcConfigDirty = true; })
                },
                {(long)Registers.IndependentData, new DoubleWordRegister(this)
                    .WithTag("CRC_IDR", 0, (int)conf.independentDataWidth)
                    .If((int)conf.independentDataWidth != 32)
                        .Then(r => r.WithReservedBits((int)conf.independentDataWidth, 32 - (int)conf.independentDataWidth))
                        .Else(_ => {})
                }
            };

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
            Bits8 = 8,
            Bits32 = 32, 
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
            var config = new CRCConfig(
                (uint)polynomial.Value,
                PolySizeToCRCWidth(polySize?.Value ?? PolySize.CRC32), 
                reflectInput: false,
                reflectOutput: reverseOutputData?.Value ?? false,
                init: (uint)initialValue.Value,
                xorOutput: 0x0
            );
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

        private readonly Dictionary<STM32Series, STM32Config> setupConfig = new Dictionary<STM32Series, STM32Config> ()
        {
            {
                STM32Series.F0,
                new STM32Config()
                {
                    configurablePoly = false,
                    configurableInitialValue = true,
                    hasPolySizeBits = true,
                    reversibleIO = true,
                    independentDataWidth = IndependentDataWidth.Bits8,
                }
            },
            {
                STM32Series.F4,
                new STM32Config()
                {
                    configurablePoly = false,
                    configurableInitialValue = false,
                    hasPolySizeBits = false,
                    reversibleIO = false,
                    independentDataWidth = IndependentDataWidth.Bits8,
                }
            },
            {
                STM32Series.WBA,
                new STM32Config()
                {
                    configurablePoly = true,
                    configurableInitialValue = true,
                    hasPolySizeBits = true,
                    reversibleIO = true,
                    independentDataWidth = IndependentDataWidth.Bits32,
                }
            }
        };
        private readonly bool configurablePoly;
        private readonly bool configurableInitialValue;
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

        private struct STM32Config
        {
            public bool configurablePoly;
            public bool configurableInitialValue;
            public bool hasPolySizeBits;
            public bool reversibleIO;
            public IndependentDataWidth independentDataWidth;
        }
    }
}
