//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using ELFSharp.ELF;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class Trace32Commands : Command
    {
        public Trace32Commands(CommandsManager manager) : base(manager)
        {
        }

        [Execute("mspr:")]  // get AArch64 system register
        public PacketData Execute(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]uint trace32Encoding,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]int sizeInBytes)
        {
            if(!(manager.Cpu is ICPUWithAArch64Support cpu))
            {
                manager.Cpu.Log(LogLevel.Error, "GDBStub: This CPU doesn't support getting AArch64 system registers.");
                return PacketData.ErrorReply(Error.OperationNotPermitted);
            }

            // Multiples of 8 can be used to read registers with incrementing encodings.
            if(sizeInBytes != 4 && (sizeInBytes % 8) != 0)
            {
                manager.Cpu.Log(LogLevel.Error, "GDBStub: Invalid size: 0x{0:X}", sizeInBytes);
                return PacketData.ErrorReply(Error.InvalidArgument);
            }
            var registersToRead = sizeInBytes <= 8 ? 1 : sizeInBytes / 8;
            var valueSize = sizeInBytes <= 8 ? sizeInBytes : 8;

            var bytes = new List<byte>(registersToRead * valueSize);
            for(var encoding = CreateAArch64Encoding(trace32Encoding); registersToRead > 0; registersToRead--, encoding.Op2++)
            {
                if(!cpu.TryGetSystemRegisterValue(encoding, out var value))
                {
                    // The 'TryGet' method will log failure details.
                    return PacketData.ErrorReply(Error.InvalidArgument);
                }
                bytes = bytes.Append(BitHelper.GetBytesFromValue(value, valueSize, reverse: cpu.Endianness == Endianess.LittleEndian)).ToList();
            }
            return new PacketData(string.Join("", bytes.Select(x => x.ToString("X2"))));
        }

        [Execute("Mspr:")]  // set AArch64 system register
        public PacketData Execute(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]uint trace32Encoding,
            [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.DecimalNumber)]int sizeInBytes,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexBytesString)]byte[] valueBytes)
        {
            if(!(manager.Cpu is ICPUWithAArch64Support cpu))
            {
                manager.Cpu.Log(LogLevel.Error, "GDBStub: This CPU doesn't support setting AArch64 system registers.");
                return PacketData.ErrorReply(Error.OperationNotPermitted);
            }

            if(sizeInBytes != 4 && sizeInBytes != 8)
            {
                manager.Cpu.Log(LogLevel.Error, "GDBStub: Invalid size: 0x{0:X}", sizeInBytes);
                return PacketData.ErrorReply(Error.InvalidArgument);
            }

            var value = BitHelper.ToUInt64(valueBytes, index: 0, length: sizeInBytes, reverse: cpu.Endianness == Endianess.LittleEndian);
            if(!cpu.TrySetSystemRegisterValue(CreateAArch64Encoding(trace32Encoding), value))
            {
                // The 'TrySet' method will log failure details.
                return PacketData.ErrorReply(Error.InvalidArgument);
            }
            return PacketData.Success;
        }

        private static AArch64SystemRegisterEncoding CreateAArch64Encoding(uint trace32Encoding)
        {
            // In Trace32's system register encoding, each hex digit refers to op0, op1... values from MRS/MSR instructions accessing
            // the given register. For example, 0x30040 refers to op0=3, op1=0, crn=0, crm=4, op2=0 (ID_AA64PFR0_EL1).
            var nibbles = BitHelper.GetNibbles(trace32Encoding).Reverse().TakeLast(5);

            var op0 = nibbles.ElementAt(0);
            var op1 = nibbles.ElementAt(1);
            var crn = nibbles.ElementAt(2);
            var crm = nibbles.ElementAt(3);
            var op2 = nibbles.ElementAt(4);
            return new AArch64SystemRegisterEncoding(op0, op1, crn, crm, op2);
        }
    }
}
