//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Sensor
{
    public interface IMagneticSensor : ISensor
    {
        // nano Tesla
        int MagneticFluxDensityX { get; set; }
        int MagneticFluxDensityY { get; set; }
        int MagneticFluxDensityZ { get; set; }
    }
}
