//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Core.Structure.Registers
{
    /// <summary>
    /// Double word register collection, allowing to write and read from specified offsets.
    /// </summary>
    public class DoubleWordRegisterCollection
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Core.Structure.Registers.DoubleWordRegisterCollection"/> class.
        /// </summary>
        /// <param name="parent">Parent peripheral (for logging purposes).</param>
        /// <param name="registersMap">Map of register offsets and registers.</param>
        public DoubleWordRegisterCollection(IPeripheral parent, IDictionary<long, DoubleWordRegister> registersMap)
        {
            this.parent = parent;
            this.registers = new Dictionary<long, DoubleWordRegister>(registersMap);
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

        private readonly IPeripheral parent;
        private readonly IDictionary<long, DoubleWordRegister> registers;
    }

    /// <summary>
    /// Word register collection, allowing to write and read from specified offsets.
    /// </summary>
    public class WordRegisterCollection
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Core.Structure.Registers.WordRegisterCollection"/> class.
        /// </summary>
        /// <param name="parent">Parent peripheral (for logging purposes).</param>
        /// <param name="registersMap">Map of register offsets and registers.</param>
        public WordRegisterCollection(IPeripheral parent, IDictionary<long, WordRegister> registersMap)
        {
            this.parent = parent;
            this.registers = new Dictionary<long, WordRegister>(registersMap);
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
                parent.LogUnhandledRead(offset);
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

        private readonly IPeripheral parent;
        private readonly IDictionary<long, WordRegister> registers;
    }

    /// <summary>
    /// Byte register collection, allowing to write and read from specified offsets.
    /// </summary>
    public class ByteRegisterCollection
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Core.Structure.Registers.ByteRegisterCollection"/> class.
        /// </summary>
        /// <param name="parent">Parent peripheral (for logging purposes).</param>
        /// <param name="registersMap">Map of register offsets and registers.</param>
        public ByteRegisterCollection(IPeripheral parent, IDictionary<long, ByteRegister> registersMap)
        {
            this.parent = parent;
            this.registers = new Dictionary<long, ByteRegister>(registersMap);
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
                parent.LogUnhandledRead(offset);
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

        private readonly IPeripheral parent;
        private readonly IDictionary<long, ByteRegister> registers;
    }
}
