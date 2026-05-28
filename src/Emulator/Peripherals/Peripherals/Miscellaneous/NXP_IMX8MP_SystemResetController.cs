//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class NXP_IMX8MP_SystemResetController : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver
    {
        public NXP_IMX8MP_SystemResetController(IMachine machine, BaseCPU[] cpus) : base(machine)
        {
            if(cpus.Length != NumCores)
            {
                throw new ConstructionException($"Expected exactly {NumCores} CPUs, got {cpus.Length}");
            }
            this.cpus = cpus;
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            var savedSource = pendingWdogSource;
            pendingWdogSource = WdogSource.None;

            base.Reset();

            if(savedSource == WdogSource.None)
            {
                return;
            }

            // SRC is in the always-on domain: preserve the watchdog reset cause
            // across the machine reset so software can read SRSR on the next boot.
            ippResetStatus.Value = false;
            switch(savedSource)
            {
            case WdogSource.Wdog1:
                wdog1ResetStatus.Value = true;
                this.NoisyLog("SRSR restored: wdog1_rst_b after watchdog reset");
                break;

            case WdogSource.Wdog2:
                wdog2ResetStatus.Value = true;
                this.NoisyLog("SRSR restored: wdog2_rst_b after watchdog reset");
                break;

            case WdogSource.Wdog3:
                wdog3ResetStatus.Value = true;
                this.NoisyLog("SRSR restored: wdog3_rst_b after watchdog reset");
                break;
            }
        }

        // Only wdog1 and wdog3 are maskable wdog2 has no mask
        public void OnGPIO(int number, bool value)
        {
            if(!value)
            {
                return;
            }

            switch((GpioInput)number)
            {
            case GpioInput.Wdog1RstB:
                HandleWdogAssertion("wdog1_rst_b", wdog1ResetStatus, WdogSource.Wdog1, maskWdog1Rst);
                break;

            case GpioInput.Wdog2RstB:
                HandleWdogAssertion("wdog2_rst_b", wdog2ResetStatus, WdogSource.Wdog2);
                break;

            case GpioInput.Wdog3RstB:
                HandleWdogAssertion("wdog3_rst_b", wdog3ResetStatus, WdogSource.Wdog3, maskWdog3Rst);
                break;

            default:
                this.ErrorLog("Unexpected GPIO number {0}", number);
                break;
            }
        }

        public long Size => 0x2000;

        // Domain-control tail at bits [31:24] shared by every SRC register. Behaviour is not modeled.
        private static DoubleWordRegister RegisterWithDomainControlTags(DoubleWordRegister register)
        {
            return register
                .WithTaggedFlag("DOMAIN0", 24)
                .WithTaggedFlag("DOMAIN1", 25)
                .WithTaggedFlag("DOMAIN2", 26)
                .WithTaggedFlag("DOMAIN3", 27)
                .WithReservedBits(28, 2)
                .WithTaggedFlag("LOCK", 30)
                .WithTaggedFlag("DOM_EN", 31);
        }

        private void HandleWdogAssertion(string name, IFlagRegisterField status, WdogSource source, IValueRegisterField mask = null)
        {
            this.InfoLog("{0} asserted", name);
            status.Value = true;
            if(mask != null && mask.Value == MaskedCode)
            {
                this.NoisyLog("{0} reset masked (mask=0x{1:X}), machine reset suppressed", name, mask.Value);
                return;
            }
            pendingWdogSource = source;
            machine.RequestReset();
        }

        private void DefineRegisters()
        {
            DefineControlRegisters();
            DefineStatusRegisters();
            DefineGprRegisters();
            DefineDdrRegisters();
        }

        private void DefineControlRegisters()
        {
            RegisterWithDomainControlTags(Registers.SCR.Define(this, 0xA0)
                .WithReservedBits(0, 4)
                .WithTag("MASK_TEMPSENSE_RESET", 4, 4)
                .WithReservedBits(8, 16));

            // A53_CORE_POR_RESETx (power-on) and A53_CORE_RESETx (warm) differ on hardware in
            // what architectural state survives the reset; both restart the core at RVBAR, which is
            // the only observable behaviour we model.
            RegisterWithDomainControlTags(Registers.A53RCR0.Define(this, 0xA0000)
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "A53_CORE_POR_RESET0", writeCallback: (_, val) => { if(val) ReleaseCoreOnWrite(0); })
                .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "A53_CORE_POR_RESET1", writeCallback: (_, val) => { if(val) ReleaseCoreOnWrite(1); })
                .WithFlag(2, FieldMode.Read | FieldMode.WriteOneToClear, name: "A53_CORE_POR_RESET2", writeCallback: (_, val) => { if(val) ReleaseCoreOnWrite(2); })
                .WithFlag(3, FieldMode.Read | FieldMode.WriteOneToClear, name: "A53_CORE_POR_RESET3", writeCallback: (_, val) => { if(val) ReleaseCoreOnWrite(3); })
                .WithFlag(4, FieldMode.Read | FieldMode.WriteOneToClear, name: "A53_CORE_RESET0", writeCallback: (_, val) => { if(val) ReleaseCoreOnWrite(0); })
                .WithFlag(5, FieldMode.Read | FieldMode.WriteOneToClear, name: "A53_CORE_RESET1", writeCallback: (_, val) => { if(val) ReleaseCoreOnWrite(1); })
                .WithFlag(6, FieldMode.Read | FieldMode.WriteOneToClear, name: "A53_CORE_RESET2", writeCallback: (_, val) => { if(val) ReleaseCoreOnWrite(2); })
                .WithFlag(7, FieldMode.Read | FieldMode.WriteOneToClear, name: "A53_CORE_RESET3", writeCallback: (_, val) => { if(val) ReleaseCoreOnWrite(3); })
                .WithTaggedFlag("A53_DBG_RESET0", 8)
                .WithTaggedFlag("A53_DBG_RESET1", 9)
                .WithTaggedFlag("A53_DBG_RESET2", 10)
                .WithTaggedFlag("A53_DBG_RESET3", 11)
                .WithTaggedFlag("A53_ETM_RESET0", 12)
                .WithTaggedFlag("A53_ETM_RESET1", 13)
                .WithTaggedFlag("A53_ETM_RESET2", 14)
                .WithTaggedFlag("A53_ETM_RESET3", 15)
                .WithValueField(16, 4, out maskWdog1Rst, name: "MASK_WDOG1_RST")
                .WithTaggedFlag("A53_SOC_DBG_RESET", 20)
                .WithTaggedFlag("A53_L2RESET", 21)
                .WithReservedBits(22, 2));

            RegisterWithDomainControlTags(Registers.A53RCR1.Define(this, 0x1)
                .WithFlag(0, FieldMode.Read, name: "A53_CORE0_ENABLE", valueProviderCallback: _ => true)
                .WithFlag(1, name: "A53_CORE1_ENABLE", writeCallback: (_, val) => SetCoreEnable(1, val))
                .WithFlag(2, name: "A53_CORE2_ENABLE", writeCallback: (_, val) => SetCoreEnable(2, val))
                .WithFlag(3, name: "A53_CORE3_ENABLE", writeCallback: (_, val) => SetCoreEnable(3, val))
                .WithTag("A53_RST_SLOW", 4, 3)
                .WithReservedBits(7, 17));

            RegisterWithDomainControlTags(Registers.M7RCR.Define(this, 0xA8)
                .WithTaggedFlag("SW_M7C_NON_SCLR_RST", 0)
                .WithTaggedFlag("SW_M7C_RST", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("ENABLE_M7", 3)
                .WithValueField(4, 4, out maskWdog3Rst, name: "MASK_WDOG3_RST")
                .WithTaggedFlag("WDOG3_RST_OPTION_M7", 8)
                .WithTaggedFlag("WDOG3_RST_OPTION", 9)
                .WithReservedBits(10, 14));

            RegisterWithDomainControlTags(Registers.SUPERMIX_RCR.Define(this)
                .WithTaggedFlag("SUPERMIX_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.AUDIOMIX_RCR.Define(this)
                .WithTaggedFlag("AUDIOMIX_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.USBPHY1_RCR.Define(this)
                .WithTaggedFlag("USB1_PHY_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.USBPHY2_RCR.Define(this)
                .WithTaggedFlag("USB2_PHY_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.MLMIX_RCR.Define(this)
                .WithTaggedFlag("MLMIX_RESET", 0)
                .WithReservedBits(1, 23));

            Registers.PCIEPHY_RCR.Define(this, 0xA)
                .WithTag("PCIEPHY_RCR", 0, 32);

            RegisterWithDomainControlTags(Registers.HDMI_RCR.Define(this)
                .WithTaggedFlag("HDMI_PHY_APB_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.MEDIA_RCR.Define(this)
                .WithTaggedFlag("MEDIAMIX_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.GPU2D_RCR.Define(this)
                .WithTaggedFlag("GPU2D_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.GPU3D_RCR.Define(this)
                .WithTaggedFlag("GPU3D_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.GPU_RCR.Define(this)
                .WithTaggedFlag("GPU_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.VPU_RCR.Define(this)
                .WithTaggedFlag("VPU_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.VPU_G1_RCR.Define(this)
                .WithTaggedFlag("VPU_G1_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.VPU_G2_RCR.Define(this)
                .WithTaggedFlag("VPU_G2_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.VPUVC8KE_RCR.Define(this)
                .WithTaggedFlag("VPU_VPUVC8KE_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.NOC_RCR.Define(this)
                .WithTaggedFlag("NOC_RESET", 0)
                .WithReservedBits(1, 23));
        }

        private void DefineStatusRegisters()
        {
            Registers.SBMR1.Define(this)
                .WithTag("BOOT_CFG", 0, 20)
                .WithReservedBits(20, 12);

            Registers.SRSR.Define(this, 0x1)
                .WithFlag(0, out ippResetStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "ipp_reset_b")
                .WithReservedBits(1, 1)
                .WithTaggedFlag("csu_reset_b", 2)
                .WithTaggedFlag("ipp_user_reset_b", 3)
                .WithFlag(4, out wdog1ResetStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "wdog1_rst_b")
                .WithTaggedFlag("jtag_rst_b", 5)
                .WithTaggedFlag("jtag_sw_rst", 6)
                .WithFlag(7, out wdog3ResetStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "wdog3_rst_b")
                .WithFlag(8, out wdog2ResetStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "wdog2_rst_b")
                .WithTaggedFlag("tempsense_rst_b", 9)
                .WithReservedBits(10, 22);

            Registers.SISR.Define(this)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("USBPHY1_PASSED_RESET", 2)
                .WithTaggedFlag("USBPHY2_PASSED_RESET", 3)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("PCIE1_PHY_PASSED_RESET", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("DISPLAY_PASSED_RESET", 7)
                .WithTaggedFlag("M7C_PASSED_RESET", 8)
                .WithTaggedFlag("M7P_PASSED_RESET", 9)
                .WithTaggedFlag("GPU_PASSED_RESET", 10)
                .WithTaggedFlag("VPU_PASSED_RESET", 11)
                .WithReservedBits(12, 20);

            Registers.SIMR.Define(this, 0x0000_03FF)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("MASK_USBPHY1_PASSED_RESET", 2)
                .WithTaggedFlag("MASK_USBPHY2_PASSED_RESET", 3)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("MASK_PCIE_PHY_PASSED_RESET", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("MASK_DISPLAY_PASSED_RESET", 7)
                .WithTaggedFlag("MASK_M7C_PASSED_RESET", 8)
                .WithTaggedFlag("MASK_M7P_PASSED_RESET", 9)
                .WithTaggedFlag("MASK_GPU_PASSED_RESET", 10)
                .WithTaggedFlag("MASK_VPU_PASSED_RESET", 11)
                .WithReservedBits(12, 20);

            Registers.SBMR2.Define(this)
                .WithTag("SEC_CONFIG", 0, 2)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("BT_FUSE_SEL", 4)
                .WithTag("FORCE_COLD_BOOT", 5, 3)
                .WithReservedBits(8, 16)
                .WithTag("IPP_BOOT_MODE", 24, 4)
                .WithReservedBits(28, 4);
        }

        private void DefineGprRegisters()
        {
            gprHigh = new IValueRegisterField[NumCores];
            gprLow = new IValueRegisterField[NumCores];

            for(var i = 0; i < NumCores; i++)
            {
                var coreIndex = i;
                // Per-core RVBAR is split across GPR(2i+1)/GPR(2i+2)
                // Odd GPRs hold bits[15:0] = high 16 bits of RVBAR>>2
                // Even GPRs hold bits[21:0] = low 22 bits of RVBAR>>2 (4-byte aligned)
                ((Registers)((long)Registers.GPR1 + i * 8)).Define(this)
                    .WithValueField(0, 16, out gprHigh[i], name: $"C{i}_START_ADDRH",
                        changeCallback: (_, val) => this.NoisyLog("Core{0} RVBAR high written: 0x{1:X}", coreIndex, val))
                    .WithReservedBits(16, 16);

                ((Registers)((long)Registers.GPR2 + i * 8)).Define(this)
                    .WithValueField(0, 22, out gprLow[i], name: $"C{i}_START_ADDRL",
                        changeCallback: (_, val) => this.NoisyLog("Core{0} RVBAR low written: 0x{1:X}", coreIndex, val))
                    .WithReservedBits(22, 10);
            }

            Registers.GPR9.Define(this, 0x100)
                .WithReservedBits(0, 8)
                // Defined as reserved in the TRM. TF-A BL31 waits for this bit to be set.
                .WithTaggedFlag("HDMIMIX_MEM_REPAIR", 8)
                .WithReservedBits(9, 22);

            Registers.GPR10.Define(this)
                .WithReservedBits(0, 32);
        }

        private void DefineDdrRegisters()
        {
            RegisterWithDomainControlTags(Registers.DDRC_RCR.Define(this, 0x0000_000F)
                .WithTaggedFlag("DDRC1_PRST", 0)
                .WithTaggedFlag("DDRC1_CORE_RST", 1)
                .WithTaggedFlag("DDRC1_PHY_RESET", 2)
                .WithTaggedFlag("DDRC1_PHY_PWROKIN", 3)
                .WithTaggedFlag("DDRC1_SYS_RST", 4)
                .WithTaggedFlag("DDRC1_PHY_WRST", 5)
                .WithReservedBits(6, 18));
            RegisterWithDomainControlTags(Registers.HDMIPHY_RCR.Define(this)
                .WithTaggedFlag("HDMIPHY_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.MIPIPHY1_RCR.Define(this)
                .WithTaggedFlag("MIPIPHY1_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.MIPIPHY2_RCR.Define(this)
                .WithTaggedFlag("MIPIPHY2_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.HSIO_RCR.Define(this)
                .WithTaggedFlag("HSIO_RESET", 0)
                .WithReservedBits(1, 23));
            RegisterWithDomainControlTags(Registers.MEDIAISPDWP_RCR.Define(this)
                .WithTaggedFlag("MEDIAISPDWP_RESET", 0)
                .WithReservedBits(1, 23));
        }

        private void ReleaseCoreOnWrite(int coreIndex)
        {
            var cpu = cpus[coreIndex];
            var rvbar = (((ulong)gprHigh[coreIndex].Value << 22) | (ulong)gprLow[coreIndex].Value) << 2;
            cpu.PC = rvbar;
            cpu.IsHalted = false;
            this.DebugLog("Released core{0} from reset: GPR_H=0x{1:X} GPR_L=0x{2:X} RVBAR=0x{3:X}", coreIndex, gprHigh[coreIndex].Value, gprLow[coreIndex].Value, rvbar);
        }

        private void SetCoreEnable(int coreIndex, bool val)
        {
            if(cpus[coreIndex] != null)
            {
                cpus[coreIndex].IsHalted = !val;
            }
            var state = val ? "unhalted" : "halted";
            this.DebugLog("A53_CORE{0}_ENABLE={1}: core{0} {2}", coreIndex, val ? 1 : 0, state);
        }

        private IValueRegisterField maskWdog1Rst;
        private IValueRegisterField maskWdog3Rst;
        private IValueRegisterField[] gprHigh;
        private IValueRegisterField[] gprLow;

        private WdogSource pendingWdogSource = WdogSource.None;

        private IFlagRegisterField ippResetStatus;
        private IFlagRegisterField wdog1ResetStatus;
        private IFlagRegisterField wdog2ResetStatus;
        private IFlagRegisterField wdog3ResetStatus;

        private readonly BaseCPU[] cpus;

        // Per TRM, the MASK_WDOGx_RST field only masks the watchdog reset when written with
        // the magic value 0b0101; any other code is treated as 0b1010 (= reset not masked).
        private const ulong MaskedCode = 0x5;
        private const int NumCores = 4;

        private enum WdogSource
        {
            None,
            Wdog1,
            Wdog2,
            Wdog3,
        }

        private enum GpioInput
        {
            Wdog1RstB = 0,
            Wdog2RstB = 1,
            Wdog3RstB = 2,
        }

        private enum Registers : long
        {
            SCR             = 0x000,
            A53RCR0         = 0x004,
            A53RCR1         = 0x008,
            M7RCR           = 0x00C,
            SUPERMIX_RCR    = 0x018,
            AUDIOMIX_RCR    = 0x01C,
            USBPHY1_RCR     = 0x020,
            USBPHY2_RCR     = 0x024,
            MLMIX_RCR       = 0x028,
            PCIEPHY_RCR     = 0x02C,
            HDMI_RCR        = 0x030,
            MEDIA_RCR       = 0x034,
            GPU2D_RCR       = 0x038,
            GPU3D_RCR       = 0x03C,
            GPU_RCR         = 0x040,
            VPU_RCR         = 0x044,
            VPU_G1_RCR      = 0x048,
            VPU_G2_RCR      = 0x04C,
            VPUVC8KE_RCR    = 0x050,
            NOC_RCR         = 0x054,
            SBMR1           = 0x058,
            SRSR            = 0x05C,
            SISR            = 0x068,
            SIMR            = 0x06C,
            SBMR2           = 0x070,
            GPR1            = 0x074,
            GPR2            = 0x078,
            GPR3            = 0x07C,
            GPR4            = 0x080,
            GPR5            = 0x084,
            GPR6            = 0x088,
            GPR7            = 0x08C,
            GPR8            = 0x090,
            GPR9            = 0x094,
            GPR10           = 0x098,
            DDRC_RCR        = 0x1000,
            HDMIPHY_RCR     = 0x1008,
            MIPIPHY1_RCR    = 0x100C,
            MIPIPHY2_RCR    = 0x1010,
            HSIO_RCR        = 0x1014,
            MEDIAISPDWP_RCR = 0x1018,
        }
    }
}
