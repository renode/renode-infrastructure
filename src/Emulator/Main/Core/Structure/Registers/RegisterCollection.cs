//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System;

namespace Antmicro.Renode.Core.Structure.Registers
{
    public sealed class QuadWordRegisterCollection : BaseRegisterCollection<ulong, QuadWordRegister>, IRegisterCollection<ulong>
    {
        public QuadWordRegisterCollection(IPeripheral parent, IDictionary<long, QuadWordRegister> registersMap = null) : base(parent, registersMap)
        {
        }
    }

    public sealed class DoubleWordRegisterCollection : BaseRegisterCollection<uint, DoubleWordRegister>, IRegisterCollection<uint>
    {
        public DoubleWordRegisterCollection(IPeripheral parent, IDictionary<long, DoubleWordRegister> registersMap = null) : base(parent, registersMap)
        {
        }
    }

    public sealed class WordRegisterCollection : BaseRegisterCollection<ushort, WordRegister>, IRegisterCollection<ushort>
    {
        public WordRegisterCollection(IPeripheral parent, IDictionary<long, WordRegister> registersMap = null) : base(parent, registersMap)
        {
        }
    }

    public sealed class ByteRegisterCollection : BaseRegisterCollection<byte, ByteRegister>, IRegisterCollection<byte>
    {
        public ByteRegisterCollection(IPeripheral parent, IDictionary<long, ByteRegister> registersMap = null) : base(parent, registersMap)
        {
        }
    }

    public static class RegisterCollectionHookExtensions
    {
        public static void AddBeforeReadHook<T, R>(this IProvidesRegisterCollection<R> @this, long offset, Func<long, T?> func)
            where T: struct
            where R: IRegisterCollection
        {
            if(!(@this.RegistersCollection is IRegisterCollection<T> registerCollection))
            {
                throw new ArgumentException($"{@this} is not valid argument", "this");
            }
            registerCollection.AddBeforeReadHook(offset, func);
        }

        public static void AddAfterReadHook<T, R>(this IProvidesRegisterCollection<R> @this, long offset, Func<long, T, T?> func)
            where T: struct
            where R: IRegisterCollection
        {
            if(!(@this.RegistersCollection is IRegisterCollection<T> registerCollection))
            {
                throw new ArgumentException($"{@this} is not valid argument", "this");
            }
            registerCollection.AddAfterReadHook(offset, func);
        }

        public static void AddBeforeWriteHook<T, R>(this IProvidesRegisterCollection<R> @this, long offset, Func<long, T, T?> func)
            where T: struct
            where R: IRegisterCollection
        {
            if(!(@this.RegistersCollection is IRegisterCollection<T> registerCollection))
            {
                throw new ArgumentException($"{@this} is not valid argument", "this");
            }
            registerCollection.AddBeforeWriteHook(offset, func);
        }

        public static void AddAfterWriteHook<T, R>(this IProvidesRegisterCollection<R> @this, long offset, Action<long, T> func)
            where T: struct
            where R: IRegisterCollection
        {
            if(!(@this.RegistersCollection is IRegisterCollection<T> registerCollection))
            {
                throw new ArgumentException($"{@this} is not valid argument", "this");
            }
            registerCollection.AddAfterWriteHook(offset, func);
        }

        public static void RemoveBeforeReadHook<R>(this IProvidesRegisterCollection<R> @this, long offset)
            where R: IRegisterCollection
        {
            @this.RegistersCollection.RemoveBeforeReadHook(offset);
        }

        public static void RemoveAfterReadHook<R>(this IProvidesRegisterCollection<R> @this, long offset)
            where R: IRegisterCollection
        {
            @this.RegistersCollection.RemoveAfterReadHook(offset);
        }

        public static void RemoveBeforeWriteHook<R>(this IProvidesRegisterCollection<R> @this, long offset)
            where R: IRegisterCollection
        {
            @this.RegistersCollection.RemoveBeforeWriteHook(offset);
        }

        public static void RemoveAfterWriteHook<R>(this IProvidesRegisterCollection<R> @this, long offset)
            where R: IRegisterCollection
        {
            @this.RegistersCollection.RemoveAfterWriteHook(offset);
        }
    }

    public abstract class BaseRegisterCollection<T, R> : IRegisterCollection where R: PeripheralRegister, IPeripheralRegister<T> where T: struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Core.Structure.Registers.BaseRegisterCollection"/> class.
        /// </summary>
        /// <param name="parent">Parent peripheral (for logging purposes).</param>
        /// <param name="registersMap">Map of register offsets and registers.</param>
        public BaseRegisterCollection(IPeripheral parent, IDictionary<long, R> registersMap = null)
        {
            this.parent = parent;
            this.beforeReadHooks = new Dictionary<long, Func<long, T?>>();
            this.afterReadHooks = new Dictionary<long, Func<long, T, T?>>();
            this.beforeWriteHooks = new Dictionary<long, Func<long, T, T?>>();
            this.afterWriteHooks = new Dictionary<long, Action<long, T>>();

            this.registers = new Dictionary<long, RegisterSelector<T>>();
            if(registersMap != null)
            {
                foreach(KeyValuePair<long, R> pair in registersMap)
                {
                    AddRegisterInner(pair.Key, pair.Value);
                }
            }
        }

