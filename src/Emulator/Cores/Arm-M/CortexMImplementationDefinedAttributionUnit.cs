//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class CortexMImplementationDefinedAttributionUnit
    {
        public abstract bool AttributionCheckCallback(uint address, bool secure, AccessType type, int accessWidth, out int region, out SecurityAttribution attribution);

        protected CortexMImplementationDefinedAttributionUnit(CortexM cpu)
        {
            cpu.ImplementationDefinedAttributionUnit = this;
        }
    }

    public enum SecurityAttribution
    {
        NonSecure         = 0,
        NonSecureCallable = 1,
        Secure            = 2,
    }
}
