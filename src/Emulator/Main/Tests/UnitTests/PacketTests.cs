//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;
using NUnit.Framework;
using System;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class PacketTests
    {
        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void TestLengthCalculation()
        {
            Assert.AreEqual(3, Packet.CalculateLength<TestStructA>());
            Assert.AreEqual(2, Packet.CalculateLength<TestStructB>());
            Assert.AreEqual(4, Packet.CalculateLength<TestStructC>());
            Assert.AreEqual(4, Packet.CalculateLength<TestStructArray>());
            Assert.AreEqual(15, Packet.CalculateLength<TestStructDefaultOffsets>());
            Assert.AreEqual(8, Packet.CalculateLength<TestStructALSB>());
            Assert.AreEqual(8, Packet.CalculateLength<TestStructWithOneUsableBit>());
            Assert.AreEqual(20, Packet.CalculateLength<TestNestedStruct>());
        }

        [Test]
        public void TestDecode()
        {
            var data = BitHelper.GetBytesFromValue(0xdeadbeef, 4, true);

            var structureA = Packet.Decode<TestStructA>(data);
            Assert.AreEqual(0xdbee, structureA.a);

            var structureB = Packet.Decode<TestStructB>(data);
            Assert.AreEqual(true, structureB.b0);
            Assert.AreEqual(true, structureB.b1);
            Assert.AreEqual(true, structureB.b2);
            Assert.AreEqual(true, structureB.b3);
            Assert.AreEqual(false, structureB.b4);
            Assert.AreEqual(true, structureB.b5);
            Assert.AreEqual(true, structureB.b6);
            Assert.AreEqual(true, structureB.b7);
            Assert.AreEqual(false, structureB.b8);
            Assert.AreEqual(true, structureB.b9);
            Assert.AreEqual(true, structureB.b10);
            Assert.AreEqual(true, structureB.b11);
            Assert.AreEqual(1, structureB.b12);
            Assert.AreEqual(1, structureB.b13);
            Assert.AreEqual(0, structureB.b14);
            Assert.AreEqual(1, structureB.b15);

            var structureC = Packet.Decode<TestStructC>(data);
            Assert.AreEqual(0xef, structureC.c0);
            Assert.AreEqual(0xbe, structureC.c1);
            Assert.AreEqual(0xad, structureC.c2);
            Assert.AreEqual(0xde, structureC.c3);

            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructInvalidWidth>(data));
        }

        [Test]
        public void TestNestedDecode()
        {
            var data = new byte[]
            {
                0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
                0xc0, 0xfe, 0xde, 0xad, 0xc0, 0xde, 0xab, 0xcd,
                0xba, 0x5e, 0xba, 0x11
            };

            var nestedStruct = Packet.Decode<TestNestedStruct>(data);
            Assert.AreEqual(0x1122334455667788, nestedStruct.field);
            Assert.AreEqual(0xc0fe, nestedStruct.nestedStructA1.nestedStructB.fieldB);
            Assert.AreEqual(0xdeadc0de, nestedStruct.nestedStructA1.fieldA);
        }

        [Test]
        public void TestDecodeEdianness()
        {
            var data = BitHelper.GetBytesFromValue(0xdeadbeefdeadbeef, 8, true);
            var alsb = Packet.Decode<TestStructALSB>(data);
            Assert.AreEqual(0xdeadbeefdeadbeef, alsb.field0);
            Assert.AreEqual(0xdeadbeef, alsb.field1);
            Assert.AreEqual(0xbeef, alsb.field2);
            Assert.AreEqual(0xef, alsb.field3);
            Assert.AreEqual(0xdeadbeefdeadbeef, (ulong)alsb.field4);
            Assert.AreEqual(0xdeadbeef, (uint)alsb.field5);
            Assert.AreEqual(0xbeef, (ushort)alsb.field6);

            var amsb = Packet.Decode<TestStructAMSB>(data);
            Assert.AreEqual(0xefbeaddeefbeadde, amsb.field0);
            Assert.AreEqual(0xefbeadde, amsb.field1);
            Assert.AreEqual(0xefbe, amsb.field2);
            Assert.AreEqual(0xef, amsb.field3);
            Assert.AreEqual(0xefbeaddeefbeadde, (ulong)amsb.field4);
            Assert.AreEqual(0xefbeadde, (uint)amsb.field5);
            Assert.AreEqual(0xefbe, (ushort)amsb.field6);
        }

        [Test]
        public void TestDecodeFieldOffsets()
        {
            var data = BitHelper.GetBytesFromValue(0xdeadbeefdeadbeef, 8, true);
            var data1 = new byte[]
            {
                0xff, 0x00, 0x11, 0x22, 0x33, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xdd, 0xcc,
                0xbb, 0xaa
            };
            var data2 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 };
            var insufficientData = new byte[0];

            var structureDefaultOffsets = Packet.Decode<TestStructDefaultOffsets>(data2);
            Assert.AreEqual(0x00, structureDefaultOffsets.field0);
            Assert.AreEqual(0x0201, structureDefaultOffsets.field1);
            Assert.AreEqual(0x06050403, structureDefaultOffsets.field2);
            Assert.AreEqual(0x0e0d0c0b0a090807, structureDefaultOffsets.field3);

            // We only support arrays that are byte aligned
            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructArrayWithOffset>(data2));

            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructA>(insufficientData));

            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructC>(data, data.Length + 1 - Packet.CalculateLength<TestStructC>()));
            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructC>(data, -1));
            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructArray>(data, -1));

            var structureC = Packet.Decode<TestStructC>(data1, 1);
            Assert.NotNull(structureC);
            Assert.AreEqual(0x00, structureC.c0);
            Assert.AreEqual(0x33, structureC.c3);

            structureC = Packet.Decode<TestStructC>(data1, data1.Length - Packet.CalculateLength<TestStructC>());
            Assert.NotNull(structureC);
            Assert.AreEqual(0xdd, structureC.c0);
            Assert.AreEqual(0xaa, structureC.c3);
        }

        [Test]
        public void TestEncode()
        {
            var structureA = new TestStructA();
            structureA.a = 0x012345;

            Assert.AreEqual(BitHelper.GetBytesFromValue(0x023450, 3, true), Packet.Encode(structureA));

            var structureB = new TestStructB();
            structureB.b0 = true;
            structureB.b1 = true;
            structureB.b2 = true;
            structureB.b3 = true;
            structureB.b4 = false;
            structureB.b5 = true;
            structureB.b6 = true;
            structureB.b7 = true;
            structureB.b8 = false;
            structureB.b9 = true;
            structureB.b10 = true;
            structureB.b11 = true;
            structureB.b12 = 1;
            structureB.b13 = 1;
            structureB.b14 = 0;
            structureB.b15 = 1;

            Assert.AreEqual(BitHelper.GetBytesFromValue(0xbeef, 2, true), Packet.Encode(structureB));

            var structureArray = new TestStructArray();
            structureArray.array = new byte[5] { 100, 201, 102, 203, 104 };
            Assert.Throws<ArgumentException>(() => Packet.Encode(structureArray));

            structureArray.array = new byte[3] { 100, 201, 102 };
            Assert.Throws<ArgumentException>(() => Packet.Encode(structureArray));

            structureArray.array = new byte[4] { 100, 201, 102, 203 };
            Assert.AreEqual(new byte[4] { 100, 201, 102, 203 }, Packet.Encode(structureArray));

            // We only support arrays that are byte aligned
            var structureArrayWithOffset = new TestStructArrayWithOffset();
            structureArrayWithOffset.array = new byte[] { 0, 1, 2, 3 };
            Assert.Throws<ArgumentException>(() => Packet.Encode(structureArrayWithOffset));

            structureArray.array = new byte[] { 5, 11, 44, 255 };
            Assert.AreEqual(new byte[] { 5, 11, 44, 255 }, Packet.Encode(structureArray));

            Assert.AreEqual(new byte[0], Packet.Encode(new TestStructZeroWidth()));
            Assert.IsEmpty(Packet.Encode(new TestStructWithEmptyNestedPacket()));

            var structureEnum = new TestStructWithEnums();
            structureEnum.enum0 = TestEnumByteType.One;
            structureEnum.enum1 = TestEnumByteType.Two;
            structureEnum.enum2 = TestEnumDefaultType.Three;
            structureEnum.enum3 = TestEnumDefaultType.One;

            Assert.AreEqual(new byte[] { 1, 2, 3, 0, 0, 0, 1 }, Packet.Encode(structureEnum));

            // test if uninitialized byte[] field will be filled with zeros
            var testStructWithBytes = new TestStructWithBytes {};
            Assert.AreEqual(new byte[] { 0, 0 }, Packet.Encode(testStructWithBytes));
        }

        [Test]
        public void TestNestedEncode()
        {
            var nestedStruct = new TestNestedStruct
            {
                field = 0x1122334455667788,
                nestedStructA1 = new NestedStructA
                {
                    nestedStructB = new NestedStructB
                    {
                        fieldB = 0xc0fe
                    },
                    fieldA = 0xdeadc0de
                },
                nestedStructA2 = new NestedStructA
                {
                    nestedStructB = new NestedStructB
                    {
                        fieldB = 0xabcd
                    },
                    fieldA = 0xba5eba11
                }
            };
            var bytes = new byte[]
            {
                0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
                0xc0, 0xfe, 0xde, 0xad, 0xc0, 0xde, 0xab, 0xcd,
                0xba, 0x5e, 0xba, 0x11
            };

            Assert.AreEqual(bytes, Packet.Encode(nestedStruct));
        }

        [LeastSignificantByteFirst]
        private struct TestStructA
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 4), Width(16)]
            public ulong a;
