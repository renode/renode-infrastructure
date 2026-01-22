//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

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
            Assert.AreEqual(0, Packet.CalculateLength<TestStructExplicitZeroWidth>());
            Assert.AreEqual(3, Packet.CalculateLength<TestStructExplicitZeroWidthWithOffset>());
            Assert.AreEqual(1, Packet.CalculateLength<TestStructLessThanByte>());
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

            var structureExplicitZeroWidth = Packet.Decode<TestStructExplicitZeroWidth>(data);
            Assert.AreEqual(0x00, structureExplicitZeroWidth.Field);

            var structureExplicitZeroWidthWithOffset = Packet.Decode<TestStructExplicitZeroWidthWithOffset>(data);
            Assert.AreEqual(0x00, structureExplicitZeroWidthWithOffset.Field);

            var structureLessThanByte = Packet.Decode<TestStructLessThanByte>(data);
            Assert.AreEqual(true, structureLessThanByte.Field0);
            Assert.AreEqual(true, structureLessThanByte.Field1);
            Assert.AreEqual(true, structureLessThanByte.Field2);
            Assert.AreEqual(true, structureLessThanByte.Field3);
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

            var structureExplicitZeroWidth = new TestStructExplicitZeroWidth() { Field = 0xee };
            Assert.IsEmpty(Packet.Encode(structureExplicitZeroWidth));

            var structureExplicitZeroWidthWithOffset = new TestStructExplicitZeroWidthWithOffset() { Field = 0xee };
            Assert.Throws<IndexOutOfRangeException>(() => Packet.Encode(structureExplicitZeroWidthWithOffset));

            var structureLessThanByte = new TestStructLessThanByte() { Field0 = true, Field1 = false, Field2 = true, Field3 = false };
            Assert.AreEqual(new byte[] { 0x05 }, Packet.Encode(structureLessThanByte));
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

        [Test]
        public void TestWidthInElements()
        {
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var structUshorts = Packet.Decode<TestStructWidthElementsUshortArray>(data);
            var structUshortsWithExplicit = Packet.Decode<TestStructWidthElementsAndExplicitUshortArray>(data);

            Assert.AreEqual(new ushort[] { 0x0201, 0x0403 }, structUshorts.Data);
            Assert.AreEqual(new ushort[] { 0x0201, 0x0403 }, structUshortsWithExplicit.Data);
            Assert.AreEqual(data, Packet.Encode(structUshorts));
            Assert.AreEqual(data, Packet.Encode(structUshortsWithExplicit));
            Assert.AreEqual(4, Packet.CalculateLength<TestStructWidthElementsUshortArray>());
            Assert.AreEqual(4, Packet.CalculateLength<TestStructWidthElementsAndExplicitUshortArray>());
        }

        [Test]
        public void TestWidthInElementsErrors()
        {
            // Specifying elements for a non-array type
            Assert.Throws<ArgumentException>(() => Packet.CalculateLength<TestStructWidthElementsNonArray>());

            // Specifying an absolute width that conflicts with the element count
            Assert.Throws<ArgumentException>(() => Packet.CalculateLength<TestStructWidthElementsAndConflictingExplicit>());
        }

        [Test]
        public void TestPaddingBefore()
        {
            var data = new byte[] { 0xAA, 0x12, 0x34, 0xBB };
            var structure = Packet.Decode<TestStructPadding>(data);

            Assert.AreEqual(0xAA, structure.A);
            Assert.AreEqual(0xBB, structure.B);
            Assert.AreEqual(4, Packet.CalculateLength<TestStructPadding>());

            var encoded = Packet.Encode(structure);
            Assert.AreEqual(new byte[] { 0xAA, 0x00, 0x00, 0xBB }, encoded);
        }

        [Test]
        public void TestAlignment()
        {
            //                            |    padding   |
            var data = new byte[] { 0x0A, 0xFF, 0xFF, 0xFF, 0x44, 0x33, 0x22, 0x11 };
            var structure = Packet.Decode<TestStructAlign>(data);

            Assert.AreEqual(0x0A, structure.A);
            Assert.AreEqual(0x11223344, structure.B);
            Assert.AreEqual(8, Packet.CalculateLength<TestStructAlign>());

            var encoded = Packet.Encode(structure);
            Assert.AreEqual(0x0A, encoded[0]);
            Assert.AreEqual(new byte[] { 0x00, 0x00, 0x00 }, encoded.Skip(1).Take(3)); // padding zeroed
            Assert.AreEqual(0x11223344, BitConverter.ToUInt32(encoded, 4));
        }

        [Test]
        public void TestPresentIf()
        {
            // If flag is true the struct is 5 bytes (1 flag + 4 val)
            var dataTrue = new byte[] { 0x01, 0xAA, 0xBB, 0xCC, 0xDD };
            var structureTrue = Packet.Decode<TestStructOptionalField>(dataTrue);
            Assert.IsTrue(structureTrue.Flag);
            Assert.AreEqual(0xDDCCBBAA, structureTrue.Val);

            // If flag is false the struct is only 1 byte and the extra should be ignored
            var dataFalse = new byte[] { 0x00, 0x12, 0x34, 0x56, 0x78 };
            var structureFalse = Packet.Decode<TestStructOptionalField>(dataFalse);
            Assert.IsFalse(structureFalse.Flag);
            Assert.AreEqual(0, structureFalse.Val);

            var encTrue = Packet.Encode(structureTrue);
            Assert.AreEqual(dataTrue, encTrue);

            var encFalse = Packet.Encode(structureFalse);
            Assert.AreEqual(new byte[] { 0x00 }, encFalse);
        }

        [Test]
        public void TestPresentIfBitfield()
        {
            // No fields (Mask = 0)
            var dataNone = new byte[] { 0x00, 0xAA };
            var structureNone = Packet.Decode<TestStructOptionalFieldsByBitfield>(dataNone);
            Assert.AreEqual(0, structureNone.Mask);
            Assert.AreEqual(0, structureNone.FieldA);
            Assert.AreEqual(null, structureNone.FieldB);

            // Only A present (Mask = 1)
            var dataA = new byte[] { 0x01, 0xAA };
            var structureA = Packet.Decode<TestStructOptionalFieldsByBitfield>(dataA);
            Assert.AreEqual(1, structureA.Mask);
            Assert.AreEqual(0xAA, structureA.FieldA);
            Assert.AreEqual(null, structureA.FieldB);
            Assert.AreEqual(dataA, Packet.Encode(structureA));

            // Only B present (Mask = 2)
            var dataB = new byte[] { 0x02, 0xBB };
            var structureB = Packet.Decode<TestStructOptionalFieldsByBitfield>(dataB);
            Assert.AreEqual(2, structureB.Mask);
            Assert.AreEqual(0, structureB.FieldA);
            Assert.AreEqual(0xBB, structureB.FieldB);
            Assert.AreEqual(dataB, Packet.Encode(structureB));

            // Both present (Mask = 3)
            var dataBoth = new byte[] { 0x03, 0xAA, 0xBB };
            var structureBoth = Packet.Decode<TestStructOptionalFieldsByBitfield>(dataBoth);
            Assert.AreEqual(3, structureBoth.Mask);
            Assert.AreEqual(0xAA, structureBoth.FieldA);
            Assert.AreEqual(0xBB, structureBoth.FieldB);
            Assert.AreEqual(dataBoth, Packet.Encode(structureBoth));
        }

        [Test]
        public void TestCalculateLengthWithOptionalFields()
        {
            Assert.AreEqual(5, Packet.CalculateLength<TestStructOptionalField>()); // upper bound

            // Exact length
            var structureTrue = new TestStructOptionalField { Flag = true, Val = 0x12345678 };
            Assert.AreEqual(5, Packet.CalculateLength(structureTrue));
            var structureFalse = new TestStructOptionalField { Flag = false };
            Assert.AreEqual(1, Packet.CalculateLength(structureFalse));

            Assert.AreEqual(3, Packet.CalculateLength<TestStructOptionalFieldsByBitfield>()); // upper bound
            Assert.AreEqual(1, Packet.CalculateLength(new TestStructOptionalFieldsByBitfield { Mask = 0 })); // No fields (Mask = 0)
            Assert.AreEqual(2, Packet.CalculateLength(new TestStructOptionalFieldsByBitfield { Mask = 1 })); // Only A present (Mask = 1)
            Assert.AreEqual(2, Packet.CalculateLength(new TestStructOptionalFieldsByBitfield { Mask = 2 })); // Only B present (Mask = 2)
            Assert.AreEqual(3, Packet.CalculateLength(new TestStructOptionalFieldsByBitfield { Mask = 3 })); // Both present (Mask = 3)
        }

        [Test]
        public void TestCalculateOffsetWithOptionalFields()
        {
            // Upper bound offsets
            Assert.AreEqual(0, Packet.CalculateOffset<TestStructOptionalFieldGap>(nameof(TestStructOptionalFieldGap.Flag)));
            Assert.AreEqual(1, Packet.CalculateOffset<TestStructOptionalFieldGap>(nameof(TestStructOptionalFieldGap.Optional)));
            Assert.AreEqual(2, Packet.CalculateOffset<TestStructOptionalFieldGap>(nameof(TestStructOptionalFieldGap.Last)));

            // Without Optional present
            var structureFalse = new TestStructOptionalFieldGap { Flag = false };
            Assert.AreEqual(0, Packet.CalculateOffset(structureFalse, nameof(TestStructOptionalFieldGap.Flag)));
            Assert.AreEqual(-1, Packet.CalculateOffset(structureFalse, nameof(TestStructOptionalFieldGap.Optional)));
            Assert.AreEqual(1, Packet.CalculateOffset(structureFalse, nameof(TestStructOptionalFieldGap.Last)));

            // With Optional present
            var structureTrue = new TestStructOptionalFieldGap { Flag = true };
            Assert.AreEqual(0, Packet.CalculateOffset(structureTrue, nameof(TestStructOptionalFieldGap.Flag)));
            Assert.AreEqual(1, Packet.CalculateOffset(structureTrue, nameof(TestStructOptionalFieldGap.Optional)));
            Assert.AreEqual(2, Packet.CalculateOffset(structureTrue, nameof(TestStructOptionalFieldGap.Last)));
        }

        [Test]
        public void TestNestedStructWithOptionalFields()
        {
            var dataMissing = new byte[] { 0x00, 0x11 };
            var structureMissing = Packet.Decode<TestStructNestedOptionalParent>(dataMissing);

            Assert.IsFalse(structureMissing.Nested.Flag);
            Assert.AreEqual(0, structureMissing.Nested.Val);
            Assert.AreEqual(0x11, structureMissing.After);

            var dataPresent = new byte[] { 0x01, 0x44, 0x33, 0x22, 0x11, 0x55 };
            var structurePresent = Packet.Decode<TestStructNestedOptionalParent>(dataPresent);

            Assert.IsTrue(structurePresent.Nested.Flag);
            Assert.AreEqual(0x11223344, structurePresent.Nested.Val);
            Assert.AreEqual(0x55, structurePresent.After);
        }

        [Test]
        public void TestSerializingReadOnlyProperties()
        {
            var data = new TestStructWithReadOnlyProperty();
            var bytes = Packet.Encode(data);
            Assert.AreEqual(data.Value, bytes[0]);
        }

        [LeastSignificantByteFirst]
        private struct TestStructA
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 4), Width(bits: 16)]
            public ulong A;
