//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class MemoryTests
    {
        [Test]
        public void ShouldReadWriteMemoryBiggerThan2GB()
        {
            const uint MemorySize = 3u * 1024 * 1024 * 1024;
            var machine = new Machine();
            var memory = new MappedMemory(machine, MemorySize);
            var start = (ulong)100.MB();
            machine.SystemBus.Register(memory, start);
            var offset1 = start + 16;
            var offset2 = start + MemorySize - 16;
            machine.SystemBus.WriteByte(offset1, 0x1);
            machine.SystemBus.WriteByte(offset2, 0x2);

            Assert.AreEqual(0x1, machine.SystemBus.ReadByte(offset1));
            Assert.AreEqual(0x2, machine.SystemBus.ReadByte(offset2));
        }
    }
}

