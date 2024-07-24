//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class SegmentMapping
    {
        public IMappedSegment Segment { get; }
        public bool Touched { get; set; }

        public SegmentMapping(IMappedSegment segment)
        {
            Segment = segment;
        }
    }
}
