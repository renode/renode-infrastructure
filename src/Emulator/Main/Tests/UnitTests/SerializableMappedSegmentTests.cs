//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Migrant;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class SerializableMappedSegmentTests
    {
        [Test]
        public void ShouldSerializeWhenNotTouched()
        {
            var testedPeripheral = new PeripheralWithMappedSegment();
            var copy = Serializer.DeepClone(testedPeripheral);
            var segments = copy.MappedSegments.ToArray();
            foreach(var segment in segments)
            {
                Assert.AreEqual(IntPtr.Zero, segment.Pointer);
            }
        }

        [Test]
        public void ShouldSerializeWhenTouched()
        {
            var testedPeripheral = new PeripheralWithMappedSegment();
            testedPeripheral.Touch();
            var copy = Serializer.DeepClone(testedPeripheral);
            var segments = copy.MappedSegments.ToArray();
            foreach(var segment in segments)
            {
                Assert.AreNotEqual(IntPtr.Zero, segment.Pointer);
            }
        }
    }

    public sealed class PeripheralWithMappedSegment : IBytePeripheral, IMapped
    {
        public PeripheralWithMappedSegment()
        {
            segments = new[] { new SerializableMappedSegment(4096, 0), new SerializableMappedSegment(4096, 8192) };
        }

        public IEnumerable<IMappedSegment> MappedSegments
        {
            get
            {
                return segments;
            }
        }

        public void Touch()
        {
            foreach(var s in segments)
            {
                s.Touch();
            }
        }

        public void Reset()
        {

        }

        public byte ReadByte(long offset)
        {
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {

        }

        private readonly SerializableMappedSegment[] segments;

    }
}

