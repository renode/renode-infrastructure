//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public struct NonstandardCSR
    {
        public NonstandardCSR(Func<ulong> readOperation, Action<ulong> writeOperation, string name) : this()
        {
            this.ReadOperation = readOperation;
            this.WriteOperation = writeOperation;
            this.Name = name;
        }

        public Func<ulong> ReadOperation { get; }
        public Action<ulong> WriteOperation { get; }
        public string Name { get; }
    }
}
