//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Time;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class MPC5645S_eDMATests
    {
        [SetUp]
        public void SetUp()
        {
            machine = new Machine();
            memory = new MappedMemory(machine, 0x1000);
            machine.SystemBus.Register(memory, new BusRangeRegistration(0x10000, 0x1000));
            dma = new MPC5645S_eDMA(machine, transferFrequency: 1000000);
            machine.SystemBus.Register(dma, new BusRangeRegistration(0x40000, 0x4000));
            mux = new MPC5645S_DMAMUX(dma);
            memory.WriteBytes(0, new byte[] { 0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87 });
        }

        [TearDown]
        public void TearDown()
        {
            machine.Dispose();
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(2, 0)]
        [TestCase(0, 2)]
        public void ShouldCopyBytesUsingClassicBigEndianTcdLayout(int sourceSize, int destinationSize)
        {
            ConfigureTransfer(sourceSize, destinationSize);
            Assert.AreEqual((sourceSize << 24) | (destinationSize << 16) | (1 << sourceSize), dma.ReadDoubleWord(0x1004));
            Assert.AreEqual(0x00010000 | (1 << destinationSize), dma.ReadDoubleWord(0x1014));
            dma.WriteByte(0x1E, 0);
            Assert.True(dma.IsChannelActive(0));
            CollectionAssert.AreEqual(new byte[8], memory.ReadBytes(0x100, 8));
            Assert.False(dma.Connections[0].IsSet);
            Advance();
            var expected = new byte[] { 0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87 };
            CollectionAssert.AreEqual(expected, memory.ReadBytes(0x100, 8));
            CollectionAssert.AreEqual(expected, memory.ReadBytes(0, 8), "DMA must preserve the source");
            Assert.AreEqual(1, dma.CompletionCount);
            Assert.False(dma.IsChannelActive(0));
            Assert.AreEqual(0x10000, dma.ReadDoubleWord(0x1000));
            Assert.AreEqual(0x10100, dma.ReadDoubleWord(0x1010));
            Assert.AreEqual(1, dma.ReadWord(0x1014));
            Assert.AreEqual(0x82, dma.ReadWord(0x101E));
            Assert.True(dma.Connections[0].IsSet);
            dma.WriteByte(0x1C, 0);
            Assert.False(dma.Connections[0].IsSet);
        }

        [Test]
        public void ShouldStopAlwaysRequestAtMajorCompletionWithDreq()
        {
            ConfigureTransfer(2, 2);
            dma.WriteWord(0x101E, 0xA);
            mux.WriteByte(0, 0xB8);
            dma.WriteByte(0x18, 0);
            Assert.True(dma.IsChannelActive(0));
            Advance();
            Assert.AreEqual(1, dma.CompletionCount);
            Assert.AreEqual(0, dma.ReadDoubleWord(0x0C));
            Assert.False(dma.IsChannelActive(0));
            Advance();
            Assert.AreEqual(1, dma.CompletionCount);
        }

        [Test]
        public void ShouldRejectScatterGatherWithoutWritingMemory()
        {
            ConfigureTransfer(2, 2);
            dma.WriteByte(0x1A, 0);
            dma.WriteWord(0x101E, 0x12);
            dma.WriteByte(0x1E, 0);
            Assert.False(dma.IsChannelActive(0));
            Assert.AreEqual(3, dma.LastErrorReasonCode);
            Assert.True(dma.Error.IsSet);
            Advance();
            Assert.AreEqual(0, dma.CompletionCount);
            CollectionAssert.AreEqual(new byte[8], memory.ReadBytes(0x100, 8));
            dma.WriteByte(0x1D, 0);
            Assert.False(dma.Error.IsSet);
        }

        [Test]
        public void ShouldRejectUnmappedDestination()
        {
            ConfigureTransfer(2, 2);
            dma.WriteDoubleWord(0x1010, 0x20000);
            dma.WriteByte(0x1E, 0);
            Assert.AreEqual(2, dma.LastErrorReasonCode);
            Assert.False(dma.IsChannelActive(0));
            Assert.AreEqual(0, dma.CompletionCount);
        }

        [Test]
        public void ShouldCancelPendingCopyOnReset()
        {
            ConfigureTransfer(2, 2);
            dma.WriteByte(0x1E, 0);
            Assert.True(dma.IsChannelActive(0));
            dma.Reset();
            Advance();
            CollectionAssert.AreEqual(new byte[8], memory.ReadBytes(0x100, 8));
            Assert.AreEqual(0, dma.CompletionCount);
            Assert.False(dma.IsChannelActive(0));
        }

        [Test]
        public void ShouldKeepMuxByteLanesIndependent()
        {
            mux.WriteDoubleWord(0, 0x81828384);
            mux.WriteWord(1, 0x9596);
            Assert.AreEqual(0x81959684, mux.ReadDoubleWord(0));
            Assert.AreEqual(0x95, dma.GetMuxConfiguration(1));
            Assert.AreEqual(0x96, dma.GetMuxConfiguration(2));
            mux.Reset();
            Assert.AreEqual(0, mux.ReadDoubleWord(0));
            Assert.AreEqual(0, dma.GetMuxConfiguration(1));
        }

        private void ConfigureTransfer(int sourceSize, int destinationSize)
        {
            // Classic MPC5645S TCD: ATTR before SOFF, CITER before DOFF,
            // and BITER before CSR (not the little-endian eDMA layout).
            dma.WriteDoubleWord(0x1000, 0x10000);
            dma.WriteWord(0x1004, (ushort)((sourceSize << 8) | destinationSize));
            dma.WriteWord(0x1006, (ushort)(1 << sourceSize));
            dma.WriteDoubleWord(0x1008, 8);
            dma.WriteDoubleWord(0x100C, 0xFFFFFFF8);
            dma.WriteDoubleWord(0x1010, 0x10100);
            dma.WriteWord(0x1014, 1);
            dma.WriteWord(0x1016, (ushort)(1 << destinationSize));
            dma.WriteDoubleWord(0x1018, 0xFFFFFFF8);
            dma.WriteWord(0x101C, 1);
            dma.WriteWord(0x101E, 2);
        }

        private void Advance()
        {
            ((BaseClockSource)machine.ClockSource).Advance(TimeInterval.FromMicroseconds(100));
        }

        private Machine machine;
        private MappedMemory memory;
        private MPC5645S_eDMA dma;
        private MPC5645S_DMAMUX mux;
    }
}
