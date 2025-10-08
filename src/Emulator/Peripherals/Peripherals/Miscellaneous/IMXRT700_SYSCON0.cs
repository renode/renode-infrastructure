//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.DMA;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class IMXRT700_SYSCON0 : BasicDoubleWordPeripheral, IKnownSize
    {
        public IMXRT700_SYSCON0(IMachine machine, IMXRT700_DmaMux dmaMux = null) : base(machine)
        {
            this.dmaMux = dmaMux;
            DefineRegisters();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.EDMA0RequestEnable0.DefineMany(this, 4, (register, ridx) =>
            {
                register
                    .WithFlags(0, 32, writeCallback: (fidx, _, value) => dmaMux?.EnableRequest(0, fidx + 32 * ridx, value));
                ;
            });

            Registers.EDMA1RequestEnable0.DefineMany(this, 4, (register, ridx) =>
            {
                register
                    .WithFlags(0, 32, writeCallback: (fidx, _, value) => dmaMux?.EnableRequest(1, fidx + 32 * ridx, value));
                ;
            });
        }

        private readonly IMXRT700_DmaMux dmaMux;

        private enum Registers
        {
            NMISourceSelect = 0x14,                         // NMISRC
            CTIMERGlobalStartEnable = 0x1C,                 // CTIMERGLOBALSTARTEN
            BusMatrixPriority = 0x78,                       // AHBMATPRIO
            ReceiveEventPulseGenerator = 0x80,              // RXEVPULSEGEN
            LatchedCortexM33TransmitEvent = 0x84,           // LATCHED_CM33_TXEV
            SystemSecureTickCalibration = 0x90,             // SYSTEM_STICK_CALIB
            SystemNonSecureTickCalibration = 0x94,          // SYSTEM_NSTICK_CALIB
            CPU0Status = 0x98,                              // CPU0_STATUS
            GPIOSynchronizationStages = 0xD0,               // GPIO_PSYNC
            AutomaticClockGateOverride = 0x114,             // AUTOCLKGATEOVERRIDE0
            SRAMClockGatingControl = 0x118,                 // SRAM_CLKGATE_CTRL
            MMU0MemoryControl = 0x130,                      // MMU0_MEM_CTRL
            EDMA0MemoryControl = 0x140,                     // EDMA0_MEM_CTRL
            EDMA1MemoryControl = 0x144,                     // EDMA1_MEM_CTRL
            ETFMemoryControl = 0x14C,                       // ETF_MEM_CTRL
            MMU1MemoryControl = 0x150,                      // MMU1_MEM_CTRL
            XSPI0MemoryControl = 0x154,                     // XSPI0_MEM_CTRL
            XSPI1MemoryControl = 0x158,                     // XSPI1_MEM_CTRL
            CACHE64_CTRL0DataMemoryControl = 0x15C,         // XSPI0_DATA_MEM_CTRL
            CACHE64_CTRL1DataMemoryControl = 0x160,         // XSPI1_DATA_MEM_CTRL
            NPUMemoryControl = 0x164,                       // NPU_MEM_CTRL
            CPU0MemoryDataControl = 0x174,                  // CM33_MEM_DATA_CTRL
            CPU0MemoryTagControl = 0x178,                   // CM33_MEM_TAG_CTRL
            HiFi4MemoryControl = 0x208,                     // HIFI4_MEM_CTL
            SAI0_2MCLKIODirectionControl = 0x240,           // SAI0_MCLK_CTRL
            CACHE64_CTRL0TagMemoryControl = 0x25C,          // XSPI0_TAG_MEM_CTRL
            CACHE64_CTRL1TagMemoryControl = 0x260,          // XSPI1_TAG_MEM_CTRL
            VDD2_COMPAutoGatingEnable = 0x280,              // COMP_AUTOGATE_EN
            HiFi4Stall = 0x300,                             // DSPSTALL
            HiFi4OCDHaltOnReset = 0x304,                    // OCDHALTONRESET
            HiFi4GeneralPurposeRegister0 = 0x308,           // HIFI4_GPR0
            HiFi4GeneralPurposeRegister1 = 0x314,           // HIFI4_GPR1
            HiFi4GeneralPurposeRegister2 = 0x318,           // HIFI4_GPR2
            HiFi4DSPVectorRemap = 0x31C,                    // DSP_VECT_REMAP
            EDMA0RequestEnable0 = 0x420,                    // EDMA0_EN0
            EDMA0RequestEnable1 = 0x424,                    // EDMA0_EN1
            EDMA0RequestEnable2 = 0x428,                    // EDMA0_EN2
            EDMA0RequestEnable3 = 0x42C,                    // EDMA0_EN3
            EDMA1RequestEnable0 = 0x430,                    // EDMA1_EN0
            EDMA1RequestEnable1 = 0x434,                    // EDMA1_EN1
            EDMA1RequestEnable2 = 0x438,                    // EDMA1_EN2
            EDMA1RequestEnable3 = 0x43C,                    // EDMA1_EN3
            AXBSControl = 0x600,                            // AXBS_CTRL
            I3CAsynchronousWakeupControl = 0x628,           // I3C_ASYNC_WAKEUP_CTRL
            GrayToBinaryConverterGrayCodeLSB = 0x650,       // GRAY_CODE_LSB
            GrayToBinaryConverterGrayCodeMSB = 0x654,       // GRAY_CODE_MSB
            GrayToBinaryConverterBinaryCodeLSB = 0x658,     // BINARY_CODE_LSB
            GrayToBinaryConverterBinaryCodeMSB = 0x65C,     // BINARY_CODE_MSB
        }
    }
}