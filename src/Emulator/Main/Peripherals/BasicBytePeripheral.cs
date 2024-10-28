//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals
{
    public abstract class BasicBytePeripheral : IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public BasicBytePeripheral(IMachine machine)
        {
            this.machine = machine;
            sysbus = machine.GetSystemBus(this);
            RegistersCollection = new ByteRegisterCollection(this);
        }

        public virtual void Reset()
        {
            RegistersCollection.Reset();
        }

        public virtual byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public virtual void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public ByteRegisterCollection RegistersCollection { get; private set; }

        protected abstract void DefineRegisters();

        protected readonly IMachine machine;
        protected readonly IBusController sysbus;
    }

    public static class BasicBytePeripheralExtensions
    {
        public static void Tag8(this System.Enum o, IProvidesRegisterCollection<ByteRegisterCollection> p, byte resetValue = 0, string name = "")
        {
        }

        public static void Define8Many(this System.Enum o, IProvidesRegisterCollection<ByteRegisterCollection> p, uint count, Action<ByteRegister, int> setup, uint stepInBytes = 1, byte resetValue = 0, bool softResettable = true, string name = "")
        {
            DefineMany(o, p, count, setup, stepInBytes, resetValue, softResettable, name);
        }

        public static void DefineMany(this System.Enum o, ByteRegisterCollection c, uint count, Action<ByteRegister, int> setup, uint stepInBytes = 1, byte resetValue = 0, bool softResettable = true, string name = "")
        {
            var baseAddress = Convert.ToInt64(o);
            for(var i = 0; i < count; i++)
            {
                var register = c.DefineRegister(baseAddress + i * stepInBytes, resetValue, softResettable);
                setup(register, i);
            }
        }

        public static void DefineMany(this System.Enum o, IProvidesRegisterCollection<ByteRegisterCollection> p, uint count, Action<ByteRegister, int> setup, uint stepInBytes = 1, byte resetValue = 0, bool softResettable = true, string name = "")
        {
            DefineMany(o, p.RegistersCollection, count, setup, stepInBytes, resetValue, softResettable, name);
        }

        public static ByteRegister Define8(this System.Enum o, IProvidesRegisterCollection<ByteRegisterCollection> p, byte resetValue = 0, bool softResettable = true, string name = "")
        {
            return Define(o, p, resetValue, softResettable);
        }

        public static ByteRegister Define(this System.Enum o, IProvidesRegisterCollection<ByteRegisterCollection> p, byte resetValue = 0, bool softResettable = true, string name = "")
        {
            return Define(o, p.RegistersCollection, resetValue, softResettable, name);
        }

        public static ByteRegister Define(this System.Enum o, ByteRegisterCollection c, byte resetValue = 0, bool softResettable = true, string name = "")
        {
            return c.DefineRegister(Convert.ToInt64(o), resetValue, softResettable);
        }

        public static ByteRegister DefineConditional(this System.Enum o, ByteRegisterCollection c, Func<bool> condition, byte resetValue = 0, bool softResettable = true, string name = "")
        {
            return c.DefineConditionalRegister(Convert.ToInt64(o), condition, resetValue, softResettable);
        }

        public static ByteRegister DefineConditional(this System.Enum o, IProvidesRegisterCollection<ByteRegisterCollection> p, Func<bool> condition, byte resetValue = 0, bool softResettable = true, string name = "")
        {
            return o.DefineConditional(p.RegistersCollection, condition, resetValue, softResettable);
        }

        public static void DefineManyConditional(this System.Enum o, ByteRegisterCollection c, uint count, Func<int, bool> condition, Action<ByteRegister, int> setup, uint stepInBytes = 1, byte resetValue = 0, string name = "")
        {
            var baseAddress = Convert.ToInt64(o);
            for(var i = 0; i < count; i++)
            {
                var idx = i;
                var register = c.DefineConditionalRegister(baseAddress + i * stepInBytes, () => condition(idx), resetValue);
                setup(register, i);
            }
        }

        public static void DefineManyConditional(this System.Enum o, IProvidesRegisterCollection<ByteRegisterCollection> p, uint count, Func<int, bool> condition, Action<ByteRegister, int> setup, uint stepInBytes = 1, byte resetValue = 0, string name = "")
        {
            o.DefineManyConditional(p.RegistersCollection, count, condition, setup, stepInBytes, resetValue, name);
        }

        public static void DefineManyConditional(this System.Enum o, ByteRegisterCollection c, uint count, Func<bool> condition, Action<ByteRegister, int> setup, uint stepInBytes = 1, byte resetValue = 0, string name = "")
        {
            o.DefineManyConditional(c, count, _ => condition(), setup, stepInBytes, resetValue, name);
        }

        public static void DefineManyConditional(this System.Enum o, IProvidesRegisterCollection<ByteRegisterCollection> p, uint count, Func<bool> condition, Action<ByteRegister, int> setup, uint stepInBytes = 1, byte resetValue = 0, string name = "")
        {
            o.DefineManyConditional(p.RegistersCollection, count, _ => condition(), setup, stepInBytes, resetValue, name);
        }

        public static ByteRegister Bind(this System.Enum o, IProvidesRegisterCollection<ByteRegisterCollection> p, ByteRegister reg, string name = "")
        {
            return p.RegistersCollection.AddRegister(Convert.ToInt64(o), reg);
        }

        public static void BindMany(this System.Enum o, IProvidesRegisterCollection<ByteRegisterCollection> p, uint count, Func<int, ByteRegister> setup, uint stepInBytes = 4)
        {
            var baseAddress = Convert.ToInt64(o);
            for(var i = 0; i < count; i++)
            {
                var register = setup(i);
                p.RegistersCollection.AddRegister(baseAddress + i * stepInBytes, register);
            }
        }
    }
}
