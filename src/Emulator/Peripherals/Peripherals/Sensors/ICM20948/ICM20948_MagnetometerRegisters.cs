//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public partial class ICM20948
    {
        private void DefineMagnetometerRegisters()
        {
            MagnetometerRegisters.DeviceID.Define(magnetometerRegisters)
                .WithReservedBits(0, 8)
            ;

            MagnetometerRegisters.Status1.Define(magnetometerRegisters)
                .WithReservedBits(2, 6)
                .WithTag("DOR", 1, 1)
                .WithTag("DRDY", 0, 1)
            ;

            MagnetometerRegisters.XAxisMeasurementDataLower.Define(magnetometerRegisters)
                .WithTag("HX_Low", 0, 8)
            ;

            MagnetometerRegisters.XAxisMeasurementDataUpper.Define(magnetometerRegisters)
                .WithTag("HX_High", 0, 8)
            ;

            MagnetometerRegisters.YAxisMeasurementDataLower.Define(magnetometerRegisters)
                .WithTag("HY_Low", 0, 8)
            ;

            MagnetometerRegisters.YAxisMeasurementDataUpper.Define(magnetometerRegisters)
                .WithTag("HY_High", 0, 8)
            ;

            MagnetometerRegisters.ZAxisMeasurementDataLower.Define(magnetometerRegisters)
                .WithTag("ZY_Low", 0, 8)
            ;

            MagnetometerRegisters.ZAxisMeasurementDataUpper.Define(magnetometerRegisters)
                .WithTag("ZY_High", 0, 8)
            ;

            MagnetometerRegisters.Status2.Define(magnetometerRegisters)
                .WithReservedBits(7, 1)
                .WithTag("RSV30", 6, 1)
                .WithTag("RCV29", 5, 1)
                .WithTag("RSV28", 4, 1)
                .WithTag("HOFL", 3, 1)
                .WithReservedBits(0, 3)
            ;

            MagnetometerRegisters.Control2.Define(magnetometerRegisters)
                .WithReservedBits(5, 3)
                .WithTag("MODE4", 4, 1)
                .WithTag("MODE3", 3, 1)
                .WithTag("MODE2", 2, 1)
                .WithTag("MODE1", 1, 1)
                .WithTag("MODE0", 0, 1)
            ;

            MagnetometerRegisters.Control3.Define(magnetometerRegisters)
                .WithReservedBits(1, 7)
                .WithTag("SRST", 0, 1)
            ;

            MagnetometerRegisters.Test1.Define(magnetometerRegisters)
                .WithReservedBits(0, 8)
            ;

            MagnetometerRegisters.Test2.Define(magnetometerRegisters)
                .WithReservedBits(0, 8)
            ;
        }

        private enum MagnetometerRegisters : byte
        {
            DeviceID = 0x1,
            Status1 = 0x10,
            XAxisMeasurementDataLower = 0x11,
            XAxisMeasurementDataUpper = 0x12,
            YAxisMeasurementDataLower = 0x13,
            YAxisMeasurementDataUpper = 0x14,
            ZAxisMeasurementDataLower = 0x15,
            ZAxisMeasurementDataUpper = 0x16,
            Status2 = 0x18,
            Control2 = 0x31,
            Control3 = 0x32,
            Test1 = 0x33,
            Test2 = 0x34
        }
    }
}
