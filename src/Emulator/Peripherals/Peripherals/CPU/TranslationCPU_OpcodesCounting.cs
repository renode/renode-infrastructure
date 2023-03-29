//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract partial class TranslationCPU
    {
        public bool EnableOpcodesCounting
        {
            set
            {
                TlibEnableOpcodesCounting(value ? 1 : 0u);
            }
        }
        
        public void InstallOpcodeCounterPattern(string name, string pattern)
        {
            if(pattern.Length != 16 && pattern.Length != 32)
            {
                throw new RecoverableException("Currently only 16 and 32-bit opcode patterns are supported");
            }

            // no need to check the result value, as we have checked
            // the pattern length already
            Misc.TryParseBitPattern(pattern, out var opcode, out var mask);

            InstallOpcodeCounterPattern(name, opcode, mask);
        }

        public void InstallOpcodeCounterPattern(string name, ulong opcode, ulong mask)
        {
            if(opcodesMap.ContainsKey(name))
            {
                throw new RecoverableException($"Opcode '{name}' already registered");
            }
            
            var id = TlibInstallOpcodeCounter(opcode, mask);
            if(id == 0)
            {
                throw new RecoverableException("Could not install opcode counter pattern");
            }

            opcodesMap[name] = id;
            this.Log(LogLevel.Debug, "Registered counter for opcode: {0}", name);
        }

        public ulong GetOpcodeCounter(string name)
        {
            if(!opcodesMap.TryGetValue(name, out var id))
            {
                throw new RecoverableException($"Couldn't find the {name} opcode");
            }
            return TlibGetOpcodeCounter(id);
        }

        public string[,] GetAllOpcodesCounters()
        {
            return new Table()
                .AddRow("Opcode", "Count")
                .AddRows(opcodesMap,
                           x => x.Key,
                           x => TlibGetOpcodeCounter(x.Value).ToString()).ToArray();
        }
        
        public void SaveAllOpcodesCounters(string path)
        {
            using(var outputFile = new StreamWriter(path))
            {
                foreach(var x in opcodesMap)
                {
                    outputFile.WriteLine(string.Format("{0};{1}", x.Key, TlibGetOpcodeCounter(x.Value)));
                }
            }
        }

        public void ResetOpcodesCounters()
        {
            TlibResetOpcodeCounters();
        }

        private readonly Dictionary<string, uint> opcodesMap = new Dictionary<string, uint>();
        
        #pragma warning disable 649

        [Import]
        private Action<uint> TlibEnableOpcodesCounting;
        
        [Import]
        private Func<uint, ulong> TlibGetOpcodeCounter;
        
        [Import]
        private Func<ulong, ulong, uint> TlibInstallOpcodeCounter;
        
        [Import]
        private Action TlibResetOpcodeCounters;
        
        #pragma warning restore 649
    }
}

