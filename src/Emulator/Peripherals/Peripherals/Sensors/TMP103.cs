//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class TMP103 : TMP108
    {
        public TMP103(IMachine machine) : base(machine)
        { }

        protected override int MaxAccessSize => 1;

        protected override bool HysteresisEnabled => false;
    }
}
