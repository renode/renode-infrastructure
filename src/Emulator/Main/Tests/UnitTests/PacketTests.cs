//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

using NUnit.Framework;

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
            Assert.AreEqual(0, Packet.CalculateLength<TestStructNoPacket>());
        }

        [Test]
        public void TestDecode()
        {
            var data = BitHelper.GetBytesFromValue(0xdeadbeef, 4, true);

            var structureA = Packet.Decode<TestStructA>(data);
            Assert.AreEqual(0xdbee, structureA.A);

            var structureB = Packet.Decode<TestStructB>(data);
            Assert.AreEqual(true, structureB.B0);
            Assert.AreEqual(true, structureB.B1);
            Assert.AreEqual(true, structureB.B2);
            Assert.AreEqual(true, structureB.B3);
            Assert.AreEqual(false, structureB.B4);
            Assert.AreEqual(true, structureB.B5);
            Assert.AreEqual(true, structureB.B6);
            Assert.AreEqual(true, structureB.B7);
            Assert.AreEqual(false, structureB.B8);
            Assert.AreEqual(true, structureB.B9);
            Assert.AreEqual(true, structureB.B10);
            Assert.AreEqual(true, structureB.B11);
            Assert.AreEqual(1, structureB.B12);
            Assert.AreEqual(1, structureB.B13);
            Assert.AreEqual(0, structureB.B14);
            Assert.AreEqual(1, structureB.B15);

            var structureC = Packet.Decode<TestStructC>(data);
            Assert.AreEqual(0xef, structureC.C0);
            Assert.AreEqual(0xbe, structureC.C1);
            Assert.AreEqual(0xad, structureC.C2);
            Assert.AreEqual(0xde, structureC.C3);

            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructInvalidWidth>(data));

            var structureNoPacket = Packet.Decode<TestStructNoPacket>(data);
            Assert.AreEqual(0x00, structureNoPacket.Field0);
            Assert.AreEqual(0x00, structureNoPacket.Field1);
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
            Assert.AreEqual(0x1122334455667788, nestedStruct.Field);
            Assert.AreEqual(0xc0fe, nestedStruct.NestedStructA1.NestedStructB.FieldB);
            Assert.AreEqual(0xdeadc0de, nestedStruct.NestedStructA1.FieldA);
        }

        [Test]
        public void TestDecodeEdianness()
        {
            var data = BitHelper.GetBytesFromValue(0xdeadbeefdeadbeef, 8, true);
            var alsb = Packet.Decode<TestStructALSB>(data);
            Assert.AreEqual(0xdeadbeefdeadbeef, alsb.Field0);
            Assert.AreEqual(0xdeadbeef, alsb.Field1);
            Assert.AreEqual(0xbeef, alsb.Field2);
            Assert.AreEqual(0xef, alsb.Field3);
            Assert.AreEqual(0xdeadbeefdeadbeef, (ulong)alsb.Field4);
            Assert.AreEqual(0xdeadbeef, (uint)alsb.Field5);
            Assert.AreEqual(0xbeef, (ushort)alsb.Field6);

            var amsb = Packet.Decode<TestStructAMSB>(data);
            Assert.AreEqual(0xefbeaddeefbeadde, amsb.Field0);
            Assert.AreEqual(0xefbeadde, amsb.Field1);
            Assert.AreEqual(0xefbe, amsb.Field2);
            Assert.AreEqual(0xef, amsb.Field3);
            Assert.AreEqual(0xefbeaddeefbeadde, (ulong)amsb.Field4);
            Assert.AreEqual(0xefbeadde, (uint)amsb.Field5);
            Assert.AreEqual(0xefbe, (ushort)amsb.Field6);
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
            Assert.AreEqual(0x00, structureDefaultOffsets.Field0);
            Assert.AreEqual(0x0201, structureDefaultOffsets.Field1);
            Assert.AreEqual(0x06050403, structureDefaultOffsets.Field2);
            Assert.AreEqual(0x0e0d0c0b0a090807, structureDefaultOffsets.Field3);

            // We only support arrays that are byte aligned
            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructArrayWithOffset>(data2));

            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructA>(insufficientData));

            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructC>(data, data.Length + 1 - Packet.CalculateLength<TestStructC>()));
            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructC>(data, -1));
            Assert.Throws<ArgumentException>(() => Packet.Decode<TestStructArray>(data, -1));

            var structureC = Packet.Decode<TestStructC>(data1, 1);
            Assert.NotNull(structureC);
            Assert.AreEqual(0x00, structureC.C0);
            Assert.AreEqual(0x33, structureC.C3);

            structureC = Packet.Decode<TestStructC>(data1, data1.Length - Packet.CalculateLength<TestStructC>());
            Assert.NotNull(structureC);
            Assert.AreEqual(0xdd, structureC.C0);
            Assert.AreEqual(0xaa, structureC.C3);
        }

        [Test]
        public void TestEncode()
        {
            var structureA = new TestStructA();
            structureA.A = 0x012345;

            Assert.AreEqual(BitHelper.GetBytesFromValue(0x023450, 3, true), Packet.Encode(structureA));

            var structureB = new TestStructB();
            structureB.B0 = true;
            structureB.B1 = true;
            structureB.B2 = true;
            structureB.B3 = true;
            structureB.B4 = false;
            structureB.B5 = true;
            structureB.B6 = true;
            structureB.B7 = true;
            structureB.B8 = false;
            structureB.B9 = true;
            structureB.B10 = true;
            structureB.B11 = true;
            structureB.B12 = 1;
            structureB.B13 = 1;
            structureB.B14 = 0;
            structureB.B15 = 1;

            Assert.AreEqual(BitHelper.GetBytesFromValue(0xbeef, 2, true), Packet.Encode(structureB));

            var structureArray = new TestStructArray();
            structureArray.Array = new byte[5] { 100, 201, 102, 203, 104 };
            Assert.Throws<ArgumentException>(() => Packet.Encode(structureArray));

            structureArray.Array = new byte[3] { 100, 201, 102 };
            Assert.Throws<ArgumentException>(() => Packet.Encode(structureArray));

            structureArray.Array = new byte[4] { 100, 201, 102, 203 };
            Assert.AreEqual(new byte[4] { 100, 201, 102, 203 }, Packet.Encode(structureArray));

            // We only support arrays that are byte aligned
            var structureArrayWithOffset = new TestStructArrayWithOffset();
            structureArrayWithOffset.Array = new byte[] { 0, 1, 2, 3 };
            Assert.Throws<ArgumentException>(() => Packet.Encode(structureArrayWithOffset));

            structureArray.Array = new byte[] { 5, 11, 44, 255 };
            Assert.AreEqual(new byte[] { 5, 11, 44, 255 }, Packet.Encode(structureArray));

            Assert.AreEqual(new byte[0], Packet.Encode(new TestStructZeroWidth()));
            Assert.IsEmpty(Packet.Encode(new TestStructWithEmptyNestedPacket()));

            var structureEnum = new TestStructWithEnums();
            structureEnum.Enum0 = TestEnumByteType.One;
            structureEnum.Enum1 = TestEnumByteType.Two;
            structureEnum.Enum2 = TestEnumDefaultType.Three;
            structureEnum.Enum3 = TestEnumDefaultType.One;

            Assert.AreEqual(new byte[] { 1, 2, 3, 0, 0, 0, 1 }, Packet.Encode(structureEnum));

            // test if uninitialized byte[] field will be filled with zeros
            var testStructWithBytes = new TestStructWithBytes {};
            Assert.AreEqual(new byte[] { 0, 0 }, Packet.Encode(testStructWithBytes));

            var structureNoPacket = new TestStructNoPacket() { Field0 = 0xee, Field1 = 0xff };
            Assert.IsEmpty(Packet.Encode(structureNoPacket));
        }

        [Test]
        public void TestNestedEncode()
        {
            var nestedStruct = new TestNestedStruct
            {
                Field = 0x1122334455667788,
                NestedStructA1 = new NestedStructA
                {
                    NestedStructB = new NestedStructB
                    {
                        FieldB = 0xc0fe
                    },
                    FieldA = 0xdeadc0de
                },
                NestedStructA2 = new NestedStructA
                {
                    NestedStructB = new NestedStructB
                    {
                        FieldB = 0xabcd
                    },
                    FieldA = 0xba5eba11
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
            public ulong A;
#pragma warning restore 649
        }

        private struct TestStructAMSB
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 0), Width(64)]
            public ulong Field0;
            [PacketField, Offset(bits: 0), Width(32)]
            public uint Field1;
            [PacketField, Offset(bits: 0), Width(16)]
            public ushort Field2;
            [PacketField, Offset(bits: 0), Width(8)]
            public byte Field3;
            [PacketField, Offset(bits: 0), Width(64)]
            public long Field4;
            [PacketField, Offset(bits: 0), Width(32)]
            public int Field5;
            [PacketField, Offset(bits: 0), Width(16)]
            public short Field6;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructALSB
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 0), Width(64)]
            public ulong Field0;
            [PacketField, Offset(bits: 0), Width(32)]
            public uint Field1;
            [PacketField, Offset(bits: 0), Width(16)]
            public ushort Field2;
            [PacketField, Offset(bits: 0), Width(8)]
            public byte Field3;
            [PacketField, Offset(bits: 0), Width(64)]
            public long Field4;
            [PacketField, Offset(bits: 0), Width(32)]
            public int Field5;
            [PacketField, Offset(bits: 0), Width(16)]
            public short Field6;
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
            public byte Field;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructB
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 0)]
            public bool B0;
            [PacketField, Offset(bits: 1)]
            public bool B1;
            [PacketField, Offset(bits: 2)]
            public bool B2;
            [PacketField, Offset(bits: 3)]
            public bool B3;
            [PacketField, Offset(bits: 4)]
            public bool B4;
            [PacketField, Offset(bits: 5)]
            public bool B5;
            [PacketField, Offset(bits: 6)]
            public bool B6;
            [PacketField, Offset(bits: 7)]
            public bool B7;
            [PacketField, Offset(bits: 8)]
            public bool B8;
            [PacketField, Offset(bits: 9)]
            public bool B9;
            [PacketField, Offset(bits: 10)]
            public bool B10;
            [PacketField, Offset(bits: 11)]
            public bool B11;
            [PacketField, Offset(bits: 12), Width(1)]
            public byte B12;
            [PacketField, Offset(bits: 13), Width(1)]
            public ushort B13;
            [PacketField, Offset(bits: 14), Width(1)]
            public uint B14;
            [PacketField, Offset(bits: 15), Width(1)]
            public ulong B15;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructC
        {
#pragma warning disable 649
            [PacketField, Offset(bytes: 0)]
            public byte C0;
            [PacketField, Offset(bytes: 1)]
            public byte C1;
            [PacketField, Offset(bytes: 2)]
            public byte C2;
            [PacketField, Offset(bytes: 3)]
            public byte C3;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructDefaultOffsets
        {
#pragma warning disable 649
            [PacketField]
            public byte Field0;
            [PacketField]
            public short Field1;
            [PacketField]
            public int Field2;
            [PacketField]
            public long Field3;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructArray
        {
#pragma warning disable 649
            [PacketField, Width(bytes: 4)]
            public byte[] Array;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructWithOneUsableBit
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 63)]
            public bool Bit;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructArrayWithOffset
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 1), Width(bytes: 4)]
            public byte[] Array;
