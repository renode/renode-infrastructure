//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT License.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // Aspeed AST2600 System Control Unit (SCU)
    // Register offsets and reset values from QEMU hw/misc/aspeed_scu.c (ast2600_a3_resets)
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_SCU : BasicDoubleWordPeripheral, IKnownSize
    {
        public Aspeed_SCU(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x1000;

        private bool IsUnlocked => protectionKey.Value == ProtectionKeyValue;

        private void DefineRegisters()
        {
            // SCU000 - Protection Key Register
            Registers.ProtectionKey.Define(this, 0x0)
                .WithValueField(0, 32, out protectionKey, name: "PROTECTION_KEY",
                    writeCallback: (_, value) =>
                    {
                        if(value == ProtectionKeyValue)
                        {
                            this.Log(LogLevel.Debug, "SCU unlocked");
                        }
                    });

            // SCU004 - Silicon Revision ID
            Registers.SiliconRevision.Define(this, SiliconRevisionAST2600A3)
                .WithValueField(0, 32, FieldMode.Read, name: "SILICON_REV");

            // SCU010 - Protection Key 2
            Registers.ProtectionKey2.Define(this, 0x0)
                .WithValueField(0, 32, name: "PROTECTION_KEY2");

            // SCU014 - Silicon Revision 2
            Registers.SiliconRevision2.Define(this, SiliconRevisionAST2600A3)
                .WithValueField(0, 32, FieldMode.Read, name: "SILICON_REV2");

            // SCU040 - System Reset Control Register 1
            Registers.SysResetCtrl.Define(this, 0xF7C3FED8)
                .WithValueField(0, 32, name: "SYS_RST_CTRL");

            // SCU044 - System Reset Control Clear 1 (W1C - write 1 to clear bits in SysResetCtrl)
            Registers.SysResetCtrlClear.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "SYS_RST_CTRL_CLR",
                    writeCallback: (_, value) =>
                    {
                        this.Log(LogLevel.Debug, "SYS_RST_CTRL clear: 0x{0:X8}", value);
                    });

            // SCU050 - System Reset Control Register 2
            Registers.SysResetCtrl2.Define(this, 0x0DFFFFFC)
                .WithValueField(0, 32, name: "SYS_RST_CTRL2");

            // SCU054 - System Reset Control Clear 2
            Registers.SysResetCtrl2Clear.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "SYS_RST_CTRL2_CLR");

            // SCU080 - Clock Stop Control Register
            Registers.ClockStopCtrl.Define(this, 0xFFFF7F8A)
                .WithValueField(0, 32, name: "CLK_STOP_CTRL");

            // SCU084 - Clock Stop Control Clear
            Registers.ClockStopCtrlClear.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "CLK_STOP_CTRL_CLR");

            // SCU090 - Clock Stop Control Register 2
            Registers.ClockStopCtrl2.Define(this, 0xFFF0FFF0)
                .WithValueField(0, 32, name: "CLK_STOP_CTRL2");

            // SCU094 - Clock Stop Control Clear 2
            Registers.ClockStopCtrl2Clear.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "CLK_STOP_CTRL2_CLR");

            // SCU0C0 - Misc Control (Linux clk-ast2600 reads bit 12 for UART_DIV13_EN)
            // Reset value 0: UART clock = 24 MHz (matches QEMU)
            Registers.MiscCtrl.Define(this, 0x0)
                .WithValueField(0, 32, name: "MISC_CTRL");

            // SCU0C8 - Debug Control
            Registers.DebugCtrl.Define(this, 0x00000FFF)
                .WithValueField(0, 32, name: "DEBUG_CTRL");

            // SCU0D8 - Debug Control 2
            Registers.DebugCtrl2.Define(this, 0x000000FF)
                .WithValueField(0, 32, name: "DEBUG_CTRL2");

            // SCU100 - SDRAM Handshake
            Registers.SdramHandshake.Define(this, 0x00000000)
                .WithValueField(0, 32, name: "SDRAM_HANDSHAKE");

            // --- CPU scratch pad registers (0x180-0x1CC, 20 dwords) ---
            for(uint i = 0; i < 20; i++)
            {
                var offset = 0x180 + (i * 4);
                ((Registers)offset).Define(this, 0x0)
                    .WithValueField(0, 32, name: $"CPU_SCRATCH_{i}");
            }

            // --- PLL Parameter Registers ---
            // Reset values from QEMU ast2600_a3_resets[]
            // Extension registers: bit 31 = PLL lock status (always locked in emulation)

            // SCU200 - HPLL Parameter
            Registers.HPLL.Define(this, 0x1000408F)
                .WithValueField(0, 32, name: "HPLL_PARAM");

            // SCU204 - HPLL Extension (bit 31 = locked)
            Registers.HPLLExt.Define(this, 0x0)
                .WithValueField(0, 31, name: "HPLL_EXT_PARAMS")
                .WithFlag(31, FieldMode.Read, name: "HPLL_LOCK",
                    valueProviderCallback: _ => true);

            // SCU210 - APLL Parameter
            Registers.APLL.Define(this, 0x1000405F)
                .WithValueField(0, 32, name: "APLL_PARAM");

            // SCU214 - APLL Extension
            Registers.APLLExt.Define(this, 0x0)
                .WithValueField(0, 32, name: "APLL_EXT");

            // SCU220 - MPLL Parameter (DDR clock)
            Registers.MPLL.Define(this, 0x1008405F)
                .WithValueField(0, 32, name: "MPLL_PARAM");

            // SCU224 - MPLL Extension (bit 31 = locked)
            Registers.MPLLExt.Define(this, 0x0)
                .WithValueField(0, 31, name: "MPLL_EXT_PARAMS")
                .WithFlag(31, FieldMode.Read, name: "MPLL_LOCK",
                    valueProviderCallback: _ => true);

            // SCU240 - EPLL Parameter (Ethernet clock)
            Registers.EPLL.Define(this, 0x1004077F)
                .WithValueField(0, 32, name: "EPLL_PARAM");

            // SCU244 - EPLL Extension (bit 31 = locked)
            Registers.EPLLExt.Define(this, 0x0)
                .WithValueField(0, 31, name: "EPLL_EXT_PARAMS")
                .WithFlag(31, FieldMode.Read, name: "EPLL_LOCK",
                    valueProviderCallback: _ => true);

            // SCU260 - DPLL Parameter (Display clock)
            Registers.DPLL.Define(this, 0x1078405F)
                .WithValueField(0, 32, name: "DPLL_PARAM");

            // SCU264 - DPLL Extension
            Registers.DPLLExt.Define(this, 0x0)
                .WithValueField(0, 32, name: "DPLL_EXT");

            // --- Clock Source Selection ---
            // Reset values from QEMU ast2600_a3_resets[]

            // SCU300 - Clock Source Selection 1
            Registers.ClockSel1.Define(this, 0xF3940000)
                .WithValueField(0, 32, name: "CLK_SEL1");

            // SCU304 - Clock Source Selection 2
            Registers.ClockSel2.Define(this, 0x00700000)
                .WithValueField(0, 32, name: "CLK_SEL2");

            // SCU308 - Clock Source Selection 3
            Registers.ClockSel3.Define(this, 0x00000000)
                .WithValueField(0, 32, name: "CLK_SEL3");

            // SCU310 - Clock Source Selection 4
            Registers.ClockSel4.Define(this, 0xF3F40000)
                .WithValueField(0, 32, name: "CLK_SEL4");

            // SCU314 - Clock Source Selection 5
            Registers.ClockSel5.Define(this, 0x30000000)
                .WithValueField(0, 32, name: "CLK_SEL5");

            // SCU338 - UART Clock Generation
            Registers.UartClk.Define(this, 0x00014506)
                .WithValueField(0, 32, name: "UARTCLK");

            // SCU33C - High-Speed UART Clock Generation
            Registers.HUartClk.Define(this, 0x000145C0)
                .WithValueField(0, 32, name: "HUARTCLK");

            // SCU350 - Clock Duty Selection
            Registers.ClockDuty.Define(this, 0x0)
                .WithValueField(0, 32, name: "CLK_DUTY");

            // --- Miscellaneous registers accessed during boot ---

            // SCU400-4B4: Multi-function pin control / clock duty registers
            // Linux reads these during clock/pinctrl init
            for(uint offset = 0x400; offset <= 0x4FC; offset += 4)
            {
                ((Registers)offset).Define(this, 0x0)
                    .WithValueField(0, 32, name: $"PIN_CTRL_{offset:X3}");
            }

            // --- Hardware Strap registers (AST2600: at 0x500) ---

            // SCU500 - Hardware Strap 1
            Registers.HWStrap1.Define(this, AST2600_EVB_HW_STRAP1)
                .WithValueField(0, 32, name: "HW_STRAP1");

            // SCU504 - Hardware Strap 1 Clear
            Registers.HWStrap1Clear.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "HW_STRAP1_CLR");

            // SCU508 - Hardware Strap 1 Protection
            Registers.HWStrap1Prot.Define(this, 0x0)
                .WithValueField(0, 32, name: "HW_STRAP1_PROT");

            // SCU510 - Hardware Strap 2
            Registers.HWStrap2.Define(this, AST2600_EVB_HW_STRAP2)
                .WithValueField(0, 32, name: "HW_STRAP2");

            // SCU514 - Hardware Strap 2 Clear
            Registers.HWStrap2Clear.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "HW_STRAP2_CLR");

            // SCU518 - Hardware Strap 2 Protection
            Registers.HWStrap2Prot.Define(this, 0x0)
                .WithValueField(0, 32, name: "HW_STRAP2_PROT");

            // SCU524 - RNG Control
            Registers.RngCtrl.Define(this, 0x0)
                .WithValueField(0, 32, name: "RNG_CTRL");

            // SCU540 - RNG Data (returns random data on read)
            Registers.RngData.Define(this, 0x0)
                .WithValueField(0, 32, FieldMode.Read, name: "RNG_DATA",
                    valueProviderCallback: _ => (uint)rng.Next());

            // SCU5B0 - Chip ID 0
            Registers.ChipId0.Define(this, 0x1234ABCD)
                .WithValueField(0, 32, FieldMode.Read, name: "CHIP_ID0");

            // SCU5B4 - Chip ID 1
            Registers.ChipId1.Define(this, 0x88884444)
                .WithValueField(0, 32, FieldMode.Read, name: "CHIP_ID1");

            // SCU820/824/C24 - Misc config registers touched by u-boot
            Registers.MiscCtrl820.Define(this, 0x0)
                .WithValueField(0, 32, name: "MISC_CTRL_820");

            Registers.MiscCtrl824.Define(this, 0x0)
                .WithValueField(0, 32, name: "MISC_CTRL_824");

            Registers.MiscCtrlC24.Define(this, 0x0)
                .WithValueField(0, 32, name: "MISC_CTRL_C24");
        }

        // Override to provide fallback R/W storage for undefined registers.
        // QEMU's SCU stores all offsets in a flat regs[] array; we mirror that behavior.
        public override uint ReadDoubleWord(long offset)
        {
            uint result;
            if(RegistersCollection.TryRead(offset, out result))
            {
                return result;
            }
            fallbackStorage.TryGetValue(offset, out result);
            return result;
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(!RegistersCollection.TryWrite(offset, value))
            {
                fallbackStorage[offset] = value;
            }
        }

        private IValueRegisterField protectionKey;
        private readonly System.Random rng = new System.Random();
        private readonly Dictionary<long, uint> fallbackStorage = new Dictionary<long, uint>();

        private const uint SiliconRevisionAST2600A3 = 0x05030303;
        private const uint ProtectionKeyValue = 0x1688A8A8;
        // AST2600-EVB hardware strap values (from QEMU aspeed_ast2600_evb.c)
        private const uint AST2600_EVB_HW_STRAP1 = 0x000000C0;
        private const uint AST2600_EVB_HW_STRAP2 = 0x00000003;

        private enum Registers
        {
            ProtectionKey         = 0x000,
            SiliconRevision       = 0x004,
            ProtectionKey2        = 0x010,
            SiliconRevision2      = 0x014,
            SysResetCtrl          = 0x040,
            SysResetCtrlClear     = 0x044,
            SysResetCtrl2         = 0x050,
            SysResetCtrl2Clear    = 0x054,
            ClockStopCtrl         = 0x080,
            ClockStopCtrlClear    = 0x084,
            ClockStopCtrl2        = 0x090,
            ClockStopCtrl2Clear   = 0x094,
            MiscCtrl              = 0x0C0,
            DebugCtrl             = 0x0C8,
            DebugCtrl2            = 0x0D8,
            SdramHandshake        = 0x100,
            // CPU scratch pad: 0x180-0x1CC (defined dynamically)
            HPLL                  = 0x200,
            HPLLExt               = 0x204,
            APLL                  = 0x210,
            APLLExt               = 0x214,
            MPLL                  = 0x220,
            MPLLExt               = 0x224,
            EPLL                  = 0x240,
            EPLLExt               = 0x244,
            DPLL                  = 0x260,
            DPLLExt               = 0x264,
            ClockSel1             = 0x300,
            ClockSel2             = 0x304,
            ClockSel3             = 0x308,
            ClockSel4             = 0x310,
            ClockSel5             = 0x314,
            UartClk               = 0x338,
            HUartClk              = 0x33C,
            ClockDuty             = 0x350,
            // 0x400-0x4FC: Pin control (defined dynamically)
            HWStrap1              = 0x500,
            HWStrap1Clear         = 0x504,
            HWStrap1Prot          = 0x508,
            HWStrap2              = 0x510,
            HWStrap2Clear         = 0x514,
            HWStrap2Prot          = 0x518,
            RngCtrl               = 0x524,
            RngData               = 0x540,
            ChipId0               = 0x5B0,
            ChipId1               = 0x5B4,
            MiscCtrl820           = 0x820,
            MiscCtrl824           = 0x824,
            MiscCtrlC24           = 0xC24,
        }
    }
}
