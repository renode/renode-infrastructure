//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.Timers
{
    [Icon("clock")]
    public interface ITimer : IHasFrequency
    {
        ulong Value { get; set; }
        bool Enabled { get; set; }
    }
}

