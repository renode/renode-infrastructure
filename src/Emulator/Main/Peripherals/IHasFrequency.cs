//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals
{
    public interface IHasFrequency
    {
        long Frequency { get; set; }
    }
}
