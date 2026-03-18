//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class Andes_AndeStarV5Extension
    {
        public static void RegisterIn(IMachine machine, RiscV32 cpu)
        {
            new Andes_AndeStarV5Extension().RegisterInternal(machine, cpu);
        }

        private void RegisterInternal(IMachine machine, RiscV32 cpu)
        {
            this.bus = machine.SystemBus;
            this.cpu = cpu;

            cpu.RegisterCSR((ushort)CustomCSR.MachineMiscellaneousControl, () => machineMiscellaneousControlValue, value =>
            {
                cpu.AllowUnalignedAccesses = BitHelper.IsBitSet(value, AllowUnalignedAccessesBit);
                machineMiscellaneousControlValue = value;
            }, "mmisc_ctl");

            // stub CSRs to make software happy
            cpu.RegisterCSR((ushort)CustomCSR.MachineExtendedStatus, () => 0x0, value => { cpu.Log(LogLevel.Warning, "Writing to the Machine Extended Status CSR (0x7c4) is currently not supported"); });
            cpu.RegisterCSR((ushort)CustomCSR.MachineCacheControl, () => 0xffffffff, value => { cpu.Log(LogLevel.Warning, "Writing to the Machine Cache Control CSR (0x7ca) is currently not supported"); });
            cpu.RegisterCSR((ushort)CustomCSR.InstructionCacheAndMemoryConfiguration, () => 0x0, value => { cpu.Log(LogLevel.Warning, "Writing to the Machine Custom read-only CSR (0xfc0) is not supported"); });
            cpu.RegisterCSR((ushort)CustomCSR.DataCacheAndMemoryConfiguration, () => 0x0, value => { cpu.Log(LogLevel.Warning, "Writing to the Machine Custom read-only CSR (0xfc1) is not supported"); });
            // Bit 16 is checked by Zephyr driver to see if CCTL is supported
            cpu.RegisterCSR((ushort)CustomCSR.MachineMiscellaneousConfiguration, () => 0x0 | (1 << 16), value => { cpu.Log(LogLevel.Warning, "Writing to the Machine Custom read-only CSR (0xfc2) is not supported"); });
            cpu.RegisterCSR((ushort)CustomCSR.MachineMiscellaneousConfigurationRV32, () => 0x0, value => { cpu.Log(LogLevel.Warning, "Writing to the Machine Custom read-only CSR (0xfc3) is not supported"); });
            cpu.RegisterCSR((ushort)CustomCSR.VectorProcessorConfiguration, () => 0x0, value => { cpu.Log(LogLevel.Warning, "Writing to the Machine Custom read-only CSR (0xfc7) is not supported"); });
            cpu.RegisterCSR((ushort)CustomCSR.ClusterCacheControlBaseAddress, () => 0x0, value => { cpu.Log(LogLevel.Warning, "Writing to the Machine Custom read-only CSR (0xfcf) is not supported"); });
            cpu.RegisterCSR((ushort)CustomCSR.Architecture, () => 0x0, value => { cpu.Log(LogLevel.Warning, "Writing to the Machine Custom read-only CSR (0xfca) is not supported"); });
            cpu.RegisterCSR((ushort)CustomCSR.CurrentStateSaveForCrashDebugging, () => 0x0, value => { cpu.Log(LogLevel.Warning, "Writing to the Machine Custom read-only CSR (0xfc8) is not supported"); });
            cpu.RegisterCSR((ushort)CustomCSR.MstatusStateSaveForCrashDebugging, () => 0x0, value => { cpu.Log(LogLevel.Warning, "Writing to the Machine Custom read-only CSR (0xfc9) is not supported"); });

            cpu.RegisterCSRStub((ushort)CustomCSR.UserITB, "UITB");
            cpu.RegisterCSRStub((ushort)CustomCSR.UserCODE, "UCODE");
            cpu.RegisterCSRStub((ushort)CustomCSR.UserCCTLBeginAddress, "UCCTLBEGINADDR");
            cpu.RegisterCSRStub((ushort)CustomCSR.UserCCTLCommand, "UCCTLCOMMAND");

            cpu.RegisterCSRStub((ushort)CustomCSR.PMAConfiguration0, "PMACFG0");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAConfiguration1, "PMACFG1");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAConfiguration2, "PMACFG2");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAConfiguration3, "PMACFG3");

            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress0, "PMAADDR0");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress1, "PMAADDR1");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress2, "PMAADDR2");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress3, "PMAADDR3");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress4, "PMAADDR4");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress5, "PMAADDR5");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress6, "PMAADDR6");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress7, "PMAADDR7");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress8, "PMAADDR8");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress9, "PMAADDR9");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress10, "PMAADDR10");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress11, "PMAADDR11");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress12, "PMAADDR12");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress13, "PMAADDR13");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress14, "PMAADDR14");
            cpu.RegisterCSRStub((ushort)CustomCSR.PMAAddress15, "PMAADDR15");

            // Custom0
            cpu.InstallCustomInstruction("iiiiiiiiiiiiiiiiii00ddddd0001011", opc => ReadFromMemoryToRegister(opc, AccessWidth.Byte, LoadExtractorByte), "LBGP");
            cpu.InstallCustomInstruction("iiiiiiiiiiiiiiiiii10ddddd0001011", opc => ReadFromMemoryToRegister(opc, AccessWidth.Byte, LoadExtractorByte, extendSign: false), "LBUGP");
            cpu.InstallCustomInstruction("iiiiiiisssssiiiiii11iiiii0001011", opc => WriteRegisterToMemory(opc, AccessWidth.Byte, StoreExtractorByte), "SBGP");
            cpu.InstallCustomInstruction("iiiiiiiiiiiiiiiiii01ddddd0001011", HandleADDIGP, "ADDIGP");

            // Custom1
            cpu.InstallCustomInstruction("iiiiiiiiiiiiiiiii001ddddd0101011", opc => ReadFromMemoryToRegister(opc, AccessWidth.Word, LoadExtractorWord), "LHGP");
            cpu.InstallCustomInstruction("iiiiiiiiiiiiiiiii101ddddd0101011", opc => ReadFromMemoryToRegister(opc, AccessWidth.Word, LoadExtractorWord, extendSign: false), "LHUGP");
            cpu.InstallCustomInstruction("iiiiiiisssssiiiii100iiiii0101011", opc => WriteRegisterToMemory(opc, AccessWidth.Word, StoreExtractorWord), "SHGP");
            cpu.InstallCustomInstruction("iiiiiiiiiiiiiiiii010ddddd0101011", opc => ReadFromMemoryToRegister(opc, AccessWidth.DoubleWord, LoadExtractorDoubleWord), "LWGP");
            cpu.InstallCustomInstruction("iiiiiiisssssiiiii100iiiii0101011", opc => WriteRegisterToMemory(opc, AccessWidth.DoubleWord, StoreExtractorDoubleWord, 19), "SWGP");
        }

        private void ReadFromMemoryToRegister(ulong opcode, AccessWidth width, Func<ulong, ulong> extractor, int immediateBits = 18, bool extendSign = true)
        {
            var rd = (int)BitHelper.GetValue(opcode, 7, 5);
            var imm = GetImmediate(opcode, extractor, immediateBits);
            var memoryOffset = imm + cpu.GetRegister(MemoryOperationsImpliedRegister).RawValue;

            int valueBits;
            ulong data;
            switch(width)
            {
            case AccessWidth.Byte:
                data = bus.ReadByte(memoryOffset, cpu);
                valueBits = 8;
                break;
            case AccessWidth.Word:
                data = bus.ReadWord(memoryOffset, cpu);
                valueBits = 16;
                break;
            case AccessWidth.DoubleWord:
                data = bus.ReadDoubleWord(memoryOffset, cpu);
                valueBits = 32;
                break;
            default:
                cpu.Log(LogLevel.Error, "Invalid memory access type: {0}", width);
                return;
            }

            if(extendSign)
            {
                cpu.SetRegister(rd, BitHelper.SignExtend(data, valueBits));
            }
            else
            {
                cpu.SetRegister(rd, data);
            }
        }

        private void WriteRegisterToMemory(ulong opcode, AccessWidth width, Func<ulong, ulong> extractor, int immediateBits = 18)
        {
            var rs2 = (int)BitHelper.GetValue(opcode, 20, 5);
            var imm = GetImmediate(opcode, extractor, immediateBits);
            var memoryOffset = imm + cpu.GetRegister(MemoryOperationsImpliedRegister).RawValue;

            var regValue = cpu.GetRegister(rs2).RawValue;

            switch(width)
            {
            case AccessWidth.Byte:
                bus.WriteByte(memoryOffset, (byte)regValue, cpu);
                break;
            case AccessWidth.Word:
                bus.WriteWord(memoryOffset, (ushort)regValue, cpu);
                break;
            case AccessWidth.DoubleWord:
                bus.WriteDoubleWord(memoryOffset, (uint)regValue, cpu);
                break;
            default:
                cpu.Log(LogLevel.Error, "Invalid memory access type: {0}", width);
                break;
            }
        }

        private void HandleADDIGP(ulong opcode)
        {
            var rd = (int)BitHelper.GetValue(opcode, 7, 5);
            var imm = GetImmediate(opcode, LoadExtractorByte, 18);
            var value = imm + cpu.GetRegister(MemoryOperationsImpliedRegister).RawValue;

            cpu.SetRegister(rd, value);
        }

        private ulong LoadExtractorByte(ulong opcode)
        {
            return LoadExtractorWord(opcode) | BitHelper.GetValue(opcode, 14, 1);
        }

        private ulong StoreExtractorByte(ulong opcode)
        {
            return StoreExtractorWord(opcode) | BitHelper.GetValue(opcode, 14, 1);
        }

        private ulong LoadExtractorWord(ulong opcode)
        {
            return BitHelper.GetValue(opcode, 21, 10) << 1 |
                BitHelper.GetValue(opcode, 20, 1) << 11 |
                BitHelper.GetValue(opcode, 17, 3) << 12 |
                BitHelper.GetValue(opcode, 15, 2) << 15 |
                BitHelper.GetValue(opcode, 31, 1) << 17;
        }

        private ulong StoreExtractorWord(ulong opcode)
        {
            return StoreExtractorDoubleWord(opcode) | (BitHelper.GetValue(opcode, 8, 1) << 1);
        }

        private ulong StoreExtractorDoubleWord(ulong opcode)
        {
            return BitHelper.GetValue(opcode, 9, 3) << 2 |
                BitHelper.GetValue(opcode, 25, 6) << 5 |
                BitHelper.GetValue(opcode, 7, 1) << 11 |
                BitHelper.GetValue(opcode, 17, 3) << 12 |
                BitHelper.GetValue(opcode, 15, 2) << 15 |
                BitHelper.GetValue(opcode, 31, 1) << 17;
        }

        private ulong LoadExtractorDoubleWord(ulong opcode)
        {
            return BitHelper.GetValue(opcode, 22, 9) << 2 |
                BitHelper.GetValue(opcode, 20, 1) << 11 |
                BitHelper.GetValue(opcode, 17, 3) << 12 |
                BitHelper.GetValue(opcode, 15, 2) << 15 |
                BitHelper.GetValue(opcode, 21, 1) << 17 |
                BitHelper.GetValue(opcode, 31, 1) << 18;
        }

        private ulong GetImmediate(ulong opcode, Func<ulong, ulong> valueExtractor, int bitCount, bool extendSign = true)
        {
            var value = valueExtractor(opcode);
            if(extendSign)
            {
                return BitHelper.SignExtend(value, bitCount);
            }
            else
            {
                return value;
            }
        }

        private ulong machineMiscellaneousControlValue;

        private RiscV32 cpu;
        private IBusController bus;

        private const int AllowUnalignedAccessesBit = 6;
        private const int MemoryOperationsImpliedRegister = 3;

        private enum AccessWidth
        {
            Byte,
            Word,
            DoubleWord,
        }

        private enum CustomCSR : ulong
        {
            MachineCacheControl = 0x7ca,
            MachineExtendedStatus = 0x7c4,
            MachineMiscellaneousControl = 0x7d0,
            InstructionCacheAndMemoryConfiguration = 0xfc0,
            DataCacheAndMemoryConfiguration = 0xfc1,
            MachineMiscellaneousConfiguration = 0xfc2,
            MachineMiscellaneousConfigurationRV32 = 0xfc3,
            VectorProcessorConfiguration = 0xfc7,
            ClusterCacheControlBaseAddress = 0xfcf,
            Architecture = 0xfca,
            CurrentStateSaveForCrashDebugging = 0xfc8,
            MstatusStateSaveForCrashDebugging = 0xfc9,
            UserITB = 0x800,
            UserCODE = 0x801,
            UserCCTLBeginAddress = 0x80b,
            UserCCTLCommand = 0x80c,

            // Andes V5 Physical Memory Attributes (PMA) CSRs
            PMAConfiguration0 = 0xbc0,
            PMAConfiguration1 = 0xbc1,
            PMAConfiguration2 = 0xbc2,
            PMAConfiguration3 = 0xbc3,
            PMAAddress0 = 0xbd0,
            PMAAddress1 = 0xbd1,
            PMAAddress2 = 0xbd2,
            PMAAddress3 = 0xbd3,
            PMAAddress4 = 0xbd4,
            PMAAddress5 = 0xbd5,
            PMAAddress6 = 0xbd6,
            PMAAddress7 = 0xbd7,
            PMAAddress8 = 0xbd8,
            PMAAddress9 = 0xbd9,
            PMAAddress10 = 0xbda,
            PMAAddress11 = 0xbdb,
            PMAAddress12 = 0xbdc,
            PMAAddress13 = 0xbdd,
            PMAAddress14 = 0xbde,
            PMAAddress15 = 0xbdf,
        }
    }
}
