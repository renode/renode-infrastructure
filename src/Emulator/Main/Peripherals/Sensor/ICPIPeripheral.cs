//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.I2C;

namespace Antmicro.Renode.Peripherals.Sensor
{
    public interface ICPIPeripheral : II2CPeripheral
    {
        byte[] ReadFrame();
    }
}
