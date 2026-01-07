//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.UnitTests.Mocks;

using NUnit.Framework;

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
            ((IRegisterablePeripheral<IBusPeripheral, BusRangeRegistration>)machine.SystemBus).Unregister(gpioReceiverMock);

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
            ((IRegisterablePeripheral<IBusPeripheral, BusRangeRegistration>)machine.SystemBus).Unregister(gpioReceiverMock);

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
            ((IRegisterablePeripheral<IBusPeripheral, BusRangeRegistration>)machine.SystemBus).Unregister(gpioReceiverMock);
            //assert
            Assert.IsFalse(gpioSender.Irq.IsConnected);
        }

        [Test]
        public void ShouldUnregisterChainedPeripheralsOnBDisconnect()
        {
            //A -> B -> C, B -> D and A -> C
            //B is disconnected

            //init
            var a = new MockGPIOByNumberConnectorPeripheral(2);
            var b = new MockGPIOByNumberConnectorPeripheral(2);
            var c = new MockReceiver();
            var d = new MockReceiver();

            machine.SystemBus.Register(a, new BusRangeRegistration(0x0, 0x10));
            machine.SystemBus.Register(b, new BusRangeRegistration(0x10, 0x10));
            machine.SystemBus.Register(c, new BusRangeRegistration(0x20, 0x10));
            machine.SystemBus.Register(d, new BusRangeRegistration(0x30, 0x10));

            a.Connections[0].Connect(b, 1);
            b.Connections[0].Connect(c, 1);
            b.Connections[1].Connect(d, 1);
            a.Connections[1].Connect(c, 2);

            //act
            ((IRegisterablePeripheral<IBusPeripheral, BusRangeRegistration>)machine.SystemBus).Unregister(b);
            var aConnections = a.Connections;
            var bConnections = b.Connections;

            //assert
            Assert.IsEmpty(aConnections[0].Endpoints);
            Assert.IsEmpty(bConnections[0].Endpoints);
            Assert.IsEmpty(bConnections[1].Endpoints);
            Assert.IsNotEmpty(aConnections[1].Endpoints);
        }

        [Test]
        public void ShouldDisconnectEndpointOfUnregisteredPeripheral()
        {
            // init
            var a = new MockGPIOByNumberConnectorPeripheral(1);
            var b = new MockGPIOByNumberConnectorPeripheral(1);
            var c = new MockReceiver();

            machine.SystemBus.Register(a, new BusRangeRegistration(0x0, 0x10));
            machine.SystemBus.Register(b, new BusRangeRegistration(0x10, 0x10));
            machine.SystemBus.Register(c, new BusRangeRegistration(0x20, 0x10));

            // act
            a.Connections[0].Connect(b, 1);
            a.Connections[0].Connect(c, 1);
            b.Connections[0].Connect(c, 1);
            Assert.True(a.Connections[0].Endpoints.Count == 2);
            Assert.True(b.Connections[0].Endpoints.Count == 1);

            // try to connect the same endpoint as before
            a.Connections[0].Connect(c, 1);
            Assert.True(a.Connections[0].Endpoints.Count(x => x.Receiver == c && x.Number == 1) == 1);

            ((IRegisterablePeripheral<IBusPeripheral, BusRangeRegistration>)machine.SystemBus).Unregister(c);

            Assert.True(a.Connections[0].Endpoints.Count == 1);
            Assert.True(a.Connections[0].Endpoints[0].Receiver == b);
            Assert.IsEmpty(b.Connections[0].Endpoints);
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