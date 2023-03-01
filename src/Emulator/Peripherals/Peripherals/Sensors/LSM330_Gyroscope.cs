//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;

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
            Registers.Control4.Define(this)
                .WithTaggedFlag("SIM", 0)
                .WithReservedBits(1, 3)
                .WithValueField(4, 2, name: "FS: Full-scale selection", writeCallback: (_, val) => UpdateSensitivity((uint)val))
                .WithTaggedFlag("BLE", 6)
                .WithTaggedFlag("BDU", 7)
            ;

            Registers.OutputXLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_L_G", valueProviderCallback: _ => GetScaledValue(AngularRateX, sensitivity, false))
            ;

            Registers.OutputXHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_H_G", valueProviderCallback: _ => GetScaledValue(AngularRateX, sensitivity, true))
            ;

            Registers.OutputYLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_L_G", valueProviderCallback: _ => GetScaledValue(AngularRateY, sensitivity, false))
            ;

            Registers.OutputYHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_H_G", valueProviderCallback: _ => GetScaledValue(AngularRateY, sensitivity, true))
            ;

            Registers.OutputZLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_L_G", valueProviderCallback: _ => GetScaledValue(AngularRateZ, sensitivity, false))
            ;

            Registers.OutputZHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_H_G", valueProviderCallback: _ => GetScaledValue(AngularRateZ, sensitivity, true))
            ;
        }

        private void UpdateSensitivity(uint val)
        {
            var range = 0;
            switch(val)
            {
                case 0:
                    range = 250;
                    break;
                case 1:
                    range = 500;
                    break;
                // yes, both cases 0b10 and 0b11 encodes scale of 2000 dps
                case 2:
                case 3:
                    range = 2000;
                    break;
                default:
                    this.Log(LogLevel.Warning, "Tried to set an unsupported sensitivity value: {0}", val);
                    return;
            }

            sensitivity = CalculateScale(-range, range, OutputWidth);
        }

        private short sensitivity;

        private const int OutputWidth = 16;

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
