//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Sensor
{
    public interface IHumiditySensor : ISensor
    {
        // percentage value, range 0-100
        decimal Humidity { get; set; }
    }
}
