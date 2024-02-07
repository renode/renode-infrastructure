//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class S32K3XX_MiscellaneousSystemControlModule : BasicDoubleWordPeripheral, IWordPeripheral, IKnownSize,
        IProvidesRegisterCollection<WordRegisterCollection>

    {
        public S32K3XX_MiscellaneousSystemControlModule(IMachine machine) : base(machine)
        {
            wordRegisterCollection = new WordRegisterCollection(this);
            DefineRegisters();
        }

        public ushort ReadWord(long offset)
        {
            return wordRegisterCollection.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            wordRegisterCollection.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            wordRegisterCollection.Reset();
        }

        public long Size => 0x4000;

        WordRegisterCollection IProvidesRegisterCollection<WordRegisterCollection>.RegistersCollection => wordRegisterCollection;

        private void DefineRegisters()
        {
            IProvidesRegisterCollection<DoubleWordRegisterCollection> asDoubleWordCollection = this;

            var processorConfigurationSize = (uint)Registers.Processor0Type - (uint)Registers.ProcessorXType;
            Registers.ProcessorXType.DefineMany(asDoubleWordCollection, ProcessorConfigurationCount, stepInBytes: processorConfigurationSize, setup: (reg, index) =>
                {
                    var processorSuffix = index == 0 ? "x" : $"{index - 1}";
                    reg.WithTag($"PersonalityOfCP{processorSuffix}", 0, 32);
                }
            );
            Registers.ProcessorXNumber.DefineMany(asDoubleWordCollection, ProcessorConfigurationCount, stepInBytes: processorConfigurationSize, setup: (reg, index) =>
                {
                    var numberSize = index == 0 ? 3 : 2;
                    reg.WithReservedBits(numberSize, 32 - numberSize)
                        .WithTag("ProcessorNumber", 0, numberSize);
                }
            );
            Registers.ProcessorXRevision.DefineMany(asDoubleWordCollection, ProcessorConfigurationCount, stepInBytes: processorConfigurationSize, setup: (reg, index) => reg
                .WithReservedBits(8, 24)
                .WithTag("ProcessorRevision", 0, 8)
            );

            Registers.ProcessorXConfiguration0.DefineMany(asDoubleWordCollection, ProcessorConfigurationCount, stepInBytes: processorConfigurationSize, setup: (reg, index) => reg
                .WithTag("L1InstructionCacheSize", 24, 8)
                .WithTag("L1InstructionCacheWays", 16, 8)
                .WithTag("L1DataCacheSize", 8, 8)
                .WithTag("L1DataCacheWays", 0, 8)
            );
            Registers.ProcessorXConfiguration1.DefineMany(asDoubleWordCollection, ProcessorConfigurationCount, stepInBytes: processorConfigurationSize, setup: (reg, index) => reg
                .WithTag("L2CacheSize", 24, 8)
                .WithTag("L2CacheWays", 16, 8)
                .WithReservedBits(0, 16)
            );
            Registers.ProcessorXConfiguration2.DefineMany(asDoubleWordCollection, ProcessorConfigurationCount, stepInBytes: processorConfigurationSize, setup: (reg, index) => reg
                .WithTag("TightlyCoupledDataMemorySize", 24, 8)
                .WithTag("InstructionTightlyCoupledMemorySize", 16, 8)
                .WithReservedBits(0, 16)
            );
            Registers.ProcessorXConfiguration3.DefineMany(asDoubleWordCollection, ProcessorConfigurationCount, stepInBytes: processorConfigurationSize, setup: (reg, index) => reg
                .WithReservedBits(5, 27)
                .WithTaggedFlag("Cryptography", 4)
                .WithTaggedFlag("CoreMemoryProtectionUnit", 3)
                .WithTaggedFlag("MemoryManagementUnit", 2)
                .WithTaggedFlag("NEONInstructionSupport", 1)
                .WithTaggedFlag("FloatingPointUnit", 0)
            );

            var interruptRegisterStep = (uint)Registers.InterruptRouterCP0InterruptStatus1 - (uint)Registers.InterruptRouterCP0InterruptStatus0;
            Registers.InterruptRouterCP0InterruptStatus0.DefineMany(asDoubleWordCollection, InterruptRouterRegisterCount, stepInBytes: interruptRegisterStep, setup: (reg, index) =>
                {
                    var cpuIndex = index / 4;
                    reg.WithReservedBits(4, 28)
                        .WithTaggedFlag($"CP3ToCP{cpuIndex}", 3)
                        .WithTaggedFlag($"CP2ToCP{cpuIndex}", 2)
                        .WithTaggedFlag($"CP1ToCP{cpuIndex}", 1)
                        .WithTaggedFlag($"CP0ToCP{cpuIndex}", 0);
                }
            );
            Registers.InterruptRouterCP0InterruptGeneration0.DefineMany(asDoubleWordCollection, InterruptRouterRegisterCount, stepInBytes: interruptRegisterStep, setup: (reg, index) => reg
                .WithReservedBits(1, 31)
                .WithTaggedFlag($"InterruptEnable", 0)
            );

            Registers.InterruptRouterConfiguration.Define(asDoubleWordCollection)
                .WithTaggedFlag("Lock", 31)
                .WithReservedBits(4, 27)
                .WithTaggedFlag("CP3AsTrustedCore", 3)
                .WithTaggedFlag("CP2AsTrustedCore", 2)
                .WithTaggedFlag("CP1AsTrustedCore", 1)
                .WithTaggedFlag("CP0AsTrustedCore", 0);

            Registers.MemoryExecutionControl.Define(asDoubleWordCollection)
                .WithTaggedFlag("HardLock", 31)
                .WithTaggedFlag("SoftLock", 30)
                .WithReservedBits(24, 6)
                .WithTaggedFlag("TransactionControlForCortex-M7_3DTCM", 23)
                .WithTaggedFlag("TransactionControlForCortex-M7_2DTCM", 22)
                .WithTaggedFlag("TransactionControlForCortex-M7_1DTCM", 21)
                .WithTaggedFlag("TransactionControlForCortex-M7_0DTCM", 20)
                .WithTaggedFlag("DisableD0andD1TCMExecutionForCortex-M7_3", 19)
                .WithTaggedFlag("DisableD0andD1TCMExecutionForCortex-M7_2", 18)
                .WithTaggedFlag("D0andD1TCMExecutionForCortex-M7_1", 17)
                .WithTaggedFlag("D0AndD1TCMExecutionForCortex-M7_0", 16)
                .WithTaggedFlag("TransactionControlForCortex-M7_3ITCM", 15)
                .WithTaggedFlag("TransactionControlForCortex-M7_2ITCM", 14)
                .WithTaggedFlag("TransactionControlForCortex-M7_1ITCM", 13)
                .WithTaggedFlag("TransactionControlForCortex-M7_0ITCM", 12)
                .WithTaggedFlag("ITCMExecutionForCortex-M7_3", 11)
                .WithTaggedFlag("ITCMExecutionForCortex-M7_2", 10)
                .WithTaggedFlag("ITCMExecutionForCortex-M7_1", 9)
                .WithTaggedFlag("ITCMExecutionForCortex-M7_0", 8)
                .WithReservedBits(3, 5)
                .WithTaggedFlag("TransactionControlForPRAM2", 2)
                .WithTaggedFlag("TransactionControlForPRAM1", 1)
                .WithTaggedFlag("TransactionControlForPRAM0", 0);

            Registers.EnableInterconnectErrorDetection0.Define(asDoubleWordCollection)
                .WithReservedBits(30, 2)
                .WithTaggedFlag("AddressCheckForCortex-M7_1_TCM", 29)
                .WithTaggedFlag("WriteDataCheckForCortex-M7_1_TCM", 28)
                .WithTaggedFlag("AddressCheckForCortex-M7_0_TCM", 27)
                .WithTaggedFlag("WriteDataCheckForCortex-M7_0_TCM", 26)
                .WithTaggedFlag("AddressCheckForAIPS2", 25)
                .WithTaggedFlag("WriteDataCheckForAIPS2", 24)
                .WithTaggedFlag("AddressCheckForAIPS1", 23)
                .WithTaggedFlag("WriteDataCheckForAIPS1", 22)
                .WithTaggedFlag("AddressCheckForAIPS0", 21)
                .WithTaggedFlag("WriteDataCheckForAIPS0", 20)
                .WithTaggedFlag("AddressCheckForQuadSPI", 19)
                .WithTaggedFlag("WriteDataCheckForQuadSPI", 18)
                .WithReservedBits(16, 2)
                .WithTaggedFlag("AddressCheckForPRAM1", 15)
                .WithTaggedFlag("WriteDataCheckForPRAM1", 14)
                .WithTaggedFlag("AddressCheckForPRAM0", 13)
                .WithTaggedFlag("WriteDataCheckForPRAM0", 12)
                .WithTaggedFlag("EnableAddressCheckForPF2", 11)
                .WithTaggedFlag("AddressCheckForPF1", 10)
                .WithTaggedFlag("AddressCheckForPF0", 9)
                .WithReservedBits(8, 1)
                .WithTaggedFlag("ReadDataCheckForCortex-M7_1_AHBP", 7)
                .WithTaggedFlag("ReadDataCheckForCortex-M7_1_AHBM", 6)
                .WithTaggedFlag("ReadDataCheckForENET", 5)
                .WithTaggedFlag("ReadDataCheckForHSE_B", 4)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("ReadDataCheckForeDMA", 2)
                .WithTaggedFlag("ReadDataCheckForCortex-M7_0_AHBP", 1)
                .WithTaggedFlag("ReadDataCheckForCortex-M7_0_AHBM", 0);

            Registers.EnableInterconnectErrorDetection1.Define(asDoubleWordCollection)
                .WithReservedBits(25, 7)
                .WithTaggedFlag("TCMGasketAddressCheck", 24)
                .WithTaggedFlag("SlaveCheckAcceleratorResultM1GasketAddressCheck", 23)
                .WithTaggedFlag("SlaveCheckAcceleratorResultM1GasketWriteDataCheck", 22)
                .WithTaggedFlag("SlaveCheckAcceleratorAddress", 21)
                .WithTaggedFlag("MasterCheckAcceleratorFeed", 20)
                .WithTaggedFlag("MasterCheckAcceleratorResult", 19)
                .WithTaggedFlag("EnableReadDataCheckCortex-M7_3_AHBP", 18)
                .WithTaggedFlag("EnableReadDataCheckCortex-M7_3_AHBM", 17)
                .WithReservedBits(16, 1)
                .WithTaggedFlag("EnableAddressCheckCortex-M7_2_TCM", 15)
                .WithTaggedFlag("EnableWriteDataCheckCortex-M7_2_TCM", 14)
                .WithTaggedFlag("EnableAddressCheckCortex-M7_3_TCM", 13)
                .WithTaggedFlag("EnableWriteDataCheckCortex-M7_3_TCM", 12)
                .WithTaggedFlag("EnableAddressCheckPRAM2", 11)
                .WithTaggedFlag("EnableWriteDataCheckPRAM2", 10)
                .WithReservedBits(8, 2)
                .WithTaggedFlag("MasterCheckENET1", 7)
                .WithTaggedFlag("EnableAddressCheckeDMAS1", 6)
                .WithTaggedFlag("EnableAddressCheckeDMAS0", 5)
                .WithTaggedFlag("EnableAddressCheckPFlash3", 4)
                .WithTaggedFlag("EnableReadDataCheckCortex-M7_2_AHBP", 3)
                .WithTaggedFlag("EnableReadDataCheckCortex-M7_2_AHBM", 2)
                .WithReservedBits(0, 2);

            IProvidesRegisterCollection<WordRegisterCollection> asWordCollection = this;
            Registers.InterruptRouterSharedPeripheralRoutingControl0.DefineMany(asWordCollection, InterruptRouterSharedRegisterCount, (reg, index) => reg
                .WithTaggedFlag("Lock", 15)
                .WithReservedBits(4, 9)
                .WithTaggedFlag("EnableCortex-M7_3InterruptSteering", 3)
                .WithTaggedFlag("EnableCortex-M7_2InterruptSteering", 2)
                .WithTaggedFlag("EnableCortex-M7_1InterruptSteering", 1)
                .WithTaggedFlag("EnableCortex-M7_0InterruptSteering", 0)
            );
        }

        private WordRegisterCollection wordRegisterCollection;

        private const uint ProcessorCount = 4;
        private const uint ProcessorConfigurationCount = ProcessorCount + 1;
        private const uint InterruptRouterRegisterCount = ProcessorCount * 4;
        private const uint InterruptRouterSharedRegisterCount = 240;

        public enum Registers
        {
            ProcessorXType = 0x0, // CPXTYPE
            ProcessorXNumber = 0x4, // CPXNUM
            ProcessorXRevision = 0x8, // CPXREV
            ProcessorXConfiguration0 = 0xC, // CPXCFG0
            ProcessorXConfiguration1 = 0x10, // CPXCFG1
            ProcessorXConfiguration2 = 0x14, // CPXCFG2
            ProcessorXConfiguration3 = 0x18, // CPXCFG3
            Processor0Type = 0x20, // CP0TYPE
            Processor0Number = 0x24, // CP0NUM
            Processor0Count = 0x28, // CP0REV
            Processor0Configuration0 = 0x2C, // CP0CFG0
            Processor0Configuration1 = 0x30, // CP0CFG1
            Processor0Configuration2 = 0x34, // CP0CFG2
            Processor0Configuration3 = 0x38, // CP0CFG3
            Processor1Type = 0x40, // CP1TYPE
            Processor1Number = 0x44, // CP1NUM
            Processor1Count = 0x48, // CP1REV
            Processor1Configuration0 = 0x4C, // CP1CFG0
            Processor1Configuration1 = 0x50, // CP1CFG1
            Processor1Configuration2 = 0x54, // CP1CFG2
            Processor1Configuration3 = 0x58, // CP1CFG3
            Processor2Type = 0x60, // CP2TYPE
            Processor2Number = 0x64, // CP2NUM
            Processor2Count = 0x68, // CP2REV
            Processor2Configuration0 = 0x6C, // CP2CFG0
            Processor2Configuration1 = 0x70, // CP2CFG1
            Processor2Configuration2 = 0x74, // CP2CFG2
            Processor2Configuration3 = 0x78, // CP2CFG3
            Processor3Type = 0x80, // CP3TYPE
            Processor3Number = 0x84, // CP3NUM
            Processor3Count = 0x88, // CP3REV
            Processor3Configuration0 = 0x8C, // CP3CFG0
            Processor3Configuration1 = 0x90, // CP3CFG1
            Processor3Configuration2 = 0x94, // CP3CFG2
            Processor3Configuration3 = 0x98, // CP3CFG3
            InterruptRouterCP0InterruptStatus0 = 0x200, // IRCP0ISR0
            InterruptRouterCP0InterruptGeneration0 = 0x204, // IRCP0IGR0
            InterruptRouterCP0InterruptStatus1 = 0x208, // IRCP0ISR1
            InterruptRouterCP0InterruptGeneration1 = 0x20C, // IRCP0IGR1
            InterruptRouterCP0InterruptStatus2 = 0x210, // IRCP0ISR2
            InterruptRouterCP0InterruptGeneration2 = 0x214, // IRCP0IGR2
            InterruptRouterCP0InterruptStatus3 = 0x218, // IRCP0ISR3
            InterruptRouterCP0InterruptGeneration3 = 0x21C, // IRCP0IGR3
            InterruptRouterCP1InterruptStatus0 = 0x220, // IRCP1ISR0
            InterruptRouterCP1InterruptGeneration0 = 0x224, // IRCP1IGR0
            InterruptRouterCP1InterruptStatus1 = 0x228, // IRCP1ISR1
            InterruptRouterCP1InterruptGeneration1 = 0x22C, // IRCP1IGR1
            InterruptRouterCP1InterruptStatus2 = 0x230, // IRCP1ISR2
            InterruptRouterCP1InterruptGeneration2 = 0x234, // IRCP1IGR2
            InterruptRouterCP1InterruptStatus3 = 0x238, // IRCP1ISR3
            InterruptRouterCP1InterruptGeneration3 = 0x23C, // IRCP1IGR3
            InterruptRouterCP2InterruptStatus0 = 0x240, // IRCP2ISR0
            InterruptRouterCP2InterruptGeneration0 = 0x244, // IRCP2IGR0
            InterruptRouterCP2InterruptStatus1 = 0x248, // IRCP2ISR1
            InterruptRouterCP2InterruptGeneration1 = 0x24C, // IRCP2IGR1
            InterruptRouterCP2InterruptStatus2 = 0x250, // IRCP2ISR2
            InterruptRouterCP2InterruptGeneration2 = 0x254, // IRCP2IGR2
            InterruptRouterCP2InterruptStatus3 = 0x258, // IRCP2ISR3
            InterruptRouterCP2InterruptGeneration3 = 0x25C, // IRCP2IGR3
            InterruptRouterCP3InterruptStatus0 = 0x260, // IRCP3ISR0
            InterruptRouterCP3InterruptGeneration0 = 0x264, // IRCP3IGR0
            InterruptRouterCP3InterruptStatus1 = 0x268, // IRCP3ISR1
            InterruptRouterCP3InterruptGeneration1 = 0x26C, // IRCP3IGR1
            InterruptRouterCP3InterruptStatus2 = 0x270, // IRCP3ISR2
            InterruptRouterCP3InterruptGeneration2 = 0x274, // IRCP3IGR2
            InterruptRouterCP3InterruptStatus3 = 0x278, // IRCP3ISR3
            InterruptRouterCP3InterruptGeneration3 = 0x27C, // IRCP3IGR3
            InterruptRouterConfiguration = 0x400, // IRCPCFG
            MemoryExecutionControl = 0x500, // XNCTRL
            EnableInterconnectErrorDetection0 = 0x600, // ENEDC
            EnableInterconnectErrorDetection1 = 0x604, // ENEDC1
            InterruptRouterSharedPeripheralRoutingControl0 = 0x880, // IRSPRC0
            InterruptRouterSharedPeripheralRoutingControl239 = 0xA5E // IRSPRC239
        }
    }
}
