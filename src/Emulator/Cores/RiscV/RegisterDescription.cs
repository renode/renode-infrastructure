//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    // Those methods use RiscV32Registers enum and assume those values are also valid for rv64
    public static class RiscVRegisterDescription
    {
        public static void AddCpuFeature(ref List<GDBFeatureDescriptor> features, uint registerWidth)
        {
            var cpuGroup = new GDBFeatureDescriptor("org.gnu.gdb.riscv.cpu");
            var intType = $"uint{registerWidth}";

            for(var index = 0u; index < NumberOfXRegisters; ++index)
            {
                cpuGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.X0 + index, registerWidth, $"x{index}", intType, "general"));
            }

            cpuGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.PC, registerWidth, "pc", "code_ptr", "general"));
            features.Add(cpuGroup);
        }

        public static void AddFpuFeature(ref List<GDBFeatureDescriptor> features, uint registerWidth, bool extensionH, bool extensionF, bool extensionD, bool extensionQ)
        {
            if(!(extensionH || extensionF || extensionD || extensionQ))
            {
                return;
            }

            var fpuGroup = new GDBFeatureDescriptor("org.gnu.gdb.riscv.fpu");
            var intType = $"uint{registerWidth}";
            var fWidth = 0u;
            var types = new List<string>();

            if(extensionH)
            {
                fWidth = 16u;
                types.Add("half");

                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("sign", 15, 15, "uint16"));
                fields.Add(new GDBTypeBitField("exponent", 10, 14, "uint16"));
                fields.Add(new GDBTypeBitField("fraction", 0, 9, "uint16"));
                var half = GDBCustomType.Struct("half", fWidth / 8, fields);
                fpuGroup.Types.Add(half);
            }
            if(extensionF)
            {
                fWidth = 32u;
                types.Add("single");
            }
            if(extensionD)
            {
                fWidth = 64u;
                types.Add("double");
            }
            if(extensionQ)
            {
                fWidth = 128u;
                types.Add("quad");

                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("sign", 127, 127, "uint128"));
                fields.Add(new GDBTypeBitField("exponent", 112, 126, "uint128"));
                fields.Add(new GDBTypeBitField("fraction", 0, 111, "uint128"));
                var quad = GDBCustomType.Struct("quad", fWidth / 8, fields);
                fpuGroup.Types.Add(quad);
            }

            var floatType = $"ieee_{types[0]}";
            // If there's more than one float type then they're combined with union
            // narrower type is in the lowest part of the wider ones, specification calls it NaN-boxing model.
            if(types.Count > 1)
            {
                floatType = "nan_boxed_float";
                var fields = new List<GDBTypeField>();
                foreach(var type in types)
                {
                    fields.Add(new GDBTypeField(type, $"ieee_{type}"));
                }
                var nanBoxedFloat = GDBCustomType.Union(floatType, fields);
                fpuGroup.Types.Add(nanBoxedFloat);
            }

            for(var index = 0u; index < NumberOfFRegisters; ++index)
            {
                fpuGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.F0 + index, fWidth, $"f{index}", floatType, "float"));
            }

            // fflags, frm and fcsr are not implemented but are required for architecture description
            var fflagsIndex = (uint)RiscV32Registers.F0 + NumberOfFRegisters;
            fpuGroup.Registers.Add(new GDBRegisterDescriptor(fflagsIndex, registerWidth, "fflags", "", "float"));
            fpuGroup.Registers.Add(new GDBRegisterDescriptor(fflagsIndex + 1, registerWidth, "frm", "", "float"));
            fpuGroup.Registers.Add(new GDBRegisterDescriptor(fflagsIndex + 2, registerWidth, "fcsr", "", "float"));

            {
                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("NX", 0, 0, "bool"));
                fields.Add(new GDBTypeBitField("UF", 1, 1, "bool"));
                fields.Add(new GDBTypeBitField("OF", 2, 2, "bool"));
                fields.Add(new GDBTypeBitField("DZ", 3, 3, "bool"));
                fields.Add(new GDBTypeBitField("NV", 4, 4, "bool"));
                var fflagsFlagsType = GDBCustomType.Flags("fflags_flags_type", 1, fields);
                fpuGroup.Types.Add(fflagsFlagsType);
            }
            {
                var fields = new List<GDBTypeEnumValue>();
                fields.Add(new GDBTypeEnumValue("RNE", 0b000));
                fields.Add(new GDBTypeEnumValue("RTZ", 0b001));
                fields.Add(new GDBTypeEnumValue("RDN", 0b010));
                fields.Add(new GDBTypeEnumValue("RUP", 0b011));
                fields.Add(new GDBTypeEnumValue("RMM", 0b100));
                fields.Add(new GDBTypeEnumValue("DYN", 0b111));
                var frmEnumType = GDBCustomType.Enum("frm_enum_type", 1, fields);
                fpuGroup.Types.Add(frmEnumType);
            }
            {
                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("value", 0, 4, "fflags_flags_type"));
                var fflagsType = GDBCustomType.Struct("fflags_type", registerWidth / 8, fields);
                fpuGroup.Types.Add(fflagsType);
            }
            {
                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("value", 0, 5, "frm_enum_type"));
                var frmType = GDBCustomType.Struct("frm_type", registerWidth / 8, fields);
                fpuGroup.Types.Add(frmType);
            }
            {
                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("flags", 0, 4, "fflags_flags_type"));
                fields.Add(new GDBTypeBitField("rm", 5, 7, "frm_enum_type"));
                var fcsrType = GDBCustomType.Struct("fcsr_type", registerWidth / 8, fields);
                fpuGroup.Types.Add(fcsrType);
            }

            features.Add(fpuGroup);
        }

        public static void AddCSRFeature(ref List<GDBFeatureDescriptor> features, uint registerWidth, bool extensionS, bool extensionU, bool extensionN, bool extensionV)
        {
            var csrGroup = new GDBFeatureDescriptor("org.gnu.gdb.riscv.csr");
            var intType = $"uint{registerWidth}";

            {
                var fields = new List<GDBTypeEnumValue>();
                fields.Add(new GDBTypeEnumValue("32", 1));
                fields.Add(new GDBTypeEnumValue("64", 2));
                fields.Add(new GDBTypeEnumValue("128", 3));
                var misaMxlType = GDBCustomType.Enum("xl_type", 2, fields);
                csrGroup.Types.Add(misaMxlType);
            }
            {
                var fields = new List<GDBTypeEnumValue>();
                fields.Add(new GDBTypeEnumValue("All off", 0));
                fields.Add(new GDBTypeEnumValue("None dirty or clean, some on", 1));
                fields.Add(new GDBTypeEnumValue("None dirty, some clean", 2));
                fields.Add(new GDBTypeEnumValue("Some dirty", 3));
                var xsType = GDBCustomType.Enum("xs_type", 1, fields);
                csrGroup.Types.Add(xsType);
            }
            {
                var fields = new List<GDBTypeEnumValue>();
                fields.Add(new GDBTypeEnumValue("Off", 0));
                fields.Add(new GDBTypeEnumValue("Initial", 1));
                fields.Add(new GDBTypeEnumValue("Clean", 2));
                fields.Add(new GDBTypeEnumValue("Dirty", 3));
                var fsType = GDBCustomType.Enum("fs_type", 1, fields);
                csrGroup.Types.Add(fsType);
            }
            {
                var fields = new List<GDBTypeEnumValue>();
                fields.Add(new GDBTypeEnumValue("U", 0b00));
                fields.Add(new GDBTypeEnumValue("S", 0b01));
                fields.Add(new GDBTypeEnumValue("M", 0b11));
                var privType = GDBCustomType.Enum("priv_type", 1, fields);
                csrGroup.Types.Add(privType);
            }
            {
                var fields = new List<GDBTypeEnumValue>();
                fields.Add(new GDBTypeEnumValue("Off", 0));
                fields.Add(new GDBTypeEnumValue("Initial", 1));
                fields.Add(new GDBTypeEnumValue("Clean", 2));
                fields.Add(new GDBTypeEnumValue("Dirty", 3));
                var vsType = GDBCustomType.Enum("vs_type", 1, fields);
                csrGroup.Types.Add(vsType);
            }
            {
                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("UIE", 0, 0, "bool"));
                fields.Add(new GDBTypeBitField("SIE", 1, 1, "bool"));
                fields.Add(new GDBTypeBitField("MIE", 3, 3, "bool"));
                fields.Add(new GDBTypeBitField("UPIE", 4, 4, "bool"));
                fields.Add(new GDBTypeBitField("SPIE", 5, 5, "bool"));
                fields.Add(new GDBTypeBitField("MPIE", 7, 7, "bool"));
                fields.Add(new GDBTypeBitField("SPP", 8, 8, "priv_type"));
                fields.Add(new GDBTypeBitField("VS", 9, 10, "vs_type"));
                fields.Add(new GDBTypeBitField("MPP", 11, 12, "priv_type"));
                fields.Add(new GDBTypeBitField("FS", 13, 14, "fs_type"));
                fields.Add(new GDBTypeBitField("XS", 15, 16, "xs_type"));
                fields.Add(new GDBTypeBitField("MPRIV", 17, 17, "bool"));
                fields.Add(new GDBTypeBitField("SUM", 18, 18, "bool"));
                fields.Add(new GDBTypeBitField("MXR", 19, 19, "bool"));
                fields.Add(new GDBTypeBitField("TVM", 20, 20, "bool"));
                fields.Add(new GDBTypeBitField("TW", 21, 21, "bool"));
                fields.Add(new GDBTypeBitField("TSR", 22, 22, "bool"));
                if(registerWidth == 32)
                {
                    fields.Add(new GDBTypeBitField("SD", 31, 31, "bool"));
                }
                else if(registerWidth == 64)
                {
                    fields.Add(new GDBTypeBitField("UXL", 32, 33, "xl_type"));
                    fields.Add(new GDBTypeBitField("SXL", 34, 35, "xl_type"));
                    fields.Add(new GDBTypeBitField("SD", 63, 63, "boolc"));
                }
                else
                {
                    throw new NotImplementedException($"There is no type definition for MSTATUS with register width {registerWidth}.");
                }
                var mstatusType = GDBCustomType.Struct("mstatus_type", registerWidth / 8, fields);
                csrGroup.Types.Add(mstatusType);
            }
            {
                var fields = new List<GDBTypeBitField>();
                for(var c = 'A'; c <= 'Z'; ++c)
                {
                    var index = (uint)(c - 'A');
                    fields.Add(new GDBTypeBitField($"{c}", index, index, "bool"));
                }
                var misaExtensionsType = GDBCustomType.Flags("misa_extensions_type", 4, fields);
                csrGroup.Types.Add(misaExtensionsType);
            }
            {
                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("MXL", registerWidth - 2, registerWidth - 1, "xl_type"));
                fields.Add(new GDBTypeBitField("Extensions", 0, 25, "misa_extensions_type"));
                var misaType = GDBCustomType.Struct("misa_type", registerWidth / 8, fields);
                csrGroup.Types.Add(misaType);
            }
            {
                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("USIP", 0, 0, "bool"));
                fields.Add(new GDBTypeBitField("SSIP", 1, 1, "bool"));
                fields.Add(new GDBTypeBitField("MSIP", 3, 3, "bool"));
                fields.Add(new GDBTypeBitField("UTIP", 4, 4, "bool"));
                fields.Add(new GDBTypeBitField("STIP", 5, 5, "bool"));
                fields.Add(new GDBTypeBitField("MTIP", 7, 7, "bool"));
                fields.Add(new GDBTypeBitField("UEIP", 8, 8, "bool"));
                fields.Add(new GDBTypeBitField("SEIP", 9, 9, "bool"));
                fields.Add(new GDBTypeBitField("MEIP", 11, 11, "bool"));
                var mipType = GDBCustomType.Flags("mip_type", registerWidth / 8, fields);
                csrGroup.Types.Add(mipType);
            }
            {
                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("USIE", 0, 0, "bool"));
                fields.Add(new GDBTypeBitField("SSIE", 1, 1, "bool"));
                fields.Add(new GDBTypeBitField("MSIE", 3, 3, "bool"));
                fields.Add(new GDBTypeBitField("UTIE", 4, 4, "bool"));
                fields.Add(new GDBTypeBitField("STIE", 5, 5, "bool"));
                fields.Add(new GDBTypeBitField("MTIE", 7, 7, "bool"));
                fields.Add(new GDBTypeBitField("UEIE", 8, 8, "bool"));
                fields.Add(new GDBTypeBitField("SEIE", 9, 9, "bool"));
                fields.Add(new GDBTypeBitField("MEIE", 11, 11, "bool"));
                var mieType = GDBCustomType.Flags("mie_type", registerWidth / 8, fields);
                csrGroup.Types.Add(mieType);
            }
            {
                var fields = new List<GDBTypeEnumValue>();
                fields.Add(new GDBTypeEnumValue("Direct", 0));
                fields.Add(new GDBTypeEnumValue("Vectored", 1));
                var mtvecModeType = GDBCustomType.Enum("tvec_mode_type", 1, fields);
                csrGroup.Types.Add(mtvecModeType);
            }
            {
                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("MODE", 0, 1, "tvec_mode_type"));
                fields.Add(new GDBTypeBitField("BASE", 2, registerWidth - 1, "code_ptr"));
                var tvecType = GDBCustomType.Struct("tvec_type", registerWidth / 8, fields);
                csrGroup.Types.Add(tvecType);
            }
            {
                var fields = new List<GDBTypeBitField>();
                fields.Add(new GDBTypeBitField("Interrupt", registerWidth - 1, registerWidth - 1, "bool"));
                fields.Add(new GDBTypeBitField("Exception Code", 0, registerWidth - 2, intType));
                var mcauseType = GDBCustomType.Struct("cause_type", registerWidth / 8, fields);
                csrGroup.Types.Add(mcauseType);
            }

            if(extensionS)
            {
                {
                    var fields = new List<GDBTypeBitField>();
                    fields.Add(new GDBTypeBitField("UIE", 0, 0, "bool"));
                    fields.Add(new GDBTypeBitField("SIE", 1, 1, "bool"));
                    fields.Add(new GDBTypeBitField("UPIE", 4, 4, "bool"));
                    fields.Add(new GDBTypeBitField("SPIE", 5, 5, "bool"));
                    fields.Add(new GDBTypeBitField("SPP", 8, 8, "priv_type"));
                    fields.Add(new GDBTypeBitField("FS", 13, 14, "fs_type"));
                    fields.Add(new GDBTypeBitField("XS", 15, 16, "xs_type"));
                    fields.Add(new GDBTypeBitField("SUM", 18, 18, "bool"));
                    fields.Add(new GDBTypeBitField("MXR", 19, 19, "bool"));
                    if(registerWidth == 32)
                    {
                        fields.Add(new GDBTypeBitField("SD", 31, 31, "bool"));
                    }
                    else if(registerWidth == 64)
                    {
                        fields.Add(new GDBTypeBitField("UXL", 32, 33, "xl_type"));
                        fields.Add(new GDBTypeBitField("SD", 63, 63, "boolc"));
                    }
                    else
                    {
                        throw new NotImplementedException($"There is no type definition for SSTATUS with register width {registerWidth}.");
                    }
                    var sstatusType = GDBCustomType.Struct("sstatus_type", registerWidth / 8, fields);
                    csrGroup.Types.Add(sstatusType);
                }
                {
                    var fields = new List<GDBTypeBitField>();
                    fields.Add(new GDBTypeBitField("USIP", 0, 0, "bool"));
                    fields.Add(new GDBTypeBitField("SSIP", 1, 1, "bool"));
                    fields.Add(new GDBTypeBitField("UTIP", 4, 4, "bool"));
                    fields.Add(new GDBTypeBitField("STIP", 5, 5, "bool"));
                    fields.Add(new GDBTypeBitField("UEIP", 8, 8, "bool"));
                    fields.Add(new GDBTypeBitField("SEIP", 9, 9, "bool"));
                    var sipType = GDBCustomType.Flags("sip_type", registerWidth / 8, fields);
                    csrGroup.Types.Add(sipType);
                }
                {
                    var fields = new List<GDBTypeBitField>();
                    fields.Add(new GDBTypeBitField("USIE", 0, 0, "bool"));
                    fields.Add(new GDBTypeBitField("SSIE", 1, 1, "bool"));
                    fields.Add(new GDBTypeBitField("UTIE", 4, 4, "bool"));
                    fields.Add(new GDBTypeBitField("STIE", 5, 5, "bool"));
                    fields.Add(new GDBTypeBitField("UEIE", 8, 8, "bool"));
                    fields.Add(new GDBTypeBitField("SEIE", 9, 9, "bool"));
                    var sieType = GDBCustomType.Flags("sie_type", registerWidth / 8, fields);
                    csrGroup.Types.Add(sieType);
                }
                {
                    var fields = new List<GDBTypeEnumValue>();
                    fields.Add(new GDBTypeEnumValue("Bare", 0));
                    fields.Add(new GDBTypeEnumValue("Sv32", 1));
                    fields.Add(new GDBTypeEnumValue("Sv39", 8));
                    fields.Add(new GDBTypeEnumValue("Sv48", 9));
                    fields.Add(new GDBTypeEnumValue("Sv57", 10));
                    fields.Add(new GDBTypeEnumValue("Sv64", 11));
                    var satpModeType = GDBCustomType.Enum("satp_mode_type", 1, fields);
                    csrGroup.Types.Add(satpModeType);
                }
                {
                    var fields = new List<GDBTypeBitField>();
                    if(registerWidth == 32)
                    {
                        fields.Add(new GDBTypeBitField("PPN", 0, 21, "data_ptr"));
                        fields.Add(new GDBTypeBitField("ASID", 22, 30, "data_ptr"));
                        fields.Add(new GDBTypeBitField("MODE", 31, 31, "satp_mode_type"));
                    }
                    else if(registerWidth == 64)
                    {
                        fields.Add(new GDBTypeBitField("PPN", 0, 43, "data_ptr"));
                        fields.Add(new GDBTypeBitField("ASID", 44, 59, "data_ptr"));
                        fields.Add(new GDBTypeBitField("MODE", 60, 63, "satp_mode_type"));
                    }
                    else
                    {
                        throw new NotImplementedException($"There is no type definition for SATP with register width {registerWidth}.");
                    }
                    var satpType = GDBCustomType.Struct("satp_type", registerWidth / 8, fields);
                    csrGroup.Types.Add(satpType);
                }

                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.SSTATUS, registerWidth, "sstatus", "sstatus_type", "csr"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.SIE, registerWidth, "sie", "sie_type", "csr"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.SIP, registerWidth, "sip", "sip_type", "csr"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.STVEC, registerWidth, "stvec", "tvec_type", "csr"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.SSCRATCH, registerWidth, "sscratch", "data_ptr", "csr"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.SEPC, registerWidth, "sepc", "code_ptr", "csr"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.SCAUSE, registerWidth, "scause", "cause_type", "csr"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.STVAL, registerWidth, "stval", intType, "csr"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.SATP, registerWidth, "satp", "satp_type", "csr"));
            }
            if(extensionS || (extensionU && extensionN))
            {
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.MEDELEG, registerWidth, "medeleg", intType, "csr"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.MIDELEG, registerWidth, "mideleg", intType, "csr"));
            }

            csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.MSTATUS, registerWidth, "mstatus", "", "csr"));
            csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.MISA, registerWidth, "misa", "", "csr"));
            csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.MIE, registerWidth, "mie", "mie_type", "csr"));
            csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.MIP, registerWidth, "mip", "mip_type", "csr"));
            csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.MTVEC, registerWidth, "mtvec", "tvec_type", "csr"));
            csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.MSCRATCH, registerWidth, "mscratch", "data_ptr", "csr"));
            csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.MEPC, registerWidth, "mepc", "code_ptr", "csr"));
            csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.MCAUSE, registerWidth, "mcause", "cause_type", "csr"));
            csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.MTVAL, registerWidth, "mtval", intType, "csr"));

            if(extensionV)
            {
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.VSTART, registerWidth, "vstart", group: "vector"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.VXSAT, registerWidth, "vxsat", group: "vector"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.VXRM, registerWidth, "vxrm", group: "vector"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.VCSR, registerWidth, "vcsr", group: "vector"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.VL, registerWidth, "vl", group: "vector"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.VTYPE, registerWidth, "vtype", group: "vector"));
                csrGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.VLENB, registerWidth, "vlenb", group: "vector"));
            }

            features.Add(csrGroup);
        }

        public static void AddVirtualFeature(ref List<GDBFeatureDescriptor> features, uint registerWidth)
        {
            var virtualGroup =  new GDBFeatureDescriptor("org.gnu.gdb.riscv.virtual");
            {
                var fields = new List<GDBTypeEnumValue>();
                fields.Add(new GDBTypeEnumValue("User/Application", 0b00));
                fields.Add(new GDBTypeEnumValue("Supervisor", 0b01));
                fields.Add(new GDBTypeEnumValue("Machine", 0b11));
                var privType = GDBCustomType.Enum("priv_type", 1, fields);
                virtualGroup.Types.Add(privType);
            }
            virtualGroup.Registers.Add(new GDBRegisterDescriptor((uint)RiscV32Registers.PRIV, registerWidth, "priv", "priv_type", "virtual"));
            features.Add(virtualGroup);
        }

        public static void AddCustomCSRFeature(ref List<GDBFeatureDescriptor> features, uint registerWidth, IReadOnlyDictionary<ulong, NonstandardCSR> customRegisters)
        {
            var customCSRGroup = new GDBFeatureDescriptor("org.gnu.gdb.riscv.custom-csr");
            var intType = $"uint{registerWidth}";

            foreach(var customRegister in customRegisters)
            {
                var name = customRegister.Value.Name;
                if(name != null)
                {
                    var number = (uint)customRegister.Key;
                    customCSRGroup.Registers.Add(new GDBRegisterDescriptor(number, registerWidth, name, intType, "custom-csr"));
                }
            }

            features.Add(customCSRGroup);
        }

        public static void AddVectorFeature(ref List<GDBFeatureDescriptor> features, uint registerWidth)
        {
            var vectorGroup = new GDBFeatureDescriptor("org.gnu.gdb.riscv.vector");

            var riscvVectorTypeFields = new List<GDBTypeField>();

            if(registerWidth / 128 > 0)
            {
                var vu128TypeID = $"vector_u128_{registerWidth / 128}";
                var vu128Type = GDBCustomType.Vector(vu128TypeID, "uint128", registerWidth / 128);
                vectorGroup.Types.Add(vu128Type);
                riscvVectorTypeFields.Add(new GDBTypeField("q", vu128TypeID));
            }

            if(registerWidth / 64 > 0)
            {
                var vu64TypeID = $"vector_u64_{registerWidth / 64}";
                var vu64Type = GDBCustomType.Vector(vu64TypeID, "uint64", registerWidth / 64);
                vectorGroup.Types.Add(vu64Type);
                riscvVectorTypeFields.Add(new GDBTypeField("l", vu64TypeID));
            }

            if(registerWidth / 32 > 0)
            {
                var vu32TypeID = $"vector_u32_{registerWidth / 32}";
                var vu32Type = GDBCustomType.Vector(vu32TypeID, "uint32", registerWidth / 32);
                vectorGroup.Types.Add(vu32Type);
                riscvVectorTypeFields.Add(new GDBTypeField("w", vu32TypeID));
            }

            if(registerWidth / 16 > 0)
            {
                var vu16TypeID = $"vector_u16_{registerWidth / 16}";
                var vu16Type = GDBCustomType.Vector(vu16TypeID, "uint16", registerWidth / 16);
                vectorGroup.Types.Add(vu16Type);
                riscvVectorTypeFields.Add(new GDBTypeField("s", vu16TypeID));
            }

            if(registerWidth / 8 > 0)
            {
                var vu8TypeID = $"vector_u8_{registerWidth / 8}";
                var vu8Type = GDBCustomType.Vector(vu8TypeID, "uint8", registerWidth / 8);
                vectorGroup.Types.Add(vu8Type);
                riscvVectorTypeFields.Add(new GDBTypeField("b", vu8TypeID));
            }
            
            var riscvVectorType = GDBCustomType.Union("riscv_vector", riscvVectorTypeFields);
            vectorGroup.Types.Add(riscvVectorType);

            for(var index = 0u; index < NumberOfVRegisters; ++index)
            {
                vectorGroup.Registers.Add(new GDBRegisterDescriptor(StartOfVRegisters + index, registerWidth, $"v{index}", "riscv_vector", "vector"));
            }
            
            features.Add(vectorGroup);
        }

        public const uint NumberOfXRegisters = 32;
        public const uint NumberOfFRegisters = 32;
        public const uint NumberOfAdditionalFRegisters = 3;
        public const uint StartOfVRegisters = 68;
        public const uint NumberOfVRegisters = 32;
    }
}
