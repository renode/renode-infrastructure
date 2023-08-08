//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using NUnit.Framework;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using System.Linq;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class NullRegistrationPointPeripheralContainerTests
    {   
        [Test]
        public void ShouldRegisterPeripheral()
        {
            container.Register(peripheral, registrationPoint);
            Assert.IsTrue(machine.IsRegistered(peripheral));
        }

        [Test]
        public void ShouldThrowWhenSecondPeripheral()
        {
            container.Register(peripheral2, NullRegistrationPoint.Instance);
            Assert.Throws<RegistrationException>(() => 
                container.Register(peripheral, NullRegistrationPoint.Instance));
        }

        [Test]
        public void ShouldUnregisterPeripheral()
        {
            container.Register(peripheral, registrationPoint);
            container.Unregister(peripheral);
            Assert.IsFalse(machine.IsRegistered(peripheral));
        }

        [Test]
        public void ShouldThrowWhenUnregisteringNotRegisteredPeripheral()
        {
            container.Register(peripheral2, NullRegistrationPoint.Instance);
            Assert.Throws<RegistrationException>(() => 
                container.Unregister(peripheral));
        }

        [Test]
        public void ShouldThrowWhenUnregisteringFromEmptyContainers()
        {
            Assert.Throws<RegistrationException>(() => 
                container.Unregister(peripheral));
        }

        [Test]
        public void ShouldGetRegistrationPoints()
        {
            container.Register(peripheral, registrationPoint);
            Assert.AreEqual(1, container.GetRegistrationPoints(peripheral).Count());
            Assert.AreSame(NullRegistrationPoint.Instance, container.GetRegistrationPoints(peripheral).First());
        }

        [Test]
        public void ShouldGetEmptyRegistrationPoints()
        {
            Assert.IsEmpty(container.GetRegistrationPoints(peripheral));
            container.Register(peripheral, registrationPoint);
            container.Unregister(peripheral);
            Assert.IsEmpty(container.GetRegistrationPoints(peripheral));
        }

        [Test]
        public void ShouldGetRegisteredPeripheralAsChildren()
        {
            container.Register(peripheral, registrationPoint);
            Assert.AreEqual(1, container.GetRegistrationPoints(peripheral).Count());
            Assert.AreSame(peripheral, container.Children.First().Peripheral);
            Assert.AreSame(NullRegistrationPoint.Instance, container.Children.First().RegistrationPoint);
        }

        [Test]
        public void ShouldGetEmptyChildren()
        {
            Assert.IsEmpty(container.Children);
            container.Register(peripheral, registrationPoint);
            container.Unregister(peripheral);
            Assert.IsEmpty(container.Children);
        }

        [Test]
        public void ShouldRegister2ndAfterUnregistering()
        {
            container.Register(peripheral, registrationPoint);
            container.Unregister(peripheral);
            container.Register(peripheral2, registrationPoint);
            Assert.IsFalse(machine.IsRegistered(peripheral));
            Assert.IsTrue(machine.IsRegistered(peripheral2));
        }


        [SetUp]
        public void SetUp()
        {
            var sysbusRegistrationPoint = new BusRangeRegistration(1337, 666);
            machine = new Machine();
            peripheral = new PeripheralMock();
            peripheral2 = new PeripheralMock();
            container = new NullRegistrationPointPeripheralContainerMock(machine);
            registrationPoint = NullRegistrationPoint.Instance;
            machine.SystemBus.Register(container, sysbusRegistrationPoint);
        }

        private IMachine machine;
        private PeripheralMock peripheral;
        private PeripheralMock peripheral2;
        private NullRegistrationPointPeripheralContainerMock container;
        private NullRegistrationPoint registrationPoint;

        private class NullRegistrationPointPeripheralContainerMock : 
            NullRegistrationPointPeripheralContainer<PeripheralMock>,
        IDoubleWordPeripheral
        {
            public NullRegistrationPointPeripheralContainerMock(IMachine machine) : base(machine) {}
            public override void Reset(){}
            public void WriteDoubleWord(long offset, uint value){}
            public uint ReadDoubleWord(long offset)
            {
                return 1337;
            }
        }

        private class PeripheralMock : IPeripheral
        {
            public void Reset(){}
        }
    }
}
