//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.PCI
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class MPFS_PCIe : SimpleContainer<IPCIePeripheral>, IDoubleWordPeripheral, IKnownSize, IPCIeRouter, IAbsoluteAddressAware
    {
        public MPFS_PCIe(IMachine machine) : base(machine)
        {
            var registersDictionary = new Dictionary<long, DoubleWordRegister>
            {
                //this register is not documented. It seems it contains the enabled flag, it may contain the port type, number of lanes, lane rate etc
                {(long)Registers.GEN_SETTINGS, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "ROOT_PORT_ENABLE")
                },
                {(long)Registers.ECC_CONTROL, new DoubleWordRegister(this)
                    .WithTag("TX_RAM_INJ_ERR", 0, 4)
                    .WithTag("RX_RAM_INJ_ERR", 4, 4)
                    .WithTag("PCIe2AXI_RAM_INJ_ERR", 8, 4)
                    .WithTag("AXI2PCIe_RAM_INJ_ERR", 12, 4)
                    .WithReservedBits(16, 8)
                    .WithTag("TX_RAM_ECC_BYPASS", 24, 1)
                    .WithTag("RX_RAM_ECC_BYPASS", 25, 1)
                    .WithTag("PCIe2AXI_RAM_ECC_BYPASS", 26, 1)
                    .WithTag("AXI2PCIe_RAM_ECC_BYPASS", 27, 1)
                    .WithReservedBits(28, 4)
                },
                //most likely w1c, unclear from the docs
                {(long)Registers.PCIE_EVENT_INT, new DoubleWordRegister(this)
                    .WithTag("L2_EXIT_INT", 0, 1)
                    .WithTag("HOTRST_EXIT_INT", 1, 1)
                    .WithTag("DLUP_EXIT_INT", 2, 1)
                    .WithReservedBits(3, 13)
                    .WithTag("L2_EXIT_INT_MASK", 16, 1)
                    .WithTag("HOTRST_EXIT_INT_MASK", 17, 1)
                    .WithTag("DLUP_EXIT_INT_MASK", 18, 1)
                    .WithReservedBits(19, 13)
                },
                //correctable ecc-related memory errors, probably w1c
                {(long)Registers.SEC_ERROR_INT, new DoubleWordRegister(this)
                    .WithTag("TX_RAM_SEC_ERR_INT", 0, 4)
                    .WithTag("RX_RAM_SEC_ERR_INT", 4, 4)
                    .WithTag("PCIE2AXI_RAM_SEC_ERR_INT", 8, 4)
                    .WithTag("AXI2PCIE_RAM_SEC_ERR_INT", 12, 4)
                    .WithReservedBits(16, 16)
                },
                //uncorrectable ecc-related memory errors, probably w1c
                {(long)Registers.DED_ERROR_INT, new DoubleWordRegister(this)
                    .WithTag("TX_RAM_DED_ERR_INT", 0, 4)
                    .WithTag("RX_RAM_DED_ERR_INT", 4, 4)
                    .WithTag("PCIE2AXI_RAM_DED_ERR_INT", 8, 4)
                    .WithTag("AXI2PCIE_RAM_DED_ERR_INT", 12, 4)
                    .WithReservedBits(16, 16)
                },
                //this is the "standard" interrupt mask register, 1 to enable interrupt source
                {(long)Registers.IMASK_LOCAL, new DoubleWordRegister(this)
                    .WithTag("IMASK_DMA_END_ENGINE_0", 0, 1)
                    .WithTag("IMASK_DMA_END_ENGINE_1", 1, 1)
                    .WithReservedBits(2, 6)
                    .WithTag("IMASK_DMA_ERROR_ENGINE_0", 8, 1)
                    .WithTag("IMASK_DMA_ERROR_ENGINE_1", 9, 1)
                    .WithReservedBits(10, 6)
                    .WithTag("IMASK_A_ATR_EVT_POST_ERR", 16, 1)
                    .WithTag("IMASK_A_ATR_EVT_FETCH_ERR", 17, 1)
                    .WithTag("IMASK_A_ATR_EVT_DISCARD_ERR", 18, 1)
                    .WithTag("IMASK_A_ATR_EVT_DOORBELL", 19, 1)
                    .WithTag("IMASK_P_ATR_EVT_POST_ERR", 20, 1)
                    .WithTag("IMASK_P_ATR_EVT_FETCH_ERR", 21, 1)
                    .WithTag("IMASK_P_ATR_EVT_DISCARD_ERR", 22, 1)
                    .WithTag("IMASK_P_ATR_EVT_DOORBELL", 23, 1)
                    .WithTag("IMASK_PM_MSI_INT_INTA", 24, 1)
                    .WithTag("IMASK_PM_MSI_INT_INTB", 25, 1)
                    .WithTag("IMASK_PM_MSI_INT_INTC", 26, 1)
                    .WithTag("IMASK_PM_MSI_INT_INTD", 27, 1)
                    .WithTag("IMASK_PM_MSI_INT_MSI", 28, 1)
                    .WithTag("IMASK_PM_MSI_INT_AER_EVT", 29, 1)
                    .WithTag("IMASK_PM_MSI_INT_EVENTS", 30, 1)
                    .WithTag("IMASK_PM_MSI_INT_SYS_ERR", 31, 1)
                },
                //this is the "standard" interrupt status register, w1c
                {(long)Registers.ISTATUS_LOCAL, new DoubleWordRegister(this)
                    .WithTag("DMA_END_ENGINE_0", 0, 1)
                    .WithTag("DMA_END_ENGINE_1", 1, 1)
                    .WithReservedBits(2, 6)
                    .WithTag("DMA_ERROR_ENGINE_0", 8, 1)
                    .WithTag("DMA_ERROR_ENGINE_1", 9, 1)
                    .WithReservedBits(10, 6)
                    .WithTag("A_ATR_EVT_POST_ERR", 16, 1)
                    .WithTag("A_ATR_EVT_FETCH_ERR", 17, 1)
                    .WithTag("A_ATR_EVT_DISCARD_ERR", 18, 1)
                    .WithTag("A_ATR_EVT_DOORBELL", 19, 1)
                    .WithTag("P_ATR_EVT_POST_ERR", 20, 1)
                    .WithTag("P_ATR_EVT_FETCH_ERR", 21, 1)
                    .WithTag("P_ATR_EVT_DISCARD_ERR", 22, 1)
                    .WithTag("P_ATR_EVT_DOORBELL", 23, 1)
                    .WithTag("PM_MSI_INT_INTA", 24, 1)
                    .WithTag("PM_MSI_INT_INTB", 25, 1)
                    .WithTag("PM_MSI_INT_INTC", 26, 1)
                    .WithTag("PM_MSI_INT_INTD", 27, 1)
                    .WithTag("PM_MSI_INT_MSI", 28, 1)
                    .WithTag("PM_MSI_INT_AER_EVT", 29, 1)
                    .WithTag("PM_MSI_INT_EVENTS", 30, 1)
                    .WithTag("PM_MSI_INT_SYS_ERR", 31, 1)
                },
                //this should reflect bits in ISTATUS_HOST, but it's described as a one field
                {(long)Registers.IMASK_HOST, new DoubleWordRegister(this)
                    .WithTag("INT_MASK_HOST", 0, 32)
                },
                //this should reflect bits in ISTATUS_LOCAL. The docs say that the host processor monitors and clears these bits (w1c), but the software writes it anyway)
                {(long)Registers.ISTATUS_HOST, new DoubleWordRegister(this)
                    .WithTag("DMA_END", 0, 8)
                    .WithTag("DMA_ERROR", 8, 2)
                    .WithTag("A_ATR_EVT", 10, 10)
                    .WithTag("P_ATR_EVT", 20, 4)
                    .WithTag("INT_REQUEST", 24, 8)
                },
            };
            for(var i = 0; i < NumberOfTranslationTables; i++)
            {
                pcieAddressTables[i] = new AddressTranslationTable(this);
                axiAddressTables[i] = new AddressTranslationTable(this);
                CreateAddressTranslationRegisters(registersDictionary, i, true);
                CreateAddressTranslationRegisters(registersDictionary, i, false);
            }
            registers = new DoubleWordRegisterCollection(this, registersDictionary);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        [ConnectionRegion("ecam")]
        public void WriteDoubleWordEcam(long offset, uint value)
        {

            var table = axiAddressTables.SingleOrDefault(x => x.DoesAddressHit(currentAccessAbsoluteAddress));
            if(table == null)
            {
                this.Log(LogLevel.Warning, "Trying to access memory at 0x{0:X} writing 0x{1:X} without address translation configured.", currentAccessAbsoluteAddress, value);
                this.LogUnhandledWrite(offset, value);
                return;
            }
            var translatedAddress = table.Translate(currentAccessAbsoluteAddress);
            if(table.TargetSpace == PCIeSpace.Configuration)
            {
                if(!TryDoEcamLookup(offset, out var result))
                {
                    this.LogUnhandledWrite(offset, value);
                    return;
                }
                result.Endpoint.ConfigurationWriteDoubleWord(result.EcamAddress.Offset, value);
            }
            else if(table.TargetSpace == PCIeSpace.TxRx)
            {
                var barCandidates = memoryMap.Where(x => x.Key.Contains(translatedAddress));
                if(!barCandidates.Any())
                {
                    this.Log(LogLevel.Warning, "Trying to write to a BAR at 0x{0:X}, but nothing is registered there, value 0x{1:X}.", translatedAddress, value);
                    return;
                }
                //This should not be required, but we need to improve RegisterBar first
                var targetBarEntry = barCandidates.Single();
                targetBarEntry.Value.TargetPeripheral.MemoryWriteDoubleWord(targetBarEntry.Value.BarNumber, (long)(translatedAddress - targetBarEntry.Key.StartAddress), value);
            }
            else
            {
                this.Log(LogLevel.Warning, "Trying to write to the PCIe space at 0x{0:X} in an unsupported mode {1}, value 0x{2:X}.", offset, table.TargetSpace, value);
                return;
            }
        }

        [ConnectionRegion("ecam")]
        public uint ReadDoubleWordEcam(long offset)
        {
            var table = axiAddressTables.SingleOrDefault(x => x.DoesAddressHit(currentAccessAbsoluteAddress));
            if(table == null)
            {
                this.Log(LogLevel.Warning, "Trying to access memory at 0x{0:X} without address translation configured.", currentAccessAbsoluteAddress);
                this.LogUnhandledRead(offset);
                return 0;
            }
            var translatedAddress = table.Translate(currentAccessAbsoluteAddress);
            if(table.TargetSpace == PCIeSpace.Configuration)
            {
                if(!TryDoEcamLookup(offset, out var result))
                {
                    this.LogUnhandledRead(offset);
                    return FunctionNotImplemented;
                }
                return result.Endpoint.ConfigurationReadDoubleWord(result.EcamAddress.Offset);
            }
            else if(table.TargetSpace == PCIeSpace.TxRx)
            {
                var barCandidates = memoryMap.Where(x => x.Key.Contains(translatedAddress));
                if(!barCandidates.Any())
                {
                    this.Log(LogLevel.Warning, "Trying to read from a BAR at 0x{0:X}, but nothing is registered there.", translatedAddress);
                    return 0;
                }
                //This should not be required, but we need to improve RegisterBar first
                var targetBarEntry = barCandidates.Single();
                return targetBarEntry.Value.TargetPeripheral.MemoryReadDoubleWord(targetBarEntry.Value.BarNumber, (long)(translatedAddress - targetBarEntry.Key.StartAddress));
            }
            else
            {
                this.Log(LogLevel.Warning, "Trying to read from the PCIe space at 0x{0:X} in an unsupported mode {1}.", offset, table.TargetSpace);
                return 0;
            }
        }

        public override void Reset()
        {
            registers.Reset();
        }

        public void SetAbsoluteAddress(ulong address)
        {
            currentAccessAbsoluteAddress = address & ~3ul;
        }

        public long Size => 0x4000;

        private bool TryDoEcamLookup(long offset, out EcamLookupResult result)
        {
            result = new EcamLookupResult();
            result.EcamAddress = new EcamAddress((uint)offset); // the case is safe as we are interested in lower 28 bits
            return result.EcamAddress.Device == 0 && TryGetByAddress(result.EcamAddress.Bus, out result.Endpoint); // this works if we have a flat structure only!
        }

        private void CreateAddressTranslationRegisters(Dictionary<long, DoubleWordRegister> registers, int tableNumber, bool isPcieTable)
        {
            var table = isPcieTable ? pcieAddressTables[tableNumber] : axiAddressTables[tableNumber];
            var baseOffset = (long)(isPcieTable ? Registers.ATR0_PCIE_WIN0_SRCADDR_PARAM : Registers.ATR0_AXI4_SLV0_SRCADDR_PARAM)
                            + tableNumber * (Registers.ATR1_AXI4_SLV0_SRCADDR_PARAM - Registers.ATR0_AXI4_SLV0_SRCADDR_PARAM);
            registers.Add(baseOffset + (long)AddressTranslationRegisters.SourceAddressLowAndParameters, new DoubleWordRegister(this)
                            .WithFlag(0, changeCallback: (_, value) => table.Enabled = value,
                                         valueProviderCallback: _ => table.Enabled, name: "ATR_IMPL")
                            .WithValueField(1, 6, changeCallback: (_, value) => table.SizePower = (uint)value,
                                                  valueProviderCallback: _ => table.SizePower, name: "ATR_SIZE")
                            .WithReservedBits(7, 5)
                            .WithValueField(12, 20, changeCallback: (_, value) => BitHelper.UpdateWithShifted(ref table.SourceAddress, value, 12, 20),
                                                    valueProviderCallback: _ => (uint)BitHelper.GetValue(table.SourceAddress, 12, 20), name: "SRC_ADDR[31:12]")
                        );
            registers.Add(baseOffset + (long)AddressTranslationRegisters.SourceAddressHigh, new DoubleWordRegister(this)
                            .WithValueField(0, 32, changeCallback: (_, value) => BitHelper.UpdateWithShifted(ref table.SourceAddress, value, 32, 32),
                                                   valueProviderCallback: _ => (uint)BitHelper.GetValue(table.SourceAddress, 32, 32), name: "SRC_ADDR[63:32]")
                        );
            registers.Add(baseOffset + (long)AddressTranslationRegisters.DestinationAddressLow, new DoubleWordRegister(this)
                            .WithReservedBits(0, 12)
                            .WithValueField(12, 20, changeCallback: (_, value) => BitHelper.UpdateWithShifted(ref table.DestinationAddress, value, 12, 20),
                                                    valueProviderCallback: _ => (uint)BitHelper.GetValue(table.DestinationAddress, 12, 20), name: "TRSL_ADDR_LSB")
                        );
            registers.Add(baseOffset + (long)AddressTranslationRegisters.DestinationAddressHigh, new DoubleWordRegister(this)
                            .WithValueField(0, 32, changeCallback: (_, value) => BitHelper.UpdateWithShifted(ref table.SourceAddress, value, 32, 32),
                                                   valueProviderCallback: _ => (uint)BitHelper.GetValue(table.SourceAddress, 32, 32), name: "TRSL_ADDR_UDW")
                        );
            registers.Add(baseOffset + (long)AddressTranslationRegisters.TranslationParametersLow, new DoubleWordRegister(this)
                            .WithEnumField(0, 4, changeCallback: (_, value) => table.TargetSpace = value,
                                                 valueProviderCallback: (PCIeSpace _) => table.TargetSpace, name: "TRSL_ID")
                            .WithReservedBits(4, 12)
                            .WithTag("TRSF_PARAM", 16, 12)
                            .WithReservedBits(28, 4)
                        );
            registers.Add(baseOffset + (long)AddressTranslationRegisters.TranslationMaskLow, new DoubleWordRegister(this)
                            .WithValueField(0, 32, valueProviderCallback: _ => (uint)BitHelper.GetValue(table.TranslationMask, 0, 32), name: "TRSL_MASK")
                        );
            registers.Add(baseOffset + (long)AddressTranslationRegisters.TranslationMaskHigh, new DoubleWordRegister(this)
                            .WithValueField(0, 32, valueProviderCallback: _ => (uint)BitHelper.GetValue(table .TranslationMask, 32, 32), name: "TRSL_MASK")
                        );
        }

        public void RegisterBar(Range range, IPCIePeripheral peripheral, uint bar)
        {
            //This has to be improved greatly.
            //1. have fast search
            //2. invalidate overlaps
            var previousRegistration = memoryMap.Where(x => x.Value.BarNumber == bar && x.Value.TargetPeripheral == peripheral).Select(x => x.Key);
            if(previousRegistration.Any())
            {
                memoryMap.Remove(previousRegistration.SingleOrDefault());
            }
            memoryMap[range] = new TargetBar { BarNumber = bar, TargetPeripheral = peripheral };
        }

        private ulong currentAccessAbsoluteAddress;
        private readonly DoubleWordRegisterCollection registers;

        private readonly AddressTranslationTable[] pcieAddressTables = new AddressTranslationTable[NumberOfTranslationTables];
        private readonly AddressTranslationTable[] axiAddressTables = new AddressTranslationTable[NumberOfTranslationTables];
        private readonly Dictionary<Range, TargetBar> memoryMap = new Dictionary<Range, TargetBar>();

        private const int NumberOfTranslationTables = 6;

        private const uint FunctionNotImplemented = 0xFFFF;

        protected struct TargetBar
        {
            public IPCIePeripheral TargetPeripheral;
            public uint BarNumber;
        }

        private struct EcamLookupResult
        {
            public IPCIePeripheral Endpoint;
            public EcamAddress EcamAddress;
        }

        private struct EcamAddress
        {
            public EcamAddress(uint address) : this()
            {
                Bus = (int)BitHelper.GetValue(address, 20, 8);
                Device = (int)BitHelper.GetValue(address, 15, 5);
                Function = (int)BitHelper.GetValue(address, 12, 3);
                Offset = BitHelper.GetValue(address, 0, 12);
            }

            public int Bus;
            public int Device;
            public int Function;
            public uint Offset;
        }

        private class AddressTranslationTable
        {
            public AddressTranslationTable(MPFS_PCIe parent)
            {
                this.parent = parent;
                this.size = MinSize;
            }

            public ulong Translate(ulong address)
            {
                return address - SourceAddress + DestinationAddress;
            }

            public bool DoesAddressHit(ulong address)
            {
                return Enabled && address >= SourceAddress && ((address - SourceAddress) & TranslationMask) == 0;
            }

            public uint SizePower
            {
                get
                {
                    return size;
                }
                set
                {
                    if(value > MaxSize || value < MinSize)
                    {
                        parent.Log(LogLevel.Warning, "Trying to set address translation table size out of bounds: writing {0} when should be between {1} and {2}. Ignoring.", value, MinSize, MaxSize);
                    }
                    size = value;
                }
            }

            public bool Enabled;
            public ulong SourceAddress;
            public ulong DestinationAddress;

            public PCIeSpace TargetSpace;

            // we handle 63 separately as it would overflow the ulong. The result is correct.
            public ulong TranslationMask => size == MaxSize ? 0ul : 0ul - (2ul << ((int)SizePower + 1));

            private uint size;
            private MPFS_PCIe parent;

            private const int MinSize = 11;
            private const int MaxSize = 63;
        }

        private enum AddressTranslationRegisters
        {
            SourceAddressLowAndParameters = 0x0,
            SourceAddressHigh = 0x4,
            DestinationAddressLow = 0x8,
            DestinationAddressHigh = 0xC,
            TranslationParametersLow = 0x10,
            Reserved = 0x14,
            TranslationMaskLow = 0x18,
            TranslationMaskHigh = 0x1C,
        }

        private enum Registers
        {
            /* PCIE Bridge Control Registers */
            BRIDGE_VER = 0x0,
            BRIDGE_BUS = 0x4,
            BRIDGE_IMPL_IF = 0x8,
            /* reserved */
            PCIE_IF_CONF = 0x10,
            PCIE_BASIC_CONF = 0x14,
            PCIE_BASIC_STATUS = 0x18,
            /* reserved */
            AXI_SLVL_CONF = 0x24,
            /* reserved */
            AXI_MST0_CONF = 0x30,
            AXI_SLV0_CONF = 0x34,
            /* reserved */
            GEN_SETTINGS = 0x80,
            PCIE_CFGCTRL = 0x84,
            PCIE_PIPE_DW0 = 0x88,
            PCIE_PIPE_DW1 = 0x8C,
            PCIE_VC_CRED_DW0 = 0x90,
            PCIE_VC_CRED_DW1 = 0x94,
            PCIE_PCI_IDS_DW0 = 0x98,
            PCIE_PCI_IDS_DW1 = 0x9C,
            PCIE_PCI_IDS_DW2 = 0xA0,
            PCIE_PCI_LPM = 0xA4,
            PCIE_PCI_IRQ_DW0 = 0xA8,
            PCIE_PCI_IRQ_DW1 = 0xAC,
            PCIE_PCI_IRQ_DW2 = 0xB0,
            PCIE_PCI_IOV_DW0 = 0xB4,
            PCIE_PCI_IOV_DW1 = 0xB8,
            /* reserved */
            PCIE_PEX_DEV = 0xC0,
            PCIE_PEX_DEV2 = 0xC4,
            PCIE_PEX_LINK = 0xC8,
            PCIE_PEX_SLOT = 0xCC,
            PCIE_PEX_ROOT_VC = 0xD0,
            PCIE_PEX_SPC = 0xD4,
            PCIE_PEX_SPC2 = 0xD8,
            PCIE_PEX_NFTS = 0xDC,
            PCIE_PEX_L1SS = 0xE0,
            PCIE_BAR_01_DW0 = 0xE4,
            PCIE_BAR_01_DW1 = 0xE8,
            PCIE_BAR_23_DW0 = 0xEC,
            PCIE_BAR_23_DW1 = 0xF0,
            PCIE_BAR_45_DW0 = 0xF4,
            PCIE_BAR_45_DW1 = 0xF8,
            PCIE_BAR_WIN = 0xFC,
            PCIE_EQ_PRESET_DW0 = 0x100,
            PCIE_EQ_PRESET_DW1 = 0x104,
            PCIE_EQ_PRESET_DW2 = 0x108,
            PCIE_EQ_PRESET_DW3 = 0x10C,
            PCIE_EQ_PRESET_DW4 = 0x110,
            PCIE_EQ_PRESET_DW5 = 0x114,
            PCIE_EQ_PRESET_DW6 = 0x118,
            PCIE_EQ_PRESET_DW7 = 0x11C,
            PCIE_SRIOV_DW0 = 0x120,
            PCIE_SRIOV_DW1 = 0x124,
            PCIE_SRIOV_DW2 = 0x128,
            PCIE_SRIOV_DW3 = 0x12C,
            PCIE_SRIOV_DW4 = 0x130,
            PCIE_SRIOV_DW5 = 0x134,
            PCIE_SRIOV_DW6 = 0x138,
            PCIE_SRIOV_DW7 = 0x13C,
            PCIE_CFGNUM = 0x140,
            /* reserved */
            PM_CONF_DW0 = 0x174,
            PM_CONF_DW1 = 0x178,
            PM_CONF_DW2 = 0x17C,
            IMASK_LOCAL = 0x180,
            ISTATUS_LOCAL = 0x184,
            IMASK_HOST = 0x188,
            ISTATUS_HOST = 0x18C,
            IMSI_ADDR = 0x190,
            ISTATUS_MSI = 0x194,
            ICMD_PM = 0x198,
            ISTATUS_PM = 0x19C,
            ATS_PRI_REPORT = 0x1A0,
            LTR_VALUES = 0x1A4,
            /* reserved */
            ISTATUS_DMA0 = 0x1B0,
            ISTATUS_DMA1 = 0x1B4,
            /* reserved */
            ISTATUS_P_ADT_WIN0 = 0x1D8,
            ISTATUS_P_ADT_WIN1 = 0x1DC,
            ISTATUS_A_ADT_SLV0 = 0x1E0,
            ISTATUS_A_ADT_SLV1 = 0x1E4,
            ISTATUS_A_ADT_SLV2 = 0x1E8,
            ISTATUS_A_ADT_SLV3 = 0x1EC,
            /* reserved */
            ROUTING_RULES_R_DW0 = 0x200,
            ROUTING_RULES_R_DW1 = 0x204,
            ROUTING_RULES_R_DW2 = 0x208,
            ROUTING_RULES_R_DW3 = 0x20C,
            ROUTING_RULES_R_DW4 = 0x210,
            ROUTING_RULES_R_DW5 = 0x214,
            ROUTING_RULES_R_DW6 = 0x218,
            ROUTING_RULES_R_DW7 = 0x21C,
            ROUTING_RULES_R_DW8 = 0x220,
            ROUTING_RULES_R_DW9 = 0x224,
            ROUTING_RULES_R_DW10 = 0x228,
            ROUTING_RULES_R_DW11 = 0x22C,
            ROUTING_RULES_R_DW12 = 0x230,
            ROUTING_RULES_R_DW13 = 0x234,
            ROUTING_RULES_R_DW14 = 0x238,
            ROUTING_RULES_R_DW15 = 0x23C,
            ROUTING_RULES_W_DW0 = 0x240,
            ROUTING_RULES_W_DW1 = 0x244,
            ROUTING_RULES_W_DW2 = 0x248,
            ROUTING_RULES_W_DW3 = 0x24C,
            ROUTING_RULES_W_DW4 = 0x250,
            ROUTING_RULES_W_DW5 = 0x254,
            ROUTING_RULES_W_DW6 = 0x258,
            ROUTING_RULES_W_DW7 = 0x25C,
            ROUTING_RULES_W_DW8 = 0x260,
            ROUTING_RULES_W_DW9 = 0x264,
            ROUTING_RULES_W_DW10 = 0x268,
            ROUTING_RULES_W_DW11 = 0x26C,
            ROUTING_RULES_W_DW12 = 0x270,
            ROUTING_RULES_W_DW13 = 0x274,
            ROUTING_RULES_W_DW14 = 0x278,
            ROUTING_RULES_W_DW15 = 0x27C,
            ARBITRATION_RULES_DW0 = 0x280,
            ARBITRATION_RULES_DW1 = 0x284,
            ARBITRATION_RULES_DW2 = 0x288,
            ARBITRATION_RULES_DW3 = 0x28C,
            ARBITRATION_RULES_DW4 = 0x290,
            ARBITRATION_RULES_DW5 = 0x294,
            ARBITRATION_RULES_DW6 = 0x298,
            ARBITRATION_RULES_DW7 = 0x29C,
            ARBITRATION_RULES_DW8 = 0x2A0,
            ARBITRATION_RULES_DW9 = 0x2A4,
            ARBITRATION_RULES_DW10 = 0x2A8,
            ARBITRATION_RULES_DW11 = 0x2AC,
            ARBITRATION_RULES_DW12 = 0x2B0,
            ARBITRATION_RULES_DW13 = 0x2B4,
            ARBITRATION_RULES_DW14 = 0x2B8,
            ARBITRATION_RULES_DW15 = 0x2BC,
            PRIORITY_RULES_DW0 = 0x2C0,
            PRIORITY_RULES_DW1 = 0x2C4,
            PRIORITY_RULES_DW2 = 0x2C8,
            PRIORITY_RULES_DW3 = 0x2CC,
            PRIORITY_RULES_DW4 = 0x2D0,
            PRIORITY_RULES_DW5 = 0x2D4,
            PRIORITY_RULES_DW6 = 0x2D8,
            PRIORITY_RULES_DW7 = 0x2DC,
            PRIORITY_RULES_DW8 = 0x2E0,
            PRIORITY_RULES_DW9 = 0x2E4,
            PRIORITY_RULES_DW10 = 0x2E8,
            PRIORITY_RULES_DW11 = 0x2EC,
            PRIORITY_RULES_DW12 = 0x2F0,
            PRIORITY_RULES_DW13 = 0x2F4,
            PRIORITY_RULES_DW14 = 0x2F8,
            PRIORITY_RULES_DW15 = 0x2FC,
            /* reserved */
            P2A_TC_QOS_CONV = 0x3C0,
            P2A_ATTR_CACHE_CONV = 0x3C4,
            P2A_NC_BASE_ADDR_DW0 = 0x3C8,
            P2A_NC_BASE_ADDR_DW1 = 0x3CC,
            /* reserved */
            DMA0_SRC_PARAM = 0x400,
            DMA0_DESTPARAM = 0x404,
            DMA0_SRCADDR_LDW = 0x408,
            DMA0_SRCADDR_UDW = 0x40C,
            DMA0_DESTADDR_LDW = 0x410,
            DMA0_DESTADDR_UDW = 0x414,
            DMA0_LENGTH = 0x418,
            DMA0_CONTROL = 0x41C,
            DMA0_STATUS = 0x420,
            DMA0_PRC_LENGTH = 0x424,
            DMA0_SHARE_ACCESS = 0x428,
            /* reserved */
            DMA1_SRC_PARAM = 0x440,
            DMA1_DESTPARAM = 0x444,
            DMA1_SRCADDR_LDW = 0x448,
            DMA1_SRCADDR_UDW = 0x44C,
            DMA1_DESTADDR_LDW = 0x450,
            DMA1_DESTADDR_UDW = 0x454,
            DMA1_LENGTH = 0x458,
            DMA1_CONTROL = 0x45C,
            DMA1_STATUS = 0x460,
            DMA1_PRC_LENGTH = 0x464,
            DMA1_SHARE_ACCESS = 0x468,
            /* reserved */
            ATR0_PCIE_WIN0_SRCADDR_PARAM = 0x600,
            ATR0_PCIE_WIN0_SRC_ADDR = 0x604,
            ATR0_PCIE_WIN0_TRSL_ADDR_LSB = 0x608,
            ATR0_PCIE_WIN0_TRSL_ADDR_UDW = 0x60C,
            ATR0_PCIE_WIN0_TRSL_PARAM = 0x610,
            /* reserved */
            ATR0_PCIE_WIN0_TRSL_MASK_DW0 = 0x618,
            ATR0_PCIE_WIN0_TRSL_MASK_DW1 = 0x61C,
            ATR1_PCIE_WIN0_SRCADDR_PARAM = 0x620,
            ATR1_PCIE_WIN0_SRC_ADDR = 0x624,
            ATR1_PCIE_WIN0_TRSL_ADDR_LSB = 0x628,
            ATR1_PCIE_WIN0_TRSL_ADDR_UDW = 0x62C,
            ATR1_PCIE_WIN0_TRSL_PARAM = 0x630,
            /* reserved */
            ATR1_PCIE_WIN0_TRSL_MASK_DW0 = 0x638,
            ATR1_PCIE_WIN0_TRSL_MASK_DW1 = 0x63C,
            ATR2_PCIE_WIN0_SRCADDR_PARAM = 0x640,
            ATR2_PCIE_WIN0_SRC_ADDR = 0x644,
            ATR2_PCIE_WIN0_TRSL_ADDR_LSB = 0x648,
            ATR2_PCIE_WIN0_TRSL_ADDR_UDW = 0x64C,
            ATR2_PCIE_WIN0_TRSL_PARAM = 0x650,
            /* reserved */
            ATR2_PCIE_WIN0_TRSL_MASK_DW0 = 0x658,
            ATR2_PCIE_WIN0_TRSL_MASK_DW1 = 0x65C,
            ATR3_PCIE_WIN0_SRCADDR_PARAM = 0x660,
            ATR3_PCIE_WIN0_SRC_ADDR = 0x664,
            ATR3_PCIE_WIN0_TRSL_ADDR_LSB = 0x668,
            ATR3_PCIE_WIN0_TRSL_ADDR_UDW = 0x66C,
            ATR3_PCIE_WIN0_TRSL_PARAM = 0x670,
            /* reserved */
            ATR3_PCIE_WIN0_TRSL_MASK_DW0 = 0x678,
            ATR3_PCIE_WIN0_TRSL_MASK_DW1 = 0x67C,
            ATR4_PCIE_WIN0_SRCADDR_PARAM = 0x680,
            ATR4_PCIE_WIN0_SRC_ADDR = 0x684,
            ATR4_PCIE_WIN0_TRSL_ADDR_LSB = 0x688,
            ATR4_PCIE_WIN0_TRSL_ADDR_UDW = 0x68C,
            ATR4_PCIE_WIN0_TRSL_PARAM = 0x690,
            /* reserved */
            ATR4_PCIE_WIN0_TRSL_MASK_DW0 = 0x698,
            ATR4_PCIE_WIN0_TRSL_MASK_DW1 = 0x69C,
            ATR5_PCIE_WIN0_SRCADDR_PARAM = 0x6A0,
            ATR5_PCIE_WIN0_SRC_ADDR = 0x6A4,
            ATR5_PCIE_WIN0_TRSL_ADDR_LSB = 0x6A8,
            ATR5_PCIE_WIN0_TRSL_ADDR_UDW = 0x6AC,
            ATR5_PCIE_WIN0_TRSL_PARAM = 0x6B0,
            /* reserved */
            ATR5_PCIE_WIN0_TRSL_MASK_DW0 = 0x6B8,
            ATR5_PCIE_WIN0_TRSL_MASK_DW1 = 0x6BC,
            ATR6_PCIE_WIN0_SRCADDR_PARAM = 0x6C0,
            ATR6_PCIE_WIN0_SRC_ADDR = 0x6C4,
            ATR6_PCIE_WIN0_TRSL_ADDR_LSB = 0x6C8,
            ATR6_PCIE_WIN0_TRSL_ADDR_UDW = 0x6CC,
            ATR6_PCIE_WIN0_TRSL_PARAM = 0x6D0,
            /* reserved */
            ATR6_PCIE_WIN0_TRSL_MASK_DW0 = 0x6D8,
            ATR6_PCIE_WIN0_TRSL_MASK_DW1 = 0x6DC,
            ATR7_PCIE_WIN0_SRCADDR_PARAM = 0x6E0,
            ATR7_PCIE_WIN0_SRC_ADDR = 0x6E4,
            ATR7_PCIE_WIN0_TRSL_ADDR_LSB = 0x6E8,
            ATR7_PCIE_WIN0_TRSL_ADDR_UDW = 0x6EC,
            ATR7_PCIE_WIN0_TRSL_PARAM = 0x6F0,
            /* reserved */
            ATR7_PCIE_WIN0_TRSL_MASK_DW0 = 0x6F8,
            ATR7_PCIE_WIN0_TRSL_MASK_DW1 = 0x6FC,

            ATR0_PCIE_WIN1_SRCADDR_PARAM = 0x700,
            ATR0_PCIE_WIN1_SRC_ADDR = 0x704,
            ATR0_PCIE_WIN1_TRSL_ADDR_LSB = 0x708,
            ATR0_PCIE_WIN1_TRSL_ADDR_UDW = 0x70C,
            ATR0_PCIE_WIN1_TRSL_PARAM = 0x710,
            /* reserved */
            ATR0_PCIE_WIN1_TRSL_MASK_DW0 = 0x718,
            ATR0_PCIE_WIN1_TRSL_MASK_DW1 = 0x71C,
            ATR1_PCIE_WIN1_SRCADDR_PARAM = 0x720,
            ATR1_PCIE_WIN1_SRC_ADDR = 0x724,
            ATR1_PCIE_WIN1_TRSL_ADDR_LSB = 0x728,
            ATR1_PCIE_WIN1_TRSL_ADDR_UDW = 0x72C,
            ATR1_PCIE_WIN1_TRSL_PARAM = 0x730,
            /* reserved */
            ATR1_PCIE_WIN1_TRSL_MASK_DW0 = 0x738,
            ATR1_PCIE_WIN1_TRSL_MASK_DW1 = 0x73C,
            ATR2_PCIE_WIN1_SRCADDR_PARAM = 0x740,
            ATR2_PCIE_WIN1_SRC_ADDR = 0x744,
            ATR2_PCIE_WIN1_TRSL_ADDR_LSB = 0x748,
            ATR2_PCIE_WIN1_TRSL_ADDR_UDW = 0x74C,
            ATR2_PCIE_WIN1_TRSL_PARAM = 0x750,
            /* reserved */
            ATR2_PCIE_WIN1_TRSL_MASK_DW0 = 0x758,
            ATR2_PCIE_WIN1_TRSL_MASK_DW1 = 0x75C,
            ATR3_PCIE_WIN1_SRCADDR_PARAM = 0x760,
            ATR3_PCIE_WIN1_SRC_ADDR = 0x764,
            ATR3_PCIE_WIN1_TRSL_ADDR_LSB = 0x768,
            ATR3_PCIE_WIN1_TRSL_ADDR_UDW = 0x76C,
            ATR3_PCIE_WIN1_TRSL_PARAM = 0x770,
            /* reserved */
            ATR3_PCIE_WIN1_TRSL_MASK_DW0 = 0x778,
            ATR3_PCIE_WIN1_TRSL_MASK_DW1 = 0x77C,
            ATR4_PCIE_WIN1_SRCADDR_PARAM = 0x780,
            ATR4_PCIE_WIN1_SRC_ADDR = 0x784,
            ATR4_PCIE_WIN1_TRSL_ADDR_LSB = 0x788,
            ATR4_PCIE_WIN1_TRSL_ADDR_UDW = 0x78C,
            ATR4_PCIE_WIN1_TRSL_PARAM = 0x790,
            /* reserved */
            ATR4_PCIE_WIN1_TRSL_MASK_DW0 = 0x798,
            ATR4_PCIE_WIN1_TRSL_MASK_DW1 = 0x79C,
            ATR5_PCIE_WIN1_SRCADDR_PARAM = 0x7A0,
            ATR5_PCIE_WIN1_SRC_ADDR = 0x7A4,
            ATR5_PCIE_WIN1_TRSL_ADDR_LSB = 0x7A8,
            ATR5_PCIE_WIN1_TRSL_ADDR_UDW = 0x7AC,
            ATR5_PCIE_WIN1_TRSL_PARAM = 0x7B0,
            /* reserved */
            ATR5_PCIE_WIN1_TRSL_MASK_DW0 = 0x7B8,
            ATR5_PCIE_WIN1_TRSL_MASK_DW1 = 0x7BC,
            ATR6_PCIE_WIN1_SRCADDR_PARAM = 0x7C0,
            ATR6_PCIE_WIN1_SRC_ADDR = 0x7C4,
            ATR6_PCIE_WIN1_TRSL_ADDR_LSB = 0x7C8,
            ATR6_PCIE_WIN1_TRSL_ADDR_UDW = 0x7CC,
            ATR6_PCIE_WIN1_TRSL_PARAM = 0x7D0,
            /* reserved */
            ATR6_PCIE_WIN1_TRSL_MASK_DW0 = 0x7D8,
            ATR6_PCIE_WIN1_TRSL_MASK_DW1 = 0x7DC,
            ATR7_PCIE_WIN1_SRCADDR_PARAM = 0x7E0,
            ATR7_PCIE_WIN1_SRC_ADDR = 0x7E4,
            ATR7_PCIE_WIN1_TRSL_ADDR_LSB = 0x7E8,
            ATR7_PCIE_WIN1_TRSL_ADDR_UDW = 0x7EC,
            ATR7_PCIE_WIN1_TRSL_PARAM = 0x7F0,
            /* reserved */
            ATR7_PCIE_WIN1_TRSL_MASK_DW0 = 0x7F8,
            ATR7_PCIE_WIN1_TRSL_MASK_DW1 = 0x7FC,

            ATR0_AXI4_SLV0_SRCADDR_PARAM = 0x800,
            ATR0_AXI4_SLV0_SRC_ADDR = 0x804,
            ATR0_AXI4_SLV0_TRSL_ADDR_LSB = 0x808,
            ATR0_AXI4_SLV0_TRSL_ADDR_UDW = 0x80C,
            ATR0_AXI4_SLV0_TRSL_PARAM = 0x810,
            /* reserved */
            ATR0_AXI4_SLV0_TRSL_MASK_DW0 = 0x818,
            ATR0_AXI4_SLV0_TRSL_MASK_DW1 = 0x81C,
            ATR1_AXI4_SLV0_SRCADDR_PARAM = 0x820,
            ATR1_AXI4_SLV0_SRC_ADDR = 0x824,
            ATR1_AXI4_SLV0_TRSL_ADDR_LSB = 0x828,
            ATR1_AXI4_SLV0_TRSL_ADDR_UDW = 0x82C,
            ATR1_AXI4_SLV0_TRSL_PARAM = 0x830,
            /* reserved */
            ATR1_AXI4_SLV0_TRSL_MASK_DW0 = 0x838,
            ATR1_AXI4_SLV0_TRSL_MASK_DW1 = 0x83C,
            ATR2_AXI4_SLV0_SRCADDR_PARAM = 0x840,
            ATR2_AXI4_SLV0_SRC_ADDR = 0x844,
            ATR2_AXI4_SLV0_TRSL_ADDR_LSB = 0x848,
            ATR2_AXI4_SLV0_TRSL_ADDR_UDW = 0x84C,
            ATR2_AXI4_SLV0_TRSL_PARAM = 0x850,
            /* reserved */
            ATR2_AXI4_SLV0_TRSL_MASK_DW0 = 0x858,
            ATR2_AXI4_SLV0_TRSL_MASK_DW1 = 0x85C,
            ATR3_AXI4_SLV0_SRCADDR_PARAM = 0x860,
            ATR3_AXI4_SLV0_SRC_ADDR = 0x864,
            ATR3_AXI4_SLV0_TRSL_ADDR_LSB = 0x868,
            ATR3_AXI4_SLV0_TRSL_ADDR_UDW = 0x86C,
            ATR3_AXI4_SLV0_TRSL_PARAM = 0x870,
            /* reserved */
            ATR3_AXI4_SLV0_TRSL_MASK_DW0 = 0x878,
            ATR3_AXI4_SLV0_TRSL_MASK_DW1 = 0x87C,
            ATR4_AXI4_SLV0_SRCADDR_PARAM = 0x880,
            ATR4_AXI4_SLV0_SRC_ADDR = 0x884,
            ATR4_AXI4_SLV0_TRSL_ADDR_LSB = 0x888,
            ATR4_AXI4_SLV0_TRSL_ADDR_UDW = 0x88C,
            ATR4_AXI4_SLV0_TRSL_PARAM = 0x890,
            /* reserved */
            ATR4_AXI4_SLV0_TRSL_MASK_DW0 = 0x898,
            ATR4_AXI4_SLV0_TRSL_MASK_DW1 = 0x89C,
            ATR5_AXI4_SLV0_SRCADDR_PARAM = 0x8A0,
            ATR5_AXI4_SLV0_SRC_ADDR = 0x8A4,
            ATR5_AXI4_SLV0_TRSL_ADDR_LSB = 0x8A8,
            ATR5_AXI4_SLV0_TRSL_ADDR_UDW = 0x8AC,
            ATR5_AXI4_SLV0_TRSL_PARAM = 0x8B0,
            /* reserved */
            ATR5_AXI4_SLV0_TRSL_MASK_DW0 = 0x8B8,
            ATR5_AXI4_SLV0_TRSL_MASK_DW1 = 0x8BC,
            ATR6_AXI4_SLV0_SRCADDR_PARAM = 0x8C0,
            ATR6_AXI4_SLV0_SRC_ADDR = 0x8C4,
            ATR6_AXI4_SLV0_TRSL_ADDR_LSB = 0x8C8,
            ATR6_AXI4_SLV0_TRSL_ADDR_UDW = 0x8CC,
            ATR6_AXI4_SLV0_TRSL_PARAM = 0x8D0,
            /* reserved */
            ATR6_AXI4_SLV0_TRSL_MASK_DW0 = 0x8D8,
            ATR6_AXI4_SLV0_TRSL_MASK_DW1 = 0x8DC,
            ATR7_AXI4_SLV0_SRCADDR_PARAM = 0x8E0,
            ATR7_AXI4_SLV0_SRC_ADDR = 0x8E4,
            ATR7_AXI4_SLV0_TRSL_ADDR_LSB = 0x8E8,
            ATR7_AXI4_SLV0_TRSL_ADDR_UDW = 0x8EC,
            ATR7_AXI4_SLV0_TRSL_PARAM = 0x8F0,
            /* reserved */
            ATR7_AXI4_SLV0_TRSL_MASK_DW0 = 0x8F8,
            ATR7_AXI4_SLV0_TRSL_MASK_DW1 = 0x8FC,

            /* PCIE Control Registers */
            SEC_ERROR_EVENT_CNT = 0x2020,
            DED_ERROR_EVENT_CNT = 0x2024,
            SEC_ERROR_INT = 0x2028,
            SEC_ERROR_INT_MASK = 0x202C, // 0x220C according to the docs. A typo?
            DED_ERROR_INT = 0x2030,
            DED_ERROR_INT_MASK = 0x2034,
            ECC_CONTROL = 0x2038,
            ECC_ERR_LOC = 0x203C,
            RAM_MARGIN_1 = 0x2040,
            RAM_MARGIN_2 = 0x2044,
            RAM_POWER_CONTROL = 0x2048,
            /* reserved */
            DEBUG_SEL = 0x2050,
            /* reserved */
            LTSSM_STATE = 0x205C,
            PHY_COMMON_INTERFACE = 0x2060,
            PL_TX_LANEIF_0 = 0x2064,
            PL_RX_LANEIF_0 = 0x2068,
            PL_WAKECLKREQ = 0x206C,
            /* reserved */
            PCICONF_PCI_IDS_OVERRIDE = 0x2080,
            PCICONF_PCI_IDS_31_0 = 0x2084,
            PCICONF_PCI_IDS_63_32 = 0x2088,
            PCICONF_PCI_IDS_95_64 = 0x208C,
            /* reserved */
            PCIE_PEX_DEV_LINK_SPC2 = 0x20A0,
            //PCIE_PEX_SPC = 0x20A4, <-- available in the header, but not in the docs. The same name occurs under 0xD4
            /* reserved */
            PCIE_AXI_MASTER_ATR_CFG0 = 0x2100,
            PCIE_AXI_MASTER_ATR_CFG1 = 0x2104,
            PCIE_AXI_MASTER_ATR_CFG2 = 0x2108,
            /* reserved */
            AXI_SLAVE_PCIE_ATR_CFG0 = 0x2120,
            AXI_SLAVE_PCIE_ATR_CFG1 = 0x2124,
            AXI_SLAVE_PCIE_ATR_CFG2 = 0x2128,
            /* reserved */
            PCIE_BAR_01 = 0x2140,
            PCIE_BAR_23 = 0x2144,
            PCIE_BAR_45 = 0x2148,
            PCIE_EVENT_INT = 0x214C,
        }
    }
}