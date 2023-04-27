//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class ARM_GenericInterruptController : IBusPeripheral, ILocalGPIOReceiver, INumberedGPIOOutput, IIRQController
    {
        public ARM_GenericInterruptController(int numberOfCPUs = 1, ARM_GenericInterruptControllerVersion architectureVersion = DefaultArchitectureVersion,
            uint productIdentifier = DefaultProductIdentifier, byte cpuInterfaceRevision = DefaultRevisionNumber, uint cpuInterfaceImplementer = DefaultImplementerIdentification)
        {
            if(numberOfCPUs < 1)
            {
                throw new ConstructionException($"The numberOfCPUs can't be lower than 1, given {numberOfCPUs}.");
            }

            // This property is only used to properly identify a GIC
            // It doesn't change the behaviour or the map of registers
            this.architectureVersion = architectureVersion;
            this.productIdentifier = productIdentifier;
            cpuInterfaceRevisionNumber = cpuInterfaceRevision;
            cpuInterfaceImplementerIdentification = cpuInterfaceImplementer;

            var irqIds = InterruptId.GetRange(InterruptId.SharedPeripheralFirst, InterruptId.SharedPeripheralLast)
                .Concat(InterruptId.GetRange(InterruptId.ExtendedSharedPeripheralFirst, InterruptId.ExtendedSharedPeripheralLast));
            sharedInterrupts = new ReadOnlyDictionary<InterruptId, SharedInterrupt>(irqIds.ToDictionary(id => id, id => new SharedInterrupt(id)));

            var groupTypes = new[]
            {
                GroupType.Group0,
                GroupType.Group1NonSecure,
                GroupType.Group1Secure
            };
            groups = new ReadOnlyDictionary<GroupType, InterruptGroup>(groupTypes.ToDictionary(type => type, _ => new InterruptGroup()));

            cpuEntries = new ReadOnlyDictionary<int, CPUEntry>(Enumerable.Range(0, numberOfCPUs)
                .ToDictionary(id => id, id =>
                    {
                        var cpu = new CPUEntry(this, id, groups.Keys);
                        cpu.PrivateInterruptChanged += OnPrivateInterrupt;
                        return cpu;
                    }));
            Connections = new ReadOnlyDictionary<int, IGPIO>(cpuEntries.ToDictionary(x => x.Key, x => (IGPIO)x.Value.IRQ));

            distributorRegisters = new DoubleWordRegisterCollection(this, BuildDistributorRegistersMap());
            redistributorDoubleWordRegisters = new DoubleWordRegisterCollection(this, BuildRedistributorDoubleWordRegistersMap());
            redistributorQuadWordRegisters = new QuadWordRegisterCollection(this, BuildRedistributorQuadWordRegistersMap());
            cpuInterfaceRegisters = new DoubleWordRegisterCollection(this, BuildCPUInterfaceRegistersMap());
            cpuInterfaceSystemRegisters = new QuadWordRegisterCollection(this, BuildCPUInterfaceSystemRegistersMap());

            Reset();
        }

        public void Reset()
        {
            LockExecuteAndUpdate(() =>
                {
                    foreach(var irq in sharedInterrupts.Values)
                    {
                        irq.Reset();
                    }
                    foreach(var group in groups.Values)
                    {
                        group.Reset();
                    }
                    foreach(var cpu in cpuEntries.Values)
                    {
                        cpu.Reset();
                    }
                });

            distributorRegisters.Reset();
            redistributorDoubleWordRegisters.Reset();
            redistributorQuadWordRegisters.Reset();
            cpuInterfaceRegisters.Reset();
            cpuInterfaceSystemRegisters.Reset();
        }

        [ConnectionRegion("distributor")]
        public void WriteByteToDistributor(long offset, byte value)
        {
            LockExecuteAndUpdate(() =>
                LogWriteAccess(TryWriteByteToDoubleWordCollection(distributorRegisters, offset, value), value, "Distributor", offset, (DistributorRegisters)offset)
            );
        }

        [ConnectionRegion("distributor")]
        public byte ReadByteFromDistributor(long offset)
        {
            byte value = 0;
            LockExecuteAndUpdate(() =>
                LogReadAccess(TryReadByteFromDoubleWordCollection(distributorRegisters, offset, out value), value, "Distributor", offset, (DistributorRegisters)offset)
            );
            return value;
        }

        [ConnectionRegion("distributor")]
        public void WriteDoubleWordToDistributor(long offset, uint value)
        {
            LockExecuteAndUpdate(() =>
                LogWriteAccess(distributorRegisters.TryWrite(offset, value), value, "Distributor", offset, (DistributorRegisters)offset)
            );
        }

        [ConnectionRegion("distributor")]
        public uint ReadDoubleWordFromDistributor(long offset)
        {
            uint value = 0;
            LockExecuteAndUpdate(() =>
                LogReadAccess(distributorRegisters.TryRead(offset, out value), value, "Distributor", offset, (DistributorRegisters)offset)
            );
            return value;
        }

        [ConnectionRegion("redistributor")]
        public void WriteByteToRedistributor(long offset, byte value)
        {
            LockExecuteAndUpdate(() =>
                LogWriteAccess(TryWriteByteToDoubleWordCollection(redistributorDoubleWordRegisters, offset, value), value, "Redistributor", offset, (RedistributorRegisters)offset)
            );
        }

        [ConnectionRegion("redistributor")]
        public byte ReadByteFromRedistributor(long offset)
        {
            byte value = 0;
            LockExecuteAndUpdate(() =>
                LogReadAccess(TryReadByteFromDoubleWordCollection(redistributorDoubleWordRegisters, offset, out value), value, "Redistributor", offset, (RedistributorRegisters)offset)
            );
            return value;
        }

        [ConnectionRegion("redistributor")]
        public void WriteDoubleWordToRedistributor(long offset, uint value)
        {
            LockExecuteAndUpdate(() =>
                LogWriteAccess(redistributorDoubleWordRegisters.TryWrite(offset, value), value, "Redistributor", offset, (RedistributorRegisters)offset)
            );
        }

        [ConnectionRegion("redistributor")]
        public uint ReadDoubleWordFromRedistributor(long offset)
        {
            uint value = 0;
            LockExecuteAndUpdate(() =>
                LogReadAccess(redistributorDoubleWordRegisters.TryRead(offset, out value), value, "Redistributor", offset, (RedistributorRegisters)offset)
            );
            return value;
        }

        [ConnectionRegion("redistributor")]
        public void WriteQuadWordToRedistributor(long offset, ulong value)
        {
            LockExecuteAndUpdate(() =>
                LogWriteAccess(redistributorQuadWordRegisters.TryWrite(offset, value), value, "Redistributor", offset, (RedistributorRegisters)offset)
            );
        }

        [ConnectionRegion("redistributor")]
        public ulong ReadQuadWordFromRedistributor(long offset)
        {
            ulong value = 0;
            LockExecuteAndUpdate(() =>
                LogReadAccess(redistributorQuadWordRegisters.TryRead(offset, out value), value, "Redistributor", offset, (RedistributorRegisters)offset)
            );
            return value;
        }

        [ConnectionRegion("cpuInterface")]
        public void WriteDoubleWordToCPUInterface(long offset, uint value)
        {
            LockExecuteAndUpdate(() =>
                LogWriteAccess(cpuInterfaceRegisters.TryWrite(offset, value), value, "memory-mapped CPU Interface", offset, (CPUInterfaceRegisters)offset)
            );
        }

        [ConnectionRegion("cpuInterface")]
        public uint ReadDoubleWordFromCPUInterface(long offset)
        {
            uint value = 0;
            LockExecuteAndUpdate(() =>
                LogReadAccess(cpuInterfaceRegisters.TryRead(offset, out value), value, "memory-mapped CPU Interface", offset, (CPUInterfaceRegisters)offset)
            );
            return value;
        }

        public void WriteSystemRegisterCPUInterface(uint offset, ulong value)
        {
            LockExecuteAndUpdate(() =>
                LogWriteAccess(cpuInterfaceSystemRegisters.TryWrite(offset, value), value, "CPU Interface", offset, (CPUInterfaceSystemRegisters)offset)
            );
        }

        public ulong ReadSystemRegisterCPUInterface(uint offset)
        {
            ulong value = 0;
            LockExecuteAndUpdate(() =>
                LogReadAccess(cpuInterfaceSystemRegisters.TryRead(offset, out value), value, "CPU Interface", offset, (CPUInterfaceSystemRegisters)offset)
            );
            return value;
        }

        public void OnGPIO(int number, bool value)
        {
            var irqId = new InterruptId((uint)number + (uint)InterruptId.SharedPeripheralFirst);
            if(!irqId.IsSharedPeripheral)
            {
                this.Log(LogLevel.Warning, "Generated interrupt isn't a Shared Peripheral Interrupt, interrupt identifier: {0}", irqId);
                return;
            }
            this.Log(LogLevel.Debug, "Setting signal of the interrupt with id {0} to {1}.", irqId, value);
            LockExecuteAndUpdate(() =>
                sharedInterrupts[irqId].AssertAsPending(value)
            );
        }

        // Private Peripheral Interrupts are connected using the ILocalGPIOReceiver interface
        // Every CPUEntry class implements the IGPIOReceiver interface used to connect PPIs to each CPU 
        // The CPUEntry provides event for handling received interrupts by an external action
        // It's expected to handle all of these interrupts by OnPrivateInterrupt method 
        public IGPIOReceiver GetLocalReceiver(int cpuConnectionId)
        {
            return GetCPUByConnectionId(cpuConnectionId);
        }

        public IEnumerable<uint> GetEnabledInterruptIdentifiers(int cpuConnectionId)
        {
            var cpu = GetCPUByConnectionId(cpuConnectionId);
            lock(sharedInterrupts)
            {
                return GetAllEnabledInterupts(cpu).Select(irq => (uint)irq.Identifier);
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private void OnPrivateInterrupt(CPUEntry cpu, int id, bool value)
        {
            var irqId = new InterruptId((uint)id);
            if(!irqId.IsPrivatePeripheral)
            {
                this.Log(LogLevel.Warning, "Generated interrupt isn't a Private Peripheral Interrupt, interrupt identifier: {0}", irqId);
                return;
            }
            this.Log(LogLevel.Debug, "Setting signal of the interrupt with id {0} to {1} for {2}.", irqId, value, cpu.Name);
            LockExecuteAndUpdate(() =>
                cpu.Interrupts[irqId].AssertAsPending(value)
            );
        }

        private void LockExecuteAndUpdate(Action action)
        {
            lock(sharedInterrupts)
            {
                action();
                UpdateBestPendingInterrupts();
                foreach(var cpu in cpuEntries.Values)
                {
                    cpu.UpdateSignals();
                }
            }
        }

        private void UpdateBestPendingInterrupts()
        {
            foreach(var cpu in cpuEntries.Values)
            {
                var pendingCandidates = GetAllPendingCandidateInterupts(cpu);
                var bestPending = pendingCandidates.FirstOrDefault();
                foreach(var irq in pendingCandidates.Skip(1))
                {
                    if(irq.Priority < bestPending.Priority)
                    {
                        bestPending = irq;
                        if(bestPending.Priority == InterruptPriority.Highest)
                        {
                            break;
                        }
                    }
                }
                // Setting the bestPending to null indicates there is no pending interrupt.
                cpu.BestPending = bestPending;
            }
        }

        private IEnumerable<Interrupt> GetAllInterupts(CPUEntry cpu)
        {
            return cpu.Interrupts.Values.Concat(sharedInterrupts.Values);
        }

        private IEnumerable<Interrupt> GetAllEnabledInterupts(CPUEntry cpu)
        {
            var enabledGroups = groups.Keys.Where(type => groups[type].Enabled && cpu.Groups[type].Enabled).ToArray();
            return cpu.Interrupts.Values
                .Concat(sharedInterrupts.Values.Where(irq => irq.IsTargetingCPU(cpu)))
                .Where(irq => irq.Enabled && enabledGroups.Contains(irq.GroupType));
        }

        private IEnumerable<Interrupt> GetAllPendingCandidateInterupts(CPUEntry cpu)
        {
            return GetAllEnabledInterupts(cpu).Where(irq => irq.Pending && irq.Priority < cpu.PriorityMask && irq.Priority < cpu.RunningPriority);
        }

        private Dictionary<long, DoubleWordRegister> BuildDistributorRegistersMap()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)DistributorRegisters.Control, new DoubleWordRegister(this)
                    .WithFlag(31, FieldMode.Read, name: "RegisterWritePending",
                        valueProviderCallback: _ => false
                    )
                    .WithReservedBits(8, 23)
                    .WithTaggedFlag("Enable1OfNWakeup", 7)
                    .WithTaggedFlag("DisableSecurity", 6)
                    .WithTaggedFlag("EnableAffinityRoutingNonSecure", 5)
                    .WithTaggedFlag("EnableAffinityRoutingSecure", 4)
                    .WithReservedBits(3, 1)
                    .WithFlag(2, name: "EnableGroup1Secure",
                        writeCallback: (_, val) => groups[GroupType.Group1Secure].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group1Secure].Enabled
                    )
                    .WithFlag(1, name: "EnableGroup1NonSecure",
                        writeCallback: (_, val) => groups[GroupType.Group1NonSecure].Enabled  = val,
                        valueProviderCallback: _ => groups[GroupType.Group1NonSecure].Enabled
                    )
                    .WithFlag(0, name: "EnableGroup0",
                        writeCallback: (_, val) => groups[GroupType.Group0].Enabled  = val,
                        valueProviderCallback: _ => groups[GroupType.Group0].Enabled
                    )
                },
                {(long)DistributorRegisters.ControllerType, new DoubleWordRegister(this)
                    .WithValueField(27, 5, name: "SharedPeripheralInterruptsExtendedCount",
                        valueProviderCallback: _ => InterruptId.SharedPeripheralExtendedCount / 32 - 1
                    )
                    .WithFlag(26, name: "AffinityLevel0RangeSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(25, name: "1OfNSharedPeripheralInterruptsSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(24, name: "AffinityLevel3Support",
                        valueProviderCallback: _ => false
                    )
                    .WithValueField(19, 5, name: "SupportedInterruptIdentifierBits",
                        valueProviderCallback: _ => InterruptId.SupportedIdentifierBits - 1
                    )
                    .WithFlag(18, name: "DirectVirtualLocalitySpecificPeripheralInterruptInjectionSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(17, name: "LocalitySpecificPeripheralInterruptSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(16, name: "MessageBasedInterruptActivationByWriteSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithReservedBits(11, 5) // Indicates the lack of the Locality-specific Peripheral Interrupt support
                    .WithFlag(10, name: "SecurityStateSupport",
                        valueProviderCallback: _ => true // Indicate two security states support
                    )
                    .WithFlag(9, name: "NonMaskableInterruptSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(8, name: "SharedPeripheralInterruptsExtendedSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithValueField(5, 3, name: "CPUsCountWithoutAffinityRouting",
                        valueProviderCallback: _ => CPUsCountWithoutAffinityRouting - 1
                    )
                    .WithValueField(0, 5, name: "SharedPeripheralInterruptsCount",
                        valueProviderCallback: _ => ((uint)InterruptId.SharedPeripheralLast + 1) / 32 - 1
                    )
                }
            };

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptSetEnable_0,
                BuildInterruptSetEnableRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.SharedPeripheralLast, "InterruptSetEnable")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptClearEnable_0,
                BuildInterruptClearEnableRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.SharedPeripheralLast, "InterruptClearEnable")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptPriority_0,
                BuildInterruptPriorityRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.SharedPeripheralLast, "InterruptPriority")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptProcessorTargets_0,
                BuildPrivateInterruptTargetsRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.PrivatePeripheralLast, "InterruptProcessorTargets")
            );
            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptProcessorTargets_8,
                BuildSharedInterruptTargetsRegisters(InterruptId.SharedPeripheralFirst, InterruptId.SharedPeripheralLast, "InterruptProcessorTargets")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptConfiguration_0,
                BuildInterruptConfigurationRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.SoftwareGeneratedLast, "InterruptConfiguration", isReadonly: true)
            );
            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptConfiguration_1,
                BuildInterruptConfigurationRegisters(InterruptId.PrivatePeripheralFirst, InterruptId.SharedPeripheralLast, "InterruptConfiguration")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptSetActive_0,
                BuildInterruptSetActiveRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.SharedPeripheralLast, "InterruptSetActive")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptClearActive_0,
                BuildInterruptClearActiveRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.SharedPeripheralLast, "InterruptClearActive")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptClearPending_0,
                BuildInterruptClearPendingRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.SharedPeripheralLast, "InterruptClearPending")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptGroup_0,
                BuildInterruptGroupRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.SharedPeripheralLast, "InterruptGroup")
            );

            AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptGroupModifier_0,
                BuildInterruptGroupModifierRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.SharedPeripheralLast, "InterruptGroupModifier")
            );

            return registersMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildRedistributorDoubleWordRegistersMap()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)RedistributorRegisters.Control, new DoubleWordRegister(this)
                    .WithFlag(31, FieldMode.Read, name: "UpstreamWritePending",
                        valueProviderCallback: _ => false
                    )
                    .WithReservedBits(27, 4)
                    .WithTaggedFlag("DisableProcessorSelectionGroup1Secure", 26)
                    .WithTaggedFlag("DisableProcessorSelectionGroup1NonSecure", 25)
                    .WithTaggedFlag("DisableProcessorSelectionGroup0", 24)
                    .WithReservedBits(4, 20)
                    .WithFlag(3, FieldMode.Read, name: "RegisterWritePending",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(2, FieldMode.Read, name: "LocalitySpecificInterruptInvalidateSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(1, FieldMode.Read, name: "LocalitySpecificInterruptClearEnableSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithTaggedFlag("LocalitySpecificInterruptEnable", 0)
                },
                {(long)RedistributorRegisters.Wake, new DoubleWordRegister(this)
                    .WithTaggedFlag("WakeImplementationDefined", 31)
                    .WithReservedBits(3, 28)
                    .WithFlag(2, FieldMode.Read, name: "ChildrenAsleep",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(1, FieldMode.Read, name: "ProcessorSleep",
                        valueProviderCallback: _ => false
                    )
                    .WithTaggedFlag("WakeImplementationDefined", 0)
                }
            };

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptSetEnable_0,
                BuildInterruptSetEnableRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.PrivatePeripheralLast, "InterruptSetEnable")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptClearEnable_0,
                BuildInterruptClearEnableRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.PrivatePeripheralLast, "InterruptClearEnable")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptClearPending_0,
                BuildInterruptClearPendingRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.PrivatePeripheralLast, "InterruptClearPending")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptPriority_0,
                BuildInterruptPriorityRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.PrivatePeripheralLast, "InterruptPriority")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.PrivatePeripheralInterruptConfiguration,
                BuildInterruptConfigurationRegisters(InterruptId.PrivatePeripheralFirst, InterruptId.PrivatePeripheralLast, "PrivatePeripheralInterruptConfiguration")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptGroup_0,
                BuildInterruptGroupRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.PrivatePeripheralLast, "InterruptGroup")
            );

            AddRegistersAtOffset(registersMap, (long)RedistributorRegisters.InterruptGroupModifier_0,
                BuildInterruptGroupModifierRegisters(InterruptId.SoftwareGeneratedFirst, InterruptId.PrivatePeripheralLast, "InterruptGroupModifier")
            );

            return registersMap;
        }

        private Dictionary<long, QuadWordRegister> BuildRedistributorQuadWordRegistersMap()
        {
            var registersMap = new Dictionary<long, QuadWordRegister>
            {
                {(long)RedistributorRegisters.ControllerType, new QuadWordRegister(this)
                    .WithValueField(32, 32, FieldMode.Read, name: "CPUAffinity",
                        valueProviderCallback: _ => GetAskingCPU().Affinity.AllLevels
                    )
                    .WithValueField(27, 5, FieldMode.Read, name: "MaximumPrivatePeripheralInterruptIdentifier",
                        valueProviderCallback: _ => 0b10 // This value indicates the highest identifier equal to 1119
                    )
                    .WithFlag(26, FieldMode.Read, name: "DirectSoftwareGEenratedInterruptInjectionSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithTag("LocalitySpecificInterruptConfigurationSharing", 24, 2)
                    .WithValueField(8, 16, FieldMode.Read, name: "ProcessorNumber",
                        valueProviderCallback: _ => GetAskingCPU().Affinity.Level0
                    )
                    .WithTaggedFlag("vPEResidentIndicator", 7)
                    .WithFlag(6, FieldMode.Read, name: "MPAMSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(5, FieldMode.Read, name: "DPGSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithTaggedFlag("HighestRedistributorInSeries", 4)
                    .WithFlag(3, FieldMode.Read, name: "LocalitySpecificInterruptDirectInjectionSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithTaggedFlag("DirtyBitControl", 2)
                    .WithFlag(1, FieldMode.Read, name: "VirtualLocalitySpecificInterruptSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(0, FieldMode.Read, name: "PhysicalLocalitySpecificInterruptSupport",
                        valueProviderCallback: _ => false
                    )
                }
            };

            return registersMap;
        }

        private Dictionary<long, QuadWordRegister> BuildCPUInterfaceSystemRegistersMap()
        {
            var registersMap = new Dictionary<long, QuadWordRegister>
            {
                {(long)CPUInterfaceSystemRegisters.SystemRegisterEnableEL3, new QuadWordRegister(this)
                    .WithReservedBits(4, 60)
                    .WithFlag(3, FieldMode.Read, name: "EnableAccessOnLowerThanEL3", valueProviderCallback: _ => true)
                    .WithFlag(2, FieldMode.Read, name: "DisableIRQBypass", valueProviderCallback: _ => true)
                    .WithFlag(1, FieldMode.Read, name: "DisableFIQBypass", valueProviderCallback: _ => true)
                    .WithFlag(0, FieldMode.Read, name: "EnableSystemRegisterAccess", valueProviderCallback: _ => true)
                },
                {(long)CPUInterfaceSystemRegisters.SystemRegisterEnableEL2, new QuadWordRegister(this)
                    .WithReservedBits(4, 60)
                    .WithFlag(3, FieldMode.Read, name: "EnableAccessOnLowerThanEL2", valueProviderCallback: _ => true)
                    .WithFlag(2, FieldMode.Read, name: "DisableIRQBypass", valueProviderCallback: _ => true)
                    .WithFlag(1, FieldMode.Read, name: "DisableFIQBypass", valueProviderCallback: _ => true)
                    .WithFlag(0, FieldMode.Read, name: "EnableSystemRegisterAccess", valueProviderCallback: _ => true)
                },
                {(long)CPUInterfaceSystemRegisters.SystemRegisterEnableEL1, new QuadWordRegister(this)
                    .WithReservedBits(3, 61)
                    .WithFlag(2, FieldMode.Read, name: "DisableIRQBypass", valueProviderCallback: _ => true)
                    .WithFlag(1, FieldMode.Read, name: "DisableFIQBypass", valueProviderCallback: _ => true)
                    .WithFlag(0, FieldMode.Read, name: "EnableSystemRegisterAccess", valueProviderCallback: _ => true)
                },
                {(long)CPUInterfaceSystemRegisters.GroupEnable0, new QuadWordRegister(this)
                    .WithReservedBits(1, 63)
                    .WithFlag(0, name: "EnableGroup0",
                        valueProviderCallback: _ => GetAskingCPU().GetGroupForRegister(GroupTypeRegister.Group0).Enabled,
                        writeCallback: (_, val) => GetAskingCPU().GetGroupForRegister(GroupTypeRegister.Group0).Enabled = val
                    )
                },
                {(long)CPUInterfaceSystemRegisters.GroupEnable1, new QuadWordRegister(this)
                    .WithReservedBits(1, 63)
                    .WithFlag(0, name: "EnableGroup1",
                        valueProviderCallback: _ => GetAskingCPU().GetGroupForRegister(GroupTypeRegister.Group1).Enabled,
                        writeCallback: (_, val) => GetAskingCPU().GetGroupForRegister(GroupTypeRegister.Group1).Enabled = val
                    )
                },
                {(long)CPUInterfaceSystemRegisters.GroupEnable1EL3, new QuadWordRegister(this)
                    .WithReservedBits(2, 62)
                    .WithFlag(1, name: "EnableGroup1S",
                        valueProviderCallback: _ => GetAskingCPU().Groups[GroupType.Group1Secure].Enabled,
                        writeCallback: (_, val) => GetAskingCPU().Groups[GroupType.Group1Secure].Enabled = val
                    )
                    .WithFlag(0, name: "EnableGroup1NS",
                        valueProviderCallback: _ => GetAskingCPU().Groups[GroupType.Group1NonSecure].Enabled,
                        writeCallback: (_, val) => GetAskingCPU().Groups[GroupType.Group1NonSecure].Enabled = val
                    )
                },
                {(long)CPUInterfaceSystemRegisters.RunningPriority, new QuadWordRegister(this)
                    .WithTaggedFlag("PriorityFromNonMaskableInterrupt", 63) // Requires FEAT_GICv3_NMI extension
                    .WithTaggedFlag("PriorityFromNonSecureNonMaskableInterrupt", 62) // Requires FEAT_GICv3_NMI extension 
                    .WithReservedBits(8, 54)
                    .WithEnumField<QuadWordRegister, InterruptPriority>(0, 8, FieldMode.Read, name: "RunningPriority",
                        valueProviderCallback: _ => GetAskingCPU().RunningPriority
                    )
                },
                {(long)CPUInterfaceSystemRegisters.PriorityMask, new QuadWordRegister(this)
                    .WithReservedBits(8, 56)
                    .WithEnumField<QuadWordRegister, InterruptPriority>(0, 8, name: "PriorityMask",
                        writeCallback: (_, val) => GetAskingCPU().PriorityMask = val,
                        valueProviderCallback: _ => GetAskingCPU().PriorityMask
                    )
                },
                {(long)CPUInterfaceSystemRegisters.InterruptAcknowledgeGroup0, new QuadWordRegister(this)
                    .WithReservedBits(24, 40)
                    .WithValueField(0, 24, FieldMode.Read, name: "InterruptAcknowledgeGroup0",
                        valueProviderCallback: _ => (ulong)GetAskingCPU().AcknowledgeBestPending(GroupTypeRegister.Group0)
                    )
                },
                {(long)CPUInterfaceSystemRegisters.InterruptAcknowledgeGroup1, new QuadWordRegister(this)
                    .WithReservedBits(24, 40)
                    .WithValueField(0, 24, FieldMode.Read, name: "InterruptAcknowledgeGroup1",
                        valueProviderCallback: _ => (ulong)GetAskingCPU().AcknowledgeBestPending(GroupTypeRegister.Group1)
                    )
                },
                {(long)CPUInterfaceSystemRegisters.InterruptEndGroup0, new QuadWordRegister(this)
                    .WithReservedBits(24, 40)
                    .WithValueField(0, 24, FieldMode.Write, name: "InterruptEndGroup0",
                        writeCallback: (_, val) => GetAskingCPU().CompleteRunning(new InterruptId((uint)val), GroupTypeRegister.Group0)
                    )
                },
                {(long)CPUInterfaceSystemRegisters.InterruptEndGroup1, new QuadWordRegister(this)
                    .WithReservedBits(24, 40)
                    .WithValueField(0, 24, FieldMode.Write, name: "InterruptEndGroup1",
                        writeCallback: (_, val) => GetAskingCPU().CompleteRunning(new InterruptId((uint)val), GroupTypeRegister.Group1)
                    )
                }
            };

            return registersMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildCPUInterfaceRegistersMap()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)CPUInterfaceRegisters.Control, new DoubleWordRegister(this)
                    .WithReservedBits(10, 22)
                    .WithTaggedFlag("EndOfInterruptMode", 9)
                    .WithTaggedFlag("IRQBypassGroup1", 8)
                    .WithTaggedFlag("FIQBypassGroup1", 7)
                    .WithTaggedFlag("IRQBypassGroup0", 6)
                    .WithTaggedFlag("FIQBypassGroup0", 5)
                    .WithTaggedFlag("PremptionConrol", 4)
                    .WithTaggedFlag("Group0Signal", 3)
                    .WithReservedBits(2, 1)
                    .WithFlag(1, name: "EnableGroup1",
                        valueProviderCallback: _ => GetAskingCPU().GetGroupForRegister(GroupTypeRegister.Group1).Enabled,
                        writeCallback: (_, val) => GetAskingCPU().GetGroupForRegister(GroupTypeRegister.Group1).Enabled = val
                    )
                    .WithFlag(0, name: "EnableGroup0",
                        valueProviderCallback: _ => GetAskingCPU().GetGroupForRegister(GroupTypeRegister.Group0).Enabled,
                        writeCallback: (_, val) => GetAskingCPU().GetGroupForRegister(GroupTypeRegister.Group0).Enabled = val
                    )
                },
                {(long)CPUInterfaceRegisters.InterfaceIdentification, new DoubleWordRegister(this)
                    .WithValueField(20, 12, FieldMode.Read, valueProviderCallback: _ => productIdentifier, name: "ProductIdentifier")
                    .WithEnumField<DoubleWordRegister, ARM_GenericInterruptControllerVersion>(16, 4, FieldMode.Read, valueProviderCallback: _ => architectureVersion, name: "ArchitectureVersion")
                    .WithValueField(12, 4, FieldMode.Read, valueProviderCallback: _ => cpuInterfaceRevisionNumber, name: "RevisionNumber")
                    .WithValueField(0, 12, FieldMode.Read, valueProviderCallback: _ => cpuInterfaceImplementerIdentification, name: "ImplementerIdentification")
                },
                {(long)CPUInterfaceRegisters.RunningPriority, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithEnumField<DoubleWordRegister, InterruptPriority>(0, 8, FieldMode.Read, name: "RunningPriority",
                        valueProviderCallback: _ => GetAskingCPU().RunningPriority
                    )
                },
                {(long)CPUInterfaceRegisters.PriorityMask, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithEnumField<DoubleWordRegister, InterruptPriority>(0, 8, name: "PriorityMask",
                        writeCallback: (_, val) => GetAskingCPU().PriorityMask = val,
                        valueProviderCallback: _ => GetAskingCPU().PriorityMask
                    )
                },
                {(long)CPUInterfaceRegisters.InterruptAcknowledge, new DoubleWordRegister(this)
                    .WithReservedBits(24, 8)
                    .WithValueField(0, 24, FieldMode.Read, name: "InterruptAcknowledge",
                        valueProviderCallback: _ => (ulong)GetAskingCPU().AcknowledgeBestPending(GroupTypeRegister.Group0)
                    )
                },
                {(long)CPUInterfaceRegisters.InterruptEnd, new DoubleWordRegister(this)
                    .WithReservedBits(24, 8)
                    .WithValueField(0, 24, FieldMode.Write, name: "InterruptEnd",
                        writeCallback: (_, val) => GetAskingCPU().CompleteRunning(new InterruptId((uint)val), GroupTypeRegister.Group0)
                    )
                },
            };

            return registersMap;
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptSetEnableRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Enabled |= val,
                valueProviderCallback: irq => irq.Enabled
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptClearEnableRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Enabled &= !val,
                valueProviderCallback: irq => irq.Enabled
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptPriorityRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptEnumRegisters<InterruptPriority>(startId, endId, name, 4,
                writeCallback: (irq, val) => irq.Priority = val,
                valueProviderCallback: irq => irq.Priority
            );
        }

        private IEnumerable<DoubleWordRegister> BuildPrivateInterruptTargetsRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptValueRegisters(startId, endId, name, 4,
                valueProviderCallback: _ => GetAskingCPU().Affinity.TargetFieldFlag
            );
        }

        private IEnumerable<DoubleWordRegister> BuildSharedInterruptTargetsRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptValueRegisters(startId, endId, name, 4,
                writeCallback: (irq, val) => ((SharedInterrupt)irq).TargetCPU = (byte)val,
                valueProviderCallback: irq => ((SharedInterrupt)irq).TargetCPU
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptConfigurationRegisters(InterruptId startId, InterruptId endId, string name, bool isReadonly = false)
        {
            Action<Interrupt, InterruptTriggerType> writeCallback = null;
            if(!isReadonly)
            {
                writeCallback = (irq, val) =>
                {
                    irq.TriggerType = val;
                    if(val != InterruptTriggerType.LevelSensitive && val != InterruptTriggerType.EdgeTriggered)
                    {
                        this.Log(LogLevel.Error, "Setting an unknown interrupt trigger type, value {0}", val);
                    }
                };
            }
            return BuildInterruptEnumRegisters<InterruptTriggerType>(startId, endId, name, 16,
                writeCallback: writeCallback,
                valueProviderCallback: irq => irq.TriggerType
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptSetActiveRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Active |= val,
                valueProviderCallback: irq => irq.Active
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptClearActiveRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Active &= !val,
                valueProviderCallback: irq => irq.Active
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptClearPendingRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Pending &= !val,
                valueProviderCallback: irq => irq.Pending
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptGroupRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.GroupBit = val,
                valueProviderCallback: irq => irq.GroupBit
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptGroupModifierRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.GroupModifierBit = val,
                valueProviderCallback: irq => irq.GroupModifierBit
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptFlagRegisters(InterruptId startId, InterruptId endId, string name,
            Action<Interrupt, bool> writeCallback = null, Func<Interrupt, bool> valueProviderCallback = null)
        {
            const int BitsPerRegister = 32;
            return BuildInterruptRegisters(startId, endId, BitsPerRegister,
                (register, irqGetter, irqId, fieldIndex) =>
                    {
                        FieldMode fieldMode = 0;
                        Action<bool, bool> writeCallbackWrapped = null;
                        if(writeCallback != null)
                        {
                            fieldMode |= FieldMode.Write;
                            writeCallbackWrapped = (_, val) => writeCallback(irqGetter(), val);
                        }
                        Func<bool, bool> valueProviderCallbackWrapped = null;
                        if(valueProviderCallback != null)
                        {
                            fieldMode |= FieldMode.Read;
                            valueProviderCallbackWrapped = _ => valueProviderCallback(irqGetter());
                        }
                        register.WithFlag(fieldIndex, fieldMode, name: $"{name}_{(uint)irqId}",
                            writeCallback: writeCallbackWrapped, valueProviderCallback: valueProviderCallbackWrapped);
                    },

                (register, fieldIndex) => register.WithReservedBits(fieldIndex, 1)
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptValueRegisters(InterruptId startId, InterruptId endId, string name, int fieldsPerRegister,
            Action<Interrupt, ulong> writeCallback = null, Func<Interrupt, ulong> valueProviderCallback = null)
        {
            const int registerWidth = 32;
            var fieldWidth = registerWidth / fieldsPerRegister;
            return BuildInterruptRegisters(startId, endId, fieldsPerRegister,
                (register, irqGetter, irqId, fieldIndex) =>
                    {
                        FieldMode fieldMode = 0;
                        Action<ulong, ulong> writeCallbackWrapped = null;
                        if(writeCallback != null)
                        {
                            fieldMode |= FieldMode.Write;
                            writeCallbackWrapped = (_, val) => writeCallback(irqGetter(), val);
                        }
                        Func<ulong, ulong> valueProviderCallbackWrapped = null;
                        if(valueProviderCallback != null)
                        {
                            fieldMode |= FieldMode.Read;
                            valueProviderCallbackWrapped = _ => valueProviderCallback(irqGetter());
                        }
                        register.WithValueField(fieldIndex * fieldWidth, fieldWidth, fieldMode, name: $"{name}_{(uint)irqId}",
                            writeCallback: writeCallbackWrapped, valueProviderCallback: valueProviderCallbackWrapped);
                    },
                (register, fieldIndex) => register.WithReservedBits(fieldIndex * fieldWidth, fieldWidth)
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptEnumRegisters<TEnum>(InterruptId startId, InterruptId endId, string name, int fieldsPerRegister,
            Action<Interrupt, TEnum> writeCallback = null, Func<Interrupt, TEnum> valueProviderCallback = null) where TEnum : struct, IConvertible
        {
            const int registerWidth = 32;
            var fieldWidth = registerWidth / fieldsPerRegister;
            return BuildInterruptRegisters(startId, endId, fieldsPerRegister,
                (register, irqGetter, irqId, fieldIndex) =>
                    {
                        FieldMode fieldMode = 0;
                        Action<TEnum, TEnum> writeCallbackWrapped = null;
                        if(writeCallback != null)
                        {
                            fieldMode |= FieldMode.Write;
                            writeCallbackWrapped = (_, val) => writeCallback(irqGetter(), val);
                        }
                        Func<TEnum, TEnum> valueProviderCallbackWrapped = null;
                        if(valueProviderCallback != null)
                        {
                            fieldMode |= FieldMode.Read;
                            valueProviderCallbackWrapped = _ => valueProviderCallback(irqGetter());
                        }
                        register.WithEnumField<DoubleWordRegister, TEnum>(fieldIndex * fieldWidth, fieldWidth, fieldMode, name: $"{name}_{(uint)irqId}",
                            writeCallback: writeCallbackWrapped, valueProviderCallback: valueProviderCallbackWrapped);
                    },
                (register, fieldIndex) => register.WithReservedBits(fieldIndex * fieldWidth, fieldWidth)
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptRegisters(InterruptId startId, InterruptId endId, int fieldsPerRegister,
            Action<DoubleWordRegister, Func<Interrupt>, InterruptId, int> fieldAction,
            Action<DoubleWordRegister, int> fieldPlaceholderAction
        )
        {
            var interruptsCount = (int)endId - (int)startId + 1;
            var registersCount = (interruptsCount + fieldsPerRegister - 1) / fieldsPerRegister;
            var fieldIndex = 0;
            foreach(var registerFirstIrqId in InterruptId.GetRange(startId, endId, (uint)fieldsPerRegister))
            {
                var register = new DoubleWordRegister(this);
                var fieldsCount = fieldsPerRegister;
                if(fieldIndex + fieldsCount > interruptsCount)
                {
                    fieldsCount = interruptsCount % fieldsPerRegister;
                }
                foreach(var irqId in InterruptId.GetRange(registerFirstIrqId, new InterruptId((uint)registerFirstIrqId + (uint)fieldsCount - 1)))
                {
                    var inRegisterIndex = fieldIndex % fieldsPerRegister;
                    if(irqId.Type == InterruptType.Reserved)
                    {
                        fieldPlaceholderAction(register, inRegisterIndex);
                    }
                    else if(irqId.IsSoftwareGenerated || irqId.IsPrivatePeripheral)
                    {
                        fieldAction(register, () => GetAskingCPU().Interrupts[irqId], irqId, inRegisterIndex);
                    }
                    else
                    {
                        fieldAction(register, () => sharedInterrupts[irqId], irqId, inRegisterIndex);
                    }
                    fieldIndex++;
                }
                // Always fill the whole register
                for(; (fieldIndex % fieldsPerRegister) != 0; fieldIndex++)
                {
                    fieldPlaceholderAction(register, fieldIndex % fieldsPerRegister);
                }

                yield return register;
            }
        }

        private CPUEntry GetAskingCPU()
        {
            return GetCPUByConnectionId(0);
        }

        private CPUEntry GetCPUByConnectionId(int cpuConnectionId)
        {
            if(!cpuEntries.ContainsKey(cpuConnectionId))
            {
                throw new RecoverableException($"There is no CPU with a connection id equal to {cpuConnectionId}.");
            }
            return cpuEntries[cpuConnectionId];
        }

        private void AddRegistersAtOffset(Dictionary<long, DoubleWordRegister> registersMap, long offset, IEnumerable<DoubleWordRegister> registers)
        {
            foreach(var register in registers)
            {
                if(registersMap.ContainsKey(offset))
                {
                    throw new ConstructionException($"The register map already constains register at 0x{offset:x} offset.");
                }
                registersMap[offset] = register;
                offset += BytesPerDoubleWordRegister;
            }
        }

        private bool TryWriteByteToDoubleWordCollection(DoubleWordRegisterCollection registers, long offset, uint value)
        {
            AlignByteRegisterOffset(offset, BytesPerDoubleWordRegister, out var allignedOffset, out var byteShift);
            var registerExists = registers.TryRead(allignedOffset, out var currentValue);
            if(registerExists)
            {
                BitHelper.UpdateWithShifted(ref currentValue, value, byteShift, BitsPerByte);
                registerExists &= registers.TryWrite(allignedOffset, currentValue);
            }
            return registerExists;
        }

        private bool TryReadByteFromDoubleWordCollection(DoubleWordRegisterCollection registers, long offset, out byte value)
        {
            AlignByteRegisterOffset(offset, BytesPerDoubleWordRegister, out var registerOffset, out var byteShift);
            var registerExists = registers.TryRead(registerOffset, out var registerValue);
            value = (byte)(registerValue >> byteShift);
            return registerExists;
        }

        private void AlignByteRegisterOffset(long offset, int bytesPerRegister, out long allignedOffset, out int byteShift)
        {
            var byteOffset = (int)(offset % bytesPerRegister);
            allignedOffset = offset - byteOffset;
            byteShift = byteOffset * BitsPerByte;
        }

        private void LogWriteAccess(bool registerExists, object value, string collectionName, long offset, object prettyOffset)
        {
            if(!registerExists)
            {
                this.Log(LogLevel.Warning, "Unhandled write to 0x{0:X} register of {1}, value 0x{2:X}.", offset, collectionName, value);
            }
            this.Log(LogLevel.Noisy, "{0} writes to 0x{1:X} ({2}) register of {3}, value 0x{4:X}.", GetAskingCPU().Name, offset, prettyOffset, collectionName, value);
        }

        private void LogReadAccess(bool registerExists, object value, string collectionName, long offset, object prettyOffset)
        {
            if(!registerExists)
            {
                this.Log(LogLevel.Warning, "Unhandled read from 0x{0:X} register of {1}.", offset, collectionName);
            }
            this.Log(LogLevel.Noisy, "{0} reads from 0x{1:X} ({2}) register of {3}, returned 0x{4:X}.", GetAskingCPU().Name, offset, prettyOffset, collectionName, value);
        }

        private readonly ReadOnlyDictionary<int, CPUEntry> cpuEntries;
        private readonly ReadOnlyDictionary<InterruptId, SharedInterrupt> sharedInterrupts;
        private readonly ReadOnlyDictionary<GroupType, InterruptGroup> groups;
        private readonly DoubleWordRegisterCollection distributorRegisters;
        private readonly DoubleWordRegisterCollection redistributorDoubleWordRegisters;
        private readonly QuadWordRegisterCollection redistributorQuadWordRegisters;
        private readonly DoubleWordRegisterCollection cpuInterfaceRegisters;
        private readonly QuadWordRegisterCollection cpuInterfaceSystemRegisters;

        private readonly ARM_GenericInterruptControllerVersion architectureVersion;
        private readonly uint productIdentifier;
        private readonly byte cpuInterfaceRevisionNumber;
        private readonly uint cpuInterfaceImplementerIdentification;

        private const ARM_GenericInterruptControllerVersion DefaultArchitectureVersion = ARM_GenericInterruptControllerVersion.GICv3;
        private const uint DefaultProductIdentifier = 0x0;
        private const byte DefaultRevisionNumber = 0x0;
        private const uint DefaultImplementerIdentification = 0x43B; // This value indicates the JEP106 code of the Arm as an implementer 

        private const int CPUsCountWithoutAffinityRouting = 1;
        private const int BytesPerDoubleWordRegister = 4;
        private const int BitsPerByte = 8;

        private const long RedistributorPrivateInterruptsFrameOffset = 0x10000;

        private class CPUEntry : IGPIOReceiver
        {
            public CPUEntry(IEmulationElement parent, int cpuId, IEnumerable<GroupType> groupTypes)
            {
                this.parent = parent;
                Affinity = new CPUAffinity((uint)cpuId);
                Name = $"cpu{Affinity.Level0}";

                var irqIds = InterruptId.GetRange(InterruptId.SoftwareGeneratedFirst, InterruptId.SoftwareGeneratedLast)
                    .Concat(InterruptId.GetRange(InterruptId.PrivatePeripheralFirst, InterruptId.PrivatePeripheralLast))
                    .Concat(InterruptId.GetRange(InterruptId.ExtendedPrivatePeripheralFirst, InterruptId.ExtendedPrivatePeripheralLast));
                Interrupts = new ReadOnlyDictionary<InterruptId, Interrupt>(irqIds.ToDictionary(id => id, id => new Interrupt(id)));

                Groups = new ReadOnlyDictionary<GroupType, InterruptGroup>(groupTypes.ToDictionary(type => type, _ => new InterruptGroup()));
                RunnningInterrupts = new Stack<Interrupt>();
            }

            public void Reset()
            {
                foreach(var irq in Interrupts.Values)
                {
                    irq.Reset();
                }
                foreach(var group in Groups.Values)
                {
                    group.Reset();
                }
                BestPending = null;
                RunnningInterrupts.Clear();
                PriorityMask = InterruptPriority.Idle;
                IRQ.Unset();
            }

            // It's expected to pass handling of private interrupts to the ARM_GenericInterruptController class using an event handler
            public void OnGPIO(int number, bool value)
            {
                PrivateInterruptChanged?.Invoke(this, number, value);
            }

            public InterruptId AcknowledgeBestPending(GroupTypeRegister groupTypeRegister)
            {
                var pendingIrq = BestPending;
                if(pendingIrq == null)
                {
                    return InterruptId.NoPending;
                }
                var groupType = GetGroupTypeForRegister(groupTypeRegister);
                if(pendingIrq.GroupType != groupType)
                {
                    parent.Log(LogLevel.Warning, "Trying to acknowledge pending interrupt using register of an incorrect interrupt group ({0}), expected {1}.", groupType, pendingIrq.GroupType);
                    return InterruptId.NoPending;
                }
                pendingIrq.Acknowledge();
                RunnningInterrupts.Push(pendingIrq);
                BestPending = null;

                return pendingIrq.Identifier;
            }

            public void CompleteRunning(InterruptId id, GroupTypeRegister groupTypeRegister)
            {
                if(RunnningInterrupts.Count == 0)
                {
                    parent.Log(LogLevel.Warning, "Trying to complete interrupt when there is no running one.");
                    return;
                }
                var runningIrq = RunnningInterrupts.Peek();
                var groupType = GetGroupTypeForRegister(groupTypeRegister);
                if(runningIrq.GroupType != groupType)
                {
                    parent.Log(LogLevel.Warning, "Trying to complete the running interrupt using the register of an incorrect interrupt group ({0}), expected {1}, request ignored.", groupType, runningIrq.GroupType);
                    return;
                }
                if(!runningIrq.Identifier.Equals(id))
                {
                    parent.Log(LogLevel.Error, "Incorrect interrupt identifier for interrupt end, expected INTID {0}, given {1}, request ignored.", runningIrq.Identifier, id);
                    return;
                }
                runningIrq.Active = false;
                // The interrupt are just removed from stack of currently running interrupts
                // It's still accessible using one of the read only collections of interrupts (shared or private ones)
                RunnningInterrupts.Pop();
            }

            public void UpdateSignals()
            {
                // TODO: Properly signal FIQ depends on the security state and the EL
                IRQ.Set(BestPending != null);
            }

            public InterruptGroup GetGroupForRegister(GroupTypeRegister type)
            {
                return Groups[GetGroupTypeForRegister(type)];
            }

            public GroupType GetGroupTypeForRegister(GroupTypeRegister type)
            {
                if(type == GroupTypeRegister.Group0)
                {
                    return GroupType.Group0;
                }
                else if(type == GroupTypeRegister.Group1)
                {
                    if(SecurityState == SecurityState.NonSecure)
                    {
                        return GroupType.Group1NonSecure;
                    }
                    else if(SecurityState == SecurityState.Secure)
                    {
                        return GroupType.Group1Secure;
                    }
                }
                throw new ArgumentOutOfRangeException($"There is no valid InterruptGroupType for value: {type}.");
            }

            public event Action<CPUEntry, int, bool> PrivateInterruptChanged;

            public IReadOnlyDictionary<InterruptId, Interrupt> Interrupts { get; }
            public IReadOnlyDictionary<GroupType, InterruptGroup> Groups { get; }
            public CPUAffinity Affinity { get; }
            public string Name { get; }
            public Interrupt BestPending { get; set; }
            public Stack<Interrupt> RunnningInterrupts { get; }
            public InterruptPriority RunningPriority => RunnningInterrupts.Count > 0 ? RunnningInterrupts.Peek().Priority : InterruptPriority.Idle;
            public InterruptPriority PriorityMask { get; set; }
            public GPIO IRQ { get; } = new GPIO();

            public SecurityState SecurityState => SecurityState.Secure;

            private readonly IEmulationElement parent;
        }

        private class Interrupt
        {
            public Interrupt(InterruptId identifier)
            {
                Identifier = identifier;
            }

            public virtual void Reset()
            {
                Enabled = false;
                GroupBit = false;
                GroupModifierBit = false;
                TriggerType = default(InterruptTriggerType);
                State = InterruptState.Inactive;
                Priority = default(InterruptPriority);
            }

            public void Acknowledge()
            {
                if(State == InterruptState.Inactive)
                {
                    throw new InvalidOperationException("It's invalid to acknowledge an interrupt in the inactive state.");
                }
                if(TriggerType == InterruptTriggerType.EdgeTriggered)
                {
                    State = InterruptState.Active;
                }
                else if(TriggerType == InterruptTriggerType.LevelSensitive)
                {
                    State = InterruptState.ActiveAndPending;
                }
            }

            public void AssertAsPending(bool signal)
            {
                if(TriggerType == InterruptTriggerType.EdgeTriggered)
                {
                    Pending |= signal;
                }
                else if(TriggerType == InterruptTriggerType.LevelSensitive)
                {
                    Pending = signal;
                }
            }

            public InterruptId Identifier { get; }
            public bool Enabled { get; set; }
            public bool GroupBit { get; set; }
            public bool GroupModifierBit { get; set; }
            public InterruptTriggerType TriggerType { get; set; }
            public InterruptState State { get; private set; }
            public InterruptPriority Priority { get; set; }

            public bool Pending
            {
                get => (State & InterruptState.Pending) != 0;
                set
                {
                    if(value)
                    {
                        State |= InterruptState.Pending;
                    }
                    else
                    {
                        State &= ~InterruptState.Pending;
                    }
                }
            }

            public bool Active
            {
                get => (State & InterruptState.Active) != 0;
                set
                {
                    if(value)
                    {
                        State |= InterruptState.Active;
                    }
                    else
                    {
                        State &= ~InterruptState.Active;
                    }
                }
            }

            public GroupType GroupType
            {
                get
                {
                    if(GroupBit)
                    {
                        return GroupType.Group1NonSecure;
                    }
                    else if(GroupModifierBit)
                    {
                        return GroupType.Group1Secure;
                    }
                    else
                    {
                        return GroupType.Group0;
                    }
                }
            }
        }

        private class SharedInterrupt : Interrupt
        {
            public SharedInterrupt(InterruptId irqId) : base(irqId) { }

            public override void Reset()
            {
                base.Reset();
                TargetCPU = 0;
            }

            public bool IsTargetingCPU(CPUEntry cpu)
            {
                return (TargetCPU & cpu.Affinity.TargetFieldFlag) != 0;
            }

            public byte TargetCPU { get; set; }
        }

        // This class will be extended at least for the Binary Point register support
        // It may be needed to separate it for CPUInterface and Distributor 
        private class InterruptGroup
        {
            public void Reset()
            {
                Enabled = false;
            }

            public bool Enabled { get; set; }
        }

        private struct InterruptId
        {
            public static IEnumerable<InterruptId> GetRange(InterruptId start, InterruptId end, uint step = 1)
            {
                for(var id = (uint)start; id <= (uint)end; id += step)
                {
                    yield return new InterruptId(id);
                }
            }

            public static explicit operator uint(InterruptId id) => id.id;
            public static explicit operator int(InterruptId id) => (int)id.id;

            public InterruptId(uint interruptId)
            {
                id = interruptId;
            }

            public InterruptType Type
            {
                get
                {
                    if(IsSoftwareGenerated)
                    {
                        return InterruptType.SoftwareGenerated;
                    }
                    else if(IsPrivatePeripheral)
                    {
                        return InterruptType.PrivatePeripheral;
                    }
                    else if(IsSharedPeripheral)
                    {
                        return InterruptType.SharedPeripheral;
                    }
                    else if(IsSpecial)
                    {
                        return InterruptType.SpecialIdentifier;
                    }
                    else if(IsLocalitySpecificPeripheral)
                    {
                        return InterruptType.LocalitySpecificPeripheral;
                    }
                    return InterruptType.Reserved;
                }
            }

            public override string ToString()
            {
                return $"{id} ({Type})";
            }

            public bool IsSoftwareGenerated => id <= (uint)InterruptId.SoftwareGeneratedLast;

            public bool IsPrivatePeripheral => ((uint)InterruptId.PrivatePeripheralFirst <= id && id <= (uint)InterruptId.PrivatePeripheralLast)
                || ((uint)InterruptId.ExtendedPrivatePeripheralFirst <= id && id <= (uint)InterruptId.ExtendedPrivatePeripheralLast);

            public bool IsSharedPeripheral => ((uint)InterruptId.SharedPeripheralFirst <= id && id <= (uint)InterruptId.SharedPeripheralLast)
                || ((uint)InterruptId.ExtendedSharedPeripheralFirst <= id && id <= (uint)InterruptId.ExtendedSharedPeripheralLast);

            public bool IsSpecial => (uint)InterruptId.ExpectedToHandleAtSecure <= id && id <= (uint)InterruptId.NoPending;

            public bool IsLocalitySpecificPeripheral => id <= (uint)InterruptId.LocalitySpecificPeripheralFirst;

            public static readonly InterruptId SoftwareGeneratedFirst = new InterruptId(0);
            public static readonly InterruptId SoftwareGeneratedLast = new InterruptId(15);
            public static readonly InterruptId PrivatePeripheralFirst = new InterruptId(16);
            public static readonly InterruptId PrivatePeripheralLast = new InterruptId(31);
            public static readonly InterruptId SharedPeripheralFirst = new InterruptId(32);
            public static readonly InterruptId SharedPeripheralLast = new InterruptId((uint)SharedPeripheralFirst + SharedPeripheralCount - 1);
            public static readonly InterruptId ExpectedToHandleAtSecure = new InterruptId(1020);
            public static readonly InterruptId ExpectedToHandleAtNonSecure = new InterruptId(1021);
            public static readonly InterruptId IndicateNonMaskableInterrupt = new InterruptId(1022);
            public static readonly InterruptId NoPending = new InterruptId(1023);
            public static readonly InterruptId ExtendedPrivatePeripheralFirst = new InterruptId(1056);
            public static readonly InterruptId ExtendedPrivatePeripheralLast = new InterruptId(1119);
            public static readonly InterruptId ExtendedSharedPeripheralFirst = new InterruptId(4096);
            public static readonly InterruptId ExtendedSharedPeripheralLast = new InterruptId((uint)ExtendedSharedPeripheralFirst + SharedPeripheralExtendedCount - 1);
            public static readonly InterruptId LocalitySpecificPeripheralFirst = new InterruptId(819);

            public const uint SharedPeripheralExtendedCount = 1024;
            public const uint SharedPeripheralCount = 988;
            public const uint SupportedIdentifierBits = 14;

            private readonly uint id;
        }

        private struct CPUAffinity
        {
            // TODO: Add other affinity levels too
            public CPUAffinity(uint cpuIdentifier)
            {
                Level0 = (byte)cpuIdentifier;
            }

            public byte Level0 { get; }
            public uint AllLevels => Level0;
            public byte TargetFieldFlag => (byte)(1 << Level0);
        }

        private enum InterruptPriority : byte
        {
            Highest = 0x00,
            Idle = 0xFF
        }

        [Flags]
        private enum InterruptState
        {
            Inactive = 0b00,
            Pending = 0b01,
            Active = 0b10,
            ActiveAndPending = Pending | Active
        }

        private enum InterruptTriggerType : byte
        {
            LevelSensitive = 0b00,
            EdgeTriggered = 0b10
        }

        private enum InterruptType
        {
            SoftwareGenerated,
            PrivatePeripheral,
            SharedPeripheral,
            SpecialIdentifier,
            LocalitySpecificPeripheral,
            Reserved
        }

        private enum GroupType
        {
            Group0,
            Group1Secure,
            Group1NonSecure
        }

        private enum GroupTypeRegister
        {
            Group0,
            Group1
        }

        private enum DistributorRegisters : long
        {
            Control = 0x0000, // GICD_CTLR 
            ControllerType = 0x0004, // GICD_TYPER 
            ImplementerIdentification = 0x0008, // GICD_IIDR 
            ControllerType2 = 0x000C2, // GICD_TYPER2 
            ErrorReportingStatus = 0x0010, // GICD_STATUSR 
            SharedPeripheralInterruptSetNonSecure = 0x0040, // GICD_SETSPI_NSR 
            SharedPeripheralInterruptClearNonSecure = 0x0048, // GICD_CLRSPI_NSR 
            SharedPeripheralInterruptSetSecure = 0x0050, // GICD_SETSPI_SR 
            SharedPeripheralInterruptClearSecure = 0x0058, // GICD_CLRSPI_SR 
            InterruptGroup_0 = 0x0080, // GICD_IGROUPR<n>
            InterruptSetEnable_0 = 0x0100, // GICD_ISENABLER<n>
            InterruptClearEnable_0 = 0x0180, // GICD_ICENABLER<n>
            InterruptSetPending_0 = 0x0200, // GICD_ISPENDR<n>
            InterruptClearPending_0 = 0x0280, // GICD_ICPENDR<n>
            InterruptSetActive_0 = 0x0300, // GICD_ISACTIVER<n>
            InterruptClearActive_0 = 0x0380, // GICD_ICACTIVER<n>
            InterruptPriority_0 = 0x0400, // GICD_IPRIORITYR<n>
            InterruptProcessorTargets_0 = 0x0800, // GICD_ITARGETSR<n>
            InterruptProcessorTargets_8 = 0x0820, // GICD_ITARGETSR<n>
            InterruptConfiguration_0 = 0x0C00, // GICD_ICFGR<n>
            InterruptConfiguration_1 = 0x0C04, // GICD_ICFGR<n>
            InterruptGroupModifier_0 = 0x0D00, // GICD_IGRPMODR<n>
            NonSecureAccessControl_0 = 0x0E00, // GICD_NSACR<n>
            SoftwareGeneratedInterruptControl = 0x0F00, // GICD_SGI 
            SoftwareGeneratedIntrruptClearPending_0 = 0x0F10, // GICD_CPENDSGIR<n>
            SoftwareGeneratedIntrruptSetPending_0 = 0x0F20, // GICD_SPENDSGIR<n>
            NonMaskableInterrupt_0 = 0x0F80, // GICD_INMIR<n>
            SharedPeripheralInterruptExtendedGroup_0 = 0x1000, // GICD_IGROUPR<n>E
            SharedPeripheralInterruptExtendedSetEnable_0 = 0x1200, // GICD_ISENABLER<n>E
            SharedPeripheralInterruptExtendedClearEnable_0 = 0x1400, // GICD_ICENABLER<n>E
            SharedPeripheralInterruptExtendedSetPending_0 = 0x1600, // GICD_ISPENDR<n>E
            SharedPeripheralInterruptExtendedClearPending_0 = 0x1800, // GICD_ICPENDR<n>E
            SharedPeripheralInterruptExtendedSetActive_0 = 0x1A00, // GICD_ISACTIVER<n>E
            SharedPeripheralInterruptExtendedClearActive_0 = 0x1C00, // GICD_ICACTIVER<n>E
            SharedPeripheralInterruptExtendedPriority_0 = 0x2000, // GICD_IPRIORITYR<n>E
            SharedPeripheralInterruptExtendedConfiguration_0 = 0x3000, // GICD_ICFGR<n>E
            SharedPeripheralInterruptExtendedGroupModifier_0 = 0x3400, // GICD_IGRPMODR<n>E
            SharedPeripheralInterruptExtendedNonSecureAccessControl_0 = 0x3600, // GICD_NSACR<n>E
            SharedPeripheralInterruptExtendedNonMaskable_0 = 0x3B00, // GICD_INMIR<n>E
            InterruptRouting_0 = 0x6100, // GICD_IROUTER<n>
            SharedPeripheralInterruptExtendedRouting_0 = 0x8000 // GICD_IROUTER<n>E
        }

        private enum RedistributorRegisters : long
        {
            Control = 0x0000, // GICR_CTLR
            ImplementerIdentification = 0x0004, // GICR_IIDR
            ControllerType = 0x0008, // GICR_TYPER
            ErrorReportingStatus = 0x0010, // GICR_STATUSR
            Wake = 0x0014, // GICR_WAKER
            MaximumPARTIDAndPMG = 0x0018, // GICR_MPAMIDR
            SetPARTIDAndPMG = 0x001C, // GICR_PARTIDR
            SetLocalitySpecificPeripheralInterruptPending = 0x0040, // GICR_SETLPIR
            ClearLocalitySpecificPeripheralInterruptPending = 0x0048, // GICR_CLRLPIR
            PropertiesBaseAddress = 0x0070, // GICR_PROPBASER
            LocalitySpecificPeripheralInterruptPendingTableBaseAddress = 0x0078, // GICR_PENDBASER
            InvalidateLocalitySpecificPeripheralInterrupt = 0x00A0, // GICR_INVLPIR
            InvalidateAll = 0x00B0, // GICR_INVALLR
            Synchronize = 0x00C0, // GICR_SYNCR

            // Registers from the SGI_base frame
            InterruptGroup_0 = 0x0080 + RedistributorPrivateInterruptsFrameOffset, // GICR_IGROUPR0 
            PrivatePeripheralInterruptExtendedGroup_0 = 0x0084 + RedistributorPrivateInterruptsFrameOffset, // GICR_IGROUPR<n>E 
            InterruptSetEnable_0 = 0x0100 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISENABLER0 
            PrivatePeripheralInterruptExtendedSetEnable_0 = 0x0104 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISENABLER<n>E 
            PrivatePeripheralInterruptExtendedClearEnable_0 = 0x0184 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICENABLER<n>E 
            InterruptClearEnable_0 = 0x0180 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICENABLER0 
            InterruptSetPending0 = 0x0200 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISPENDR0 
            PrivatePeripheralInterruptExtendedSetPending_0 = 0x0204 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISPENDR<n>E 
            InterruptClearPending_0 = 0x0280 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICPENDR0 
            PrivatePeripheralInterruptExtendedClearPending_0 = 0x0284 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICPENDR<n>E 
            InterruptSetActive_0 = 0x0300 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISACTIVER0 
            PrivatePeripheralInterruptExtendedSetActive_0 = 0x0304 + RedistributorPrivateInterruptsFrameOffset, // GICR_ISACTIVER<n>E 
            InterruptClearActive_0 = 0x0380 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICACTIVER0 
            PrivatePeripheralInterruptExtendedClearActive_0 = 0x0384 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICACTIVER<n>E 
            InterruptPriority_0 = 0x0400 + RedistributorPrivateInterruptsFrameOffset, // GICR_IPRIORITYR<n> 
            PrivatePeripheralInterruptExtendedPriority_0 = 0x0420 + RedistributorPrivateInterruptsFrameOffset, // GICR_IPRIORITYR<n>E 
            SoftwareGeneratedInterruptConfiguration = 0x0C00 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICFGR0 
            PrivatePeripheralInterruptConfiguration = 0x0C04 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICFGR1 
            PrivatePeripheralInterruptExtendedConfiguration_0 = 0x0C08 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICFGR<n>E 
            InterruptGroupModifier_0 = 0x0D00 + RedistributorPrivateInterruptsFrameOffset, // GICR_IGRPMODR0 
            PrivatePeripheralInterruptExtendedGroupModifier_0 = 0x0D04 + RedistributorPrivateInterruptsFrameOffset, // GICR_IGRPMODR<n>E 
            NonSecureAccessControl = 0x0E00 + RedistributorPrivateInterruptsFrameOffset, // GICR_NSACR 
            PrivatePeripheralInterruptNonMaskable = 0x0F80 + RedistributorPrivateInterruptsFrameOffset, // GICR_INMIR0 
            PrivatePeripheralInterruptExtendeNonMaskable_0 = 0x0F84 + RedistributorPrivateInterruptsFrameOffset, // GICR_INMIR<n>E 
        }

        private enum CPUInterfaceRegisters : long
        {
            Control = 0x0000, // GICC_CTLR
            PriorityMask = 0x0004, // GICC_PMR
            PriorityBinaryPoint = 0x0008, // GICC_BPR
            InterruptAcknowledge = 0x000C, // GICC_IAR
            InterruptEnd = 0x0010, // GICC_EOIR
            RunningPriority = 0x0014, // GICC_RPR
            HighestPriorityPendingInterrupt = 0x0018, // GICC_HPPIR
            ActivePriority = 0x001C, // GICC_ABPR
            InterruptAcknowledgeAlias = 0x0020, // GICC_AIAR
            InterruptEndAlias = 0x0024, // GICC_AEOIR
            HighestPriorityPendingInterruptAlias = 0x0028, // GICC_AHPPIR
            ErrorReportingStatus = 0x002C, // GICC_STATUSR 
            ActivePriorities_0 = 0x00D0, // GICC_APR<n> 
            ActivePrioritiesNonSecure_0 = 0x00E0, // GICC_NSAPR<n> 
            InterfaceIdentification = 0x00FC, // GICC_IIDR 
            InterruptDeactivate = 0x1000, // GICC_DIR  
        }

        private enum CPUInterfaceSystemRegisters : long
        {
            // Enum values are created from op0, op1, CRn, CRm and op2 fields of the MRS instruction
            SystemRegisterEnableEL3 = 0xF665, // ICC_SRE_EL3
            SystemRegisterEnableEL1 = 0xC665, // ICC_SRE_EL1
            PriorityMask = 0xC230, // ICC_PMR_EL1
            GroupEnable1 = 0xC667, // ICC_IGRPEN1_EL1
            ActivePriorityGroup0_0 = 0xC644, // ICC_AP0R0_EL1
            ActivePriorityGroup0_1 = 0xC645, // ICC_AP0R1_EL1
            ActivePriorityGroup0_2 = 0xC646, // ICC_AP0R2_EL1
            ActivePriorityGroup0_3 = 0xC647, // ICC_AP0R3_EL1
            ActivePriorityGroup1_0 = 0xC648, // ICC_AP1R0_EL1
            ActivePriorityGroup1_1 = 0xC649, // ICC_AP1R1_EL1
            ActivePriorityGroup1_2 = 0xC64A, // ICC_AP1R2_EL1
            ActivePriorityGroup1_3 = 0xC64B, // ICC_AP1R3_EL1
            SoftwareGeneratedInterruptGroup1GenerateAlias = 0xC65E, // ICC_ASGI1R_EL1
            PriorityBinaryPointGroup0 = 0xC643, // ICC_BPR0_EL1
            PriorityBinaryPointGroup1 = 0xC663, // ICC_BPR1_EL1
            ControlEL1 = 0xC664, // ICC_CTLR_EL1
            ControlEL3 = 0xF664, // ICC_CTLR_EL3
            InterruptDeactivate = 0xC659, // ICC_DIR_EL1
            InterruptEndGroup0 = 0xC641, // ICC_EOIR0_EL1
            InterruptEndGroup1 = 0xC661, // ICC_EOIR1_EL1
            HighestPriorityPendingInterruptGroup0 = 0xC642, // ICC_HPPIR0_EL1
            HighestPriorityPendingInterruptGroup1 = 0xC662, // ICC_HPPIR1_EL1
            InterruptAcknowledgeGroup0 = 0xC640, // ICC_IAR0_EL1
            InterruptAcknowledgeGroup1 = 0xC660, // ICC_IAR1_EL1
            GroupEnable0 = 0xC666, // ICC_IGRPEN0_EL1
            GroupEnable1EL3 = 0xF667, // ICC_IGRPEN1_EL3
            InterruptAcknowladgeNonMaskable = 0xC64D, // ICC_NMIAR1_EL1
            RunningPriority = 0xC65B, // ICC_RPR_EL1
            SoftwareGeneratedInterruptGroup0Generate = 0xC65F, // ICC_SGI0R_EL1
            SoftwareGeneratedInterruptGroup1Generate = 0xC65D, // ICC_SGI1R_EL1
            SystemRegisterEnableEL2 = 0xE64D, // ICC_SRE_EL2
        }
    }

    public enum ARM_GenericInterruptControllerVersion : byte
    {
        GICv1 = 1,
        GICv2 = 2,
        GICv3 = 3,
        GICv4 = 4
    }
}
