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
    public abstract class BasicWordPeripheral : IWordPeripheral, IProvidesRegisterCollection<WordRegisterCollection>
    {
        public BasicWordPeripheral(IMachine machine)
        {
            this.machine = machine;
            sysbus = machine.GetSystemBus(this);
            RegistersCollection = new WordRegisterCollection(this);
        }

        public virtual void Reset()
        {
            RegistersCollection.Reset();
        }

        public virtual ushort ReadWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public virtual void WriteWord(long offset, ushort value)
        {
            RegistersCollection.Write(offset, value);
        }

        public WordRegisterCollection RegistersCollection { get; private set; }

        protected readonly IMachine machine;
        protected readonly IBusController sysbus;
    }

    public static class BasicWordPeripheralExtensions
    {
        public static void Tag16(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, ushort resetValue = 0, string name = "")
        {
        }

        public static void Define16Many(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, uint count, Action<WordRegister, int> setup, uint stepInBytes = 2, ushort resetValue = 0, bool softResettable = true, string name = "")
        {
            DefineMany(o, p, count, setup, stepInBytes, resetValue, softResettable, name);
        }

        public static void DefineMany(this System.Enum o, WordRegisterCollection c, uint count, Action<WordRegister, int> setup, uint stepInBytes = 2, ushort resetValue = 0, bool softResettable = true, string name = "")
        {
            var baseAddress = Convert.ToInt64(o);
            for(var i = 0; i < count; i++)
            {
                var register = c.DefineRegister(baseAddress + i * stepInBytes, resetValue, softResettable);
                setup(register, i);
            }
        }

        public static void DefineMany(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, uint count, Action<WordRegister, int> setup, uint stepInBytes = 2, ushort resetValue = 0, bool softResettable = true, string name = "")
        {
            o.DefineMany(p.RegistersCollection, count, setup, stepInBytes, resetValue, softResettable, name);
        }

        public static WordRegister Define16(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, ushort resetValue = 0, bool softResettable = true, string name = "")
        {
            return Define(o, p, resetValue, softResettable);
        }

        public static WordRegister Define(this System.Enum o, WordRegisterCollection c, ushort resetValue = 0, bool softResettable = true, string name = "")
        {
            return c.DefineRegister(Convert.ToInt64(o), resetValue, softResettable);
        }

        public static WordRegister Define(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, ushort resetValue = 0, bool softResettable = true, string name = "")
        {
            return p.RegistersCollection.DefineRegister(Convert.ToInt64(o), resetValue, softResettable);
        }

        public static WordRegister DefineConditional(this System.Enum o, WordRegisterCollection c, Func<bool> condition, ushort resetValue = 0, bool softResettable = true, string name = "")
        {
            return c.DefineConditionalRegister(Convert.ToInt64(o), condition, resetValue, softResettable);
        }

        public static WordRegister DefineConditional(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, Func<bool> condition, ushort resetValue = 0, bool softResettable = true, string name = "")
        {
            return o.DefineConditional(p.RegistersCollection, condition, resetValue, softResettable);
        }

        public static void DefineManyConditional(this System.Enum o, WordRegisterCollection c, uint count, Func<int, bool> condition, Action<WordRegister, int> setup, uint stepInBytes = 1, ushort resetValue = 0, string name = "")
        {
            var baseAddress = Convert.ToInt64(o);
            for(var i = 0; i < count; i++)
            {
                var idx = i;
                var register = c.DefineConditionalRegister(baseAddress + i * stepInBytes, () => condition(idx), resetValue);
                setup(register, i);
            }
        }

        public static void DefineManyConditional(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, uint count, Func<int, bool> condition, Action<WordRegister, int> setup, uint stepInBytes = 1, ushort resetValue = 0, string name = "")
        {
            o.DefineManyConditional(p.RegistersCollection, count, condition, setup, stepInBytes, resetValue, name);
        }

        public static void DefineManyConditional(this System.Enum o, WordRegisterCollection c, uint count, Func<bool> condition, Action<WordRegister, int> setup, uint stepInBytes = 1, ushort resetValue = 0, string name = "")
        {
            o.DefineManyConditional(c, count, _ => condition(), setup, stepInBytes, resetValue, name);
        }

        public static void DefineManyConditional(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, uint count, Func<bool> condition, Action<WordRegister, int> setup, uint stepInBytes = 1, ushort resetValue = 0, string name = "")
        {
            o.DefineManyConditional(p.RegistersCollection, count, _ => condition(), setup, stepInBytes, resetValue, name);
        }

        public static WordRegister Bind(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, WordRegister reg, string name = "")
        {
            return p.RegistersCollection.AddRegister(Convert.ToInt64(o), reg);
        }

        public static void BindMany(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, uint count, Func<int, WordRegister> setup, uint stepInBytes = 4)
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
