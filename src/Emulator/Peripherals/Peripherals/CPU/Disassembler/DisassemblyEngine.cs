//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;
using System.IO;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
    public class DisassemblyEngine
    {
        public DisassemblyEngine(IDisassemblable disasm, Func<ulong, ulong> addressTranslator)
        {
            this.cpu = disasm;
            this.AddressTranslator = addressTranslator;
        }

        public void LogSymbol(ulong pc, uint size, uint flags)
        {
            if (disassembler == null || LogFile == null)
            {
                return;
            }

            using (var file = File.AppendText(LogFile))
            {
                var phy = AddressTranslator(pc);
                var symbol = cpu.Bus.FindSymbolAt(pc);
                var disas = Disassemble(pc, phy, size, flags);

                if (disas == null)
                {
                    return;
                }

                file.WriteLine("-------------------------");
                if (size > 0)
                {
                    file.Write("IN: {0} ", symbol ?? string.Empty);
                    if(phy != pc)
                    {
                        file.WriteLine("(physical: 0x{0:x8}, virtual: 0x{1:x8})", phy, pc);
                    }
                    else
                    {
                        file.WriteLine("(address: 0x{0:x8})", phy);
                    }
                }
                else
                {
                    // special case when disassembling magic addresses in Cortex-M
                    file.WriteLine("Magic PC value detected: 0x{0:x8}", flags > 0 ? pc | 1 : pc);
                }

                file.WriteLine(string.IsNullOrWhiteSpace(disas) ? string.Format("Cannot disassemble from 0x{0:x8} to 0x{1:x8}", pc, pc + size)  : disas);
                file.WriteLine(string.Empty);
            }
        }

        public void LogDisassembly(IntPtr memory, uint size)
        {
            if (disassembler == null || LogFile == null)
            {
                return;
            }

            using (var file = File.AppendText(LogFile))
            {
                var disassembled = Disassemble(memory, size);
                if (disassembled != null)
                {
                    file.WriteLine(disassembled);
                }
            }
        }

        public string Disassemble(IntPtr memory, uint size, ulong pc = 0, uint flags = 0)
        {
            if (disassembler == null)
            {
                return null;
            }

            // let's assume that we have 2 byte processor commands and each is disassembled to 160 characters
            // sometimes it happens that size is equal to 0 (e.g. addresses like 0xfffffffd) - in such case wee need to add 1 to make it work
            var outputLength = (size + 1) * 160;
            var outputPtr = Marshal.AllocHGlobal((int)outputLength);
            int result = disassembler.Disassemble(pc, memory, size, flags, outputPtr, outputLength);
            var lines = Marshal.PtrToStringAnsi(outputPtr) ?? string.Empty;
            Marshal.FreeHGlobal(outputPtr);

            return result == 0 ? null : lines;
        }

        public string LogFile
        {
            get { return logFile; }
            set
            {
                if(value != null && disassembler == null)
                {
                    throw new RecoverableException(string.Format("Could not set log file: {0} as there is no selected disassembler.", value));
                }

                logFile = value;
                cpu.LogTranslatedBlocks = (value != null);

                if (logFile != null && File.Exists(logFile))
                {
                    // truncate the file if it already exists
                    File.WriteAllText(logFile, string.Empty);
                }
            }
        }

        public string Disassemble(ulong addr, bool isPhysical, uint size, uint flags)
        {
            var physical = isPhysical ? addr : AddressTranslator(addr);
            /*if (physical == 0xffffffff)
            {
                this.Log(LogLevel.Warning, "Couldn't disassemble address 0x{0:x8}", addr);
                return string.Empty;
            }*/

            return Disassemble(addr, physical, size, flags);
        }

        public bool SetDisassembler(IDisassembler dis)
        {
            disassembler = dis;
            return true;
        }

        public string CurrentDisassemblerType { get { return disassembler == null ? string.Empty : disassembler.Name; } }

        private string Disassemble(ulong pc, ulong physical, uint size, uint flags)
        {
            var tabPtr = Marshal.AllocHGlobal((int)size);
            var tab = cpu.Bus.ReadBytes(physical, (int)size, true);
            Marshal.Copy(tab, 0, tabPtr, (int)size);

            var result = Disassemble(tabPtr, size, pc, flags);
            Marshal.FreeHGlobal(tabPtr);
            return result;
        }

        private IDisassembler disassembler;
        protected readonly IDisassemblable cpu;
        private string logFile;
        private Func<ulong, ulong> AddressTranslator;
    }
}
