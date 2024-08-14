//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class CV32E40P : RiscV32
    {
        public CV32E40P(IMachine machine, IRiscVTimeProvider timeProvider = null, uint hartId = 0, [NameAlias("privilegeArchitecture")] PrivilegedArchitecture privilegedArchitecture = PrivilegedArchitecture.Priv1_11, Endianess endianness = Endianess.LittleEndian, string cpuType = "rv32imfc_zicsr_zifencei")
            : base(machine, cpuType, timeProvider, hartId, privilegedArchitecture, endianness, allowUnalignedAccesses : true)
        {
            // enable all interrupt sources
            MIE = 0xffffffff;

            RegisterCSR((ulong)CustomCSR.PerformanceCounterMode, () => LogUnhandledCSRRead("PerformanceCounterMode"), val => LogUnhandledCSRWrite("PerformanceCounterMode", val));
            RegisterCSR((ulong)CustomCSR.StackCheckEnable      , () => LogUnhandledCSRRead("StackCheckEnable")      , val => LogUnhandledCSRWrite("StackCheckEnable", val));
            RegisterCSR((ulong)CustomCSR.StackBase             , () => LogUnhandledCSRRead("StackBase")             , val => LogUnhandledCSRWrite("StackBase", val));
            RegisterCSR((ulong)CustomCSR.StackEnd              , () => LogUnhandledCSRRead("StackEnd")              , val => LogUnhandledCSRWrite("StackEnd", val));
            RegisterCSR((ulong)CustomCSR.HardwareLoop0Start    , () => LogUnhandledCSRRead("HardwareLoop0Start")    , val => LogUnhandledCSRWrite("HardwareLoop0Start", val));
            RegisterCSR((ulong)CustomCSR.HardwareLoop0End      , () => LogUnhandledCSRRead("HardwareLoop0End")      , val => LogUnhandledCSRWrite("HardwareLoop0End", val));
            RegisterCSR((ulong)CustomCSR.HardwareLoop0Counter  , () => LogUnhandledCSRRead("HardwareLoop0Counter")  , val => LogUnhandledCSRWrite("HardwareLoop0Counter", val));
            RegisterCSR((ulong)CustomCSR.HardwareLoop1Start    , () => LogUnhandledCSRRead("HardwareLoop1Start")    , val => LogUnhandledCSRWrite("HardwareLoop1Start", val));
            RegisterCSR((ulong)CustomCSR.HardwareLoop1End      , () => LogUnhandledCSRRead("HardwareLoop1End")      , val => LogUnhandledCSRWrite("HardwareLoop1End", val));
            RegisterCSR((ulong)CustomCSR.HardwareLoop1Counter  , () => LogUnhandledCSRRead("HardwareLoop1Counter")  , val => LogUnhandledCSRWrite("HardwareLoop1Counter", val));

            InstallCustomInstruction(pattern: "FFFFFFFFFFFFBBBBB000DDDDD0001011", handler: opcode => LoadRegisterImmediate(opcode, Width.Byte, BitExtension.Sign, "p.lb rD, Imm(rs1!)"));
            InstallCustomInstruction(pattern: "FFFFFFFFFFFFBBBBB100DDDDD0001011", handler: opcode => LoadRegisterImmediate(opcode, Width.Byte, BitExtension.Zero, "p.lbu rD, Imm(rs1!)"));
            InstallCustomInstruction(pattern: "FFFFFFFFFFFFBBBBB001DDDDD0001011", handler: opcode => LoadRegisterImmediate(opcode, Width.HalfWord, BitExtension.Sign, "p.lh rD, Imm(rs1!)"));
            InstallCustomInstruction(pattern: "FFFFFFFFFFFFBBBBB101DDDDD0001011", handler: opcode => LoadRegisterImmediate(opcode, Width.HalfWord, BitExtension.Zero, "p.lhu rD, Imm(rs1!)"));
            InstallCustomInstruction(pattern: "FFFFFFFFFFFFBBBBB010DDDDD0001011", handler: opcode => LoadRegisterImmediate(opcode, Width.Word, BitExtension.Zero, "p.lw rD, Imm(rs1!)"));
            InstallCustomInstruction(pattern: "0000000FFFFFBBBBB111DDDDD0001011", handler: opcode => LoadRegisterRegister(opcode, Width.Byte, BitExtension.Sign, postIncrement: true, log: "p.lb rD, rs2(rs1!)"));
            InstallCustomInstruction(pattern: "0100000FFFFFBBBBB111DDDDD0001011", handler: opcode => LoadRegisterRegister(opcode, Width.Byte, BitExtension.Zero, postIncrement: true, log: "p.lbu rD, rs2(rs1!)"));
            InstallCustomInstruction(pattern: "0001000FFFFFBBBBB111DDDDD0001011", handler: opcode => LoadRegisterRegister(opcode, Width.HalfWord, BitExtension.Sign, postIncrement: true, log: "p.lh rD, rs2(rs1!)"));
            InstallCustomInstruction(pattern: "0101000FFFFFBBBBB111DDDDD0001011", handler: opcode => LoadRegisterRegister(opcode, Width.HalfWord, BitExtension.Zero, postIncrement: true, log: "p.lhu rD, rs2(rs1!)"));
            InstallCustomInstruction(pattern: "0010000FFFFFBBBBB111DDDDD0001011", handler: opcode => LoadRegisterRegister(opcode, Width.Word, BitExtension.Zero, postIncrement: true, log: "p.lw rD, rs2(rs1!)"));
            InstallCustomInstruction(pattern: "0000000FFFFFBBBBB111DDDDD0000011", handler: opcode => LoadRegisterRegister(opcode, Width.Byte, BitExtension.Sign, "p.lb rD, rs2(rs1)"));
            InstallCustomInstruction(pattern: "0100000FFFFFBBBBB111DDDDD0000011", handler: opcode => LoadRegisterRegister(opcode, Width.Byte, BitExtension.Zero, "p.lbu rD, rs2(rs1)"));
            InstallCustomInstruction(pattern: "0001000FFFFFBBBBB111DDDDD0000011", handler: opcode => LoadRegisterRegister(opcode, Width.HalfWord, BitExtension.Sign, "p.lh rD, rs2(rs1)"));
            InstallCustomInstruction(pattern: "0101000FFFFFBBBBB111DDDDD0000011", handler: opcode => LoadRegisterRegister(opcode, Width.HalfWord, BitExtension.Zero, "p.lhu rD, rs2(rs1)"));
            InstallCustomInstruction(pattern: "0010000FFFFFBBBBB111DDDDD0000011", handler: opcode => LoadRegisterRegister(opcode, Width.Word, BitExtension.Zero, "p.lw rD, rs2(rs1)"));
            InstallCustomInstruction(pattern: "FFFFFFFSSSSSBBBBB000FFFFF0101011", handler: opcode => StoreRegisterImmediate(opcode, Width.Byte, "p.sb rs2, Imm(rs1!)"));
            InstallCustomInstruction(pattern: "FFFFFFFSSSSSBBBBB001FFFFF0101011", handler: opcode => StoreRegisterImmediate(opcode, Width.HalfWord, "p.sh rs2, Imm(rs1!)"));
            InstallCustomInstruction(pattern: "FFFFFFFSSSSSBBBBB010FFFFF0101011", handler: opcode => StoreRegisterImmediate(opcode, Width.Word, "p.sw rs2, Imm(rs1!)"));
            InstallCustomInstruction(pattern: "0000000SSSSSBBBBB100FFFFF0101011", handler: opcode => StoreRegisterRegister(opcode, Width.Byte, postIncrement: true, log: "p.sb rs2, rs3(rs1!)"));
            InstallCustomInstruction(pattern: "0000000SSSSSBBBBB101FFFFF0101011", handler: opcode => StoreRegisterRegister(opcode, Width.HalfWord, postIncrement: true, log: "p.sh rs2, rs3(rs1!)"));
            InstallCustomInstruction(pattern: "0000000SSSSSBBBBB110FFFFF0101011", handler: opcode => StoreRegisterRegister(opcode, Width.Word, postIncrement: true, log: "p.sw rs2, rs3(rs1!)"));
            InstallCustomInstruction(pattern: "0000000SSSSSBBBBB100FFFFF0100011", handler: opcode => StoreRegisterRegister(opcode, Width.Byte, "p.sb rs2, rs3(rs1)"));
            InstallCustomInstruction(pattern: "0000000SSSSSBBBBB101FFFFF0100011", handler: opcode => StoreRegisterRegister(opcode, Width.HalfWord, "p.sh rs2, rs3(rs1)"));
            InstallCustomInstruction(pattern: "0000000SSSSSBBBBB110FFFFF0100011", handler: opcode => StoreRegisterRegister(opcode, Width.Word, "p.sw rs2, rs3(rs1)"));
            InstallCustomInstruction(pattern: "0000010RRRRRSSSSS100DDDDD0110011", handler: opcode => CompareRegisters(opcode, ComparisonType.Min, Sign.Signed, "p.min rD, rs1, rs2"));
            InstallCustomInstruction(pattern: "0000010RRRRRSSSSS101DDDDD0110011", handler: opcode => CompareRegisters(opcode, ComparisonType.Min, Sign.Unsigned, "p.minu rD, rs1, rs2"));
            InstallCustomInstruction(pattern: "0000010RRRRRSSSSS110DDDDD0110011", handler: opcode => CompareRegisters(opcode, ComparisonType.Max, Sign.Signed, "p.max rD, rs1, rs2"));
            InstallCustomInstruction(pattern: "0000010RRRRRSSSSS111DDDDD0110011", handler: opcode => CompareRegisters(opcode, ComparisonType.Max, Sign.Unsigned, "p.maxu rD, rs1, rs2"));
            InstallCustomInstruction(pattern: "11LLLLLLLLLLSSSSS000DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Immediate, Operation.Extract, Width.Word, Sign.Signed, "p.extract rD, rs1, Is3, Is2"));
            InstallCustomInstruction(pattern: "11LLLLLLLLLLSSSSS001DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Immediate, Operation.Extract, Width.Word, Sign.Unsigned, "p.extractu rD, rs1, Is3, Is2"));
            InstallCustomInstruction(pattern: "1000000LLLLLSSSSS000DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Register, Operation.Extract, Width.Word, Sign.Signed, "p.extractr rD, rs1, rs2"));
            InstallCustomInstruction(pattern: "1000000LLLLLSSSSS001DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Register, Operation.Extract, Width.Word, Sign.Unsigned, "p.extractur rD, rs1, rs2"));
            InstallCustomInstruction(pattern: "000100000000SSSSS100DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Immediate, Operation.Extract, Width.HalfWord, Sign.Signed, "p.exths rD, rs1"));
            InstallCustomInstruction(pattern: "000100000000SSSSS101DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Immediate, Operation.Extract, Width.HalfWord, Sign.Unsigned, "p.exthz rD, rs1"));
            InstallCustomInstruction(pattern: "000100000000SSSSS110DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Immediate, Operation.Extract, Width.Byte, Sign.Signed, "p.extbs rD, rs1"));
            InstallCustomInstruction(pattern: "000100000000SSSSS111DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Immediate, Operation.Extract, Width.Byte, Sign.Unsigned, "p.extbz rD, rs1"));
            InstallCustomInstruction(pattern: "11LLLLLLLLLLSSSSS010DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Immediate, Operation.Insert, Width.Word, Sign.Unsigned, "p.insert rD, rs1, Is3, Is2"));
            InstallCustomInstruction(pattern: "1000000LLLLLSSSSS010DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Register, Operation.Insert, Width.Word, Sign.Unsigned, "p.insertr rD, rs1, rs2"));
            InstallCustomInstruction(pattern: "11LLLLLLLLLLSSSSS011DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Immediate, Operation.Clear, Width.Word, Sign.Unsigned, "p.bclr rD, rs1, Is3, Is2"));
            InstallCustomInstruction(pattern: "1000000LLLLLSSSSS011DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Register, Operation.Clear, Width.Word, Sign.Unsigned, "p.bclrr rD, rs1, rs2"));
            InstallCustomInstruction(pattern: "11LLLLLLLLLLSSSSS100DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Immediate, Operation.Set, Width.Word, Sign.Unsigned, "p.bset rD, rs1, Is3, Is2"));
            InstallCustomInstruction(pattern: "1000000LLLLLSSSSS100DDDDD0110011", handler: opcode => ManipulateBitsInRegister(opcode, Source.Register, Operation.Set, Width.Word, Sign.Unsigned, "p.bsetr rD, rs1, rs2"));
            InstallCustomInstruction(pattern: "JJJJJJJIIIIISSSSS010JJJJJ1100011", handler: opcode => BranchIf(opcode, Equality.Equal, "p.beqimm rs1, Imm5, Imm12"));
            InstallCustomInstruction(pattern: "JJJJJJJIIIIISSSSS011JJJJJ1100011", handler: opcode => BranchIf(opcode, Equality.NotEqual, "p.bneimm rs1, Imm5, Imm12"));
            InstallCustomInstruction(pattern: "0100001RRRRRSSSSS001DDDDD0110011", handler: opcode => MultiplyAccumulate(opcode, operationAdd: false, log: "p.msu rD, rs1, rs2"));
            InstallCustomInstruction(pattern: "0100001RRRRRSSSSS000DDDDD0110011", handler: opcode => MultiplyAccumulate(opcode, operationAdd: true, log: "p.mac rD, rs1, rs2"));
            InstallCustomInstruction(pattern: "00---------------001-----1011011", handler: _ => LogUnsupported("p.macuN rD, rs1, rs2, Is3"));
            InstallCustomInstruction(pattern: "00---------------110-----1011011", handler: _ => LogUnsupported("p.mac.zh.zl"));
            InstallCustomInstruction(pattern: "00---------------100-----1011011", handler: _ => LogUnsupported("p.mac.zl.zl"));
            InstallCustomInstruction(pattern: "00---------------111-----1011011", handler: _ => LogUnsupported("p.mac.zh.zh"));
            InstallCustomInstruction(pattern: "00---------------101-----1011011", handler: _ => LogUnsupported("p.mac.zl.zh"));
            InstallCustomInstruction(pattern: "01---------------100-----1011011", handler: _ => LogUnsupported("p.mac.zl.sl"));
            InstallCustomInstruction(pattern: "------------000000000000-1111011", handler: _ => LogUnsupported("lp.starti L, uimmL"));
            InstallCustomInstruction(pattern: "------------000000010000-1111011", handler: _ => LogUnsupported("lp.endi L, uimmL"));
            InstallCustomInstruction(pattern: "00---------------010-----1011011", handler: _ => LogUnsupported("p.addN rD, rs1, rs2, Is3"));
            InstallCustomInstruction(pattern: "1100000----------010-----1011011", handler: _ => LogUnsupported("p.adduNr rD, rs1, rs"));
            InstallCustomInstruction(pattern: "-----------------1000000-1111011", handler: _ => LogUnsupported("lp.setup L, rs1, uimmL"));
            InstallCustomInstruction(pattern: "-----------------1010000-1111011", handler: _ => LogUnsupported("lp.setupi L, uimmS, uimmL"));
            InstallCustomInstruction(pattern: "000000000000-----0100000-1111011", handler: _ => LogUnsupported("lp.count L, rs1"));
            InstallCustomInstruction(pattern: "00---------------011-----1011011", handler: _ => LogUnsupported("p.subN rD, rs1, rs2, Is3"));
            InstallCustomInstruction(pattern: "10---------------011-----1011011", handler: _ => LogUnsupported("p.subuN rD, rs1, rs2, Is3"));
            InstallCustomInstruction(pattern: "10---------------000-----1011011", handler: _ => LogUnsupported("p.mulsN rD, rs1, rs2, Is3"));
            InstallCustomInstruction(pattern: "01---------------000-----1011011", handler: _ => LogUnsupported("p.mulhhuN rD, rs1, rs2, Is3"));
            InstallCustomInstruction(pattern: "11---------------000-----1011011", handler: _ => LogUnsupported("p.mulhhsN rD, rs1, rs2, Is3"));
            InstallCustomInstruction(pattern: "000100000000-----001-----0110011", handler: _ => LogUnsupported("p.fl1 rD, rs1"));
            InstallCustomInstruction(pattern: "-----------------110-----0000011", handler: _ => LogUnsupported("p.elw"));
        }

        private ulong LogUnhandledCSRRead(string name)
        {
            this.Log(LogLevel.Error, "Reading from an unsupported CSR {0}", name);
            return 0u;
        }

        private void LogUnhandledCSRWrite(string name, ulong value)
        {
            this.Log(LogLevel.Error, "Writing to an unsupported CSR {0} value: 0x{1:X}", name, value);
        }

        private void MultiplyAccumulate(ulong opcode, bool operationAdd, string log)
        {
            this.Log(LogLevel.Noisy, "({0}) at PC={1:X}", log, PC.RawValue);

            var rD = (int)BitHelper.GetValue(opcode, 7, 5);
            var rs1 = (int)BitHelper.GetValue(opcode, 15, 5);
            var rs2 = (int)BitHelper.GetValue(opcode, 20, 5);

            var rDValue = (long)GetRegister(rD).RawValue;
            var rs1Value = (long)GetRegister(rs1).RawValue;
            var rs2Value = (long)GetRegister(rs2).RawValue;

            var result = (ulong)(operationAdd
                ? rDValue + rs1Value * rs2Value
                : rDValue - rs1Value * rs2Value);

            SetRegister(rD, result);
        }

        private void LoadRegisterImmediate(ulong opcode, Width width, BitExtension extension, string log)
        {
            this.Log(LogLevel.Noisy, "({0}) at PC={1:X}", log, PC.RawValue);
            // rD = Sext/Zext(Mem8/16/32(rs1))
            // rs1 += Imm[11:0]
            // It seems that Imm should be extended, but the docs do not confirm it explicitly
            var imm = (int)BitHelper.SignExtend((uint)BitHelper.GetValue(opcode, 20, 12), 12);
            var rD = (int)BitHelper.GetValue(opcode, 7, 5);
            var rs1 = (int)BitHelper.GetValue(opcode, 15, 5);
            var rs1Value = (long)GetRegister(rs1).RawValue;
            SetRegister(rD, GetMemValue(width, extension, (ulong)rs1Value));
            SetRegister(rs1, (ulong)(rs1Value + imm));
        }

        private void LoadRegisterRegister(ulong opcode, Width width, BitExtension extension, string log, bool postIncrement = false)
        {
            this.Log(LogLevel.Noisy, "({0}) at PC={1:X}", log, PC.RawValue);
            // with post-increment:
            // rD = Sext/Zext(Mem8/16/32(rs1))
            // rs1 += rs2
            // without post-increment:
            // rD = Sext/Zext(Mem8/16/32(rs1 + rs2))
            var rD = (int)BitHelper.GetValue(opcode, 7, 5);
            var rs1 = (int)BitHelper.GetValue(opcode, 15, 5);
            var rs1Value = GetRegister(rs1).RawValue;
            var rs2 = (int)BitHelper.GetValue(opcode, 20, 5);
            var rs2Value = GetRegister(rs2).RawValue;
            SetRegister(rD, GetMemValue(width, extension, postIncrement ? rs1Value : rs1Value + rs2Value));
            if(postIncrement)
            {
                SetRegister(rs1, rs1Value + rs2Value);
            }
        }

        private void StoreRegisterImmediate(ulong opcode, Width width, string log)
        {
            this.Log(LogLevel.Noisy, "({0}) at PC={1:X}", log, PC.RawValue);
            // Mem8/16/32(rs1) = rs2
            // rs1 += Imm[11:0]
            var imm = (int)BitHelper.SignExtend((uint)((BitHelper.GetValue(opcode, 25, 7) << 5) | BitHelper.GetValue(opcode, 7, 5)), 12);
            var rs2 = (int)BitHelper.GetValue(opcode, 20, 5);
            var rs1 = (int)BitHelper.GetValue(opcode, 15, 5);
            var rs2Value = GetRegister(rs2).RawValue;
            var rs1Value = (long)GetRegister(rs1).RawValue;
            SetMemValue(width, rs2Value, (ulong)rs1Value);
            SetRegister(rs1, (ulong)(rs1Value + imm));
        }

        private void StoreRegisterRegister(ulong opcode, Width width, string log, bool postIncrement = false)
        {
            this.Log(LogLevel.Noisy, "({0}) at PC={1:X}", log, PC.RawValue);
            // with post-increment:
            // Mem8/16/32(rs1) = rs2
            // rs1 += rs3
            // without post-increment:
            // Mem8/16/32(rs1 + rs3) = rs2
            var rs2 = (int)BitHelper.GetValue(opcode, 20, 5);
            var rs3 = (int)BitHelper.GetValue(opcode, 7, 5);
            var rs1 = (int)BitHelper.GetValue(opcode, 15, 5);
            var rs1Value = GetRegister(rs1).RawValue;
            var rs3Value = GetRegister(rs3).RawValue;
            var rs2Value = GetRegister(rs2).RawValue;
            SetMemValue(width, rs2Value, postIncrement ? rs1Value : rs1Value + rs3Value);
            if(postIncrement)
            {
                SetRegister(rs1, rs1Value + rs3Value);
            }
        }

        private void CompareRegisters(ulong opcode, ComparisonType type, Sign sign, string log)
        {
            this.Log(LogLevel.Noisy, "({0}) at PC=0x{1:X}", log, PC.RawValue);
            var rD = (int)BitHelper.GetValue(opcode, 7, 5);
            var rs1 = (int)BitHelper.GetValue(opcode, 15, 5);
            var rs1Value = GetRegister(rs1).RawValue;
            var rs2 = (int)BitHelper.GetValue(opcode, 20, 5);
            var rs2Value = GetRegister(rs2).RawValue;
            var result = 0UL;
            if(type == ComparisonType.Min && sign == Sign.Signed)
            {
                result = (int)rs1Value < (int)rs2Value ? rs1Value : rs2Value;
            }
            else if(type == ComparisonType.Min && sign == Sign.Unsigned)
            {
                result = rs1Value < rs2Value ? rs1Value : rs2Value;
            }
            else if(type == ComparisonType.Max && sign == Sign.Signed)
            {
                result = (int)rs1Value < (int)rs2Value ? rs2Value : rs1Value;
            }
            else if(type == ComparisonType.Max && sign == Sign.Unsigned)
            {
                result = rs1Value < rs2Value ? rs2Value : rs1Value;
            }
            else
            {
                this.Log(LogLevel.Error, "Could not execute compare instruction: {0}", log);
            }
            SetRegister(rD, result);
        }

        private void ManipulateBitsInRegister(ulong opcode, Source source, Operation operation, Width width, Sign sign, string log)
        {
            this.Log(LogLevel.Noisy, "({0}) at PC={1:X}", log, PC.RawValue);
            var rD = (int)BitHelper.GetValue(opcode, 7, 5);
            var rs1 = (int)BitHelper.GetValue(opcode, 15, 5);
            var is2 = (int)BitHelper.GetValue(opcode, 20, 5);
            var is3 = (int)BitHelper.GetValue(opcode, 25, 5);
            var rs1Value = GetRegister(rs1).RawValue;
            if(source == Source.Register)
            {
                var rs2 = (int)BitHelper.GetValue(opcode, 20, 5);
                var rs2Value = GetRegister(rs2).RawValue;
                is2 = (int)BitHelper.GetValue(rs2Value, 0, 5);
                is3 = (int)BitHelper.GetValue(rs2Value, 5, 5);
            }
            var rDValue = GetRegister(rD).RawValue;
            if(is2 + is3 > 32)
            {
                this.Log(LogLevel.Error, "Sum of operands Is3 an Is2 is equal to {0} but should not be larger than 32", is2 + is3);
                return;
            }
            var result = 0UL;
            switch(operation)
            {
                case Operation.Set:
                    // p.bset:
                    // rD = rs1 | (((1 << (Is3 + 1)) - 1) << Is2)
                    // p.bsetr:
                    // rD = rs1 | (((1 << (rs2[9:5]+1)) - 1) << rs2[4:0])
                    result = rs1Value | ((ulong)((1 << (is3 + 1)) - 1) << is2);
                    break;
                case Operation.Insert:
                    // p.insert:
                    // rD = rD | (rs1[Is3:0] << Is2)
                    // p.insertr:
                    // rD = rD | (rs1[rs2[9:5]:0] << rs2[4:0])
                    result = rDValue | (BitHelper.GetValue(rs1Value, 0, is3 + 1) << is2);
                    break;
                case Operation.Extract:
                    result = ExtractBits(width, sign, is2, is3, rs1Value);
                    break;
                case Operation.Clear:
                    // p.bclr:
                    // rD = rs1 & ~(((1 << (Is3 + 1)) - 1) << Is2)
                    // p.bclrr:
                    // rD = rs1 & ~(((1 << (rs2[9:5]+1)) - 1) << rs2[4:0])
                    result = rs1Value & (~(ulong)(((1 << (is3 + 1)) - 1) << is2));
                    break;
                default:
                    this.Log(LogLevel.Error, "Encountered an unexpected option: {0}", sign);
                    break;
            }
            SetRegister(rD, result);
        }

        private void BranchIf(ulong opcode, Equality equality, string log)
        {
            this.Log(LogLevel.Noisy, "({0}) at PC={1:X}", log, PC.RawValue);
            var rs1 = (int)BitHelper.GetValue(opcode, 15, 5);
            var rs1Value = (long)GetRegister(rs1).RawValue;
            var imm5 = (int)BitHelper.SignExtend((uint)BitHelper.GetValue(opcode, 20, 5), 5);
            if((equality == Equality.NotEqual && rs1Value != imm5) || (equality == Equality.Equal && rs1Value == imm5))
            {
                var imm12 = (int)BitHelper.SignExtend((uint)((BitHelper.GetValue(opcode, 31, 1) << 11) | (BitHelper.GetValue(opcode, 7, 1) << 10) | (BitHelper.GetValue(opcode, 25, 6) << 4) | BitHelper.GetValue(opcode, 8, 4)), 12);
                var newPC = (uint)((uint)GetRegister((int)RiscV32Registers.PC).RawValue + (imm12 << 1));
                PC = newPC;
            }
        }

        private ulong ExtractBits(Width width, Sign sign, int is2, int is3, ulong rs1Value)
        {
            // This seems to be contradicting the documentation, but experimentally
            // makes things work.
            is3++;
            var result = 0UL;
            switch(width)
            {
                case Width.Byte:
                    // p.extbs rD, rs1
                    // rD = Sext(rs1[7:0])
                    // p.extbz rD, rs1
                    // rD = Zext(rs1[7:0])
                    result = sign == Sign.Signed
                        ? BitHelper.SignExtend((uint)BitHelper.GetValue(rs1Value, 0, 8), 8)
                        : BitHelper.GetValue(rs1Value, 0, 8);
                    break;
                case Width.HalfWord:
                    // p.exths rD, rs1
                    // rD = Sext(rs1[15:0])
                    // p.exthz rD, rs1
                    // rD = Zext(rs1[15:0])
                    result = sign == Sign.Signed
                        ? BitHelper.SignExtend((uint)BitHelper.GetValue(rs1Value, 0, 16), 16)
                        : BitHelper.GetValue(rs1Value, 0, 16);
                    break;
                case Width.Word:
                    // p.extract rD, rs1, Is3, Is2
                    // rD = Sext((rs1 & ((1 << Is3) - 1) << Is2) >> Is2)
                    // p.extractu rD, rs1, Is3, Is2
                    // rD = Zext((rs1 & ((1 << Is3) - 1) << Is2) >> Is2)
                    // p.extractr rD, rs1, rs2
                    // rD = Sext((rs1 & ((1 << rs2[9:5]) - 1) << rs2[4:0]) >> rs2[4:0])
                    // p.extractur rD, rs1, rs2
                    // rD = Zext((rs1 & ((1 << rs2[9:5]) - 1) << rs2[4:0]) >> rs2[4:0])
                    var temp = ((int)rs1Value & ((1 << is3) - 1) << is2) >> is2;
                    result = sign == Sign.Signed
                        ? BitHelper.SignExtend((uint)temp, 32 - is2)
                        : (uint)temp;
                    break;
                default:
                    this.Log(LogLevel.Error, "Encountered an unexpected option: {0}", sign);
                    break;
            }
            return result;
        }

        private void SetMemValue(Width width, ulong value, ulong address)
        {
            switch(width)
            {
                case Width.Byte:
                    WriteByteToBus(address, (uint)value);
                    break;
                case Width.HalfWord:
                    WriteWordToBus(address, (uint)value);
                    break;
                case Width.Word:
                    WriteDoubleWordToBus(address, (uint)value);
                    break;
                default:
                    this.Log(LogLevel.Error, "Encountered an unexpected option: {0}", width);
                    break;
            }
        }

        private ulong GetMemValue(Width width, BitExtension extension, ulong address)
        {
            var mem = 0UL;
            switch(width)
            {
                case Width.Byte:
                    mem = extension == BitExtension.Sign ? BitHelper.SignExtend(ReadByteFromBus(address), 8) : ReadByteFromBus(address);
                    break;
                case Width.HalfWord:
                    mem = extension == BitExtension.Sign ? BitHelper.SignExtend(ReadWordFromBus(address), 16) : ReadWordFromBus(address);
                    break;
                case Width.Word:
                    mem = ReadDoubleWordFromBus(address);
                    break;
                default:
                    this.Log(LogLevel.Error, "Encountered an unexpected option: {0}", width);
                    break;
            }
            return mem;
        }

        private void LogUnsupported(string text)
        {
            this.Log(LogLevel.Error, "Encountered unsupported instruction: ({0}) at PC={1:X}", text, PC);
        }

        private enum Equality
        {
            Equal,
            NotEqual
        }

        private enum ComparisonType
        {
            Max,
            Min
        }

        private enum Sign
        {
            Signed,
            Unsigned
        }

        private enum Width
        {
            Byte,
            HalfWord,
            Word
        }

        private enum Operation
        {
            Extract,
            Insert,
            Clear,
            Set
        }

        private enum Source
        {
            Register,
            Immediate
        }
        private enum BitExtension
        {
            Sign,
            Zero
        }

        private enum CustomCSR
        {
            PerformanceCounterMode = 0x7a1,
            StackCheckEnable = 0x7d0,
            StackBase = 0x7d1,
            StackEnd = 0x7d2,
            HardwareLoop0Start = 0x7c0,
            HardwareLoop0End = 0x7c1,
            HardwareLoop0Counter = 0x7c2,
            HardwareLoop1Start = 0x7c4,
            HardwareLoop1End = 0x7d5,
            HardwareLoop1Counter = 0x7d6
        }
    }
}
