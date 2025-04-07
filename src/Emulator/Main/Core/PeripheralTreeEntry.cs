//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core
{
    public sealed class PeripheralTreeEntry
    {
        public PeripheralTreeEntry(IPeripheral peripheral, IPeripheral parent, Type type, IRegistrationPoint registrationPoint, string name, int level)
        {
            this.type = type;
            Name = name;
            RegistrationPoint = registrationPoint;
            Peripheral = peripheral;
            Parent = parent;
            Level = level;
        }

        public override string ToString()
        {
            return string.Format("[PeripheralTreeEntry: Type={0}, Peripheral={1}, Parent={2}, Name={3}, Level={4}, RegistrationPoint={5}]",
                Type, Peripheral.GetType(), Parent == null ? "(none)" : Parent.GetType().ToString(), Name, Level, RegistrationPoint);
        }

        public void Reparent(PeripheralTreeEntry entry)
        {
            Parent = entry.Parent;
            Level = entry.Level + 1;
        }

        public Type Type
        {
            get
            {
                return type;
            }
        }

        public IPeripheral Peripheral { get; private set; }

        public IPeripheral Parent { get; private set; }

        public string Name { get; private set; }

        public int Level { get; private set; }

        public IRegistrationPoint RegistrationPoint { get; private set; }

        [PreSerialization]
        private void SaveType()
        {
            typeName = type.FullName;
        }

        [PostDeserialization]
        private void RecoverType()
        {
            type = TypeManager.Instance.GetTypeByName(typeName);
            typeName = null;
        }

        [Transient]
        private Type type;
        private string typeName;
    }
}