//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;

using static Antmicro.Renode.Peripherals.Bus.WindowMMUBusController;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public partial class ARM_SMMUv3 : SimpleContainer<IPeripheral>, IDoubleWordPeripheral, IQuadWordPeripheral,
        IProvidesRegisterCollection<DoubleWordRegisterCollection>, IProvidesRegisterCollection<QuadWordRegisterCollection>, IKnownSize
    {
        static ARM_SMMUv3()
        {
            // Supported command list is fixed for all SMMUs
            var commands = new Dictionary<Opcode, Type>();
            foreach(var command in typeof(ARM_SMMUv3).GetNestedTypes().Select(type => new { type, attr = type.GetCustomAttribute<CommandAttribute>() }).Where(p => p.attr != null))
            {
                commands.Add(command.attr.Opcode, command.type);
            }
            registeredCommands = commands;
        }

        public ARM_SMMUv3(IMachine machine, IPeripheral context = null) : base(machine)
        {
            this.Context = context ?? this; // Context used to read descriptors and tables from memory
            sysbus = machine.GetSystemBus(this);
            RegistersCollection = new DoubleWordRegisterCollection(this);
            QuadWordRegisters = new QuadWordRegisterCollection(this);
            DefineRegisters();
            commandQueue = new WrappingQueue<Command>(this, sysbus, WrappingQueue<Command>.Role.Consumer,
                commandQueueAddress, commandQueueShift, commandQueueProduce, commandQueueConsume, GetCommandType);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public ulong ReadQuadWord(long offset)
        {
            return ((IProvidesRegisterCollection<QuadWordRegisterCollection>)this).RegistersCollection.Read(offset);
        }

        public void WriteQuadWord(long offset, ulong value)
        {
            ((IProvidesRegisterCollection<QuadWordRegisterCollection>)this).RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
        }

        public override void Register(IPeripheral peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            if(peripheral is IBusPeripheral busPeripheral)
            {
                var busController = new ARM_SMMUv3BusController(this, sysbus);
                streamControllers.Add(registrationPoint.Address, busController);
                machine.RegisterBusController(busPeripheral, busController);
            }
            else
            {
                throw new RecoverableException($"Don't know how to register a {peripheral?.GetType()?.Name ?? "(null)"}");
            }
            base.Register(peripheral, registrationPoint);
            streams.Add(registrationPoint.Address, peripheral);
        }

        public override void Unregister(IPeripheral peripheral)
        {
            var streamId = streams[peripheral];
            if(peripheral is IBusPeripheral busPeripheral)
            {
                machine.RegisterBusController(busPeripheral, sysbus); // Explicitly register sysbus as the controller to remove the SMMU bus controller
            }
            streamControllers.Remove(streamId);
            base.Unregister(peripheral);
            streams.Remove(peripheral);
        }

        public MMUWindow GetWindowFromPageTable(ulong address, IPeripheral dmaContext)
        {
            if(!streams.TryGetValue(dmaContext, out var streamId))
            {
                this.WarningLog("No stream for context {0}", dmaContext);
                return null;
            }
            var ste = streamTable[streamId];
            var cd = ReadStruct<ContextDescriptor>(streamTable[streamId].S1ContextPtr);
            // TODO: treating UseIncoming as Privileged
            var privileged = streamTable[streamId].PRIVCFG != Privilege.Unprivileged;

            ulong? tableAddr = null; // Start with TTB0/TTB1
            for(var level = 0; level <= MaxPageTableLevel; ++level)
            {
                var maybeTp = cd.GetTranslationParams(address, level, tableAddr);
                if(!maybeTp.HasValue)
                {
                    this.WarningLog("Translation failed for address 0x{0:x} at level {1}", address, level);
                    return null;
                }
                var tp = maybeTp.Value;
                var pte = ReadSubclass<PageTableEntry>(tp.TableAddress, GetPageTableEntryType);
                if(pte is BlockDescriptor block)
                {
                    if(level == 1 && tp.PageSizeShift != 12)
                    {
                        this.WarningLog("Translation failed for address 0x{0:x}: block entry allowed on level 1 only with 4K pages, but we have {1}B",
                            address, Misc.NormalizeBinary(1 << tp.PageSizeShift));
                        return null;
                    }
                    if(level > 2)
                    {
                        this.WarningLog("Translation failed for address 0x{0:x}: invalid block descriptor at level {1}", address, level);
                        return null;
                    }

                    var blockSize = 1UL << GetVaSizeShiftAtLevel(level, tp.PageSizeShift);
                    var mask = ~(blockSize - 1);
                    var virt = address & mask;
                    var phys = (block.NextTableAddress << tp.PageSizeShift) & mask;
                    var win = new MMUWindow(this)
                    {
                        Start = virt,
                        End = virt + blockSize,
                        Offset = (long)(phys - virt),
                        Privileges = BusAccessPrivileges.Read | BusAccessPrivileges.Write, // TODO: Privileges for block descriptors
                    };
                    win.AssertIsValid();
                    return win;
                }
                else if(pte is TableDescriptor table)
                {
                    if(level == MaxPageTableLevel)
                    {
                        var blockSize = 1UL << tp.PageSizeShift;
                        var mask = ~(blockSize - 1);
                        var virt = address & mask;
                        var phys = table.OutputAddress << tp.PageSizeShift;
                        var win = new MMUWindow(this)
                        {
                            Start = virt,
                            End = virt + (blockSize),
                            Offset = (long)(phys - virt),
                            Privileges = table.GetPrivileges(privileged),
                        };
                        win.AssertIsValid();
                        return win;
                    }
                    tableAddr = table.OutputAddress << tp.PageSizeShift;
                }
                else
                {
                    this.WarningLog("Translation failed for address 0x{0:x}: invalid PTE at level {1}: {2} @ 0x{3:x}", address, level, pte, tp.TableAddress);
                    return null;
                }
            }
            throw new Exception("Unreachable");
        }

        public long Size => 0x24000;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public QuadWordRegisterCollection QuadWordRegisters { get; }

        public readonly IPeripheral Context;

        QuadWordRegisterCollection IProvidesRegisterCollection<QuadWordRegisterCollection>.RegistersCollection => QuadWordRegisters;

        private static Type GetPageTableEntryType(IList<byte> pte)
        {
            switch((PageTableEntryType)(pte[0] & 0b11))
            {
            case PageTableEntryType.Block:
                return typeof(BlockDescriptor);
            case PageTableEntryType.Table:
                return typeof(TableDescriptor);
            default:
                return typeof(PageTableEntry); // So we can examine Valid
            }
        }

        private static Type GetCommandType(IList<byte> command)
        {
            // Default to the base Command to log the unhandled operation.
            return registeredCommands.GetOrDefault((Opcode)command[0], typeof(Command));
        }

        private static int GetIndexBitsPerLevel(int pageSizeShift)
        {
            return pageSizeShift - 3;
        }

        private static int GetVaSizeShiftAtLevel(int level, int pageSizeShift)
        {
            return pageSizeShift + GetIndexBitsPerLevel(pageSizeShift) * (MaxPageTableLevel - level);
        }

        private static readonly IReadOnlyDictionary<Opcode, Type> registeredCommands;

        private void DefineRegisters()
        {
            var vatosSupported = false;
            var priSupported = false;
            var msiSupported = false;
            var ecmdqSupported = false;
            var dptSupported = false;
            var mpamSupported = false;
            var s2piSupported = false;

            Registers.SMMU_IDR0.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "S2P")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "S1P")
                .WithEnumField<DoubleWordRegister, TranslationTableFormat>(2, 2, FieldMode.Read, valueProviderCallback: _ => TranslationTableFormat.AArch64, name: "TTF")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "COHACC")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => false, name: "BTM")
                .WithEnumField<DoubleWordRegister, HardwareTranslationTableUpdate>(6, 2, FieldMode.Read, valueProviderCallback: _ => HardwareTranslationTableUpdate.NoFlagUpdates, name: "HTTU")
                .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => false, name: "DORMHINT")
                .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => false, name: "Hyp")
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => false, name: "ATS")
                .WithFlag(11, FieldMode.Read, valueProviderCallback: _ => false, name: "NS1ATS")
                .WithFlag(12, FieldMode.Read, valueProviderCallback: _ => true, name: "ASID16")
                .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => msiSupported, name: "MSI")
                .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => false, name: "SEV")
                .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => false, name: "ATOS")
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => priSupported, name: "PRI")
                .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => false, name: "VMW")
                .WithFlag(18, FieldMode.Read, valueProviderCallback: _ => true, name: "VMID16")
                .WithFlag(19, FieldMode.Read, valueProviderCallback: _ => false, name: "CD2L")
                .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => vatosSupported, name: "VATOS")
                .WithEnumField<DoubleWordRegister, EndiannessSupport>(21, 2, FieldMode.Read, valueProviderCallback: _ => EndiannessSupport.LittleEndian, name: "TTENDIAN")
                .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => false, name: "ATSRECERR")
                .WithEnumField<DoubleWordRegister, StallModelSupport>(24, 2, FieldMode.Read, valueProviderCallback: _ => StallModelSupport.StallNotSupported, name: "STALL_MODEL")
                .WithFlag(26, FieldMode.Read, valueProviderCallback: _ => false, name: "TERM_MODEL")
                .WithEnumField<DoubleWordRegister, StreamTableLevel>(27, 2, FieldMode.Read, valueProviderCallback: _ => StreamTableLevel.Linear, name: "ST_LEVEL")
                .WithReservedBits(29, 1)
                .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => false, name: "RME_IMPL")
                .WithReservedBits(31, 1)
            ;

            Registers.SMMU_IDR1.Define(this)
                .WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ => StreamIdBits, name: "SIDSIZE")
                .WithValueField(6, 5, FieldMode.Read, valueProviderCallback: _ => 0, name: "SSIDSIZE")
                .WithValueField(11, 5, FieldMode.Read, valueProviderCallback: _ => 7, name: "PRIQS")
                .WithValueField(16, 5, FieldMode.Read, valueProviderCallback: _ => 7, name: "EVENTQS")
                .WithValueField(21, 5, FieldMode.Read, valueProviderCallback: _ => MaxCommandQueueShift, name: "CMDQS")
                .WithFlag(26, FieldMode.Read, valueProviderCallback: _ => false, name: "ATTR_PERMS_OVR")
                .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => false, name: "ATTR_TYPES_OVR")
                .WithFlag(28, FieldMode.Read, valueProviderCallback: _ => false, name: "REL") // RES0 when QUEUES_PRESET, TABLES_PRESET both 0
                .WithFlag(29, FieldMode.Read, valueProviderCallback: _ => false, name: "QUEUES_PRESET")
                .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => false, name: "TABLES_PRESET")
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => ecmdqSupported, name: "ECMDQ")
            ;

            if(vatosSupported)
            {
                Registers.SMMU_IDR2.Define(this)
                    .WithValueField(0, 10, FieldMode.Read, valueProviderCallback: _ => (ulong)(Registers.SMMU_VATOS_BASE - 0x20000) / 0x10000, name: "BA_VATOS")
                    .WithReservedBits(10, 22);
                ;
            }

            Registers.SMMU_IDR3.Define(this)
                .WithReservedBits(0, 2)
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "HAD")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "PBHA")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "XNX")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => false, name: "PPS")
                .WithReservedBits(6, 1)
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => mpamSupported, name: "MPAM")
                .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => false, name: "FWB")
                .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => false, name: "STT")
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => false, name: "RIL")
                .WithEnumField<DoubleWordRegister, BreakBeforeMakeLevel>(11, 2, FieldMode.Read, valueProviderCallback: _ => BreakBeforeMakeLevel.Level1, name: "BBML")
                .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => false, name: "E0PD")
                .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => false, name: "PTWNNC")
                .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => dptSupported, name: "DPT")
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => false, name: "PASIDTT")
                .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => false, name: "EPAN")
                .WithFlag(18, FieldMode.Read, valueProviderCallback: _ => false, name: "S1PI")
                .WithFlag(19, FieldMode.Read, valueProviderCallback: _ => s2piSupported, name: "S2PI")
                .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => false, name: "S2PO")
                .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => false, name: "THE")
                .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => false, name: "MTEPERM")
                .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => false, name: "AIE")
                .WithReservedBits(24, 8)
            ;

            Registers.SMMU_IDR4.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0x6f6e6572, name: "IMPLEMENTATION_DEFINED")
            ;

            Registers.SMMU_IDR5.Define(this)
                .WithEnumField<DoubleWordRegister, AddressSize>(0, 3, FieldMode.Read, valueProviderCallback: _ => AddressSize.Bits48, name: "OAS")
                .WithReservedBits(3, 1)
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => true, name: "GRAN4K")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => false, name: "GRAN16K")
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => false, name: "GRAN64K")
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => false, name: "DS")
                .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => false, name: "D128")
                .WithReservedBits(9, 1)
                .WithEnumField<DoubleWordRegister, VirtualAddressExtend>(10, 2, FieldMode.Read, valueProviderCallback: _ => VirtualAddressExtend.Bits48, name: "VAX")
                .WithReservedBits(12, 4)
                .WithValueField(16, 16, valueProviderCallback: _ => 0, name: "STALL_MAX")
            ;

            if(ecmdqSupported)
            {
                Registers.SMMU_IDR6.Define(this)
                    .WithReservedBits(0, 16)
                    .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => 4, name: "CMDQ_CONTROL_PAGE_LOG2NUMQ")
                    .WithReservedBits(20, 4)
                    .WithValueField(24, 4, FieldMode.Read, valueProviderCallback: _ => 1, name: "CMDQ_CONTROL_PAGE_LOG2NUMP")
                    .WithReservedBits(28, 4)
                ;
            }

            Registers.SMMU_IIDR.Define(this)
                .WithValueField(0, 12, FieldMode.Read, valueProviderCallback: _ => 0x43B, name: "Implementer")
                .WithValueField(12, 4, FieldMode.Read, name: "Revision")
                .WithValueField(16, 4, FieldMode.Read, name: "Variant")
                .WithValueField(20, 12, FieldMode.Read, name: "ProductID")
            ;

            Registers.SMMU_AIDR.Define(this)
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => 2, name: "ArchMinorRev") // SMMUv3.2
                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => 3, name: "ArchMajorRev") // SMMUv3
                .WithReservedBits(8, 24)
            ;

            Registers.SMMU_CR0.Define(this)
                .WithFlag(0, out smmuEnable, name: "SMMUEN")
                .WithFlag(1, out pageRequestQueueEnable, name: "PRIQEN")
                .WithFlag(2, out eventQueueEnable, name: "EVENTQEN")
                .WithFlag(3, out commandQueueEnable, name: "CMDQEN")
                .WithFlag(4, out atsCheckEnable, name: "ATSCHK")
                .WithReservedBits(5, 1)
                .WithTag("VMW", 6, 3)
                .WithReservedBits(9, 1)
                .WithTaggedFlag("DPT_WALK_EN", 10)
                .WithReservedBits(11, 21)
            ;

            Registers.SMMU_CR0ACK.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => smmuEnable.Value, name: "SMMUEN_ACK")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => pageRequestQueueEnable.Value, name: "PRIQEN_ACK")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => eventQueueEnable.Value, name: "EVENTQEN_ACK")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => commandQueueEnable.Value, name: "CMDQEN_ACK")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => atsCheckEnable.Value, name: "ATSCHK_ACK")
            ;

            Registers.SMMU_CR1.Define(this)
                .WithTag("QUEUE_IC", 0, 2)
                .WithTag("QUEUE_OC", 2, 2)
                .WithTag("QUEUE_SH", 4, 2)
                .WithTag("TABLE_IC", 6, 2)
                .WithTag("TABLE_OC", 8, 2)
                .WithTag("TABLE_SH", 10, 2)
                .WithReservedBits(12, 20)
            ;

            Registers.SMMU_CR2.Define(this)
                .WithTaggedFlag("E2H", 0)
                .WithTaggedFlag("RECINVSID", 1)
                .WithTaggedFlag("PTM", 2)
                .WithTaggedFlag("REC_CFG_ATS", 3)
                .WithReservedBits(4, 28)
            ;

            if(s2piSupported)
            {
                QuadWordRegisters.DefineRegister((long)Registers.SMMU_S2PII)
                    .WithTags("S2PII", 0, 16, 4)
                ;
            }

            Registers.SMMU_STATUSR.Define(this)
                .WithTaggedFlag("DORMANT", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.SMMU_GBPA.Define(this)
                .WithTag("MemAttr", 0, 4)
                .WithTaggedFlag("MTCFG", 4)
                .WithReservedBits(5, 3)
                .WithTag("ALLOCCFG", 8, 4)
                .WithTag("SHCFG", 12, 2)
                .WithReservedBits(14, 2)
                .WithTag("PRIVCFG", 16, 2)
                .WithTag("INSTCFG", 18, 2)
                .WithTaggedFlag("ABORT", 20)
                .WithReservedBits(21, 10)
                .WithFlag(31, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            this.WarningLog("SMMU_GBPA update requested");
                        }
                    }, name: "Update")
            ;

            Registers.SMMU_AGBPA.Define(this)
                .WithValueField(0, 32, name: "IMPLEMENTATION_DEFINED");

            Registers.SMMU_IRQ_CTRL.Define(this)
                .WithFlag(0, out globalErrorInterruptEnable, name: "GERROR_IRQEN")
                .WithFlag(1, out pageRequestQueueErrorInterruptEnable, name: "PRIQ_IRQEN")
                .WithFlag(2, out eventQueueInterruptEnable, name: "EVENTQ_IRQEN")
                .WithReservedBits(3, 29)
            ;

            Registers.SMMU_IRQ_CTRLACK.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => globalErrorInterruptEnable.Value, name: "GERROR_IRQEN_ACK")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => pageRequestQueueErrorInterruptEnable.Value, name: "PRIQ_IRQEN_ACK")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => eventQueueInterruptEnable.Value, name: "EVENTQ_IRQEN_ACK")
                .WithReservedBits(3, 29)
            ;

            Registers.SMMU_GERROR.Define(this)
                .WithFlag(0, out commandQueueErrorPresent, name: "CMDQ_ERR")
                .WithReservedBits(1, 1)
                .WithTaggedFlag("EVENTQ_ABT_ERR", 2)
                .WithTaggedFlag("PRIQ_ABT_ERR", 3)
                .WithTaggedFlag("MSI_CMDQ_ABT_ERR", 4)
                .WithTaggedFlag("MSI_EVENTQ_ABT_ERR", 5)
                .WithTaggedFlag("MSI_PRIQ_ABT_ERR", 6)
                .WithTaggedFlag("MSI_GERROR_ABT_ERR", 7)
                .WithTaggedFlag("SFM_ERR", 8)
                .WithTaggedFlag("CMDQP_ERR", 9)
                .WithTaggedFlag("DPT_ERR", 10)
                .WithReservedBits(11, 21)
            ;

            Registers.SMMU_GERRORN.Define(this)
                .WithFlag(0, name: "CMDQ_ERR_N")
                .WithReservedBits(1, 1)
                .WithTaggedFlag("EVENTQ_ABT_ERR_N", 2)
                .WithTaggedFlag("PRIQ_ABT_ERR_N", 3)
                .WithTaggedFlag("MSI_CMDQ_ABT_ERR_N", 4)
                .WithTaggedFlag("MSI_EVENTQ_ABT_ERR_N", 5)
                .WithTaggedFlag("MSI_PRIQ_ABT_ERR_N", 6)
                .WithTaggedFlag("MSI_GERROR_ABT_ERR_N", 7)
                .WithTaggedFlag("SFM_ERR_N", 8)
                .WithTaggedFlag("CMDQP_ERR_N", 9)
                .WithTaggedFlag("DPT_ERR_N", 10)
                .WithReservedBits(11, 21)
            ;

            if(msiSupported)
            {
                QuadWordRegisters.DefineRegister((long)Registers.SMMU_GERROR_IRQ_CFG0)
                    .WithReservedBits(0, 2)
                    .WithTag("ADDR", 2, 54)
                    .WithReservedBits(56, 8)
                ;

                Registers.SMMU_GERROR_IRQ_CFG1.Define(this)
                    .WithTag("DATA", 0, 32)
                ;

                Registers.SMMU_GERROR_IRQ_CFG2.Define(this)
                    .WithTag("MemAttr", 0, 4)
                    .WithTag("SH", 4, 2)
                    .WithReservedBits(6, 26)
                ;

                QuadWordRegisters.DefineRegister((long)Registers.SMMU_EVENTQ_IRQ_CFG0)
                    .WithReservedBits(0, 2)
                    .WithTag("ADDR", 2, 54)
                    .WithReservedBits(56, 8)
                ;

                Registers.SMMU_EVENTQ_IRQ_CFG1.Define(this)
                    .WithTag("DATA", 0, 32)
                ;

                Registers.SMMU_EVENTQ_IRQ_CFG2.Define(this)
                    .WithTag("MemAttr", 0, 4)
                    .WithTag("SH", 4, 2)
                    .WithReservedBits(6, 26)
                ;
            }

            QuadWordRegisters.DefineRegister((long)Registers.SMMU_STRTAB_BASE)
                .WithValueField(6, 50, out streamTableAddress, name: "ADDR")
                .WithTaggedFlag("RA", 62)
            ;

            Registers.SMMU_STRTAB_BASE_CFG.Define(this)
                .WithValueField(0, 6, out streamTableShift, name: "LOG2SIZE")
                .WithTag("SPLIT", 6, 5)
                .WithReservedBits(11, 5)
                .WithEnumField(16, 2, out streamTableFormat, changeCallback: (_, val) =>
                    {
                        if(val == StreamTableLevel.TwoLevel)
                        {
                            this.ErrorLog("Two-level stream table is not supported yet");
                        }
                    }, name: "FMT")
                .WithReservedBits(18, 14)
            ;

            QuadWordRegisters.DefineRegister((long)Registers.SMMU_CMDQ_BASE)
                .WithValueField(0, 5, out commandQueueShift, name: "LOG2SIZE", changeCallback: (_, val) =>
                    {
                        if(val > MaxCommandQueueShift)
                        {
                            commandQueueShift.Value = MaxCommandQueueShift;
                        }
                    })
                .WithValueField(5, 51, out commandQueueAddress, name: "ADDR")
                .WithReservedBits(56, 6)
                .WithTaggedFlag("RA", 62)
                .WithReservedBits(63, 1)
            ;

            Registers.SMMU_CMDQ_PROD.Define(this)
                .WithValueField(0, 20, out commandQueueProduce, changeCallback: (_, __) =>
                    {
                        if(!commandQueueEnable.Value)
                        {
                            this.WarningLog("Command queue is disabled, ignoring PROD update");
                            return;
                        }
                        ProcessCommandQueue();
                    }, name: "WR")
                .WithReservedBits(20, 12)
            ;

            Registers.SMMU_CMDQ_CONS.Define(this)
                .WithValueField(0, 20, out commandQueueConsume, mode: FieldMode.Read, name: "RD")
                .WithReservedBits(20, 4)
                .WithValueField(24, 7, out commandQueueErrorReason, mode: FieldMode.Read, name: "ERR")
                .WithReservedBits(31, 1)
            ;

            QuadWordRegisters.DefineRegister((long)Registers.SMMU_EVENTQ_BASE)
                .WithTag("LOG2SIZE", 0, 5)
                .WithReservedBits(56, 6)
                .WithTag("ADDR", 5, 51)
                .WithTaggedFlag("WA", 62)
                .WithReservedBits(63, 1)
            ;

            if(priSupported)
            {
                QuadWordRegisters.DefineRegister((long)Registers.SMMU_PRIQ_BASE)
                    .WithTag("LOG2SIZE", 0, 5)
                    .WithTag("ADDR", 5, 51)
                    .WithReservedBits(56, 6)
                    .WithTaggedFlag("WA", 62)
                    .WithReservedBits(63, 1)
                ;
            }

            if(priSupported && msiSupported)
            {
                QuadWordRegisters.DefineRegister((long)Registers.SMMU_PRIQ_IRQ_CFG0)
                    .WithReservedBits(0, 2)
                    .WithTag("ADDR", 2, 54)
                    .WithReservedBits(56, 8)
                ;

                Registers.SMMU_PRIQ_IRQ_CFG1.Define(this)
                    .WithTag("DATA", 0, 32)
                ;

                Registers.SMMU_PRIQ_IRQ_CFG2.Define(this)
                    .WithTag("MemAttr", 0, 4)
                    .WithTag("SH", 4, 2)
                    .WithReservedBits(6, 25)
                    .WithTaggedFlag("LO", 31)
                ;
            }

            Registers.SMMU_GATOS_CTRL.Define(this)
                .WithTaggedFlag("RUN", 0)
                .WithReservedBits(1, 31)
            ;

            QuadWordRegisters.DefineRegister((long)Registers.SMMU_GATOS_SID)
                .WithTag("STREAMID", 0, 32)
                .WithTag("SUBSTREAMID", 32, 20)
                .WithTaggedFlag("SSID_VALID", 52)
            ;

            QuadWordRegisters.DefineRegister((long)Registers.SMMU_GATOS_ADDR)
                .WithTaggedFlag("HTTUI", 6)
                .WithTaggedFlag("InD", 7)
                .WithTaggedFlag("RnW", 8)
                .WithTaggedFlag("PnU", 9)
                .WithEnumField<QuadWordRegister, AddressTranslationType>(10, 2, name: "TYPE")
                .WithTag("ADDR", 12, 52)
            ;

            QuadWordRegisters.DefineRegister((long)Registers.SMMU_GATOS_PAR)
                .WithTaggedFlag("FAULT", 0) // TODO: If FAULT is set, then the layout of the following fields is different
                .WithTag("SH", 8, 2)
                .WithTaggedFlag("Size", 11)
                .WithTag("ADDR", 12, 44)
                .WithTag("ATTR", 56, 8)
            ;

            if(mpamSupported)
            {
                Registers.SMMU_MPAMIDR.Define(this)
                    .WithTag("PARTID_MAX", 0, 16)
                    .WithTag("PMG_MAX", 16, 8)
                    .WithReservedBits(24, 8)
                ;

                Registers.SMMU_GMPAM.Define(this)
                    .WithTag("SO_PARTID", 0, 16)
                    .WithTag("SO_PMG", 16, 8)
                    .WithReservedBits(24, 7)
                    .WithTaggedFlag("Update", 31)
                ;

                Registers.SMMU_GBPMPAM.Define(this)
                    .WithTag("GBP_PARTID", 0, 16)
                    .WithTag("GBP_PMG", 16, 8)
                    .WithReservedBits(24, 7)
                    .WithTaggedFlag("Update", 31)
                ;
            }

            if(vatosSupported)
            {
                Registers.SMMU_VATOS_SEL.Define(this)
                    .WithTag("VMID", 0, 16)
                    .WithReservedBits(16, 16)
                ;
            }

            if(dptSupported)
            {
                QuadWordRegisters.DefineRegister((long)Registers.SMMU_DPT_BASE)
                    .WithReservedBits(0, 12)
                    .WithTag("BADDR", 12, 44)
                    .WithReservedBits(56, 6)
                    .WithTaggedFlag("RA", 62)
                    .WithReservedBits(63, 1)
                ;

                Registers.SMMU_DPT_BASE_CFG.Define(this)
                    .WithTag("DPTPS", 0, 3)
                    .WithEnumField<DoubleWordRegister, Stage2TranslationGranule>(14, 2, name: "DPTGS")
                    .WithTag("L0DPTSZ", 20, 4)
                ;

                QuadWordRegisters.DefineRegister((long)Registers.SMMU_DPT_CFG_FAR)
                    .WithTaggedFlag("FAULT", 0)
                    .WithTaggedFlag("LEVEL", 1)
                    .WithTag("DPT_FAULTCODE", 4, 4)
                    .WithTag("FADDR", 12, 44)
                ;
            }

            Registers.SMMU_CIDR0.Define(this, 0x0d);
            Registers.SMMU_CIDR1.Define(this, 0x90);
            Registers.SMMU_CIDR2.Define(this, 0x05);
            Registers.SMMU_CIDR3.Define(this, 0xb1);
            Registers.SMMU_PIDR0.Define(this, 0x83); // PART_0
            Registers.SMMU_PIDR1.Define(this, 0xb4); // DES_0=b, PART_1=4
            Registers.SMMU_PIDR2.Define(this, 0x2f); // REV=2, JEDEC=1, DES_1=3
            Registers.SMMU_PIDR3.Define(this, 0x00); // REVAND=2, CMOD=0
            Registers.SMMU_PIDR4.Define(this, 0x04); // SIZE=0, DES_2=4
            Registers.SMMU_PIDR5.Define(this, 0); // RES0
            Registers.SMMU_PIDR6.Define(this, 0); // RES0
            Registers.SMMU_PIDR7.Define(this, 0); // RES0
            Registers.SMMU_PMDEVARCH.Define(this, 0x47702a56);
            Registers.SMMU_PMDEVTYPE.Define(this, 0x56);

            Registers.SMMU_CMDQ_CONTROL_PAGE_CFG.DefineMany(this, 256, stepInBytes: 32, setup: (r, n) => r
                .WithTaggedFlag($"EN_{n}", 0)
                .WithReservedBits(1, 31))
            ;

            Registers.SMMU_CMDQ_CONTROL_PAGE_STATUS.DefineMany(this, 256, stepInBytes: 32, setup: (r, n) => r
                .WithTaggedFlag($"ENACK_{n}", 0)
                .WithReservedBits(1, 31))
            ;

            Registers.SMMU_EVENTQ_PROD.Define(this)
                .WithTag("WR", 0, 20)
                .WithReservedBits(20, 11)
                .WithTaggedFlag("OVFLG", 31)
            ;

            Registers.SMMU_EVENTQ_CONS.Define(this)
                .WithTag("RD", 0, 20)
                .WithReservedBits(20, 11)
                .WithTaggedFlag("OVACKFLG", 31)
            ;

            if(priSupported)
            {
                Registers.SMMU_PRIQ_PROD.Define(this)
                    .WithTag("WR", 0, 20)
                    .WithReservedBits(20, 11)
                    .WithTaggedFlag("OVFLG", 31)
                ;

                Registers.SMMU_PRIQ_CONS.Define(this)
                    .WithTag("RD", 0, 20)
                    .WithReservedBits(20, 11)
                    .WithTaggedFlag("OVACKFLG", 31)
                ;
            }

            Registers.SMMU_VATOS_CTRL.Define(this)
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, changeCallback: (_, val) =>
                    {
                        this.ErrorLog("VATOS translation requested, not implemented");
                    })
                .WithReservedBits(1, 31)
            ;

            QuadWordRegisters.DefineRegister((long)Registers.SMMU_VATOS_SID)
                .WithValueField(0, 32, name: "STREAMID")
                .WithValueField(32, 20, name: "SUBSTREAMID")
                .WithFlag(52, name: "SSID_VALID")
                .WithReservedBits(53, 11)
            ;

            QuadWordRegisters.DefineRegister((long)Registers.SMMU_VATOS_ADDR)
                .WithReservedBits(0, 6)
                .WithFlag(6, name: "HTTUI")
                .WithFlag(7, name: "InD")
                .WithFlag(8, name: "RnW")
                .WithFlag(9, name: "PnU")
                .WithEnumField<QuadWordRegister, AddressTranslationType>(10, 2, name: "TYPE")
                .WithValueField(12, 52, name: "ADDR")
            ;

            Registers.SMMU_EVENTQ_PROD_Alias.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => this.ReadDoubleWord((long)Registers.SMMU_EVENTQ_PROD))
            ;

            Registers.SMMU_EVENTQ_CONS_Alias.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => this.ReadDoubleWord((long)Registers.SMMU_EVENTQ_CONS))
            ;

            Registers.SMMU_PRIQ_PROD_Alias.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => this.ReadDoubleWord((long)Registers.SMMU_PRIQ_PROD))
            ;

            Registers.SMMU_PRIQ_CONS_Alias.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => this.ReadDoubleWord((long)Registers.SMMU_PRIQ_CONS))
            ;
        }

        private void ProcessCommandQueue()
        {
            while(commandQueue.TryPeek(out var command))
            {
                try
                {
                    this.DebugLog("Executing command {0} {1}", command.Opcode, command);
                    command.Run(this);
                    // If the command fails, SMMU_(*_)CMDQ_CONS.RD remains pointing to the erroneous command in the Command queue
                    commandQueue.AdvanceConsumerIndex();
                }
                catch(CommandException e)
                {
                    this.WarningLog("Command {0} failed with {1}: {2}", command.Opcode, e.Reason, e.Message);
                    commandQueueErrorReason.Value = e.Reason;
                    commandQueueErrorPresent.Value = !commandQueueErrorPresent.Value;
                    break;
                }
            }
        }

        private void InvalidateSte(uint streamId)
        {
            // TODO: Errors, leaf, multilevel stream table
            if(streamId >= StreamTableSize)
            {
                this.WarningLog("Attempt to invalidate STE {0} which is out of range (table size: {1})", streamId, StreamTableSize);
                return;
            }
            var ste = ReadStruct<StreamTableEntry>(StreamTableAddress, streamId);
            this.NoisyLog("Invalidated STE {0} = {1}", streamId, ste);
            streamTable[streamId] = ste;
        }

        private void InvalidateTlb(ulong? virtualAddress = null)
        {
            foreach(var controller in streamControllers.Values)
            {
                controller.InvalidateTlb(virtualAddress);
            }
        }

        private int StreamTableSize => 1 << Math.Min((int)streamTableShift.Value, StreamIdBits);

        private ulong StreamTableAddress => streamTableAddress.Value << 6;

        private IFlagRegisterField smmuEnable;
        private IFlagRegisterField pageRequestQueueEnable;
        private IFlagRegisterField eventQueueEnable;
        private IFlagRegisterField commandQueueEnable;
        private IFlagRegisterField atsCheckEnable;
        private IFlagRegisterField commandQueueErrorPresent;
        private IFlagRegisterField globalErrorInterruptEnable;
        private IFlagRegisterField pageRequestQueueErrorInterruptEnable;
        private IFlagRegisterField eventQueueInterruptEnable;
        private IValueRegisterField commandQueueShift;
        private IValueRegisterField commandQueueAddress;
        private IValueRegisterField commandQueueConsume;
        private IValueRegisterField commandQueueProduce;
        private IValueRegisterField commandQueueErrorReason;
        private IValueRegisterField streamTableShift;
        private IValueRegisterField streamTableAddress;
        private IEnumRegisterField<StreamTableLevel> streamTableFormat;

        private readonly WrappingQueue<Command> commandQueue;
        private readonly StreamTableEntry[] streamTable = new StreamTableEntry[1 << StreamIdBits];
        private readonly TwoWayDictionary<int, IPeripheral> streams = new TwoWayDictionary<int, IPeripheral>();
        private readonly Dictionary<int, ISMMUv3StreamController> streamControllers = new Dictionary<int, ISMMUv3StreamController>(); // TODO: Index by (ASID, VMID, StreamWorld)

        private readonly IBusController sysbus;

        private const int MaxPageTableLevel = 3;
        private const int MaxCommandQueueShift = 7; // 128 bytes
        private const int StreamIdBits = 8;

        public class CommandException : Exception
        {
            public CommandException(uint reason, string message = null, Exception innerException = null) : base(message, innerException)
            {
                Reason = reason;
            }

            public uint Reason { get; }
        }

        public enum Registers
        {
            // Page 0
            // Non-secure registers
            SMMU_IDR0 = 0x0,
            SMMU_IDR1 = 0x4,
            SMMU_IDR2 = 0x8,
            SMMU_IDR3 = 0xC,
            SMMU_IDR4 = 0x10,
            SMMU_IDR5 = 0x14,
            SMMU_IIDR = 0x18,
            SMMU_AIDR = 0x1C,
            SMMU_CR0 = 0x20,
            SMMU_CR0ACK = 0x24,
            SMMU_CR1 = 0x28,
            SMMU_CR2 = 0x2C,
            SMMU_S2PII = 0x30,
            SMMU_STATUSR = 0x40,
            SMMU_GBPA = 0x44,
            SMMU_AGBPA = 0x48,
            SMMU_IRQ_CTRL = 0x50,
            SMMU_IRQ_CTRLACK = 0x54,
            SMMU_GERROR = 0x60,
            SMMU_GERRORN = 0x64,
            SMMU_GERROR_IRQ_CFG0 = 0x68,
            SMMU_GERROR_IRQ_CFG1 = 0x70,
            SMMU_GERROR_IRQ_CFG2 = 0x74,
            SMMU_STRTAB_BASE = 0x80,
            SMMU_STRTAB_BASE_CFG = 0x88,
            SMMU_CMDQ_BASE = 0x90,
            SMMU_CMDQ_PROD = 0x98,
            SMMU_CMDQ_CONS = 0x9C,
            SMMU_EVENTQ_BASE = 0xA0,
            SMMU_EVENTQ_PROD_Alias = 0xA8,
            SMMU_EVENTQ_CONS_Alias = 0xAC,
            SMMU_EVENTQ_IRQ_CFG0 = 0xB0,
            SMMU_EVENTQ_IRQ_CFG1 = 0xB8,
            SMMU_EVENTQ_IRQ_CFG2 = 0xBC,
            SMMU_PRIQ_BASE = 0xC0,
            SMMU_PRIQ_PROD_Alias = 0xC8,
            SMMU_PRIQ_CONS_Alias = 0xCC,
            SMMU_PRIQ_IRQ_CFG0 = 0xD0,
            SMMU_PRIQ_IRQ_CFG1 = 0xD8,
            SMMU_PRIQ_IRQ_CFG2 = 0xDC,
            SMMU_GATOS_CTRL = 0x100,
            SMMU_GATOS_SID = 0x108,
            SMMU_GATOS_ADDR = 0x110,
            SMMU_GATOS_PAR = 0x118,
            SMMU_MPAMIDR = 0x130,
            SMMU_GMPAM = 0x138,
            SMMU_GBPMPAM = 0x13C,
            SMMU_VATOS_SEL = 0x180,
            SMMU_IDR6 = 0x190,
            SMMU_DPT_BASE = 0x200,
            SMMU_DPT_BASE_CFG = 0x208,
            SMMU_DPT_CFG_FAR = 0x210,
            // Peripheral identification registers
            SMMU_PMDEVARCH = 0xFBC,
            SMMU_PMDEVTYPE = 0xFCC,
            SMMU_PIDR4 = 0xFD0,
            SMMU_PIDR5 = 0xFD4,
            SMMU_PIDR6 = 0xFD8,
            SMMU_PIDR7 = 0xFDC,
            SMMU_PIDR0 = 0xFE0,
            SMMU_PIDR1 = 0xFE4,
            SMMU_PIDR2 = 0xFE8,
            SMMU_PIDR3 = 0xFEC,
            SMMU_CIDR0 = 0xFF0,
            SMMU_CIDR1 = 0xFF4,
            SMMU_CIDR2 = 0xFF8,
            SMMU_CIDR3 = 0xFFC,
            SMMU_CMDQ_CONTROL_PAGE_BASE = 0x4000,
            SMMU_CMDQ_CONTROL_PAGE_CFG = 0x4008,
            SMMU_CMDQ_CONTROL_PAGE_STATUS = 0x400C,
            // Secure registers
            SMMU_SECURE_BASE = 0x8000,
            SMMU_S_IDR0 = SMMU_SECURE_BASE | SMMU_IDR0,
            SMMU_S_IDR1 = SMMU_SECURE_BASE | SMMU_IDR1,
            SMMU_S_IDR2 = SMMU_SECURE_BASE | SMMU_IDR2,
            SMMU_S_IDR3 = SMMU_SECURE_BASE | SMMU_IDR3,
            SMMU_S_IDR4 = SMMU_SECURE_BASE | SMMU_IDR4,
            SMMU_S_CR0 = SMMU_SECURE_BASE | SMMU_CR0,
            SMMU_S_CR0ACK = SMMU_SECURE_BASE | SMMU_CR0ACK,
            SMMU_S_CR1 = SMMU_SECURE_BASE | SMMU_CR1,
            SMMU_S_CR2 = SMMU_SECURE_BASE | SMMU_CR2,
            SMMU_S_S2PII = SMMU_SECURE_BASE | SMMU_S2PII,
            SMMU_S_INIT = SMMU_SECURE_BASE | 0x3C,
            SMMU_S_GBPA = SMMU_SECURE_BASE | SMMU_GBPA,
            SMMU_S_AGBPA = SMMU_SECURE_BASE | SMMU_AGBPA,
            SMMU_S_IRQ_CTRL = SMMU_SECURE_BASE | SMMU_IRQ_CTRL,
            SMMU_S_IRQ_CTRLACK = SMMU_SECURE_BASE | SMMU_IRQ_CTRLACK,
            SMMU_S_GERROR = SMMU_SECURE_BASE | SMMU_GERROR,
            SMMU_S_GERRORN = SMMU_SECURE_BASE | SMMU_GERRORN,
            SMMU_S_GERROR_IRQ_CFG0 = SMMU_SECURE_BASE | SMMU_GERROR_IRQ_CFG0,
            SMMU_S_GERROR_IRQ_CFG1 = SMMU_SECURE_BASE | SMMU_GERROR_IRQ_CFG1,
            SMMU_S_GERROR_IRQ_CFG2 = SMMU_SECURE_BASE | SMMU_GERROR_IRQ_CFG2,
            SMMU_S_STRTAB_BASE = SMMU_SECURE_BASE | SMMU_STRTAB_BASE,
            SMMU_S_STRTAB_BASE_CFG = SMMU_SECURE_BASE | SMMU_STRTAB_BASE_CFG,
            SMMU_S_CMDQ_BASE = SMMU_SECURE_BASE | SMMU_CMDQ_BASE,
            SMMU_S_CMDQ_PROD = SMMU_SECURE_BASE | SMMU_CMDQ_PROD,
            SMMU_S_CMDQ_CONS = SMMU_SECURE_BASE | SMMU_CMDQ_CONS,
            SMMU_S_EVENTQ_BASE = SMMU_SECURE_BASE | SMMU_EVENTQ_BASE,
            SMMU_S_EVENTQ_PROD = SMMU_SECURE_BASE | SMMU_EVENTQ_PROD,
            SMMU_S_EVENTQ_CONS = SMMU_SECURE_BASE | SMMU_EVENTQ_CONS,
            SMMU_S_EVENTQ_IRQ_CFG0 = SMMU_SECURE_BASE | SMMU_EVENTQ_IRQ_CFG0,
            SMMU_S_EVENTQ_IRQ_CFG1 = SMMU_SECURE_BASE | SMMU_EVENTQ_IRQ_CFG1,
            SMMU_S_EVENTQ_IRQ_CFG2 = SMMU_SECURE_BASE | SMMU_EVENTQ_IRQ_CFG2,
            SMMU_S_GATOS_CTRL = SMMU_SECURE_BASE | SMMU_GATOS_CTRL,
            SMMU_S_GATOS_SID = SMMU_SECURE_BASE | SMMU_GATOS_SID,
            SMMU_S_GATOS_ADDR = SMMU_SECURE_BASE | SMMU_GATOS_ADDR,
            SMMU_S_GATOS_PAR = SMMU_SECURE_BASE | SMMU_GATOS_PAR,
            SMMU_S_MPAMIDR = SMMU_SECURE_BASE | SMMU_MPAMIDR,
            SMMU_S_GMPAM = SMMU_SECURE_BASE | SMMU_GMPAM,
            SMMU_S_GBPMPAM = SMMU_SECURE_BASE | SMMU_GBPMPAM,
            SMMU_S_VATOS_SEL = SMMU_SECURE_BASE | SMMU_VATOS_SEL,
            SMMU_S_IDR6 = SMMU_SECURE_BASE | SMMU_IDR6,
            SMMU_S_CMDQ_CONTROL_PAGE_BASE = SMMU_SECURE_BASE | SMMU_CMDQ_CONTROL_PAGE_BASE,
            SMMU_S_CMDQ_CONTROL_PAGE_CFG = SMMU_SECURE_BASE | SMMU_CMDQ_CONTROL_PAGE_CFG,
            SMMU_S_CMDQ_CONTROL_PAGE_STATUS = SMMU_SECURE_BASE | SMMU_CMDQ_CONTROL_PAGE_STATUS,
            // Page 1
            SMMU_EVENTQ_PROD = 0x100A8,
            SMMU_EVENTQ_CONS = 0x100AC,
            SMMU_PRIQ_PROD = 0x100C8,
            SMMU_PRIQ_CONS = 0x100CC,
            // VATOS page
            SMMU_VATOS_BASE = 0x20000,
            SMMU_VATOS_CTRL = 0x20A00,
            SMMU_VATOS_SID = 0x20A08,
            SMMU_VATOS_ADDR = 0x20A10,
            SMMU_VATOS_PAR = 0x20A18,
        }
    }
}