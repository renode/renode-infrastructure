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
using System;

namespace Antmicro.Renode.Core.Structure.Registers
{
    /// <summary>
    /// Quad word register collection, allowing to write and read from specified offsets.
    /// </summary>
    public class QuadWordRegisterCollection : IRegisterCollection
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Core.Structure.Registers.QuadWordRegisterCollection"/> class.
        /// </summary>
        /// <param name="parent">Parent peripheral (for logging purposes).</param>
        /// <param name="registersMap">Map of register offsets and registers.</param>
        public QuadWordRegisterCollection(IPeripheral parent, IDictionary<long, QuadWordRegister> registersMap = null)
        {
            this.parent = parent;
            this.registers = (registersMap != null)
                ? new Dictionary<long, QuadWordRegister>(registersMap)
                : new Dictionary<long, QuadWordRegister>();
        }

        /// <summary>
        /// Returns the value of a register in a specified offset. If no such register is found, a logger message is issued.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        public ulong Read(long offset)
        {
            ulong result;
            if(TryRead(offset, out result))
            {
                return result;
            }
            parent.LogUnhandledRead(offset);
            return 0;
        }

        /// <summary>
        /// Looks for a register in a specified offset.
        /// </summary>
        /// <returns><c>true</c>, if register was found, <c>false</c> otherwise.</returns>
        /// <param name="offset">Register offset.</param>
        /// <param name="result">Read value.</param>
        public bool TryRead(long offset, out ulong result)
        {
            QuadWordRegister register;
            if(registers.TryGetValue(offset, out register))
            {
                result = register.Read();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>
        /// Writes to a register in a specified offset. If no such register is found, a logger message is issued.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="value">Value to write.</param>
        public void Write(long offset, ulong value)
        {
            if(!TryWrite(offset, value))
            {
                parent.LogUnhandledWrite(offset, value);
            }
        }

        /// <summary>
        /// Tries to write to a register in a specified offset.
        /// </summary>
        /// <returns><c>true</c>, if register was found, <c>false</c> otherwise.</returns>
        /// <param name="offset">Register offset.</param>
        /// <param name="value">Value to write.</param>
        public bool TryWrite(long offset, ulong value)
        {
            QuadWordRegister register;
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
        public QuadWordRegister DefineRegister(long offset, ulong resetValue = 0, bool softResettable = true)
        {
            var reg = new QuadWordRegister(parent, resetValue);
            registers.Add(offset, reg);
            return reg;
        }

        /// <summary>
        /// Adds an existing register to the collection.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="register">Register to add.</param>
        /// <returns>Added register (the same passed in <see cref="register"> argument).</returns>
        public QuadWordRegister AddRegister(long offset, QuadWordRegister register)
        {
            registers.Add(offset, register);
            return register;
        }

        private readonly IPeripheral parent;
        private readonly IDictionary<long, QuadWordRegister> registers;
    }

    /// <summary>
    /// Double word register collection, allowing to write and read from specified offsets.
    /// </summary>
    public class DoubleWordRegisterCollection : IRegisterCollection
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Core.Structure.Registers.DoubleWordRegisterCollection"/> class.
        /// </summary>
        /// <param name="parent">Parent peripheral (for logging purposes).</param>
        /// <param name="registersMap">Map of register offsets and registers.</param>
        public DoubleWordRegisterCollection(IPeripheral parent, IDictionary<long, DoubleWordRegister> registersMap = null)
        {
            this.parent = parent;
            this.registers = (registersMap != null)
                ? new Dictionary<long, DoubleWordRegister>(registersMap)
                : new Dictionary<long, DoubleWordRegister>();
        }

        /// <summary>
        /// Returns the value of a register in a specified offset. If no such register is found, a logger message is issued.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        public uint Read(long offset)
        {
            uint result;
            if(TryRead(offset, out result))
            {
                return result;
            }
            parent.LogUnhandledRead(offset);
            return 0;
        }

        /// <summary>
        /// Looks for a register in a specified offset.
        /// </summary>
        /// <returns><c>true</c>, if register was found, <c>false</c> otherwise.</returns>
        /// <param name="offset">Register offset.</param>
        /// <param name="result">Read value.</param>
        public bool TryRead(long offset, out uint result)
        {
            DoubleWordRegister register;
            if(registers.TryGetValue(offset, out register))
            {
                result = register.Read();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>
        /// Writes to a register in a specified offset. If no such register is found, a logger message is issued.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="value">Value to write.</param>
        public void Write(long offset, uint value)
        {
            if(!TryWrite(offset, value))
            {
                parent.LogUnhandledWrite(offset, value);
            }
        }

        /// <summary>
        /// Tries to write to a register in a specified offset.
        /// </summary>
        /// <returns><c>true</c>, if register was found, <c>false</c> otherwise.</returns>
        /// <param name="offset">Register offset.</param>
        /// <param name="value">Value to write.</param>
        public bool TryWrite(long offset, uint value)
        {
            DoubleWordRegister register;
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
        public DoubleWordRegister DefineRegister(long offset, uint resetValue = 0, bool softResettable = true)
        {
            var reg = new DoubleWordRegister(parent, resetValue);
            registers.Add(offset, reg);
            return reg;
        }

        /// <summary>
        /// Adds an existing register to the collection.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="register">Register to add.</param>
        /// <returns>Added register (the same passed in <see cref="register"> argument).</returns>
        public DoubleWordRegister AddRegister(long offset, DoubleWordRegister register)
        {
            registers.Add(offset, register);
            return register;
        }

        private readonly IPeripheral parent;
        private readonly IDictionary<long, DoubleWordRegister> registers;
    }

    /// <summary>
    /// Word register collection, allowing to write and read from specified offsets.
    /// </summary>
    public class WordRegisterCollection : IRegisterCollection
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Core.Structure.Registers.WordRegisterCollection"/> class.
        /// </summary>
        /// <param name="parent">Parent peripheral (for logging purposes).</param>
        /// <param name="registersMap">Map of register offsets and registers.</param>
        public WordRegisterCollection(IPeripheral parent, IDictionary<long, WordRegister> registersMap = null)
        {
            this.parent = parent;
            this.registers = (registersMap != null)
                ? new Dictionary<long, WordRegister>(registersMap)
                : new Dictionary<long, WordRegister>();
        }

        /// <summary>
        /// Returns the value of a register in a specified offset. If no such register is found, a logger message is issued.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        public ushort Read(long offset)
        {
            ushort result;
            if(TryRead(offset, out result))
            {
                return result;
            }
            parent.LogUnhandledRead(offset);
            return 0;
        }

        /// <summary>
        /// Looks for a register in a specified offset.
        /// </summary>
        /// <returns><c>true</c>, if register was found, <c>false</c> otherwise.</returns>
        /// <param name="offset">Register offset.</param>
        /// <param name="result">Read value.</param>
        public bool TryRead(long offset, out ushort result)
        {
            WordRegister register;
            if(registers.TryGetValue(offset, out register))
            {
                result = register.Read();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>
        /// Writes to a register in a specified offset. If no such register is found, a logger message is issued.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="value">Value to write.</param>
        public void Write(long offset, ushort value)
        {
            if(!TryWrite(offset, value))
            {
                parent.LogUnhandledWrite(offset, value);
            }
        }

        /// <summary>
        /// Tries to write to a register in a specified offset.
        /// </summary>
        /// <returns><c>true</c>, if register was found, <c>false</c> otherwise.</returns>
        /// <param name="offset">Register offset.</param>
        /// <param name="value">Value to write.</param>
        public bool TryWrite(long offset, ushort value)
        {
            WordRegister register;
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
        public WordRegister DefineRegister(long offset, ushort resetValue = 0, bool softResettable = true)
        {
            var reg = new WordRegister(parent, resetValue, softResettable);
            registers.Add(offset, reg);
            return reg;
        }

        /// <summary>
        /// Adds an existing register to the collection.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="register">Register to add.</param>
        /// <returns>Added register (the same passed in <see cref="register"> argument).</returns>
        public WordRegister AddRegister(long offset, WordRegister register)
        {
            registers.Add(offset, register);
            return register;
        }

        private readonly IPeripheral parent;
        private readonly IDictionary<long, WordRegister> registers;
    }

    /// <summary>
    /// Byte register collection, allowing to write and read from specified offsets.
    /// </summary>
    public class ByteRegisterCollection : IRegisterCollection
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Core.Structure.Registers.ByteRegisterCollection"/> class.
        /// </summary>
        /// <param name="parent">Parent peripheral (for logging purposes).</param>
        /// <param name="registersMap">Map of register offsets and registers.</param>
        public ByteRegisterCollection(IPeripheral parent, IDictionary<long, ByteRegister> registersMap = null)
        {
            this.parent = parent;
            this.registers = (registersMap != null)
                ? new Dictionary<long, ByteRegister>(registersMap)
                : new Dictionary<long, ByteRegister>();
        }

        /// <summary>
        /// Returns the value of a register in a specified offset. If no such register is found, a logger message is issued.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        public byte Read(long offset)
        {
            byte result;
            if(TryRead(offset, out result))
            {
                return result;
            }
            parent.LogUnhandledRead(offset);
            return 0;
        }

        /// <summary>
        /// Tries to read from a register in a specified offset.
        /// </summary>
        /// <returns><c>true</c>, if register was found, <c>false</c> otherwise.</returns>
        /// <param name="offset">Register offset.</param>
        /// <param name="result">Read value.</param>
        public bool TryRead(long offset, out byte result)
        {
            ByteRegister register;
            if(registers.TryGetValue(offset, out register))
            {
                result = register.Read();
                return true;
            }
            result = 0;
            return false;
        }

        /// <summary>
        /// Writes to a register in a specified offset. If no such register is found, a logger message is issued.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="value">Value to write.</param>
        public void Write(long offset, byte value)
        {
            if(!TryWrite(offset, value))
            {
                parent.LogUnhandledWrite(offset, value);
            }
        }

        /// <summary>
        /// Tries to write to a register in a specified offset.
        /// </summary>
        /// <returns><c>true</c>, if register was found, <c>false</c> otherwise.</returns>
        /// <param name="offset">Register offset.</param>
        /// <param name="value">Value to write.</param>
        public bool TryWrite(long offset, byte value)
        {
            ByteRegister register;
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
        public ByteRegister DefineRegister(long offset, byte resetValue = 0, bool softResettable = true)
        {
            var reg = new ByteRegister(parent, resetValue, softResettable);
            registers.Add(offset, reg);
            return reg;
        }

        /// <summary>
        /// Adds an existing register to the collection.
        /// </summary>
        /// <param name="offset">Register offset.</param>
        /// <param name="register">Register to add.</param>
        /// <returns>Added register (the same passed in <see cref="register"> argument).</returns>
        public ByteRegister AddRegister(long offset, ByteRegister register)
        {
            registers.Add(offset, register);
            return register;
        }

        private readonly IPeripheral parent;
        private readonly IDictionary<long, ByteRegister> registers;
    }

    public interface IRegisterCollection
    {
    }

    public interface IProvidesRegisterCollection<T> where T : IRegisterCollection
    {
        T RegistersCollection { get; }
    }
}
