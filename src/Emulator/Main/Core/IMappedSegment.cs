//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Core
{
    public interface IMappedSegment
    {
        IntPtr Pointer { get; }
        ulong StartingOffset { get; }
        ulong Size { get; }
        void Touch();
    }

    public static class IMappedSegmentExtensions
    {
        public static Range GetRange(this IMappedSegment segment)
        {
            return new Range(segment.StartingOffset, segment.Size);
        }
    }
}

