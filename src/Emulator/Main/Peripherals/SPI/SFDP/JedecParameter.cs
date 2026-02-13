//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Reflection;

using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.SPI.SFDP
{
    public class JedecParameter : SFDPParameter
    {
        public static JedecParameter DecodeAsJEDECParameter(IList<byte> data, uint dWSize)
        {
            var target = new JedecParameterPacket { DWSize = dWSize };
            if(Packet.TryDecodeInto<JedecParameterPacket>(data, ref target))
            {
                return new JedecParameter(target);
            }
            throw new Exception("Jedec Parameter decoding failed.");
        }

        public static ushort ParameterId = 0xFF00;

        public JedecParameter() : base(1, 6) { }

        public JedecParameter(int eraseSize, long flashDensity, uint pageSize, byte eraseCode, byte major = 1, byte minor = 0x6)
            : base(major, minor)
        {
            jedecParameterPacket = new JedecParameterPacket();
            EraseSize = eraseSize;
            EraseCode = eraseCode;
            FlashDensity = flashDensity;
            PageSize = pageSize;
        }

        public bool SupportsRead1s8s8s(out uint cmd)
        {
            if(jedecParameterPacket.Read1s8s8sCode != null && jedecParameterPacket.Read1s8s8sCode != 0)
            {
                cmd = jedecParameterPacket.Read1s8s8sCode.Value;
                return true;
            }
            cmd = default;
            return false;
        }

        public bool SupportsRead1s1s8s(out uint cmd)
        {
            if(jedecParameterPacket.Read1s1s8sCode != null && jedecParameterPacket.Read1s1s8sCode != 0)
            {
                cmd = jedecParameterPacket.Read1s1s8sCode.Value;
                return true;
            }
            cmd = default;
            return false;
        }

        public bool SupportsRead1s4s4s(out uint cmd)
        {
            if(jedecParameterPacket.Supports1s4s4s == true &&
                jedecParameterPacket.Read1s4s4sCode != null)
            {
                cmd = jedecParameterPacket.Read1s4s4sCode.Value;
                return true;
            }
            cmd = default;
            return false;
        }

        public bool SupportsRead1s1s4s(out uint cmd)
        {
            if(jedecParameterPacket.Supports1s1s4s == true &&
                jedecParameterPacket.Read1s1s4sCode != null)
            {
                cmd = jedecParameterPacket.Read1s1s4sCode.Value;
                return true;
            }
            cmd = default;
            return false;
        }

        public bool SupportsRead1s2s2s(out uint cmd)
        {
            if(jedecParameterPacket.Supports1s2s2s == true &&
                jedecParameterPacket.Read1s2s2sCode != null)
            {
                cmd = jedecParameterPacket.Read1s2s2sCode.Value;
                return true;
            }
            cmd = default;
            return false;
        }

        public bool SupportsRead1s1s2s(out uint cmd)
        {
            if(jedecParameterPacket.Supports1s1s2s == true &&
                jedecParameterPacket.Read1s1s2sCode != null)
            {
                cmd = jedecParameterPacket.Read1s1s2sCode.Value;
                return true;
            }
            cmd = default;
            return false;
        }

        public bool SupportsRead2s2s2s(out uint cmd, out uint dummy)
        {
            if(jedecParameterPacket.Supports2s2s2s == true &&
                jedecParameterPacket.Read2s2s2sCode != null &&
                jedecParameterPacket.NumberOfWaitStates2s2s2s != null)
            {
                cmd = jedecParameterPacket.Read2s2s2sCode.Value;
                dummy = jedecParameterPacket.NumberOfWaitStates2s2s2s.Value;
                return true;
            }
            cmd = default;
            dummy = default;
            return false;
        }

        public bool SupportsRead4s4s4s(out uint cmd, out uint dummy)
        {
            if(jedecParameterPacket.Supports4s4s4s == true &&
                jedecParameterPacket.Read4s4s4sCode != null &&
                jedecParameterPacket.NumberOfWaitStates4s4s4s != null)
            {
                cmd = jedecParameterPacket.Read4s4s4sCode.Value;
                dummy = jedecParameterPacket.NumberOfWaitStates4s4s4s.Value;
                return true;
            }
            cmd = default;
            dummy = default;
            return false;
        }

        public long PageSize
        {
            get => (long)Math.Pow(2, (double)jedecParameterPacket.PageSize);
            set => jedecParameterPacket.PageSize = (byte)Math.Floor(Math.Log(value, 2));
        }

        // JESD216H, Section 6.4.5:
        // For densities 2 gigabits or less, bit-31 is set to 0b. The field 30:0 defines the size in bits.
        // Example: 00FFFFFFh = 16 megabits
        // For densities 4 gigabits and above, bit-31 is set to 1b. The field 30:0 defines ‘N’ where the
        // density is computed as 2^N bits (N must be >= 32).
        // Example: 80000021h = 2^33 = 8 gigabits
        public long FlashDensity
        {
            get
            {
                if(BitHelper.IsBitSet(jedecParameterPacket.FlashMemoryDensity, 31))
                {
                    return (long)Math.Pow(2, (double)(jedecParameterPacket.FlashMemoryDensity & ~BitHelper.Bit(31)));
                }
                return (jedecParameterPacket.FlashMemoryDensity + 1) / 8;
            }

            set
            {
                if(value <= MaxLinearlySavedSize)
                {
                    jedecParameterPacket.FlashMemoryDensity = (uint)(value * 8 - 1);
                }
                else
                {
                    jedecParameterPacket.FlashMemoryDensity = (uint)Math.Floor(Math.Log(FlashDensity, 2)) | (uint)BitHelper.Bit(31);
                }
            }
        }

        public long EraseSize { get => (long)Math.Pow(2, (double)jedecParameterPacket.EraseType1Size); set => jedecParameterPacket.EraseType1Size = (byte)Math.Floor(Math.Log(value, 2)); }

        public bool CmdF0Supported => (jedecParameterPacket.SoftResetAndRescueSequence ?? 0) == 0b001000;

        public bool ByteOrder8d8d8d => jedecParameterPacket.ByteOrder8d8d8d ?? false;

        public byte EraseCode { get => (byte)jedecParameterPacket.EraseType1Instruction; set => jedecParameterPacket.EraseType1Instruction = value; }

        public override byte ParameterIdMsb => (byte)(ParameterId >> 8);

        public override byte ParameterIdLsb => (byte)ParameterId;

        protected override byte[] ToBytes()
        {
            jedecParameterPacket.SetDWSizeAutomatically();
            return Packet.Encode(jedecParameterPacket);
        }

        private JedecParameter(JedecParameterPacket jedecParamPacket) : base(1, 6)
        {
            jedecParameterPacket = jedecParamPacket;
        }

        private readonly JedecParameterPacket jedecParameterPacket;

        private const long MaxLinearlySavedSize =  2 * (1u << 30) / 8;
    }

    [LeastSignificantByteFirst]
    public class JedecParameterPacket
    {
        // This method automatically calculates DWSize based on the presence of its fields.
        // It was designed specifically to set the DWSize before encoding.
        public void SetDWSizeAutomatically()
        {
            var type = this.GetType();
            var maxByteOffset = 0u;
            foreach(var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var offsetAttr = field.GetCustomAttribute<OffsetAttribute>();
                if(offsetAttr != null)
                {
                    var v = field.GetValue(this);
                    if(v != null)
                    {
                        maxByteOffset = Math.Max(offsetAttr.OffsetInBytes, maxByteOffset);
                    }
                }
            }

            DWSize = maxByteOffset / 4 + 1;
        }

        public bool HasDWi(int dwIndex)
        {
            return DWSize >= dwIndex;
        }

        public uint DWSize = 12;

        // 1st DWORD
        [PacketField, Offset(doubleWords: 0, bits:  0), Width(bits: 2)]
        public uint EraseSizes;
        [PacketField, Offset(doubleWords: 0, bits:  2), Width(bits: 1)]
        public bool WriteGranularity;
        [PacketField, Offset(doubleWords: 0, bits:  3), Width(bits: 1)]
        public bool VolatileStatusRegisterBlockProtect;
        [PacketField, Offset(doubleWords: 0, bits:  3), Width(bits: 1)]
        public bool WriteEnableSelect;
        [PacketField, Offset(doubleWords: 0, bits:  8), Width(bits: 8)]
        public uint Erase4KBCode;
        [PacketField, Offset(doubleWords: 0, bits:  16), Width(bits: 1)]
        public bool Supports1s1s2s;
        [PacketField, Offset(doubleWords: 0, bits:  17), Width(bits: 2)]
        public uint AddressBytes;
        [PacketField, Offset(doubleWords: 0, bits:  19), Width(bits: 1)]
        public bool SupportsDTR;
        [PacketField, Offset(doubleWords: 0, bits:  20), Width(bits: 1)]
        public bool Supports1s2s2s;
        [PacketField, Offset(doubleWords: 0, bits:  21), Width(bits: 1)]
        public bool Supports1s4s4s;
        [PacketField, Offset(doubleWords: 0, bits:  22), Width(bits: 1)]
        public bool Supports1s1s4s;
        // reserved 23-31

        // 2nd DWORD
        [PacketField, Offset(doubleWords: 1, bits:  0), Width(bits: 32)]
        public uint FlashMemoryDensity;

        // 3rd DWORD
        [PacketField, Offset(doubleWords: 2, bits:  0), Width(bits: 5), PresentIf(nameof(HasDWi), 3)]
        public uint? NumberOfWaitStates1s4s4s;

        [PacketField, Offset(doubleWords: 2, bits:  5), Width(bits: 3), PresentIf(nameof(HasDWi), 3)]
        public uint? NumberOfModeClocks1s4s4s;
        [PacketField, Offset(doubleWords: 2, bits:  8), Width(bits: 8), PresentIf(nameof(HasDWi), 3)]
        public uint? Read1s4s4sCode;

        [PacketField, Offset(doubleWords: 2, bits:  16), Width(bits: 5), PresentIf(nameof(HasDWi), 3)]
        public uint? NumberOfWaitStates1s1s4s;
        [PacketField, Offset(doubleWords: 2, bits:  21), Width(bits: 3), PresentIf(nameof(HasDWi), 3)]
        public uint? NumberOfModeClocks1s1s4s;
        [PacketField, Offset(doubleWords: 2, bits:  24), Width(bits: 8), PresentIf(nameof(HasDWi), 3)]
        public uint? Read1s1s4sCode;

        // 4th DWORD
        [PacketField, Offset(doubleWords: 3, bits:  0), Width(bits: 5), PresentIf(nameof(HasDWi), 4)]
        public uint? NumberOfWaitStates1s1s2s;
        [PacketField, Offset(doubleWords: 3, bits:  5), Width(bits: 3), PresentIf(nameof(HasDWi), 4)]
        public uint? NumberOfModeClocks1s1s2s;
        [PacketField, Offset(doubleWords: 3, bits:  8), Width(bits: 8), PresentIf(nameof(HasDWi), 4)]
        public uint? Read1s1s2sCode;
        [PacketField, Offset(doubleWords: 3, bits:  16), Width(bits: 5), PresentIf(nameof(HasDWi), 4)]
        public uint? NumberOfWaitStates1s2s2s;
        [PacketField, Offset(doubleWords: 3, bits:  21), Width(bits: 3), PresentIf(nameof(HasDWi), 4)]
        public uint? NumberOfModeClocks1s2s2s;
        [PacketField, Offset(doubleWords: 3, bits:  24), Width(bits: 8), PresentIf(nameof(HasDWi), 4)]
        public uint? Read1s2s2sCode;

        // 5th DWORD
        [PacketField, Offset(doubleWords: 4, bits:  0), Width(bits: 1), PresentIf(nameof(HasDWi), 5)]
        public bool? Supports2s2s2s;
        //reserver [1-3]
        [PacketField, Offset(doubleWords: 4, bits:  4), Width(bits: 1), PresentIf(nameof(HasDWi), 5)]
        public bool? Supports4s4s4s;
        //  reserved [5-31]

        // 6th DWORD
        //reserved [0 - 15]
        [PacketField, Offset(doubleWords: 5, bits:  16), Width(bits: 5), PresentIf(nameof(HasDWi), 6)]
        public uint? NumberOfWaitStates2s2s2s;
        [PacketField, Offset(doubleWords: 5, bits:  21), Width(bits: 3), PresentIf(nameof(HasDWi), 6)]
        public uint? NumberOfModeClocks2s2s2s;
        [PacketField, Offset(doubleWords: 5, bits:  24), Width(bits: 8), PresentIf(nameof(HasDWi), 6)]
        public uint? Read2s2s2sCode;

        // 7th DWORD

        //reserved [0 - 15]
        [PacketField, Offset(doubleWords: 6, bits:  16), Width(bits: 5), PresentIf(nameof(HasDWi), 7)]
        public uint? NumberOfWaitStates4s4s4s;
        [PacketField, Offset(doubleWords: 6, bits:  21), Width(bits: 3), PresentIf(nameof(HasDWi), 7)]
        public uint? NumberOfModeClocks4s4s4s;
        [PacketField, Offset(doubleWords: 6, bits:  24), Width(bits: 8), PresentIf(nameof(HasDWi), 7)]
        public uint? Read4s4s4sCode;

        // 8th DWORD
        [PacketField, Offset(doubleWords: 7, bits:  0), Width(bits: 8), PresentIf(nameof(HasDWi), 8)]
        public uint? EraseType1Size;
        [PacketField, Offset(doubleWords: 7, bits:  8), Width(bits: 8), PresentIf(nameof(HasDWi), 8)]
        public uint? EraseType1Instruction;
        [PacketField, Offset(doubleWords: 7, bits:  16), Width(bits: 8), PresentIf(nameof(HasDWi), 8)]
        public uint? EraseType2Size;
        [PacketField, Offset(doubleWords: 7, bits:  24), Width(bits: 8), PresentIf(nameof(HasDWi), 8)]
        public uint? EraseType2Instruction;

        // 9th DWORD
        [PacketField, Offset(doubleWords: 8, bits:  0), Width(bits: 8), PresentIf(nameof(HasDWi), 9)]
        public uint? EraseType3Size;
        [PacketField, Offset(doubleWords: 8, bits:  8), Width(bits: 8), PresentIf(nameof(HasDWi), 9)]
        public uint? EraseType3Instruction;
        [PacketField, Offset(doubleWords: 8, bits:  16), Width(bits: 8), PresentIf(nameof(HasDWi), 9)]
        public uint? EraseType4Size;
        [PacketField, Offset(doubleWords: 8, bits:  24), Width(bits: 8), PresentIf(nameof(HasDWi), 9)]
        public uint? EraseType4Instruction;

        // 10th DWORD
        [PacketField, Offset(doubleWords: 9, bits:  0), Width(bits:4), PresentIf(nameof(HasDWi), 10)]
        public uint? MultiplierFromTypicalEraseTimeToMaximumEraseTime;
        [PacketField, Offset(doubleWords: 9, bits:  4), Width(bits:7), PresentIf(nameof(HasDWi), 10)]
        public uint? EraseType1TypicalTime;
        [PacketField, Offset(doubleWords: 9, bits:  11), Width(bits:7), PresentIf(nameof(HasDWi), 10)]
        public uint? EraseType2TypicalTime;
        [PacketField, Offset(doubleWords: 9, bits:  18), Width(bits:7), PresentIf(nameof(HasDWi), 10)]
        public uint? EraseType3TypicalTime;
        [PacketField, Offset(doubleWords: 9, bits:  25), Width(bits:7), PresentIf(nameof(HasDWi), 10)]
        public uint? EraseType4TypicalTime;

        // 11th DWORD
        [PacketField, Offset(doubleWords: 10, bits:  0), Width(bits:4), PresentIf(nameof(HasDWi), 11)]
        public uint? MultiplierFromTypicalProgramTimeToMaximumProgramTime;
        [PacketField, Offset(doubleWords: 10, bits:  4), Width(bits:4), PresentIf(nameof(HasDWi), 11)]
        public uint? PageSize;
        [PacketField, Offset(doubleWords: 10, bits:  8), Width(bits:2), PresentIf(nameof(HasDWi), 11)]
        public uint? PageProgramTypicalTime;
        [PacketField, Offset(doubleWords: 10, bits:  14), Width(bits:5), PresentIf(nameof(HasDWi), 11)]
        public uint? FirstByteProgramTypicalTime;
        [PacketField, Offset(doubleWords: 10, bits:  19), Width(bits:5), PresentIf(nameof(HasDWi), 11)]
        public uint? AdditionalByteProgramTypicalTime;
        [PacketField, Offset(doubleWords: 10, bits:  24), Width(bits:7), PresentIf(nameof(HasDWi), 11)]
        public uint? ChipEraseTypicalTime;

        // reserved 31

        // 12th DWORD

        [PacketField, Offset(doubleWords: 11, bits:  0), Width(bits:4), PresentIf(nameof(HasDWi), 12)]
        public uint? ProhibitedOperationsDuringProgramSuspend;
        [PacketField, Offset(doubleWords: 11, bits:  4), Width(bits:4), PresentIf(nameof(HasDWi), 12)]
        public uint? ProhibitedOperationsDuringEraseSuspend;
        // reserved 8 
        [PacketField, Offset(doubleWords: 11, bits:  9), Width(bits: 4), PresentIf(nameof(HasDWi), 12)]
        public uint? ProgramResumeToSuspendInterval;
        [PacketField, Offset(doubleWords: 11, bits:  13), Width(bits:7), PresentIf(nameof(HasDWi), 12)]
        public uint? SuspendInProgressProgramMaxLatency;
        [PacketField, Offset(doubleWords: 11, bits:  20), Width(bits:4), PresentIf(nameof(HasDWi), 12)]
        public uint? ResumeToSuspendInterval;
        [PacketField, Offset(doubleWords: 11, bits:  24), Width(bits:7), PresentIf(nameof(HasDWi), 12)]
        public uint? SuspendInProgressEraseMaxLatency;
        [PacketField, Offset(doubleWords: 11, bits: 31), Width(bits: 1), PresentIf(nameof(HasDWi), 12)]
        public bool? SuspendResumeSupported;

        // 13th DWORD
        [PacketField, Offset(doubleWords: 12, bits:  0), Width(bits: 8), PresentIf(nameof(HasDWi), 13)]
        public uint? ProgramResumeCode;
        [PacketField, Offset(doubleWords: 12, bits:  8), Width(bits: 8), PresentIf(nameof(HasDWi), 13)]
        public uint? ProgramSuspendCode;
        [PacketField, Offset(doubleWords: 12, bits:  16), Width(bits: 8), PresentIf(nameof(HasDWi), 13)]
        public uint? ProgramInstructionCode;
        [PacketField, Offset(doubleWords: 12, bits:  24), Width(bits: 8), PresentIf(nameof(HasDWi), 13)]
        public uint? SuspendInstructionCode;

        // 14th DWORD
        // reserved [0-1]
        [PacketField, Offset(doubleWords: 13, bits:  2), Width(bits: 6), PresentIf(nameof(HasDWi), 14)]
        public uint? StatusRegisterPollingDeviceBusy;
        [PacketField, Offset(doubleWords: 13, bits:  8), Width(bits: 7), PresentIf(nameof(HasDWi), 14)]
        public uint? ExitDeepPowerdownToNextOperationDelay;
        [PacketField, Offset(doubleWords: 13, bits:  15), Width(bits: 8), PresentIf(nameof(HasDWi), 14)]
        public uint? ExitDeepPowerdownInstruction;
        [PacketField, Offset(doubleWords: 13, bits:  23), Width(bits: 8), PresentIf(nameof(HasDWi), 14)]
        public uint? EnterDeepPowerdownInstruction;
        [PacketField, Offset(doubleWords: 13, bits:  31), Width(bits: 1), PresentIf(nameof(HasDWi), 14)]
        public bool? DeepPowerdownSupported;

        // 15th DWORD
        [PacketField, Offset(doubleWords: 14, bits:  0), Width(bits: 4), PresentIf(nameof(HasDWi), 15)]
        public uint? Disable4s4s4sSequence;
        [PacketField, Offset(doubleWords: 14, bits:  4), Width(bits: 5), PresentIf(nameof(HasDWi), 15)]
        public uint? Enable4s4s4sSequence;
        [PacketField, Offset(doubleWords: 14, bits:  9), Width(bits: 1), PresentIf(nameof(HasDWi), 15)]
        public bool? Supports044;
        [PacketField, Offset(doubleWords: 14, bits:  10), Width(bits: 6), PresentIf(nameof(HasDWi), 15)]
        public uint? ExitMethod044;
        [PacketField, Offset(doubleWords: 14, bits:  16), Width(bits: 4), PresentIf(nameof(HasDWi), 15)]
        public uint? EntryMethod044;
        [PacketField, Offset(doubleWords: 14, bits:  20), Width(bits: 3), PresentIf(nameof(HasDWi), 15)]
        public uint? QuadEnableRequirements;
        [PacketField, Offset(doubleWords: 14, bits:  23), Width(bits: 1), PresentIf(nameof(HasDWi), 15)]
        public bool? HoldOrResetDisable;
        // reserved [24-31]

        // 16th DWORD
        [PacketField, Offset(doubleWords: 15, bits:  0), Width(bits: 7), PresentIf(nameof(HasDWi), 16)]
        public uint? StatusRegisterWriteEnableInstruction;
        // reserved 7
        [PacketField, Offset(doubleWords: 15, bits:  8), Width(bits: 6), PresentIf(nameof(HasDWi), 16)]
        public uint? SoftResetAndRescueSequence;
        [PacketField, Offset(doubleWords: 15, bits:  14), Width(bits: 10), PresentIf(nameof(HasDWi), 16)]
        public uint? Exit4ByteAddressing;
        [PacketField, Offset(doubleWords: 15, bits:  24), Width(bits: 8), PresentIf(nameof(HasDWi), 16)]
        public uint? Enter4ByteAddressing;

        // 17th DWORD
        [PacketField, Offset(doubleWords: 16, bits:  0), Width(bits: 5), PresentIf(nameof(HasDWi), 17)]
        public uint? NumberOfWaitStates1s8s8s;
        [PacketField, Offset(doubleWords: 16, bits:  5), Width(bits: 3), PresentIf(nameof(HasDWi), 17)]
        public uint? NumberOfModeClocks1s8s8s;
        [PacketField, Offset(doubleWords: 16, bits:  8), Width(bits: 8), PresentIf(nameof(HasDWi), 17)]
        public uint? Read1s8s8sCode;
        [PacketField, Offset(doubleWords: 16, bits:  16), Width(bits: 5), PresentIf(nameof(HasDWi), 17)]
        public uint? NumberOfWaitStates1s1s8s;
        [PacketField, Offset(doubleWords: 16, bits:  21), Width(bits: 3), PresentIf(nameof(HasDWi), 17)]
        public uint? NumberOfModeClocks1s1s8s;
        [PacketField, Offset(doubleWords: 16, bits:  24), Width(bits: 8), PresentIf(nameof(HasDWi), 17)]
        public uint? Read1s1s8sCode;

        // 18th DWORD
        // reserved [0-17]
        [PacketField, Offset(doubleWords: 17, bits:  18), Width(bits: 5), PresentIf(nameof(HasDWi), 18)]
        public uint? VariableOutputDriverStrength;
        [PacketField, Offset(doubleWords: 17, bits:  23), Width(bits: 1), PresentIf(nameof(HasDWi), 18)]
        public bool? JEDECSPIProtocolReset;
        [PacketField, Offset(doubleWords: 17, bits:  24), Width(bits: 2), PresentIf(nameof(HasDWi), 18)]
        public uint? DataStrobeWaveformsInSTR;
        [PacketField, Offset(doubleWords: 17, bits:  26), Width(bits: 1), PresentIf(nameof(HasDWi), 18)]
        public bool? DataStrobeSupportForQPISTR;
        [PacketField, Offset(doubleWords: 17, bits:  27), Width(bits: 1), PresentIf(nameof(HasDWi), 18)]
        public bool? DataStrobeSupportForQPIDTR;
        // reserved 28
        [PacketField, Offset(doubleWords: 17, bits:  29), Width(bits: 2), PresentIf(nameof(HasDWi), 18)]
        public uint? OctalDTRCommandAndCommandExtension;
        [PacketField, Offset(doubleWords: 17, bits:  31), Width(bits: 1), PresentIf(nameof(HasDWi), 18)]
        public bool? ByteOrder8d8d8d;

        // 19th DWORD
        [PacketField, Offset(doubleWords: 18, bits:  0), Width(bits: 4), PresentIf(nameof(HasDWi), 19)]
        public uint? Disable8s8s8sSequence;
        [PacketField, Offset(doubleWords: 18, bits:  4), Width(bits: 5), PresentIf(nameof(HasDWi), 19)]
        public uint? Enable8s8s8sSequence;
        [PacketField, Offset(doubleWords: 18, bits:  9), Width(bits: 1), PresentIf(nameof(HasDWi), 19)]
        public bool? Supports088;
        [PacketField, Offset(doubleWords: 18, bits:  10), Width(bits: 6), PresentIf(nameof(HasDWi), 19)]
        public uint? ExitMethod088;
        [PacketField, Offset(doubleWords: 18, bits:  16), Width(bits: 4), PresentIf(nameof(HasDWi), 19)]
        public uint? ModeEntryMethod088;
        [PacketField, Offset(doubleWords: 18, bits:  20), Width(bits: 3), PresentIf(nameof(HasDWi), 19)]
        public uint? OctalEnableRequirements;
        // reserved 23-31

        // 20th DWORD
        [PacketField, Offset(doubleWords: 19, bits:  0), Width(bits: 4), PresentIf(nameof(HasDWi), 20)]
        public uint? MaxDataSpeedWithoutDataStrobe4s4s4s;
        [PacketField, Offset(doubleWords: 19, bits:  4), Width(bits: 4), PresentIf(nameof(HasDWi), 20)]
        public uint? MaxDataSpeedWithDataStrobe4s4s4s;
        [PacketField, Offset(doubleWords: 19, bits:  8), Width(bits: 4), PresentIf(nameof(HasDWi), 20)]
        public uint? MaxDataSpeedWithoutDataStrobe4s4d4d;
        [PacketField, Offset(doubleWords: 19, bits:  12), Width(bits: 4), PresentIf(nameof(HasDWi), 20)]
        public uint? MaxDataSpeedWithDataStrobe4s4d4d;
        [PacketField, Offset(doubleWords: 19, bits:  16), Width(bits: 4), PresentIf(nameof(HasDWi), 20)]
        public uint? MaxDataSpeedWithoutDataStrobe8s8s8s;
        [PacketField, Offset(doubleWords: 19, bits:  20), Width(bits: 4), PresentIf(nameof(HasDWi), 20)]
        public uint? MaxDataSpeedWithDataStrobe8s8s8s;
        [PacketField, Offset(doubleWords: 19, bits:  24), Width(bits: 4), PresentIf(nameof(HasDWi), 20)]
        public uint? MaxDataSpeedWithoutDataStrobe8d8d8d;
        [PacketField, Offset(doubleWords: 19, bits:  28), Width(bits: 4), PresentIf(nameof(HasDWi), 20)]
        public uint? MaxDataSpeedWithDataStrobe8d8d8d;

        // 21th DWORD
        [PacketField, Offset(doubleWords: 20, bits:  0), Width(bits: 1), PresentIf(nameof(HasDWi), 21)]
        public bool? Supports1s1d1d;
        [PacketField, Offset(doubleWords: 20, bits:  1), Width(bits: 1), PresentIf(nameof(HasDWi), 21)]
        public bool? Supports1s2d2d;
        [PacketField, Offset(doubleWords: 20, bits:  2), Width(bits: 1), PresentIf(nameof(HasDWi), 21)]
        public bool? Supports1s4d4d;
        [PacketField, Offset(doubleWords: 20, bits:  3), Width(bits: 1), PresentIf(nameof(HasDWi), 21)]
        public bool? Supports4s4d4d;
        // reserved 4-31

        // 22th DWORD
        [PacketField, Offset(doubleWords: 21, bits:  0), Width(bits: 5), PresentIf(nameof(HasDWi), 22)]
        public uint? NumberOfWaitStates1s1d1d;
        [PacketField, Offset(doubleWords: 21, bits:  5), Width(bits: 3), PresentIf(nameof(HasDWi), 22)]
        public uint? NumberOfModeClocks1s1d1d;
        [PacketField, Offset(doubleWords: 21, bits:  8), Width(bits: 8), PresentIf(nameof(HasDWi), 22)]
        public uint? Read1s1d1dCode;
        [PacketField, Offset(doubleWords: 21, bits:  16), Width(bits: 5), PresentIf(nameof(HasDWi), 22)]
        public uint? NumberOfWaitStates1s2d2d;
        [PacketField, Offset(doubleWords: 21, bits:  21), Width(bits: 3), PresentIf(nameof(HasDWi), 22)]
        public uint? NumberOfModeClocks1s2d2d;
        [PacketField, Offset(doubleWords: 21, bits:  24), Width(bits: 8), PresentIf(nameof(HasDWi), 22)]
        public uint? Read1s2d2dCode;

        // 23th DWORD
        [PacketField, Offset(doubleWords: 21, bits:  0), Width(bits: 5), PresentIf(nameof(HasDWi), 23)]
        public uint? NumberOfWaitStates1s4d4d;
        [PacketField, Offset(doubleWords: 21, bits:  5), Width(bits: 3), PresentIf(nameof(HasDWi), 23)]
        public uint? NumberOfModeClocks1s4d4d;
        [PacketField, Offset(doubleWords: 21, bits:  8), Width(bits: 8), PresentIf(nameof(HasDWi), 23)]
        public uint? Read1s4d4dCode;
        [PacketField, Offset(doubleWords: 21, bits:  16), Width(bits: 5), PresentIf(nameof(HasDWi), 23)]
        public uint? NumberOfWaitStates4s4d4d;
        [PacketField, Offset(doubleWords: 21, bits:  21), Width(bits: 3), PresentIf(nameof(HasDWi), 23)]
        public uint? NumberOfModeClocks4s4d4d;
        [PacketField, Offset(doubleWords: 21, bits:  24), Width(bits: 8), PresentIf(nameof(HasDWi), 23)]
        public uint? Read4s4d4dCode;
    }
}
