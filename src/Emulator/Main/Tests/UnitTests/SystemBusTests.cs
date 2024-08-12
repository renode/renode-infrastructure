//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;
using NUnit.Framework;
using Moq;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Mocks;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class SystemBusTests
    {
        [SetUp]
        public void SetUp()
        {
            var machine = new Machine();
            EmulationManager.Instance.CurrentEmulation.AddMachine(machine);
            sysbus = machine.SystemBus;
            bytes = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
        }

        [Test]
        public void ShouldReturnZeroAtNonExistingDevice()
        {
            var read = sysbus.ReadByte(0xABCD1234);
            Assert.AreEqual(0, read);
        }

        [Test]
        public void ShouldFindAfterRegistration()
        {
            var peripheral = new Mock<IDoubleWordPeripheral>();
            peripheral.Setup(x => x.ReadDoubleWord(0)).Returns(0x666);
            sysbus.Register(peripheral.Object, 1000.By(1000));
            Assert.AreEqual(0x666, sysbus.ReadDoubleWord(1000));
        }

        [Test]
        public void ShouldFindAfterManyRegistrations()
        {
            var peri1 = new Mock<IDoubleWordPeripheral>();
            var peri2 = new Mock<IDoubleWordPeripheral>();
            var peri3 = new Mock<IDoubleWordPeripheral>();
            peri1.Setup(x => x.ReadDoubleWord(0)).Returns(0x666);
            peri2.Setup(x => x.ReadDoubleWord(0)).Returns(0x667);
            peri3.Setup(x => x.ReadDoubleWord(0)).Returns(0x668);
            sysbus.Register(peri1.Object, 1000.By(100));
            sysbus.Register(peri2.Object, 2000.By(100));
            sysbus.Register(peri3.Object, 3000.By(100));
            Assert.AreEqual(0x666, sysbus.ReadDoubleWord(1000));
            Assert.AreEqual(0x667, sysbus.ReadDoubleWord(2000));
            Assert.AreEqual(0x668, sysbus.ReadDoubleWord(3000));
        }

        [Test, Ignore("Ignored")]
        public void ShouldFindAfterManyRegistrationsAndRemoves()
        {
            const int NumberOfPeripherals = 100;
            var MaximumPeripheralSize = 16.KB();

            var regPoints = new ulong[NumberOfPeripherals];
            var unregisteredPoints = new HashSet<ulong>();
            var random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            var lastPoint = 4u;
            for(var i = 0; i < NumberOfPeripherals; i++)
            {
                // gap
                lastPoint += (uint)random.Next(MaximumPeripheralSize);
                var size = (uint)random.Next(1, MaximumPeripheralSize + 1);
                regPoints[i] = lastPoint;
                var mock = new Mock<IDoubleWordPeripheral>();
                mock.Setup(x => x.ReadDoubleWord(0)).Returns((uint)regPoints[i]);
                sysbus.Register(mock.Object, lastPoint.By(size));
                // peripheral
                lastPoint += size;
            }

            // now remove random devices
            for(var i = 0; i < NumberOfPeripherals; i++)
            {
                if(random.Next(100) < 10)
                {
                    sysbus.UnregisterFromAddress(regPoints[i], context: null);
                    unregisteredPoints.Add(regPoints[i]);
                }
            }
			
            // finally some assertions
            for(var i = 0; i < NumberOfPeripherals; i++)
            {
                var value = sysbus.ReadDoubleWord(regPoints[i]);
                if(unregisteredPoints.Contains(regPoints[i]))
                {
                    Assert.AreEqual(0, value);
                }
                else
                {
                    Assert.AreEqual(regPoints[i], value);
                }
            }
        }

        [Test]
        public void ShouldPauseAndResumeOnlyOnce()
        {
            using(var emulation = new Emulation())
            using(var machine = new Machine())
            {
                emulation.AddMachine(machine);
                var sb = machine.SystemBus;
                var mock = new Mock<IHasOwnLife>();
                sb.Register(mock.As<IDoubleWordPeripheral>().Object, 0.To(100));

                var cpuMock = new Mock<ICPU>();
                cpuMock.Setup(cpu => cpu.Architecture).Returns("mock");  // Required by InitializeInvalidatedAddressesList.
                sb.Register(cpuMock.Object, new CPURegistrationPoint());

                machine.Start();
                PauseResumeRetries.Times(machine.Pause);
                mock.Verify(x => x.Pause(), Times.Once());
                PauseResumeRetries.Times(machine.Start);
                mock.Verify(x => x.Resume(), Times.Once());
            }
        }

        [Test]
        public void ShouldRegisterMultiFunctionPeripheral()
        {
            var multiRegistration1 = new BusMultiRegistration(0, 100, "region1");
            var multiRegistration2 = new BusMultiRegistration(100, 200, "region2");
            var peripheral = new MultiRegistrationPeripheral();
            sysbus.Register(peripheral, multiRegistration1);
            sysbus.Register(peripheral, multiRegistration2);
            sysbus.Register(peripheral, new BusRangeRegistration(300, 100));

            Assert.AreEqual(false, peripheral.ByteRead1);
            Assert.AreEqual(false, peripheral.ByteRead2);
            Assert.AreEqual(false, peripheral.ByteWritten1);
            Assert.AreEqual(false, peripheral.ByteWritten2);
            Assert.AreEqual(false, peripheral.DoubleWordRead);
            Assert.AreEqual(false, peripheral.DoubleWordWritten);

            sysbus.ReadByte(10);

            Assert.AreEqual(true, peripheral.ByteRead1);
            Assert.AreEqual(false, peripheral.ByteRead2);
            Assert.AreEqual(false, peripheral.ByteWritten1);
            Assert.AreEqual(false, peripheral.ByteWritten2);
            Assert.AreEqual(false, peripheral.DoubleWordRead);
            Assert.AreEqual(false, peripheral.DoubleWordWritten);

            sysbus.ReadByte(110);

            Assert.AreEqual(true, peripheral.ByteRead1);
            Assert.AreEqual(true, peripheral.ByteRead2);
            Assert.AreEqual(false, peripheral.ByteWritten1);
            Assert.AreEqual(false, peripheral.ByteWritten2);
            Assert.AreEqual(false, peripheral.DoubleWordRead);
            Assert.AreEqual(false, peripheral.DoubleWordWritten);

            sysbus.WriteByte(10, 0);

            Assert.AreEqual(true, peripheral.ByteRead1);
            Assert.AreEqual(true, peripheral.ByteRead2);
            Assert.AreEqual(true, peripheral.ByteWritten1);
            Assert.AreEqual(false, peripheral.ByteWritten2);
            Assert.AreEqual(false, peripheral.DoubleWordRead);
            Assert.AreEqual(false, peripheral.DoubleWordWritten);

            sysbus.WriteByte(110, 0);

            Assert.AreEqual(true, peripheral.ByteRead1);
            Assert.AreEqual(true, peripheral.ByteRead2);
            Assert.AreEqual(true, peripheral.ByteWritten1);
            Assert.AreEqual(true, peripheral.ByteWritten2);
            Assert.AreEqual(false, peripheral.DoubleWordRead);
            Assert.AreEqual(false, peripheral.DoubleWordWritten);

            sysbus.ReadDoubleWord(310);

            Assert.AreEqual(true, peripheral.ByteRead1);
            Assert.AreEqual(true, peripheral.ByteRead2);
            Assert.AreEqual(true, peripheral.ByteWritten1);
            Assert.AreEqual(true, peripheral.ByteWritten2);
            Assert.AreEqual(true, peripheral.DoubleWordRead);
            Assert.AreEqual(false, peripheral.DoubleWordWritten);

            sysbus.WriteDoubleWord(310, 0);

            Assert.AreEqual(true, peripheral.ByteRead1);
            Assert.AreEqual(true, peripheral.ByteRead2);
            Assert.AreEqual(true, peripheral.ByteWritten1);
            Assert.AreEqual(true, peripheral.ByteWritten2);
            Assert.AreEqual(true, peripheral.DoubleWordRead);
            Assert.AreEqual(true, peripheral.DoubleWordWritten);
        }

        [Test]
        public void ShouldRegisterParametrizedPeripheral()
        {
            var parametrizedRegistration = new ParametrizedRegistrationPeripheral.Registration(0, 100);
            var peripheral = new ParametrizedRegistrationPeripheral();
            sysbus.Register(peripheral, parametrizedRegistration);
            sysbus.Register(peripheral, new BusRangeRegistration(200, 100));

            Assert.AreEqual(false, peripheral.ByteRead);
            Assert.AreEqual(false, peripheral.ByteWritten);
            Assert.AreEqual(false, peripheral.DoubleWordRead);
            Assert.AreEqual(false, peripheral.DoubleWordWritten);

            sysbus.ReadByte(10);

            Assert.AreEqual(true, peripheral.ByteRead);
            Assert.AreEqual(false, peripheral.ByteWritten);
            Assert.AreEqual(false, peripheral.DoubleWordRead);
            Assert.AreEqual(false, peripheral.DoubleWordWritten);

            sysbus.WriteByte(10, 0);

            Assert.AreEqual(true, peripheral.ByteRead);
            Assert.AreEqual(true, peripheral.ByteWritten);
            Assert.AreEqual(false, peripheral.DoubleWordRead);
            Assert.AreEqual(false, peripheral.DoubleWordWritten);

            sysbus.ReadDoubleWord(10);

            Assert.AreEqual(true, peripheral.ByteRead);
            Assert.AreEqual(true, peripheral.ByteWritten);
            Assert.AreEqual(true, peripheral.DoubleWordRead);
            Assert.AreEqual(false, peripheral.DoubleWordWritten);

            sysbus.WriteDoubleWord(10, 0);

            Assert.AreEqual(true, peripheral.ByteRead);
            Assert.AreEqual(true, peripheral.ByteWritten);
            Assert.AreEqual(true, peripheral.DoubleWordRead);
            Assert.AreEqual(true, peripheral.DoubleWordWritten);
        }

        [Test]
        public void ShouldTestEnableAllTranslations()
        {
            var testData = new Tuple<int, IBusPeripheral> []
            {
                Tuple.Create(64, (IBusPeripheral)new MockQuadWordPeripheralWithoutTranslations()),
                Tuple.Create(32, (IBusPeripheral)new MockDoubleWordPeripheralWithoutTranslations()),
                Tuple.Create(16, (IBusPeripheral)new MockWordPeripheralWithoutTranslations()),
                Tuple.Create(8, (IBusPeripheral)new MockBytePeripheralWithoutTranslations())
            };

            ulong address = 0;
            foreach(var data in testData)
            {
                var implementedWidth = data.Item1;
                var peripheral = data.Item2;
                sysbus.Register(peripheral, new BusRangeRegistration(address, 8));
                sysbus.Register(peripheral, new BusMultiRegistration(address + 8, 8, "region"));
                address += 16;
            }
            
            var testValue = 0xdeadc0de12345678;
            for(var i = 0; i < testData.Length; i += 1)
            {
                var implementedWidth = testData[i].Item1;
                testAllTranslatedAccesses((ulong)i * 16, testValue, implementedWidth);
                testAllTranslatedAccesses((ulong)i * 16 + 8, testValue, implementedWidth);
            }
        }

        [Test]
        public void ShouldHandleWriteToMemorySegment()
        {
            CreateMachineAndExecute(sysbus =>
            {
                // bytes fit into memory segment
                sysbus.WriteBytes(bytes, 4);
                Assert.AreEqual(bytes, sysbus.ReadBytes(4, 8));
            });
        }

        [Test]
        public void ShouldHandlePartialWriteToMemorySegmentAtTheBeginning()
        {
            CreateMachineAndExecute(sysbus =>
            {
                // beginning of bytes fits into memory segment
                sysbus.WriteBytes(bytes, 10);
                Assert.AreEqual(bytes.Take(6).Concat(Enumerable.Repeat((byte)0, 2)).ToArray(), sysbus.ReadBytes(10, 8));
            });
        }

        [Test]
        public void ShouldReadBytes([Values(true, false)] bool isFirstMemory, [Values(true, false)] bool isSecondMemory)
        {
            var peri1Values = new Dictionary<long, byte>();
            var peri2Values = new Dictionary<long, byte>();
            var machine = new Machine();
            if(isFirstMemory)
            {
                machine.SystemBus.Register(new MappedMemory(machine, 100), new BusPointRegistration(50));
            }
            else
            {
                var mock = new Mock<IBytePeripheral>();
                mock.Setup(x => x.WriteByte(It.IsAny<long>(), It.IsAny<byte>())).Callback<long, byte>((x, y) => peri1Values.Add(x, y));
                mock.Setup(x => x.ReadByte(It.IsAny<long>())).Returns<long>(x => peri1Values[x]);
                machine.SystemBus.Register(mock.Object, new BusRangeRegistration(50, 100));
            }

            if(isSecondMemory)
            {
                machine.SystemBus.Register(new MappedMemory(machine, 100), new BusPointRegistration(200));
            }
            else
            {
                var mock = new Mock<IBytePeripheral>();
                mock.Setup(x => x.WriteByte(It.IsAny<long>(), It.IsAny<byte>())).Callback<long, byte>((x, y) => peri2Values.Add(x, y));
                mock.Setup(x => x.ReadByte(It.IsAny<long>())).Returns<long>(x => peri2Values[x]);
                machine.SystemBus.Register(mock.Object, new BusRangeRegistration(200, 100));
            }
            var testArray = Enumerable.Range(0, 350).Select(x => (byte)(x % byte.MaxValue)).ToArray();
            machine.SystemBus.WriteBytes(testArray, 0);
            var resultArray = machine.SystemBus.ReadBytes(0, 350);
            int i = 0;
            for(; i < 50; ++i)
            {
                Assert.AreEqual(0, resultArray[i]);
            }
            for(; i < 150; ++i)
            {
                Assert.AreEqual(testArray[i], resultArray[i]);
            }
            for(; i < 200; ++i)
            {
                Assert.AreEqual(0, resultArray[i]);
            }
            for(; i < 300; ++i)
            {
                Assert.AreEqual(testArray[i], resultArray[i]);
            }
            for(; i < 350; ++i)
            {
                Assert.AreEqual(0, resultArray[i]);
            }
        }

        [Test]
        public void ShouldHandlePartialWriteToMemorySegmentAtTheEnd()
        {
            CreateMachineAndExecute(sysbus =>
            {
                // beginning of bytes fits into memory segment
                sysbus.WriteBytes(bytes, 0xC0000000 - 4);
                Assert.AreEqual(Enumerable.Repeat((byte)0, 4).Concat(bytes.Skip(4).Take(4)).ToArray(), sysbus.ReadBytes(0xC0000000 - 4, 8));
            });
        }

        [Test]
        public void ShouldHandleWriteToAHoleBetweenMemorySegments()
        {
            CreateMachineAndExecute(sb =>
            {
                // bytes do not fit into memory segment
                sb.WriteBytes(bytes, 100);
                Assert.AreEqual(Enumerable.Repeat((byte)0, 8).ToArray(), sb.ReadBytes(100, 8));
            });
        }

        [Test]
        public void ShouldHandleWriteOverlappingMemorySegment()
        {
            CreateMachineAndExecute(sysbus =>
            {
                // bytes overlap memory segment
                var hugeBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
                sysbus.WriteBytes(hugeBytes, 0xC0000000 - 4);
                Assert.AreEqual(Enumerable.Repeat((byte)0, 4).Concat(hugeBytes.Skip(4).Take(16)).Concat(Enumerable.Repeat((byte)0, 12)).ToArray(), sysbus.ReadBytes(0xC0000000 - 4, 32));
            });
        }

        private void CreateMachineAndExecute(Action<IBusController> action)
        {
            using(var machine = new Machine())
            {
                var sb = machine.SystemBus;
                var memory = new MappedMemory(machine, 16);
                sb.Register(memory, 0.By(16));
                sb.Register(memory, 0xC0000000.By(16));

                action(sb);
            }
        }

        private void testAllTranslatedAccesses(ulong address, ulong testValue, int implementedWidth)
        {
            var widths = new int [] { 8, 16, 32, 64 };
            var types = new dynamic []
            {
                (Func<ulong, byte>)Convert.ToByte,
                (Func<ulong, ushort>)Convert.ToUInt16,
                (Func<ulong, uint>)Convert.ToUInt32,
                (Func<ulong, ulong>)Convert.ToUInt64,
            };

            var accessMethods = new dynamic []
            {
                Tuple.Create((Func<ulong, ICPU, byte>)sysbus.ReadByte, (Action<ulong, byte, ICPU>)sysbus.WriteByte),
                Tuple.Create((Func<ulong, ICPU, ushort>)sysbus.ReadWord, (Action<ulong, ushort, ICPU>)sysbus.WriteWord),
                Tuple.Create((Func<ulong, ICPU, uint>)sysbus.ReadDoubleWord, (Action<ulong, uint, ICPU>)sysbus.WriteDoubleWord),
                Tuple.Create((Func<ulong, ICPU, ulong>)sysbus.ReadQuadWord, (Action<ulong, ulong, ICPU>)sysbus.WriteQuadWord),
            };

            for(var i = 0; i < accessMethods.Length; i++)
            {
                var width = widths[i];
                var type = types[i];
                var read = accessMethods[i].Item1;
                var write = accessMethods[i].Item2;

                if(width == implementedWidth)
                    continue;

                sysbus.EnableAllTranslations();

                for(var offset = 0; offset < 64; offset += width)
                {
                    var value = type(BitHelper.GetValue(testValue, offset, width));
                    write(address + (ulong)offset / 8, value, null);
                    Assert.AreEqual(value, read(address + (ulong)offset / 8, null));
                }

                sysbus.EnableAllTranslations(false);

                for(var offset = 0; offset < 64; offset += width)
                {
                    Assert.AreEqual(0x0, read(address + (ulong)offset / 8, null));
                }

                sysbus.EnableAllTranslations();
                sysbus.WriteQuadWord(address, 0x0);
                Assert.AreEqual(0x0, sysbus.ReadQuadWord(address));
            }
        }

        private IBusController sysbus;
        private byte[] bytes;
        private const int PauseResumeRetries = 5;

        private class MultiRegistrationPeripheral : IBusPeripheral, IDoubleWordPeripheral
        {
            public void Reset()
            {
                throw new NotImplementedException();
            }

            public bool ByteRead1 { get; private set; }

            public bool ByteRead2 { get; private set; }

            public bool ByteWritten1 { get; private set; }

            public bool ByteWritten2 { get; private set; }

            public bool DoubleWordRead { get; private set; }

            public bool DoubleWordWritten { get; private set; }

            public uint ReadDoubleWord(long offset)
            {
                DoubleWordRead = true;
                return 0;
            }

            public void WriteDoubleWord(long offset, uint value)
            {
                DoubleWordWritten = true;
            }

            [ConnectionRegion("region1")]
            public byte ReadByte1(long offset)
            {
                ByteRead1 = true;
                return 0;
            }

            [ConnectionRegion("region2")]
            public byte ReadByte2(long offset)
            {
                ByteRead2 = true;
                return 0;
            }

            [ConnectionRegion("region1")]
            public void WriteByte1(long offset, byte value)
            {
                ByteWritten1 = true;
            }

            [ConnectionRegion("region2")]
            public void WriteByte2(long offset, byte value)
            {
                ByteWritten2 = true;
            }
        }

        private class ParametrizedRegistrationPeripheral : IBusPeripheral, IDoubleWordPeripheral
        {
            public void Reset()
            {
                throw new NotImplementedException();
            }

            public bool ByteRead { get; private set; }

            public bool ByteWritten { get; private set; }

            public bool DoubleWordRead { get; private set; }

            public bool DoubleWordWritten { get; private set; }

            public uint ReadDoubleWord(long offset)
            {
                DoubleWordRead = true;
                return 0;
            }

            public void WriteDoubleWord(long offset, uint value)
            {
                DoubleWordWritten = true;
            }

            public class Registration : BusParametrizedRegistration
            {
                public Registration(ulong address, ulong size) : base(address, size, null)
                {
                }

                public override Func<long, byte> GetReadByteMethod(IBusPeripheral peripheral)
                {
                    var parent = peripheral as ParametrizedRegistrationPeripheral;
                    return (offset) =>
                    {
                        parent.ByteRead = true;
                        return 0;
                    };
                }

                public override Action<long, byte> GetWriteByteMethod(IBusPeripheral peripheral)
                {
                    var parent = peripheral as ParametrizedRegistrationPeripheral;
                    return (offset, value) =>
                    {
                        parent.ByteWritten = true;
                    };
                }

                public override void RegisterForEachContext(Action<BusParametrizedRegistration> register)
                {
                    RegisterForEachContextInner(register, _ => new Registration(Range.StartAddress, Range.Size));
                }
            }
        }
    }

    public static class SysbusExtensions
    {
        public static void Register(this SystemBus sysbus, IBusPeripheral peripheral, Antmicro.Renode.Core.Range range)
        {
            sysbus.Register(peripheral, new BusRangeRegistration(range));
        }
    }
}
