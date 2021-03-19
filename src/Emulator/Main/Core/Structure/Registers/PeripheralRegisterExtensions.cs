//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;

namespace Antmicro.Renode.Core.Structure.Registers
{
    public static class PeripheralRegisterExtensions
    {
        /// <summary>
        /// Fluent API for flag field creation. For parameters see <see cref="PeripheralRegister.DefineFlagField"/>.
        /// </summary>
        /// <returns>This register with a defined flag.</returns>
        public static T WithFlag<T>(this T register, int position, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<bool, bool> readCallback = null,
            Action<bool, bool> writeCallback = null, Action<bool, bool> changeCallback = null, Func<bool, bool> valueProviderCallback = null, string name = null) where T : PeripheralRegister
        {
            register.DefineFlagField(position, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, name);
            return register;
        }

        /// <summary>
        /// Fluent API for creation of a set of consecutive flag fields.
        /// </summary>
        /// <param name="position">Offset in the register of the first field.</param>
        /// <param name="count">Number of flag fields to create.</param>
        /// <param name="mode">Access modifiers of each field.</param>
        /// <param name="readCallback">Method to be called whenever the containing register is read. The first parameter is the index of the flag field, the second is the value of this field before read,
        /// the third parameter is the value after read. Note that it will also be called for unreadable fields.</param>
        /// <param name="writeCallback">Method to be called whenever the containing register is written to. The first parameter is the index of the flag, the second is the value of this field before write,
        /// the third parameter is the value written (without any modification). Note that it will also be called for unwrittable fields.</param>
        /// <param name="changeCallback">Method to be called whenever this field's value is changed, either due to read or write. The first parameter is the index of the flag, the second is the value of this field before change,
        /// the third parameter is the value after change. Note that it will also be called for unwrittable fields.</param>
        /// <param name="valueProviderCallback">Method to be called whenever this field is read. The value passed is the current field's value, that will be overwritten by
        /// the value returned from it. This returned value is eventually passed as the second parameter of <paramref name="readCallback"/>.</param>
        /// <param name="name">Ignored parameter, for convenience. Treat it as a comment.</param>
        /// <returns>This register with defined flags.</returns>
        public static T WithFlags<T>(this T register, int position, int count, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<int, bool, bool> readCallback = null,
            Action<int, bool, bool> writeCallback = null, Action<int, bool, bool> changeCallback = null, Func<int, bool, bool> valueProviderCallback = null, string name = null) where T : PeripheralRegister
        {
            return WithFlags(register, position, count, out var _, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, name);
        }

        /// <summary>
        /// Fluent API for value field creation. For parameters see <see cref="PeripheralRegister.DefineValueField"/>.
        /// </summary>
        /// <returns>This register with a defined value field.</returns>
        public static T WithValueField<T>(this T register, int position, int width, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<uint, uint> readCallback = null,
            Action<uint, uint> writeCallback = null, Action<uint, uint> changeCallback = null, Func<uint, uint> valueProviderCallback = null, string name = null) where T : PeripheralRegister
        {
            register.DefineValueField(position, width, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, name);
            return register;
        }

        /// <summary>
        /// Fluent API for enum field creation. For parameters see <see cref="PeripheralRegister.DefineEnumField"/>.
        /// </summary>
        /// <returns>This register with a defined enum field.</returns>
        public static R WithEnumField<R, T>(this R register, int position, int width, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<T, T> readCallback = null,
            Action<T, T> writeCallback = null, Action<T, T> changeCallback = null, Func<T, T> valueProviderCallback = null, string name = null) where R : PeripheralRegister
            where T : struct, IConvertible
        {
            register.DefineEnumField<T>(position, width, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, name);
            return register;
        }

        /// <summary>
        /// Fluent API for tagged field creation. For parameters see <see cref="PeripheralRegister.Tag"/>.
        /// </summary>
        /// <returns>This register with a defined tag field.</returns>
        public static T WithTag<T>(this T register, string name, int position, int width) where T : PeripheralRegister
        {
            register.Tag(name, position, width);
            return register;
        }

        /// <summary>
        /// Fluent API for tagged flag creation - a tag of width equal to 1. For parameters see <see cref="PeripheralRegister.DefineValueField"/>.
        /// </summary>
        /// <returns>This register with a defined tag field.</returns>
        public static T WithTaggedFlag<T>(this T register, string name, int position) where T : PeripheralRegister
        {
            register.Tag(name, position, 1);
            return register;
        }

