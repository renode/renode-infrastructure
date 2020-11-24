//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 

namespace Antmicro.Renode.Peripherals.Sensors
{
    public abstract class SensorSample 
    {
        public abstract bool TryLoad(params string[] data);
    }
}
