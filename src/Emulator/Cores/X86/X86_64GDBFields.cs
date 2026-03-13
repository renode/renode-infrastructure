//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    class X86_64GDBFields
    {
        public static readonly IEnumerable<GDBTypeBitField> EflagsFlags = new List<GDBTypeBitField>
        {
            new GDBTypeBitField("CF", 0, 0, "bool"),
            new GDBTypeBitField("", 1, 1, "priv_type"), // Reserved
            new GDBTypeBitField("PF", 2, 2, "bool"),
            new GDBTypeBitField("", 3, 3, "bool"), // Reserved
            new GDBTypeBitField("AF", 4, 4, "bool"),
            new GDBTypeBitField("", 5, 5, "priv_type"), // Reserved
            new GDBTypeBitField("ZF", 6, 6, "bool"),
            new GDBTypeBitField("SF", 7, 7, "bool"),
            new GDBTypeBitField("TF", 8, 8, "bool"),
            new GDBTypeBitField("IF", 9, 9, "bool"),
            new GDBTypeBitField("DF", 10, 10, "bool"),
            new GDBTypeBitField("OF", 11, 11, "bool"),
            new GDBTypeBitField("IOPL", 12, 13, "uint8"),
            new GDBTypeBitField("NT", 14, 14, "bool"),
            new GDBTypeBitField("", 15, 15, "priv_type"), // Reserved
            new GDBTypeBitField("RF", 16, 16, "bool"),
            new GDBTypeBitField("VM", 17, 17, "bool"),
            new GDBTypeBitField("AC", 18, 18, "bool"),
            new GDBTypeBitField("VIF", 19, 19, "bool"),
            new GDBTypeBitField("VIP", 20, 20, "bool"),
            new GDBTypeBitField("ID", 21, 21, "bool"),
            new GDBTypeBitField("", 22, 63, "priv_type") // Reserved
        };

        public static readonly IEnumerable<GDBTypeBitField> Cr0Flags = new List<GDBTypeBitField>
        {
            new GDBTypeBitField("PE", 0, 0, "bool"),
            new GDBTypeBitField("MP", 1, 1, "bool"),
            new GDBTypeBitField("EM", 2, 2, "bool"),
            new GDBTypeBitField("TS", 3, 3, "bool"),
            new GDBTypeBitField("ET", 4, 4, "bool"),
            new GDBTypeBitField("NE", 5, 5, "bool"),
            new GDBTypeBitField("", 6, 15, "priv_type"), // Reserved
            new GDBTypeBitField("WP", 16, 16, "bool"),
            new GDBTypeBitField("", 17, 17, "priv_type"), // Reserved
            new GDBTypeBitField("AM", 18, 18, "bool"),
            new GDBTypeBitField("", 19, 28, "priv_type"), // Reserved
            new GDBTypeBitField("NW", 29, 29, "bool"),
            new GDBTypeBitField("CD", 30, 30, "bool"),
            new GDBTypeBitField("PG", 31, 31, "bool"),
            new GDBTypeBitField("", 32, 63, "priv_type") // Reserved
        };

        public static readonly IEnumerable<GDBTypeBitField> Cr3Flags = new List<GDBTypeBitField>
        {
            new GDBTypeBitField("PCID", 0, 11, "uint16"),
            new GDBTypeBitField("PDBR", 12, 63, "uin64")
        };

        public static readonly IEnumerable<GDBTypeBitField> Cr4Flags = new List<GDBTypeBitField>
        {
            new GDBTypeBitField("VME", 0, 0, "bool"),
            new GDBTypeBitField("PVI", 1, 1, "bool"),
            new GDBTypeBitField("TSD", 2, 2, "bool"),
            new GDBTypeBitField("DE", 3, 3, "bool"),
            new GDBTypeBitField("PSE", 4, 4, "bool"),
            new GDBTypeBitField("PAE", 5, 5, "bool"),
            new GDBTypeBitField("MCE", 6, 6, "bool"),
            new GDBTypeBitField("PGE", 7, 7, "bool"),
            new GDBTypeBitField("PCE", 8, 8, "bool"),
            new GDBTypeBitField("OSFXSR", 9, 9, "bool"),
            new GDBTypeBitField("OSXMMEXCEPT", 10, 10, "bool"),
            new GDBTypeBitField("UMIP", 11, 11, "bool"),
            new GDBTypeBitField("LA57", 12, 12, "bool"),
            new GDBTypeBitField("VMXE", 13, 13, "bool"),
            new GDBTypeBitField("SMXE", 14, 14, "bool"),
            new GDBTypeBitField("", 15, 15, "priv_type"), // Reserved
            new GDBTypeBitField("FSGSBASE", 16, 16, "bool"),
            new GDBTypeBitField("PCIDE", 17, 17, "bool"),
            new GDBTypeBitField("OSXSAVE", 18, 18, "bool"),
            new GDBTypeBitField("SMEP", 20, 20, "bool"),
            new GDBTypeBitField("SMAP", 21, 21, "bool"),
            new GDBTypeBitField("PKE", 22, 22, "bool"),
            new GDBTypeBitField("CET", 23, 23, "bool"),
            new GDBTypeBitField("PKS", 24, 24, "bool"),
            new GDBTypeBitField("", 25, 63, "priv_type") // Reserved
        };

        public static readonly IEnumerable<GDBTypeBitField> EferFlags = new List<GDBTypeBitField>
        {
            new GDBTypeBitField("SCE", 0, 0, "bool"),
            new GDBTypeBitField("", 1, 7, "priv_type"), // Reserved
            new GDBTypeBitField("LME", 8, 8, "bool"),
            new GDBTypeBitField("", 9, 9, "priv_type"), // Reserved
            new GDBTypeBitField("LMA", 10, 10, "bool"),
            new GDBTypeBitField("NXE", 11, 11, "bool"),
            new GDBTypeBitField("SVME", 12, 12, "bool"),
            new GDBTypeBitField("LMSLE", 13, 13, "bool"),
            new GDBTypeBitField("FFXSR", 14, 14, "bool"),
            new GDBTypeBitField("TCE", 15, 15, "bool"),
            new GDBTypeBitField("", 16, 63, "priv_type") // Reserved
        };
    }
}
