//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;
using NUnit.Framework;
using Moq;
using Antmicro.Renode.UnitTests.Mocks;
using System.Threading;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class MultiCPUTests
    {
        [Test]
        public void ShouldEnumerateCPUs()
        {
            const int numberOfCpus = 10;
            var cpus = new ICPU[numberOfCpus];
            using(var machine = new Machine())
            {
                var sysbus = machine.SystemBus;
                for(var i = 0; i < cpus.Length; i++)
                {
                    var mock = new Mock<ICPU>();
                    mock.Setup(cpu => cpu.Architecture).Returns("mock");  // Required by InitializeInvalidatedAddressesList.
                    cpus[i] = mock.Object;
                    sysbus.Register(cpus[i], new CPURegistrationPoint());
                }
                for(var i = 0; i < cpus.Length; i++)
                {
                    Assert.AreEqual(i, sysbus.GetCPUId(cpus[i]));
                }
            }
        }

        [Test]
        public void ShouldGuardPeripheralReads([Range(1, 4)] int cpuCount)
        {
            using(var emulation = new Emulation())
            using(var machine = new Machine())
            {
                emulation.AddMachine(machine);
                var sysbus = machine.SystemBus;
                cpuCount.Times(() => sysbus.Register(new ActivelyAskingCPU(machine, 0), new CPURegistrationPoint()));
                var peripheral = new ActivelyAskedPeripheral();
                sysbus.Register(peripheral, 0.By(1000));
                machine.Start();
                Thread.Sleep(1000);
                machine.Pause();
                Assert.IsFalse(peripheral.Failed, "Peripheral was concurrently accessed from multiple CPUs.");
            }
        }
    }
}