        /// <summary>
        /// Returns the value of a register in a specified offset. If no such register is found, a logger message is issued.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        public T Read(long offset)
        {
            T result;
            if(TryRead(offset, out result))
            {
                return result;
            }
            parent.LogUnhandledRead(offset);
            return default(T);
        }

        /// <summary>
        /// Tries to read from a register in a specified offset.
        /// </summary>
        /// <returns><c>true</c>, if register was found, <c>false</c> otherwise.</returns>
        /// <param name="offset">Register offset.</param>
        /// <param name="result">Read value.</param>
        public bool TryRead(long offset, out T result)
        {
            if(beforeReadHooks.TryGetValue(offset, out var beforeReadHook))
            {
                var hookOutput = beforeReadHook(offset);
                if(hookOutput != null)
                {
                    result = (T)hookOutput;
                    return true;
                }
            }

            T? output = null;
            if(registers.TryGetValue(offset, out var register))
            {
                output = register.Read();
            }

            if(afterReadHooks.TryGetValue(offset, out var afterReadHook))
            {
                var hookOutput = afterReadHook(offset, output ?? default(T));
                if(hookOutput != null)
                {
                    output = hookOutput;
                }
            }

            result = output ?? default(T);
            return output != null;
        }

        /// <summary>
        /// Writes to a register in a specified offset. If no such register is found, a logger message is issued.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="value">Value to write.</param>
        public void Write(long offset, T value)
        {
            if(!TryWrite(offset, value))
            {
                parent.LogUnhandledWrite(offset, Misc.CastToULong(value));
            }
        }

