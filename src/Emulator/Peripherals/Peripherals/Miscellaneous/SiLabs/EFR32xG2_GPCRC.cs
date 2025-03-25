//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class EFR32xG2_GPCRC : IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_GPCRC()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out IpVersion, FieldMode.Read)
                },

                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithFlag(0, changeCallback: (_, enable) => { isEnabled = enable; },
                    valueProviderCallback: _ => isEnabled, name: "EN")
                    .WithReservedBits(1, 31)
                },

                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithReservedBits(0, 4)
                    .WithFlag(4, changeCallback: (_, polySel) => 
                    { 
                        POLY16_EN = polySel; 
                        ReloadCRCConfig();
                    }, name: "POLYSEL")
                    .WithReservedBits(5, 3)
                    .WithFlag(8, changeCallback: (_, enable) => { BYTEMODE = enable;}, name: "BYTEMODE")
                    .WithFlag(9, changeCallback: (_, enable) => { BITREVERSE = enable;}, name: "BITREVERSE")
                    .WithFlag(10, changeCallback: (_, enable) => { BYTEREVERSE = false;}, name: "BYTEREVERSE")
                    .WithReservedBits(11, 2)
                    .WithFlag(13, changeCallback: (_, enable) => { AUTOINIT = enable;}, name: "AUTOINIT")
                    .WithReservedBits(14, 18)
                },

                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, 
                    writeCallback: (_, value) => { 
                    if(value) 
                    { 
                        ReloadCRCConfig(); 
                        OUT_CRC = (uint)initDataField.Value;
                    } }, name: "INIT")
                    .WithReservedBits(1, 31)
                },

                {(long)Registers.Init, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out initDataField, name: "INIT")
                },

                {(long)Registers.Poly, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out Poly16, name: "POLY")
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.InputData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => 
                    {
                        UpdateData((uint)value);
                    }, name: "INPUTDATA")
                },

                {(long)Registers.InputDataHWord, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, writeCallback: (_, value) => 
                    {
                        UpdateData((ushort)value);
                    }, name: "INPUTDATAHWORD")
                },

                {(long)Registers.InputDataByte, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) => 
                    {
                        UpdateData((byte)value);
                    }, name: "INPUTDATABYTE")
                },

                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, 
                    valueProviderCallback: _ => OUT_CRC,
                    name: "DATA")
                    .WithReadCallback((_, __) => 
                    { 
                        if(AUTOINIT) 
                        {
                            OUT_CRC = (uint)initDataField.Value;  
                        };
                    })
                },

                {(long)Registers.DataRev, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, 
                    valueProviderCallback: _ => 
                    BitHelper.ReverseBits(OUT_CRC << (CRCConfig.MaxCRCWidth - gpcrc.Config.Width)),
                    name: "DATAREV")
                    .WithReadCallback((_, __) => 
                    { 
                        if(AUTOINIT) 
                        {
                            OUT_CRC = (uint)initDataField.Value;  
                        };
                    })
                },

                {(long)Registers.DataByteRev, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, 
                    valueProviderCallback: _ => BitHelper.ReverseBytes(OUT_CRC),
                    name: "DATABYTEREV")
                    .WithReadCallback((_, __) => 
                    { 
                        if(AUTOINIT) 
                        {
                            OUT_CRC = (uint)initDataField.Value;  
                        };
                    })
                },

                {(long)Registers.Enable_Set, new DoubleWordRegister(this)
                    .WithFlag(0, writeCallback: (_, enable) => {isEnabled=(enable)?true:isEnabled;}, name: "EN_SET")
                    .WithReservedBits(1, 31)
                },

                {(long)Registers.Enable_Clr, new DoubleWordRegister(this)
                    .WithFlag(0, writeCallback: (_, enable) => {isEnabled=(enable)?false:isEnabled;}, name: "EN_Clr")
                    .WithReservedBits(1, 31)
                },
            };
            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        public void Reset()
        {
            registers.Reset();
            isEnabled = false;
            ReloadCRCConfig();
        }

        public byte ReadByte(long offset)
        {
            return (byte)registers.Read(offset);
        }
        public ushort ReadWord(long offset)
        {
            return (ushort)registers.Read(offset);
        }
        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }
        
        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }
        
        private void ReloadCRCConfig()
        {
            CRCPolynomial temp;
            if(POLY16_EN) 
            {
                temp = new CRCPolynomial(BitHelper.ReverseBits((ushort)Poly16.Value), 16);
            }
            else 
            {
                temp = DefaultPolynomial;
            }
            var config = new CRCConfig(temp, true, true, (uint)initDataField.Value, 0x0);
            if(isEnabled)
            {
                if(gpcrc == null || !config.Equals(gpcrc.Config))
                {
                    gpcrc = new CRCEngine(config);
                }
                else
                {
                    gpcrc.Reset();
                }
            }
        }

        private void UpdateData(uint temp)
        {
            if(BYTEMODE)
            {
                UpdateData((byte)temp);
                return;
            }
            if(BYTEREVERSE)
            {
                temp = BitHelper.ReverseBytes(temp);
            }
            if(BITREVERSE)
            {
                temp = BitHelper.ReverseBitsByByte(temp);
            }
            if(isEnabled)
            {
                gpcrc.Update(BitHelper.GetBytesFromValue(temp,4));
                OUT_CRC = gpcrc.Value;
            }
        }
        private void UpdateData(ushort temp)
        {
            if(BYTEMODE)
            {
                UpdateData((byte)temp);
                return;
            }
            if(BYTEREVERSE)
            {
                temp = BitHelper.ReverseBytes(temp);
            }
            if(BITREVERSE)
            {
                temp = (ushort)BitHelper.ReverseBitsByByte(temp);
            }
            if(isEnabled)
            {
                gpcrc.Update(BitHelper.GetBytesFromValue(temp,2));
                OUT_CRC = gpcrc.Value;
            }
        }
        private void UpdateData(byte temp)
        {
            if(BITREVERSE)
            {
                temp = BitHelper.ReverseBits(temp);
            }
            if(isEnabled)
            {
                gpcrc.Update(BitHelper.GetBytesFromValue(temp,1));
                OUT_CRC = gpcrc.Value;
            }
        }
        
        public long Size => 0x400;
        private CRCEngine gpcrc;
        private readonly DoubleWordRegisterCollection registers;
        private bool isEnabled;
        private bool BYTEMODE;
        private bool BITREVERSE;
        private bool BYTEREVERSE;
        private bool AUTOINIT;
        private bool POLY16_EN;
        private IValueRegisterField initDataField;
        private IValueRegisterField IpVersion;
        private IValueRegisterField Poly16;
        private uint OUT_CRC;

        private readonly CRCPolynomial DefaultPolynomial = CRCPolynomial.CRC32;

        private enum Registers
        {
            Ipversion = 0x00,
            Enable = 0x04,
            Control = 0x08,
            Command = 0x0C,
            Init = 0x10,
            Poly = 0x14,
            InputData = 0x18,
            InputDataHWord = 0x1C,
            InputDataByte = 0x20,
            Data = 0x24,
            DataRev = 0x28,
            DataByteRev = 0x2C,

            Ipversion_Set = 0x1000,
            Enable_Set = 0x1004,
            Control_Set = 0x1008,
            Command_Set = 0x100c,
            Init_Set = 0x1010,
            Poly_Set = 0x1014,
            InputData_Set = 0x1018,
            InputDataHWord_Set = 0x101c,
            InputDataByte_Set = 0x1020,
            Data_Set = 0x1024,
            DataRev_Set = 0x1028,
            DataByteRev_Set = 0x102c,

            Ipversion_Clr = 0x2000,
            Enable_Clr = 0x2004,
            Control_Clr = 0x2008,
            Command_Clr = 0x200c,
            Init_Clr = 0x2010,
            Poly_Clr = 0x2014,
            InputData_Clr = 0x2018,
            InputDataHWord_Clr = 0x201c,
            InputDataByte_Clr = 0x2020,
            Data_Clr = 0x2024,
            DataRev_Clr = 0x2028,
            DataByteRev_Clr = 0x202c,

            Ipversion_Tgl = 0x3000,
            Enable_Tgl = 0x3004,
            Control_Tgl = 0x3008,
            Command_Tgl = 0x300c,
            Init_Tgl = 0x3010,
            Poly_Tgl = 0x3014,
            InputData_Tgl = 0x3018,
            InputDataHWord_Tgl = 0x301c,
            InputDataByte_Tgl = 0x3020,
            Data_Tgl = 0x3024,
            DataRev_Tgl = 0x3028,
            DataByteRev_Tgl = 0x302c,
        }
    }
}