//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class AK09916 : AK0991x
    {
        public AK09916(IMachine machine) : base(machine)
        {
        }

        public override byte CompanyID => 0x48;
        public override byte DeviceID => 0xc;
    }
}
