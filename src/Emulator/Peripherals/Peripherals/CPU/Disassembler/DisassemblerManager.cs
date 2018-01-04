//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Utilities;
using System.Reflection;

namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
    public class DisassemblerManager
    {
        static DisassemblerManager()
        {
            Instance = new DisassemblerManager();
        }

        public static DisassemblerManager Instance { get; private set; }

        public string[] GetAvailableDisassemblers(string cpuArchitecture = null)
        {
            var disassemblers = TypeManager.Instance.AutoLoadedTypes.Where(x => x.GetCustomAttribute<DisassemblerAttribute>() != null);
            return disassemblers
                        .Where(x => (cpuArchitecture == null) || 
                                x.GetCustomAttribute<DisassemblerAttribute>().Architectures.Contains(cpuArchitecture))
                        .Select(x => x.GetCustomAttribute<DisassemblerAttribute>().Name).ToArray();
        }

        public IDisassembler CreateDisassembler(string type, IDisassemblable cpu)
        {
            var disassemblerType = TypeManager.Instance.AutoLoadedTypes.Where(x => x.GetCustomAttribute<DisassemblerAttribute>() != null 
                && x.GetCustomAttribute<DisassemblerAttribute>().Name == type
                && x.GetCustomAttribute<DisassemblerAttribute>().Architectures.Contains(cpu.Architecture)).SingleOrDefault();
 
            return disassemblerType == null
                ? null
                : (IDisassembler)disassemblerType.GetConstructor(new [] { typeof(IDisassemblable) }).Invoke(new [] { cpu });
        }

        private DisassemblerManager()
        {
        }
    }
}

