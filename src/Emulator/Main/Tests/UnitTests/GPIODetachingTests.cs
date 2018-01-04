//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.UnitTests.Mocks;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class GPIODetachingTests
    {
        Machine machine;

        [SetUp]
        public void SetUp()
        {
            machine = new Machine();
        }

        [Test]
        public void ShouldGetAllGPIOConnections()
        {
            //init
            var gpioByNumberConnectorPeripheralMock = new MockGPIOByNumberConnectorPeripheral(1);
            var gpioReceiverMock = new MockReceiver();

            gpioByNumberConnectorPeripheralMock.Connections[0].Connect(gpioReceiverMock, 1);
            machine.SystemBus.Register(gpioByNumberConnectorPeripheralMock, new BusRangeRegistration(0x0, 0x10));
            machine.SystemBus.Register(gpioReceiverMock, new BusRangeRegistration(0x10, 0x10));

            //act
            var connections = gpioByNumberConnectorPeripheralMock.Connections;

            //assert
            Assert.AreEqual(1, connections.Count());
        }

        [Test]
        public void ShouldDetachConnectionFromGPIOByNumberConnection()
        {
            //init
            var gpioByNumberConnectorPeripheralMock = new MockGPIOByNumberConnectorPeripheral(1);
            var gpioReceiverMock = new MockReceiver();

            gpioByNumberConnectorPeripheralMock.Connections[0].Connect(gpioReceiverMock, 1);
            machine.SystemBus.Register(gpioByNumberConnectorPeripheralMock, new BusRangeRegistration(0x0, 0x10));
            machine.SystemBus.Register(gpioReceiverMock, new BusRangeRegistration(0x10, 0x10));

            //act
            machine.SystemBus.Unregister(gpioReceiverMock);

            //assert
            var connections = gpioByNumberConnectorPeripheralMock.Connections;
            Assert.IsNull(connections[0].Endpoint);
        }


        [Test]
        public void ShouldDetachOnlyOneConnectionFromGPIOByNumberConnection()
        {
            //init
            var gpioByNumberConnectorPeripheralMock = new MockGPIOByNumberConnectorPeripheral(3);
            var gpioReceiverMock = new MockReceiver();
            var gpioReceiverMock2 = new MockReceiver();
            var gpioReceiverMock3 = new MockReceiver();

            gpioByNumberConnectorPeripheralMock.Connections[0].Connect(gpioReceiverMock, 1);
            gpioByNumberConnectorPeripheralMock.Connections[1].Connect(gpioReceiverMock2, 2);
            gpioByNumberConnectorPeripheralMock.Connections[2].Connect(gpioReceiverMock3, 3);
            machine.SystemBus.Register(gpioByNumberConnectorPeripheralMock, new BusRangeRegistration(0x00, 0x10));
            machine.SystemBus.Register(gpioReceiverMock, new BusRangeRegistration(0x10, 0x10));
            machine.SystemBus.Register(gpioReceiverMock2, new BusRangeRegistration(0x20, 0x10));
            machine.SystemBus.Register(gpioReceiverMock3, new BusRangeRegistration(0x30, 0x10));

            //act
            machine.SystemBus.Unregister(gpioReceiverMock);

            //assert
            var connections = gpioByNumberConnectorPeripheralMock.Connections;
            Assert.IsNull(connections[0].Endpoint);
            Assert.IsNotNull(connections[1].Endpoint);
            Assert.IsNotNull(connections[2].Endpoint);

        }

        [Test]
        public void ShoulDisconnectGPIOSenderAttachedToGPIOReceiver()
        {
            //init
            var gpioReceiverMock = new MockReceiver();
            var gpioSender = new MockIrqSender();

            machine.SystemBus.Register(gpioReceiverMock, new BusRangeRegistration(0x0, 0x10));
            machine.SystemBus.Register(gpioSender, new BusRangeRegistration(0x10, 0x10));
            gpioSender.Irq.Connect(gpioReceiverMock, 1);

            //act
            machine.SystemBus.Unregister(gpioReceiverMock);
            //assert
            Assert.IsFalse(gpioSender.Irq.IsConnected);
        }

        [Test]
        public void ShouldUnregisterChainedPeripheralsOnBDisconnect()
        {
            //A -> B -> C, B -> D and A -> C
            //B is disconnected

            //init
            var A = new MockGPIOByNumberConnectorPeripheral(2);
            var B = new MockGPIOByNumberConnectorPeripheral(2);
            var C = new MockReceiver();
            var D = new MockReceiver();

            machine.SystemBus.Register(A, new BusRangeRegistration(0x0, 0x10));
            machine.SystemBus.Register(B, new BusRangeRegistration(0x10, 0x10));
            machine.SystemBus.Register(C, new BusRangeRegistration(0x20, 0x10));
            machine.SystemBus.Register(D, new BusRangeRegistration(0x30, 0x10));

            A.Connections[0].Connect(B, 1);
            B.Connections[0].Connect(C, 1);
            B.Connections[1].Connect(D, 1);
            A.Connections[1].Connect(C, 2);

            //act
            machine.SystemBus.Unregister(B);
            var AConnections = A.Connections;
            var BConnections = B.Connections;

            //assert
            Assert.IsNull(AConnections[0].Endpoint);
            Assert.IsNull(BConnections[0].Endpoint);
            Assert.IsNull(BConnections[1].Endpoint);
            Assert.IsNotNull(AConnections[1].Endpoint);
            Assert.IsTrue(BConnections.All(x => x.Value.Endpoint == null));
            // TODO: o co tu chodzi? dlaczego dwa razy sprawdzamy BConnections czy są nullami?
        }

        [Test]
        public void ShouldConnectGPIOToReceiverAndReturnTheSameReceiver()
        {
            //init
            var gpioByNumberConnectorPeripheralMock = new MockGPIOByNumberConnectorPeripheral(3);
            var gpioReceiverMock = new MockReceiver();

            machine.SystemBus.Register(gpioByNumberConnectorPeripheralMock, new BusRangeRegistration(0x0, 0x10));
            machine.SystemBus.Register(gpioReceiverMock, new BusRangeRegistration(0x10, 0x10));
            gpioByNumberConnectorPeripheralMock.Connections[0].Connect(gpioReceiverMock, 1);

            //act
            var gpioConnections = gpioByNumberConnectorPeripheralMock.Connections;
            var receiver = gpioConnections[0].Endpoint.Receiver;

            Assert.True(gpioReceiverMock == receiver);
        }
    }
}
