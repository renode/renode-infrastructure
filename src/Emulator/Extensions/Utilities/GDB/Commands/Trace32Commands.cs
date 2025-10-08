//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

using ELFSharp.ELF;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class Trace32Commands : Command
    {
        public Trace32Commands(CommandsManager manager) : base(manager) { }

        [Execute("Mc14:")]
        public PacketData CP14SetOld(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint trace32Encoding,
            [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint sizeInBytes,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexBytesString)] byte[] valueBytes)
        {
            return HandleAccessingArmSystemRegisters(ArmSystemRegisterEncoding.CoprocessorEnum.CP14, trace32Encoding, sizeInBytes, isRead: false, valueBytes);
        }

        [Execute("Mc15:")]
        public PacketData CP15SetOld(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint trace32Encoding,
            [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint sizeInBytes,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexBytesString)] byte[] valueBytes)
        {
            return HandleAccessingArmSystemRegisters(ArmSystemRegisterEncoding.CoprocessorEnum.CP15, trace32Encoding, sizeInBytes, isRead: false, valueBytes);
        }

        [Execute("mc14:")]
        public PacketData CP14GetOld(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint trace32Encoding,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint sizeInBytes)
        {
            return HandleAccessingArmSystemRegisters(ArmSystemRegisterEncoding.CoprocessorEnum.CP14, trace32Encoding, sizeInBytes, isRead: true);
        }

        [Execute("mc15:")]
        public PacketData CP15GetOld(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint trace32Encoding,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint sizeInBytes)
        {
            return HandleAccessingArmSystemRegisters(ArmSystemRegisterEncoding.CoprocessorEnum.CP15, trace32Encoding, sizeInBytes, isRead: true);
        }

        [Execute("Qtrace32.memory:c")]
        public PacketData AArch32SystemRegisterSet(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.DecimalNumber)] uint coprocessor,
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint trace32Encoding,
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint sizeInBytes,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexBytesString)] byte[] valueBytes)
        {
            if(coprocessor != 14 && coprocessor != 15)
            {
                throw new ArgumentException($"Invalid coprocessor: {coprocessor}");
            }
            return HandleAccessingArmSystemRegisters((ArmSystemRegisterEncoding.CoprocessorEnum)coprocessor, trace32Encoding, sizeInBytes, isRead: false, valueBytes);
        }

        [Execute("qtrace32.memory:c")]
        public PacketData AArch32SystemRegisterGet(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.DecimalNumber)] uint coprocessor,
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint trace32Encoding,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint sizeInBytes)
        {
            if(coprocessor != 14 && coprocessor != 15)
            {
                throw new ArgumentException($"Invalid coprocessor: {coprocessor}");
            }
            return HandleAccessingArmSystemRegisters((ArmSystemRegisterEncoding.CoprocessorEnum)coprocessor, trace32Encoding, sizeInBytes, isRead: true);
        }

        [Execute("Qtrace32.memory:spr,")]
        public PacketData AArch64SystemRegisterSet(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint trace32Encoding,
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint sizeInBytes,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexBytesString)] byte[] valueBytes)
        {
            return HandleAccessingArmSystemRegisters(ArmSystemRegisterEncoding.CoprocessorEnum.AArch64, trace32Encoding, sizeInBytes, isRead: false, valueBytes);
        }

        [Execute("Mspr:")]
        public PacketData AArch64SystemRegisterSetOld(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint trace32Encoding,
            [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint sizeInBytes,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexBytesString)] byte[] valueBytes)
        {
            return HandleAccessingArmSystemRegisters(ArmSystemRegisterEncoding.CoprocessorEnum.AArch64, trace32Encoding, sizeInBytes, isRead: false, valueBytes);
        }

        [Execute("qtrace32.memory:spr,")]
        public PacketData AArch64SystemRegisterGet(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint trace32Encoding,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint sizeInBytes)
        {
            return HandleAccessingArmSystemRegisters(ArmSystemRegisterEncoding.CoprocessorEnum.AArch64, trace32Encoding, sizeInBytes, isRead: true);
        }

        [Execute("mspr:")]
        public PacketData AArch64SystemRegisterGetOld(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint trace32Encoding,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)] uint sizeInBytes)
        {
            return HandleAccessingArmSystemRegisters(ArmSystemRegisterEncoding.CoprocessorEnum.AArch64, trace32Encoding, sizeInBytes, isRead: true);
        }

        private static PacketData HandleReadingArmSystemRegisters(IArmWithSystemRegisters cpu, ArmSystemRegisterEncoding encoding, uint registerSizeInBytes, uint sizeInBytes)
        {
            var registersToRead = sizeInBytes <= registerSizeInBytes ? 1 : sizeInBytes / registerSizeInBytes;
            var valueSize = Math.Min(sizeInBytes, registerSizeInBytes);

            var bytes = new List<byte>((int)(registersToRead * valueSize));
            while(registersToRead > 0)
            {
                if(!cpu.TryGetSystemRegisterValue(encoding, out var value))
                {
                    // The 'TryGet' method will log failure details.
                    return PacketData.ErrorReply(Error.InvalidArgument);
                }
                bytes = bytes.Append(BitHelper.GetBytesFromValue(value, (int)valueSize, reverse: cpu.Endianness == Endianess.LittleEndian)).ToList();

                registersToRead--;
                if(registersToRead >= 1)
                {
                    encoding = encoding.NextInGroup();
                }
            }
            return new PacketData(string.Join("", bytes.Select(x => x.ToString("X2"))));
        }

        private static PacketData HandleWritingArmSystemRegisters(IArmWithSystemRegisters cpu, ArmSystemRegisterEncoding encoding, uint sizeInBytes, byte[] valueBytes)
        {
            var value = BitHelper.ToUInt64(valueBytes, index: 0, length: (int)sizeInBytes, reverse: cpu.Endianness == Endianess.LittleEndian);
            if(!cpu.TrySetSystemRegisterValue(encoding, value))
            {
                // The 'TrySet' method will log failure details.
                return PacketData.ErrorReply(Error.InvalidArgument);
            }
            return PacketData.Success;
        }

        private static bool IsCommandSizeValid(ICPU cpu, uint sizeInBytes, bool multipleAllowed, uint registerSizeInBytes, out PacketData errorReply)
        {
            DebugHelper.Assert(registerSizeInBytes == 4 || registerSizeInBytes == 8);

            // 4-byte accesses are allowed for AArch64 registers.
            if(sizeInBytes == 4 || sizeInBytes == registerSizeInBytes || (multipleAllowed && (sizeInBytes % registerSizeInBytes) == 0))
            {
                errorReply = null;
                return true;
            }
            cpu.Log(LogLevel.Error, "GDBStub: Invalid size: 0x{0:X}", sizeInBytes);
            errorReply = PacketData.ErrorReply(Error.InvalidArgument);
            return false;
        }

        private static bool TryGetArmWithSystemRegistersCPU(ICPU cpu, ExecutionState requiredExecutionState, out IArmWithSystemRegisters armWithSystemRegisters, out PacketData errorReply)
        {
            armWithSystemRegisters = cpu as IArmWithSystemRegisters;
            if(armWithSystemRegisters != null && armWithSystemRegisters.SupportedExecutionStates.Contains(requiredExecutionState))
            {
                errorReply = null;
                return true;
            }
            cpu.Log(LogLevel.Error, $"GDBStub: This CPU doesn't support accessing {requiredExecutionState} system registers.");
            errorReply = PacketData.ErrorReply(Error.OperationNotPermitted);
            return false;
        }

        private PacketData HandleAccessingArmSystemRegisters(ArmSystemRegisterEncoding.CoprocessorEnum coprocessor, uint trace32Encoding, uint sizeInBytes, bool isRead, byte[] valueBytes = null)
        {
            var encoding = ArmSystemRegisterEncodingExtensions.ParseTrace32Encoding(coprocessor, trace32Encoding);
            var registerSizeInBytes = encoding.Width / 8;

            var requiredExecutionState = coprocessor == ArmSystemRegisterEncoding.CoprocessorEnum.AArch64 ? ExecutionState.AArch64 : ExecutionState.AArch32;
            if(!TryGetArmWithSystemRegistersCPU(manager.Cpu, requiredExecutionState, out var cpu, out var errorReply)
               || !IsCommandSizeValid(cpu, sizeInBytes, multipleAllowed: true, registerSizeInBytes, out errorReply))
            {
                return errorReply;
            }

            if(isRead)
            {
                return HandleReadingArmSystemRegisters(cpu, encoding, registerSizeInBytes, sizeInBytes);
            }
            else
            {
                return HandleWritingArmSystemRegisters(cpu, encoding, sizeInBytes, valueBytes);
            }
        }
    }

    internal static class ArmSystemRegisterEncodingExtensions
    {
        internal static ArmSystemRegisterEncoding NextInGroup(this ArmSystemRegisterEncoding encoding)
        {
            var nextRegistersCrn = encoding.Crn;
            var nextRegistersOp2 = (byte)encoding.Op2;

            // Trace32 always increments the encoding to get the next in group.
            switch(encoding.Coprocessor)
            {
            case ArmSystemRegisterEncoding.CoprocessorEnum.AArch64:
                // For AArch64, the last byte of the encoding represents op2, so that is what gets incremented.
                nextRegistersOp2++;
                break;
            case ArmSystemRegisterEncoding.CoprocessorEnum.CP15:
            case ArmSystemRegisterEncoding.CoprocessorEnum.CP14:
                // For AArch32, the last byte of the encoding represents CRn, so that is what gets incremented.
                nextRegistersCrn++;
                break;
            default:
                throw new ArgumentException($"Invalid coprocessor: {encoding.Coprocessor}");
            }
            return new ArmSystemRegisterEncoding(encoding.Coprocessor, encoding.Crm, encoding.Op1, nextRegistersCrn, encoding.Op0, nextRegistersOp2, encoding.Width);
        }

        internal static ArmSystemRegisterEncoding ParseTrace32Encoding(ArmSystemRegisterEncoding.CoprocessorEnum coprocessor, uint trace32Encoding)
        {
            // In Trace32's system register encoding, each hex digit refers to op0, crn... parts of ARM
            // instructions accessing the given register, with instruction-related differences in meaning.
            var nibbles = BitHelper.GetNibbles(trace32Encoding);

            byte crm, op1;
            byte? crn = null, op0 = null, op2 = null;
            var width = 64u;
            if(coprocessor == ArmSystemRegisterEncoding.CoprocessorEnum.AArch64)
            {
                // For example, 0x30040 refers to MSR/MRS instructions with op0=3, op1=0, crn=0, crm=4, op2=0
                // (ID_AA64PFR0_EL1).
                op0 = nibbles.ElementAt(4);
                op1 = nibbles.ElementAt(3);
                crn = nibbles.ElementAt(2);
                crm = nibbles.ElementAt(1);
                op2 = nibbles.ElementAt(0);
            }
            else
            {
                // This can be a 32-bit (MCR/MRC) or 64-bit (MCRR/MRRC) access which is indicated by 0 or 1,
                // respectively, in the fifth nibble (bit16 to be exact). The instructions differ in that
                // 64-bit MCRR/MRRC ones have neither crn nor op2 but Trace32 always passes 0 for them in the
                // same order as for 32-bit accesses so the nibble handling below is the same for both.
                var accessing64BitRegister = nibbles.ElementAt(4) == 1;

                // For example:
                // * 0x412A refers to MCR/MRC with op1=4, crn=A, crm=2 and op2=1 (HMAIR1)
                // * 0x14020 (the MSB=1 is 64-bit flag) refers to MCRR/MRRC with crm=2, op1=4 (HTTBR)
                crm = nibbles.ElementAt(1);
                op1 = nibbles.ElementAt(3);

                if(!accessing64BitRegister)
                {
                    crn = nibbles.ElementAt(0);
                    op2 = nibbles.ElementAt(2);
                    width = 32u;
                }
            }
            return new ArmSystemRegisterEncoding(coprocessor, op0: op0, op1: op1, op2: op2, crm: crm, crn: crn, width: width);
        }
    }
}