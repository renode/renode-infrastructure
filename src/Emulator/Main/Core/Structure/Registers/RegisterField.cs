//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.Structure.Registers
{
    public interface IRegisterField<T>
    {
        /// <summary>
        /// Gets or sets the field's value. Access to this property does not invoke verification procedures in terms of FieldMode checking.
        /// Also, it does not invoke callbacks.
        /// </summary>
        T Value { get; set; }

        /// <summary>
        /// Gets the field's width in bits. It should be used to verify if the value assigned to <cref="Value"> is valid, as exceeding
        /// the field's limits causes an ArgumentException.
        /// </summary>
        int Width { get; }

        Action<T, T> ReadCallback { get; set; }
        Action<T, T> WriteCallback { get; set; }
        Action<T, T> ChangeCallback { get; set; }
        Func<T, T> ValueProviderCallback { get; set; }
    }

    public partial class PeripheralRegister
    {
        private sealed class ValueRegisterField : RegisterField<ulong>, IValueRegisterField
        {
            public ValueRegisterField(PeripheralRegister parent, int position, int width, FieldMode fieldMode, Action<ulong, ulong> readCallback,
                Action<ulong, ulong> writeCallback, Action<ulong, ulong> changeCallback, Func<ulong, ulong> valueProviderCallback, string name)
                : base(parent, position, width, fieldMode, readCallback, writeCallback, changeCallback, valueProviderCallback, name)
            {
            }

            protected override ulong FromBinary(ulong value)
            {
                return value;
            }

            protected override ulong ToBinary(ulong value)
            {
                return value;
            }
        }

        private sealed class EnumRegisterField<TEnum> : RegisterField<TEnum>, IEnumRegisterField<TEnum> where TEnum : struct, IConvertible
        {
            public EnumRegisterField(PeripheralRegister parent, int position, int width, FieldMode fieldMode, Action<TEnum, TEnum> readCallback,
                Action<TEnum, TEnum> writeCallback, Action<TEnum, TEnum> changeCallback, Func<TEnum, TEnum> valueProviderCallback, string name)
                : base(parent, position, width, fieldMode, readCallback, writeCallback, changeCallback, valueProviderCallback, name)
            {
            }

            protected override TEnum FromBinary(ulong value)
            {
                return EnumConverter<TEnum>.ToEnum(value);
            }

            protected override ulong ToBinary(TEnum value)
            {
                return EnumConverter<TEnum>.ToUInt64(value);
            }
        }

        private sealed class FlagRegisterField : RegisterField<bool>, IFlagRegisterField
        {
            public FlagRegisterField(PeripheralRegister parent, int position, FieldMode fieldMode, Action<bool, bool> readCallback,
                Action<bool, bool> writeCallback, Action<bool, bool> changeCallback, Func<bool, bool> valueProviderCallback, string name)
                : base(parent, position, 1, fieldMode, readCallback, writeCallback, changeCallback, valueProviderCallback, name)
            {
            }

            protected override bool FromBinary(ulong value)
            {
                return value != 0;
            }

            protected override ulong ToBinary(bool value)
            {
                return value ? 1u : 0;
            }
        }

        private abstract class RegisterField<T> : RegisterField, IRegisterField<T>
        {
            public T Value
            {
                get
                {
                    return FromBinary(FilterValue(parent.UnderlyingValue));
                }
                set
                {
                    ulong binary = ToBinary(value);
                    if((binary >> width) > 0 && width < 64)
                    {
                        throw new ConstructionException("Value exceeds the size of the field.");
                    }
                    WriteFiltered(binary);
                }
            }

            public int Width => width;

            public Action<T, T> ReadCallback { get; set; }
            public Action<T, T> WriteCallback { get; set; }
            public Action<T, T> ChangeCallback { get; set; }
            public Func<T, T> ValueProviderCallback { get; set; }

            public override void CallReadHandler(ulong oldValue, ulong newValue)
            {
                if(ReadCallback != null)
                {
                    var oldValueFiltered = FilterValue(oldValue);
                    var newValueFiltered = FilterValue(newValue);
                    ReadCallback(FromBinary(oldValueFiltered), FromBinary(newValueFiltered));
                }
            }

            public override void CallWriteHandler(ulong oldValue, ulong newValue)
            {
                if(WriteCallback != null)
                {
                    var oldValueFiltered = FilterValue(oldValue);
                    var newValueFiltered = FilterValue(newValue);
                    WriteCallback(FromBinary(oldValueFiltered), FromBinary(newValueFiltered));
                }
            }

            public override void CallChangeHandler(ulong oldValue, ulong newValue)
            {
                if(ChangeCallback != null)
                {
                    var oldValueFiltered = FilterValue(oldValue);
                    var newValueFiltered = FilterValue(newValue);
                    ChangeCallback(FromBinary(oldValueFiltered), FromBinary(newValueFiltered));
                }
            }

            public override ulong CallValueProviderHandler(ulong currentValue)
            {
                if(ValueProviderCallback != null)
                {
                    var currentValueFiltered = FilterValue(currentValue);
                    return UnfilterValue(currentValue, ToBinary(ValueProviderCallback(FromBinary(currentValueFiltered))));
                }
                return currentValue;
            }

            public override string ToString()
            {
                return $"[RegisterType<{typeof(T).Name}> Value={Value} Width={Width}]";
            }

            protected RegisterField(PeripheralRegister parent, int position, int width, FieldMode fieldMode, Action<T, T> readCallback,
                Action<T, T> writeCallback, Action<T, T> changeCallback, Func<T, T> valueProviderCallback, string name) : base(parent, position, width, fieldMode, name)
            {
                if(!fieldMode.IsReadable() && valueProviderCallback != null)
                {
                    throw new ConstructionException($"A write-only field cannot provide a value callback.");
                }

                ReadCallback = readCallback;
                WriteCallback = writeCallback;
                ChangeCallback = changeCallback;
                ValueProviderCallback = valueProviderCallback;
            }

            protected abstract T FromBinary(ulong value);

            protected abstract ulong ToBinary(T value);
        }

        private abstract class RegisterField
        {
            public abstract void CallReadHandler(ulong oldValue, ulong newValue);

            public abstract void CallWriteHandler(ulong oldValue, ulong newValue);

            public abstract void CallChangeHandler(ulong oldValue, ulong newValue);

            public abstract ulong CallValueProviderHandler(ulong currentValue);

            public readonly int position;
            public readonly int width;
            public readonly string name;
            public readonly FieldMode fieldMode;

            protected RegisterField(PeripheralRegister parent, int position, int width, FieldMode fieldMode, string name)
            {
                if(!fieldMode.IsValid())
                {
                    throw new ConstructionException("Invalid {0} flags for register field: {1}.".FormatWith(fieldMode.GetType().Name, fieldMode.ToString()));
                }
                this.parent = parent;
                this.position = position;
                this.fieldMode = fieldMode;
                this.width = width;
                this.name = name;
            }

            protected ulong FilterValue(ulong value)
            {
                return BitHelper.GetValue(value, position, width);
            }

            protected ulong UnfilterValue(ulong baseValue, ulong fieldValue)
            {
                BitHelper.UpdateWithShifted(ref baseValue, fieldValue, position, width);
                return baseValue;
            }

            protected void WriteFiltered(ulong value)
            {
                BitHelper.UpdateWithShifted(ref parent.UnderlyingValue, value, position, width);
            }

            protected readonly PeripheralRegister parent;
        }
    }
}
