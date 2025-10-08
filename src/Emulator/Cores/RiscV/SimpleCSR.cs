﻿//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public struct SimpleCSR : IEquatable<SimpleCSR>
    {
        public SimpleCSR(string name, ushort number, PrivilegeLevel mode)
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

        public ushort Number { get; }

        public PrivilegeLevel Mode { get; }

        public string Name { get; }
    }
}