#pragma warning restore 649
        }

        private struct TestStructAMSB
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 0), Width(64)]
            public ulong field0;
            [PacketField, Offset(bits: 0), Width(32)]
            public uint field1;
            [PacketField, Offset(bits: 0), Width(16)]
            public ushort field2;
            [PacketField, Offset(bits: 0), Width(8)]
            public byte field3;
            [PacketField, Offset(bits: 0), Width(64)]
            public long field4;
            [PacketField, Offset(bits: 0), Width(32)]
            public int field5;
            [PacketField, Offset(bits: 0), Width(16)]
            public short field6;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructALSB
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 0), Width(64)]
            public ulong field0;
            [PacketField, Offset(bits: 0), Width(32)]
            public uint field1;
            [PacketField, Offset(bits: 0), Width(16)]
            public ushort field2;
            [PacketField, Offset(bits: 0), Width(8)]
            public byte field3;
            [PacketField, Offset(bits: 0), Width(64)]
            public long field4;
            [PacketField, Offset(bits: 0), Width(32)]
            public int field5;
            [PacketField, Offset(bits: 0), Width(16)]
            public short field6;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructZeroWidth
        {
        }

        [LeastSignificantByteFirst]
        private struct TestStructInvalidWidth
        {
#pragma warning disable 649
            [PacketField, Width(64)]
            public byte field;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructB
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 0)]
            public bool b0;
            [PacketField, Offset(bits: 1)]
            public bool b1;
            [PacketField, Offset(bits: 2)]
            public bool b2;
            [PacketField, Offset(bits: 3)]
            public bool b3;
            [PacketField, Offset(bits: 4)]
            public bool b4;
            [PacketField, Offset(bits: 5)]
            public bool b5;
            [PacketField, Offset(bits: 6)]
            public bool b6;
            [PacketField, Offset(bits: 7)]
            public bool b7;
            [PacketField, Offset(bits: 8)]
            public bool b8;
            [PacketField, Offset(bits: 9)]
            public bool b9;
            [PacketField, Offset(bits: 10)]
            public bool b10;
            [PacketField, Offset(bits: 11)]
            public bool b11;
            [PacketField, Offset(bits: 12), Width(1)]
            public byte b12;
            [PacketField, Offset(bits: 13), Width(1)]
            public ushort b13;
            [PacketField, Offset(bits: 14), Width(1)]
            public uint b14;
            [PacketField, Offset(bits: 15), Width(1)]
            public ulong b15;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructC
        {
#pragma warning disable 649
            [PacketField, Offset(bytes: 0)]
            public byte c0;
            [PacketField, Offset(bytes: 1)]
            public byte c1;
            [PacketField, Offset(bytes: 2)]
            public byte c2;
            [PacketField, Offset(bytes: 3)]
            public byte c3;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructDefaultOffsets
        {
#pragma warning disable 649
            [PacketField]
            public byte field0;
            [PacketField]
            public short field1;
            [PacketField]
            public int field2;
            [PacketField]
            public long field3;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructArray
        {
#pragma warning disable 649
            [PacketField, Width(4)]
            public byte[] array;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructWithOneUsableBit
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 63)]
            public bool bit;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructArrayWithOffset
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 1), Width(4)]
            public byte[] array;
