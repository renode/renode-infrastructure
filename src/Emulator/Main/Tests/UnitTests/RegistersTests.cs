//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class RegistersTests
    {
        [Test]
        public void ShouldNotAcceptOutOfBoundsValues()
        {
            Assert.Catch<ArgumentException>(() => enumRWField.Value = (TwoBitEnum)(1 << 2));
            Assert.Catch<ArgumentException>(() => valueRWField.Value = (1 << 4));
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
            var localRegister = new DoubleWordRegister(null);
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
            Assert.AreEqual(0, register.Read());
        }

        [Test]
        public void ShouldWriteOneToClear()
        {
            flagW1CField.Value = true;
            Assert.AreEqual(1 << 17 | RegisterResetValue, register.Read());
            register.Write(0, 1 << 17);
            Assert.AreEqual(false, flagW0CField.Value);
            Assert.AreEqual(0, register.Read());
        }

        [Test]
        public void ShouldReadToClear()
        {
            flagWRTCField.Value = true;
            Assert.AreEqual(1 << 19 | RegisterResetValue, register.Read());
            Assert.AreEqual(false, flagWRTCField.Value);
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
            Assert.IsTrue(oldUintValue == newUintValue);
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
            Assert.IsTrue(oldUintValue == 13);
            Assert.IsTrue(newUintValue == 10);
        }

        [Test]
        public void ShouldCallGlobalReadHandler()
        {
            Assert.AreEqual(0, globalCallbacks);
            register.Read();
            Assert.AreEqual(1, globalCallbacks);
            Assert.AreEqual(RegisterResetValue, oldGlobalValue);
            Assert.AreEqual(RegisterResetValue, newGlobalValue);
        }

        [Test]
        public void ShouldCallGlobalWriteAndChangeHandler()
        {
            Assert.AreEqual(0, globalCallbacks);
            register.Write(0, 0x2A80);
            //1 for write, 1 for change
            Assert.AreEqual(2, globalCallbacks);

            Assert.AreEqual(RegisterResetValue, oldGlobalValue);
            Assert.AreEqual(0x2A80, newGlobalValue);
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
        public void ShouldHandle32BitWideRegistersProperly()
        {
            uint test = 0;
            new DoubleWordRegister(null, 0).WithValueField(0, 32, writeCallback: (oldValue, newValue) => test = newValue).Write(0x0, 0xDEADBEEF);
            Assert.AreEqual(0xDEADBEEF, test);
        }

        [SetUp]
        public void SetUp()
        {
            register = new DoubleWordRegister(null, RegisterResetValue);
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
            oldUintValue = 0;
            newUintValue = 0;
            oldGlobalValue = 0;
            newGlobalValue = 0;
        }

        private void GlobalCallback(uint oldValue, uint newValue)
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

        private void NumberCallback(uint oldValue, uint newValue)
        {
            numberCallbacks++;
            oldUintValue = oldValue;
            newUintValue = newValue;
        }

        private uint ModifyingValueCallback(uint currentValue)
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
        */
        private DoubleWordRegister register;

        private int enumCallbacks;
        private int boolCallbacks;
        private int numberCallbacks;

        private int globalCallbacks;

        private TwoBitEnum oldEnumValue;
        private TwoBitEnum newEnumValue;
        private bool oldBoolValue;
        private bool newBoolValue;
        private uint oldUintValue;
        private uint newUintValue;
        private uint oldGlobalValue;
        private uint newGlobalValue;

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

        private const uint RegisterResetValue = 0x3780u;

        private enum TwoBitEnum
        {
            A = 0,
            B = 1,
            D = 3
        }
    }
}

