//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Sensor;

namespace Antmicro.Renode.Peripherals.Sensors
{
    // This model is a stub, roughly compatible with TMP103
    public class TMP108 : TMP103
    {
        public TMP108(IMachine machine) : base(machine)
        {
        }
    }
}