#pragma warning restore 649
        }

        private struct TestStructWithEmptyNestedPacket
        {
#pragma warning disable 649
            [PacketField]
            public object unsupported;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructWithEnums
        {
#pragma warning disable 649
            [PacketField]
            public TestEnumByteType enum0;
            [PacketField, Width(8)]
            public TestEnumByteType enum1;
            [PacketField]
            public TestEnumDefaultType enum2;
            [PacketField, Width(8)]
            public TestEnumDefaultType enum3;
#pragma warning restore 649
        }

        private struct TestStructWithBytes
        {
#pragma warning disable 649
            [PacketField, Width(2)]
            public byte[] field;
#pragma warning restore 649
        }

        private struct NestedStructB
        {
#pragma warning disable 649
            [PacketField]
            public ushort fieldB;
#pragma warning restore 649
        }

        private struct NestedStructA
        {
#pragma warning disable 649
            [PacketField]
            public NestedStructB nestedStructB;
            [PacketField]
            public uint fieldA;
#pragma warning restore 649
        }

        private struct TestNestedStruct
        {
#pragma warning disable 649
            [PacketField]
            public ulong field;
            [PacketField]
            public NestedStructA nestedStructA1;
            [PacketField]
            public NestedStructA nestedStructA2;
#pragma warning restore 649
        }

        private enum TestEnumByteType : byte
        {
            One = 1,
            Two,
            Three
        }

        private enum TestEnumDefaultType
        {
            One = 1,
            Two,
            Three
        }
    }
}
