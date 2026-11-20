//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class LSM330_Gyroscope : ST_I2CSensorBase<LSM330_Gyroscope.Registers>
    {
        public LSM330_Gyroscope()
        {
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            UpdateSensitivity(0);
        }

        public decimal AngularRateX { get; set; }

        public decimal AngularRateY { get; set; }

        public decimal AngularRateZ { get; set; }

        protected override void DefineRegisters()
        {
            // LSM330 datasheet (DocID023426 Rev 3), Table 17: register address map and default values
            Registers.WhoaAmI.Define(this, WhoAmIValue)
                .WithValueField(0, 8, FieldMode.Read, name: "WHO_AM_I_G", valueProviderCallback: _ => WhoAmIValue)
            ;

            Registers.Control1.Define(this, 0x07)
                .WithValueField(0, 8, name: "CTRL_REG1_G")
            ;

            Registers.Control2.Define(this)
                .WithValueField(0, 8, name: "CTRL_REG2_G")
            ;

            Registers.Control3.Define(this)
                .WithValueField(0, 8, name: "CTRL_REG3_G")
            ;

            Registers.Control4.Define(this)
                .WithTaggedFlag("SIM", 0)
                .WithReservedBits(1, 3)
                .WithValueField(4, 2, name: "FS: Full-scale selection", writeCallback: (_, val) => UpdateSensitivity((uint)val))
                .WithTaggedFlag("BLE", 6)
                .WithTaggedFlag("BDU", 7)
            ;

            Registers.OutputXLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_L_G", valueProviderCallback: _ => GetOutputByte(AngularRateX, false))
            ;

            Registers.OutputXHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_H_G", valueProviderCallback: _ => GetOutputByte(AngularRateX, true))
            ;

            Registers.OutputYLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_L_G", valueProviderCallback: _ => GetOutputByte(AngularRateY, false))
            ;

            Registers.OutputYHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_H_G", valueProviderCallback: _ => GetOutputByte(AngularRateY, true))
            ;

            Registers.OutputZLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_L_G", valueProviderCallback: _ => GetOutputByte(AngularRateZ, false))
            ;

            Registers.OutputZHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_H_G", valueProviderCallback: _ => GetOutputByte(AngularRateZ, true))
            ;
        }

        private byte GetOutputByte(decimal angularRate, bool upperByte)
        {
            // two's complement, saturated at the 16-bit output range
            var digits = Math.Round(angularRate / sensitivityDpsPerDigit);
            var clamped = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, digits));
            return upperByte
                ? (byte)(clamped >> 8)
                : (byte)clamped;
        }

        private void UpdateSensitivity(uint val)
        {
            // LSM330 datasheet (DocID023426 Rev 3), Table 3 (G_So): 8.75 / 17.50 / 70 mdps/digit for FS = 250 / 500 / 2000 dps
            switch(val)
            {
            case 0:
                sensitivityDpsPerDigit = 0.00875m;
                break;
            case 1:
                sensitivityDpsPerDigit = 0.0175m;
                break;
            // yes, both cases 0b10 and 0b11 encodes scale of 2000 dps
            case 2:
            case 3:
                sensitivityDpsPerDigit = 0.07m;
                break;
            default:
                this.Log(LogLevel.Warning, "Tried to set an unsupported sensitivity value: {0}", val);
                return;
            }
        }

        private decimal sensitivityDpsPerDigit;

        private const byte WhoAmIValue = 0xD4;

        public enum Registers
        {
            WhoaAmI = 0x0F,

            Control1 = 0x20,
            Control2 = 0x21,
            Control3 = 0x22,
            Control4 = 0x23,
            Control5 = 0x24,

            Reference = 0x25,
            TemperatureOutput = 0x26,
            Status = 0x27,

            OutputXLow = 0x28,
            OutputXHigh = 0x29,
            OutputYLow = 0x2A,
            OutputYHigh = 0x2B,
            OutputZLow = 0x2C,
            OutputZHigh = 0x2D,

            FifoControl = 0x2E,
            FifoSource = 0x2F,

            InterruptConfig = 0x30,
            InterruptSource = 0x31,
            InterruptThresholdXHigh = 0x32,
            InterruptThresholdXLow = 0x33,
            InterruptThresholdYHigh = 0x34,
            InterruptThresholdYLow = 0x35,
            InterruptThresholdZHigh = 0x36,
            InterruptThresholdZLow = 0x37,
            InterruptDuration = 0x38,
        }
    }
}