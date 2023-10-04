//
// Copyright (c) 2010-2023 Antmicro
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

        public static void Define16Many(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, uint count, Action<WordRegister, int> setup, uint stepInBytes = 2, ushort resetValue = 0, string name = "")
        {
            DefineMany(o, p, count, setup, stepInBytes, resetValue, name);
        }

        public static void DefineMany(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, uint count, Action<WordRegister, int> setup, uint stepInBytes = 2, ushort resetValue = 0, string name = "")
        {
            var baseAddress = Convert.ToInt64(o);
            for(var i = 0; i < count; i++)
            {
                var register = p.RegistersCollection.DefineRegister(baseAddress + i * stepInBytes, resetValue);
                setup(register, i);
            }
        }

        public static WordRegister Define16(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, ushort resetValue = 0, string name = "")
        {
            return Define(o, p, resetValue);
        }

        public static WordRegister Define(this System.Enum o, IProvidesRegisterCollection<WordRegisterCollection> p, ushort resetValue = 0, string name = "")
        {
            return p.RegistersCollection.DefineRegister(Convert.ToInt64(o), resetValue);
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