#pragma warning restore 649
        }

        private struct TestStructAMSB
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 0), Width(bits: 64)]
            public ulong Field0;
            [PacketField, Offset(bits: 0), Width(bits: 32)]
            public uint Field1;
            [PacketField, Offset(bits: 0), Width(bits: 16)]
            public ushort Field2;
            [PacketField, Offset(bits: 0), Width(bits: 8)]
            public byte Field3;
            [PacketField, Offset(bits: 0), Width(bits: 64)]
            public long Field4;
            [PacketField, Offset(bits: 0), Width(bits: 32)]
            public int Field5;
            [PacketField, Offset(bits: 0), Width(bits: 16)]
            public short Field6;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructALSB
        {
#pragma warning disable 649
            [PacketField, Offset(bits: 0), Width(bits: 64)]
            public ulong Field0;
            [PacketField, Offset(bits: 0), Width(bits: 32)]
            public uint Field1;
            [PacketField, Offset(bits: 0), Width(bits: 16)]
            public ushort Field2;
            [PacketField, Offset(bits: 0), Width(bits: 8)]
            public byte Field3;
            [PacketField, Offset(bits: 0), Width(bits: 64)]
            public long Field4;
            [PacketField, Offset(bits: 0), Width(bits: 32)]
            public int Field5;
            [PacketField, Offset(bits: 0), Width(bits: 16)]
            public short Field6;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructZeroWidth
        {
        }

        [LeastSignificantByteFirst]
        private struct TestStructExplicitZeroWidth
        {
#pragma warning disable 649
            [PacketField, Width(bits: 0)]
            public byte Field;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructExplicitZeroWidthWithOffset
        {
#pragma warning disable 649
            [PacketField, Width(bits: 0), Offset(bytes: 3)]
            public byte Field;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructInvalidWidth
        {
#pragma warning disable 649
            [PacketField, Width(bits: 64)]
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
            [PacketField, Offset(bits: 12), Width(bits: 1)]
            public byte B12;
            [PacketField, Offset(bits: 13), Width(bits: 1)]
            public ushort B13;
            [PacketField, Offset(bits: 14), Width(bits: 1)]
            public uint B14;
            [PacketField, Offset(bits: 15), Width(bits: 1)]
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
        private struct TestStructLessThanByte
        {
#pragma warning disable 649
            [PacketField, Width(bits: 1), Offset(bits: 0)]
            public bool Field0;
            [PacketField, Width(bits: 1), Offset(bits: 1)]
            public bool Field1;
            [PacketField, Width(bits: 1), Offset(bits: 2)]
            public bool Field2;
            [PacketField, Width(bits: 1), Offset(bits: 3)]
            public bool Field3;
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
            [PacketField, Width(bits: 8)]
            public TestEnumByteType Enum1;
            [PacketField]
            public TestEnumDefaultType Enum2;
            [PacketField, Width(bits: 8)]
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

        [LeastSignificantByteFirst]
        private struct TestStructWidthElementsUshortArray
        {
#pragma warning disable 649
            [PacketField, Width(elements: 2)]
            public ushort[] Data;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructWidthElementsAndExplicitUshortArray
        {
#pragma warning disable 649
            [PacketField, Width(elements: 2, bytes: 4)]
            public ushort[] Data;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructWidthElementsNonArray
        {
#pragma warning disable 649
            [PacketField, Width(elements: 2)]
            public uint Val;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructWidthElementsAndConflictingExplicit
        {
#pragma warning disable 649
            [PacketField, Width(elements: 4, bytes: 5)]
            public byte[] Data;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructPadding
        {
#pragma warning disable 649
            [PacketField]
            public byte A;
            [PacketField, PaddingBefore(bytes: 2)]
            public byte B;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructAlign
        {
#pragma warning disable 649
            [PacketField]
            public byte A; // 0, +1
            [PacketField, Align(bytes: 4)]
            public uint B; // 4, +4
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructOptionalField
        {
#pragma warning disable 649
            [PacketField]
            public bool Flag;
            [PacketField, PresentIf(nameof(HasVal))]
            public uint Val;

            public bool HasVal() => Flag;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructOptionalFieldGap
        {
#pragma warning disable 649
            [PacketField]
            public bool Flag;
            [PacketField, PresentIf(nameof(HasFlag))]
            public byte Optional;
            [PacketField]
            public byte Last;

            public bool HasFlag() => Flag;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructOptionalFieldsByBitfield
        {
#pragma warning disable 649
            [PacketField]
            public byte Mask;

            [PacketField, PresentIf(nameof(HasA))]
            public byte FieldA;

            [PacketField, PresentIf(nameof(HasB))]
            public byte? FieldB;

            public bool HasA() => (Mask & 1) != 0;

            public bool HasB() => (Mask & 2) != 0;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructNestedOptionalParent
        {
#pragma warning disable 649
            [PacketField]
            public TestStructOptionalField Nested;

            [PacketField]
            public byte After;
#pragma warning restore 649
        }

        [LeastSignificantByteFirst]
        private struct TestStructWithReadOnlyProperty
        {
            [PacketField, Width(bits: 8)]
            public byte Value => 0xAA;
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
