//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Analog;
using Antmicro.Renode.Peripherals.CAN;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Peripherals.Video;
using Antmicro.Renode.Time;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class MPC5645S_PeripheralTests
    {
        [SetUp]
        public void SetUp()
        {
            machine = new Machine();
        }

        [TearDown]
        public void TearDown()
        {
            machine.Dispose();
        }

        [Test]
        public void ShouldConvertSelectedAdcChannelAndClearInterrupt()
        {
            var adc = new MPC5645S_ADC(machine);
            adc.SetChannelValue(0, 0x155);
            adc.WriteDoubleWord(0xA8, 1);
            adc.WriteDoubleWord(0x20, 3);
            adc.WriteDoubleWord(0, 0x01000000);
            Assert.False(adc.EOC.IsSet);
            AdvanceMicroseconds(10);
            Assert.True(adc.EOC.IsSet);
            Assert.AreEqual(0x00080155, adc.ReadDoubleWord(0x180));
            Assert.AreEqual(3, adc.ReadDoubleWord(0x10));
            adc.WriteByte(0x10, 3);
            Assert.True(adc.EOC.IsSet, "Upper byte must not clear low status bits");
            adc.WriteByte(0x13, 3);
            Assert.False(adc.EOC.IsSet);
        }

        [Test]
        public void ShouldCommitDcuFrameAndClearStatusByByteLane()
        {
            var dcu = new MPC5645S_DCU(machine, frameFrequency: 1000);
            dcu.WriteDoubleWord(0x1D8, 0x00010001);
            dcu.WriteDoubleWord(0x1F0, 0);
            dcu.WriteByte(0x1D3, 1);
            Assert.AreEqual(0, dcu.CommittedFrameCount);
            AdvanceMicroseconds(1000);
            Assert.AreEqual(1, dcu.CommittedFrameCount);
            Assert.AreEqual(0x4008, dcu.ReadDoubleWord(0x1EC));
            Assert.True(dcu.IRQ.IsSet);
            dcu.WriteByte(0x1EE, 0x40);
            Assert.AreEqual(8, dcu.ReadDoubleWord(0x1EC));
            Assert.True(dcu.IRQ.IsSet);
            dcu.WriteByte(0x1EF, 8);
            Assert.False(dcu.IRQ.IsSet);
            dcu.WriteByte(0x1D3, 0);
            AdvanceMicroseconds(2000);
            Assert.AreEqual(1, dcu.CommittedFrameCount);
        }

        [TestCase(false, 0xFF000000u)]
        [TestCase(true, 0x02000000u)]
        public void ShouldReadQuadSpiStatusAfterDelayedCommand(bool flashPresent, uint expected)
        {
            var qspi = new MPC5645S_QuadSPI(machine);
            if(flashPresent)
            {
                qspi.AttachS25FL256S();
            }
            else
            {
                qspi.DetachFlash();
            }
            qspi.WriteDoubleWord(0, 0x10);
            qspi.WriteDoubleWord(0x100, 0x70000000);
            qspi.WriteDoubleWord(0x104, 6);
            Assert.AreEqual(0, qspi.CompletionCount);
            AdvanceMicroseconds(10);
            Assert.AreEqual(1, qspi.CompletionCount);
            qspi.WriteDoubleWord(0x104, 0x00010005);
            AdvanceMicroseconds(10);
            Assert.AreEqual(expected, qspi.ReadDoubleWord(0x200));
            Assert.AreEqual(2, qspi.CompletionCount);
            Assert.AreEqual(1, qspi.ReadDoubleWord(0x160) & 1);
            qspi.WriteByte(0x163, 1);
            Assert.AreEqual(0, qspi.ReadDoubleWord(0x160) & 1);
        }

        [Test]
        public void ShouldFilterFlexCanIdentifierAndStoreBigEndianPayload()
        {
            var can = new MPC5645S_FlexCAN(machine);
            can.WriteDoubleWord(0, 0xF);
            can.WriteDoubleWord(0x10, 0x1FFC0000);
            can.WriteDoubleWord(0x84, 0x123u << 18);
            can.WriteDoubleWord(0x80, 0x04000000);
            can.WriteDoubleWord(0x28, 1);
            can.InjectFrame(0x124, "11223344");
            Assert.AreEqual(0, can.ReceivedFrameCount);
            Assert.AreEqual(0, can.ReadDoubleWord(0x30));
            can.InjectFrame(0x123, "1122334455667788");
            Assert.AreEqual(1, can.ReceivedFrameCount);
            Assert.AreEqual(0x11223344, can.ReadDoubleWord(0x88));
            Assert.AreEqual(0x55667788, can.ReadDoubleWord(0x8C));
            Assert.AreEqual(0x02080000, can.ReadDoubleWord(0x80) & 0x0FFF0000);
            Assert.AreEqual(1, can.ReadDoubleWord(0x30));
            can.WriteByte(0x30, 1);
            Assert.AreEqual(1, can.ReadDoubleWord(0x30));
            can.WriteByte(0x33, 1);
            Assert.AreEqual(0, can.ReadDoubleWord(0x30));
        }

        private void AdvanceMicroseconds(ulong value)
        {
            ((BaseClockSource)machine.ClockSource).Advance(TimeInterval.FromMicroseconds(value));
        }

        private Machine machine;
    }
}
