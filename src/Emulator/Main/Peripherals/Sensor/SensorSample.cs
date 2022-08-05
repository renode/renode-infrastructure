//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 

using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public abstract class SensorSample
    {
        public abstract void Load(IList<decimal> data);
        public abstract bool TryLoad(params string[] data);
    }
}
