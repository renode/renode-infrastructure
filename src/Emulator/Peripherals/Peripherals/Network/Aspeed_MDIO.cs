// Copyright (c) 2026 Microsoft
// Licensed under the MIT license.
//
// Aspeed AST2600 MDIO Controller
// Register-level emulation of the separate MDIO bus controller used for
// PHY access on AST2600 (compatible: "aspeed,ast2600-mdio").
// Register layout from Linux drivers/net/mdio/mdio-aspeed.c

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Network
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class Aspeed_MDIO : IDoubleWordPeripheral, IKnownSize
    {
        public Aspeed_MDIO()
        {
            phyRegs = new uint[32];
            Reset();
        }

        public void Reset()
        {
            ctrl = 0;
            data = 0;

            Array.Clear(phyRegs, 0, phyRegs.Length);
            phyRegs[MII_BMCR] = 0x1000;
            phyRegs[MII_BMSR] = 0x796D;
            phyRegs[MII_PHYID1] = 0x001C;
            phyRegs[MII_PHYID2] = 0xC916;
            phyRegs[MII_ANAR] = 0x01E1;
            phyRegs[MII_ANLPAR] = 0x45E1;
            phyRegs[MII_1000BTCR] = 0x0300;
            phyRegs[MII_1000BTSR] = 0x7C00;
        }

        public uint ReadDoubleWord(long offset)
        {
            switch(offset)
            {
                case REG_CTRL:
                    return ctrl;
                case REG_DATA:
                    return data | IDLE_BIT;
                default:
                    return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch(offset)
            {
                case REG_CTRL:
                    ctrl = value;
                    if((value & FIRE_BIT) != 0)
                    {
                        ProcessMdioOp(value);
                        ctrl &= ~FIRE_BIT;
                    }
                    break;
                case REG_DATA:
                    data = value;
                    break;
            }
        }

        public long Size => 0x8;

        private void ProcessMdioOp(uint value)
        {
            var op = (value >> 26) & 0x3;
            var regAddr = (int)((value >> 16) & 0x1F);

            if(op == OP_READ)
            {
                uint readData = 0;
                if(regAddr < 32)
                {
                    readData = phyRegs[regAddr];
                }
                data = (data & 0xFFFF0000) | (readData & 0xFFFF);
            }
            else if(op == OP_WRITE)
            {
                var writeData = value & 0xFFFF;
                if(regAddr < 32)
                {
                    phyRegs[regAddr] = writeData;
                    phyRegs[MII_BMSR] |= BMSR_LINK_ST;
                }
            }
        }

        private uint ctrl;
        private uint data;
        private readonly uint[] phyRegs;

        private const long REG_CTRL = 0x0;
        private const long REG_DATA = 0x4;

        private const uint FIRE_BIT = 1u << 31;
        private const uint IDLE_BIT = 1u << 16;
        private const uint OP_WRITE = 0x1;
        private const uint OP_READ = 0x2;

        private const int MII_BMCR = 0;
        private const int MII_BMSR = 1;
        private const int MII_PHYID1 = 2;
        private const int MII_PHYID2 = 3;
        private const int MII_ANAR = 4;
        private const int MII_ANLPAR = 5;
        private const int MII_1000BTCR = 9;
        private const int MII_1000BTSR = 10;
        private const uint BMSR_LINK_ST = 1u << 2;
    }
}
