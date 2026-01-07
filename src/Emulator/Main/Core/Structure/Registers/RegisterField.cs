//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

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
        /// Gets or sets the field's shadow value.
        /// No callbacks are involved upon acccess.
        /// </summary>
        T ShadowValue { get; set; }

        /// <summary>
        /// Gets the field's width in bits. It should be used to verify if the value assigned to <cref="Value"> is valid, as exceeding
        /// the field's limits causes an ArgumentException.
        /// </summary>
        int Width { get; }

        Action<T, T> ReadCallback { get; set; }

        Action<T, T> WriteCallback { get; set; }

        Action<T, T> ChangeCallback { get; set; }

        Action<T, T> ShadowReloadCallback { get; set; }

        Func<T, T> ValueProviderCallback { get; set; }
    }

    public partial class PeripheralRegister
    {
        private sealed class ValueRegisterField : RegisterField<ulong>, IValueRegisterField
        {
            public ValueRegisterField(PeripheralRegister parent, int position, int width, FieldMode fieldMode, Action<ulong, ulong> readCallback,
                Action<ulong, ulong> writeCallback, Action<ulong, ulong> changeCallback, Func<ulong, ulong> valueProviderCallback, Action<ulong, ulong> shadowReloadCallback, string name)
                : base(parent, position, width, fieldMode, readCallback, writeCallback, changeCallback, valueProviderCallback, shadowReloadCallback, name)
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
                Action<TEnum, TEnum> writeCallback, Action<TEnum, TEnum> changeCallback, Func<TEnum, TEnum> valueProviderCallback, Action<TEnum, TEnum> shadowReloadCallback, string name)
                : base(parent, position, width, fieldMode, readCallback, writeCallback, changeCallback, valueProviderCallback, shadowReloadCallback, name)
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

        private sealed class PacketRegisterField<TPacket> : RegisterField<TPacket>, IPacketRegisterField<TPacket> where TPacket : struct
        {
            public PacketRegisterField(PeripheralRegister parent, int position, int width, FieldMode fieldMode, Action<TPacket, TPacket> readCallback,
                Action<TPacket, TPacket> writeCallback, Action<TPacket, TPacket> changeCallback, Func<TPacket, TPacket> valueProviderCallback, Action<TPacket, TPacket> shadowReloadCallback, string name)
                : base(parent, position, width, fieldMode, readCallback, writeCallback, changeCallback, valueProviderCallback, shadowReloadCallback, name)
            {
            }

            protected override TPacket FromBinary(ulong value)
            {
                return Packet.Decode<TPacket>(BitConverter.GetBytes(value));
            }

            protected override ulong ToBinary(TPacket value)
            {
                var bytes = Packet.Encode<TPacket>(value);
                var padded = new byte[sizeof(ulong)];
                bytes.CopyTo(padded, 0);
                return BitConverter.ToUInt64(padded, 0);
            }
        }

        private sealed class FlagRegisterField : RegisterField<bool>, IFlagRegisterField
        {
            public FlagRegisterField(PeripheralRegister parent, int position, FieldMode fieldMode, Action<bool, bool> readCallback,
                Action<bool, bool> writeCallback, Action<bool, bool> changeCallback, Func<bool, bool> valueProviderCallback, Action<bool, bool> shadowReloadCallback, string name)
                : base(parent, position, 1, fieldMode, readCallback, writeCallback, changeCallback, valueProviderCallback, shadowReloadCallback, name)
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

            public override void CallShadowReloadHandler(ulong oldValue, ulong newValue)
            {
                if(ShadowReloadCallback != null)
                {
                    var oldValueFiltered = FilterValue(oldValue);
                    var newValueFiltered = FilterValue(newValue);
                    ShadowReloadCallback(FromBinary(oldValueFiltered), FromBinary(newValueFiltered));
                }
            }

            public override string ToString()
            {
                return $"[RegisterType<{typeof(T).Name}> Value={Value} Width={Width}]";
            }

            public T Value
            {
                get => GetValueFrom(parent.UnderlyingValue);
                set => SetValueFrom(ref parent.UnderlyingValue, value);
            }

            public T ShadowValue
            {
                get => GetValueFrom(parent.UnderlyingShadowValue);
                set => SetValueFrom(ref parent.UnderlyingShadowValue, value);
            }

            public new int Width => base.Width;

            public Action<T, T> ReadCallback { get; set; }

            public Action<T, T> WriteCallback { get; set; }

            public Action<T, T> ChangeCallback { get; set; }

            public Func<T, T> ValueProviderCallback { get; set; }

            public Action<T, T> ShadowReloadCallback { get; set; }

            protected RegisterField(PeripheralRegister parent, int position, int width, FieldMode fieldMode, Action<T, T> readCallback,
                Action<T, T> writeCallback, Action<T, T> changeCallback, Func<T, T> valueProviderCallback, Action<T, T> shadowReloadCallback, string name) : base(parent, position, width, fieldMode, name)
            {
                if(!fieldMode.IsReadable() && valueProviderCallback != null)
                {
                    throw new ConstructionException($"A write-only field cannot provide a value callback.");
                }

                ReadCallback = readCallback;
                WriteCallback = writeCallback;
                ChangeCallback = changeCallback;
                ValueProviderCallback = valueProviderCallback;
                ShadowReloadCallback = shadowReloadCallback;
            }

            protected abstract T FromBinary(ulong value);

            protected abstract ulong ToBinary(T value);

            private T GetValueFrom(ulong parentValue) => FromBinary(FilterValue(parentValue));

            private void SetValueFrom(ref ulong parentValue, T value)
            {
                ulong binary = ToBinary(value);
                if((binary >> base.Width) > 0 && base.Width < 64)
                {
                    throw new ConstructionException("Value exceeds the size of the field.");
                }
                parentValue = UnfilterValue(parentValue, binary);
            }
        }

        private abstract class RegisterField
        {
            public abstract void CallReadHandler(ulong oldValue, ulong newValue);

            public abstract void CallWriteHandler(ulong oldValue, ulong newValue);

            public abstract void CallChangeHandler(ulong oldValue, ulong newValue);

            public abstract ulong CallValueProviderHandler(ulong currentValue);

            public abstract void CallShadowReloadHandler(ulong oldValue, ulong newValue);

            public readonly int Position;
            public readonly int Width;
            public readonly string Name;
            public readonly FieldMode FieldMode;

            protected RegisterField(PeripheralRegister parent, int position, int width, FieldMode fieldMode, string name)
            {
                if(!fieldMode.IsValid())
                {
                    throw new ConstructionException("Invalid {0} flags for register field: {1}.".FormatWith(fieldMode.GetType().Name, fieldMode.ToString()));
                }
                this.parent = parent;
                this.Position = position;
                this.FieldMode = fieldMode;
                this.Width = width;
                this.Name = name;
            }

            protected ulong FilterValue(ulong value)
            {
                return BitHelper.GetValue(value, Position, Width);
            }

            protected ulong UnfilterValue(ulong baseValue, ulong fieldValue)
            {
                BitHelper.UpdateWithShifted(ref baseValue, fieldValue, Position, Width);
                return baseValue;
            }

            protected readonly PeripheralRegister parent;
        }
    }
}
