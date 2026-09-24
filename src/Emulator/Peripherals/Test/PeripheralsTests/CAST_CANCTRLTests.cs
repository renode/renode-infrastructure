//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CAN;

using NUnit.Framework;

namespace Antmicro.Renode.PeripheralsTests;

[TestFixture]
public class CAST_CANCTRLTests
{
    [SetUp]
    public void SetUp()
    {
        machine = new Machine();
        can = new CAST_CANCTRL(machine);
        can.WriteByte((long)CAST_CANCTRL.Registers.ConfigurationAndStatus, 0x00);
    }

    [Test]
    public void ShouldExposeDocumentedRegistersAndResetValues()
    {
        can.Reset();
        Assert.AreEqual(0x80, can.ReadByte((long)CAST_CANCTRL.Registers.ConfigurationAndStatus));
        Assert.AreEqual(0x84, can.ReadByte((long)CAST_CANCTRL.Registers.TransmitControl));
        Assert.AreEqual(0xFE, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptEnable));
        Assert.AreEqual(0x1B, can.ReadByte((long)CAST_CANCTRL.Registers.WarningLimits));
        Assert.AreEqual(0x03, can.ReadByte((long)CAST_CANCTRL.Registers.SlowSpeedBitTiming1));
        Assert.AreEqual(0x02, can.ReadByte((long)CAST_CANCTRL.Registers.SlowSpeedBitTiming2));
        Assert.AreEqual(0x02, can.ReadByte((long)CAST_CANCTRL.Registers.SlowSpeedBitTiming3));
        Assert.AreEqual(0x01, can.ReadByte((long)CAST_CANCTRL.Registers.SlowSpeedPrescaler));
        Assert.AreEqual(0x02, can.ReadByte((long)CAST_CANCTRL.Registers.CiA603TimeStampConfig));
        Assert.AreEqual(0x01, can.ReadByte((long)CAST_CANCTRL.Registers.AcceptanceFilterEnable0));
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.AcceptanceFilterEnable1));
        Assert.AreEqual(0x09, can.ReadByte((long)CAST_CANCTRL.Registers.Version0));
        Assert.AreEqual(0x07, can.ReadByte((long)CAST_CANCTRL.Registers.Version1));
        Assert.AreEqual(0x01, can.ReadByte((long)CAST_CANCTRL.Registers.SpatialRedundancyConfiguration));

        // ACFCTRL.SELMASK selects the mask bank, whose first filter accepts every ID.
        can.WriteByte((long)CAST_CANCTRL.Registers.AcceptanceFilterControl, 0x20);
        Assert.AreEqual(0xFF, can.ReadByte((long)CAST_CANCTRL.Registers.AcceptanceFilterCodeMask));
        Assert.AreEqual(0xFF, can.ReadByte((long)CAST_CANCTRL.Registers.AcceptanceFilterCodeMask + 1));
        Assert.AreEqual(0xFF, can.ReadByte((long)CAST_CANCTRL.Registers.AcceptanceFilterCodeMask + 2));
        Assert.AreEqual(0x1F, can.ReadByte((long)CAST_CANCTRL.Registers.AcceptanceFilterCodeMask + 3));

        can.WriteByte((long)CAST_CANCTRL.Registers.AcceptanceFilterCodeMask, 0x55);
        can.WriteByte((long)CAST_CANCTRL.Registers.AcceptanceFilterControl, 0x00); // select the code view
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.AcceptanceFilterCodeMask));
        can.WriteByte((long)CAST_CANCTRL.Registers.AcceptanceFilterCodeMask, 0xAA);
        can.WriteByte((long)CAST_CANCTRL.Registers.AcceptanceFilterControl, 0x20); // mask view preserves its own value
        Assert.AreEqual(0x55, can.ReadByte((long)CAST_CANCTRL.Registers.AcceptanceFilterCodeMask));
    }

    [Test]
    public void ShouldTransmitFullLengthFdFrame()
    {
        CANMessageFrame sent = null;
        can.FrameSent += frame => sent = frame;
        // TBUF[0:3] is the identifier, TBUF[4] contains IDE, FDF, BRS and DLC.
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer, 0x67);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 1, 0x45);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 2, 0x23);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 3, 0x01);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 4, 0xBF);
        foreach(var i in Enumerable.Range(0, 64))
        {
            can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 8 + i, (byte)(i + 1));
        }

        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitCommand, 0x10); // TCMD.TPE

        Assert.NotNull(sent);
        Assert.AreEqual(0x1234567u, sent.Id);
        Assert.IsTrue(sent.ExtendedFormat);
        Assert.IsTrue(sent.FDFormat);
        Assert.IsTrue(sent.BitRateSwitch);
        CollectionAssert.AreEqual(Enumerable.Range(1, 64).Select(i => (byte)i), sent.Data);
        Assert.AreEqual(0x08, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag)); // RTIF.TPIF
        Assert.IsTrue(can.IRQ.IsSet);

        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag, 0x08);
        Assert.AreEqual(0, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        Assert.IsFalse(can.IRQ.IsSet);
    }

    [Test]
    public void ShouldReceiveRemoteFrameLengthWithoutPayload()
    {
        // Data.Length represents the requested response length.
        can.OnFrameReceived(new CANMessageFrame(0x123, Enumerable.Repeat((byte)0xA5, 4).ToArray(), remoteFrame: true));

        Assert.AreEqual(0x44, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 4));
        Assert.IsTrue(Enumerable.Range(0, 8).All(i => can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 8 + i) == 0));
    }

    [Test]
    public void ShouldDiscardSecondaryRequestsOnAnEmptyBuffer()
    {
        CANMessageFrame sent = null;
        can.FrameSent += frame => sent = frame;
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitCommand, 0x80); // TCMD.TBSEL selects STB
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer, 0x23);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 1, 0x01);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 4, 0x02);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 8, 0xA5);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 9, 0x5A);

        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitCommand, 0x84); // TSONE before TSNEXT has no slot to transmit
        Assert.AreEqual(0x80, can.ReadByte((long)CAST_CANCTRL.Registers.TransmitCommand));
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitCommand, 0x82); // TSALL on an empty STB also clears
        Assert.AreEqual(0x80, can.ReadByte((long)CAST_CANCTRL.Registers.TransmitCommand));
        Assert.IsNull(sent);

        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitControl, 0x40); // TCTRL.TSNEXT
        Assert.IsNull(sent);
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitCommand, 0x84); // request the filled slot

        Assert.NotNull(sent);
        Assert.AreEqual(0x123u, sent.Id);
        CollectionAssert.AreEqual(new byte[] { 0xA5, 0x5A }, sent.Data);
        Assert.AreEqual(0x04, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag)); // RTIF.TSIF
    }

    [Test]
    public void ShouldAcceptTsnextAndTsoneInTheSameDoubleWordWrite()
    {
        ulong BusOffset(CAST_CANCTRL.Registers offset)
        {
            return (ulong)offset + 0x1000;
        }

        machine.SystemBus.Register(can, BusOffset(0).By(0x1000));
        CANMessageFrame sent = null;
        can.FrameSent += frame => sent = frame;
        machine.SystemBus.WriteByte(BusOffset(CAST_CANCTRL.Registers.TransmitCommand), 0x80); // select STB
        machine.SystemBus.WriteDoubleWord(BusOffset(CAST_CANCTRL.Registers.TransmitBuffer), 0x123);
        machine.SystemBus.WriteDoubleWord(BusOffset(CAST_CANCTRL.Registers.TransmitBuffer + 4), 0x01);
        machine.SystemBus.WriteDoubleWord(BusOffset(CAST_CANCTRL.Registers.TransmitBuffer + 8), 0xAB);

        // TSNEXT and TSONE take effect together, with RESET cleared.
        machine.SystemBus.WriteDoubleWord(BusOffset(CAST_CANCTRL.Registers.ConfigurationAndStatus), 0x00408400);

        Assert.NotNull(sent);
        Assert.AreEqual(0x123u, sent.Id);
        CollectionAssert.AreEqual(new byte[] { 0xAB }, sent.Data);
        Assert.AreEqual(0x04, machine.SystemBus.ReadByte(BusOffset(CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag)));
    }

    [Test]
    public void ShouldAbortQueuedSecondaryFramesWithoutTransmitting()
    {
        CANMessageFrame sent = null;
        can.FrameSent += frame => sent = frame;
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitCommand, 0x80); // select STB
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer, 0x23);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 4, 0x01);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 8, 0xAB);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitControl, 0x40); // TSNEXT fills the slot
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitControl, 0x40); // another slot
        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptEnable, 0x00);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitCommand, 0x83); // TSALL | TSA clears the entire STB

        Assert.IsNull(sent);
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.TransmitControl) & 0x43);
        Assert.AreEqual(0x80, can.ReadByte((long)CAST_CANCTRL.Registers.TransmitCommand));
        Assert.AreEqual(0x01, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag)); // AIF has no enable bit
        Assert.IsTrue(can.IRQ.IsSet);
        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag, 0x01);
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        Assert.IsFalse(can.IRQ.IsSet);
    }

    [Test]
    public void ShouldKeepPrimaryBufferWhenWritingSecondaryBuffer()
    {
        var sent = new List<CANMessageFrame>();
        can.FrameSent += sent.Add;
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer, 0x11);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 4, 0x01);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 8, 0xAA);

        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitCommand, 0x80); // select STB
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer, 0x22);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 4, 0x01);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitBuffer + 8, 0xBB);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitControl, 0x40); // TSNEXT
        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptEnable, 0x00);
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitCommand, 0x84); // TSONE
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitCommand, 0x00); // select PTB
        can.WriteByte((long)CAST_CANCTRL.Registers.TransmitCommand, 0x10); // TPE

        Assert.AreEqual(2, sent.Count);
        Assert.AreEqual(0x22u, sent[0].Id);
        CollectionAssert.AreEqual(new byte[] { 0xBB }, sent[0].Data);
        Assert.AreEqual(0x11u, sent[1].Id);
        CollectionAssert.AreEqual(new byte[] { 0xAA }, sent[1].Data);
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptEnable, 0x0C);
        Assert.IsFalse(can.IRQ.IsSet);
    }

    [Test]
    public void ShouldOnlyLatchReceiveFlagWhenInterruptWasEnabledAtReception()
    {
        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptEnable, 0x00);
        can.OnFrameReceived(new CANMessageFrame(0x123, new byte[] { 0xAA }));
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        Assert.IsFalse(can.IRQ.IsSet);

        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptEnable, 0x80);
        Assert.IsFalse(can.IRQ.IsSet);

        can.OnFrameReceived(new CANMessageFrame(0x124, new byte[] { 0xBB }));
        Assert.AreEqual(0x80, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        Assert.IsTrue(can.IRQ.IsSet);

        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag, 0x80);
        Assert.IsFalse(can.IRQ.IsSet);
    }

    [Test]
    public void ShouldExposeOldestReceivedFrameAndAdvanceOnRelease()
    {
        var fdData = Enumerable.Range(1, 12).Select(i => (byte)i).ToArray();
        can.OnFrameReceived(new CANMessageFrame(0x321, new byte[] { 0xAA, 0xBB }));
        can.OnFrameReceived(new CANMessageFrame(0x1234567, fdData, extendedFormat: true, fdFormat: true, bitRateSwitch: true));

        Assert.AreEqual(0x02, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x03); // AFWL=1 means two slots
        Assert.AreEqual(0x21, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer));
        Assert.AreEqual(0x03, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 1));
        Assert.AreEqual(0x02, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 4));
        Assert.AreEqual(0xAA, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 8));
        Assert.AreEqual(0xBB, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 9));

        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveControl, 0x10); // RREL

        Assert.AreEqual(0x01, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x03);
        Assert.AreEqual(0x67, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer));
        Assert.AreEqual(0x45, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 1));
        Assert.AreEqual(0x23, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 2));
        Assert.AreEqual(0x01, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 3));
        Assert.AreEqual(0xB9, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 4)); // IDE, FDF, BRS, DLC=9
        CollectionAssert.AreEqual(fdData, Enumerable.Range(0, 12)
            .Select(i => can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 8 + i)));

        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveControl, 0x10);
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x03);
    }

    [Test]
    public void ShouldApplyAcceptanceMaskAndFrameType()
    {
        var filter = (long)CAST_CANCTRL.Registers.AcceptanceFilterCodeMask;
        can.WriteByte((long)CAST_CANCTRL.Registers.AcceptanceFilterEnable0, 0x00);
        can.OnFrameReceived(new CANMessageFrame(0x123, new byte[] { 0xAA }));
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x03);

        // Filter 0 compares all 11 standard ID bits and accepts standard frames only.
        can.WriteByte((long)CAST_CANCTRL.Registers.ConfigurationAndStatus, 0x80);
        can.WriteByte(filter, 0x23);
        can.WriteByte(filter + 1, 0x01);
        can.WriteByte((long)CAST_CANCTRL.Registers.AcceptanceFilterControl, 0x20); // mask view
        can.WriteByte(filter, 0x00);
        can.WriteByte(filter + 1, 0xF8);
        can.WriteByte(filter + 2, 0xFF);
        can.WriteByte(filter + 3, 0x5F); // AIDEE=1, AIDE=0
        can.WriteByte((long)CAST_CANCTRL.Registers.AcceptanceFilterEnable0, 0x01);
        can.WriteByte((long)CAST_CANCTRL.Registers.ConfigurationAndStatus, 0x00);

        // Filter programming is locked outside reset.
        can.WriteByte(filter, 0xFF);
        Assert.AreEqual(0x00, can.ReadByte(filter));

        can.OnFrameReceived(new CANMessageFrame(0x124, new byte[] { 0xBB }));
        can.OnFrameReceived(new CANMessageFrame(0x123, new byte[] { 0xCC }, extendedFormat: true));
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x03);
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));

        can.OnFrameReceived(new CANMessageFrame(0x123, new byte[] { 0xDD }));
        Assert.AreEqual(0x01, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x03);
        Assert.AreEqual(0xDD, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer + 8));
    }

    [Test]
    public void ShouldSignalAlmostFullAtScaledWarningLimit()
    {
        can.WriteByte((long)CAST_CANCTRL.Registers.WarningLimits, 0x2B); // AFWL=2, threshold=4 for 16 slots
        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptEnable, 0x10); // RAFIE only
        for(var id = 0u; id < 3; ++id)
        {
            can.OnFrameReceived(new CANMessageFrame(id, new byte[0]));
        }
        Assert.AreEqual(0x01, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x03);
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        Assert.IsFalse(can.IRQ.IsSet);

        can.OnFrameReceived(new CANMessageFrame(3, new byte[0]));
        Assert.AreEqual(0x02, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x03);
        Assert.AreEqual(0x10, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        Assert.IsTrue(can.IRQ.IsSet);

        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag, 0x10);
        Assert.IsFalse(can.IRQ.IsSet);
        can.OnFrameReceived(new CANMessageFrame(4, new byte[0])); // above the threshold does not trigger it again
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveControl, 0x10);
        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveControl, 0x10);
        Assert.AreEqual(0x01, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x03);
    }

    [Test]
    public void ShouldHandleBothReceiveOverflowModesAndSoftwareReset()
    {
        for(var id = 0u; id < 16; ++id)
        {
            can.OnFrameReceived(new CANMessageFrame(id, new byte[0]));
        }
        Assert.AreEqual(0x03, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x03);
        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag, 0xF0);
        can.OnFrameReceived(new CANMessageFrame(16, new byte[0])); // ROM=0 replaces oldest
        Assert.AreEqual(0x23, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x23);
        Assert.AreEqual(0xE0, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        Assert.AreEqual(0x01, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer));
        Assert.IsTrue(can.IRQ.IsSet);

        can.WriteByte((long)CAST_CANCTRL.Registers.ConfigurationAndStatus, 0x80); // software reset
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x23);
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        Assert.IsFalse(can.IRQ.IsSet);
        can.OnFrameReceived(new CANMessageFrame(0x123, new byte[0]));
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x03);
        can.WriteByte((long)CAST_CANCTRL.Registers.ConfigurationAndStatus, 0x00);

        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveControl, 0x40); // ROM=1 discards newest
        for(var id = 0u; id < 16; ++id)
        {
            can.OnFrameReceived(new CANMessageFrame(id, new byte[0]));
        }
        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag, 0xF0);
        can.OnFrameReceived(new CANMessageFrame(16, new byte[0]));
        Assert.AreEqual(0x63, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x63);
        Assert.AreEqual(0x60, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveTransmitInterruptFlag));
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer));

        can.WriteByte((long)CAST_CANCTRL.Registers.ReceiveControl, 0x50); // retain ROM while releasing oldest
        Assert.AreEqual(0x00, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveControl) & 0x20); // ROV cleared
        Assert.AreEqual(0x01, can.ReadByte((long)CAST_CANCTRL.Registers.ReceiveBuffer));
    }

    [Test]
    public void ShouldAccessCounterWithBytes()
    {
        machine.SystemBus.Register(can, new BusMultiRegistration(0x2000, 0x100, "counter"));
        machine.SystemBus.WriteDoubleWord(0x2004, 0x12345678);
        Assert.AreEqual(0x56, machine.SystemBus.ReadByte(0x2005));

        machine.SystemBus.WriteByte(0x2005, 0xAB);
        Assert.AreEqual(0x1234AB78u, machine.SystemBus.ReadDoubleWord(0x2004));

        machine.SystemBus.WriteByte(0x2000, 0x04); // CNTR_CLEAR
        Assert.AreEqual(0u, machine.SystemBus.ReadDoubleWord(0x2004));
    }

    private Machine machine;
    private CAST_CANCTRL can;
}
