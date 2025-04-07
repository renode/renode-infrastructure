//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using System.Linq;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Core.Structure.Registers
{
    /// <summary>
    /// 64 bit <see cref="PeripheralRegister"/>.
    /// </summary>
    public sealed class QuadWordRegister : PeripheralRegister, IPeripheralRegister<ulong>
    {
        /// <summary>
        /// Creates a register with one field, serving a purpose of read and write register.
        /// </summary>
        /// <returns>A new register.</returns>
        /// <param name="resetValue">Reset value.</param>
        /// <param name="name">Ignored parameter, for convenience. Treat it as a comment.</param>
        public static QuadWordRegister CreateRWRegister(ulong resetValue = 0, string name = null, bool softResettable = true)
        {
            //null because parent is used for logging purposes only - this will never happen in this case.
            var register = new QuadWordRegister(null, resetValue, softResettable);
            register.DefineValueField(0, register.RegisterWidth);
            return register;
        }

        public QuadWordRegister(IPeripheral parent, ulong resetValue = 0, bool softResettable = true) : base(parent, resetValue, softResettable, QuadWordWidth)
        {
        }

        /// <summary>
        /// Retrieves the current value of readable fields. All FieldMode values are interpreted and callbacks are executed where applicable.
        /// </summary>
        public ulong Read()
        {
            return ReadInner();
        }

        /// <summary>
        /// Writes the given value to writeable fields. All FieldMode values are interpreted and callbacks are executed where applicable.
        /// </summary>
        public void Write(long offset, ulong value)
        {
            WriteInner(offset, value);
        }

        /// <summary>
        /// Defines the read callback that is called once on each read, regardles of the number of defined register fields.
        /// Note that it will also be called for unreadable registers.
        /// </summary>
        /// <param name="readCallback">Method to be called whenever this register is read. The first parameter is the value of this register before read,
        /// the second parameter is the value after read.</param>
        public void DefineReadCallback(Action<ulong, ulong> readCallback)
        {
            readCallbacks.Add(readCallback);
        }

        /// <summary>
        /// Defines the write callback that is called once on each write, regardles of the number of defined register fields.
        /// Note that it will also be called for unwrittable registers.
        /// </summary>
        /// <param name="writeCallback">Method to be called whenever this register is written to. The first parameter is the value of this register before write,
        /// the second parameter is the value written (without any modification).</param>
        public void DefineWriteCallback(Action<ulong, ulong> writeCallback)
        {
            writeCallbacks.Add(writeCallback);
        }

        /// <summary>
        /// Defines the change callback that is called once on each change, regardles of the number of defined register fields.
        /// Note that it will also be called for unwrittable registers.
        /// </summary>
        /// <param name="changeCallback">Method to be called whenever this register's value is changed, either due to read or write. The first parameter is the value of this register before change,
        /// the second parameter is the value after change.</param>
        public void DefineChangeCallback(Action<ulong, ulong> changeCallback)
        {
            changeCallbacks.Add(changeCallback);
        }

        /// <summary>
        /// Gets or sets the underlying value without any modification or reaction.
        /// </summary>
        public ulong Value
        {
            get
            {
                return UnderlyingValue;
            }
            set
            {
                UnderlyingValue = value;
            }
        }

        public const int QuadWordWidth = 64;

        protected override void CallChangeHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(changeCallbacks, oldValue, newValue);
        }

        protected override void CallReadHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(readCallbacks, oldValue, newValue);
        }

        protected override void CallWriteHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(writeCallbacks, oldValue, newValue);
        }

        private List<Action<ulong, ulong>> readCallbacks = new List<Action<ulong, ulong>>();
        private List<Action<ulong, ulong>> writeCallbacks = new List<Action<ulong, ulong>>();
        private List<Action<ulong, ulong>> changeCallbacks = new List<Action<ulong, ulong>>();
    }

    /// <summary>
    /// 32 bit <see cref="PeripheralRegister"/>.
    /// </summary>
    public sealed class DoubleWordRegister : PeripheralRegister, IPeripheralRegister<uint>
    {
        /// <summary>
        /// Creates a register with one field, serving a purpose of read and write register.
        /// </summary>
        /// <returns>A new register.</returns>
        /// <param name="resetValue">Reset value.</param>
        /// <param name="name">Ignored parameter, for convenience. Treat it as a comment.</param>
        public static DoubleWordRegister CreateRWRegister(uint resetValue = 0, string name = null, bool softResettable = true)
        {
            //null because parent is used for logging purposes only - this will never happen in this case.
            var register = new DoubleWordRegister(null, resetValue, softResettable);
            register.DefineValueField(0, register.RegisterWidth);
            return register;
        }

        public DoubleWordRegister(IPeripheral parent, ulong resetValue = 0, bool softResettable = true) : base(parent, resetValue, softResettable, DoubleWordWidth)
        {
        }

        /// <summary>
        /// Retrieves the current value of readable fields. All FieldMode values are interpreted and callbacks are executed where applicable.
        /// </summary>
        public uint Read()
        {
            return (uint)ReadInner();
        }

        /// <summary>
        /// Writes the given value to writeable fields. All FieldMode values are interpreted and callbacks are executed where applicable.
        /// </summary>
        public void Write(long offset, uint value)
        {
            WriteInner(offset, value);
        }

        /// <summary>
        /// Defines the read callback that is called once on each read, regardles of the number of defined register fields.
        /// Note that it will also be called for unreadable registers.
        /// </summary>
        /// <param name="readCallback">Method to be called whenever this register is read. The first parameter is the value of this register before read,
        /// the second parameter is the value after read.</param>
        public void DefineReadCallback(Action<uint, uint> readCallback)
        {
            readCallbacks.Add(readCallback);
        }

        /// <summary>
        /// Defines the write callback that is called once on each write, regardles of the number of defined register fields.
        /// Note that it will also be called for unwrittable registers.
        /// </summary>
        /// <param name="writeCallback">Method to be called whenever this register is written to. The first parameter is the value of this register before write,
        /// the second parameter is the value written (without any modification).</param>
        public void DefineWriteCallback(Action<uint, uint> writeCallback)
        {
            writeCallbacks.Add(writeCallback);
        }

        /// <summary>
        /// Defines the change callback that is called once on each change, regardles of the number of defined register fields.
        /// Note that it will also be called for unwrittable registers.
        /// </summary>
        /// <param name="changeCallback">Method to be called whenever this register's value is changed, either due to read or write. The first parameter is the value of this register before change,
        /// the second parameter is the value after change.</param>
        public void DefineChangeCallback(Action<uint, uint> changeCallback)
        {
            changeCallbacks.Add(changeCallback);
        }

        /// <summary>
        /// Gets or sets the underlying value without any modification or reaction.
        /// </summary>
        public uint Value
        {
            get
            {
                return (uint)UnderlyingValue;
            }
            set
            {
                UnderlyingValue = value;
            }
        }

        public const int DoubleWordWidth = 32;

        protected override void CallChangeHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(changeCallbacks, (uint)oldValue, (uint)newValue);
        }

        protected override void CallReadHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(readCallbacks, (uint)oldValue, (uint)newValue);
        }

        protected override void CallWriteHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(writeCallbacks, (uint)oldValue, (uint)newValue);
        }

        private List<Action<uint, uint>> readCallbacks = new List<Action<uint, uint>>();
        private List<Action<uint, uint>> writeCallbacks = new List<Action<uint, uint>>();
        private List<Action<uint, uint>> changeCallbacks = new List<Action<uint, uint>>();
    }

    /// <summary>
    /// 16 bit <see cref="PeripheralRegister"/>.
    /// </summary>
    public sealed class WordRegister : PeripheralRegister, IPeripheralRegister<ushort>
    {
        /// <summary>
        /// Creates a register with one field, serving a purpose of read and write register.
        /// </summary>
        /// <returns>A new register.</returns>
        /// <param name="resetValue">Reset value.</param>
        /// <param name="name">Ignored parameter, for convenience. Treat it as a comment.</param>
        public static WordRegister CreateRWRegister(uint resetValue = 0, string name = null, bool softResettable = true)
        {
            //null because parent is used for logging purposes only - this will never happen in this case.
            var register = new WordRegister(null, resetValue, softResettable);
            register.DefineValueField(0, register.RegisterWidth);
            return register;
        }

        public WordRegister(IPeripheral parent, ulong resetValue = 0, bool softResettable = true) : base(parent, resetValue, softResettable, WordWidth)
        {
        }

        /// <summary>
        /// Retrieves the current value of readable fields. All FieldMode values are interpreted and callbacks are executed where applicable.
        /// </summary>
        public ushort Read()
        {
            return (ushort)ReadInner();
        }

        /// <summary>
        /// Writes the given value to writeable fields. All FieldMode values are interpreted and callbacks are executed where applicable.
        /// </summary>
        public void Write(long offset, ushort value)
        {
            WriteInner(offset, value);
        }

        /// <summary>
        /// Defines the read callback that is called once on each read, regardles of the number of defined register fields.
        /// Note that it will also be called for unreadable registers.
        /// </summary>
        /// <param name="readCallback">Method to be called whenever this register is read. The first parameter is the value of this register before read,
        /// the second parameter is the value after read.</param>
        public void DefineReadCallback(Action<ushort, ushort> readCallback)
        {
            readCallbacks.Add(readCallback);
        }

        /// <summary>
        /// Defines the write callback that is called once on each write, regardles of the number of defined register fields.
        /// Note that it will also be called for unwrittable registers.
        /// </summary>
        /// <param name="writeCallback">Method to be called whenever this register is written to. The first parameter is the value of this register before write,
        /// the second parameter is the value written (without any modification).</param>
        public void DefineWriteCallback(Action<ushort, ushort> writeCallback)
        {
            writeCallbacks.Add(writeCallback);
        }

        /// <summary>
        /// Defines the change callback that is called once on each change, regardles of the number of defined register fields.
        /// Note that it will also be called for unwrittable registers.
        /// </summary>
        /// <param name="changeCallback">Method to be called whenever this register's value is changed, either due to read or write. The first parameter is the value of this register before change,
        /// the second parameter is the value after change.</param>
        public void DefineChangeCallback(Action<ushort, ushort> changeCallback)
        {
            changeCallbacks.Add(changeCallback);
        }

        /// <summary>
        /// Gets or sets the underlying value without any modification or reaction.
        /// </summary>
        public ushort Value
        {
            get
            {
                return (ushort)UnderlyingValue;
            }
            set
            {
                UnderlyingValue = value;
            }
        }

        public const int WordWidth = 16;

        protected override void CallChangeHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(changeCallbacks, (ushort)oldValue, (ushort)newValue);
        }

        protected override void CallReadHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(readCallbacks, (ushort)oldValue, (ushort)newValue);
        }

        protected override void CallWriteHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(writeCallbacks, (ushort)oldValue, (ushort)newValue);
        }

        private List<Action<ushort, ushort>> readCallbacks = new List<Action<ushort, ushort>>();
        private List<Action<ushort, ushort>> writeCallbacks = new List<Action<ushort, ushort>>();
        private List<Action<ushort, ushort>> changeCallbacks = new List<Action<ushort, ushort>>();
    }

    /// <summary>
    /// 8 bit <see cref="PeripheralRegister"/>.
    /// </summary>
    public sealed class ByteRegister : PeripheralRegister, IPeripheralRegister<byte>
    {
        /// <summary>
        /// Creates a register with one field, serving a purpose of read and write register.
        /// </summary>
        /// <returns>A new register.</returns>
        /// <param name="resetValue">Reset value.</param>
        /// <param name="name">Ignored parameter, for convenience. Treat it as a comment.</param>
        public static ByteRegister CreateRWRegister(uint resetValue = 0, string name = null, bool softResettable = true)
        {
            //null because parent is used for logging purposes only - this will never happen in this case.
            var register = new ByteRegister(null, resetValue, softResettable);
            register.DefineValueField(0, register.RegisterWidth);
            return register;
        }

        public ByteRegister(IPeripheral parent, ulong resetValue = 0, bool softResettable = true) : base(parent, resetValue, softResettable, ByteWidth)
        {
        }

        /// <summary>
        /// Retrieves the current value of readable fields. All FieldMode values are interpreted and callbacks are executed where applicable.
        /// </summary>
        public byte Read()
        {
            return (byte)ReadInner();
        }

        /// <summary>
        /// Writes the given value to writeable fields. All FieldMode values are interpreted and callbacks are executed where applicable.
        /// </summary>
        public void Write(long offset, byte value)
        {
            WriteInner(offset, value);
        }

        /// <summary>
        /// Defines the read callback that is called once on each read, regardles of the number of defined register fields.
        /// Note that it will also be called for unreadable registers.
        /// </summary>
        /// <param name="readCallback">Method to be called whenever this register is read. The first parameter is the value of this register before read,
        /// the second parameter is the value after read.</param>
        public void DefineReadCallback(Action<byte, byte> readCallback)
        {
            readCallbacks.Add(readCallback);
        }

        /// <summary>
        /// Defines the write callback that is called once on each write, regardles of the number of defined register fields.
        /// Note that it will also be called for unwrittable registers.
        /// </summary>
        /// <param name="writeCallback">Method to be called whenever this register is written to. The first parameter is the value of this register before write,
        /// the second parameter is the value written (without any modification).</param>
        public void DefineWriteCallback(Action<byte, byte> writeCallback)
        {
            writeCallbacks.Add(writeCallback);
        }

        /// <summary>
        /// Defines the change callback that is called once on each change, regardles of the number of defined register fields.
        /// Note that it will also be called for unwrittable registers.
        /// </summary>
        /// <param name="changeCallback">Method to be called whenever this register's value is changed, either due to read or write. The first parameter is the value of this register before change,
        /// the second parameter is the value after change.</param>
        public void DefineChangeCallback(Action<byte, byte> changeCallback)
        {
            changeCallbacks.Add(changeCallback);
        }

        /// <summary>
        /// Gets or sets the underlying value without any modification or reaction.
        /// </summary>
        public byte Value
        {
            get
            {
                return (byte)UnderlyingValue;
            }
            set
            {
                UnderlyingValue = value;
            }
        }

        public const int ByteWidth = 8;

        protected override void CallChangeHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(changeCallbacks, (byte)oldValue, (byte)newValue);
        }

        protected override void CallReadHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(readCallbacks, (byte)oldValue, (byte)newValue);
        }

        protected override void CallWriteHandlers(ulong oldValue, ulong newValue)
        {
            CallHandlers(writeCallbacks, (byte)oldValue, (byte)newValue);
        }

        private List<Action<byte, byte>> readCallbacks = new List<Action<byte, byte>>();
        private List<Action<byte, byte>> writeCallbacks = new List<Action<byte, byte>>();
        private List<Action<byte, byte>> changeCallbacks = new List<Action<byte, byte>>();

    }

    public interface IPeripheralRegister<T>
    {
        T Read();
        void Write(long offset, T value);
        void Reset();
    }

    /// <summary>
    /// Represents a register of a given width, containing defined fields.
    /// Fields may not exceed this register's width, nor may they overlap each other.
    /// Fields that are not handled (e.g. left for future implementation or unimportant) have to be tagged.
    /// Otherwise, they will not be logged.
    /// </summary>
    public abstract partial class PeripheralRegister
    {
        /// <summary>
        /// Restores this register's value to its reset value, defined on per-field basis.
        /// </summary>
        public void Reset()
        {
            BitHelper.UpdateWithMasked(ref UnderlyingValue, resetValue, resettableMask);
        }

        /// <summary>
        /// Wrapper for <see cref="Tag"/> method, tagging bits as "RESERVED".
        /// </summary>
        /// <param name="position">Offset in the register.</param>
        /// <param name="width">Width of field.</param>
        /// <param name="allowedValue">Value allowed to be written.<\param>
        public void Reserved(int position, int width, ulong? allowedValue = null)
        {
            Tag("RESERVED", position, width, allowedValue);
        }

        /// <summary>
        /// Mark an unhandled field, so it is logged with its name.
        /// </summary>
        /// <param name="name">Name of the unhandled field.</param>
        /// <param name="position">Offset in the register.</param>
        /// <param name="width">Width of field.</param>
        /// <param name="allowedValue">Value allowed to be written.<\param>
        public void Tag(string name, int position, int width, ulong? allowedValue = null)
        {
            ThrowIfRangeIllegal(position, width, name);

            if(allowedValue != null)
            {
                ThrowIfAllowedValueDoesNotFitInWidth(width, allowedValue.Value, name);
            }

            tags.Add(new Tag
            {
                Name = name,
                Position = position,
                Width = width,
                AllowedValue = allowedValue
            });
        }

        /// <summary>
        /// Defines the flag field. Its width is always 1 and is interpreted as boolean value.
        /// </summary>
        /// <param name="position">Offset in the register.</param>
        /// <param name="mode">Access modifiers of this field.</param>
        /// <param name="readCallback">Method to be called whenever the containing register is read. The first parameter is the value of this field before read,
        /// the second parameter is the value after read. Note that it will also be called for unreadable fields.</param>
        /// <param name="writeCallback">Method to be called whenever the containing register is written to. The first parameter is the value of this field before write,
        /// the second parameter is the value written (without any modification). Note that it will also be called for unwrittable fields.</param>
        /// <param name="changeCallback">Method to be called whenever this field's value is changed, either due to read or write. The first parameter is the value of this field before change,
        /// the second parameter is the value after change. Note that it will also be called for unwrittable fields.</param>
        /// <param name="valueProviderCallback">Method to be called whenever this field is read. The value passed is the current field's value, that will be overwritten by
        /// the value returned from it. This returned value is eventually passed as the first parameter of <paramref name="readCallback"/>.</param>
        /// <param name="softResettable">Indicates whether the field should be cleared by soft reset.</param>
        /// <param name="name">Ignored parameter, for convenience. Treat it as a comment.</param>
        public IFlagRegisterField DefineFlagField(int position, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<bool, bool> readCallback = null,
            Action<bool, bool> writeCallback = null, Action<bool, bool> changeCallback = null, Func<bool, bool> valueProviderCallback = null, bool softResettable = true,
            string name = null)
        {
            ThrowIfRangeIllegal(position, 1, name);
            var field = new FlagRegisterField(this, position, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, name);
            registerFields.Add(field);
            if(!softResettable)
            {
                MarkNonResettable(position, 1);
            }
            RecalculateFieldMask();
            return field;
        }

        /// <summary>
        /// Defines the value field. Its value is interpreted as a regular number.
        /// </summary>
        /// <param name="position">Offset in the register.</param>
        /// <param name="width">Maximum width of the value, in terms of binary representation.</param>
        /// <param name="mode">Access modifiers of this field.</param>
        /// <param name="readCallback">Method to be called whenever the containing register is read. The first parameter is the value of this field before read,
        /// the second parameter is the value after read. Note that it will also be called for unreadable fields.</param>
        /// <param name="writeCallback">Method to be called whenever the containing register is written to. The first parameter is the value of this field before write,
        /// the second parameter is the value written (without any modification). Note that it will also be called for unwrittable fields.</param>
        /// <param name="changeCallback">Method to be called whenever this field's value is changed, either due to read or write. The first parameter is the value of this field before change,
        /// the second parameter is the value after change. Note that it will also be called for unwrittable fields.</param>
        /// <param name="valueProviderCallback">Method to be called whenever this field is read. The value passed is the current field's value, that will be overwritten by
        /// the value returned from it. This returned value is eventually passed as the first parameter of <paramref name="readCallback"/>.</param>
        /// <param name="softResettable">Indicates whether the field should be cleared by soft reset.</param>
        /// <param name="name">Ignored parameter, for convenience. Treat it as a comment.</param>
        public IValueRegisterField DefineValueField(int position, int width, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<ulong, ulong> readCallback = null,
            Action<ulong, ulong> writeCallback = null, Action<ulong, ulong> changeCallback = null, Func<ulong, ulong> valueProviderCallback = null, bool softResettable = true,
            string name = null)
        {
            ThrowIfRangeIllegal(position, width, name);
            ThrowIfZeroWidth(position, width, name);
            var field = new ValueRegisterField(this, position, width, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, name);
            registerFields.Add(field);
            if(!softResettable)
            {
                MarkNonResettable(position, width);
            }
            RecalculateFieldMask();
            return field;
        }

        /// <summary>
        /// Defines the enum field. Its value is interpreted as an enumeration
        /// </summary>
        /// <param name="position">Offset in the register.</param>
        /// <param name="width">Maximum width of the value, in terms of binary representation.</param>
        /// <param name="mode">Access modifiers of this field.</param>
        /// <param name="readCallback">Method to be called whenever the containing register is read. The first parameter is the value of this field before read,
        /// the second parameter is the value after read. Note that it will also be called for unreadable fields.</param>
        /// <param name="writeCallback">Method to be called whenever the containing register is written to. The first parameter is the value of this field before write,
        /// the second parameter is the value written (without any modification). Note that it will also be called for unwrittable fields.</param>
        /// <param name="changeCallback">Method to be called whenever this field's value is changed, either due to read or write. The first parameter is the value of this field before change,
        /// the second parameter is the value after change. Note that it will also be called for unwrittable fields.</param>
        /// <param name="valueProviderCallback">Method to be called whenever this field is read. The value passed is the current field's value, that will be overwritten by
        /// the value returned from it. This returned value is eventually passed as the first parameter of <paramref name="readCallback"/>.</param>
        /// <param name="softResettable">Indicates whether the field should be cleared by soft reset.</param>
        /// <param name="name">Ignored parameter, for convenience. Treat it as a comment.</param>
        public IEnumRegisterField<TEnum> DefineEnumField<TEnum>(int position, int width, FieldMode mode = FieldMode.Read | FieldMode.Write, Action<TEnum, TEnum> readCallback = null,
            Action<TEnum, TEnum> writeCallback = null, Action<TEnum, TEnum> changeCallback = null, Func<TEnum, TEnum> valueProviderCallback = null, bool softResettable = true,
            string name = null)
            where TEnum : struct, IConvertible
        {
            ThrowIfRangeIllegal(position, width, name);
            ThrowIfZeroWidth(position, width, name);
            var field = new EnumRegisterField<TEnum>(this, position, width, mode, readCallback, writeCallback, changeCallback, valueProviderCallback, name);
            registerFields.Add(field);
            if(!softResettable)
            {
                MarkNonResettable(position, width);
            }
            RecalculateFieldMask();
            return field;
        }

        public int RegisterWidth { get; }

        protected PeripheralRegister(IPeripheral parent, ulong resetValue, bool softResettable, int width)
        {
            this.parent = parent;
            RegisterWidth = width;
            this.resetValue = resetValue;
            // We want to reset the register before setting the resettableMask. If we don't do that then
            // the register will not be initialized to the resetValue, instead it will hold the default value of 0
            Reset();
            if(!softResettable)
            {
                resettableMask = 0;
            }
        }

        protected ulong ReadInner()
        {
            foreach(var registerField in registerFields)
            {
                UnderlyingValue = registerField.CallValueProviderHandler(UnderlyingValue);
            }
            var baseValue = UnderlyingValue;
            var valueToRead = UnderlyingValue;
            var changedFields = new List<RegisterField>();
            foreach(var registerField in registerFields)
            {
                if(!registerField.fieldMode.IsReadable())
                {
                    BitHelper.ClearBits(ref valueToRead, registerField.position, registerField.width);
                }

                if(registerField.fieldMode.IsFlagSet(FieldMode.ReadToClear)
                   && BitHelper.AreAnyBitsSet(UnderlyingValue, registerField.position, registerField.width))
                {
                    BitHelper.ClearBits(ref UnderlyingValue, registerField.position, registerField.width);
                    changedFields.Add(registerField);
                }
                if(registerField.fieldMode.IsFlagSet(FieldMode.ReadToSet)
                   && !BitHelper.AreAllBitsSet(UnderlyingValue, registerField.position, registerField.width))
                {
                    BitHelper.SetBits(ref UnderlyingValue, registerField.position, registerField.width);
                    changedFields.Add(registerField);
                }
            }
            foreach(var registerField in registerFields)
            {
                registerField.CallReadHandler(baseValue, UnderlyingValue);
            }
            foreach(var changedRegister in changedFields.Distinct())
            {
                changedRegister.CallChangeHandler(baseValue, UnderlyingValue);
            }

            CallReadHandlers(baseValue, UnderlyingValue);
            if(changedFields.Any())
            {
                CallChangeHandlers(baseValue, UnderlyingValue);
            }

            return valueToRead;
        }

        protected void WriteInner(long offset, ulong value)
        {
            var baseValue = UnderlyingValue;
            var difference = UnderlyingValue ^ value;
            var changedRegisters = new List<RegisterField>();
            foreach(var registerField in registerFields)
            {
                //switch is OK, because write modes are exclusive.
                switch(registerField.fieldMode.WriteBits())
                {
                    case FieldMode.Write:
                        if(BitHelper.AreAnyBitsSet(difference, registerField.position, registerField.width))
                        {
                            BitHelper.UpdateWith(ref UnderlyingValue, value, registerField.position, registerField.width);
                            changedRegisters.Add(registerField);
                        }
                        break;
                    case FieldMode.Set:
                        var setRegisters = value & (~UnderlyingValue);
                        if(BitHelper.AreAnyBitsSet(setRegisters, registerField.position, registerField.width))
                        {
                            BitHelper.OrWith(ref UnderlyingValue, setRegisters, registerField.position, registerField.width);
                            changedRegisters.Add(registerField);
                        }
                        break;
                    case FieldMode.Toggle:
                        if(BitHelper.AreAnyBitsSet(value, registerField.position, registerField.width))
                        {
                            BitHelper.XorWith(ref UnderlyingValue, value, registerField.position, registerField.width);
                            changedRegisters.Add(registerField);
                        }
                        break;
                    case FieldMode.WriteOneToClear:
                        if(BitHelper.AreAnyBitsSet((~difference & value), registerField.position, registerField.width))
                        {
                            BitHelper.AndWithNot(ref UnderlyingValue, value, registerField.position, registerField.width);
                            changedRegisters.Add(registerField);
                        }
                        break;
                    case FieldMode.WriteZeroToClear:
                        if(BitHelper.AreAnyBitsSet((difference & UnderlyingValue), registerField.position, registerField.width))
                        {
                            BitHelper.AndWithNot(ref UnderlyingValue, ~value, registerField.position, registerField.width);
                            changedRegisters.Add(registerField);
                        }
                        break;
                    case FieldMode.WriteZeroToSet:
                        var negSetRegisters = ~value & (~UnderlyingValue);
                        if(BitHelper.AreAnyBitsSet(negSetRegisters, registerField.position, registerField.width))
                        {
                            BitHelper.OrWith(ref UnderlyingValue, negSetRegisters, registerField.position, registerField.width);
                            changedRegisters.Add(registerField);
                        }
                        break;
                    case FieldMode.WriteZeroToToggle:
                        if(BitHelper.AreAnyBitsSet(~value, registerField.position, registerField.width))
                        {
                            BitHelper.XorWith(ref UnderlyingValue, ~value, registerField.position, registerField.width);
                            changedRegisters.Add(registerField);
                        }
                        break;
                    case FieldMode.WriteToClear:
                        if(BitHelper.AreAnyBitsSet(UnderlyingValue, registerField.position, registerField.width))
                        {
                            BitHelper.ClearBits(ref UnderlyingValue, registerField.position, registerField.width);
                            changedRegisters.Add(registerField);
                        }
                        break;
                    case FieldMode.WriteToSet:
                        if(!BitHelper.AreAllBitsSet(UnderlyingValue, registerField.position, registerField.width))
                        {
                            BitHelper.SetBits(ref UnderlyingValue, registerField.position, registerField.width);
                            changedRegisters.Add(registerField);
                        }
                        break;
                }
            }
            foreach(var registerField in registerFields)
            {
                registerField.CallWriteHandler(baseValue, value);
            }
            foreach(var changedRegister in changedRegisters.Distinct())
            {
                changedRegister.CallChangeHandler(baseValue, UnderlyingValue);
            }

            CallWriteHandlers(baseValue, value);
            if(changedRegisters.Any())
            {
                CallChangeHandlers(baseValue, UnderlyingValue);
            }

            var unhandledWrites = difference & ~definedFieldsMask;
            if(unhandledWrites != 0)
            {
                parent.Log(LogLevel.Warning, TagLogger(offset, unhandledWrites, value));
            }

            if(InvalidTagValues(offset, value, out var invalidValueLog))
            {
                parent.Log(LogLevel.Error, invalidValueLog);
            }
        }

        protected void CallHandlers<T>(List<Action<T, T>> handlers, T oldValue, T newValue)
        {
            foreach(var handler in handlers)
            {
                handler(oldValue, newValue);
            }
        }

        protected abstract void CallWriteHandlers(ulong oldValue, ulong newValue);
        protected abstract void CallReadHandlers(ulong oldValue, ulong newValue);
        protected abstract void CallChangeHandlers(ulong oldValue, ulong newValue);


        protected ulong UnderlyingValue;

        /// <summary>
        /// Returns information about tag writes. Extracted as a method to allow future lazy evaluation.
        /// </summary>
        /// <param name="offset">The offset of the affected register.</param>
        /// <param name="unhandledMask">Unhandled bits mask.</param>
        /// <param name="originalValue">The whole value written to the register.</param>
        private string TagLogger(long offset, ulong unhandledMask, ulong originalValue)
        {
            var tagsAffected = tags.Where(x => BitHelper.AreAnyBitsSet(unhandledMask, x.Position, x.Width))
                .Select(x =>  new { x.Name, Value = BitHelper.GetValue(originalValue, x.Position, x.Width) });
            return "Unhandled write to offset 0x{2:X}. Unhandled bits: [{1}] when writing value 0x{3:X}.{0}"
                .FormatWith(tagsAffected.Any() ? " Tags: {0}.".FormatWith(
                    tagsAffected.Select(x => "{0} (0x{1:X})".FormatWith(x.Name, x.Value)).Stringify(", ")) : String.Empty,
                    BitHelper.GetSetBitsPretty(unhandledMask),
                    offset,
                    originalValue);
        }

        private bool InvalidTagValues(long offset, ulong originalValue, out string log)
        {
            ulong allowedValuesMask = 0;
            ulong allowedValues = 0;

            foreach(var tag in tags.Where(x => x.AllowedValue != null))
            {
                allowedValuesMask |= BitHelper.CalculateQuadWordMask(tag.Width, tag.Position);
                allowedValues |= tag.AllowedValue.Value << tag.Position;
            }

            var invalidBits = (allowedValues ^ originalValue) & allowedValuesMask;
            if(invalidBits == 0)
            {
                log = "";
                return false;
            }

            var writtenValue = "0b" + Convert.ToString((long)originalValue, 2).PadLeft(RegisterWidth, '0');
            var desiredValues = "0b";

            for(int i = RegisterWidth - 1; i >= 0; i--)
            {
                if(((allowedValuesMask >> i) & 1u) == 1)
                {
                    desiredValues += ((allowedValues >> i) & 1) == 0 ? "0" : "1";
                }
                else
                {
                    desiredValues += "x";
                }
            }
            log = $"Invalid value written to offset 0x{offset:X} reserved bits. Allowed values = {desiredValues}, Value written = {writtenValue}";
            return true;
        }


        private void ThrowIfRangeIllegal(int position, int width, string name)
        {
            if(width < 0)
            {
                throw new ArgumentException("Field {0} has to have a size larger than or equal to 0.".FormatWith(name ?? "at {0} of {1} bits".FormatWith(position, width)));
            }
            if(position + width > RegisterWidth)
            {
                throw new ArgumentException("Field {0} does not fit in the register size.".FormatWith(name ?? "at {0} of {1} bits".FormatWith(position, width)));
            }
            foreach(var field in registerFields.Select(x => new { x.position, x.width }).Concat(tags.Select(x => new { position = x.Position, width = x.Width })))
            {
                var minEnd = Math.Min(position + width, field.position + field.width);
                var maxStart = Math.Max(position, field.position);
                if(minEnd > maxStart)
                {
                    throw new ArgumentException("Field {0} intersects with another range.".FormatWith(name ?? "at {0} of {1} bits".FormatWith(position, width)));
                }
            }
        }

        private void ThrowIfZeroWidth(int position, int width, string name)
        {
            if(width == 0)
            {
                throw new ArgumentException("Field {0} has to have a size not equal to 0.".FormatWith(name ?? "at {0} of {1} bits".FormatWith(position, width)));
            }
        }

        private void ThrowIfAllowedValueDoesNotFitInWidth(int width, ulong allowedValue, string name)
        {
            if((allowedValue >> width) != 0)
            {
                throw new ArgumentException($"Fields {name} allowedValue does not fit in its width");
            }
        }

        private void RecalculateFieldMask()
        {
            var mask = 0UL;
            foreach(var field in registerFields)
            {
                mask |= BitHelper.CalculateQuadWordMask(field.width, field.position);
            }
            definedFieldsMask = mask;
        }

        private void MarkNonResettable(int position, int width)
        {
            BitHelper.ClearBits(ref resettableMask, position, width);
        }

        private List<RegisterField> registerFields = new List<RegisterField>();

        private List<Tag> tags = new List<Tag>();

        private IPeripheral parent;

        private ulong definedFieldsMask;

        private ulong resettableMask = ulong.MaxValue;
        private readonly ulong resetValue;
    }
}
