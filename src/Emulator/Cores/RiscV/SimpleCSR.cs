//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public struct SimpleCSR : IEquatable<SimpleCSR>
    {
        public SimpleCSR(string name, uint number, PrivilegeLevel mode)
        {
            Name = name;
            Number = number;
            Mode = mode;
        }

        public override bool Equals(object obj)
        {
            if(obj is SimpleCSR csr)
            {
                return Equals(csr);
            }
            return false;
        }

        public bool Equals(SimpleCSR csr)
        {
            return Name == csr.Name && Number == csr.Number && Mode == csr.Mode;
        }

        public override int GetHashCode()
        {
            return (int)Mode ^ ((int)Number << 3) ^ Name.GetHashCode();
        }

        public uint Number { get; }
        public PrivilegeLevel Mode { get; }
        public string Name { get; }
    }
}