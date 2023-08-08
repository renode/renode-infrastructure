//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class MAX77818 : II2CPeripheral, IProvidesRegisterCollection<WordRegisterCollection>
    {
        public MAX77818(IMachine machine)
        {
            RegistersCollection = new WordRegisterCollection(this);
            DefineRegisters();
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }

            var offset = 0;
            if(state == States.Read)
            {
                this.Log(LogLevel.Warning, "Trying to write while in read mode; ignoring");
                return;
            }
            else if(state == States.Idle)
            {
                registerAddress = (Registers)data[0];
                offset = 1;
                state = data.Length == 1 ? States.Read : States.Write;
            }

            if(data.Length > offset)
            {
                foreach(var item in data.Skip(offset).Select((value, index) => new { index, value }))
                {
                    RegistersCollection.WriteWithOffset((long)registerAddress.Value, item.index, item.value);
                }
            }
        }

        public byte[] Read(int count)
        {
            if(!registerAddress.HasValue)
            {
                this.Log(LogLevel.Error, "Trying to read without setting address");
                return new byte[] {};
            }

            if(state != States.Read)
            {
                this.Log(LogLevel.Error, "Trying to read while in write mode");
                return new byte[] {};
            }

            var result = new byte[count];
            for(var i = 0; i < count; ++i)
            {
                result[i] = RegistersCollection.ReadWithOffset((long)registerAddress.Value, i);
            }
            return result;
        }

        public void FinishTransmission()
        {
            registerAddress = null;
            state = States.Idle;
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            registerAddress = null;
            state = States.Idle;
        }

        public WordRegisterCollection RegistersCollection { get; }

        public decimal Temperature { get; set; }
        public decimal CellVoltage { get; set; }
        public decimal Current { get; set; }

        public decimal DesignCapacity { get; set; }
        public decimal FullCapacity { get; set; }
        public decimal AvailableCapacity { get; set; }
        public decimal ReportedCapacity { get; set; }
        public decimal MixCapacity { get; set; }

        public decimal ReportedStateOfCharge { get; set; }
        public decimal Age { get; set; }
        public decimal QResidual { get; set; }
        public decimal Cycles { get; set; }

        private ushort ConvertTemperature(decimal temperature)
        {
            temperature = temperature.Clamp(MinTemperature, MaxTemperature);
            return (ushort)((short)(temperature * 256.0m));
        }

        private void DefineRegisters()
        {
            Registers.ReportedStateOfCharge.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "RepSOC",
                    valueProviderCallback: _ => (ushort)(ReportedStateOfCharge * 256.0m))
            ;

            Registers.Age.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "AGE",
                    valueProviderCallback: _ => (ushort)(Age * 256.0m))
            ;

            Registers.Temperature.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "TEMP",
                    valueProviderCallback: _ => ConvertTemperature(Temperature))
            ;

            Registers.CellVoltage.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "VCELL",
                    valueProviderCallback: _ => (ushort)(CellVoltage / CellVoltageSensitivity).Clamp(0m, (decimal)ushort.MaxValue))
            ;

            Registers.Current.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "CURRENT",
                    valueProviderCallback: _ => (ushort)(Current / CurrentSensitivity).Clamp(0m, (decimal)ushort.MaxValue))
            ;

            Registers.AverageCurrent.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "AVGCURRENT",
                    valueProviderCallback: _ => (ushort)(Current / CurrentSensitivity).Clamp(0m, (decimal)ushort.MaxValue))
            ;

            Registers.Qresidual.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "Qresidual",
                    valueProviderCallback: _ => (ushort)(QResidual / CapacitySensitivity).Clamp(0m, (decimal)ushort.MaxValue))
            ;

            Registers.MixCapacity.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "MaxCap",
                    valueProviderCallback: _ => (ushort)(MixCapacity / CapacitySensitivity).Clamp(0m, (decimal)ushort.MaxValue))
            ;

            Registers.FullCapacity.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "FullCAP",
                    valueProviderCallback: _ => (ushort)(FullCapacity / CapacitySensitivity).Clamp(0m, (decimal)ushort.MaxValue))
            ;

            Registers.AverageTemperature.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "AVGTA",
                    valueProviderCallback: _ => ConvertTemperature(Temperature))
            ;

            Registers.Cycles.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "CYCLES",
                    valueProviderCallback: _ => (ushort)(Cycles * 100.0m))
            ;

            Registers.DesignCapacity.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "DesignCAP",
                    valueProviderCallback: _ => (ushort)(DesignCapacity / CapacitySensitivity).Clamp(0m, (decimal)ushort.MaxValue))
            ;

            Registers.AverageCellVoltage.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "AVGVCELL",
                    valueProviderCallback: _ => (ushort)(CellVoltage / CellVoltageSensitivity).Clamp(0m, (decimal)ushort.MaxValue))
            ;

            Registers.AvailableCapacity.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "AvailableCAP",
                    valueProviderCallback: _ => (ushort)(AvailableCapacity / CapacitySensitivity).Clamp(0m, (decimal)ushort.MaxValue))
            ;
        }

        private Registers? registerAddress;
        private States state;

        private const decimal MinTemperature = (decimal)short.MinValue / 256.0m;
        private const decimal MaxTemperature = (decimal)short.MaxValue / 256.0m;
        private const decimal CellVoltageSensitivity = 78.125e-06m;
        private const decimal CurrentSensitivity = 0.078125m;
        private const decimal CapacitySensitivity = 0.5m;

        private enum States
        {
            Idle,
            Write,
            Read,
        }

        private enum Registers : byte
        {
            Status = 0x00,
            VoltageAlarmThreshold = 0x01,
            TemperatureAlarmThreshold = 0x02,
            StateOfChargeAlarmThreshold = 0x03,
            AtRate = 0x04,
            ReportedCapacity = 0x05,
            ReportedStateOfCharge = 0x06,
            Age = 0x07,
            Temperature = 0x08,
            CellVoltage = 0x09,
            Current = 0x0A,
            AverageCurrent = 0x0B,
            Qresidual = 0x0C,
            MixSOC = 0x0D,
            AvailableStateOfCharge = 0x0E,
            MixCapacity = 0x0F,

            FullCapacity = 0x10,
            TimeToEmpty = 0x11,
            QRtable00 = 0x12,
            FullStateOfChargeThreshold = 0x13,
            RSlow = 0x14,
            // Reserved
            AverageTemperature = 0x16,
            Cycles = 0x17,
            DesignCapacity = 0x18,
            AverageCellVoltage = 0x19,
            MaxMinTemperature = 0x1A,
            MaxMinVoltage = 0x1B,
            MaxMinCurrrent = 0x1C,
            Config = 0x1D,
            ICHGTerm = 0x1E,
            AvailableCapacity = 0x1F,

            TimeToFull = 0x20,
            DevName = 0x21,
            QRtable10 = 0x22,
            FullCapacityNominal = 0x23,
            TemperatureNominal = 0x24,
            TemperatureLimit = 0x25,
            // Reserved
            AIn0 = 0x27,
            LearnConfig = 0x28,
            FilterConfig = 0x29,
            RelaxConfig = 0x2A,
            MiscConfig = 0x2B,
            TemperatureGain = 0x2C,
            TemperatureOffset = 0x2D,
            CapacityGain = 0x2E,
            CapacityOffset = 0x2F,

            // Reserved
            // Reserved
            QRtable20 = 0x32,
            AtTimeToFull = 0x33,
            // Reserved
            FullCapacityRep = 0x35,
            AverageCurrentEmptyEvent = 0x36,
            FCTC = 0x37,
            RComp0 = 0x38,
            TempCoefficient = 0x39,
            VoltageEmpty = 0x3A,
            // Reserved
            // Reserved
            // Reserved
            Timer = 0x3E,
            ShutdownTimer = 0x3F,

            // Reserved
            // Reserved
            QRtable30 = 0x42,
            // Reserved
            // Reserved
            dQAcc = 0x45,
            dPAcc = 0x46,
            // Reserved
            // Reserved
            ConvergeConfig = 0x49,
            VoltageFuelRemainingCapacity = 0x4A,
            // Reserved
            // Reserved
            QH = 0x4D,
            // Reserved
            // Reserved

            // Reserved 0x50-0x7F

            OCV0 = 0x80,
            OCV1 = 0x81,
            OCV2 = 0x82,
            OCV3 = 0x83,
            OCV4 = 0x84,
            OCV5 = 0x85,
            OCV6 = 0x86,
            OCV7 = 0x87,
            OCV8 = 0x88,
            OCV9 = 0x89,
            OCVA = 0x8A,
            OCVB = 0x8B,
            OCVC = 0x8C,
            OCVD = 0x8D,
            OCVE = 0x8E,
            OCVF = 0x8F,

            CapacityAvailableToApplication0 = 0x90,
            CapacityAvailableToApplication1 = 0x91,
            CapacityAvailableToApplication2 = 0x92,
            CapacityAvailableToApplication3 = 0x93,
            CapacityAvailableToApplication4 = 0x94,
            CapacityAvailableToApplication5 = 0x95,
            CapacityAvailableToApplication6 = 0x96,
            CapacityAvailableToApplication7 = 0x97,
            CapacityAvailableToApplication8 = 0x98,
            CapacityAvailableToApplication9 = 0x99,
            CapacityAvailableToApplicationA = 0x9A,
            CapacityAvailableToApplicationB = 0x9B,
            CapacityAvailableToApplicationC = 0x9C,
            CapacityAvailableToApplicationD = 0x9D,
            CapacityAvailableToApplicationE = 0x9E,
            CapacityAvailableToApplicationF = 0x9F,

            RCompSegment0 = 0xA0,
            RCompSegment1 = 0xA1,
            RCompSegment2 = 0xA2,
            RCompSegment3 = 0xA3,
            RCompSegment4 = 0xA4,
            RCompSegment5 = 0xA5,
            RCompSegment6 = 0xA6,
            RCompSegment7 = 0xA7,
            RCompSegment8 = 0xA8,
            RCompSegment9 = 0xA9,
            RCompSegmentA = 0xAA,
            RCompSegmentB = 0xAB,
            RCompSegmentC = 0xAC,
            RCompSegmentD = 0xAD,
            RCompSegmentE = 0xAE,
            RCompSegmentF = 0xAF,

            Status2 = 0xB0,
            // Reserved
            TemperatureAlarmThreshold2 = 0xB2,
            // Reserved
            // Reserved
            TimeToFullConfig = 0xB5,
            CVMixCapacity = 0xB6,
            CVHalfTime = 0xB7,
            CGTemperatureCoefficient = 0xB8,
            Curve = 0xB9,
            // Reserved
            Config2 = 0xBB,
            Vripple = 0xBC,
            RippleConfig = 0xBD,
            TimerH = 0xBE,
            MaxError = 0xBF,

            // Reserved 0xC0-0xCF

            // Reserved
            ChargeState0 = 0xD1,
            ChargeState1 = 0xD2,
            ChargeState2 = 0xD3,
            ChargeState3 = 0xD4,
            ChargeState4 = 0xD5,
            ChargeState5 = 0xD6,
            ChargeState6 = 0xD7,
            ChargeState7 = 0xD8,
            JEITAVoltage = 0xD9,
            JEITACurrent = 0xDA,
            SmartChargingConfig = 0xDB,
            AtQresidual = 0xDC,
            AtTimeToEmpty = 0xDD,
            AtAvailableStateOfCharge = 0xDE,
            AtAvailableCapacity = 0xDF,

            // Reserved 0xE0-0xEF

            // Reserved 0xF0-FA
            VFOCV = 0xFB,
            // Reserved
            // Reserved
            // Reserved
            VFSOC = 0xFF,
        }
    }
}
