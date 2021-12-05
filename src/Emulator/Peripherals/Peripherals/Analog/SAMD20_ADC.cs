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


namespace Antmicro.Renode.Peripherals.Analog
{
     
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class SAMD20_ADC : BasicDoubleWordPeripheral, IKnownSize, IWordPeripheral, IBytePeripheral
    {


        public SAMD20_ADC(Machine machine) : base(machine)
        {
            IRQ = new GPIO();

            // Create a list of adc values
            adc_values = new List<ushort>();

            for (int i=0; i < NumberOfADCs; i++)
            {
                adc_values.Add(0x0000);
            }

            DefineRegisters();
        }


  


        public long Size => 0x100;

        public GPIO IRQ { get; private set; }


        public override void Reset()
        {
            base.Reset();
            UpdateInterrupts();
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

        public void WriteAdcValue(int offset, ushort value)
        {
            if (offset < NumberOfADCs)
                adc_values[offset] = value;
        }

        public ushort ReadAdcValue(int offset)
        {
            if (offset < NumberOfADCs)
                return adc_values[offset];
            return 0;
        }




        private const int NumberOfADCs = 32;

        private IFlagRegisterField RESRDY;
        private IFlagRegisterField RESRDY_Clear;
        private IFlagRegisterField RESRDY_Set;

        private IFlagRegisterField OVERRUN;
        private IFlagRegisterField OVERRUN_Clear;
        private IFlagRegisterField OVERRUN_Set;

        private IFlagRegisterField WINMON;
        private IFlagRegisterField WINMON_Clear;
        private IFlagRegisterField WINMON_Set;

        private IFlagRegisterField SYNCRDY;
        private IFlagRegisterField SYNCRDY_Clear;
        private IFlagRegisterField SYNCRDY_Set;

        private IFlagRegisterField START;
        private IValueRegisterField ChannelIndex;

        private List<ushort> adc_values;

        private void DefineRegisters()
        {

            Register.ControlA.Define(this, 0x00, "ControlA")
            .WithValueField(0, 8, name: "unused"); ;

            Register.RefCtrl.Define(this, 0x00, "RefCtrl")
            .WithValueField(0, 8, name: "unused"); ;

            Register.AvgCtrl.Define(this, 0x00, "AvgCtrl")
            .WithValueField(0, 8, name: "unused"); ;

            Register.SampCtrl.Define(this, 0x00, "SampCtrl")
            .WithValueField(0, 8, name: "unused"); ;

            Register.ControlB.Define(this, 0x00, "ControlB")
            .WithTag("Reserved", 0, 16);

            Register.WinCtrl.Define(this, 0x00, "WinCtrl")
            .WithValueField(0, 8, name: "unused"); ;

            Register.SWTrig.Define(this, 0x00, "SWTrig")
            .WithTaggedFlag("FLUSH", 0)
            .WithFlag(1, out START, FieldMode.WriteOneToClear | FieldMode.Read,
                        writeCallback: (_, value) =>
                        {
                            if (value == true)
                            {
                                RESRDY.Value = true;
                                UpdateInterrupts();
                            }
                        }, name: "START")
            .WithTag("Reserved", 2, 6);

            Register.InputCtrl.Define(this, 0x00, "InputCtrl")
                .WithValueField(0, 5, out ChannelIndex)
                .WithTag("Reserved", 5, 27);

            Register.EvCtrl.Define(this, 0x00, "EvCtrl")
            .WithValueField(0, 8, name: "unused"); ;

            Register.IntenClr.Define(this, 0x00, "IntenClr")
            .WithFlag(0, out RESRDY_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    RESRDY_Set.Value = false;
                    UpdateInterrupts();
                }
            }, name: "RESRDY Interrupt Enable")
            .WithFlag(1, out OVERRUN_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    OVERRUN_Set.Value = false;
                    UpdateInterrupts();
                }
            }, name: "OVERRUN interrupt is disabled.")
            .WithFlag(2, out WINMON_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    WINMON_Set.Value = false;
                    UpdateInterrupts();
                }
            }, name: "WINMON Interrupt is diabled")
            .WithFlag(3, out SYNCRDY_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    SYNCRDY_Set.Value = false;
                    UpdateInterrupts();
                }
            }, name: "Int Disable SYNCRDY")
            .WithTag("RESERVED", 4, 4);

            Register.IntenSet.Define(this, 0x00, "IntenSet")
            .WithFlag(0, out RESRDY_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    RESRDY_Clear.Value = false;
                    UpdateInterrupts();
                }
            }, name: "RESRDY Interrupt Enable")
            .WithFlag(1, out OVERRUN_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    OVERRUN_Clear.Value = false;
                    UpdateInterrupts();
                }
            }, name: "OVERRUN interrupt is disabled.")
            .WithFlag(2, out WINMON_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    WINMON_Clear.Value = false;
                    UpdateInterrupts();
                }
            }, name: "WINMON Interrupt Enable")
            .WithFlag(3, out SYNCRDY_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    SYNCRDY_Clear.Value = false;
                    UpdateInterrupts();
                }
            }, name: "SYNCRDY Interrupt Enable")
            .WithTag("RESERVED", 4, 4);


            Register.IntFlag.Define(this, 0x00, "IntenFlag")
            .WithFlag(0, out RESRDY, FieldMode.WriteOneToClear | FieldMode.Read, name: "RESRDY")
            .WithFlag(1, out OVERRUN, FieldMode.WriteOneToClear | FieldMode.Read, name: "OVERRUN")
            .WithFlag(2, out WINMON, FieldMode.WriteOneToClear | FieldMode.Read, name: "WINMON")
            .WithFlag(3, out SYNCRDY, FieldMode.WriteOneToClear | FieldMode.Read, name: "SYNCRDY")
            .WithTag("RESERVED", 4, 4);

            Register.Status.Define(this, 0x00, "Status")
            .WithValueField(0, 8, name: "unused"); ;

            Register.Result.Define(this, 0x00, "Result")
                .WithValueField(0, 16,
                valueProviderCallback: _ =>
                {
                    WINMON.Value = false;
                    RESRDY.Value = false;
                    UpdateInterrupts();
                    this.Log(LogLevel.Info, "Read ADC result channel: {0} value: {1}", ChannelIndex.Value, adc_values[(int)ChannelIndex.Value]);
                    return adc_values[(int)ChannelIndex.Value];

                }, name: "RESULT");

            Register.WinLt.Define(this, 0x00, "WinLt")
            .WithTag("Reserved", 0, 16);

            Register.WinUt.Define(this, 0x00, "WinUt")
            .WithTag("Reserved", 0, 16);

            Register.GainCorr.Define(this, 0x00, "GainCorr")
            .WithTag("Reserved", 0, 16);

            Register.OffsetCorr.Define(this, 0x00, "OffsetCorr")
            .WithTag("Reserved", 0, 16);

            Register.Calibration.Define(this, 0x00, "Calibration")
            .WithTag("Reserved", 0, 16);

            Register.DbgCtrl.Define(this, 0x00, "DbgCtrl")
            .WithValueField(0, 8, name: "unused"); ;



        }

        private void UpdateInterrupts()
        {
            bool RESRDY_IntActive = false;
            bool OVERRUN_IntActive = false;
            bool WINMON_IntActive = false;
            bool SYNCRDY_IntActive = false;

            if (RESRDY_Set.Value & RESRDY.Value)
            {
                RESRDY_IntActive = true;
            }


            if (OVERRUN_Set.Value & OVERRUN.Value)
            {
                OVERRUN_IntActive = true;
            }

            if (WINMON_Set.Value & WINMON.Value)
            {
                WINMON_IntActive = true;
            }

            if (SYNCRDY_Set.Value & SYNCRDY.Value)
            {
                SYNCRDY_IntActive = true;
            }
            // Set or Clear Interrupt
            IRQ.Set(SYNCRDY_IntActive | WINMON_IntActive | OVERRUN_IntActive | RESRDY_IntActive);

        }




        private enum Register : long
        {
            ControlA = 0x00,
            RefCtrl  = 0x01,
            AvgCtrl  = 0x02,
            SampCtrl = 0x03,
            ControlB = 0x04,
            WinCtrl = 0x08,
            SWTrig = 0x0C,
            InputCtrl = 0x10,
            EvCtrl = 0x14,
            IntenClr = 0x16,
            IntenSet = 0x17,
            IntFlag = 0x18,
            Status = 0x19,
            Result = 0x1A,
            WinLt = 0x1C,
            WinUt = 0x20,
            GainCorr = 0x24,
            OffsetCorr = 0x26,
            Calibration = 0x28,
            DbgCtrl = 0x2A
        }
    }
}

