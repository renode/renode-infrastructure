//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
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
        IMachine machine;

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
            Assert.AreEqual(1, connections.Count);
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
            ((IPeripheralRegister<IBusPeripheral, BusRangeRegistration>)machine.SystemBus).Unregister(gpioReceiverMock);

            //assert
            var connections = gpioByNumberConnectorPeripheralMock.Connections;
            Assert.IsEmpty(connections[0].Endpoints);
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
            ((IPeripheralRegister<IBusPeripheral, BusRangeRegistration>)machine.SystemBus).Unregister(gpioReceiverMock);

            //assert
            var connections = gpioByNumberConnectorPeripheralMock.Connections;
            Assert.IsEmpty(connections[0].Endpoints);
            Assert.IsNotEmpty(connections[1].Endpoints);
            Assert.IsNotEmpty(connections[2].Endpoints);

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
            ((IPeripheralRegister<IBusPeripheral, BusRangeRegistration>)machine.SystemBus).Unregister(gpioReceiverMock);
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
            ((IPeripheralRegister<IBusPeripheral, BusRangeRegistration>)machine.SystemBus).Unregister(B);
            var AConnections = A.Connections;
            var BConnections = B.Connections;

            //assert
            Assert.IsEmpty(AConnections[0].Endpoints);
            Assert.IsEmpty(BConnections[0].Endpoints);
            Assert.IsEmpty(BConnections[1].Endpoints);
            Assert.IsNotEmpty(AConnections[1].Endpoints);
        }

        [Test]
        public void ShouldDisconnectEndpointOfUnregisteredPeripheral()
        {
            // init
            var A = new MockGPIOByNumberConnectorPeripheral(1);
            var B = new MockGPIOByNumberConnectorPeripheral(1);
            var C = new MockReceiver();

            machine.SystemBus.Register(A, new BusRangeRegistration(0x0, 0x10));
            machine.SystemBus.Register(B, new BusRangeRegistration(0x10, 0x10));
            machine.SystemBus.Register(C, new BusRangeRegistration(0x20, 0x10));

            // act
            A.Connections[0].Connect(B, 1);
            A.Connections[0].Connect(C, 1);
            B.Connections[0].Connect(C, 1);
            Assert.True(A.Connections[0].Endpoints.Count == 2);
            Assert.True(B.Connections[0].Endpoints.Count == 1);

            // try to connect the same endpoint as before
            A.Connections[0].Connect(C, 1);
            Assert.True(A.Connections[0].Endpoints.Count(x => x.Receiver == C && x.Number == 1) == 1);

            ((IPeripheralRegister<IBusPeripheral, BusRangeRegistration>)machine.SystemBus).Unregister(C);

            Assert.True(A.Connections[0].Endpoints.Count == 1);
            Assert.True(A.Connections[0].Endpoints[0].Receiver == B);
            Assert.IsEmpty(B.Connections[0].Endpoints);
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
            var receiver = gpioConnections[0].Endpoints[0].Receiver;

            Assert.True(gpioConnections[0].Endpoints.Count == 1);
            Assert.IsEmpty(gpioConnections[1].Endpoints);
            Assert.IsEmpty(gpioConnections[2].Endpoints);
            Assert.True(gpioReceiverMock == receiver);
        }
    }
}
