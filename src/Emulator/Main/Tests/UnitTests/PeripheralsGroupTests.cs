//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using NUnit.Framework;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class PeripheralsGroupTests
    {
        [Test]
        public void ShouldNotUnregisterSinglePeripheralFromGroup()
        {
            using(var machine = new Machine())
            {
                var peripheral = new MappedMemory(machine, 10);
                machine.SystemBus.Register(peripheral, new BusPointRegistration(0x0));
                machine.SystemBus.Unregister(peripheral);

                machine.SystemBus.Register(peripheral, new BusPointRegistration(0x0));
                machine.PeripheralsGroups.GetOrCreate("test-group", new [] { peripheral });

                try
                {
                    machine.SystemBus.Unregister(peripheral);
                }
                catch (RegistrationException)
                {
                    return;
                }

                Assert.Fail();
            }
        }

        [Test]
        public void ShouldUnregisterPeripheralGroups()
        {
            using(var machine = new Machine())
            {
                var peripheral = new MappedMemory(machine, 10);
                machine.SystemBus.Register(peripheral, new BusPointRegistration(0x0));
                var group = machine.PeripheralsGroups.GetOrCreate("test-group", new [] { peripheral });
                Assert.IsTrue(machine.IsRegistered(peripheral));
                group.Unregister();
                Assert.IsFalse(machine.IsRegistered(peripheral));
            }
        }

        [Test]
        public void ShouldNotAddUnregisteredPeripheralToGroup()
        {
            using(var machine = new Machine())
            {
                try
                {
                    machine.PeripheralsGroups.GetOrCreate("test-group", new [] { new MappedMemory(machine, 10) });
                }
                catch (RegistrationException)
                {
                    return;
                }

                Assert.Fail();
            }
        }
    }
}

