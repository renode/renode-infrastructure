//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals
{
    public interface IHasDivisibleFrequency : IHasFrequency
    {
        ulong Divider { get; set; }
    }
}