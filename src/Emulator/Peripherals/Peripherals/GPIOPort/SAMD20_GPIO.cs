
//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using System;
using Antmicro.Migrant;
using Antmicro.Renode.Peripherals.GPIOPort;
 
namespace Antmicro.Renode.Peripherals.GPIOPort
{

    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class SAMD20_GPIO : BaseGPIOPort, IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral
    {


        public SAMD20_GPIO(Machine machine) : base(machine, NumberOfPins)
        {
            dwordregisters = new DoubleWordRegisterCollection(this);

            DefineRegisters();
        }





        public long Size => 0x100;


        public override void OnGPIO(int number, bool value)
        {
            base.OnGPIO(number, value);
            Connections[number].Set(value);
            uint val = (uint)0x01 << number;

            if (value)
            {
                IN.Value |= val;
            }
            else
            {
                IN.Value &= ~val;
            }
        }

        private void UpdateState()
        {
            var value = OUT.Value;
            for (var i = 0; i < NumberOfPins; i++)
            {
                var state = ((value & 1u) == 1);

                State[i] = state;
                if (state)
                {
                    Connections[i].Set();
                }
                else
                {
                    Connections[i].Unset();
                }

                value >>= 1;
            }
        }



        public override void Reset()
        {
            base.Reset();
        }
        public ushort ReadWord(long offset)
        {
            return (ushort)ReadDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteDoubleWord(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return (byte)ReadDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteDoubleWord(offset, value);
        }



        private readonly DoubleWordRegisterCollection dwordregisters;
        private IValueRegisterField DIRSET;
        private IValueRegisterField DIR;
        private IValueRegisterField DIRCLR;

        private IValueRegisterField OUTSET;
        private IValueRegisterField OUT;
        private IValueRegisterField OUTCLR;

        private IValueRegisterField IN;

        private const int NumberOfPins = 32;

        private enum Registers : long
        {
            DIR = 0x00,
            DIRCLR = 0x04,
            DIRSET = 0x08,
            DIRTGL = 0x0C,
            OUT = 0x10,
            OUTCLR = 0x14,
            OUTSET = 0x18,
            OUTTGL = 0x1C,
            IN = 0x20,
            CTRL = 0x24,
            WRCONFIG = 0x28,
            PMUX0 = 0x30,
            PMUX1 = 0x31,
            PMUX2 = 0x32,
            PMUX3 = 0x33,
            PMUX4 = 0x34,
            PMUX5 = 0x35,
            PMUX6 = 0x36,
            PMUX7 = 0x37,
            PMUX8 = 0x38,
            PMUX9 = 0x39,
            PMUX10 = 0x3A,
            PMUX11 = 0x3B,
            PMUX12 = 0x3C,
            PMUX13 = 0x3D,
            PMUX14 = 0x3E,
            PMUX15 = 0x3F,
            PINCFG0 = 0x40,
            PINCFG1 = 0x41,
            PINCFG2 = 0x42,
            PINCFG3 = 0x43,
            PINCFG4 = 0x44,
            PINCFG5 = 0x45,
            PINCFG6 = 0x46,
            PINCFG7 = 0x47,
            PINCFG8 = 0x48,
            PINCFG9 = 0x49,
            PINCFG10 = 0x4A,
            PINCFG11 = 0x4B,
            PINCFG12 = 0x4C,
            PINCFG13 = 0x4D,
            PINCFG14 = 0x4E,
            PINCFG15 = 0x4F,
            PINCFG16 = 0x50,
            PINCFG17 = 0x51,
            PINCFG18 = 0x52,
            PINCFG19 = 0x53,
            PINCFG20 = 0x54,
            PINCFG21 = 0x55,
            PINCFG22 = 0x56,
            PINCFG23 = 0x57,
            PINCFG24 = 0x58,
            PINCFG25 = 0x59,
            PINCFG26 = 0x5A,
            PINCFG27 = 0x5B,
            PINCFG28 = 0x5C,
            PINCFG29 = 0x5D,
            PINCFG30 = 0x5E,
            PINCFG31 = 0x5F
        }


        private void DefineRegisters()
        {

            Registers.DIR.Define(dwordregisters, 0x00000000, "DIR")
            .WithValueField(0, 32, out DIR, name: "OUT");

            Registers.DIRCLR.Define(dwordregisters, 0x00000000, "DIRCLR")
            .WithValueField(0, 32, out DIRCLR, FieldMode.Read | FieldMode.Write,
            writeCallback: (_, value) =>
            {
                DIR.Value = ~value & DIR.Value;
                DIRCLR.Value = ~DIR.Value;
                DIRSET.Value = ~DIRCLR.Value;
            }, name: "DIRCLR");

            Registers.DIRSET.Define(dwordregisters, 0x00000000, "DIRSET")
            .WithValueField(0, 32, out DIRSET, FieldMode.Read | FieldMode.Write,
            writeCallback: (_, value) =>
            {
                DIR.Value = value | DIR.Value;
                DIRSET.Value = DIR.Value;
                DIRCLR.Value = ~DIRSET.Value;
            }, name: "DIRSET");

            Registers.DIRTGL.Define(dwordregisters, 0x00, "DIRTGL")
            .WithValueField(0, 32, name: "unused");

            //
            Registers.OUT.Define(dwordregisters, 0x00, "OUT")
            .WithValueField(0, 32, out OUT, FieldMode.Read | FieldMode.Write,
            writeCallback: (_, value) =>
            {
                OUT.Value = value;
                UpdateState();
            }, name: "OUT");


            Registers.OUTCLR.Define(dwordregisters, 0x00, "OUTCLR")
            .WithValueField(0, 32, out OUTCLR, FieldMode.Read | FieldMode.Write,
            writeCallback: (_, value) =>
            {
                OUT.Value = ~value & OUT.Value;
                OUTCLR.Value = ~OUT.Value;
                OUTSET.Value = ~OUTCLR.Value;
                UpdateState();
            }, name: "OUTCLR");

            Registers.OUTSET.Define(dwordregisters, 0x00, "OUTSET")
            .WithValueField(0, 32, out OUTSET, FieldMode.Read | FieldMode.Write,
            writeCallback: (_, value) =>
            {
                OUT.Value = value | OUT.Value;
                OUTSET.Value = OUT.Value;
                OUTCLR.Value = ~OUTSET.Value;
                UpdateState();
            }, name: "OUTSET");


            Registers.OUTTGL.Define(dwordregisters, 0x00, "OUTTGL")
            .WithValueField(0, 32, name: "unused");

            Registers.IN.Define(dwordregisters, 0x00, "IN")
            .WithValueField(0, 32, out IN, name: "IN");

            Registers.CTRL.Define(dwordregisters, 0x00, "CTRL")
            .WithValueField(0, 32, name: "unused");

            Registers.WRCONFIG.Define(dwordregisters, 0x00, "WRCONFIG")
            .WithValueField(0, 32, name: "unused");

            Registers.PMUX0.Define(dwordregisters, 0x00, "PMUX0")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX1.Define(dwordregisters, 0x00, "PMUX1")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX2.Define(dwordregisters, 0x00, "PMUX2")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX3.Define(dwordregisters, 0x00, "PMUX3")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX4.Define(dwordregisters, 0x00, "PMUX4")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX5.Define(dwordregisters, 0x00, "PMUX5")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX6.Define(dwordregisters, 0x00, "PMUX6")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX7.Define(dwordregisters, 0x00, "PMUX7")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX8.Define(dwordregisters, 0x00, "PMUX8")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX9.Define(dwordregisters, 0x00, "PMUX9")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX10.Define(dwordregisters, 0x00, "PMUX10")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX11.Define(dwordregisters, 0x00, "PMUX11")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX12.Define(dwordregisters, 0x00, "PMUX12")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX13.Define(dwordregisters, 0x00, "PMUX13")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX14.Define(dwordregisters, 0x00, "PMUX14")
            .WithValueField(0, 8, name: "unused");
            Registers.PMUX15.Define(dwordregisters, 0x00, "PMUX15")
            .WithValueField(0, 8, name: "unused");

            Registers.PINCFG0.Define(dwordregisters, 0x00, "PINCFG0")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG1.Define(dwordregisters, 0x00, "PINCFG1")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG2.Define(dwordregisters, 0x00, "PINCFG2")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG3.Define(dwordregisters, 0x00, "PINCFG3")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG4.Define(dwordregisters, 0x00, "PINCFG4")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG5.Define(dwordregisters, 0x00, "PINCFG5")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG6.Define(dwordregisters, 0x00, "PINCFG6")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG7.Define(dwordregisters, 0x00, "PINCFG7")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG8.Define(dwordregisters, 0x00, "PINCFG8")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG9.Define(dwordregisters, 0x00, "PINCFG9")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG10.Define(dwordregisters, 0x00, "PINCFG10")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG11.Define(dwordregisters, 0x00, "PINCFG11")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG12.Define(dwordregisters, 0x00, "PINCFG12")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG13.Define(dwordregisters, 0x00, "PINCFG13")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG14.Define(dwordregisters, 0x00, "PINCFG14")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG15.Define(dwordregisters, 0x00, "PINCFG15")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG16.Define(dwordregisters, 0x00, "PINCFG16")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG17.Define(dwordregisters, 0x00, "PINCFG17")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG18.Define(dwordregisters, 0x00, "PINCFG18")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG19.Define(dwordregisters, 0x00, "PINCFG19")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG20.Define(dwordregisters, 0x00, "PINCFG20")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG21.Define(dwordregisters, 0x00, "PINCFG21")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG22.Define(dwordregisters, 0x00, "PINCFG22")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG23.Define(dwordregisters, 0x00, "PINCFG23")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG24.Define(dwordregisters, 0x00, "PINCFG24")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG25.Define(dwordregisters, 0x00, "PINCFG25")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG26.Define(dwordregisters, 0x00, "PINCFG26")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG27.Define(dwordregisters, 0x00, "PINCFG27")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG28.Define(dwordregisters, 0x00, "PINCFG28")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG29.Define(dwordregisters, 0x00, "PINCFG29")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG30.Define(dwordregisters, 0x00, "PINCFG30")
            .WithValueField(0, 8, name: "unused");
            Registers.PINCFG31.Define(dwordregisters, 0x00, "PINCFG31")
            .WithValueField(0, 8, name: "unused");

        }



        public uint ReadDoubleWord(long offset)
        {
            return dwordregisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
        }

        public Boolean IsGPIOHigh (int Pin)
        {
            Boolean retVal = false;
            uint val = dwordregisters.Read(0x10);
            uint test = 1;
            uint bitpos = test << Pin;
            uint result = bitpos & val;
            if (result > 0)
               retVal = true;
            return retVal;
        }
    }
}