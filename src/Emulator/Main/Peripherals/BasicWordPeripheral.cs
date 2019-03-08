//
// Copyright (c) 2010-2018 Antmicro
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
        public BasicWordPeripheral(Machine machine)
        {
            this.machine = machine;
            RegistersCollection = new WordRegisterCollection(this);
            DefineRegisters();
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

        protected abstract void DefineRegisters();

        protected readonly Machine machine;
    }

    public static class BasicWordPeripheralExtensions
    {
        public static void Tag16(this IConvertible o, IProvidesRegisterCollection<WordRegisterCollection> p, ushort resetValue = 0, string name = "")
        {
        }

        public static void Define16Many(this IConvertible o, IProvidesRegisterCollection<WordRegisterCollection> p, uint count, Action<WordRegister, int> setup, uint stepInBytes = 2, ushort resetValue = 0, string name = "")
        {
            DefineMany(o, p, count, setup, stepInBytes, resetValue, name);
        }

        public static void DefineMany(this IConvertible o, IProvidesRegisterCollection<WordRegisterCollection> p, uint count, Action<WordRegister, int> setup, uint stepInBytes = 2, ushort resetValue = 0, string name = "")
        {
            if(!o.GetType().IsEnum)
            {
                throw new ArgumentException("This method should be called on enumerated type");
            }

            var baseAddress = Convert.ToInt64(o);
            for(var i = 0; i < count; i++)
            {
                var register = p.RegistersCollection.DefineRegister(baseAddress + i * stepInBytes, resetValue);
                setup(register, i);
            }
        }

        public static WordRegister Define16(this IConvertible o, IProvidesRegisterCollection<WordRegisterCollection> p, ushort resetValue = 0, string name = "")
        {
            return Define(o, p, resetValue);
        }

        // this method should be visible for enums only, but... it's not possible in C#
        public static WordRegister Define(this IConvertible o, IProvidesRegisterCollection<WordRegisterCollection> p, ushort resetValue = 0, string name = "")
        {
            if(!o.GetType().IsEnum)
            {
                throw new ArgumentException("This method should be called on enumerated type");
            }

            return p.RegistersCollection.DefineRegister(Convert.ToInt64(o), resetValue);
        }

        public static WordRegister Bind(this IConvertible o, IProvidesRegisterCollection<WordRegisterCollection> p, WordRegister reg, string name = "")
        {
            if(!o.GetType().IsEnum)
            {
                throw new ArgumentException("This method should be called on enumerated type");
            }

            return p.RegistersCollection.AddRegister(Convert.ToInt64(o), reg);
        }
    }
}