        /// <summary>
        /// Fluent API for value field creation. For parameters see <see cref="PeripheralRegister.DefineValueField"/>.
        /// This overload allows you to retrieve the created field via <c>valueField</c> parameter.
        /// </summary>
        /// <returns>This register with a defined value field.</returns>
        public static T WithValueField<T>(this T register, int position, int width, out IValueRegisterField valueField, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<uint, uint> readCallback = null,
            Action<uint, uint> writeCallback = null, Action<uint, uint> changeCallback = null, Func<uint, uint> valueProviderCallback = null, string name = null) where T : PeripheralRegister
        {
            valueField = register.DefineValueField(position, width, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, name);
            return register;
        }

        /// <summary>
        /// Fluent API for enum field creation. For parameters see <see cref="PeripheralRegister.DefineEnumField"/>.
        /// This overload allows you to retrieve the created field via <c>enumField</c> parameter.
        /// </summary>
        /// <returns>This register with a defined enum field.</returns>
        public static R WithEnumField<R, T>(this R register, int position, int width, out IEnumRegisterField<T> enumField, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<T, T> readCallback = null,
            Action<T, T> writeCallback = null, Action<T, T> changeCallback = null, Func<T, T> valueProviderCallback = null, string name = null) where R : PeripheralRegister
            where T : struct, IConvertible
        {
            enumField = register.DefineEnumField<T>(position, width, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, name);
            return register;
        }

        /// <summary>
        /// Fluent API for creation of set of enum fields. For parameters see the other overload of <see cref="PeripheralRegisterExtensions.WithEnumFields"/>.
        /// This overload allows you to retrieve the created array of fields via <c>enumFields</c> parameter.
        /// </summary>
        /// <returns>This register with defined enum fields.</returns>
        public static R WithEnumFields<R, T>(this R register, int position, int width, int count, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<int, T, T> readCallback = null,
            Action<int, T, T> writeCallback = null, Action<int, T, T> changeCallback = null, Func<int, T, T> valueProviderCallback = null, string name = null) where R : PeripheralRegister where T : struct, IConvertible
        {
            return WithEnumFields<R, T>(register, position, width, count, out var _, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, name);
        }

        /// <summary>
        /// Fluent API for creation of set of enum fields. For parameters see the other overload of <see cref="PeripheralRegisterExtensions.WithEnumFields"/>.
        /// This overload allows you to retrieve the created array of fields via <c>enumFields</c> parameter.
        /// </summary>
        /// <returns>This register with defined enum fields.</returns>
        public static R WithEnumFields<R, T>(this R register, int position, int width, int count, out IEnumRegisterField<T>[] enumFields, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<int, T, T> readCallback = null,
            Action<int, T, T> writeCallback = null, Action<int, T, T> changeCallback = null, Func<int, T, T> valueProviderCallback = null, string name = null) where R : PeripheralRegister where T : struct, IConvertible
        {
            enumFields = new IEnumRegisterField<T>[count];
            for(var i = 0; i < count; i++)
            {
                var j = i;

                enumFields[j] = register.DefineEnumField<T>(position + (j * width), width, mode,
                    readCallback == null ? null : (Action<T, T>)((x, y) => readCallback(j, x, y)),
                    writeCallback == null ? null : (Action<T, T>)((x, y) => writeCallback(j, x, y)),
                    changeCallback == null ? null : (Action<T, T>)((x, y) => changeCallback(j, x, y)),
                    valueProviderCallback == null ? null : (Func<T, T>)((x) => valueProviderCallback(j, x)),
                    name == null ? null : $"{name}_{j}");
            }
            return register;
        }

        /// <summary>
        /// Fluent API for flag field creation. For parameters see <see cref="PeripheralRegister.DefineFlagField"/>.
        /// This overload allows you to retrieve the created field via <c>flagField</c> parameter.
        /// </summary>
        /// <returns>This register with a defined flag.</returns>
        public static T WithFlag<T>(this T register, int position, out IFlagRegisterField flagField, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<bool, bool> readCallback = null,
            Action<bool, bool> writeCallback = null, Action<bool, bool> changeCallback = null, Func<bool, bool> valueProviderCallback = null, string name = null) where T : PeripheralRegister
        {
            flagField = register.DefineFlagField(position, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, name);
            return register;
        }

