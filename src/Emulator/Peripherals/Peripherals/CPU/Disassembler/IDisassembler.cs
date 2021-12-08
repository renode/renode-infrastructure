//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using Antmicro.Renode.Peripherals.CPU.Disassembler;
using System.Reflection;
using System.Linq;

namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
    public interface IDisassembler
    {
        bool TryDisassembleInstruction(ulong pc, byte[] memory, uint flags, out DisassemblyResult result, int memoryOffset = 0);
    }

    public struct DisassemblyResult
    {
        public ulong PC { get; set; }
        public int OpcodeSize { get; set; }
        public string OpcodeString { get; set; } 
        public string DisassemblyString { get; set; }

        public override string ToString()
        {
            return $"0x{PC:x8}:   {OpcodeString} {DisassemblyString}";
        }
    }

    public static class IDisassemblerExtensions
    {
        public static int DisassembleBlock(this IDisassembler @this, ulong pc, byte[] memory, uint flags, out string text)
        {
            var sofar = 0;
            var strBldr = new StringBuilder();

            while(sofar < (int)memory.Length)
            {
                if(!@this.TryDisassembleInstruction(pc, memory, flags, out var result, memoryOffset: sofar))
                {
                    strBldr.AppendLine("Disassembly error detected. The rest of the output will be truncated.");
                    break;
                }
                
                if(result.OpcodeSize == 0)
                {
                    strBldr.AppendFormat("0x{0:x8}:  ", pc).AppendLine("No valid instruction, disassembling stopped.");
                    break;
                }
                else
                {
                    strBldr.AppendLine(result.ToString());
                }

                sofar += result.OpcodeSize;
                pc += (ulong)result.OpcodeSize;
            }

            text = strBldr.ToString();
            return sofar;
        }
    }
}

