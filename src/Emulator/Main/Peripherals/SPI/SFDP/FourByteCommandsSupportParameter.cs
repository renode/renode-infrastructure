//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.SPI.SFDP
{
    [LeastSignificantByteFirst]
    public class FourByteCommandsSupportParameter : SFDPParameter
    {
        public static FourByteCommandsSupportParameter DecodeAsSupport4ByteCommandsParameter(IList<byte> data, uint _)
        {
            if(Packet.TryDecode<FourByteCommandsSupportParameter>(data, out var decodedJEDECParameter))
            {
                return decodedJEDECParameter;
            }
            throw new Exception($"'{nameof(FourByteCommandsSupportParameter)}' decoding failed.");
        }

        public static ushort ParameterId = 0xFF84;

        public FourByteCommandsSupportParameter() : base(1, 1)
        {
        }

        public override byte ParameterIdMsb => (byte)(ParameterId >> 8);

        public override byte ParameterIdLsb => (byte)ParameterId;

        // 1st DWORD
        [PacketField, Offset(doubleWords: 0, bits:  0), Width(bits: 1)]
        public bool Support1s1s1sReadCommand; // 13h
        [PacketField, Offset(doubleWords: 0, bits:  1), Width(bits: 1)]
        public bool Support1s1s1sFastReadCommand; // 0Ch
        [PacketField, Offset(doubleWords: 0, bits:  2), Width(bits: 1)]
        public bool Support1s1s2sFastReadCommand; // 3Ch
        [PacketField, Offset(doubleWords: 0, bits:  3), Width(bits: 1)]
        public bool Support1s2s2sFastReadCommand; // BCh
        [PacketField, Offset(doubleWords: 0, bits:  4), Width(bits: 1)]
        public bool Support1s1s4sFastReadCommand; // 6Ch
        [PacketField, Offset(doubleWords: 0, bits:  5), Width(bits: 1)]
        public bool Support1s4s4sFastReadCommand; // ECh
        [PacketField, Offset(doubleWords: 0, bits:  6), Width(bits: 1)]
        public bool Support1s1s1sPageProgramCommand; // 12h
        [PacketField, Offset(doubleWords: 0, bits:  7), Width(bits: 1)]
        public bool Support1s1s4sPageProgramCommand; // 34h
        [PacketField, Offset(doubleWords: 0, bits:  8), Width(bits: 1)]
        public bool Support1s4s4sPageProgramCommand; // 3Eh
        [PacketField, Offset(doubleWords: 0, bits:  9), Width(bits: 1)]
        public bool Support1TypeEraseCommand;
        [PacketField, Offset(doubleWords: 0, bits:  10), Width(bits: 1)]
        public bool Support2TypeEraseCommand;
        [PacketField, Offset(doubleWords: 0, bits:  11), Width(bits: 1)]
        public bool Support3TypeEraseCommand;
        [PacketField, Offset(doubleWords: 0, bits:  12), Width(bits: 1)]
        public bool Support4TypeEraseCommand;
        [PacketField, Offset(doubleWords: 0, bits:  13), Width(bits: 1)]
        public bool Support1s1d1dDTRReadCommand; // OEh
        [PacketField, Offset(doubleWords: 0, bits:  14), Width(bits: 1)]
        public bool Support1s2d2dDTRReadCommand; // BEh
        [PacketField, Offset(doubleWords: 0, bits:  15), Width(bits: 1)]
        public bool Support1s4d4dDTRReadCommand; // EEh
        [PacketField, Offset(doubleWords: 0, bits:  16), Width(bits: 1)]
        public bool SupportVolatileIndividualSectorReadLockCommand; // E0h
        [PacketField, Offset(doubleWords: 0, bits:  17), Width(bits: 1)]
        public bool SupportVolatileIndividualSectorWriteLockCommand; // E1h
        [PacketField, Offset(doubleWords: 0, bits:  18), Width(bits: 1)]
        public bool SupportNonVolatileIndividualSectorReadLockCommand; // E2h
        [PacketField, Offset(doubleWords: 0, bits:  19), Width(bits: 1)]
        public bool SupportNonVolatileIndividualSectorWriteLockCommand; // E3h
        [PacketField, Offset(doubleWords: 0, bits:  20), Width(bits: 1)]
        public bool Support1s1s8sFastReadCommand; // 7Ch
        [PacketField, Offset(doubleWords: 0, bits:  21), Width(bits: 1)]
        public bool Support1s8s8sFastReadCommand; // CCh
        [PacketField, Offset(doubleWords: 0, bits:  22), Width(bits: 1)]
        public bool Support1s8d8dDTRReadCommand; // FDh
        [PacketField, Offset(doubleWords: 0, bits:  23), Width(bits: 1)]
        public bool Support1s1s8sPageProgramCommand; // 84h
        [PacketField, Offset(doubleWords: 0, bits:  24), Width(bits: 1)]
        public bool Support1s8s8sPageProgramCommand; // 8Eh

        // 2nd DWORD
        [PacketField, Offset(doubleWords: 1, bits:  0), Width(bits: 8)]
        public uint EraseType1Cmd; // industry standard: 21h - 4KB 
        [PacketField, Offset(doubleWords: 1, bits:  8), Width(bits: 8)]
        public uint EraseType2Cmd; // industry standard: 5Ch - 32KB
        [PacketField, Offset(doubleWords: 1, bits:  16), Width(bits: 8)]
        public uint EraseType3Cmd; // industry standard: DCh - 64KB
        [PacketField, Offset(doubleWords: 1, bits:  24), Width(bits: 8)]
        public uint EraseType4Cmd; // industry standard: DCh - 256KB

        protected override byte[] ToBytes()
        {
            return Packet.Encode<FourByteCommandsSupportParameter>(this);
        }
    }
}
