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
    public abstract class BasicDoubleWordPeripheral : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public BasicDoubleWordPeripheral(Machine machine)
        {
            this.machine = machine;
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

        protected readonly Machine machine;
    }

    public static class BasicDoubleWordPeripheralExtensions
    {
        public static void Tag32(this IConvertible o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint resetValue = 0, string name = "")
        {
        }

        public static void Define32Many(this IConvertible o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint count, Action<DoubleWordRegister, int> setup, uint stepInBytes = 4, uint resetValue = 0, string name = "")
        {
            DefineMany(o, p, count, setup, stepInBytes, resetValue, name);
        }

        public static void DefineMany(this IConvertible o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint count, Action<DoubleWordRegister, int> setup, uint stepInBytes = 4, uint resetValue = 0, string name = "")
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

        // this method it for easier use in peripherals implementing registers of different width
        public static DoubleWordRegister Define32(this IConvertible o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint resetValue = 0, string name = "")
        {
            return Define(o, p, resetValue, name);
        }

        // this method should be visible for enums only, but... it's not possible in C#
        public static DoubleWordRegister Define(this IConvertible o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, uint resetValue = 0, string name = "")
        {
            if(!o.GetType().IsEnum)
            {
                throw new ArgumentException("This method should be called on enumerated type");
            }

            return p.RegistersCollection.DefineRegister(Convert.ToInt64(o), resetValue);
        }

        public static DoubleWordRegister Bind(this IConvertible o, IProvidesRegisterCollection<DoubleWordRegisterCollection> p, DoubleWordRegister reg)
        {
            if(!o.GetType().IsEnum)
            {
                throw new ArgumentException("This method should be called on enumerated type");
            }

            return p.RegistersCollection.AddRegister(Convert.ToInt64(o), reg);
        }
    }
}