        /// <summary>
        /// Tries to write to a register in a specified offset.
        /// </summary>
        /// <returns><c>true</c>, if register was found, <c>false</c> otherwise.</returns>
        /// <param name="offset">Register offset.</param>
        /// <param name="value">Value to write.</param>
        public bool TryWrite(long offset, T value)
        {
            if(registers.TryGetValue(offset, out var register))
            {
                if(beforeWriteHooks.TryGetValue(offset, out var beforeWriteHook))
                {
                    var hookOutput = beforeWriteHook(offset, value);
                    value = hookOutput ?? value;
                }

                register.Write(offset, value);

                if(afterWriteHooks.TryGetValue(offset, out var afterWriteHook))
                {
                    afterWriteHook(offset, value);
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if collection has register defined at given offset.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        public bool HasRegisterAtOffset(long offset)
        {
            return registers.TryGetValue(offset, out var selector) && selector.HasRegister();
        }

        /// <summary>
        /// Resets all registers in this collection.
        /// </summary>
        public void Reset()
        {
            foreach(var register in registers.Values)
            {
                register.Reset();
            }
        }

        /// <summary>
        /// Adds hook which will be executed before any value has been read from the register.
        /// First argument of the callback is the same as the provided offset.
        /// Return value can be a number, in which case the underlying read to register will be bypassed,
        /// or null to continue normal execution.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="hook">Callback which will be run.</param>
        public void AddBeforeReadHook(long offset, Func<long, T?> hook)
        {
            if(beforeReadHooks.ContainsKey(offset))
            {
                throw new RecoverableException($"Before-read hook for 0x{offset:X} is already registered");
            }
            beforeReadHooks.Add(offset, hook);
        }

        /// <summary>
        /// Adds hook which will be executed after the value has been read from the register.
        /// First argument of the callback is the same as the provided offset.
        /// Second argument of the callback is the read value.
        /// Return value can be a number, in which case the read value will be overridden,
        /// or null to continue normal execution.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="hook">Callback which will be run.</param>
        public void AddAfterReadHook(long offset, Func<long, T, T?> hook)
        {
            if(afterReadHooks.ContainsKey(offset))
            {
                throw new RecoverableException($"After-read hook for 0x{offset:X} is already registered");
            }
            afterReadHooks.Add(offset, hook);
        }

        /// <summary>
        /// Adds hook which will be executed before any value has been written to the register.
        /// First argument of the callback is the same as the provided offset.
        /// Second argument of the callback is a value that is going to be written to the register.
        /// Return value can be a number, in which case the value will be overridden,
        /// or null to continue normal execution.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="hook">Callback which will be run.</param>
        public void AddBeforeWriteHook(long offset, Func<long, T, T?> hook)
        {
            if(beforeWriteHooks.ContainsKey(offset))
            {
                throw new RecoverableException($"Before-write hook for 0x{offset:X} is already registered");
            }
            beforeWriteHooks.Add(offset, hook);
        }

        /// <summary>
        /// Adds hook which will be executed before any value has been written to the register.
        /// First argument of the callback is the same as the provided offset.
        /// Second argument of the callback is the value that has been written to the register.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="hook">Callback which will be run.</param>
        public void AddAfterWriteHook(long offset, Action<long, T> hook)
        {
            if(afterWriteHooks.ContainsKey(offset))
            {
                throw new RecoverableException($"After-write hook for 0x{offset:X} is already registered");
            }
            afterWriteHooks.Add(offset, hook);
        }

        /// <summary>
        /// Removes before-read hook at given offset.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        public void RemoveBeforeReadHook(long offset)
        {
            if(!beforeReadHooks.ContainsKey(offset))
            {
                throw new RecoverableException("Before-read hook for 0x{0:X} doesn't exist");
            }
            beforeReadHooks.Remove(offset);
        }

        /// <summary>
        /// Removes after-read hook at given offset.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        public void RemoveAfterReadHook(long offset)
        {
            if(!afterReadHooks.ContainsKey(offset))
            {
                throw new RecoverableException("After-read hook for 0x{0:X} doesn't exist");
            }
            afterReadHooks.Remove(offset);
        }

        /// <summary>
        /// Removes before-write hook at given offset.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        public void RemoveBeforeWriteHook(long offset)
        {
            if(!beforeWriteHooks.ContainsKey(offset))
            {
                throw new RecoverableException("Before-write hook for 0x{0:X} doesn't exist");
            }
            beforeWriteHooks.Remove(offset);
        }

        /// <summary>
        /// Removes after-write hook at given offset.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        public void RemoveAfterWriteHook(long offset)
        {
            if(!afterWriteHooks.ContainsKey(offset))
            {
                throw new RecoverableException("After-write hook for 0x{0:X} doesn't exist");
            }
            afterWriteHooks.Remove(offset);
        }

        /// <summary>
        /// Defines a new register and adds it to the collection.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="resetValue">Register reset value.</param>
        /// <param name="softResettable">Indicates if the register is cleared on soft reset.</param>
        /// <returns>Newly added register.</returns>
        public R DefineRegister(long offset, T resetValue = default(T), bool softResettable = true)
        {
            return DefineConditionalRegister(offset, null, resetValue, softResettable);
        }

        /// <summary>
        /// Defines a new conditional register and adds it to the collection.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="resetValue">Register reset value.</param>
        /// <param name="condition">Condition based on which a register is selected.</param>
        /// <param name="softResettable">Indicates if the register is cleared on soft reset.</param>
        /// <returns>Newly added register.</returns>
        public R DefineConditionalRegister(long offset, Func<bool> condition, T resetValue = default(T), bool softResettable = true)
        {
            var constructor = typeof(R).GetConstructor(new Type[] { typeof(IPeripheral), typeof(ulong), typeof(bool) });
            var reg = (R)constructor.Invoke(new object[] { parent, Misc.CastToULong(resetValue), softResettable });
            AddRegisterInner(offset, reg, condition);
            return reg;
        }

        /// <summary>
        /// Adds an existing register to the collection.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="register">Register to add.</param>
        /// <returns>Added register (the same passed in <see cref="register"> argument).</returns>
        public R AddRegister(long offset, R register)
        {
            AddRegisterInner(offset, register);
            return register;
        }

        /// <summary>
        /// Adds an existing register with a condition to the collection.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="register">Register to add.</param>
        /// <param name="condition">Condition based on which a register is selected.</param>
        /// <returns>Added register (the same passed in <see cref="register"> argument).</returns>
        public R AddConditionalRegister(long offset, R register, Func<bool> condition)
        {
            AddRegisterInner(offset, register, condition);
            return register;
        }

        /// <summary>
        /// Adds a register and condition to a new or existing selector.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="register">Rgister to add.</param>
        /// <param name="condition">Condition based on which a register is selected.</param>
        private void AddRegisterInner(long offset, R register, Func<bool> condition = null)
        {
            if(!registers.ContainsKey(offset))
            {
                registers.Add(offset, new RegisterSelector<T>());
            }
            try
            {
                registers[offset].AddRegister(register, condition);
            }
            catch(Exception e)
            {
                throw new ConstructionException($"At offset 0x{offset:x}: {e.Message}", e);
            }
        }

        private readonly IPeripheral parent;
        private readonly IDictionary<long, RegisterSelector<T>> registers;

        private readonly IDictionary<long, Func<long, T?>> beforeReadHooks;
        private readonly IDictionary<long, Func<long, T, T?>> afterReadHooks;
        private readonly IDictionary<long, Func<long, T, T?>> beforeWriteHooks;
        private readonly IDictionary<long, Action<long, T>> afterWriteHooks;
    }

    public interface IRegisterCollection
    {
        void Reset();

        void RemoveBeforeReadHook(long offset);
        void RemoveAfterReadHook(long offset);
        void RemoveBeforeWriteHook(long offset);
        void RemoveAfterWriteHook(long offset);
    }

    public interface IRegisterCollection<T> : IRegisterCollection where T: struct
    {
        void AddBeforeReadHook(long offset, Func<long, T?> hook);
        void AddAfterReadHook(long offset, Func<long, T, T?> hook);
        void AddBeforeWriteHook(long offset, Func<long, T, T?> hook);
        void AddAfterWriteHook(long offset, Action<long, T> hook);
    }

    public interface IProvidesRegisterCollection<T> where T : IRegisterCollection
    {
        T RegistersCollection { get; }
    }
}
