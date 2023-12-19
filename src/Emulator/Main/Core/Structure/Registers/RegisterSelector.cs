//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Core.Structure.Registers
{
    public sealed class RegisterSelector<T> : IPeripheralRegister<T>
    {
        public RegisterSelector()
        {
            conditionalRegisters = new List<ConditionalRegister>();
        }

        public T Read()
        {
            return GetRegister().Read();
        }

        public void Write(long offset, T value)
        {
            GetRegister().Write(offset, value);
        }

        public void Reset()
        {
            foreach(var c in conditionalRegisters)
            {
                c.Register.Reset();
            }
        }

        public void AddRegister(IPeripheralRegister<T> register, Func<bool> condition)
        {
            conditionalRegisters.Add(new ConditionalRegister(register, condition));
        }

        private IPeripheralRegister<T> GetRegister()
        {
            if(conditionalRegisters.Count == 1)
            {
                return conditionalRegisters[0].Register;
            }
            var result = conditionalRegisters.Where(c => c.Condition()).ToArray();
            if(result.Length > 1)
            {
                throw new CpuAbortException("Encountered multiple registers with valid condition!");
            }
            else if(result.Length == 0)
            {
                throw new CpuAbortException("No register found with valid condition!");
            }
            return result[0].Register;
        }

        private readonly IList<ConditionalRegister> conditionalRegisters;

        private class ConditionalRegister
        {
            public ConditionalRegister(IPeripheralRegister<T> reg, Func<bool> cond)
            {
                Register = reg;
                Condition = cond;
            }

            public IPeripheralRegister<T> Register { get; }
            public Func<bool> Condition { get; } 
        }
    }
}
