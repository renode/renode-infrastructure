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
    public class LSM330_Accelerometer : ST_I2CSensorBase<LSM330_Accelerometer.Registers>
    {
        public LSM330_Accelerometer()
        {
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            UpdateSensitivity(0);
        }

        public decimal AccelerationX { get; set; }

        public decimal AccelerationY { get; set; }

        public decimal AccelerationZ { get; set; }

        protected override void DefineRegisters()
        {
            Registers.Control6.Define(this)
                .WithTaggedFlag("SIM", 0)
                .WithReservedBits(1, 2)
                .WithValueField(3, 3, name: "FSCALE: Full-scale selection", writeCallback: (_, val) => UpdateSensitivity((uint)val))
                .WithTag("BW", 6, 2)
            ;

            Registers.OutputXLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_L_A", valueProviderCallback: _ => GetScaledValue(AccelerationX, sensitivity, false))
            ;

            Registers.OutputXHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_H_A", valueProviderCallback: _ => GetScaledValue(AccelerationX, sensitivity, true))
            ;

            Registers.OutputYLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_L_A", valueProviderCallback: _ => GetScaledValue(AccelerationY, sensitivity, false))
            ;

            Registers.OutputYHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_H_A", valueProviderCallback: _ => GetScaledValue(AccelerationY, sensitivity, true))
            ;

            Registers.OutputZLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_L_A", valueProviderCallback: _ => GetScaledValue(AccelerationZ, sensitivity, false))
            ;

            Registers.OutputZHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_H_A", valueProviderCallback: _ => GetScaledValue(AccelerationZ, sensitivity, true))
            ;
        }

        private void UpdateSensitivity(uint val)
        {
            var range = 0;
            switch(val)
            {
                case 0:
                    range = 2;
                    break;
                case 1:
                    range = 4;
                    break;
                case 2:
                    range = 6;
                    break;
                case 3:
                    range = 8;
                    break;
                case 4:
                    range = 16;
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

            // yes, according to the documentation those are out-of-order;
            // don't ask
            Control5 = 0x20,
            Control4 = 0x23,
            Control6 = 0x24,
            Control7 = 0x25,

            AxisOffsetCorrectionX = 0x10,
            AxisOffsetCorrectionY = 0x11,
            AxisOffsetCorrectionZ = 0x12,

            ConstantShiftX = 0x13,
            ConstantShiftY = 0x14,
            ConstantShiftZ = 0x15,

            LongCounterLow = 0x16,
            LongCounterHigh = 0x17,

            InterruptSync = 0x18,
            SM1_Peak = 0x19,
            SM2_Peak = 0x1A,

            VectorFilterCoefficient1 = 0x1B,
            VectorFilterCoefficient2 = 0x1C,
            VectorFilterCoefficient3 = 0x1D,
            VectorFilterCoefficient4 = 0x1E,

            Threshold3 = 0x1F,

            Control2 = 0x21,
            Control3 = 0x22,

            OutputXLow = 0x28,
            OutputXHigh = 0x29,
            OutputYLow = 0x2A,
            OutputYHigh = 0x2B,
            OutputZLow = 0x2C,
            OutputZHigh = 0x2D,

            FifoControl = 0x2E,
            FifoSource = 0x2F,

            // State machine 1 registers
            SM1_OpCode01 = 0x40,
            SM1_OpCode02 = 0x41,
            SM1_OpCode03 = 0x42,
            SM1_OpCode04 = 0x43,
            SM1_OpCode05 = 0x44,
            SM1_OpCode06 = 0x45,
            SM1_OpCode07 = 0x46,
            SM1_OpCode08 = 0x47,
            SM1_OpCode09 = 0x48,
            SM1_OpCode10 = 0x49,
            SM1_OpCode11 = 0x4A,
            SM1_OpCode12 = 0x4B,
            SM1_OpCode13 = 0x4C,
            SM1_OpCode14 = 0x4D,
            SM1_OpCode15 = 0x4E,
            SM1_OpCode16 = 0x4F,

            SM1_Timer4 = 0x50,
            SM1_Timer3 = 0x51,
            SM1_Timer2Low = 0x52,
            SM1_Timer2High = 0x53,
            SM1_Timer1Low = 0x54,
            SM1_Timer1High = 0x55,

            SM1_Threshold2 = 0x56,
            SM1_Threshold1 = 0x57,

            SM1_AxisSignMaskB = 0x59,
            SM1_AxisSignMaskA = 0x5A,

            SM1_Settings = 0x5B,
            SM1_ProgramReset = 0x5C,

            SM1_TimerCounterLow = 0x5D,
            SM1_TimerCounterHigh = 0x5E,

            SM1_OutputFlags = 0x5F,

            // State machine 2 registers
            SM2_OpCode01 = 0x60,
            SM2_OpCode02 = 0x61,
            SM2_OpCode03 = 0x62,
            SM2_OpCode04 = 0x63,
            SM2_OpCode05 = 0x64,
            SM2_OpCode06 = 0x65,
            SM2_OpCode07 = 0x66,
            SM2_OpCode08 = 0x67,
            SM2_OpCode09 = 0x68,
            SM2_OpCode10 = 0x69,
            SM2_OpCode11 = 0x6A,
            SM2_OpCode12 = 0x6B,
            SM2_OpCode13 = 0x6C,
            SM2_OpCode14 = 0x6D,
            SM2_OpCode15 = 0x6E,
            SM2_OpCode16 = 0x6F,

            SM2_Timer4 = 0x70,
            SM2_Timer3 = 0x71,
            SM2_Timer2Low = 0x72,
            SM2_Timer2High = 0x73,
            SM2_Timer1Low = 0x74,
            SM2_Timer1High = 0x75,

            SM2_Threshold2 = 0x76,
            SM2_Threshold1 = 0x77,

            SM2_Decimation = 0x78,

            SM2_AxisSignMaskB = 0x79,
            SM2_AxisSignMaskA = 0x7A,

            SM2_Settings = 0x7B,
            SM2_ProgramReset = 0x7C,

            SM2_TimerCounterLow = 0x7D,
            SM2_TimerCounterHigh = 0x7E,

            SM2_OutputFlags = 0x7F,
        }
    }
}