        /// <summary>
        /// Fluent API for creation of sets of flag fields. For parameters see the other overload of <see cref="PeripheralRegisterExtensions.WithFlags"/>.
        /// This overload allows you to retrieve the created array of fields via <c>flagFields</c> parameter.
        /// </summary>
        /// <returns>This register with defined flags.</returns>
        public static T WithFlags<T>(this T register, int position, int count, out IFlagRegisterField[] flagFields, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<int, bool, bool> readCallback = null,
            Action<int, bool, bool> writeCallback = null, Action<int, bool, bool> changeCallback = null, Func<int, bool, bool> valueProviderCallback = null, string name = null) where T : PeripheralRegister
        {
            flagFields = new IFlagRegisterField[count];
            for(var i = 0; i < count; i++)
            {
                var j = i;

                flagFields[j] = register.DefineFlagField(position + j, mode,
                    readCallback == null ? null : (Action<bool, bool>)((x, y) => readCallback(j, x, y)),
                    writeCallback == null ? null : (Action<bool, bool>)((x, y) => writeCallback(j, x, y)),
                    changeCallback == null ? null : (Action<bool, bool>)((x, y) => changeCallback(j, x, y)),
                    valueProviderCallback == null ? null : (Func<bool, bool>)((x) => valueProviderCallback(j, x)),
                    name == null ? null : $"{name}_{j}");
            }
            return register;
        }

        /// <summary>
        /// Fluent API for read callback registration. For description see <see cref="DoubleWordRegister.DefineReadCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static DoubleWordRegister WithReadCallback(this DoubleWordRegister register, Action<uint, uint> readCallback)
        {
            register.DefineReadCallback (readCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for write callback registration. For description see <see cref="DoubleWordRegister.DefineWriteCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static DoubleWordRegister WithWriteCallback (this DoubleWordRegister register, Action<uint, uint> writeCallback)
        {
            register.DefineWriteCallback (writeCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for change callback registration. For description see <see cref="DoubleWordRegister.DefineChangeCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static DoubleWordRegister WithChangeCallback (this DoubleWordRegister register, Action<uint, uint> changeCallback)
        {
            register.DefineChangeCallback (changeCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for read callback registration. For description see <see cref="WordRegister.DefineReadCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static WordRegister WithReadCallback (this WordRegister register, Action<ushort, ushort> readCallback)
        {
            register.DefineReadCallback (readCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for write callback registration. For description see <see cref="WordRegister.DefineWriteCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static WordRegister WithWriteCallback (this WordRegister register, Action<ushort, ushort> writeCallback)
        {
            register.DefineWriteCallback (writeCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for change callback registration. For description see <see cref="WordRegister.DefineChangeCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static WordRegister WithChangeCallback (this WordRegister register, Action<ushort, ushort> changeCallback)
        {
            register.DefineChangeCallback (changeCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for read callback registration. For description see <see cref="ByteRegister.DefineReadCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static ByteRegister WithReadCallback (this ByteRegister register, Action<byte, byte> readCallback)
        {
            register.DefineReadCallback (readCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for write callback registration. For description see <see cref="ByteRegister.DefineWriteCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static ByteRegister WithWriteCallback (this ByteRegister register, Action<byte, byte> writeCallback)
        {
            register.DefineWriteCallback (writeCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for change callback registration. For description see <see cref="ByteRegister.DefineChangeCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static ByteRegister WithChangeCallback (this ByteRegister register, Action<byte, byte> changeCallback)
        {
            register.DefineChangeCallback (changeCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for tagging bits as "RESERVED". For description see <see cref="PeripheralRegister.Reserved"/>.
        /// </summary>
        /// <returns>This register with a new "RESERVED" tag.</returns>
        public static DoubleWordRegister WithReservedBits(this DoubleWordRegister register, int position, int width, uint? allowedValue = null)
        {
            register.Reserved(position, width, allowedValue);
            return register;
        }

        /// <summary>
        /// Fluent API for tagging bits as "RESERVED". For description see <see cref="PeripheralRegister.Reserved"/>.
        /// </summary>
        /// <returns>This register with a new "RESERVED" tag.</returns>
        public static WordRegister WithReservedBits(this WordRegister register, int position, int width, uint? allowedValue = null)
        {
            register.Reserved(position, width, allowedValue);
            return register;
        }

        /// <summary>
        /// Fluent API for tagging bits as "RESERVED". For description see <see cref="PeripheralRegister.Reserved"/>.
        /// </summary>
        /// <returns>This register with a new "RESERVED" tag.</returns>
        public static ByteRegister WithReservedBits(this ByteRegister register, int position, int width, uint? allowedValue = null)
        {
            register.Reserved(position, width, allowedValue);
            return register;
        }

        /// <summary>
        /// Fluent API for tagging bits as ignored.
        /// </summary>
        /// <returns>This defines a value field to avoid warnings about unhandled bits.</returns>
        public static R WithIgnoredBits<R>(this R register, int position, int width) where R : PeripheralRegister
        {
            return register.WithValueField(position, width, name: "ignored");
        }
    }
}
