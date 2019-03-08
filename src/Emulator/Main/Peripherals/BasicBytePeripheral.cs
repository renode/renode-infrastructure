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
    public abstract class BasicBytePeripheral : IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public BasicBytePeripheral(Machine machine)
        {
            this.machine = machine;
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
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

        protected readonly Machine machine;
    }

    public static class BasicBytePeripheralExtensions
    {
        public static void Tag8(this IConvertible o, IProvidesRegisterCollection<ByteRegisterCollection> p, byte resetValue = 0, string name = "")
        {
        }

        public static void Define8Many(this IConvertible o, IProvidesRegisterCollection<ByteRegisterCollection> p, uint count, Action<ByteRegister, int> setup, uint stepInBytes = 1, byte resetValue = 0, string name = "")
        {
            DefineMany(o, p, count, setup, stepInBytes, resetValue, name);
        }

        public static void DefineMany(this IConvertible o, IProvidesRegisterCollection<ByteRegisterCollection> p, uint count, Action<ByteRegister, int> setup, uint stepInBytes = 1, byte resetValue = 0, string name = "")
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

        public static ByteRegister Define8(this IConvertible o, IProvidesRegisterCollection<ByteRegisterCollection> p, byte resetValue = 0, string name = "")
        {
            return Define(o, p, resetValue);
        }

        // this method should be visible for enums only, but... it's not possible in C#
        public static ByteRegister Define(this IConvertible o, IProvidesRegisterCollection<ByteRegisterCollection> p, byte resetValue = 0, string name = "")
        {
            if(!o.GetType().IsEnum)
            {
                throw new ArgumentException("This method should be called on enumerated type");
            }

            return p.RegistersCollection.DefineRegister(Convert.ToInt64(o), resetValue);
        }

        public static ByteRegister Bind(this IConvertible o, IProvidesRegisterCollection<ByteRegisterCollection> p, ByteRegister reg, string name = "")
        {
            if(!o.GetType().IsEnum)
            {
                throw new ArgumentException("This method should be called on enumerated type");
            }

            return p.RegistersCollection.AddRegister(Convert.ToInt64(o), reg);
        }
    }
}
