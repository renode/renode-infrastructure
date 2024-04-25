//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.Structure.Registers
{
    public static class PeripheralRegisterExtensions
    {
        /// <summary>
        /// Creates a fluent conditional wrapper for conditionally executing actions on a register.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <returns>An <see cref="IfWrapper{T}"/> object wrapping the register.</returns>
        /// <example>
        /// This example demonstrates how to use the <c>If</c> and <c>Else</c> methods
        /// of <see cref="IfWrapper{T}"/> to conditionally define a field on a register:
        /// <code>
        /// Registers.Control.Define(this)
        ///     .WithValueField(0, 4, out transmissionSize, name: "TXSIZE")
        ///     .If(SupportsEncryption)
        ///         .Then(r => r.WithValueField(4, 12, out transmissionKey, name: "TXKEY"))
        ///         .Else(r => r.WithReservedBits(4, 12));
        /// </code>
        /// </example>
        public static IfWrapper<T> If<T>(this T register, bool condition) where T : PeripheralRegister
        {
            return new IfWrapper<T>(register, condition);
        }

        /// <summary>
        /// Fluent API for handling for loops. This helper is equivalent to calling the callback in a for loop
        /// going from "start" to "end" being incremented by "step"
        /// </summary>
        /// <param name="callback">Callback to execute on each loop iteration</param>
        /// <param name="start">Starting value of the loop variable</param>
        /// <param name="end">Exclusive ending value of the loop variable</param>
        /// <param name="step">Amount that the loop variable will be incremented by after each iteration</param>
        /// <returns>This register after all loop iterations</returns>
        public static T For<T>(this T register, Action<T, int> callback, int start, int end, int step = 1) where T : PeripheralRegister
        {
            for(var i = start; i < end; i += step)
            {
                callback(register, i);
            }
            return register;
        }

        /// <summary>
        /// Fluent API for flag field creation. For parameters see <see cref="PeripheralRegister.DefineFlagField"/>.
        /// </summary>
        /// <returns>This register with a defined flag.</returns>
        public static T WithFlag<T>(this T register, int position, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<bool, bool> readCallback = null,
            Action<bool, bool> writeCallback = null, Action<bool, bool> changeCallback = null, Func<bool, bool> valueProviderCallback = null, bool softResettable = true,
            string name = null) where T : PeripheralRegister
        {
            register.DefineFlagField(position, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, softResettable, name);
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
        /// <param name="softResettable">Indicates whether the field should be cleared by soft reset.</param>
        /// <param name="name">Ignored parameter, for convenience. Treat it as a comment.</param>
        /// <returns>This register with defined flags.</returns>
        public static T WithFlags<T>(this T register, int position, int count, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<int, bool, bool> readCallback = null,
            Action<int, bool, bool> writeCallback = null, Action<int, bool, bool> changeCallback = null, Func<int, bool, bool> valueProviderCallback = null, bool softResettable = true,
            string name = null) where T : PeripheralRegister
        {
            return WithFlags(register, position, count, out var _, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, softResettable, name);
        }

        /// <summary>
        /// Fluent API for value field creation. For parameters see <see cref="PeripheralRegister.DefineValueField"/>.
        /// </summary>
        /// <returns>This register with a defined value field.</returns>
        public static T WithValueField<T>(this T register, int position, int width, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<ulong, ulong> readCallback = null,
            Action<ulong, ulong> writeCallback = null, Action<ulong, ulong> changeCallback = null, Func<ulong, ulong> valueProviderCallback = null, bool softResettable = true,
            string name = null) where T : PeripheralRegister
        {
            register.DefineValueField(position, width, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, softResettable, name);
            return register;
        }

        /// <summary>
        /// Fluent API for creation of a set of consecutive value fields
        /// </summary>
        /// <param name="position">Offset in the register of the first field.</param>
        /// <param name="width">Maximum width of the value, in terms of binary representation.</param>
        /// <param name="count">Number of flag fields to create.</param>
        /// <param name="mode">Access modifiers of each field.</param>
        /// <param name="readCallback">Method to be called whenever the containing register is read. The first parameter is the index of the value field, the second is the value of this field before read,
        /// the third parameter is the value after read. Note that it will also be called for unreadable fields.</param>
        /// <param name="writeCallback">Method to be called whenever the containing register is written to. The first parameter is the index of the field, the second is the value of this field before write,
        /// the third parameter is the value written (without any modification). Note that it will also be called for unwrittable fields.</param>
        /// <param name="changeCallback">Method to be called whenever this field's value is changed, either due to read or write. The first parameter is the index of the field, the second is the value of this field before change,
        /// the third parameter is the value after change. Note that it will also be called for unwrittable fields.</param>
        /// <param name="valueProviderCallback">Method to be called whenever this field is read. The value passed is the current field's value, that will be overwritten by
        /// the value returned from it. This returned value is eventually passed as the second parameter of <paramref name="readCallback"/>.</param>
        /// <param name="softResettable">Indicates whether the field should be cleared by soft reset.</param>
        /// <param name="name">Ignored parameter, for convenience. Treat it as a comment.</param>
        /// <returns>This register with defined value fields.</returns>
        public static T WithValueFields<T>(this T register, int position, int width, int count, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<int, ulong, ulong> readCallback = null,
            Action<int, ulong, ulong> writeCallback = null, Action<int, ulong, ulong> changeCallback = null, Func<int, ulong, ulong> valueProviderCallback = null, bool softResettable = true,
            string name = null) where T : PeripheralRegister
        {
            return WithValueFields(register, position, width, count, out var _, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, softResettable, name);
        }

        /// <summary>
        /// Fluent API for enum field creation. For parameters see <see cref="PeripheralRegister.DefineEnumField"/>.
        /// </summary>
        /// <returns>This register with a defined enum field.</returns>
        public static R WithEnumField<R, T>(this R register, int position, int width, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<T, T> readCallback = null,
            Action<T, T> writeCallback = null, Action<T, T> changeCallback = null, Func<T, T> valueProviderCallback = null, bool softResettable = true, string name = null)
            where R : PeripheralRegister
            where T : struct, IConvertible
        {
            register.DefineEnumField<T>(position, width, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, softResettable, name);
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
        /// Fluent API for creating a set of tagged fields. For parameters see <see cref="PeripheralRegister.Tag"/>.
        /// </summary>
        /// <returns>This register with a defined tag field set.</returns>
        public static T WithTags<T>(this T register, string name, int position, int width, int count) where T : PeripheralRegister
        {
            for(var i = 0; i < count; i++)
            {
                register.Tag(name == null ? null : $"{name}_{i}", position + (i * width), width);
            }
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
        /// Fluent API for creating a set of tagged flags - a tag of width equal to 1. For parameters see <see cref="PeripheralRegister.DefineValueField"/>.
        /// </summary>
        /// <returns>This register with a defined tag field set.</returns>
        public static T WithTaggedFlags<T>(this T register, string name, int position, int count) where T : PeripheralRegister
        {
            for(var i = 0; i < count; i++)
            {
                register.WithTaggedFlag(name == null ? null : $"{name}_{i}", position + i);
            }
            return register;
        }

        /// <summary>
        /// Fluent API for value field creation. For parameters see <see cref="PeripheralRegister.DefineValueField"/>.
        /// This overload allows you to retrieve the created field via <c>valueField</c> parameter.
        /// </summary>
        /// <returns>This register with a defined value field.</returns>
        public static T WithValueField<T>(this T register, int position, int width, out IValueRegisterField valueField, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<ulong, ulong> readCallback = null,
            Action<ulong, ulong> writeCallback = null, Action<ulong, ulong> changeCallback = null, Func<ulong, ulong> valueProviderCallback = null, bool softResettable = true, string name = null) where T : PeripheralRegister
        {
            valueField = register.DefineValueField(position, width, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, softResettable, name);
            return register;
        }

        /// <summary>
        /// Fluent API for creation of set of value fields. For parameters see the other overload of <see cref="PeripheralRegisterExtensions.WithValueFields"/>.
        /// This overload allows you to retrieve the created array of fields via <c>valueFields</c> parameter.
        /// </summary>
        /// <returns>This register with defined value fields.</returns>
        public static T WithValueFields<T>(this T register, int position, int width, int count, out IValueRegisterField[] valueFields, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<int, ulong, ulong> readCallback = null,
            Action<int, ulong, ulong> writeCallback = null, Action<int, ulong, ulong> changeCallback = null, Func<int, ulong, ulong> valueProviderCallback = null, bool softResettable = true,
            string name = null) where T : PeripheralRegister
        {
            valueFields = new IValueRegisterField[count];
            for(var i = 0; i < count; i++)
            {
                var j = i;

                valueFields[j] = register.DefineValueField(position + (j * width), width, mode,
                    readCallback == null ? null : (Action<ulong, ulong>)((x, y) => readCallback(j, x, y)),
                    writeCallback == null ? null : (Action<ulong, ulong>)((x, y) => writeCallback(j, x, y)),
                    changeCallback == null ? null : (Action<ulong, ulong>)((x, y) => changeCallback(j, x, y)),
                    valueProviderCallback == null ? null : (Func<ulong, ulong>)((x) => valueProviderCallback(j, x)),
                    softResettable,
                    name == null ? null : $"{name}_{j}");
            }
            return register;
        }

        /// <summary>
        /// Fluent API for enum field creation. For parameters see <see cref="PeripheralRegister.DefineEnumField"/>.
        /// This overload allows you to retrieve the created field via <c>enumField</c> parameter.
        /// </summary>
        /// <returns>This register with a defined enum field.</returns>
        public static R WithEnumField<R, T>(this R register, int position, int width, out IEnumRegisterField<T> enumField, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<T, T> readCallback = null,
            Action<T, T> writeCallback = null, Action<T, T> changeCallback = null, Func<T, T> valueProviderCallback = null, bool softResettable = true, string name = null) where R : PeripheralRegister
            where T : struct, IConvertible
        {
            enumField = register.DefineEnumField<T>(position, width, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, softResettable, name);
            return register;
        }

        /// <summary>
        /// Fluent API for creation of set of enum fields. For parameters see the other overload of <see cref="PeripheralRegisterExtensions.WithEnumFields"/>.
        /// This overload allows you to retrieve the created array of fields via <c>enumFields</c> parameter.
        /// </summary>
        /// <returns>This register with defined enum fields.</returns>
        public static R WithEnumFields<R, T>(this R register, int position, int width, int count, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<int, T, T> readCallback = null,
            Action<int, T, T> writeCallback = null, Action<int, T, T> changeCallback = null, Func<int, T, T> valueProviderCallback = null, bool softResettable = true, string name = null)
            where R : PeripheralRegister
            where T : struct, IConvertible
        {
            return WithEnumFields<R, T>(register, position, width, count, out var _, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, softResettable, name);
        }

        /// <summary>
        /// Fluent API for creation of set of enum fields. For parameters see the other overload of <see cref="PeripheralRegisterExtensions.WithEnumFields"/>.
        /// This overload allows you to retrieve the created array of fields via <c>enumFields</c> parameter.
        /// </summary>
        /// <returns>This register with defined enum fields.</returns>
        public static R WithEnumFields<R, T>(this R register, int position, int width, int count, out IEnumRegisterField<T>[] enumFields, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<int, T, T> readCallback = null,
            Action<int, T, T> writeCallback = null, Action<int, T, T> changeCallback = null, Func<int, T, T> valueProviderCallback = null, bool softResettable = true, string name = null)
            where R : PeripheralRegister
            where T : struct, IConvertible
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
                    softResettable,
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
            Action<bool, bool> writeCallback = null, Action<bool, bool> changeCallback = null, Func<bool, bool> valueProviderCallback = null, bool softResettable = true, string name = null)
            where T : PeripheralRegister
        {
            flagField = register.DefineFlagField(position, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, softResettable, name);
            return register;
        }

        /// <summary>
        /// Fluent API for creation of sets of flag fields. For parameters see the other overload of <see cref="PeripheralRegisterExtensions.WithFlags"/>.
        /// This overload allows you to retrieve the created array of fields via <c>flagFields</c> parameter.
        /// </summary>
        /// <returns>This register with defined flags.</returns>
        public static T WithFlags<T>(this T register, int position, int count, out IFlagRegisterField[] flagFields, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<int, bool, bool> readCallback = null,
            Action<int, bool, bool> writeCallback = null, Action<int, bool, bool> changeCallback = null, Func<int, bool, bool> valueProviderCallback = null, bool softResettable = true,
            string name = null) where T : PeripheralRegister
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
                    softResettable,
                    name == null ? null : $"{name}_{j}");
            }
            return register;
        }

        /// <summary>
        /// Fluent API for tagging bits as "RESERVED". For description see <see cref="PeripheralRegister.Reserved"/>.
        /// </summary>
        /// <returns>This register with a new "RESERVED" tag.</returns>
        public static T WithReservedBits<T>(this T register, int position, int width, uint? allowedValue = null) where T : PeripheralRegister
        {
            register.Reserved(position, width, allowedValue);
            return register;
        }

        /// <summary>
        /// Fluent API for tagging bits as ignored.
        /// </summary>
        /// <returns>This defines a value field to avoid warnings about unhandled bits.</returns>
        public static T WithIgnoredBits<T>(this T register, int position, int width) where T : PeripheralRegister
        {
            return register.WithValueField(position, width, name: "ignored");
        }
    }

    public static class QuadWordRegisterExtensions
    {
        /// <summary>
        /// Fluent API for read callback registration. For description see <see cref="QuadWordRegister.DefineReadCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static QuadWordRegister WithReadCallback(this QuadWordRegister register, Action<ulong, ulong> readCallback)
        {
            register.DefineReadCallback(readCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for write callback registration. For description see <see cref="QuadWordRegister.DefineWriteCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static QuadWordRegister WithWriteCallback(this QuadWordRegister register, Action<ulong, ulong> writeCallback)
        {
            register.DefineWriteCallback(writeCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for change callback registration. For description see <see cref="QuadWordRegister.DefineChangeCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static QuadWordRegister WithChangeCallback(this QuadWordRegister register, Action<ulong, ulong> changeCallback)
        {
            register.DefineChangeCallback(changeCallback);
            return register;
        }
    }

    public static class DoubleWordRegisterExtensions
    {
        /// <summary>
        /// Fluent API for read callback registration. For description see <see cref="DoubleWordRegister.DefineReadCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static DoubleWordRegister WithReadCallback(this DoubleWordRegister register, Action<uint, uint> readCallback)
        {
            register.DefineReadCallback(readCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for write callback registration. For description see <see cref="DoubleWordRegister.DefineWriteCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static DoubleWordRegister WithWriteCallback(this DoubleWordRegister register, Action<uint, uint> writeCallback)
        {
            register.DefineWriteCallback(writeCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for change callback registration. For description see <see cref="DoubleWordRegister.DefineChangeCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static DoubleWordRegister WithChangeCallback(this DoubleWordRegister register, Action<uint, uint> changeCallback)
        {
            register.DefineChangeCallback(changeCallback);
            return register;
        }
    }

    public static class WordRegisterExtensions
    {
        /// <summary>
        /// Fluent API for read callback registration. For description see <see cref="WordRegister.DefineReadCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static WordRegister WithReadCallback(this WordRegister register, Action<ushort, ushort> readCallback)
        {
            register.DefineReadCallback(readCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for write callback registration. For description see <see cref="WordRegister.DefineWriteCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static WordRegister WithWriteCallback(this WordRegister register, Action<ushort, ushort> writeCallback)
        {
            register.DefineWriteCallback(writeCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for change callback registration. For description see <see cref="WordRegister.DefineChangeCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static WordRegister WithChangeCallback(this WordRegister register, Action<ushort, ushort> changeCallback)
        {
            register.DefineChangeCallback(changeCallback);
            return register;
        }
    }

    public static class ByteRegisterExtensions
    {
        /// <summary>
        /// Fluent API for read callback registration. For description see <see cref="ByteRegister.DefineReadCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static ByteRegister WithReadCallback(this ByteRegister register, Action<byte, byte> readCallback)
        {
            register.DefineReadCallback(readCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for write callback registration. For description see <see cref="ByteRegister.DefineWriteCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static ByteRegister WithWriteCallback(this ByteRegister register, Action<byte, byte> writeCallback)
        {
            register.DefineWriteCallback(writeCallback);
            return register;
        }

        /// <summary>
        /// Fluent API for change callback registration. For description see <see cref="ByteRegister.DefineChangeCallback"/>.
        /// </summary>
        /// <returns>This register with a defined callback.</returns>
        public static ByteRegister WithChangeCallback(this ByteRegister register, Action<byte, byte> changeCallback)
        {
            register.DefineChangeCallback(changeCallback);
            return register;
        }
    }
}
