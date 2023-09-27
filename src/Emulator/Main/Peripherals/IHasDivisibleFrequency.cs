//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals
{
    public interface IHasDivisibleFrequency : IHasFrequency
    {
        int Divider { get; set; }
    }
}
