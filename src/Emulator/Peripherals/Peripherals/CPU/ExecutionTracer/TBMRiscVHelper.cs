//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class TBMRiscVHelper
    {
        static TBMRiscVHelper()
        {
            // Precompile some regular expressions
            VectorStoreRegex = new Regex(@"(vse|vsuxei|vsse|vsoxei)\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            AddressOffset1Regex = new Regex(@"^\d*\((\w+)\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            AddressOffset2Regex = new Regex(@"^(\w+)\s*[-+]\s*(\d+|0x[0-9a-fA-F]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            ImmediateRegex = new Regex(@"^(-?\d+|0x[0-9a-fA-F]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            VectorRegisterRegex = new Regex(@"^v(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            FloatRegisterRegex = new Regex(@"^f(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            IntegerRegisterRegex = new Regex(@"^x(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        } 

        /// <summary> Generate list of inputs and output registers from operands. </summary>
        /// <param name="mnemonic"> assembly instruction mnemonic </param>
        /// <param name="operands"> operands </param>
        /// <returns> inputs: input registers outputs: output registers </returns>
        public static Tuple<string[], string[]> AsmRegisters(string mnemonic, string[] operands)
        {
            string[] inputOps;
            string[] outputOps;

            if(mnemonic == "sb" || mnemonic == "sh" || mnemonic == "sw" || mnemonic == "sbu" || mnemonic == "shu" || mnemonic == "fsw" || mnemonic == "fsd" || VectorStoreRegex.Match(mnemonic).Success)
            {
                // store
                inputOps = operands;
                outputOps = new string[0];
            }
            else if(mnemonic == "j" || mnemonic == "jr" || mnemonic == "c.j" || mnemonic.StartsWith("b"))
            {
                // jump/branch
                inputOps = operands;
                outputOps = new string[0];
            }
            else if((mnemonic == "jal" || mnemonic == "jalr") && operands.Length == 1)
            {
                // pseudo-instructions
                inputOps = operands; 
                outputOps = new string[] { "x1" };
            }
            else
            {
                // default behaviour: first operand is destination, remainder are outputs
                inputOps = operands.Skip(1).ToArray();
                outputOps = operands.Take(1).ToArray();
            }

            var inputs = inputOps.Select(o => InputReg(o)).Where(r => r != null).ToArray();
            var outputs = outputOps.Where(o => !o.StartsWith("0")).ToArray();

            // Add implicit inputs and outputs of instructions
            if(mnemonic.StartsWith("vset"))
            {
                outputs = outputs.Concat(new string[] { "vtype", "vl" }).ToArray();
            }
            else if(mnemonic.StartsWith("v"))
            {
                inputs = inputs.Concat(new string[] { "vtype", "vl", "vstart" }).ToArray();
            }

            return Tuple.Create(Normalize(inputs), Normalize(outputs));
        }

        /// <summary> Extract a register from an input operand. </summary>
        /// <param name="operand"> input operand </param>
        /// <returns> register or null </returns>
        public static string InputReg(string operand)
        {
            var m = AddressOffset1Regex.Match(operand);
            if(m.Success)
            {
                return m.Groups[1].Value;
            }

            m = AddressOffset2Regex.Match(operand);
            if(m.Success)
            {
                return m.Groups[1].Value;
            }

            if(ImmediateRegex.Match(operand).Success)
            {
                return null;
            }

            return operand;
        }

        /// <summary> Replace ABI register names with their architectural names and remove duplicates. </summary>
        /// <param name="rs"> list of registers. </param>
        /// <returns> list of normalized registers </returns>
        public static string[] Normalize(string[] rs)
        {
            var arch = rs.Select(r => ABINames.TryGetValue(r, out var a) ? a : r);
            var result = new HashSet<string>(arch);
            result.RemoveWhere(r => BogusRegisters.Contains(r));
            return result.ToArray();
        }

        public static bool IsNop(string mnemonic)
        {
            return NopInstructions.Contains(mnemonic);
        }

        public static bool IsBranch(string mnemonic)
        {
            return BranchInstructions.Contains(mnemonic);
        }

        public static bool IsFlush(string mnemonic)
        {
            return FlushInstructions.Contains(mnemonic);
        }

        public static bool IsVctrl(string mnemonic)
        {
            return VectorControlInstructions.Contains(mnemonic);
        }

        public static bool IsVectorRegister(string reg)
        {
            return VectorRegisterRegex.Match(reg).Success;
        }

        public static bool IsFloatRegister(string reg)
        {
            return FloatRegisterRegex.Match(reg).Success;
        }

        public static bool IsIntegerRegister(string reg)
        {
            /// Assumes that ABI register names were replaced with their architectural names
            /// (e.g. x0, x1, ... instead of zero, ra, ...). It is done by <see cref="Normalize"/>.
            return IntegerRegisterRegex.Match(reg).Success;
        }

        public static bool IsVectorInstruction(string[] inputs, string[] outputs)
        {
            // Only vector instructions access vector registers
            return inputs.Any(IsVectorRegister) || outputs.Any(IsVectorRegister);
        }
        
        /// <summary> Get selected element width (SEW) from vector selected element width register (vsew). </summary>
        /// <param name="vsew"> vsew register value </param>
        /// <returns> selected element width </returns>
        public static byte GetSelectedElementWidth(ulong vsew)
        {
            switch(vsew)
            {
                case 0b000:
                    return 8;
                case 0b001:
                    return 16;
                case 0b010:
                    return 32;
                case 0b011:
                    return 64;
                default:
                    // Reserved
                    return 0;
            }
        }
        
        /// <summary> Get vector length multiplier (LMUL) from vector length multiplier register (vlmul). </summary>
        /// <param name="vlmul"> vlmul register value </param>
        /// <returns> vector length multiplier </returns>
        public static float GetVectorLengthMultiplier(ulong vlmul)
        {
            switch(vlmul)
            {
                case 0b000:
                    return 1;
                case 0b001:
                    return 2;
                case 0b010:
                    return 4;
                case 0b011:
                    return 8;
                case 0b111:
                    return 1/2;
                case 0b110:
                    return 1/4;
                case 0b101:
                    return 1/8;
                default:
                    // Reserved
                    return 0;
            }
        }

        private static readonly Regex VectorStoreRegex;
        private static readonly Regex AddressOffset1Regex;
        private static readonly Regex AddressOffset2Regex;
        private static readonly Regex ImmediateRegex;
        private static readonly Regex VectorRegisterRegex;
        private static readonly Regex FloatRegisterRegex;
        private static readonly Regex IntegerRegisterRegex;

        private static readonly Dictionary<string, string> ABINames = new Dictionary<string, string>()
        {
            {"zero", "x0"},
            {"ra", "x1"},
            {"sp", "x2"},
            {"gp", "x3"},
            {"tp", "x4"},
            {"t0", "x5"},
            {"t1", "x6"},
            {"t2", "x7"},
            {"s0", "x8"},
            {"s1", "x9"},
            {"a0", "x10"},
            {"a1", "x11"},
            {"a2", "x12"},
            {"a3", "x13"},
            {"a4", "x14"},
            {"a5", "x15"},
            {"a6", "x16"},
            {"a7", "x17"},
            {"s2", "x18"},
            {"s3", "x19"},
            {"s4", "x20"},
            {"s5", "x21"},
            {"s6", "x22"},
            {"s7", "x23"},
            {"s8", "x24"},
            {"s9", "x25"},
            {"s10", "x26"},
            {"s11", "x27"},
            {"t3", "x28"},
            {"t4", "x29"},
            {"t5", "x30"},
            {"t6", "x31"},
            //  This is the RVV mask register (not exactly abi).
            {"v0.t", "v0"},
        };

        private static readonly string[] BogusRegisters = 
        {
            "x0",
            "e8",
            "e16",
            "e32",
            "e64",
            "e128",
            "m1",
            "m2",
            "m4",
            "m8",
            "m16",
            "ta",
            "tu",
            "ma",
            "mu",
        };

        private static readonly string[] NopInstructions = 
        {
            "nop",
            "c.nop",
            "fence",
            "fence.i",
            "sfence.vma",
            "wfi",
        };

        private static readonly string[] BranchInstructions = 
        {
            "beq",
            "bne",
            "blt",
            "bge",
            "bltu",
            "bgeu",
            "jal",
            "jalr",
            "bnez",
            "beqz",
            "blez",
            "bgez",
            "bltz",
            "bgtz",
            "bleu",
            "bgtu",
            "j",
            "c.j",
            "jr",
            "ret",
            "sret",
            "mret",
            "ecall",
            "ebreak",
        };

        private static readonly string[] FlushInstructions = 
        {
            "csrr",
            "csrw",
            "csrs",
            "csrwi",
            "csrrw",
            "csrrs",
            "csrrc",
            "csrrwi",
            "csrrsi",
            "csrrci",
            "fence",
            "fence.i",
            "sfence.vma",
        };

        private static readonly string[] VectorControlInstructions = 
        {
            "vsetivli",
            "vsetvli",
            "vsetvl",
        };

        // Incomplete list of control/status registers.
        private static readonly string[] CSRInstructions = 
        {
            "cycle",
            "cycleh",
            "dcsr",
            "dpc",
            "dscratch0",
            "dscratch1",
            "fcsr",
            "fflags",
            "frm",
            "hcounteren",
            "hedeleg",
            "hgatp",
            "hgeie",
            "hgeip",
            "hideleg",
            "hie",
            "hip",
            "hstatus",
            "htimedelta",
            "htimedeltah",
            "htinst",
            "htval",
            "hvip",
            "instret",
            "instreth",
            "marchid",
            "mcause",
            "mcontext",
            "mcounteren",
            "mcountinhibit",
            "mcycle",
            "medeleg",
            "mepc",
            "mhartid",
            "mideleg",
            "mie",
            "mimpid",
            "minstret",
            "mintstatus",
            "mip",
            "misa",
            "mnxti",
            "mscratch",
            "mscratchcsw",
            "mscratchcswl",
            "mstatus",
            "mtinst",
            "mtval",
            "mtval2",
            "mtvec",
            "mtvt",
            "mvendorid",
            "pmpaddr0",
            "pmpaddr1",
            "pmpaddr10",
            "pmpaddr11",
            "pmpaddr12",
            "pmpaddr13",
            "pmpaddr14",
            "pmpaddr15",
            "pmpaddr2",
            "pmpaddr3",
            "pmpaddr4",
            "pmpaddr5",
            "pmpaddr6",
            "pmpaddr7",
            "pmpaddr8",
            "pmpaddr9",
            "pmpcfg0",
            "pmpcfg1",
            "pmpcfg2",
            "pmpcfg3",
            "satp",
            "scause",
            "scontext",
            "scounteren",
            "sedeleg",
            "sentropy",
            "sepc",
            "sideleg",
            "sie",
            "sintstatus",
            "sip",
            "snxti",
            "sscratch",
            "sscratchcsw",
            "sscratchcswl",
            "sstatus",
            "stval",
            "stvec",
            "stvt",
            "tcontrol",
            "tdata1",
            "tdata2",
            "tdata3",
            "time",
            "timeh",
            "tinfo",
            "tselect",
            "ucause",
            "uepc",
            "uie",
            "uintstatus",
            "uip",
            "unxti",
            "uscratch",
            "uscratchcsw",
            "uscratchcswl",
            "ustatus",
            "utval",
            "utvec",
            "utvt",
            "vcsr",
            "vl",
            "vlenb",
            "vsatp",
            "vscause",
            "vsepc",
            "vsie",
            "vsip",
            "vsscratch",
            "vsstatus",
            "vstart",
            "vstval",
            "vstvec",
            "vtype",
            "vxrm",
            "vxsat",
        };
    }
}
