//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class RegistersTests
    {
        [Test]
        public void ShouldNotAcceptOutOfBoundsValues()
        {
            Assert.Catch<ConstructionException>(() => enumRWField.Value = (TwoBitEnum)(1 << 2));
            Assert.Catch<ConstructionException>(() => valueRWField.Value = (1 << 4));
        }

        [Test]
        public void ShouldNotAcceptNegativeFields()
        {
            var localRegister = new DoubleWordRegister(null);
            Assert.Catch<ArgumentException>(() => localRegister.DefineEnumField<TwoBitEnum>(0, -1));
            Assert.Catch<ArgumentException>(() => localRegister.DefineValueField(0, -1));
        }

        [Test]
        public void ShouldNotExceedRegisterSize()
        {
            var registersAndPositions = new Dictionary<PeripheralRegister, int>
            {
                { new QuadWordRegister(null), 63 },
                { new DoubleWordRegister(null), 31 },
                { new WordRegister(null), 15 },
                { new ByteRegister(null), 7 }
            };
            foreach(var registerAndPosition in registersAndPositions)
            {
                var localRegister = registerAndPosition.Key;
                Assert.Catch<ArgumentException>(() => localRegister.DefineEnumField<TwoBitEnum>(registerAndPosition.Value, 2));
            }
        }

        [Test]
        public void ShouldNotAllowIntersectingFields()
        {
            var localRegister = new QuadWordRegister(null);
            localRegister.DefineValueField(1, 5);
            Assert.Catch<ArgumentException>(() => localRegister.DefineValueField(0, 2));
        }

        [Test]
        public void ShouldWriteFieldsWithMaxLength()
        {
            var localRegister = new DoubleWordRegister(null);
            localRegister.DefineValueField(0, 32);
            localRegister.Write(0, uint.MaxValue);
            Assert.AreEqual(uint.MaxValue, localRegister.Read());
        }

        [Test]
        public void ShouldReadBoolField()
        {
            register.Write(0, 1 << 2);
            Assert.AreEqual(true, flagRWField.Value);
        }

        [Test]
        public void ShouldReadEnumField()
        {
            register.Write(0, 3);
            Assert.AreEqual(TwoBitEnum.D, enumRWField.Value);
        }

        [Test]
        public void ShouldRead64BitWideEnum()
        {
            var localRegister = new QuadWordRegister(null);
            var localEnumField = localRegister.DefineEnumField<SixtyFourBitEnum>(0, 64);

            localRegister.Write(0, ulong.MaxValue);
            Assert.AreEqual(SixtyFourBitEnum.B, localEnumField.Value);
        }

        [Test]
        public void ShouldReadValueField()
        {
            register.Write(0, 88); //1011000
            Assert.AreEqual(11, valueRWField.Value); //1011
        }

        [Test]
        public void ShouldWriteBoolField()
        {
            flagRWField.Value = true;
            Assert.AreEqual(1 << 2 | RegisterResetValue, register.Read());
        }

        [Test]
        public void ShouldWriteEnumField()
        {
            enumRWField.Value = TwoBitEnum.D;
            Assert.AreEqual((uint)TwoBitEnum.D | RegisterResetValue, register.Read());
        }

        [Test]
        public void ShouldWrite64BitWideEnum()
        {
            var localRegister = new QuadWordRegister(null);
            var localEnumField = localRegister.DefineEnumField<SixtyFourBitEnum>(0, 64);

            localEnumField.Value = SixtyFourBitEnum.A;
            Assert.AreEqual((ulong)SixtyFourBitEnum.A, localRegister.Read());
        }

        [Test]
        public void ShouldWriteValueField()
        {
            valueRWField.Value = 11;
            Assert.AreEqual(88 | RegisterResetValue, register.Read());
        }

        [Test]
        public void ShouldResetComplexRegister()
        {
            register.Reset();
            Assert.AreEqual(0x3780, register.Read());
        }

        [Test]
        public void ShouldNotWriteUnwritableField()
        {
            register.Write(0, 1 << 21);
            Assert.AreEqual(false, flagRField.Value);
        }

        [Test]
        public void ShouldNotReadUnreadableField()
        {
            flagWField.Value = true;
            Assert.AreEqual(RegisterResetValue, register.Read());
        }

        [Test]
        public void ShouldWriteZeroToClear()
        {
            flagW0CField.Value = true;
            Assert.AreEqual(1 << 18 | RegisterResetValue, register.Read());
            register.Write(0, 0);
            Assert.AreEqual(false, flagW0CField.Value);
            Assert.AreEqual(0, register.Read() & readMask);
        }

        [Test]
        public void ShouldWriteOneToClear()
        {
            flagW1CField.Value = true;
            Assert.AreEqual(1 << 17 | RegisterResetValue, register.Read() & readMask);
            register.Write(0, 1 << 17);
            Assert.AreEqual(false, flagW0CField.Value);
            Assert.AreEqual(0, register.Read() & readMask);
        }

        [Test]
        public void ShouldWriteToClear()
        {
            flagWTCField.Value = true;
            Assert.AreEqual(1u << 31 | RegisterResetValue, register.Read());
            register.Write(0, 1u << 31);
            Assert.IsFalse(flagWTCField.Value);

            flagWTCField.Value = true;
            register.Write(0, 0);
            Assert.IsFalse(flagWTCField.Value);
        }

        [Test]
        public void ShouldReadToClear()
        {
            flagWRTCField.Value = true;
            Assert.AreEqual(1 << 19 | RegisterResetValue, register.Read());
            Assert.AreEqual(false, flagWRTCField.Value);
        }

        [Test]
        public void ShouldWriteZeroToSet()
        {
            flagWZTSField.Value = false;

            register.Write(0, 1 << 0x1c);
            Assert.IsFalse(flagWZTSField.Value);

            register.Write(0, 0);
            Assert.IsTrue(flagWZTSField.Value);

            register.Write(0, 0);
            Assert.IsTrue(flagWZTSField.Value);

            register.Write(0, 1 << 0x1c);
            Assert.IsTrue(flagWZTSField.Value);
        }

        [Test]
        public void ShouldWriteZeroToToggle()
        {
            flagWZTTField.Value = false;

            register.Write(0, 1 << 0x1d);
            Assert.IsFalse(flagWZTTField.Value);

            register.Write(0, 0);
            Assert.IsTrue(flagWZTTField.Value);

            register.Write(0, 1 << 0x1d);
            Assert.IsTrue(flagWZTTField.Value);

            register.Write(0, 0);
            Assert.IsFalse(flagWZTTField.Value);
        }

        [Test]
        public void ShouldReadToSet()
        {
            flagRTSField.Value = false;
            Assert.AreEqual(~(1UL << 0x1e) & RegisterResetValue, register.Read());
            Assert.IsTrue(flagRTSField.Value);
        }

        [Test]
        public void ShouldCallReadHandler()
        {
            //for the sake of sanity
            Assert.AreEqual(0, enumCallbacks);
            Assert.AreEqual(0, boolCallbacks);
            Assert.AreEqual(0, numberCallbacks);
            register.Read();
            Assert.AreEqual(1, enumCallbacks);
            Assert.AreEqual(1, boolCallbacks);
            Assert.AreEqual(1, numberCallbacks);

            Assert.IsTrue(oldBoolValue == newBoolValue);
            Assert.IsTrue(oldEnumValue == newEnumValue);
            Assert.IsTrue(oldNumberValue == newNumberValue);
        }

        [Test]
        public void ShouldRetrieveValueFromHandler()
        {
            enableValueProviders = true;
            register.Write(0, 3 << 0x16 | 1 << 0x19);
            Assert.AreEqual(4 << 0x16 | 1 << 0x1A, register.Read());
        }

        [Test]
        public void ShouldCallWriteAndChangeHandler()
        {
            Assert.AreEqual(0, enumCallbacks);
            Assert.AreEqual(0, boolCallbacks);
            Assert.AreEqual(0, numberCallbacks);
            register.Write(0, 0x2A80);
            //Two calls for changed registers, 1 call for unchanged register
            Assert.AreEqual(2, enumCallbacks);
            Assert.AreEqual(1, boolCallbacks);
            Assert.AreEqual(2, numberCallbacks);

            Assert.IsTrue(oldBoolValue == newBoolValue);
            Assert.IsTrue(oldEnumValue == TwoBitEnum.D && newEnumValue == TwoBitEnum.B);
            Assert.IsTrue(oldNumberValue == 13);
            Assert.IsTrue(newNumberValue == 10);
        }

        [Test]
        public void ShouldCallGlobalReadHandler()
        {
            flagRTSField.Value = true; // prevent change callback from being fired
            Assert.AreEqual(0, globalCallbacks);
            register.Read();
            Assert.AreEqual(1, globalCallbacks);
            Assert.AreEqual(RegisterResetValue & readMask, oldGlobalValue & readMask);
            Assert.AreEqual(RegisterResetValue & readMask, newGlobalValue & readMask);
        }

        [Test]
        public void ShouldCallGlobalWriteAndChangeHandler()
        {
            register.Reset();
            Assert.AreEqual(0, globalCallbacks);
            register.Write(0, 0x2A80 & writeMask);
            //1 for write, 1 for change
            Assert.AreEqual(2, globalCallbacks);

            Assert.AreEqual(RegisterResetValue, oldGlobalValue);
            Assert.AreEqual(0x2A80 & writeMask, newGlobalValue & writeMask);
        }

        [Test]
        public void ShouldWorkWithUndefinedEnumValue()
        {
            register.Write(0, 2);
            Assert.AreEqual((TwoBitEnum)2, enumRWField.Value);
        }

        [Test]
        public void ShouldToggleField()
        {
            register.Write(0, 1 << 15);
            Assert.AreEqual(true, flagTRField.Value);
            register.Write(0, 1 << 15);
            Assert.AreEqual(false, flagTRField.Value);
            register.Write(0, 1 << 15);
            Assert.AreEqual(true, flagTRField.Value);
        }

        [Test]
        public void ShouldSetField()
        {
            register.Write(0, 1 << 16);
            Assert.AreEqual(true, flagSRField.Value);
            register.Write(0, 0);
            Assert.AreEqual(true, flagSRField.Value);
        }

        [Test]
        public void ShouldHandle64BitWideRegistersProperly()
        {
            ulong test = 0;
            new QuadWordRegister(null, 0).WithValueField(0, 64, writeCallback: (oldValue, newValue) => test = newValue).Write(0x0, 0xDEADBEEF01234567UL);
            Assert.AreEqual(0xDEADBEEF01234567UL, test);
        }

        [Test]
        public void ShouldHandle32BitWideRegistersProperly()
        {
            uint test = 0;
            new DoubleWordRegister(null, 0).WithValueField(0, 32, writeCallback: (oldValue, newValue) => test = (uint)newValue).Write(0x0, 0xDEADBEEF);
            Assert.AreEqual(0xDEADBEEF, test);
        }

        [SetUp]
        public void SetUp()
        {
            register = new QuadWordRegister(null, RegisterResetValue);
            enumRWField = register.DefineEnumField<TwoBitEnum>(0, 2);
            flagRWField = register.DefineFlagField(2);
            valueRWField = register.DefineValueField(3, 4);
            register.DefineEnumField<TwoBitEnum>(7, 2, readCallback: EnumCallback, writeCallback: EnumCallback, changeCallback: EnumCallback);
            register.DefineFlagField(9, readCallback: BoolCallback, writeCallback: BoolCallback, changeCallback: BoolCallback);
            register.DefineValueField(10, 4, readCallback: NumberCallback, writeCallback: NumberCallback, changeCallback: NumberCallback);
            flagTRField = register.DefineFlagField(15, FieldMode.Read | FieldMode.Toggle);
            flagSRField = register.DefineFlagField(16, FieldMode.Read | FieldMode.Set);
            flagW1CField = register.DefineFlagField(17, FieldMode.Read | FieldMode.WriteOneToClear);
            flagW0CField = register.DefineFlagField(18, FieldMode.Read | FieldMode.WriteZeroToClear);
            flagWRTCField = register.DefineFlagField(19, FieldMode.ReadToClear | FieldMode.Write);
            flagWField = register.DefineFlagField(20, FieldMode.Write);
            flagRField = register.DefineFlagField(21, FieldMode.Read);
            register.DefineValueField(22, 3, valueProviderCallback: ModifyingValueCallback);
            register.DefineFlagField(25, valueProviderCallback: ModifyingFlagCallback);
            register.DefineEnumField<TwoBitEnum>(26, 2, valueProviderCallback: ModifyingEnumCallback);
            flagWZTSField = register.DefineFlagField(28, FieldMode.WriteZeroToSet);
            flagWZTTField = register.DefineFlagField(29, FieldMode.WriteZeroToToggle);
            flagRTSField = register.DefineFlagField(30, FieldMode.ReadToSet);
            flagWTCField = register.DefineFlagField(31, FieldMode.Read | FieldMode.WriteToClear);

            register.WithReadCallback(GlobalCallback).WithWriteCallback(GlobalCallback).WithChangeCallback(GlobalCallback);

            enableValueProviders = false;

            enumCallbacks = 0;
            boolCallbacks = 0;
            numberCallbacks = 0;
            globalCallbacks = 0;
            oldBoolValue = false;
            newBoolValue = false;
            oldEnumValue = TwoBitEnum.A;
            newEnumValue = TwoBitEnum.A;
            oldNumberValue = 0;
            newNumberValue = 0;
            oldGlobalValue = 0;
            newGlobalValue = 0;
        }

        private void GlobalCallback(ulong oldValue, ulong newValue)
        {
            globalCallbacks++;
            oldGlobalValue = oldValue;
            newGlobalValue = newValue;
        }

        private void EnumCallback(TwoBitEnum oldValue, TwoBitEnum newValue)
        {
            enumCallbacks++;
            oldEnumValue = oldValue;
            newEnumValue = newValue;
        }

        private void BoolCallback(bool oldValue, bool newValue)
        {
            boolCallbacks++;
            oldBoolValue = oldValue;
            newBoolValue = newValue;
        }

        private void NumberCallback(ulong oldValue, ulong newValue)
        {
            numberCallbacks++;
            oldNumberValue = oldValue;
            newNumberValue = newValue;
        }

        private ulong ModifyingValueCallback(ulong currentValue)
        {
            if(enableValueProviders)
            {
                return currentValue + 1;
            }
            return currentValue;
        }

        private bool ModifyingFlagCallback(bool currentValue)
        {
            if(enableValueProviders)
            {
                return !currentValue;
            }
            return currentValue;
        }

        private TwoBitEnum ModifyingEnumCallback(TwoBitEnum currentValue)
        {
            if(enableValueProviders)
            {
                return currentValue + 1;
            }
            return currentValue;
        }

        /*Offset  |   Field
          --------------------
          0       |   Enum rw
          1       |
          --------------------
          2       |   Bool rw
          --------------------
          3       |   Value rw
          4       |
          5       |
          6       |
          --------------------
          7       |   Enum rw w/callbacks, reset value 3
          8       |
          --------------------
          9       |   Bool rw w/callbacks, reset value 1
          --------------------
          A       |   Value rw w/callbacks, reset value 13
          B       |
          C       |
          D       |
          --------------------
          F       |   Bool tr
          --------------------
          10      |   Bool sr
          --------------------
          11      |   Bool w1c
          --------------------
          12      |   Bool w0c
          --------------------
          13      |   Bool wrtc
          --------------------
          14      |   Bool w
          --------------------
          15      |   Bool r
          --------------------
          16      |   Value rw w/changing r callback
          17      |
          18      |
          --------------------
          19      |   Bool rw w/changing r callback
          --------------------
          1A      |   Enum rw w/changing r callback
          1B      |
          --------------------
          1C      |   Bool wzts
          --------------------
          1D      |   Bool wztt
          --------------------
          1E      |   Bool rts
          --------------------
          1F      |   Bool wtc
        */
        private QuadWordRegister register;

        private int enumCallbacks;
        private int boolCallbacks;
        private int numberCallbacks;

        private int globalCallbacks;

        private TwoBitEnum oldEnumValue;
        private TwoBitEnum newEnumValue;
        private bool oldBoolValue;
        private bool newBoolValue;
        private ulong oldNumberValue;
        private ulong newNumberValue;
        private ulong oldGlobalValue;
        private ulong newGlobalValue;

        private bool enableValueProviders;

        private IEnumRegisterField<TwoBitEnum> enumRWField;
        private IFlagRegisterField flagRWField;
        private IValueRegisterField valueRWField;
        private IFlagRegisterField flagTRField;
        private IFlagRegisterField flagSRField;
        private IFlagRegisterField flagW1CField;
        private IFlagRegisterField flagW0CField;
        private IFlagRegisterField flagWRTCField;
        private IFlagRegisterField flagWField;
        private IFlagRegisterField flagRField;
        private IFlagRegisterField flagWZTSField;
        private IFlagRegisterField flagWZTTField;
        private IFlagRegisterField flagRTSField;
        private IFlagRegisterField flagWTCField;

        // Bits that store written value
        private ulong writeMask = 0b0000_1111_1101_1000__0111_1111_1111_1111;
        // Bits that provide stored value
        private ulong readMask =  0b0000_1111_1110_0111__1111_1111_1111_1111;
        private const ulong RegisterResetValue = 0x3780u;

        private enum TwoBitEnum
        {
            A = 0,
            B = 1,
            D = 3
        }

        private enum SixtyFourBitEnum : ulong
        {
            A = 0x0123456789123456,
            B = 0xFFFFFFFFFFFFFFFF
        }
    }
}
