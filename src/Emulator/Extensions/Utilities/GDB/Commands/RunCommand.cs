//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Globalization;
using System.Linq;
using System.Text;

using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class RunCommand : Command
    {
        public RunCommand(CommandsManager manager) : base(manager)
        {
        }

        // This packet is only available in extended mode (see https://sourceware.org/gdb/current/onlinedocs/gdb.html/Packets.html#extended-mode)
        // Use `target extended-remote` instead of `target remote`
        [Execute("vRun;")]
        public PacketData Run(
        [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.String)] string args)
        {
            var decodedArgs = args.Split(';')
                .Select(s => Encoding.UTF8.GetString(
                    s.Split(2).Select(x => byte.Parse(x, NumberStyles.HexNumber)).ToArray()))
                .ToArray();

            // filename is passed by the client via the `set remote exec-file <file>` command
            var filename = decodedArgs.FirstOrDefault() ?? "";
            var argsString = string.Join(" ", decodedArgs.Skip(1));

            var semihostingHandler = manager.Machine.GetPeripheralsOfType<SemihostingHandler>().FirstOrDefault(handler => handler.AttachedToCPU == manager.Cpu.GetName());
            if(semihostingHandler != null)
            {
                if(!string.IsNullOrEmpty(filename))
                {
                    semihostingHandler.ProgramName = filename;
                    manager.Cpu.InfoLog("Setting program name \"{0}\" in SemihostingHandler", filename);
                }
                semihostingHandler.ProgramArguments = argsString;
                manager.Cpu.InfoLog("Setting program arguments \"{0}\" in SemihostingHandler", argsString);
            }

            using(manager.Machine.ObtainPausedState(true))
            {
                manager.Machine.Reset();
                if(!string.IsNullOrEmpty(filename))
                {
                    manager.Machine.SystemBus.LoadELF(filename);
                }
                manager.Cpu.ExecutionMode = ExecutionMode.SingleStep; // Wait for gdb at the first instruction
            }
            manager.Machine.Start();
            return null;
        }
    }
}
