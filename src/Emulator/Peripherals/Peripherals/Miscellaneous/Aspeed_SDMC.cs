//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT License.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // Aspeed AST2600 SDRAM Memory Controller (SDMC)
    // Reference: QEMU hw/misc/aspeed_sdmc.c, u-boot drivers/ram/aspeed/sdram_ast2600.c
    //
    // Large R/W register file matching QEMU behavior. Any offset can be written/read.
    // Special handling for protection key, config, status, and ECC test registers.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_SDMC : BasicDoubleWordPeripheral, IKnownSize
    {
        public Aspeed_SDMC(IMachine machine) : base(machine)
        {
            DefineRegisters();
            Reset();
        }

        public long Size => RegisterSpaceSize;

        public override void Reset()
        {
            base.Reset();
            Array.Clear(storage, 0, storage.Length);

            // Protection key: locked on reset
            storage[0x00 / 4] = ProtSoftlocked;

            // Config: default hardware config
            storage[0x04 / 4] = DefaultConfig;

            // Status1: PHY PLL locked, not busy
            storage[0x60 / 4] = PhyPllLockStatus;

            // PHY status registers at 0x400+
            storage[0x400 / 4] = 0x00000002; // PHY init done
            storage[0x430 / 4] = 0x00000001; // PHY DLL locked
            storage[0x450 / 4] = 0x0FFFFFFF; // eye window 1
            storage[0x468 / 4] = 0x000000FF; // eye window 2
            storage[0x47C / 4] = 0x000000FF; // eye window 3
            storage[0x488 / 4] = 0x000000FF; // eye window pass
            storage[0x490 / 4] = 0x000000FF; // eye window pass
            storage[0x4C8 / 4] = 0x000000FF; // eye window pass
        }

        public override uint ReadDoubleWord(long offset)
        {
            if(offset >= 0 && offset < RegisterSpaceSize)
            {
                return storage[(uint)offset / 4];
            }
            this.Log(LogLevel.Warning, "Read from offset 0x{0:X} beyond register space", offset);
            return 0;
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(offset < 0 || offset >= RegisterSpaceSize)
            {
                this.Log(LogLevel.Warning, "Write to offset 0x{0:X} beyond register space", offset);
                return;
            }

            var reg = (uint)offset / 4;

            switch((uint)offset)
            {
                case 0x00: // Protection key — transform like QEMU
                    if(value == ProtKeyUnlock)
                    {
                        storage[reg] = ProtUnlocked;
                        this.Log(LogLevel.Debug, "SDMC unlocked");
                    }
                    else if(value == ProtKeyHardlock)
                    {
                        storage[reg] = ProtHardlocked;
                        this.Log(LogLevel.Debug, "SDMC hardlocked");
                    }
                    else
                    {
                        storage[reg] = ProtSoftlocked;
                        this.Log(LogLevel.Debug, "SDMC softlocked");
                    }
                    return;

                case 0x04: // Config — preserve readonly bits
                    value = ComputeConfig(value);
                    break;

                case 0x60: // Status1 — clear busy, always set PLL lock
                    value &= ~PhyBusyState;
                    value |= PhyPllLockStatus;
                    break;

                case 0x70: // ECC test control — always done, never fail
                    value |= EccTestFinished;
                    value &= ~EccTestFail;
                    break;

                default:
                    if(!IsExemptFromProtection((uint)offset) && !IsUnlocked)
                    {
                        return;
                    }
                    break;
            }

            storage[reg] = value;
        }

        private uint ComputeConfig(uint data)
        {
            data &= ~ReadonlyConfigMask;
            return data | FixedConfig;
        }

        private bool IsUnlocked => storage[0x00 / 4] == ProtUnlocked;

        private bool IsExemptFromProtection(uint offset)
        {
            switch(offset)
            {
                case 0x00:  // R_PROT
                case 0x04:  // R_CONF (special handling)
                case 0x50:  // R_ISR
                case 0x60:  // R_STATUS1 (special handling)
                case 0x6C:  // R_MCR6C
                case 0x70:  // R_ECC_TEST_CTRL (special handling)
                case 0x74:  // R_TEST_START_LEN
                case 0x78:  // R_TEST_FAIL_DQ
                case 0x7C:  // R_TEST_INIT_VAL
                case 0x88:  // R_DRAM_SW
                case 0x8C:  // R_DRAM_TIME
                case 0xB4:  // R_ECC_ERR_INJECT
                    return true;
                default:
                    return false;
            }
        }

        private void DefineRegisters()
        {
            // No framework registers — everything goes through storage array
            // via ReadDoubleWord/WriteDoubleWord overrides
        }

        private readonly uint[] storage = new uint[RegisterSpaceSize / 4];

        // Protection key constants (per QEMU)
        private const uint ProtKeyUnlock   = 0xFC600309;
        private const uint ProtKeyHardlock = 0xDEADDEAD;
        private const uint ProtUnlocked    = 0x01;
        private const uint ProtHardlocked  = 0x10;
        private const uint ProtSoftlocked  = 0x00;

        // AST2600 1GiB: HW_VERSION=3, VGA=64MB(3), DRAM=1GiB(2)
        private const uint DefaultConfig = (3u << 28) | (3u << 2) | 2u;
        private const uint FixedConfig = DefaultConfig;
        private const uint ReadonlyConfigMask = 0xF000000F;

        private const uint PhyBusyState     = 1u << 0;
        private const uint PhyPllLockStatus = 1u << 4;
        private const uint EccTestFinished  = 1u << 12;
        private const uint EccTestFail      = 1u << 13;

        private const int RegisterSpaceSize = 0x1000;
    }
}
