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
    public abstract class BasicDoubleWordPeripheral : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public BasicDoubleWordPeripheral(IMachine machine)
        {
            this.machine = machine;
            sysbus = machine.GetSystemBus(this);
            RegistersCollection = new DoubleWordRegisterCollection(this);
        }

        public virtual void Reset()
        {
            RegistersCollection.Reset();
        }

        public virtual uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        protected readonly IMachine machine;
        protected readonly IBusController sysbus;
    }

    public static class BasicDoubleWordPeripheralExtensions
    {
        public static void Tag32(this System.Enum o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint resetValue = 0, string name = "")
        {
        }

        public static void Define32Many(this System.Enum o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint count, Action<DoubleWordRegister, int> setup, uint stepInBytes = 4, uint resetValue = 0, bool softResettable = true, string name = "")
        {
            DefineMany(o, p, count, setup, stepInBytes, resetValue, softResettable, name);
        }

        public static void DefineMany(this System.Enum o, DoubleWordRegisterCollection c, uint count, Action<DoubleWordRegister, int> setup, uint stepInBytes = 4, uint resetValue = 0, bool softResettable = true, string name = "")
        {
            var baseAddress = Convert.ToInt64(o);
            for(var i = 0; i < count; i++)
            {
                var register = c.DefineRegister(baseAddress + i * stepInBytes, resetValue, softResettable);
                setup(register, i);
            }
        }

        public static void DefineMany(this System.Enum o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint count, Action<DoubleWordRegister, int> setup, uint stepInBytes = 4, uint resetValue = 0, bool softResettable = true, string name = "")
        {
            DefineMany(o, p.RegistersCollection, count, setup, stepInBytes, resetValue, softResettable, name);
        }

        // this method it for easier use in peripherals implementing registers of different width
        public static DoubleWordRegister Define32(this System.Enum o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint resetValue = 0, bool softResettable = true, string name = "")
        {
            return Define(o, p, resetValue, softResettable, name);
        }

        public static DoubleWordRegister Define(this System.Enum o, DoubleWordRegisterCollection c, uint resetValue = 0, bool softResettable = true, string name = "")
        {
            return c.DefineRegister(Convert.ToInt64(o), resetValue, softResettable);
        }

        public static DoubleWordRegister Define(this System.Enum o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint resetValue = 0, bool softResettable = true, string name = "")
        {
            return Define(o, p.RegistersCollection, resetValue, softResettable, name);
        }

        public static DoubleWordRegister DefineConditional(this System.Enum o, DoubleWordRegisterCollection c, Func<bool> condition, ushort resetValue = 0, bool softResettable = true, string name = "")
        {
            return c.DefineConditionalRegister(Convert.ToInt64(o), condition, resetValue, softResettable);
        }

        public static DoubleWordRegister DefineConditional(this System.Enum o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, Func<bool> condition, ushort resetValue = 0, bool softResettable = true, string name = "")
        {
            return o.DefineConditional(p.RegistersCollection, condition, resetValue, softResettable);
        }

        public static void DefineManyConditional(this System.Enum o, DoubleWordRegisterCollection c, uint count, Func<int, bool> condition, Action<DoubleWordRegister, int> setup, uint stepInBytes = 1, uint resetValue = 0, string name = "")
        {
            var baseAddress = Convert.ToInt64(o);
            for(var i = 0; i < count; i++)
            {
                var idx = i;
                var register = c.DefineConditionalRegister(baseAddress + i * stepInBytes, () => condition(idx), resetValue);
                setup(register, i);
            }
        }

        public static void DefineManyConditional(this System.Enum o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint count, Func<int, bool> condition, Action<DoubleWordRegister, int> setup, uint stepInBytes = 1, uint resetValue = 0, string name = "")
        {
            o.DefineManyConditional(p.RegistersCollection, count, condition, setup, stepInBytes, resetValue, name);
        }

        public static void DefineManyConditional(this System.Enum o, DoubleWordRegisterCollection c, uint count, Func<bool> condition, Action<DoubleWordRegister, int> setup, uint stepInBytes = 1, uint resetValue = 0, string name = "")
        {
            o.DefineManyConditional(c, count, _ => condition(), setup, stepInBytes, resetValue, name);
        }

        public static void DefineManyConditional(this System.Enum o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint count, Func<bool> condition, Action<DoubleWordRegister, int> setup, uint stepInBytes = 1, uint resetValue = 0, string name = "")
        {
            o.DefineManyConditional(p.RegistersCollection, count, _ => condition(), setup, stepInBytes, resetValue, name);
        }

        public static DoubleWordRegister Bind(this System.Enum o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, DoubleWordRegister reg)
        {
            return p.RegistersCollection.AddRegister(Convert.ToInt64(o), reg);
        }

        public static void BindMany(this System.Enum o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint count, Func<int, DoubleWordRegister> setup, uint stepInBytes = 4)
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
