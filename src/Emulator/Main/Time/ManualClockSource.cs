//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Time
{
    public sealed class ManualClockSource : BaseClockSource
    {
        public void AdvanceBySeconds(long seconds)
        {
            Advance(Utilities.SecondsToTicks(seconds));
        }
    }
}
