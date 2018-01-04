//
// Copyright (c) 2010-2018 Antmicro
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
            machine.SystemBus.Register(peripheral1, 0.To(10));
            machine.SystemBus.Register(peripheral2, 10.To(20));
            machine.SetLocalName(peripheral1, "name");

            Assert.Throws(typeof(RecoverableException), () => machine.SetLocalName(peripheral2, "name"));
        }

        [Test]
        public void ShouldFindPeripheralByPath()
        {
            var machine = new Machine();
            var peripheral1 = new Mock<IDoubleWordPeripheral>().Object;
            machine.SystemBus.Register(peripheral1, 0.To(10));
            machine.SetLocalName(peripheral1, "name");

            Assert.AreEqual(peripheral1, machine["sysbus.name"]);
        }

        [Test]
        public void ShouldFindPeripheralByPathWhenThereAreTwo()
        {
            var machine = new Machine();
            var peripheral1 = new Mock<IDoubleWordPeripheral>().Object;
            var peripheral2 = new Mock<IDoubleWordPeripheral>().Object;
            machine.SystemBus.Register(peripheral1, 0.To(10));
            machine.SystemBus.Register(peripheral2, 10.To(20));
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
            machine.SystemBus.Register(peripheral1, 0.To(10));

            Assert.Throws(typeof(RecoverableException), () => machine.SetLocalName(peripheral1, ""));
            Assert.Throws(typeof(RecoverableException), () => machine.SetLocalName(peripheral1, null));
        }

        public sealed class Mother : IPeripheralRegister<IPeripheral, NullRegistrationPoint>, IDoubleWordPeripheral
        {
            public Mother(Machine machine)
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

            private readonly Machine machine;
        }

        public sealed class PeripheralWithManagedThread : IDoubleWordPeripheral
        {
            public PeripheralWithManagedThread(Machine machine)
            {
                resetEvent = new ManualResetEventSlim(false);
                machine.ObtainManagedThread(ThreadAction, this, 0, "test", false).Start();
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

            public bool IsTheThreadRunning
            {
                get
                {
                    return resetEvent.Wait(500);
                }
            }

            private void ThreadAction()
            {
                resetEvent.Set();
                Thread.Sleep(100);
                resetEvent.Reset();
            }

            private readonly ManualResetEventSlim resetEvent;
        }
    }
}

