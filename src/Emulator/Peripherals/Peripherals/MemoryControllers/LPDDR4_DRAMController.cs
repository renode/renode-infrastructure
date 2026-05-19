//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public partial class LPDDR4_DRAMController : BasicDoubleWordPeripheral, IKnownSize
    {
        public LPDDR4_DRAMController(IMachine machine) : base(machine)
        {
            DefineRegisters();
            Reset();
        }

        public void DefineRegisters()
        {
            Registers.INIT_DONE.Define(this, 0x0)
                .WithFlag(0, out INIT_DONE, name: "INIT_DONE")
                .WithReservedBits(1, 31);

            Registers.INIT_ERROR.Define(this, 0x0)
                .WithFlag(0, out INIT_ERROR, name: "INIT_ERROR")
                .WithReservedBits(1, 31);

            Registers.RST.Define(this, 0x0)
                .WithFlag(0, out RST, name: "RST")
                .WithReservedBits(1, 31);

            Registers.TRP.Define(this, 0x3)
                .WithValueField(0, 3, out TRP, name: "TRP")
                .WithReservedBits(3, 29);

            Registers.TRCD.Define(this, 0x2)
                .WithValueField(0, 3, out TRCD, name: "TRCD")
                .WithReservedBits(3, 29);

            Registers.TWR.Define(this, 0x2)
                .WithValueField(0, 3, out TWR, name: "TWR")
                .WithReservedBits(3, 29);

            Registers.TWTR.Define(this, 0x4)
                .WithValueField(0, 4, out TWTR, name: "TWTR")
                .WithReservedBits(4, 28);

            Registers.TREFI.Define(this, 0x125)
                .WithValueField(0, 10, out TREFI, name: "TREFI")
                .WithReservedBits(10, 22);

            Registers.TRFC.Define(this, 0xe)
                .WithValueField(0, 6, out TRFC, name: "TRFC")
                .WithReservedBits(6, 26);

            Registers.TFAW.Define(this, 0x4)
                .WithValueField(0, 4, out TFAW, name: "TFAW")
                .WithReservedBits(4, 28);

            Registers.TCCD.Define(this, 0x10)
                .WithValueField(0, 6, out TCCD, name: "TCCD")
                .WithReservedBits(6, 26);

            Registers.TCCD_WR.Define(this, 0x0)
                .WithFlag(0, out TCCD_WR, name: "TCCD_WR")
                .WithReservedBits(1, 31);

            Registers.TRTP.Define(this, 0x0)
                .WithFlag(0, out TRTP, name: "TRTP")
                .WithReservedBits(1, 31);

            Registers.TRRD.Define(this, 0x2)
                .WithValueField(0, 3, out TRRD, name: "TRRD")
                .WithReservedBits(3, 29);

            Registers.TRC.Define(this, 0x6)
                .WithValueField(0, 4, out TRC, name: "TRC")
                .WithReservedBits(4, 28);

            Registers.TRAS.Define(this, 0x4)
                .WithValueField(0, 4, out TRAS, name: "TRAS")
                .WithReservedBits(4, 28);

            Registers.PHY_INIT_REQ.Define(this, 0x0)
                .WithFlag(0, out PHY_INIT_REQ, name: "PHY_INIT_REQ")
                .WithReservedBits(1, 31);

            Registers.PHY_INIT_DONE.Define(this, 0x0)
                .WithFlag(0, mode: FieldMode.Read, valueProviderCallback: _ => PHY_INIT_REQ.Value, name: "PHY_INIT_DONE")
                .WithReservedBits(1, 31);
        }

        public long Size => 0x2000;

        public IFlagRegisterField INIT_DONE;
        public IFlagRegisterField INIT_ERROR;
        public IFlagRegisterField RST;
        public IValueRegisterField TRP;
        public IValueRegisterField TRCD;
        public IValueRegisterField TWR;
        public IValueRegisterField TWTR;
        public IValueRegisterField TREFI;
        public IValueRegisterField TRFC;
        public IValueRegisterField TFAW;
        public IValueRegisterField TCCD;
        public IFlagRegisterField TCCD_WR;
        public IFlagRegisterField TRTP;
        public IValueRegisterField TRRD;
        public IValueRegisterField TRC;
        public IValueRegisterField TRAS;
        public IFlagRegisterField PHY_INIT_REQ;

        public enum Registers
        {
            INIT_DONE = 0x0,
            INIT_ERROR = 0x4,
            RST = 0x800,
            TRP = 0x1000,
            TRCD = 0x1004,
            TWR = 0x1008,
            TWTR = 0x100c,
            TREFI = 0x1010,
            TRFC = 0x1014,
            TFAW = 0x1018,
            TCCD = 0x101c,
            TCCD_WR = 0x1020,
            TRTP = 0x1024,
            TRRD = 0x1028,
            TRC = 0x102c,
            TRAS = 0x1030,
            PHY_INIT_REQ = 0x1034,
            PHY_INIT_DONE = 0x1038,
        }
    }
}
