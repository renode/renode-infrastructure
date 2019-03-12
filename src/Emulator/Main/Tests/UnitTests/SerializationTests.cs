//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;
using NUnit.Framework;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.UnitTests.Mocks;
using Antmicro.Migrant;
using System.Collections.ObjectModel;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class SerializationTests
    {
        [SetUp]
        public void SetUp()
        {
            ClearStream();
            serializer = new Serializer();
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void ShouldSerializeMemory()
        {
            // preparing some random data
            const int size = 128 * 1024 * 1024;
            const int bufCount = 30;
            const int bufSize = 100 * 1024;
            var random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            var buffers = new List<Tuple<int, byte[]>>(bufCount);
            for(var i = 0; i < bufCount; i++)
            {
                var buffer = new byte[bufSize];
                random.NextBytes(buffer);
                int address;
                do
                {
                    address = random.Next(size - bufSize);
                }
                while(buffers.Any(x => address >= (x.Item1 - bufSize) && address <= (x.Item1 + bufSize)));
                buffers.Add(new Tuple<int, byte[]>(address, buffer));
            }

            using(var memory = new MappedMemory(null, size))
            {
                foreach(var buf in buffers)
                {
                    memory.WriteBytes(buf.Item1, buf.Item2);
                }
                serializer.Serialize(memory, stream);
            }

            RewindStream();
            using(var memory = serializer.Deserialize<MappedMemory>(stream))
            {
                foreach(var buf in buffers)
                {
                    var bufCopy = memory.ReadBytes(buf.Item1, bufSize);
                    CollectionAssert.AreEqual(bufCopy, buf.Item2);
                }
            }
        }

        [Test]
        public void ShouldSerializeAllCpus([Range(1, 4)] int cpuCount)
        {
            var machine = new Machine();
            var sysbus = machine.SystemBus;
            for(var i = 0; i < cpuCount; i++)
            {
                sysbus.Register(new MockCPU(machine), new CPURegistrationPoint());
            }
            machine = Serializer.DeepClone(machine);
            sysbus = machine.SystemBus;
            var cpus = sysbus.GetCPUs();
            Assert.AreEqual(cpuCount, cpus.Count());
        }

        [Test]
        public void ShouldRememberCpuNames()
        {
            var names = new[] { "Xavier", "Alice", "Bob" };
            var machine = new Machine();
            foreach(var name in names)
            {
                var cpu = new MockCPU(machine) { Placeholder = name };
                machine.SystemBus.Register(cpu, new CPURegistrationPoint());
                machine.SetLocalName(cpu, name);
            }
            machine = Serializer.DeepClone(machine);
            var cpus = machine.SystemBus.GetCPUs();
            CollectionAssert.AreEquivalent(names, cpus.Select(x => machine.GetLocalName(x)));
            foreach(var cpu in cpus.Cast<MockCPU>())
            {
                Assert.AreEqual(cpu.Placeholder, machine.GetLocalName(cpu));
            }
        }

        [Test]
        public void ShouldSerializeGPIOs()
        {
            var mocks = new GPIOMock[4];
            mocks[0] = new GPIOMock { Id = 1 };
            mocks[1] = new GPIOMock { Id = 2 };
            mocks[2] = new GPIOMock { Id = 3 };
            mocks[3] = new GPIOMock { Id = 4 };

            mocks[0].Connections[0].Connect(mocks[1], 5);
            mocks[0].Connections[0].Connect(mocks[3], 7);
            mocks[1].Connections[1].Connect(mocks[2], 0);
            mocks[2].Connections[2].Connect(mocks[1], 6);
            mocks[0].GPIOSet(0, true);
            mocks[2].GPIOSet(1, true);
            mocks[2].GPIOSet(2, true);

            var devs = Serializer.DeepClone(mocks).ToList();
            Assert.AreEqual(4, devs.Count);
            var mock1 = devs.First(x => x.Id == 1);
            var mock2 = devs.First(x => x.Id == 2);
            var mock3 = devs.First(x => x.Id == 3);
            var mock4 = devs.First(x => x.Id == 4);

            // checking connections
            var conn1 = mock1.Connections[0].Endpoints;
            Assert.AreEqual(5, conn1[0].Number);
            Assert.AreEqual(7, conn1[1].Number);
            Assert.AreEqual(mock2, conn1[0].Receiver);
            Assert.AreEqual(mock4, conn1[1].Receiver);
            var conn2 = mock2.Connections[1].Endpoints;
            Assert.AreEqual(0, conn2[0].Number);
            Assert.AreEqual(mock3, conn2[0].Receiver);
            var conn3 = mock3.Connections[2].Endpoints;
            Assert.AreEqual(6, conn3[0].Number);
            Assert.AreEqual(mock2, conn3[0].Receiver);

            // checking signaled state
            Assert.IsTrue(mock2.HasSignaled(5), "Mock 2:5 is not signaled!");
            Assert.IsTrue(mock2.HasSignaled(6), "Mock 2:6 is not signaled!");
            Assert.IsFalse(mock3.HasSignaled(0), "Mock 3:0 is signaled!");

            // now we connect sth to signaled mock3:1 and what happens?
            mock3.Connections[1].Connect(mock1, 9);
            Assert.IsTrue(mock1.HasSignaled(9), "Mock 1:9 is not signaled!");
        }

        private void RewindStream()
        {
            var buffer = stream.GetBuffer();
            stream = new MemoryStream(buffer, 0, (int)stream.Length);
        }

        private void ClearStream()
        {
            stream = new MemoryStream();
        }

        private MemoryStream stream;
        private Serializer serializer;

    }

    public class GPIOMock : IGPIOReceiver, INumberedGPIOOutput
    {
        public GPIOMock()
        {
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < 10; i++)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
        }

        public void Reset()
        {
        }

        public int Id { get; set; }

        public void GPIOSet(int number, bool value)
        {
            Connections[number].Set(value);
        }

        public bool HasSignaled(int number)
        {
            return activeIns.Contains(number);
        }

        public override string ToString()
        {
            return string.Format("[GPIOMock: Id={0}]", Id);
        }

        public void OnGPIO(int number, bool value)
        {
            if(activeIns == null)
            {
                activeIns = new HashSet<int>();
            }
            if(value)
            {
                activeIns.Add(number);
            }
            else
            {
                activeIns.Remove(number);
            }
        }

        private HashSet<int> activeIns;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }
    }
}

