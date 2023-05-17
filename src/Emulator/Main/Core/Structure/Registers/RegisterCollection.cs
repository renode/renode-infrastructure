//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System;

namespace Antmicro.Renode.Core.Structure.Registers
{
    public sealed class QuadWordRegisterCollection : BaseRegisterCollection<ulong, QuadWordRegister>
    {
        public QuadWordRegisterCollection(IPeripheral parent, IDictionary<long, QuadWordRegister> registersMap = null) : base(parent, registersMap)
        {
        }
    }

    public sealed class DoubleWordRegisterCollection : BaseRegisterCollection<uint, DoubleWordRegister>
    {
        public DoubleWordRegisterCollection(IPeripheral parent, IDictionary<long, DoubleWordRegister> registersMap = null) : base(parent, registersMap)
        {
        }
    }

    public sealed class WordRegisterCollection : BaseRegisterCollection<ushort, WordRegister>
    {
        public WordRegisterCollection(IPeripheral parent, IDictionary<long, WordRegister> registersMap = null) : base(parent, registersMap)
        {
        }
    }

    public sealed class ByteRegisterCollection : BaseRegisterCollection<byte, ByteRegister>
    {
        public ByteRegisterCollection(IPeripheral parent, IDictionary<long, ByteRegister> registersMap = null) : base(parent, registersMap)
        {
        }
    }

    public abstract class BaseRegisterCollection<T, R> : IRegisterCollection where R: PeripheralRegister, IPeripheralRegister<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Core.Structure.Registers.ByteRegisterCollection"/> class.
        /// </summary>
        /// <param name="parent">Parent peripheral (for logging purposes).</param>
        /// <param name="registersMap">Map of register offsets and registers.</param>
        public BaseRegisterCollection(IPeripheral parent, IDictionary<long, R> registersMap = null)
        {
            this.parent = parent;
            this.registers = (registersMap != null)
                ? new Dictionary<long, R>(registersMap)
                : new Dictionary<long, R>();
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
            R register;
            if(registers.TryGetValue(offset, out register))
            {
                result = register.Read();
                return true;
            }
            result = default(T);
            return false;
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
            R register;
            if(registers.TryGetValue(offset, out register))
            {
                register.Write(offset, value);
                return true;
            }
            return false;
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
        /// Defines a new register and adds it to the collection.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="resetValue">Register reset value.</param>
        /// <param name="softResettable">Indicates if the register is cleared on soft reset.</param>
        /// <returns>Newly added register.</returns>
        public R DefineRegister(long offset, T resetValue = default(T), bool softResettable = true)
        {
            var constructor = typeof(R).GetConstructor(new Type[] { typeof(IPeripheral), typeof(ulong), typeof(bool) });
            var reg = (R)constructor.Invoke(new object[] { parent, Misc.CastToULong(resetValue), softResettable });
            registers.Add(offset, reg);
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
            registers.Add(offset, register);
            return register;
        }

        private readonly IPeripheral parent;
        private readonly IDictionary<long, R> registers;
    }

    public interface IRegisterCollection
    {
        void Reset();
    }

    public interface IProvidesRegisterCollection<T> where T : IRegisterCollection
    {
        T RegistersCollection { get; }
    }
}
