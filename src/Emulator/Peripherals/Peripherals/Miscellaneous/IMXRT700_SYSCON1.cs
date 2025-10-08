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
    public class IMXRT700_SYSCON1 : BasicDoubleWordPeripheral, IKnownSize
    {
        public IMXRT700_SYSCON1(IMachine machine, IMXRT700_DmaMux dmaMux = null) : base(machine)
        {
            this.dmaMux = dmaMux;
            DefineRegisters();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.EDMA2RequestEnable0.DefineMany(this, 2, (register, ridx) =>
            {
                register
                    .WithFlags(0, 32, writeCallback: (fidx, _, value) => dmaMux?.EnableRequest(2, fidx + 32 * ridx, value));
                ;
            });

            Registers.EDMA3RequestEnable0.DefineMany(this, 2, (register, ridx) =>
            {
                register
                    .WithFlags(0, 32, writeCallback: (fidx, _, value) => dmaMux?.EnableRequest(3, fidx + 32 * ridx, value));
                ;
            });
        }

        private readonly IMXRT700_DmaMux dmaMux;

        private enum Registers
        {
            NMISourceSelect = 0x14,                         // NMISRC
            CTIMERGlobalStartEnable = 0x1C,                 // CTIMERGLOBALSTARTEN
            BusMatrixPriority = 0x78,                       // AHBMATPRIO
            SystemSecureTickCalibration = 0x90,             // SYSTEM_STICK_CALIB
            SystemNonSecureTickCalibration = 0x94,          // SYSTEM_NSTICK_CALIB
            GPIOSynchronizationStages = 0xD0,               // GPIO_PSYNC
            EDMA2MemoryControl = 0x144,                     // EDMA2_MEM_CTRL
            EDMA3MemoryControl = 0x148,                     // EDMA3_MEM_CTRL
            SAI3MCLKIODirectionControl = 0x240,             // SAI3_MCLK_CTRL
            HiFi1DSPStall = 0x300,                          // DSPSTALL
            HiFi1OCDHaltOnReset = 0x304,                    // OCDHALTONRESET
            HiFi1GeneralPurposeRegister0 = 0x308,           // HIFI1_GPR0
            HiFi1GeneralPurposeRegister1 = 0x314,           // HIFI1_GPR1
            HiFi1GeneralPurposeRegister2 = 0x318,           // HIFI1_GPR2
            HiFi1DSPVectorRemap = 0x31C,                    // DSP_VECT_REMAP
            EDMA2RequestEnable0 = 0x420,                    // EDMA2_EN0
            EDMA2RequestEnable1 = 0x424,                    // EDMA2_EN1
            EDMA3RequestEnable0 = 0x430,                    // EDMA3_EN0
            EDMA3RequestEnable1 = 0x434,                    // EDMA3_EN1
            AXBSControl = 0x600,                            // AXBS_CTRL
            I3CAsynchronousWakeupControl = 0x628,           // I3C_ASYNC_WAKEUP_CTRL
        }
    }
}