#pragma warning restore 649
        }

        private struct TestStructWithEmptyNestedPacket
        {
#pragma warning disable 649
            [PacketField]
            public object Unsupported;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructWithEnums
        {
#pragma warning disable 649
            [PacketField]
            public TestEnumByteType Enum0;
            [PacketField, Width(8)]
            public TestEnumByteType Enum1;
            [PacketField]
            public TestEnumDefaultType Enum2;
            [PacketField, Width(8)]
            public TestEnumDefaultType Enum3;
#pragma warning restore 649
        }

        private struct TestStructWithBytes
        {
#pragma warning disable 649
            [PacketField, Width(bytes: 2)]
            public byte[] Field;
#pragma warning restore 649
        }

        private struct NestedStructB
        {
#pragma warning disable 649
            [PacketField]
            public ushort FieldB;
#pragma warning restore 649
        }

        private struct NestedStructA
        {
#pragma warning disable 649
            [PacketField]
            public NestedStructB NestedStructB;
            [PacketField]
            public uint FieldA;
#pragma warning restore 649
        }

        private struct TestNestedStruct
        {
#pragma warning disable 649
            [PacketField]
            public ulong Field;
            [PacketField]
            public NestedStructA NestedStructA1;
            [PacketField]
            public NestedStructA NestedStructA2;
#pragma warning restore 649
        }

        private struct TestStructNoPacket
        {
#pragma warning disable 649
            public byte Field0;
            public byte Field1;
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