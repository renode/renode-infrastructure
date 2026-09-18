//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CAN;
using Antmicro.Renode.Tools.Network;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class CANHubTests
    {
        [SetUp]
        public void SetUp()
        {
            EmulationManager.Instance.Clear();
            // Deliver time domain events synchronously so the test does not need a running time source.
            EmulationManager.Instance.CurrentEmulation.Mode = Emulation.EmulationMode.SynchronizedTimers;
            machine = new Machine();
            sender = new MockCAN();
            receiver = new MockCAN();
            machine.SystemBus.Register(sender, new BusRangeRegistration(0x0, 0x10));
            machine.SystemBus.Register(receiver, new BusRangeRegistration(0x10, 0x10));
            machine.SetLocalName(sender, "can0");
            machine.SetLocalName(receiver, "can1");
            EmulationManager.Instance.CurrentEmulation.AddMachine(machine);
            hub = new CANHub();
            EmulationManager.Instance.CurrentEmulation.ExternalsManager.AddExternal(hub, "hub");
            hub.AttachTo(sender);
            hub.AttachTo(receiver);
        }

        [TearDown]
        public void TearDown()
        {
            EmulationManager.Instance.Clear();
        }

        [Test]
        public void ShouldForwardFramesWhileRunning()
        {
            hub.Start();
            sender.Send(new CANMessageFrame(0x181, new byte[] { 1, 2 }));
            Assert.AreEqual(1, receiver.Received.Count);
            Assert.AreEqual(0x181u, receiver.Received[0].Id);
        }

        [Test]
        public void ShouldDeliverFramesReceivedWhilePausedOnResume()
        {
            // A host-side bridge (e.g. SocketCANBridge) keeps reading its socket while the emulation is
            // paused between RunFor calls. Those frames must not be lost; they are delivered on Resume.
            hub.Start();
            hub.Pause();
            sender.Send(new CANMessageFrame(0x181, new byte[] { 1 }));
            sender.Send(new CANMessageFrame(0x182, new byte[] { 2 }));
            Assert.AreEqual(0, receiver.Received.Count, "nothing may reach the machine while paused");

            hub.Resume();
            Assert.AreEqual(2, receiver.Received.Count);
            Assert.AreEqual(0x181u, receiver.Received[0].Id);
            Assert.AreEqual(0x182u, receiver.Received[1].Id);

            hub.Resume();
            Assert.AreEqual(2, receiver.Received.Count, "queued frames are delivered once");
        }

        [Test]
        public void ShouldNotEchoQueuedFrameToItsSender()
        {
            hub.Start();
            hub.Pause();
            sender.Send(new CANMessageFrame(0x181, new byte[] { 1 }));
            hub.Resume();
            Assert.AreEqual(0, sender.Received.Count);
            Assert.AreEqual(1, receiver.Received.Count);
        }

        private IMachine machine;
        private MockCAN sender;
        private MockCAN receiver;
        private CANHub hub;

        private sealed class MockCAN : ICAN, IDoubleWordPeripheral, IKnownSize
        {
            public void Send(CANMessageFrame frame)
            {
                FrameSent?.Invoke(frame);
            }

            public void OnFrameReceived(CANMessageFrame message)
            {
                Received.Add(message);
            }

            public void Reset()
            {
                Received.Clear();
            }

            public uint ReadDoubleWord(long offset)
            {
                return 0;
            }

            public void WriteDoubleWord(long offset, uint value)
            {
            }

            public List<CANMessageFrame> Received { get; } = new List<CANMessageFrame>();

            public long Size => 0x10;

            public event Action<CANMessageFrame> FrameSent;
        }
    }
}
