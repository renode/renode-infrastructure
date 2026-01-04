//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class EgisET171_SMU2 : BasicDoubleWordPeripheral, IKnownSize
    {
        public EgisET171_SMU2(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.PadMuxA.Define(this)
                .WithTag("Pad_MuxA", 0, 32);
            Registers.PadMuxB.Define(this)
                .WithTag("Pad_MuxB", 0, 32);
            Registers.DebugMux.Define(this)
                .WithTag("DBG_Mux", 0, 32);
            Registers.DebugIP.Define(this)
                .WithTag("DBG_IP", 0, 32);
            Registers.Analog.Define(this)
                .WithTag("Analog", 0, 32);
            Registers.MemoryMarginEnable.Define(this)
                .WithTag("Memory_MSE", 0, 32);
            Registers.MemoryMarginSetting.Define(this)
                .WithTag("Memory_MS", 0, 32);
            Registers.SPISMPUControl.Define(this)
                .WithTag("SPIS_MPU_Ctrl", 0, 32);
            Registers.SPISMPUStatus.Define(this)
                .WithTag("SPIS_MPU_Status", 0, 32);
            Registers.SPISMPURegion0Start.Define(this)
                .WithTag("SPIS_MPU_R0_Start", 0, 32);
            Registers.SPISMPURegion0End.Define(this)
                .WithTag("SPIS_MPU_R0_End", 0, 32);
            Registers.SPISMPURegion1Start.Define(this)
                .WithTag("SPIS_MPU_R1_Start", 0, 32);
            Registers.SPISMPURegion1End.Define(this)
                .WithTag("SPIS_MPU_R1_End", 0, 32);
            Registers.SPISMPURegion2Start.Define(this)
                .WithTag("SPIS_MPU_R2_Start", 0, 32);
            Registers.SPISMPURegion2End.Define(this)
                .WithTag("SPIS_MPU_R2_End", 0, 32);
            Registers.SPISMPURegion3Start.Define(this)
                .WithTag("SPIS_MPU_R3_Start", 0, 32);
            Registers.SPISMPURegion3End.Define(this)
                .WithTag("SPIS_MPU_R3_End", 0, 32);
            Registers.FrequencyMeasure.Define(this)
                .WithTag("Freq_Measure", 0, 32);
            Registers.USB2PHY.Define(this)
                .WithTag("USB2PHY", 0, 32);
        }

        private enum Registers
        {
            // Gap intentional
            PadMuxA = 0x100,
            PadMuxB = 0x104,
            DebugMux = 0x110,
            DebugIP = 0x114,
            // Gap intentional
            Analog = 0x200,
            // Gap intentional
            MemoryMarginEnable = 0x300,
            MemoryMarginSetting = 0x304,
            // Gap intentional
            SPISMPUControl = 0x400,
            SPISMPUStatus = 0x404,
            SPISMPURegion0Start = 0x408,
            SPISMPURegion0End = 0x40C,
            SPISMPURegion1Start = 0x410,
            SPISMPURegion1End = 0x414,
            SPISMPURegion2Start = 0x418,
            SPISMPURegion2End = 0x41C,
            SPISMPURegion3Start = 0x420,
            SPISMPURegion3End = 0x424,
            // Gap intentional
            FrequencyMeasure = 0x500,
            // Gap intentional
            USB2PHY = 0x600,
        }
    }
}