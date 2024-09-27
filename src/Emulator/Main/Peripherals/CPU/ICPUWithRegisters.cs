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
        void SetRegisterUnsafe(int register, RegisterValue value);
        RegisterValue GetRegisterUnsafe(int register);
        IEnumerable<CPURegister> GetRegisters();
    }

    public static class ICPUWithRegistersExtensions
    {
        // this is to support Python integration
        public static void SetRegisterUlong(this ICPUWithRegisters cpu, int register, ulong value)
        {
            cpu.SetRegister(register, value);
        }

        public static string[,] GetRegistersValues(this ICPUWithRegisters cpu)
        {
            var result = new List<Tuple<string, int, ulong>>();

            foreach(var reg in cpu.GetRegisters().Where(r => r.Aliases != null))
            {
                result.Add(Tuple.Create(reg.ToString(), reg.Index, cpu.GetRegister(reg.Index).RawValue));
            }

            var table = new Table().AddRow(" Name ", " Index ", " Value ");
            table.AddRows(result, x => " {0} ".FormatWith(x.Item1), x => " {0} ".FormatWith(x.Item2), x => " 0x{0:X} ".FormatWith(x.Item3));
            return table.ToArray();
        }
    }
}

