//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class S32KXX_ModeEntryModule : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32KXX_ModeEntryModule(IMachine machine, ICPU core0,
            ICPU core1 = null, ICPU core2 = null, ICPU core3 = null, ICPU core4 = null, ICPU core5 = null)
            : base(machine)
        {
            cores = new ICPU[]
            {
                core0,
                core1,
                core2,
                core3,
                core4,
                core5
            };
            cofbsStatus = new uint[][]
            {
                new uint[2],
                new uint[4],
                new uint[3]
            };
            DefineRegisters();
            Initialize();
        }

        public override void Reset()
        {
            base.Reset();
            Initialize();
        }

        public long Size => 0x4000;

        private void DefineRegisters()
        {
            Registers.ControlKey.Define(this)
                .WithTag("CTL_KEY (Control Key)", 0, 16)
                .WithReservedBits(16, 16);

            Registers.ModeConfiguration.Define(this)
                .WithTaggedFlag("DEST_RST (Destructive Reset Request)", 0)
                .WithTaggedFlag("FUNC_RST (Functional Reset Request)", 1)
                .WithReservedBits(2, 13)
                .WithTaggedFlag("STANDBY (Standby Request)", 15)
                .WithReservedBits(16, 16);

            Registers.ModeUpdate.Define(this)
                .WithTaggedFlag("MODE_UPD (Mode Update)", 0)
                .WithReservedBits(1, 31);

            Registers.ModeStatus.Define(this)
                .WithTaggedFlag("PREV_MODE (Previous Mode)", 0)
                .WithReservedBits(1, 31);

            Registers.MainCoreID.Define(this)
                .WithTag("CIDX (Core Index)", 0, 2)
                .WithReservedBits(3, 5)
                .WithTag("PIDX (Partition Index)", 8, 5)
                .WithReservedBits(13, 19);

            uint partitionSize = Registers.Partition1ProcessConfiguration - Registers.Partition0ProcessConfiguration;
            Registers.Partition0ProcessConfiguration.DefineMany(this, PartitionCount, stepInBytes: partitionSize, resetValue: 0x1, setup: (reg, index) => reg
                .WithTaggedFlag("PCE (Partition Clock Enable)", 0)
                .WithReservedBits(1, 31)
            );

            Registers.Partition0ProcessUpdate.DefineMany(this, PartitionCount, stepInBytes: partitionSize, setup: (reg, index) => reg
                .WithTaggedFlag("PCUD (Partition Clock Update)", 0)
                .WithReservedBits(1, 31)
            );

            Registers.Partition0Status.DefineMany(this, PartitionCount, stepInBytes: partitionSize, resetValue: 0x1, setup: (reg, index) => reg
                .WithTaggedFlag("PCS (Partition Clock Status)", 0)
                .WithReservedBits(1, 31)
            );

            DefineStatusEnableRegisters(Registers.Partition0COFBSet0ClockStatus, cofbsStatus[0]);
            DefineStatusEnableRegisters(Registers.Partition1COFBSet0ClockStatus, cofbsStatus[1]);
            DefineStatusEnableRegisters(Registers.Partition2COFBSet0ClockStatus, cofbsStatus[2]);

            // Registers specific for the Partition 0
            Registers.Partition0CoreLockstepControl.Define(this)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("LS2 (Lockstep 2)", 2)
                .WithReservedBits(3, 29);

            var coreSize = Registers.Partition0Core1ProcessConfiguration - Registers.Partition0Core0ProcessConfiguration;
            foreach (var index in Enumerable.Range(0, cores.Length).Where(x => x != 2))
            {
                var offset = index * coreSize;
                (Registers.Partition0Core0ProcessConfiguration + offset).Define(this)
                    .WithTaggedFlag($"CCE (Core{index} Clock Enable)", 0)
                    .WithReservedBits(1, 31);

                (Registers.Partition0Core0ProcessUpdate + offset).Define(this)
                    .WithTaggedFlag($"CCUPD (Core{index} Clock Update)", 0)
                    .WithReservedBits(1, 31);
            }

            // Currently only a single core configuration is supported.
            // To mimics the initial state of a system, halted cores reports waiting for interrupt.
            Registers.Partition0Core0Status.DefineMany(this, (uint)cores.Length, stepInBytes: (uint)coreSize, setup: (reg, index) => reg
                .WithFlag(0, name: $"CCS (Core{index} Clock Process Status)",
                    valueProviderCallback: (_) => cores[index] != null
                )
                .WithReservedBits(1, 30)
                .WithFlag(31, name: "WFI (WaitForInterruptStatus)",
                    valueProviderCallback: (_) => cores[index]?.IsHalted ?? false
                )
            );

            Registers.Partition0Core0Address.DefineMany(this, (uint)cores.Length, stepInBytes: (uint)coreSize, setup: (reg, index) => reg
                .WithReservedBits(0, 2)
                .WithTag($"ADDR (Core{index} Boot Address)", 2, 30)
            );
        }

        private void DefineStatusEnableRegisters(Registers statusOffset, uint[] statuses)
        {
            // COFB enable and status registers provides just a readback functionality.
            // The registers don't distinguish between regular fields and reserved bits.
            statusOffset.DefineMany(this, (uint)statuses.Length, (reg, index) => reg
                .WithValueField(0, 32, FieldMode.Read, name: "BLOCKn (IP Blocks Status)",
                    valueProviderCallback: (_) => statuses[index]
                )
            );

            var enableOffset = statusOffset - Registers.Partition0COFBSet0ClockStatus + Registers.Partition0COFBSet0ClockEnable;
            enableOffset.DefineMany(this, (uint)statuses.Length, (reg, index) => reg
                .WithValueField(0, 32, name: "REQn (Clocks Enable)",
                    writeCallback: (_, val) => statuses[index] = (uint)val,
                    valueProviderCallback: (_) => statuses[index]
                )
            );
        }

        private void Initialize()
        {
            cofbsStatus[0][0] = 0x00000004;
            cofbsStatus[0][1] = 0x00001000;
            cofbsStatus[1][0] = 0x5E3F0007;
            cofbsStatus[1][1] = 0x7CFE2FFC;
            cofbsStatus[1][2] = 0x300C0000;
            cofbsStatus[1][3] = 0x00005FEE;
            cofbsStatus[2][0] = 0x0600000F;
            cofbsStatus[2][1] = 0xCC000000;
            cofbsStatus[2][2] = 0x00000003;
        }

        private readonly uint[][] cofbsStatus;
        private readonly ICPU[] cores;
        private const uint PartitionCount = 3;

        public enum Registers
        {
            ControlKey = 0x0, // CTL_KEY 
            ModeConfiguration = 0x4, // MODE_CONF 
            ModeUpdate = 0x8, // MODE_UPD 
            ModeStatus = 0xC, // MODE_STAT 
            MainCoreID = 0x10, // MAIN_COREID 
            Partition0ProcessConfiguration = 0x100, // PRTN0_PCONF 
            Partition0ProcessUpdate = 0x104, // PRTN0_PUPD 
            Partition0Status = 0x108, // PRTN0_STAT 
            Partition0CoreLockstepControl = 0x10C, // PRTN0_CORE_LOCKSTE
            Partition0COFBSet0ClockStatus = 0x110, // PRTN0_COFB0_STAT
            Partition0COFBSet1ClockStatus = 0x114, // PRTN0_COFB1_STAT
            Partition0COFBSet0ClockEnable = 0x130, // PRTN0_COFB0_CLKE
            Partition0COFBSet1ClockEnable = 0x134, // PRTN0_COFB1_CLKE
            Partition0Core0ProcessConfiguration = 0x140, // PRTN0_CORE0_PCON
            Partition0Core0ProcessUpdate = 0x144, // PRTN0_CORE0_PUPD 
            Partition0Core0Status = 0x148, // PRTN0_CORE0_STAT 
            Partition0Core0Address = 0x14C, // PRTN0_CORE0_ADDR 
            Partition0Core1ProcessConfiguration = 0x160, // PRTN0_CORE1_PCON
            Partition0Core1ProcessUpdate = 0x164, // PRTN0_CORE1_PUPD 
            Partition0Core1Status = 0x168, // PRTN0_CORE1_STAT 
            Partition0Core1Address = 0x16C, // PRTN0_CORE1_ADDR 
            Partition0Core2Status = 0x188, // PRTN0_CORE2_STAT 
            Partition0Core2Address = 0x18C, // PRTN0_CORE2_ADDR 
            Partition0Core3ProcessConfiguration = 0x1A0, // PRTN0_CORE3_PCON
            Partition0Core3ProcessUpdate = 0x1A4, // PRTN0_CORE3_PUPD 
            Partition0Core3Status = 0x1A8, // PRTN0_CORE3_STAT 
            Partition0Core3Address = 0x1AC, // PRTN0_CORE3_ADDR 
            Partition0Core4ProcessConfiguration = 0x1C0, // PRTN0_CORE4_PCON
            Partition0Core4ProcessUpdate = 0x1C4, // PRTN0_CORE4_PUPD 
            Partition0Core4Status = 0x1C8, // PRTN0_CORE4_STAT 
            Partition0Core4Address = 0x1CC, // PRTN0_CORE4_ADDR 
            Partition0Core5ProcessConfiguration = 0x1E0, // PRTN0_CORE5_PCON
            Partition0Core5ProcessUpdate = 0x1E4, // PRTN0_CORE5_PUPD 
            Partition0Core5Status = 0x1E8, // PRTN0_CORE5_STAT 
            Partition0Core5Address = 0x1EC, // PRTN0_CORE5_ADDR 
            Partition1ProcessConfiguration = 0x300, // PRTN1_PCONF 
            Partition1ProcessUpdate = 0x304, // PRTN1_PUPD 
            Partition1Status = 0x308, // PRTN1_STAT 
            Partition1COFBSet0ClockStatus = 0x310, // PRTN1_COFB0_STAT
            Partition1COFBSet1ClockStatus = 0x314, // PRTN1_COFB1_STAT
            Partition1COFBSet2ClockStatus = 0x318, // PRTN1_COFB2_STAT
            Partition1COFBSet3ClockStatus = 0x31C, // PRTN1_COFB3_STAT
            Partition1COFBSet0ClockEnable = 0x330, // PRTN1_COFB0_CLKE
            Partition1COFBSet1ClockEnable = 0x334, // PRTN1_COFB1_CLKE
            Partition1COFBSet2ClockEnable = 0x338, // PRTN1_COFB2_CLKE
            Partition1COFBSet3ClockEnable = 0x33C, // PRTN1_COFB3_CLKE
            Partition2ProcessConfiguration = 0x500, // PRTN2_PCONF 
            Partition2ProcessUpdate = 0x504, // PRTN2_PUPD 
            Partition2Status = 0x508, // PRTN2_STAT 
            Partition2COFBSet0ClockStatus = 0x510, // PRTN2_COFB0_STAT
            Partition2COFBSet1ClockStatus = 0x514, // PRTN2_COFB1_STAT
            Partition2COFBSet2ClockStatus = 0x518, // PRTN2_COFB2_STAT
            Partition2COFBSet0ClockEnable = 0x530, // PRTN2_COFB0_CLKE
            Partition2COFBSet1ClockEnable = 0x534, // PRTN2_COFB1_CLKE
            Partition2COFBSet2ClockEnable = 0x538 // PRTN2_COFB2_CLKE
        }
    }
}
