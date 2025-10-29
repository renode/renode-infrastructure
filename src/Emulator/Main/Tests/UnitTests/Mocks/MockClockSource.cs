//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Time;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class MockClockSource : BaseClockSource
    {
        public void AdvanceBySeconds(ulong seconds)
        {
            Advance(TimeInterval.FromSeconds(seconds));
        }

        public void AdvanceByMicroseconds(ulong microseconds)
        {
            Advance(TimeInterval.FromMicroseconds(microseconds));
        }

        public void AdvanceByMilliseconds(ulong miliseconds)
        {
            Advance(TimeInterval.FromMilliseconds(miliseconds));
        }

        public void AdvanceByNanoseconds(ulong nanoseconds)
        {
            Advance(TimeInterval.FromNanoseconds(nanoseconds));
        }
    }
}
