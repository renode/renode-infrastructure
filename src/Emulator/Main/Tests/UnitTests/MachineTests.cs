//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using NUnit.Framework;
using Antmicro.Renode.Core;
using Moq;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure;
using System.Threading;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.UnitTests
{
    public class MachineTests
    {
        [Test]
        public void ShouldThrowOnRegisteringAnotherPeripheralWithTheSameName()
        {
            var machine = new Machine();
            var peripheral1 = new Mock<IDoubleWordPeripheral>().Object;
            var peripheral2 = new Mock<IDoubleWordPeripheral>().Object;
            machine.SystemBus.Register(peripheral1,  0.By(10));
            machine.SystemBus.Register(peripheral2, 10.By(10));
            machine.SetLocalName(peripheral1, "name");

            Assert.Throws(typeof(RecoverableException), () => machine.SetLocalName(peripheral2, "name"));
        }

        [Test]
        public void ShouldFindPeripheralByPath()
        {
            var machine = new Machine();
            var peripheral1 = new Mock<IDoubleWordPeripheral>().Object;
            machine.SystemBus.Register(peripheral1, 0.By(10));
            machine.SetLocalName(peripheral1, "name");

            Assert.AreEqual(peripheral1, machine["sysbus.name"]);
        }

        [Test]
        public void ShouldFindPeripheralByPathWhenThereAreTwo()
        {
            var machine = new Machine();
            var peripheral1 = new Mock<IDoubleWordPeripheral>().Object;
            var peripheral2 = new Mock<IDoubleWordPeripheral>().Object;
            machine.SystemBus.Register(peripheral1,  0.By(10));
            machine.SystemBus.Register(peripheral2, 10.By(10));
            machine.SetLocalName(peripheral1, "first");
            machine.SetLocalName(peripheral2, "second");

            Assert.AreEqual(peripheral1, machine["sysbus.first"]);
            Assert.AreEqual(peripheral2, machine["sysbus.second"]);
        }

        [Test]
        public void ShouldThrowOnNullOrEmptyPeripheralName()
        {
            var machine = new Machine();
            var peripheral1 = new Mock<IDoubleWordPeripheral>().Object;
            machine.SystemBus.Register(peripheral1, 0.By(10));

            Assert.Throws(typeof(RecoverableException), () => machine.SetLocalName(peripheral1, ""));
            Assert.Throws(typeof(RecoverableException), () => machine.SetLocalName(peripheral1, null));
        }

        [Test]
        public void ShouldHandleManagedThreads()
        {
            var counter = 0;
            var a = (Action)(() => counter++);

            using(var emulation = new Emulation())
            {
                var machine = new Machine();
                emulation.AddMachine(machine);

                emulation.SetGlobalQuantum(Time.TimeInterval.FromMilliseconds(1000));
                emulation.SetGlobalAdvanceImmediately(true);

                var mt = machine.ObtainManagedThread(a, 1);

                emulation.RunToNearestSyncPoint();
                emulation.RunToNearestSyncPoint();
                emulation.RunToNearestSyncPoint();
                Assert.AreEqual(0, counter);

                mt.Start();

                emulation.RunToNearestSyncPoint();
                Assert.AreEqual(1, counter);

                emulation.RunToNearestSyncPoint();
                Assert.AreEqual(2, counter);

                emulation.RunToNearestSyncPoint();
                Assert.AreEqual(3, counter);

                mt.Stop();

                emulation.RunToNearestSyncPoint();
                emulation.RunToNearestSyncPoint();
                emulation.RunToNearestSyncPoint();
                Assert.AreEqual(3, counter);
            }
        }

        public sealed class Mother : IPeripheralRegister<IPeripheral, NullRegistrationPoint>, IDoubleWordPeripheral
        {
            public Mother(IMachine machine)
            {
                this.machine = machine;
            }

            public void Register(IPeripheral peripheral, NullRegistrationPoint registrationPoint)
            {
                machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            }

            public void Unregister(IPeripheral peripheral)
            {
                machine.UnregisterAsAChildOf(this, peripheral);
            }

            public uint ReadDoubleWord(long offset)
            {
                return 0;
            }

            public void WriteDoubleWord(long offset, uint value)
            {

            }

            public void Reset()
            {

            }

            private readonly IMachine machine;
        }
    }
}

