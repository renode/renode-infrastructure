//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Text;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithRegisters : ICPU
    {
        void SetRegister(int register, RegisterValue value);
        RegisterValue GetRegister(int register);
        IEnumerable<CPURegister> GetRegisters();
    }

    public static class ICPUWithRegistersExtensions
    {
        // this is to support Python integration
        public static ulong GetRegisterUlong(this ICPUWithRegisters cpu, int register)
        {
            return cpu.GetRegister(register).RawValue;
        }

        public static ulong GetRegisterUlong(this ICPUWithRegisters cpu, string register)
        {
            return cpu.GetRegister(register).RawValue;
        }

        public static void SetRegisterUlong(this ICPUWithRegisters cpu, int register, ulong value)
        {
            cpu.SetRegister(register, value);
        }

        public static void SetRegisterUlong(this ICPUWithRegisters cpu, string register, ulong value)
        {
            cpu.SetRegister(register, value);
        }

        public static void SetRegister(this ICPUWithRegisters cpu, string register, RegisterValue value)
        {
            // for dynamically added registers (e.g., V ones for RISC-V) it's possible for `Alias` to be null;
            // this needs to be investigated, but for now let's just filter such situations out
            var reg = cpu.GetRegisters().FirstOrDefault(x => x.Aliases != null && x.Aliases.Contains(register));
            // `reg` is a struct so it'll never be `null`
            if(reg.Aliases == null)
            {
                throw new RecoverableException($"Wrong register name: {register}");
            }
            cpu.SetRegister(reg.Index, value);
        }

        public static RegisterValue GetRegister(this ICPUWithRegisters cpu, string register)
        {
            // for dynamically added registers (e.g., V ones for RISC-V) it's possible for `Alias` to be null;
            // this needs to be investigated, but for now let's just filter such situations out
            var reg = cpu.GetRegisters().FirstOrDefault(x => x.Aliases != null && x.Aliases.Contains(register));
            // `reg` is a struct, so it'll never be `null`
            if(reg.Aliases == null)
            {
                throw new RecoverableException($"Wrong register name: {register}");
            }
            return cpu.GetRegister(reg.Index);
        }

        public static string[,] GetRegistersValues(this ICPUWithRegisters cpu)
        {
            var result = new List<Tuple<string, int, ulong>>();

            foreach(var reg in cpu.GetRegisters()/*.Where(r => r.Aliases != null)*/)
            {
                result.Add(Tuple.Create(reg.ToString(), reg.Index, cpu.GetRegister(reg.Index).RawValue));
            }

            var table = new Table().AddRow(" Name ", " Index ", " Value ");
            table.AddRows(result, x => " {0} ".FormatWith(x.Item1), x => " {0} ".FormatWith(x.Item2), x => " 0x{0:X} ".FormatWith(x.Item3));
            return table.ToArray();
        }
    }
}

