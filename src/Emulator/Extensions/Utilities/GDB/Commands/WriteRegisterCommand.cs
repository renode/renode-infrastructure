//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using System;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Logging;
using ELFSharp.ELF;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class WriteRegisterCommand : Command
    {
        public WriteRegisterCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("P")]
        public PacketData Execute(
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber, Separator = '=')]int registerNumber,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexBytesString)]byte[] value)
        {
            var isLittleEndian = manager.Cpu.Endianness == Endianess.LittleEndian;
            var reg = manager.Cpu.GetRegisters().SingleOrDefault(x => x.Index == registerNumber);
            if(reg.Width == 0)
            {
                manager.Cpu.Log(LogLevel.Warning, "Writing to register #{0} failed, register doesn't exit.", registerNumber);
                return PacketData.ErrorReply();
            }

            // register may have been reported with bigger Width, try to truncate the value
            var width = reg.Width / 8;
            if(value.Length > width)
            {
                // split value into excess and proper value
                var excess = value.Skip(isLittleEndian ? width : 0).Take(value.Length - width);
                value = value.Skip(isLittleEndian ? 0 : value.Length - width).Take(width).ToArray();

                // Allow excess to be filled with zeros and (for two's complement support) 0xff when msb is set.
                // All bytes in excess need to be the same and checked,
                // use sample byte to test value and check if all bytes equal to the sample.
                var sampleByte = excess.First();
                var msb = value[isLittleEndian ? value.Length - 1 : 0] >> 7;
                if(!(sampleByte == 0 || (sampleByte == 0xff && msb == 1)) || excess.Any(b => b != sampleByte))
                {
                    manager.Cpu.Log(LogLevel.Warning, "Writing to register #{0} failed, sent value doesn't fit in {1} bits.", registerNumber, reg.Width);
                    return PacketData.ErrorReply();
                }
            }

            manager.Cpu.SetRegister(registerNumber, reg.ValueFromBytes(value, manager.Cpu.Endianness));
            return PacketData.Success;
        }
    }
}
