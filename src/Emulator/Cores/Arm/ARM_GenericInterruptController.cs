//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.IRQControllers.ARM_GenericInterruptControllerModel;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    // NOTE: Memory mapped Virtual CPU Interface is currently not supported.
    //       It can be accesses only through system registers.
    public class ARM_GenericInterruptController : IARMCPUsConnectionsProvider, IBusPeripheral, ILocalGPIOReceiver, INumberedGPIOOutput, IIRQController, IHasAutomaticallyConnectedGPIOOutputs
    {
        public ARM_GenericInterruptController(IMachine machine, bool supportsTwoSecurityStates = true, ARM_GenericInterruptControllerVersion architectureVersion = ARM_GenericInterruptControllerVersion.Default, uint sharedPeripheralCount = 960)
        {
            if(architectureVersion == ARM_GenericInterruptControllerVersion.GICv1)
            {
                // On GICv1, `GICD_ITARGETSRn` supports up to 8 CPU targets, so we ignore higher affinity levels completely
                affinityToIdMask = 0xFF;
            }
            else
            {
                affinityToIdMask = uint.MaxValue;
            }
            busController = machine.GetSystemBus(this);

            if(sharedPeripheralCount > InterruptsDecoder.MaximumSharedPeripheralCount)
            {
                throw new ConstructionException($"The number of shared peripherals {sharedPeripheralCount} is larger than supported {(InterruptsDecoder.MaximumSharedPeripheralCount)}");
            }

            // The behavior of the GIC doesn't directly depend on the supportsTwoSecurityState field
            // The disabledSecurity field corresponds to the GICD_CTRL.DS flag described in the GICv3 Architecture Specification
            // The GIC without support for two security states has disabled security
            // Changing the disabledSecurity field affects the behavior and register map of a GIC
            // Once security is disabled it's impossible to enable it
            // So it is impossible to enable security for the GIC that doesn't support two security states
            this.supportsTwoSecurityStates = supportsTwoSecurityStates;
            if(architectureVersion == ARM_GenericInterruptControllerVersion.Default)
            {
                this.Log(LogLevel.Warning, "GIC architecture version not explicitly set. Defaulting to v3");
                this.ArchitectureVersion = DefaultArchitectureVersion;
            }
            else
            {
                this.ArchitectureVersion = architectureVersion;
            }

            this.irqsDecoder = new InterruptsDecoder(sharedPeripheralCount, identifierBits: 10);

            var irqIds = InterruptId.GetRange(irqsDecoder.SharedPeripheralFirst, irqsDecoder.SharedPeripheralLast)
                .Concat(InterruptId.GetRange(irqsDecoder.ExtendedSharedPeripheralFirst, irqsDecoder.ExtendedSharedPeripheralLast));
            sharedInterrupts = new ReadOnlyDictionary<InterruptId, SharedInterrupt>(irqIds.ToDictionary(id => id, id => new SharedInterrupt(id)));

            var groupTypes = new[]
            {
                GroupType.Group0,
                GroupType.Group1NonSecure,
                GroupType.Group1Secure
            };
            groups = new ReadOnlyDictionary<GroupType, InterruptGroup>(groupTypes.ToDictionary(type => type, _ => new InterruptGroup()));

            supportedInterruptSignals = (InterruptSignalType[])Enum.GetValues(typeof(InterruptSignalType));
            Connections = new ReadOnlyDictionary<int, IGPIO>(new Dictionary<int, IGPIO>());

            // Field layouts of some of the registers depend on the current security state
            distributorRegistersSecureView = new DoubleWordRegisterCollection(this, BuildDistributorRegistersMapSecurityView(false, SecurityState.Secure));
            distributorRegistersNonSecureView = new DoubleWordRegisterCollection(this, BuildDistributorRegistersMapSecurityView(false, SecurityState.NonSecure));
            distributorRegistersDisabledSecurityView = new DoubleWordRegisterCollection(this, BuildDistributorRegistersMapSecurityView(true));
            cpuInterfaceRegistersSecureView = new DoubleWordRegisterCollection(this, BuildCPUInterfaceRegistersMapSecurityView(false, SecurityState.Secure));
            cpuInterfaceRegistersNonSecureView = new DoubleWordRegisterCollection(this, BuildCPUInterfaceRegistersMapSecurityView(false, SecurityState.NonSecure));
            cpuInterfaceRegistersDisabledSecurityView = new DoubleWordRegisterCollection(this, BuildCPUInterfaceRegistersMapSecurityView(true));

            // The rest may behave differently for various security settings, but the layout of fields doesn't change
            distributorDoubleWordRegisters = new DoubleWordRegisterCollection(this, BuildDistributorDoubleWordRegistersMap());
            distributorQuadWordRegisters = new QuadWordRegisterCollection(this, BuildDistributorQuadWordRegistersMap());
            cpuInterfaceRegisters = new DoubleWordRegisterCollection(this, BuildCPUInterfaceRegistersMap());
            cpuInterfaceSystemRegisters = new QuadWordRegisterCollection(this, BuildCPUInterfaceSystemRegistersMap());

            Reset();
        }

        public void Reset()
        {
            LockExecuteAndUpdate(() =>
                {
                    ackControl = false;
                    enableFIQ = ArchitectureVersionAtLeast3;
                    disabledSecurity = false;
                    affinityRoutingEnabledSecure = false;
                    affinityRoutingEnabledNonSecure = false;
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

            distributorRegistersSecureView.Reset();
            distributorRegistersNonSecureView.Reset();
            distributorRegistersDisabledSecurityView.Reset();
            distributorDoubleWordRegisters.Reset();
            distributorQuadWordRegisters.Reset();
            cpuInterfaceRegistersSecureView.Reset();
            cpuInterfaceRegistersNonSecureView.Reset();
            cpuInterfaceRegistersDisabledSecurityView.Reset();
            cpuInterfaceRegisters.Reset();
            cpuInterfaceSystemRegisters.Reset();
        }

        public void AttachCPU(IARMSingleSecurityStateCPU cpu)
        {
            if(ArchitectureVersion == ARM_GenericInterruptControllerVersion.GICv1)
            {
                var affinities = cpuEntries.Keys.Select(c => c.Affinity).Select(a => new Affinity(a.AllLevels & ~affinityToIdMask));
                var highAffinity = cpu.Affinity.AllLevels & ~affinityToIdMask;
                if(affinities.Any(a => highAffinity != a.AllLevels))
                {
                    throw new RecoverableException($"Previously registered CPUs have Affinities above Aff0 different from {new Affinity(highAffinity)}."
                        + $" This is illegal for {nameof(ARM_GenericInterruptControllerVersion.GICv1)}.");
                }
            }
            var processorNumber = GetProcessorNumber(cpu);
            this.Log(LogLevel.Noisy, "Trying to attach CPU {0}", cpu.Affinity);
            if(TryGetCPUEntry(processorNumber, out var existingCPUEntry))
            {
                throw new RecoverableException($"The CPU with the Processor Number {processorNumber} already exists.");
            }
            if(cpuEntries.Values.Any(entry => entry.affinity.AllLevels == cpu.Affinity.AllLevels))
            {
                throw new RecoverableException($"The CPU with the affinity {cpu.Affinity} already exists.");
            }

            var cpuMappedConnections = supportedInterruptSignals.ToDictionary(type => type, _ => (IGPIO)new GPIO());
            CPUEntry cpuEntry = null;
            var cpuTwoSecurityStates = cpu as IARMTwoSecurityStatesCPU;
            if(cpuTwoSecurityStates != null)
            {
                cpuEntry = new CPUEntryWithTwoSecurityStates(this, cpuTwoSecurityStates, groups.Keys, cpuMappedConnections);
                cpuTwoSecurityStates.ExecutionModeChanged += (_, __) => OnExecutionModeChanged(cpuEntry);
            }
            else
            {
                if(this.supportsTwoSecurityStates)
                {
                    cpu.Log(LogLevel.Info, "CPU is attached to GIC supporting Security Extensions."
                            + " This CPU doesn't implement Security Extensions so all its GIC accesses will be {0}. GIC's '{1}'"
                            + " constructor argument can be set to 'false' to create GIC without support for Security Extensions.",
                            cpu.SecurityState, nameof(supportsTwoSecurityStates));
                }
                cpuEntry = new CPUEntry(this, cpu, groups.Keys, cpuMappedConnections);
            }
            cpuEntry.PrivateInterruptChanged += OnPrivateInterrupt;

            // The convention of connecting interrupt signals can be found near the InterruptSignalType definition
            var firstGPIO = (int)processorNumber * supportedInterruptSignals.Length;
            var cpuConnections = cpuMappedConnections.Select(x => new KeyValuePair<int, IGPIO>(firstGPIO + (int)x.Key, x.Value));
            Connections = new ReadOnlyDictionary<int, IGPIO>(Connections.Concat(cpuConnections).ToDictionary(x => x.Key, x => x.Value));

            // SGIs require an information about a requesting CPU when Affinity Routing is disabled.
            foreach(var requester in cpuEntries.Values.Where(req => req.processorNumber < CPUsCountLegacySupport))
            {
                cpuEntry.RegisterLegacySGIRequester(requester);
            }

            cpusByProcessorNumberCache.Add(processorNumber, cpu);
            cpuEntries.Add(cpu, cpuEntry);
            CPUAttached?.Invoke(cpu);

            if(processorNumber < CPUsCountLegacySupport)
            {
                legacyCpusAttachedMask |= cpuEntry.targetFieldFlag;
                // The new attached CPU need to be registered for all CPUs including itself.
                foreach(var target in cpuEntries.Values)
                {
                    target.RegisterLegacySGIRequester(cpuEntry);
                }
            }

            AddAutomaticGPIOConnections(cpu);
        }

        [ConnectionRegion("distributor")]
        public void WriteByteToDistributor(long offset, byte value)
        {
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = IsDistributorByteAccessible(offset) && Utils.TryWriteByteToDoubleWordCollection(distributorDoubleWordRegisters, offset, value, this);
                    LogWriteAccess(registerExists, value, "Distributor (byte access)", offset, (DistributorRegisters)offset);
                }
            );
        }

        [ConnectionRegion("distributor")]
        public byte ReadByteFromDistributor(long offset)
        {
            byte value = 0;
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = IsDistributorByteAccessible(offset) && Utils.TryReadByteFromDoubleWordCollection(distributorDoubleWordRegisters, offset, out value, this);
                    LogReadAccess(registerExists, value, "Distributor (byte access)", offset, (DistributorRegisters)offset);
                }
            );
            return value;
        }

        [ConnectionRegion("distributor")]
        public void WriteDoubleWordToDistributor(long offset, uint value)
        {
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = TryWriteRegisterSecurityView(offset, value, distributorDoubleWordRegisters,
                        distributorRegistersSecureView, distributorRegistersNonSecureView, distributorRegistersDisabledSecurityView);
                    registerExists = registerExists || Utils.TryWriteDoubleWordToQuadWordCollection(distributorQuadWordRegisters, offset, value, this);
                    LogWriteAccess(registerExists, value, "Distributor", offset, (DistributorRegisters)offset);
                }
            );
        }

        [ConnectionRegion("distributor")]
        public uint ReadDoubleWordFromDistributor(long offset)
        {
            uint value = 0;
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = TryReadRegisterSecurityView(offset, out value, distributorDoubleWordRegisters,
                        distributorRegistersSecureView, distributorRegistersNonSecureView, distributorRegistersDisabledSecurityView);
                    registerExists = registerExists || Utils.TryReadDoubleWordFromQuadWordCollection(distributorQuadWordRegisters, offset, out value, this);
                    LogReadAccess(registerExists, value, "Distributor", offset, (DistributorRegisters)offset);
                }
            );
            return value;
        }

        [ConnectionRegion("distributor")]
        public void WriteQuadWordToDistributor(long offset, ulong value)
        {
            LockExecuteAndUpdate(() =>
                LogWriteAccess(distributorQuadWordRegisters.TryWrite(offset, value), value, "Distributor", offset, (DistributorRegisters)offset)
            );
        }

        [ConnectionRegion("distributor")]
        public ulong ReadQuadWordFromDistributor(long offset)
        {
            ulong value = 0;
            LockExecuteAndUpdate(() =>
                LogWriteAccess(distributorQuadWordRegisters.TryRead(offset, out value), value, "Distributor", offset, (DistributorRegisters)offset)
            );
            return value;
        }

        [ConnectionRegion("cpuInterface")]
        public void WriteDoubleWordToCPUInterface(long offset, uint value)
        {
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = TryWriteRegisterSecurityView(offset, value, cpuInterfaceRegisters,
                        cpuInterfaceRegistersSecureView, cpuInterfaceRegistersNonSecureView, cpuInterfaceRegistersDisabledSecurityView);
                    LogWriteAccess(registerExists, value, "memory-mapped CPU Interface", offset, (CPUInterfaceRegisters)offset);
                }
            );
        }

        [ConnectionRegion("cpuInterface")]
        public uint ReadDoubleWordFromCPUInterface(long offset)
        {
            uint value = 0;
            LockExecuteAndUpdate(() =>
                {
                    var registerExists = TryReadRegisterSecurityView(offset, out value, cpuInterfaceRegisters,
                        cpuInterfaceRegistersSecureView, cpuInterfaceRegistersNonSecureView, cpuInterfaceRegistersDisabledSecurityView);
                    LogReadAccess(registerExists, value, "memory-mapped CPU Interface", offset, (CPUInterfaceRegisters)offset);
                }
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

        // Handles SPIs.
        public void OnGPIO(int number, bool value)
        {
            var irqId = new InterruptId((uint)number + (uint)irqsDecoder.SharedPeripheralFirst);
            if(!irqsDecoder.IsSharedPeripheral(irqId))
            {
                this.Log(LogLevel.Warning, "Generated interrupt isn't a Shared Peripheral Interrupt, interrupt identifier: {0}", irqId);
                return;
            }
            this.Log(LogLevel.Debug, "Setting Shared Peripheral Interrupt #{0} signal to {1}", irqId, value);
            LockExecuteAndUpdate(() =>
                sharedInterrupts[irqId].State.AssertAsPending(value)
            );
        }

        // Private Peripheral Interrupts are connected using the ILocalGPIOReceiver interface
        // Every CPUEntry class implements the IGPIOReceiver interface used to connect PPIs to each CPU
        // The CPUEntry provides event for handling received interrupts by an external action
        // It's expected to handle all of these interrupts by OnPrivateInterrupt method
        public IGPIOReceiver GetLocalReceiver(int processorNumber)
        {
            return GetCPUEntry((uint)processorNumber);
        }

        public IEnumerable<uint> GetEnabledInterruptIdentifiers(uint processorNumber)
        {
            var cpu = GetCPUEntry(processorNumber);
            lock(locker)
            {
                return GetAllEnabledInterrupts(cpu).Select(irq => (uint)irq.Identifier);
            }
        }

        public IEnumerable<ArmGicRedistributorRegistration> GetRedistributorRegistrations()
        {
            return this.GetMachine().GetSystemBus(this).GetRegistrationPoints(this).OfType<ArmGicRedistributorRegistration>();
        }

        public void LockExecuteAndUpdate(Action action)
        {
            lock(locker)
            {
                action();
                UpdateBestPendingInterrupts();
                foreach(var cpu in cpuEntries.Values)
                {
                    cpu.UpdateSignals();
                }
            }
        }

        public void LogWriteAccess(bool registerExists, object value, string collectionName, long offset, object prettyOffset)
        {
            this.Log(LogLevel.Noisy, "{0} writes to 0x{1:X} ({2}) register of {3}, value 0x{4:X}.", GetAskingCPUEntry().Name, offset, prettyOffset, collectionName, value);
            if(!registerExists)
            {
                this.Log(LogLevel.Warning, "Unhandled write to 0x{0:X} register of {1}, value 0x{2:X}.", offset, collectionName, value);
            }
        }

        public void LogReadAccess(bool registerExists, object value, string collectionName, long offset, object prettyOffset)
        {
            if(!registerExists)
            {
                this.Log(LogLevel.Warning, "Unhandled read from 0x{0:X} register of {1}.", offset, collectionName);
            }
            this.Log(LogLevel.Noisy, "{0} reads from 0x{1:X} ({2}) register of {3}, returned 0x{4:X}.", GetAskingCPUEntry().Name, offset, prettyOffset, collectionName, value);
        }

        public bool TryGetCPUEntry(uint processorNumber, out CPUEntry cpuEntry)
        {
            var exists = cpusByProcessorNumberCache.TryGetValue(processorNumber, out var cpu);
            cpuEntry = exists ? cpuEntries[cpu] : null;
            return exists;
        }

        public bool TryGetCPUEntryForCPU(IARMSingleSecurityStateCPU cpu, out CPUEntry cpuEntry)
        {
            return cpuEntries.TryGetValue(cpu, out cpuEntry);
        }

        public CPUEntry GetCPUEntry(uint processorNumber)
        {
            if(!TryGetCPUEntry(processorNumber, out var cpuEntry))
            {
                throw new RecoverableException($"There is no CPU with the Processor Number {processorNumber}.");
            }
            return cpuEntry;
        }

        public IEnumerable<DoubleWordRegister> BuildInterruptSetEnableRegisters(InterruptId startId, InterruptId endId, string name, Func<CPUEntry> cpuEntryProvider = null)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Config.Enabled |= val,
                valueProviderCallback: irq => irq.Config.Enabled,
                cpuEntryProvider: cpuEntryProvider
            );
        }

        public IEnumerable<DoubleWordRegister> BuildInterruptClearEnableRegisters(InterruptId startId, InterruptId endId, string name, Func<CPUEntry> cpuEntryProvider = null)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Config.Enabled &= !val,
                valueProviderCallback: irq => irq.Config.Enabled,
                cpuEntryProvider: cpuEntryProvider
            );
        }

        public IEnumerable<DoubleWordRegister> BuildInterruptPriorityRegisters(InterruptId startId, InterruptId endId, string name, Func<CPUEntry> cpuEntryProvider = null)
        {
            return BuildInterruptEnumRegisters<InterruptPriority>(startId, endId, name, 4,
                writeCallback: (irq, val) => irq.Config.Priority = val,
                valueProviderCallback: irq => irq.Config.Priority,
                cpuEntryProvider: cpuEntryProvider
            );
        }

        public IEnumerable<DoubleWordRegister> BuildPrivateInterruptTargetsRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptValueRegisters(startId, endId, name, 4,
                valueProviderCallback: _ => GetAskingCPUEntry().targetFieldFlag
            );
        }

        public IEnumerable<DoubleWordRegister> BuildSharedInterruptTargetsRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptValueRegisters(startId, endId, name, 4,
                writeCallback: (irq, val) =>
                    {
                        if(IsAffinityRoutingEnabled(GetAskingCPUEntry()))
                        {
                            this.Log(LogLevel.Warning, "Trying to write ITARGETSR register when Affinity Routing is enabled, write ignored.");
                            return;
                        }
                        var validTargets = legacyCpusAttachedMask & val;
                        ((SharedInterrupt)irq).TargetCPUs = (byte)validTargets;
                        if(validTargets != val)
                        {
                            this.Log(LogLevel.Warning, "Interrupt {0} configured to target an invalid CPU, id: {1}, writes ignored.", irq.Identifier, String.Join(", ", BitHelper.GetSetBits(validTargets ^ val)));
                        }
                    },
                valueProviderCallback: irq =>
                    {
                        if(IsAffinityRoutingEnabled(GetAskingCPUEntry()))
                        {
                            this.Log(LogLevel.Warning, "Trying to read ITARGETSR register when Affinity Routing is enabled, returning 0.");
                            return 0;
                        }
                        return ((SharedInterrupt)irq).TargetCPUs;
                    }
            );
        }

        public IEnumerable<DoubleWordRegister> BuildInterruptConfigurationRegisters(InterruptId startId, InterruptId endId, string name, bool isReadonly = false, Func<CPUEntry> cpuEntryProvider = null)
        {
            Action<Interrupt, InterruptTriggerType> writeCallback = null;
            if(!isReadonly)
            {
                writeCallback = (irq, val) =>
                {
                    irq.State.TriggerType = val;
                    if(val != InterruptTriggerType.LevelSensitive && val != InterruptTriggerType.EdgeTriggered)
                    {
                        this.Log(LogLevel.Error, "Setting an unknown interrupt trigger type, value {0}", val);
                    }
                };
            }
            return BuildInterruptEnumRegisters<InterruptTriggerType>(startId, endId, name, 16,
                writeCallback: writeCallback,
                valueProviderCallback: irq => irq.State.TriggerType,
                cpuEntryProvider: cpuEntryProvider
            );
        }

        public IEnumerable<DoubleWordRegister> BuildInterruptSetActiveRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.State.Active |= val,
                valueProviderCallback: irq => irq.State.Active
            );
        }

        public IEnumerable<DoubleWordRegister> BuildInterruptClearActiveRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.State.Active &= !val,
                valueProviderCallback: irq => irq.State.Active
            );
        }

        public IEnumerable<DoubleWordRegister> BuildInterruptSetPendingRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.State.Pending |= val,
                valueProviderCallback: irq => irq.State.Pending
            );
        }

        public IEnumerable<DoubleWordRegister> BuildInterruptClearPendingRegisters(InterruptId startId, InterruptId endId, string name, Func<CPUEntry> cpuEntryProvider = null)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.State.Pending &= !val,
                valueProviderCallback: irq => irq.State.Pending,
                cpuEntryProvider: cpuEntryProvider
            );
        }

        public IEnumerable<DoubleWordRegister> BuildInterruptGroupRegisters(InterruptId startId, InterruptId endId, string name, Func<CPUEntry> cpuEntryProvider = null)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) => irq.Config.GroupBit = val,
                valueProviderCallback: irq => irq.Config.GroupBit,
                allowAccessWhenNonSecureGroup: false,
                cpuEntryProvider: cpuEntryProvider
            );
        }

        public IEnumerable<DoubleWordRegister> BuildInterruptGroupModifierRegisters(InterruptId startId, InterruptId endId, string name, Func<CPUEntry> cpuEntryProvider = null)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                writeCallback: (irq, val) =>
                    {
                        if(!DisabledSecurity)
                        {
                            irq.Config.GroupModifierBit = val;
                        }
                        else
                        {
                            // The Zephyr uses this field as usual, so the log message isn't a warning
                            this.Log(LogLevel.Debug, "The group modifier register is reserved for the disabled security, write ignored.");
                        }
                    },
                valueProviderCallback: irq => irq.Config.GroupModifierBit,
                allowAccessWhenNonSecureGroup: false,
                cpuEntryProvider: cpuEntryProvider
            );
        }

        public IEnumerable<DoubleWordRegister> BuildPrivateOrSharedPeripheralInterruptStatusRegisters(InterruptId startId, InterruptId endId, string name)
        {
            return BuildInterruptFlagRegisters(startId, endId, name,
                valueProviderCallback: irq => irq.State.Pending
            );
        }

        public void DisconnectAutomaticallyConnectedGPIOOutputs()
        {
            foreach(var connection in automaticallyConnectedGPIOs)
            {
                connection.Disconnect();
            }
            automaticallyConnectedGPIOs.Clear();
        }

        public long PeripheralIdentificationOffset => ArchitectureVersionAtLeast3
            ? (long)RedistributorRegisters.PeripheralIdentification2_v3v4
            : (long)RedistributorRegisters.PeripheralIdentification2_v1v2;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }
        public IEnumerable<IARMSingleSecurityStateCPU> AttachedCPUs => cpuEntries.Keys;
        public InterruptsDecoder IrqsDecoder => irqsDecoder;

        public bool DisabledSecurity
        {
            get => !supportsTwoSecurityStates || disabledSecurity;
            set
            {
                if(!supportsTwoSecurityStates)
                {
                    this.Log(LogLevel.Warning, "Disabling security isn't allowed for a single security GIC, write ignored.");
                    return;
                }
                if(!SetOnTransitionToTrue(ref disabledSecurity, value, "Trying to enable security when it's disabled, write ignored."))
                {
                    return;
                }

                if(groups.Values.Any(group => group.Enabled))
                {
                    this.Log(LogLevel.Warning, "Disabling security when a group of interrupts is enabled.");
                }

                var cpuConfigs = cpuEntries.Values.SelectMany(cpu => cpu.AllPrivateAndSoftwareGeneratedInterruptsConfigs);
                var allInterruptConfigs = cpuConfigs.Concat(sharedInterrupts.Values.Select(irq => irq.Config));
                foreach(var config in allInterruptConfigs)
                {
                    config.GroupModifierBit = false;
                }
            }
        }

        public bool AffinityRoutingEnabledSecure
        {
            get => affinityRoutingEnabledSecure;
            set
            {
                if(!SetOnTransitionToTrue(ref affinityRoutingEnabledSecure, value, "Trying to disable affinity routing for secure state when it's enabled, write ignored."))
                {
                    return;
                }
                // According to the specification the value for secure state overrides the value for non-secure state.
                affinityRoutingEnabledNonSecure = true;

                if(groups.Values.Any(group => group.Enabled))
                {
                    this.Log(LogLevel.Warning, "Enabling affinity routing for secure state when a group of interrupts is enabled.");
                }
            }
        }

        public bool AffinityRoutingEnabledNonSecure
        {
            get => affinityRoutingEnabledNonSecure;
            set
            {
                if(!SetOnTransitionToTrue(ref affinityRoutingEnabledNonSecure, value, "Trying to disable affinity routing for non-secure state when it's enabled, write ignored."))
                {
                    return;
                }
                if(groups[GroupType.Group1NonSecure].Enabled)
                {
                    this.Log(LogLevel.Warning, "Enabling affinity routing for non-secure state when the Group 1 Non-secure is enabled.");
                }
            }
        }

        public bool AffinityRoutingEnabledBoth => DisabledSecurity || AffinityRoutingEnabledSecure && AffinityRoutingEnabledNonSecure;

        /// <summary>
        /// Setting this property to true will causes all interrupts to be reported to a core with lowest ID, which configuration allows it to take.
        ///
        /// This is mostly for debugging purposes.
        /// It allows to predict a core (in a multi-core setup) to handle the given interrupt making it easier to debug.
        /// </summary>
        public bool ForceLowestIdCpuAsInterruptTarget { get; set; }

        public ARM_GenericInterruptControllerVersion ArchitectureVersion { get; }
        public bool ArchitectureVersionAtLeast3 => ArchitectureVersion >= ARM_GenericInterruptControllerVersion.GICv3;
        public uint CPUInterfaceProductIdentifier { get; set; } = DefaultCPUInterfaceProductIdentifier;
        public uint DistributorProductIdentifier { get; set; } = DefaultDistributorProductIdentifier;
        public byte CPUInterfaceRevision { get; set; } = DefaultRevisionNumber;
        public uint CPUInterfaceImplementer { get; set; } = DefaultImplementerIdentification;
        public byte DistributorVariant { get; set; } = DefaultVariantNumber;
        public byte DistributorRevision { get; set; } = DefaultRevisionNumber;
        public uint DistributorImplementer { get; set; } = DefaultImplementerIdentification;
        public uint RedistributorProductIdentifier { get; set; } = DefaultRedistributorProductIdentifier;
        public byte RedistributorVariant { get; set; } = DefaultVariantNumber;
        public byte RedistributorRevision { get; set; } = DefaultRevisionNumber;
        public uint RedistributorImplementer { get; set; } = DefaultImplementerIdentification;

        public event Action<IARMSingleSecurityStateCPU> CPUAttached;

        private void OnPrivateInterrupt(CPUEntry cpu, int id, bool value)
        {
            var irqId = new InterruptId((uint)id);
            if(!irqsDecoder.IsPrivatePeripheral(irqId))
            {
                this.Log(LogLevel.Warning, "Generated interrupt isn't a Private Peripheral Interrupt, interrupt identifier: {0}", irqId);
                return;
            }
            this.Log(LogLevel.Debug, "Setting Private Peripheral Interrupt #{0} signal to {1} for {2}", irqId, value, cpu.Name);
            LockExecuteAndUpdate(() =>
                cpu.PrivatePeripheralInterrupts[irqId].State.AssertAsPending(value)
            );
        }

        private void OnSoftwareGeneratedInterrupt(CPUEntry requestingCPU, SoftwareGeneratedInterruptRequest request)
        {
            // The GIC uses a groups configuration at the moment of SGI request to choose correct target CPUs.
            var irqId = request.InterruptId;
            DebugHelper.Assert(irqsDecoder.IsSoftwareGenerated(irqId), $"Invalid interrupt identifier ({irqId}), it doesn't indicate an SGI.");
            this.Log(LogLevel.Noisy, "The {0} requests an SGI with id {1}.", requestingCPU.Name, irqId);

            var targetCPUs = new List<CPUEntry>();
            switch(request.TargetCPUsType)
            {
                case SoftwareGeneratedInterruptRequest.TargetType.Loopback:
                    targetCPUs.Add(requestingCPU);
                    break;
                case SoftwareGeneratedInterruptRequest.TargetType.AllCPUs:
                    targetCPUs.AddRange(cpuEntries.Values.Where(cpu => cpu != requestingCPU));
                    break;
                case SoftwareGeneratedInterruptRequest.TargetType.TargetList:
                    foreach(var affinity in request.TargetsList)
                    {
                        if(TryGetCPUEntry(affinity.AllLevels, out var targetCPU))
                        {
                            targetCPUs.Add(targetCPU);
                        }
                        else
                        {
                            this.Log(LogLevel.Debug, "There is no target CPU with the affinity {0} for an SGI request.", affinity);
                        }
                    }
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unknown Software Generated Interrupt target type {0}", request.TargetCPUsType);
                    return;
            }

            if(IsAffinityRoutingEnabled(requestingCPU))
            {
                OnSGIAffinityRouting(requestingCPU, targetCPUs, request);
            }
            else
            {
                OnSGILegacyRouting(requestingCPU, targetCPUs, request);
            }
        }

        private void OnSGIAffinityRouting(CPUEntry requestingCPU, List<CPUEntry> targetCPUs, SoftwareGeneratedInterruptRequest request)
        {
            var isOtherSecurityState = GroupTypeToIsStateSecure(request.TargetGroup) != requestingCPU.IsStateSecure;
            if(isOtherSecurityState && !AffinityRoutingEnabledBoth)
            {
                this.Log(LogLevel.Debug, "Generating SGIs for the other Security state is only supported when affinity rouing is enabled for both Security states.");
                return;
            }
            var irqId = request.InterruptId;
            foreach(var target in targetCPUs)
            {
                var interrupt = target.SoftwareGeneratedInterruptsUnknownRequester[irqId];
                this.Log(LogLevel.Noisy, "Trying to request interrupt for target {0}, interrupt group type {1}, request group {2}, access in {3} state.",
                    target.Name, interrupt.Config.GroupType, request.TargetGroup, requestingCPU.IsStateSecure ? "secure" : "non-secure");

                if(ShouldAssertSGIAffinityRouting(requestingCPU, request, interrupt, target.NonSecureSGIAccess[(uint)irqId]))
                {
                    this.Log(LogLevel.Noisy, "Setting Software Generated Interrupt #{0} signal for {1}", irqId, target.Name);
                    // SGIs are triggered by a register access so the method is already called inside a lock.
                    interrupt.State.AssertAsPending(true);
                }
                else
                {
                    this.Log(LogLevel.Noisy, "SGI #{0} not forwarded for {1}.", irqId, target.Name);
                }
            }
        }

        private bool ShouldAssertSGIAffinityRouting(CPUEntry requestingCPU, SoftwareGeneratedInterruptRequest request, SoftwareGeneratedInterrupt interrupt,
            NonSecureAccess targetNonSecureAccess)
        {
            if(requestingCPU.IsStateSecure)
            {
                return request.TargetGroup == interrupt.Config.GroupType ||
                    request.TargetGroup == GroupType.Group1Secure && interrupt.Config.GroupType == GroupType.Group0 && DisabledSecurity;
            }
            if(request.TargetGroup == interrupt.Config.GroupType || interrupt.Config.GroupType == GroupType.Group0)
            {
                return request.TargetGroup == GroupType.Group1NonSecure || DisabledSecurity || NonSecureAccessPermitsGroup(targetNonSecureAccess, request.TargetGroup);
            }
            return request.TargetGroup == GroupType.Group1NonSecure && interrupt.Config.GroupType == GroupType.Group1Secure && NonSecureAccessPermitsGroup(targetNonSecureAccess, request.TargetGroup);
        }

        private void OnSGILegacyRouting(CPUEntry requestingCPU, List<CPUEntry> targetCPUs, SoftwareGeneratedInterruptRequest request)
        {
            var irqId = request.InterruptId;
            foreach(var target in targetCPUs)
            {
                if(!target.SoftwareGeneratedInterruptsLegacyRequester.TryGetValue(requestingCPU, out var interrupts))
                {
                    this.Log(LogLevel.Warning, "The GIC doesn't support requesting an SGI from the CPU with the Processor Number ({0}) greater than {1}, request ignored.", requestingCPU.processorNumber, CPUsCountLegacySupport - 1);
                    return;
                }

                var interrupt = interrupts[irqId];
                this.Log(LogLevel.Noisy, "Trying to request interrupt for target {0}, interrupt group type (GICD_IGROUPRn) {1}, request group (NSATT) {2}, access in {3} state.",
                    target.Name, interrupt.Config.GroupType, request.TargetGroup, requestingCPU.IsStateSecure ? "secure" : "non-secure");

                if(ShouldAssertSGILegacyRouting(requestingCPU, request, interrupt))
                {
                    this.Log(LogLevel.Noisy, "Setting Software Generated Interrupt #{0} signal for {1}", irqId, target.Name);
                    // SGIs are triggered by a register access so the method is already called inside a lock.
                    interrupt.State.AssertAsPending(true);
                }
                else
                {
                    this.Log(LogLevel.Noisy, "SGI #{0} not forwarded for {1}.", irqId, target.Name);
                }
            }
        }

        private bool ShouldAssertSGILegacyRouting(CPUEntry requestingCPU, SoftwareGeneratedInterruptRequest request, SoftwareGeneratedInterrupt interrupt)
        {
            // See: "SGI generation when the GIC implements the Security Extensions" for the truth table
            // Without the Security Extension, this is irrelevant
            if(DisabledSecurity)
            {
                return true;
            }
            // If GICD_SGIR write is done in non-secure mode, only Group1 is forwarded
            else if(!requestingCPU.IsStateSecure)
            {
                return interrupt.Config.GroupType != GroupType.Group0;
            }
            // According to the specification, interrupt with a group different than a target group is just ignored, if GICD_SGIR write is done in secure mode.
            else if(requestingCPU.IsStateSecure && interrupt.Config.GroupType == request.TargetGroup)
            {
                return true;
            }

            return false;
        }

        private bool NonSecureAccessPermitsGroup(NonSecureAccess access, GroupType type)
        {
            // To maintain the principle that as the value increases additional accesses are permitted
            // Arm strongly recommends that implementations treat the reserved value as 0b10.
            // That's why we compare `access` using the >= operator.
            switch(type)
            {
                case GroupType.Group1NonSecure:
                    return true;
                case GroupType.Group1Secure:
                    return access >= NonSecureAccess.BothGroupsPermitted;
                case GroupType.Group0:
                    return access >= NonSecureAccess.SecureGroup0Permitted;
                default:
                    throw new ArgumentOutOfRangeException($"There is no valid GroupType for value: {type}.");
            }
        }

        private bool GroupTypeToIsStateSecure(GroupType type)
        {
            switch(type)
            {
                case GroupType.Group0:
                case GroupType.Group1Secure:
                    return true;
                case GroupType.Group1NonSecure:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException($"There is no valid GroupType for value: {type}.");
            }
        }

        private GroupType GetGroup1ForSecurityState(bool isSecure)
        {
            return isSecure
                // When System register access is not enabled for Secure EL1, or when GICD_CTLR.DS == 1,
                // the Distributor treats Secure Group 1 interrupts as Group 0 interrupts
                ? DisabledSecurity ? GroupType.Group0 : GroupType.Group1Secure
                : GroupType.Group1NonSecure;
        }

        // The GIC uses the latest CPU state and the latest groups configuration to choose a correct interrupt signal to assert.
        private void OnExecutionModeChanged(CPUEntry cpu)
        {
            lock(locker)
            {
                cpu.UpdateSignals();
            }
        }

        // Returns the best pending interrupt for given candidates.
        // If null is returned, that means there's no pending interrupt.
        private T FindBestPendingInterrupt<T>(IEnumerable<T> pendingCandidates) where T : Interrupt
        {
            var bestPending = pendingCandidates.FirstOrDefault();
            foreach(var irq in pendingCandidates.Skip(1))
            {
                if(irq.Config.Priority < bestPending.Config.Priority)
                {
                    bestPending = irq;
                    if(bestPending.Config.Priority == InterruptPriority.Highest)
                    {
                        break;
                    }
                }
            }
            return bestPending;
        }

        private void UpdateBestPendingInterrupts()
        {
            foreach(var cpu in cpuEntries.Values)
            {
                if(cpu.VirtualCPUInterfaceEnabled)
                {
                    cpu.BestPendingVirtual = FindBestPendingInterrupt(GetAllPendingCandidateVirtualInterrupts(cpu));
                }
                else
                {
                    cpu.BestPendingVirtual = null;
                }
                cpu.BestPending = FindBestPendingInterrupt(GetAllPendingCandidateInterrupts(cpu));
            }
        }

        private IEnumerable<Interrupt> GetSharedInterruptsTargetingCPU(CPUEntry cpu)
        {
            IEnumerable<SharedInterrupt> interrupts = sharedInterrupts.Values;
            if(cpuEntries.Count == 1)
            {
                // If there is only one CPU all interrupts target it.
                return interrupts;
            }
            if(!IsAffinityRoutingEnabled(cpu))
            {
                return interrupts.Where(irq =>
                    irq.IsLegacyRoutingTargetingCPU(cpu) && (!ForceLowestIdCpuAsInterruptTarget || irq.IsLowestLegacyRoutingTargettedCPU(cpu))
                );
            }

            return interrupts.Where(irq =>
                irq.IsAffinityRoutingTargetingCPU(cpu) && (!ForceLowestIdCpuAsInterruptTarget || irq.IsLowestAffinityRoutingTargettedCPU(cpu, this))
            );
        }

        private IEnumerable<Interrupt> GetAllEnabledInterrupts(CPUEntry cpu)
        {
            var enabledGroups = groups.Keys.Where(type => groups[type].Enabled && cpu.Groups.Physical[type].Enabled).ToArray();
            IEnumerable<SharedInterrupt> filteredSharedInterrupts = sharedInterrupts.Values;
            return cpu.AllPrivateAndSoftwareGeneratedInterrupts
                .Concat(GetSharedInterruptsTargetingCPU(cpu))
                .Where(irq => irq.Config.Enabled && enabledGroups.Contains(irq.Config.GroupType));
        }

        private IEnumerable<Interrupt> GetAllInterrupts(CPUEntry cpu)
        {
            return cpu.AllPrivateAndSoftwareGeneratedInterrupts.Concat(sharedInterrupts.Values);
        }

        private Func<Interrupt, bool> InterruptPriorityFilter(InterruptPriority priorityMask, InterruptPriority runningPriority)
        {
            return irq => irq.State.Pending && !irq.State.Active && irq.Config.Priority < priorityMask && irq.Config.Priority < runningPriority;
        }

        private IEnumerable<Interrupt> GetAllPendingCandidateInterrupts(CPUEntry cpu)
        {
            var filter = InterruptPriorityFilter(cpu.PhysicalPriorityMask, cpu.RunningInterrupts.PhysicalPriority);
            return GetAllEnabledInterrupts(cpu).Where(filter);
        }

        private IEnumerable<VirtualInterrupt> GetAllPendingCandidateVirtualInterrupts(CPUEntry cpu)
        {
            var enabledGroups = groups.Keys.Where(type => cpu.Groups.Virtual[type].Enabled).ToArray();
            var filter = InterruptPriorityFilter(cpu.VirtualPriorityMask, cpu.RunningInterrupts.VirtualPriority);
            return cpu
                .VirtualInterrupts
                .Where(irq => enabledGroups.Contains(irq.Config.GroupType) && filter(irq));
        }

        private Dictionary<long, DoubleWordRegister> BuildDistributorDoubleWordRegistersMap()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)DistributorRegisters.ControllerType, new DoubleWordRegister(this)
                    .WithValueField(27, 5, name: "SharedPeripheralInterruptsExtendedCount",
                        valueProviderCallback: _ => irqsDecoder.SharedPeripheralExtendedCount / 32 - 1
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
                        valueProviderCallback: _ => irqsDecoder.IdentifierBits - 1
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
                        valueProviderCallback: _ => !DisabledSecurity
                    )
                    .WithFlag(9, name: "NonMaskableInterruptSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(8, name: "SharedPeripheralInterruptsExtendedSupport",
                        valueProviderCallback: _ => false
                    )
                    .WithValueField(5, 3, name: "LegacyCpusCount",
                        valueProviderCallback: _ => (ulong)BitHelper.GetSetBits(legacyCpusAttachedMask).Count - 1
                    )
                    .WithValueField(0, 5, name: "SharedPeripheralInterruptsCount",
                        valueProviderCallback: _ => ((uint)irqsDecoder.SharedPeripheralLast + 1) / 32 - 1
                    )
                },
                {(long)DistributorRegisters.ImplementerIdentification, new DoubleWordRegister(this)
                    .WithValueField(24, 8, FieldMode.Read, valueProviderCallback: _ => DistributorProductIdentifier, name: "ProductIdentifier")
                    .WithReservedBits(20, 4)
                    .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => DistributorVariant, name: "VariantNumber")
                    .WithValueField(12, 4, FieldMode.Read, valueProviderCallback: _ => DistributorRevision, name: "RevisionNumber")
                    .WithValueField(0, 12, FieldMode.Read, valueProviderCallback: _ => DistributorImplementer, name: "ImplementerIdentification")
                },
                {(long)DistributorRegisters.SoftwareGeneratedInterruptControl, new DoubleWordRegister(this)
                    .WithReservedBits(26, 6)
                    .WithEnumField<DoubleWordRegister, SoftwareGeneratedInterruptRequest.TargetType>(24, 2, out var type, FieldMode.Write, name: "TargetListFilter")
                    .WithValueField(16, 8, out var targetList, FieldMode.Write, name: "TargetsList")
                    .WithFlag(15, out var group, FieldMode.Write, name: "GroupFilterSecureAccess")
                    .WithReservedBits(4, 10)
                    .WithValueField(0, 4, out var id, FieldMode.Write, name: "SoftwareGeneratedInterruptIdentifier")
                    .WithWriteCallback((_, __) =>
                    {
                        var list = new Affinity[]{};
                        if(type.Value == SoftwareGeneratedInterruptRequest.TargetType.TargetList)
                        {
                            list = BitHelper.GetSetBits(targetList.Value).Select(n => new Affinity((byte)n)).ToArray();
                        }
                        var interrupt =  new SoftwareGeneratedInterruptRequest(type.Value, list, group.Value ? GroupType.Group1 : GroupType.Group0, new InterruptId((uint)id.Value));
                        OnSoftwareGeneratedInterrupt(GetAskingCPUEntry(), interrupt);
                    })
                },
                {PeripheralIdentificationOffset, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithEnumField<DoubleWordRegister, ARM_GenericInterruptControllerVersion>(4, 4, FieldMode.Read, name: "ArchitectureVersion",
                        valueProviderCallback: _ => ArchitectureVersion
                    )
                    .WithTag("ImplementationDefinedIdentificator", 0, 4)
                }
            };

            // All BuildInterrupt*Registers methods create registers with respect for Security State
            // There is no separate view (RegistersCollection) for this kind of registers, because their layout are independent of Security State
            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptSetEnable_0,
                BuildInterruptSetEnableRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptSetEnable")
            );

            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptClearEnable_0,
                BuildInterruptClearEnableRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptClearEnable")
            );

            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptPriority_0,
                BuildInterruptPriorityRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptPriority")
            );

            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptProcessorTargets_0,
                BuildPrivateInterruptTargetsRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.PrivatePeripheralLast, "InterruptProcessorTargets")
            );
            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptProcessorTargets_8,
                BuildSharedInterruptTargetsRegisters(irqsDecoder.SharedPeripheralFirst, irqsDecoder.SharedPeripheralLast, "InterruptProcessorTargets")
            );

            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptConfiguration_0,
                BuildInterruptConfigurationRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SoftwareGeneratedLast, "InterruptConfiguration", isReadonly: true)
            );
            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptConfiguration_1,
                BuildInterruptConfigurationRegisters(irqsDecoder.PrivatePeripheralFirst, irqsDecoder.SharedPeripheralLast, "InterruptConfiguration")
            );

            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptSetActive_0,
                BuildInterruptSetActiveRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptSetActive")
            );

            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptClearActive_0,
                BuildInterruptClearActiveRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptClearActive")
            );

            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptSetPending_0,
                BuildInterruptSetPendingRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptSetPending")
            );

            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptClearPending_0,
                BuildInterruptClearPendingRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptClearPending")
            );

            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptGroup_0,
                BuildInterruptGroupRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptGroup")
            );

            // The range between 0xD00-0xDFC is implementation defined for GICv1 and GICv2.
            if(ArchitectureVersionAtLeast3)
            {
                Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptGroupModifier_0_PPIStatus,
                    BuildInterruptGroupModifierRegisters(irqsDecoder.SoftwareGeneratedFirst, irqsDecoder.SharedPeripheralLast, "InterruptGroupModifier")
                );
            }
            else
            {
                // See e.g. https://developer.arm.com/documentation/ddi0416/b/programmers-model/distributor-register-descriptions and https://developer.arm.com/documentation/ddi0471/b/programmers-model/distributor-register-summary
                Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptGroupModifier_0_PPIStatus,
                    BuildPrivateOrSharedPeripheralInterruptStatusRegisters(irqsDecoder.PrivatePeripheralFirst, irqsDecoder.PrivatePeripheralLast, "PPI Status")
                );

                Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptGroupModifier_1_SPIStatus_0,
                    BuildPrivateOrSharedPeripheralInterruptStatusRegisters(irqsDecoder.SharedPeripheralFirst, irqsDecoder.SharedPeripheralLast, "SPI Status")
                );
            }

            return registersMap;
        }

        private Dictionary<long, QuadWordRegister> BuildDistributorQuadWordRegistersMap()
        {
            var registersMap = new Dictionary<long, QuadWordRegister>();
            Utils.AddRegistersAtOffset(registersMap, (long)DistributorRegisters.InterruptRouting_0,
                BuildInterruptRoutingRegisters(irqsDecoder.SharedPeripheralFirst, irqsDecoder.SharedPeripheralLast)
            );
            return registersMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildDistributorRegistersMapSecurityView(bool accessForDisabledSecurity, SecurityState? securityStateAccess = null)
        {
            var controlRegister = new DoubleWordRegister(this)
                .WithFlag(31, FieldMode.Read, name: "RegisterWritePending", valueProviderCallback: _ => false);
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)DistributorRegisters.Control, controlRegister}
            };

            if(accessForDisabledSecurity)
            {
                controlRegister
                    .WithReservedBits(9, 22)
                    .WithTaggedFlag("nASSGIreq", 8) // Requires FEAT_GICv4p1 support
                    .WithFlag(7, FieldMode.Read, name: "Enable1ofNWakeup", valueProviderCallback: _ => false) // There is no support for waking up
                    .WithFlag(6, FieldMode.Read, name: "DisableSecurity", valueProviderCallback: _ => true)
                    .WithReservedBits(5, 1)
                    .WithFlag(4, name: "EnableAffinityRouting",
                        writeCallback: (_, value) => AffinityRoutingEnabledSecure = value,
                        valueProviderCallback: _ => AffinityRoutingEnabledSecure
                    )
                    .WithReservedBits(2, 2)
                    .WithFlag(1, name: "EnableGroup1",
                        writeCallback: (_, val) => groups[GroupType.Group1].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group1].Enabled
                    )
                    .WithFlag(0, name: "EnableGroup0",
                        writeCallback: (_, val) => groups[GroupType.Group0].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group0].Enabled
                    );
            }
            else if(securityStateAccess == SecurityState.Secure)
            {
                controlRegister
                    .WithReservedBits(8, 23)
                    .WithFlag(7, FieldMode.Read, name: "Enable1ofNWakeUp", valueProviderCallback: _ => false) // There is no support for waking up
                    .WithFlag(6, name: "DisableSecurity",
                        writeCallback: (_, val) => DisabledSecurity = val,
                        valueProviderCallback: _ => DisabledSecurity
                    )
                    .WithFlag(5, name: "EnableAffinityRoutingNonSecure",
                        writeCallback: (_, value) => AffinityRoutingEnabledNonSecure = value,
                        valueProviderCallback: _ => AffinityRoutingEnabledNonSecure
                    )
                    .WithFlag(4, name: "EnableAffinityRoutingSecure",
                        writeCallback: (_, value) => AffinityRoutingEnabledSecure = value,
                        valueProviderCallback: _ => AffinityRoutingEnabledSecure
                    )
                    .WithReservedBits(3, 1)
                    .WithFlag(2, name: "EnableGroup1Secure",
                        writeCallback: (_, val) => groups[GroupType.Group1Secure].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group1Secure].Enabled
                    )
                    .WithFlag(1, name: "EnableGroup1NonSecure",
                        writeCallback: (_, val) => groups[GroupType.Group1NonSecure].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group1NonSecure].Enabled
                    )
                    .WithFlag(0, name: "EnableGroup0",
                        writeCallback: (_, val) => groups[GroupType.Group0].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group0].Enabled
                    );
                registersMap.Add((long)DistributorRegisters.NonSecureAccessControl_0, new DoubleWordRegister(this)
                    .WithEnumFields<DoubleWordRegister, NonSecureAccess>(0, 2, 16, name: "NS_access",
                        writeCallback: (i, _, val) =>
                        {
                            var cpu = GetAskingCPUEntry();
                            if(IsAffinityRoutingEnabled(cpu))
                            {
                                cpu.NonSecureSGIAccess[i] = val;
                            }
                        },
                        valueProviderCallback: (i, _) =>
                        {
                            var cpu = GetAskingCPUEntry();
                            return IsAffinityRoutingEnabled(cpu) ? (NonSecureAccess)0 : cpu.NonSecureSGIAccess[i];
                        }
                    )
                    // Those could be emitted in valueProvider/writeCallback instead,
                    // but we don't want to emit the same warning 16 times per access.
                    .WithWriteCallback((_, __) =>
                    {
                        var cpu = GetAskingCPUEntry();
                        if(IsAffinityRoutingEnabled(cpu))
                        {
                            this.Log(LogLevel.Warning, "Tried to write to GICD_NSACR0 when affinity routing is enabled. Access ignored, use GICR_NSACR instead.");
                        }
                    })
                    .WithReadCallback((_, __) =>
                    {
                        var cpu = GetAskingCPUEntry();
                        if(IsAffinityRoutingEnabled(cpu))
                        {
                            this.Log(LogLevel.Warning, "Tried to read from GICD_NSACR0 when affinity routing is enabled. Access ignored, use GICR_NSACR instead.");
                        }
                    })
                );
                // These registers do not support PPIs, therefore GICD_NSACR1 is RAZ/WI
                registersMap.Add((long)DistributorRegisters.NonSecureAccessControl_0 + 4, new DoubleWordRegister(this)
                    .WithValueFields(0, 2, 16, FieldMode.Read, name: "NS_access", valueProviderCallback: (_, __) => 0)
                );
                for(var j = 2; j < 64; ++j)
                {
                    var i = j;
                    registersMap.Add((long)DistributorRegisters.NonSecureAccessControl_0 + 4 * i, new DoubleWordRegister(this)
                        .WithValueFields(0, 2, 16, name: "NS_access", valueProviderCallback: (_, __) => 0)
                        .WithReadCallback((_, __) => this.Log(LogLevel.Warning, "GICD_NSACR{0} is not implemented yet", i))
                        .WithWriteCallback((_, __) => this.Log(LogLevel.Warning, "GICD_NSACR{0} is not implemented yet", i))
                    );
                }
            }
            else
            {
                controlRegister
                    .WithReservedBits(5, 26)
                    .WithFlag(4, name: "EnableAffinityRoutingNonSecure",
                        writeCallback: (_, value) => AffinityRoutingEnabledNonSecure = value,
                        valueProviderCallback: _ => AffinityRoutingEnabledNonSecure
                    )
                    .WithReservedBits(2, 2)
                    .WithFlag(1, name: "EnableGroup1NonSecureAlias",
                        writeCallback: (_, val) => groups[GroupType.Group1NonSecure].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group1NonSecure].Enabled
                    )
                    .WithFlag(0, name: "EnableGroup1NonSecureAlias",
                        writeCallback: (_, val) => groups[GroupType.Group1NonSecure].Enabled = val,
                        valueProviderCallback: _ => groups[GroupType.Group1NonSecure].Enabled
                    );
            }

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
                        valueProviderCallback: _ => GetAskingCPUEntry().Groups[GroupType.Group0].Enabled,
                        writeCallback: (_, val) => GetAskingCPUEntry().Groups[GroupType.Group0].Enabled = val
                    )
                },
                {(long)CPUInterfaceSystemRegisters.GroupEnable1, new QuadWordRegister(this)
                    .WithReservedBits(1, 63)
                    .WithFlag(0, name: "EnableGroup1",
                        valueProviderCallback: _ => GetAskingCPUEntry().GetGroupForRegisterSecurityAgnostic(GroupTypeSecurityAgnostic.Group1).Enabled,
                        writeCallback: (_, val) => GetAskingCPUEntry().GetGroupForRegisterSecurityAgnostic(GroupTypeSecurityAgnostic.Group1).Enabled = val
                    )
                },
                {(long)CPUInterfaceSystemRegisters.GroupEnable1EL3, new QuadWordRegister(this)
                    .WithReservedBits(2, 62)
                    .WithFlag(1, name: "EnableGroup1S",
                        valueProviderCallback: _ => GetAskingCPUEntry().Groups[GroupType.Group1Secure].Enabled,
                        writeCallback: (_, val) => GetAskingCPUEntry().Groups[GroupType.Group1Secure].Enabled = val
                    )
                    .WithFlag(0, name: "EnableGroup1NS",
                        valueProviderCallback: _ => GetAskingCPUEntry().Groups[GroupType.Group1NonSecure].Enabled,
                        writeCallback: (_, val) => GetAskingCPUEntry().Groups[GroupType.Group1NonSecure].Enabled = val
                    )
                },
                {(long)CPUInterfaceSystemRegisters.RunningPriority, new QuadWordRegister(this)
                    .WithTaggedFlag("PriorityFromNonMaskableInterrupt", 63) // Requires FEAT_GICv3_NMI extension
                    .WithTaggedFlag("PriorityFromNonSecureNonMaskableInterrupt", 62) // Requires FEAT_GICv3_NMI extension
                    .WithReservedBits(8, 54)
                    .WithEnumField<QuadWordRegister, InterruptPriority>(0, 8, FieldMode.Read, name: "RunningPriority",
                        valueProviderCallback: _ => GetAskingCPUEntry().RunningInterrupts.Priority
                    )
                },
                {(long)CPUInterfaceSystemRegisters.PriorityMask, new QuadWordRegister(this)
                    .WithReservedBits(8, 56)
                    .WithEnumField<QuadWordRegister, InterruptPriority>(0, 8, name: "PriorityMask",
                        writeCallback: (_, val) => GetAskingCPUEntry().PriorityMask = val,
                        valueProviderCallback: _ => GetAskingCPUEntry().PriorityMask
                    )
                },
                {(long)CPUInterfaceSystemRegisters.InterruptAcknowledgeGroup0,
                    BuildInterruptAcknowledgeRegister(new QuadWordRegister(this), 64, "InterruptAcknowledgeGroup0", () => GroupTypeSecurityAgnostic.Group0, false)
                },
                {(long)CPUInterfaceSystemRegisters.InterruptAcknowledgeGroup1,
                    BuildInterruptAcknowledgeRegister(new QuadWordRegister(this), 64, "InterruptAcknowledgeGroup1", () => GroupTypeSecurityAgnostic.Group1, false)
                },
                {(long)CPUInterfaceSystemRegisters.InterruptDeactivate,
                    BuildInterruptDeactivateOrInterruptEndRegister(new QuadWordRegister(this), 64, "InterruptDeactivate", null,
                        useCPUIdentifier: false, isDeactivateRegister: true)
                },
                {(long)CPUInterfaceSystemRegisters.InterruptEndGroup0,
                    BuildInterruptDeactivateOrInterruptEndRegister(new QuadWordRegister(this), 64, "InterruptEndGroup0", () => GroupTypeSecurityAgnostic.Group0,
                        useCPUIdentifier: false, isDeactivateRegister: false)
                },
                {(long)CPUInterfaceSystemRegisters.InterruptEndGroup1,
                    BuildInterruptDeactivateOrInterruptEndRegister(new QuadWordRegister(this), 64, "InterruptEndGroup1", () => GroupTypeSecurityAgnostic.Group1,
                        useCPUIdentifier: false, isDeactivateRegister: false)
                },
                {(long)CPUInterfaceSystemRegisters.ControlEL1, new QuadWordRegister(this)
                    .WithReservedBits(20, 44)
                    .WithTaggedFlag("ExtendedINTIDRange", 19)
                    .WithTaggedFlag("RangeSelectorSupport", 18)
                    .WithReservedBits(16, 2)
                    .WithTaggedFlag("Affinity3Valid", 15)
                    .WithTaggedFlag("SEISupport", 14)
                    .WithTag("Identifier bits", 11, 3)
                    .WithTag("PriorityBits", 8, 3)
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("PriorityMaskHintEnable", 6)
                    .WithReservedBits(2, 4)
                    .WithFlag(1, name: "EndOfInterruptMode",
                        writeCallback: (_, val) => GetAskingCPUEntry().EndOfInterruptModeEL1 = (EndOfInterruptModes)(val ? 1 : 0),
                        valueProviderCallback: _ => GetAskingCPUEntry().EndOfInterruptModeEL1 == EndOfInterruptModes.PriorityDropOnly
                    )
                    .WithTaggedFlag("CommonBinaryPointRegister", 0)
                },
                {(long)CPUInterfaceSystemRegisters.ControlEL3, new QuadWordRegister(this)
                    .WithReservedBits(20, 44)
                    .WithTaggedFlag("ExtendedINTIDRange", 19)
                    .WithTaggedFlag("RangeSelectorSupport", 18)
                    .WithTaggedFlag("DisableSecurityNotSupported", 17)
                    .WithReservedBits(16, 1)
                    .WithTaggedFlag("Affinity3Valid", 15)
                    .WithTaggedFlag("SEISupport", 14)
                    .WithTag("Identifier bits", 11, 3)
                    .WithTag("PriorityBits", 8, 3)
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("PriorityMaskHintEnable", 6)
                    .WithTaggedFlag("RoutingModifier", 5)
                    .WithFlag(4, name: "EndOfInterruptModeEL1NonSecure",
                        writeCallback: (_, val) => GetAskingCPUEntry().EndOfInterruptModeEL1NonSecure = (EndOfInterruptModes)(val ? 1 : 0),
                        valueProviderCallback: _ => GetAskingCPUEntry().EndOfInterruptModeEL1NonSecure == EndOfInterruptModes.PriorityDropOnly
                    )
                    .WithFlag(3, name: "EndOfInterruptModeEL1Secure",
                        writeCallback: (_, val) => GetAskingCPUEntry().EndOfInterruptModeEL1Secure = (EndOfInterruptModes)(val ? 1 : 0),
                        valueProviderCallback: _ => GetAskingCPUEntry().EndOfInterruptModeEL1Secure == EndOfInterruptModes.PriorityDropOnly
                    )
                    .WithFlag(2, name: "EndOfInterruptModeEL3",
                        writeCallback: (_, val) => GetAskingCPUEntryWithTwoSecurityStates().EndOfInterruptModeEL3 = (EndOfInterruptModes)(val ? 1 : 0),
                        valueProviderCallback: _ => GetAskingCPUEntryWithTwoSecurityStates().EndOfInterruptModeEL3 == EndOfInterruptModes.PriorityDropOnly
                    )
                    .WithTaggedFlag("CommonBinaryPointRegisterEL1NonSecure", 1)
                    .WithTaggedFlag("CommonBinaryPointRegisterEL1Secure", 0)
                },
                {(long)CPUInterfaceSystemRegisters.HypControl, new QuadWordRegister(this)
                    .WithReservedBits(32, 32)
                    .WithValueField(27, 5, FieldMode.Read, name: "EOIcount",
                        valueProviderCallback: _ => GetAskingCPUEntry().EOICount
                    )
                    .WithReservedBits(16, 11)
                    .WithTaggedFlag("DVIM", 15) // Reserved when ICH_VTR_EL2.DVIM = 0
                    .WithTaggedFlag("TDIR", 14) // Reserved when FEAT_GICv3_TDIR is not implemented
                    .WithTaggedFlag("TSEI", 13)
                    .WithTaggedFlag("TALL1", 12)
                    .WithTaggedFlag("TALL0", 11)
                    .WithTaggedFlag("TC", 10)
                    .WithReservedBits(9, 1)
                    .WithTaggedFlag("vSGIEOICount", 8) // Reserved when FEAT_GICv4p1 is not implemented
                    .WithTaggedFlag("VGrp0DIE", 7)
                    .WithTaggedFlag("VGrp1EIE", 6)
                    .WithTaggedFlag("VGrp0DIE", 5)
                    .WithTaggedFlag("VGrp0EIE", 4)
                    .WithTaggedFlag("NPIE", 3)
                    .WithTaggedFlag("LRENPIE", 2)
                    .WithTaggedFlag("UIE", 1)
                    .WithFlag(0, name: "En",
                        valueProviderCallback: _ => GetAskingCPUEntry().VirtualCPUInterfaceEnabled,
                        writeCallback: (_, val) => GetAskingCPUEntry().VirtualCPUInterfaceEnabled = val
                    )
                },
                {(long)CPUInterfaceSystemRegisters.VGICType, new QuadWordRegister(this)
                    .WithReservedBits(32, 32)
                    .WithValueField(29, 3, FieldMode.Read, name: "PRIbits",
                        valueProviderCallback: _ => VirtualPriorityBits - 1
                    )
                    .WithTag("PREbits", 26, 3)
                    .WithTag("IDbits", 23, 3)
                    .WithTaggedFlag("SEIS", 22)
                    .WithTaggedFlag("A3V", 21)
                    .WithTaggedFlag("nV4", 20)
                    .WithTaggedFlag("TDS", 19)
                    .WithTaggedFlag("DVIM", 18)
                    .WithReservedBits(5, 13)
                    .WithValueField(0, 5, FieldMode.Read, name: "ListRegs",
                        valueProviderCallback: _ => VirtualInterruptCount - 1
                    )
                },
                {(long)CPUInterfaceSystemRegisters.VMControl, new QuadWordRegister(this)
                    .WithReservedBits(32, 32)
                    .WithEnumField<QuadWordRegister, InterruptPriority>(24, 8, name: "VPMR",
                        valueProviderCallback: _ => GetAskingCPUEntry().VirtualPriorityMask,
                        writeCallback: (_, val) => GetAskingCPUEntry().VirtualPriorityMask = val
                    )
                    .WithTag("VBPR0", 21, 3)
                    .WithTag("VBPR1", 18, 3)
                    .WithEnumField<QuadWordRegister, EndOfInterruptModes>(9, 1, name: "VEOIM",
                        valueProviderCallback: _ => GetAskingCPUEntry().EndOfInterruptModeVirtual,
                        writeCallback: (_, val) => GetAskingCPUEntry().EndOfInterruptModeVirtual = val
                    )
                    .WithReservedBits(5, 4)
                    .WithTaggedFlag("VCBPR", 4)
                    .WithFlag(3, name: "VFIQEn",
                        valueProviderCallback: _ => GetAskingCPUEntry().VirtualFIQEnabled,
                        writeCallback: (_, val) => GetAskingCPUEntry().VirtualFIQEnabled = val
                    )
                    .WithTaggedFlag("VAckCtl", 2)
                    .WithFlag(1, name: "VENG1",
                        valueProviderCallback: _ => GetAskingCPUEntry().Groups.Virtual[GroupType.Group1].Enabled,
                        writeCallback: (_, val) => GetAskingCPUEntry().Groups.Virtual[GroupType.Group1].Enabled = val
                    )
                    .WithFlag(0, name: "VENG0",
                        valueProviderCallback: _ => GetAskingCPUEntry().Groups.Virtual[GroupType.Group0].Enabled,
                        writeCallback: (_, val) => GetAskingCPUEntry().Groups.Virtual[GroupType.Group0].Enabled = val
                    )
                },
                {(long)CPUInterfaceSystemRegisters.SoftwareGeneratedInterruptGroup1Generate, BuildSGIGenerateRegister(() => GetGroup1ForSecurityState(GetAskingCPUEntry().IsStateSecure))},
                {(long)CPUInterfaceSystemRegisters.SoftwareGeneratedInterruptGroup1GenerateAlias, BuildSGIGenerateRegister(() => GetGroup1ForSecurityState(!GetAskingCPUEntry().IsStateSecure))},
                {(long)CPUInterfaceSystemRegisters.SoftwareGeneratedInterruptGroup0Generate, BuildSGIGenerateRegister(() => GroupType.Group0)},
            };

            for(int j = 0; j < VirtualInterruptCount; ++j)
            {
                var i = j;

                Func<VirtualInterrupt> virtualInterrupt = () => GetAskingCPUEntry().VirtualInterrupts[i];
                Action syncInterruptState = () =>
                {
                    var currentInterrupt = virtualInterrupt();
                    var activeInterrupt = GetAskingCPUEntry().RunningInterrupts.GetActiveVirtual(currentInterrupt.Identifier);
                    if(activeInterrupt != null)
                    {
                        activeInterrupt.Sync(currentInterrupt);
                    }
                };

                registersMap.Add((long)CPUInterfaceSystemRegisters.ListRegister_0 + i, new QuadWordRegister(this)
                    .WithValueField(62, 2, name: "State",
                        valueProviderCallback: _ => virtualInterrupt().State.Bits,
                        writeCallback: (_, val) => virtualInterrupt().State.Bits = val
                    )
                    .WithFlag(61, name: "HW",
                        valueProviderCallback: _ => virtualInterrupt().Hardware,
                        writeCallback: (_, val) => virtualInterrupt().Hardware = val
                    )
                    .WithFlag(60, name: "Group",
                        valueProviderCallback: _ => virtualInterrupt().Config.GroupBit,
                        writeCallback: (_, val) => virtualInterrupt().Config.GroupBit = val
                    )
                    .WithReservedBits(56, 4)
                    .WithEnumField<QuadWordRegister, InterruptPriority>(48, 8, name: "Priority",
                        valueProviderCallback: _ => virtualInterrupt().Config.Priority,
                        writeCallback: (_, val) => virtualInterrupt().Config.Priority = val
                    )
                    .WithReservedBits(45, 3)
                    .WithValueField(32, 13, name: "pINTID",
                        valueProviderCallback: _ => (uint)virtualInterrupt().HardwareIdentifier,
                        writeCallback: (_, val) => virtualInterrupt().SetHardwareIdentifier((uint)val)
                    )
                    .WithValueField(0, 32, name: "vINTID",
                        valueProviderCallback: _ => (uint)virtualInterrupt().Identifier,
                        writeCallback: (_, val) => virtualInterrupt().SetIdentifier((uint)val)
                    )
                    .WithWriteCallback((_, __) => syncInterruptState())
                );

                registersMap.Add((long)CPUInterfaceSystemRegisters.ListRegisterUpper_0 + i, new QuadWordRegister(this)
                    .WithReservedBits(32, 32)
                    .WithValueField(30, 2, name: "State",
                        valueProviderCallback: _ => virtualInterrupt().State.Bits,
                        writeCallback: (_, val) => virtualInterrupt().State.Bits = val
                    )
                    .WithFlag(29, name: "HW",
                        valueProviderCallback: _ => virtualInterrupt().Hardware,
                        writeCallback: (_, val) => virtualInterrupt().Hardware = val
                    )
                    .WithFlag(28, name: "Group",
                        valueProviderCallback: _ => virtualInterrupt().Config.GroupBit,
                        writeCallback: (_, val) => virtualInterrupt().Config.GroupBit = val
                    )
                    .WithReservedBits(24, 4)
                    .WithEnumField<QuadWordRegister, InterruptPriority>(16, 8, name: "Priority",
                        valueProviderCallback: _ => virtualInterrupt().Config.Priority,
                        writeCallback: (_, val) => virtualInterrupt().Config.Priority = val
                    )
                    .WithReservedBits(13, 3)
                    .WithValueField(0, 13, name: "pINTID",
                        valueProviderCallback: _ => (uint)virtualInterrupt().HardwareIdentifier,
                        writeCallback: (_, val) => virtualInterrupt().SetHardwareIdentifier((uint)val)
                    )
                    .WithWriteCallback((_, __) => syncInterruptState())
                );
            }

            return registersMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildCPUInterfaceRegistersMap()
        {
            Func<GroupTypeSecurityAgnostic> getRegisterGroupTypeSecurityAgnostic = () => (DisabledSecurity || GetAskingCPUEntry().IsStateSecure) ? GroupTypeSecurityAgnostic.Group0 : GroupTypeSecurityAgnostic.Group1;
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)CPUInterfaceRegisters.InterfaceIdentification, new DoubleWordRegister(this)
                    .WithValueField(20, 12, FieldMode.Read, valueProviderCallback: _ => CPUInterfaceProductIdentifier, name: "ProductIdentifier")
                    .WithEnumField<DoubleWordRegister, ARM_GenericInterruptControllerVersion>(16, 4, FieldMode.Read, valueProviderCallback: _ => ArchitectureVersion, name: "ArchitectureVersion")
                    .WithValueField(12, 4, FieldMode.Read, valueProviderCallback: _ => CPUInterfaceRevision, name: "RevisionNumber")
                    .WithValueField(0, 12, FieldMode.Read, valueProviderCallback: _ => CPUInterfaceImplementer, name: "ImplementerIdentification")
                },
                {(long)CPUInterfaceRegisters.RunningPriority, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithEnumField<DoubleWordRegister, InterruptPriority>(0, 8, FieldMode.Read, name: "RunningPriority",
                        valueProviderCallback: _ => GetAskingCPUEntry().RunningInterrupts.Priority
                    )
                },
                {(long)CPUInterfaceRegisters.PriorityMask, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithEnumField<DoubleWordRegister, InterruptPriority>(0, 8, name: "PriorityMask",
                        writeCallback: (_, val) => GetAskingCPUEntry().PriorityMask = val,
                        valueProviderCallback: _ => GetAskingCPUEntry().PriorityMask
                    )
                },
                {(long)CPUInterfaceRegisters.InterruptAcknowledge,
                    BuildInterruptAcknowledgeRegister(new DoubleWordRegister(this), 32, "InterruptAcknowledge", getRegisterGroupTypeSecurityAgnostic, true)
                },
                {(long)CPUInterfaceRegisters.InterruptEnd,
                    BuildInterruptDeactivateOrInterruptEndRegister(new DoubleWordRegister(this), 32, "InterruptEnd", getRegisterGroupTypeSecurityAgnostic,
                        useCPUIdentifier: true, isDeactivateRegister: false)
                },
                {(long)CPUInterfaceRegisters.InterruptDeactivate,
                    BuildInterruptDeactivateOrInterruptEndRegister(new DoubleWordRegister(this), 32, "InterruptDeactivate", null,
                        useCPUIdentifier: true, isDeactivateRegister: true)
                },
            };

            return registersMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildCPUInterfaceRegistersMapSecurityView(bool accessForDisabledSecurity, SecurityState? securityStateAccess = null)
        {
            var controlRegister = new DoubleWordRegister(this);
            if(accessForDisabledSecurity || securityStateAccess == SecurityState.Secure)
            {
                controlRegister
                    .WithFlag(8, FieldMode.Read, name: "IRQBypassGroup1", valueProviderCallback: _ => false)
                    .WithFlag(7, FieldMode.Read, name: "FIQBypassGroup1", valueProviderCallback: _ => false)
                    .WithFlag(6, FieldMode.Read, name: "IRQBypassGroup0", valueProviderCallback: _ => false)
                    .WithFlag(5, FieldMode.Read, name: "FIQBypassGroup0", valueProviderCallback: _ => false)
                    .WithTaggedFlag("PreemptionControl", 4)
                    .WithFlag(3, name: "EnableFIQ",
                        writeCallback: (_, val) =>
                        {
                            if(!ArchitectureVersionAtLeast3)
                            {
                                enableFIQ = val;
                            }
                            else
                            {
                                this.Log(LogLevel.Warning, "Modifying EnableFIQ flag is not supported in this architecture version");
                            }
                        },
                        valueProviderCallback: _ => enableFIQ
                    )
                    .WithFlag(2, name: "AcknowledgementControl",
                        writeCallback: (_, val) => { if(val) this.Log(LogLevel.Warning, "Setting deprecated GICC_CTLR.AckCtl flag!"); ackControl = val; },
                        valueProviderCallback: _ => ackControl
                    )
                    .WithFlag(1, name: "EnableGroup1",
                        writeCallback: (_, val) => GetAskingCPUEntry().Groups[GroupType.Group1].Enabled = val,
                        valueProviderCallback: _ => GetAskingCPUEntry().Groups[GroupType.Group1].Enabled
                    )
                    .WithFlag(0, name: "EnableGroup0",
                        writeCallback: (_, val) => GetAskingCPUEntry().Groups[GroupType.Group0].Enabled = val,
                        valueProviderCallback: _ => GetAskingCPUEntry().Groups[GroupType.Group0].Enabled
                    );

                if(accessForDisabledSecurity)
                {
                    controlRegister
                        .WithReservedBits(10, 22)
                        .WithFlag(9, name: "EndOfInterruptMode",
                            writeCallback: (_, val) => GetAskingCPUEntry().EndOfInterruptModeEL1 = (EndOfInterruptModes)(val ? 1 : 0),
                            valueProviderCallback: _ => GetAskingCPUEntry().EndOfInterruptModeEL1 == EndOfInterruptModes.PriorityDropOnly
                        );
                }
                else
                {
                    controlRegister
                        .WithReservedBits(11, 21)
                        .WithFlag(10, name: "EndOfInterruptModeNonSecure",
                            writeCallback: (_, val) => GetAskingCPUEntry().EndOfInterruptModeEL1NonSecure = (EndOfInterruptModes)(val ? 1 : 0),
                            valueProviderCallback: _ => GetAskingCPUEntry().EndOfInterruptModeEL1NonSecure == EndOfInterruptModes.PriorityDropOnly
                        )
                        .WithFlag(9, name: "EndOfInterruptModeSecure",
                            writeCallback: (_, val) => GetAskingCPUEntry().EndOfInterruptModeEL1Secure = (EndOfInterruptModes)(val ? 1 : 0),
                            valueProviderCallback: _ => GetAskingCPUEntry().EndOfInterruptModeEL1Secure == EndOfInterruptModes.PriorityDropOnly
                        );
                }
            }
            else
            {
                controlRegister
                    .WithReservedBits(10, 22)
                    .WithFlag(9, name: "EndOfInterruptModeNonSecure",
                        writeCallback: (_, val) => GetAskingCPUEntry().EndOfInterruptModeEL1NonSecure = (EndOfInterruptModes)(val ? 1 : 0),
                        valueProviderCallback: _ => GetAskingCPUEntry().EndOfInterruptModeEL1NonSecure == EndOfInterruptModes.PriorityDropOnly
                    )
                    .WithReservedBits(7, 2)
                    .WithFlag(6, FieldMode.Read, name: "IRQBypassGroup1", valueProviderCallback: _ => false)
                    .WithFlag(5, FieldMode.Read, name: "FIQBypassGroup1", valueProviderCallback: _ => false)
                    .WithReservedBits(1, 4)
                    .WithFlag(0, name: "EnableGroup1",
                        writeCallback: (_, val) => GetAskingCPUEntry().Groups[GroupType.Group1].Enabled = val,
                        valueProviderCallback: _ => GetAskingCPUEntry().Groups[GroupType.Group1].Enabled
                    );
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)CPUInterfaceRegisters.Control, controlRegister},

                // Aliases for acknowledging/ending non-secure interrupts in secure state.
                {(long)CPUInterfaceRegisters.InterruptAcknowledgeAlias,
                    BuildInterruptAcknowledgeRegister(new DoubleWordRegister(this), 32, "InterruptAcknowledgeAlias", () => GroupTypeSecurityAgnostic.Group1, true)
                },
                {(long)CPUInterfaceRegisters.InterruptEndAlias,
                    BuildInterruptDeactivateOrInterruptEndRegister(new DoubleWordRegister(this), 32, "InterruptEndAlias", () => GroupTypeSecurityAgnostic.Group1,
                        useCPUIdentifier: true, isDeactivateRegister: false)
                },
            };
            return registersMap;
        }

        private QuadWordRegister BuildSGIGenerateRegister(Func<GroupType> getGroupType)
        {
            return new QuadWordRegister(this)
                .WithReservedBits(56, 8)
                .WithValueField(48, 8, out var affinity3, FieldMode.Write, name: "Affinity3")
                .WithValueField(44, 4, out var rangeSelector, FieldMode.Write, name: "Range Selector")
                .WithReservedBits(41, 3)
                .WithFlag(40, out var interruptRoutingMode, FieldMode.Write, name: "Interrupt Routing Mode")
                .WithValueField(32, 8, out var affinity2, FieldMode.Write, name: "Affinity2")
                .WithReservedBits(28, 4)
                .WithValueField(24, 4, out var interruptID, FieldMode.Write, name: "Interrupt ID")
                .WithValueField(16, 8, out var affinity1, FieldMode.Write, name: "Affinity1")
                .WithReservedBits(5, 11)
                .WithValueField(0, 5, out var targetList)
                .WithWriteCallback((_, newValue) =>
                {
                    var targetType = interruptRoutingMode.Value ? SoftwareGeneratedInterruptRequest.TargetType.AllCPUs : SoftwareGeneratedInterruptRequest.TargetType.TargetList;

                    var list = new Affinity[]{};
                    if(targetType == SoftwareGeneratedInterruptRequest.TargetType.TargetList)
                    {
                        var range = 16 * (byte)rangeSelector.Value;
                        var aff1 = (byte)affinity1.Value;
                        var aff2 = (byte)affinity2.Value;
                        var aff3 = (byte)affinity3.Value;
                        list = BitHelper.GetSetBits(targetList.Value).Select(n => new Affinity((byte)(range + n), aff1, aff2, aff3)).ToArray();
                    }

                    var interrupt = new SoftwareGeneratedInterruptRequest(targetType, list, getGroupType(), new InterruptId((uint)interruptID.Value));
                    OnSoftwareGeneratedInterrupt(GetAskingCPUEntry(), interrupt);
                });
        }

        private T BuildInterruptAcknowledgeRegister<T>(T register, int registerWidth, string name,
            Func<GroupTypeSecurityAgnostic> groupTypeRegisterProvider, bool useCPUIdentifier) where T : PeripheralRegister
        {
            return register
                .WithReservedBits(24, registerWidth - 24)
                .WithValueField(0, 24, FieldMode.Read, name: name,
                    valueProviderCallback: _ =>
                    {
                        var irqId = (uint)GetAskingCPUEntry().AcknowledgeBestPending(groupTypeRegisterProvider(), out var requester);
                        var cpuId = useCPUIdentifier ? requester?.processorNumber ?? 0 : 0;
                        return cpuId << 10 | irqId;
                    }
                );
        }

        private T BuildInterruptDeactivateOrInterruptEndRegister<T>(T register, int registerWidth, string name,
            Func<GroupTypeSecurityAgnostic> groupTypeRegisterProvider, bool useCPUIdentifier, bool isDeactivateRegister) where T : PeripheralRegister
        {
            return register
                .WithReservedBits(24, registerWidth - 24)
                .WithValueField(0, 24, FieldMode.Write, name: name,
                    writeCallback: (_, val) =>
                    {
                        var irqId = BitHelper.GetValue((uint)val, 0, 10);
                        CPUEntry cpu = null;
                        var cpuId = BitHelper.GetValue((uint)val, 10, 3);
                        if(useCPUIdentifier && !TryGetCPUEntry(cpuId, out cpu))
                        {
                            this.Log(LogLevel.Warning, "Trying to {0} the interrupt ({1}) for the non-existing CPU ({2}), write ignored.",
                                isDeactivateRegister ? "deactivate" : "end", irqId, cpuId);
                            return;
                        }
                        var askingCPUEntry = GetAskingCPUEntry();
                        if(isDeactivateRegister)
                        {
                            askingCPUEntry.InterruptDeactivate(new InterruptId(irqId), cpu);
                        }
                        else
                        {
                            askingCPUEntry.InterruptEnd(new InterruptId(irqId), groupTypeRegisterProvider(), cpu);
                        }
                    }
                );
        }

        private IEnumerable<QuadWordRegister> BuildInterruptRoutingRegisters(InterruptId startId, InterruptId endId)
        {
            return InterruptId.GetRange(startId, endId).Select(
                id => new QuadWordRegister(this)
                    .WithReservedBits(40, 24)
                    .WithValueField(32, 8, name: $"Affinity3_{(uint)id}",
                        writeCallback: (_, val) => { if(IsAffinityRoutingEnabled(GetAskingCPUEntry())) sharedInterrupts[id].TargetAffinity.SetLevel(3, (byte)val); },
                        valueProviderCallback: _ => IsAffinityRoutingEnabled(GetAskingCPUEntry()) ? (ulong)sharedInterrupts[id].TargetAffinity.GetLevel(3) : 0
                    )
                    .WithEnumField<QuadWordRegister, InterruptRoutingMode>(31, 1, name: $"RoutingMode_{(uint)id}",
                        writeCallback: (_, val) => { if(IsAffinityRoutingEnabled(GetAskingCPUEntry())) sharedInterrupts[id].RoutingMode = val; },
                        valueProviderCallback: _ => IsAffinityRoutingEnabled(GetAskingCPUEntry()) ? sharedInterrupts[id].RoutingMode : default(InterruptRoutingMode)
                    )
                    .WithReservedBits(24, 7)
                    .WithValueField(16, 8, name: $"Affinity2_{(uint)id}",
                        writeCallback: (_, val) => { if(IsAffinityRoutingEnabled(GetAskingCPUEntry())) sharedInterrupts[id].TargetAffinity.SetLevel(2, (byte)val); },
                        valueProviderCallback: _ => IsAffinityRoutingEnabled(GetAskingCPUEntry()) ? (ulong)sharedInterrupts[id].TargetAffinity.GetLevel(2) : 0
                    )
                    .WithValueField(8, 8, name: $"Affinity1_{(uint)id}",
                        writeCallback: (_, val) => { if(IsAffinityRoutingEnabled(GetAskingCPUEntry())) sharedInterrupts[id].TargetAffinity.SetLevel(1, (byte)val); },
                        valueProviderCallback: _ => IsAffinityRoutingEnabled(GetAskingCPUEntry()) ? (ulong)sharedInterrupts[id].TargetAffinity.GetLevel(1) : 0
                    )
                    .WithValueField(0, 8, name: $"Affinity0_{(uint)id}",
                        writeCallback: (_, val) => { if(IsAffinityRoutingEnabled(GetAskingCPUEntry())) sharedInterrupts[id].TargetAffinity.SetLevel(0, (byte)val); },
                        valueProviderCallback: _ => IsAffinityRoutingEnabled(GetAskingCPUEntry()) ? (ulong)sharedInterrupts[id].TargetAffinity.GetLevel(0) : 0
                    )
                    .WithWriteCallback((_, __) => { if(!IsAffinityRoutingEnabled(GetAskingCPUEntry())) this.Log(LogLevel.Warning, "Trying to write IROUTER register when Affinity Routing is disabled, write ignored."); })
                    .WithReadCallback((_, __) => { if(!IsAffinityRoutingEnabled(GetAskingCPUEntry())) this.Log(LogLevel.Warning, "Trying to read IROUTER register when Affinity Routing is disabled."); })
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptFlagRegisters(InterruptId startId, InterruptId endId, string name,
            Action<Interrupt, bool> writeCallback = null, Func<Interrupt, bool> valueProviderCallback = null, bool allowAccessWhenNonSecureGroup = true,
            CPUEntry sgiRequestingCPU = null, Func<CPUEntry> cpuEntryProvider = null)
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
                            writeCallbackWrapped = (_, val) => { if(CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup)) writeCallback(irqGetter(), val); };
                        }
                        Func<bool, bool> valueProviderCallbackWrapped = null;
                        if(valueProviderCallback != null)
                        {
                            fieldMode |= FieldMode.Read;
                            valueProviderCallbackWrapped = _ => CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup) ? valueProviderCallback(irqGetter()) : false;
                        }
                        register.WithFlag(fieldIndex, fieldMode, name: $"{name}_{(uint)irqId}",
                            writeCallback: writeCallbackWrapped, valueProviderCallback: valueProviderCallbackWrapped);
                    },

                (register, fieldIndex) => register.WithReservedBits(fieldIndex, 1),
                sgiRequestingCPU, cpuEntryProvider
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptValueRegisters(InterruptId startId, InterruptId endId, string name, int fieldsPerRegister,
            Action<Interrupt, ulong> writeCallback = null, Func<Interrupt, ulong> valueProviderCallback = null, bool allowAccessWhenNonSecureGroup = true,
            CPUEntry sgiRequestingCPU = null, Func<CPUEntry> cpuEntryProvider = null)
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
                            writeCallbackWrapped = (_, val) => { if(CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup)) writeCallback(irqGetter(), val); };
                        }
                        Func<ulong, ulong> valueProviderCallbackWrapped = null;
                        if(valueProviderCallback != null)
                        {
                            fieldMode |= FieldMode.Read;
                            valueProviderCallbackWrapped = _ => CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup) ? valueProviderCallback(irqGetter()) : 0;
                        }
                        register.WithValueField(fieldIndex * fieldWidth, fieldWidth, fieldMode, name: $"{name}_{(uint)irqId}",
                            writeCallback: writeCallbackWrapped, valueProviderCallback: valueProviderCallbackWrapped);
                    },
                (register, fieldIndex) => register.WithReservedBits(fieldIndex * fieldWidth, fieldWidth),
                sgiRequestingCPU, cpuEntryProvider
            );
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptEnumRegisters<TEnum>(InterruptId startId, InterruptId endId, string name, int fieldsPerRegister,
            Action<Interrupt, TEnum> writeCallback = null, Func<Interrupt, TEnum> valueProviderCallback = null, bool allowAccessWhenNonSecureGroup = true,
            CPUEntry sgiRequestingCPU = null, Func<CPUEntry> cpuEntryProvider = null) where TEnum : struct, IConvertible
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
                            writeCallbackWrapped = (_, val) => { if(CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup)) writeCallback(irqGetter(), val); };
                        }
                        Func<TEnum, TEnum> valueProviderCallbackWrapped = null;
                        if(valueProviderCallback != null)
                        {
                            fieldMode |= FieldMode.Read;
                            valueProviderCallbackWrapped = _ => CheckInterruptAccess(irqGetter, allowAccessWhenNonSecureGroup) ? valueProviderCallback(irqGetter()) : default(TEnum);
                        }
                        register.WithEnumField<DoubleWordRegister, TEnum>(fieldIndex * fieldWidth, fieldWidth, fieldMode, name: $"{name}_{(uint)irqId}",
                            writeCallback: writeCallbackWrapped, valueProviderCallback: valueProviderCallbackWrapped);
                    },
                (register, fieldIndex) => register.WithReservedBits(fieldIndex * fieldWidth, fieldWidth),
                sgiRequestingCPU, cpuEntryProvider
            );
        }

        private bool IsAffinityRoutingEnabled(CPUEntry cpu)
        {
            return DisabledSecurity || cpu.IsStateSecure ? AffinityRoutingEnabledSecure : AffinityRoutingEnabledNonSecure;
        }

        private bool CheckInterruptAccess(Func<Interrupt> irqGetter, bool allowAccessWhenNonSecureGroup)
        {
            if(DisabledSecurity || GetAskingCPUEntry().IsStateSecure
                || (allowAccessWhenNonSecureGroup && irqGetter().Config.GroupType == GroupType.Group1NonSecure))
            {
                return true;
            }
            this.Log(LogLevel.Debug, "Trying to access a field of the interrupt {0}, which isn't accessible from the current security state.", irqGetter().Identifier);
            return false;
        }

        private IEnumerable<DoubleWordRegister> BuildInterruptRegisters(InterruptId startId, InterruptId endId, int fieldsPerRegister,
            Action<DoubleWordRegister, Func<Interrupt>, InterruptId, int> fieldAction,
            Action<DoubleWordRegister, int> fieldPlaceholderAction,
            // NOTE: sgiRequestingCPU is currently always null as we don't support registers like GICD_CPENDSGIRn which need this information
            CPUEntry sgiRequestingCPU, Func<CPUEntry> cpuEntryProvider
        )
        {
            if(cpuEntryProvider == null)
            {
                cpuEntryProvider = GetAskingCPUEntry;
            }
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
                    if(irqsDecoder.Type(irqId) == InterruptType.Reserved)
                    {
                        fieldPlaceholderAction(register, inRegisterIndex);
                    }
                    else if(irqsDecoder.IsSoftwareGenerated(irqId))
                    {
                        if(sgiRequestingCPU == null)
                        {
                            // NOTE: We're returning here from SoftwareGeneratedInterruptsUnknownRequester despite some of the registers operating on
                            //       the LegacyRequester interrupts. This is fine, as all of those registers actually operate on InterruptConfig
                            //       and that's shared between interrupts in UnknownRequester and LegacyRequester.
                            fieldAction(register, () => cpuEntryProvider().SoftwareGeneratedInterruptsUnknownRequester[irqId], irqId, inRegisterIndex);
                        }
                        else
                        {
                            fieldAction(register, () => cpuEntryProvider().SoftwareGeneratedInterruptsLegacyRequester[sgiRequestingCPU][irqId], irqId, inRegisterIndex);
                        }
                    }
                    else if(irqsDecoder.IsPrivatePeripheral(irqId))
                    {
                        fieldAction(register, () => cpuEntryProvider().PrivatePeripheralInterrupts[irqId], irqId, inRegisterIndex);
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

        private CPUEntry GetAskingCPUEntry()
        {
            var cpu = busController.GetCurrentCPU();
            var armCPU = cpu as IARMSingleSecurityStateCPU;
            if(armCPU == null || !cpuEntries.ContainsKey(armCPU))
            {
                throw new InvalidOperationException($"Non-Arm CPU or one that isn't attached to the GIC tried to access the GIC: {cpu.GetName()}");
            }
            return cpuEntries[armCPU];
        }

        private CPUEntryWithTwoSecurityStates GetAskingCPUEntryWithTwoSecurityStates()
        {
            var cpuEntry = GetAskingCPUEntry();
            if(!(cpuEntry is CPUEntryWithTwoSecurityStates cpuEntryWithTwoSecurityStates))
            {
                throw new InvalidOperationException($"The asking CPU '{cpuEntry.Name}' doesn't have two security states!");
            }
            return cpuEntryWithTwoSecurityStates;
        }

        private bool TryWriteRegisterSecurityView(long offset, uint value, DoubleWordRegisterCollection notBankedRegisters,
            DoubleWordRegisterCollection secureRegisters, DoubleWordRegisterCollection nonSecureRegisters, DoubleWordRegisterCollection disabledSecurityRegisters)
        {
            var bankedRegisters = GetBankedRegistersCollection(secureRegisters, nonSecureRegisters, disabledSecurityRegisters);
            return bankedRegisters.TryWrite(offset, value) || notBankedRegisters.TryWrite(offset, value);
        }

        private bool TryReadRegisterSecurityView(long offset, out uint value, DoubleWordRegisterCollection notBankedRegisters,
            DoubleWordRegisterCollection secureRegisters, DoubleWordRegisterCollection nonSecureRegisters, DoubleWordRegisterCollection disabledSecurityRegisters)
        {
            var bankedRegisters = GetBankedRegistersCollection(secureRegisters, nonSecureRegisters, disabledSecurityRegisters);
            return bankedRegisters.TryRead(offset, out value) || notBankedRegisters.TryRead(offset, out value);
        }

        private DoubleWordRegisterCollection GetBankedRegistersCollection(DoubleWordRegisterCollection secureRegisters, DoubleWordRegisterCollection nonSecureRegisters,
            DoubleWordRegisterCollection disabledSecurityRegisters)
        {
            if(DisabledSecurity)
            {
                return disabledSecurityRegisters;
            }
            else
            {
                return GetAskingCPUEntry().IsStateSecure ? secureRegisters : nonSecureRegisters;
            }
        }

        private bool IsDistributorByteAccessible(long offset)
        {
            return IsByteOffsetInDistributorRegistersRange(offset, DistributorRegisters.InterruptPriority_0, DistributorRegisters.InterruptPriority_254)
                || IsByteOffsetInDistributorRegistersRange(offset, DistributorRegisters.InterruptProcessorTargets_0, DistributorRegisters.InterruptProcessorTargets_254)
                || IsByteOffsetInDistributorRegistersRange(offset, DistributorRegisters.SoftwareGeneratedIntrruptClearPending_0, DistributorRegisters.SoftwareGeneratedIntrruptClearPending_3)
                || IsByteOffsetInDistributorRegistersRange(offset, DistributorRegisters.SoftwareGeneratedIntrruptSetPending_0, DistributorRegisters.SoftwareGeneratedIntrruptSetPending_3);
        }

        private bool IsByteOffsetInDistributorRegistersRange(long offset, DistributorRegisters startOffset, DistributorRegisters endOffset)
        {
            const long maxByteOffset = 3;
            return (long)startOffset <= offset && offset <= (long)endOffset + maxByteOffset;
        }

        private bool IsRedistributorByteAccessible(long offset)
        {
            const long maxByteOffset = 3;
            return (long)RedistributorRegisters.InterruptPriority_0 <= offset && offset <= (long)RedistributorRegisters.InterruptPriority_7 + maxByteOffset;
        }

        private bool SetOnTransitionToTrue(ref bool value, bool newValue, string warningOnTransitionToFalse)
        {
            if(newValue == value)
            {
                return false;
            }
            if(!newValue)
            {
                this.Log(LogLevel.Warning, warningOnTransitionToFalse);
                return false;
            }
            value = true;
            return true;
        }

        private void ClearForcedTargettingCache()
        {
            forcedTargettedCpuForAffinityRouting = null;
        }

        private void AddAutomaticGPIOConnections(IARMSingleSecurityStateCPU cpu)
        {
            var inputCount = cpu.GetPeripheralInputCount();
            var processorNumber = (int)GetProcessorNumber(cpu);
            // For the convention of connecting interrupt signals see `InterruptSignalType` definition
            for(int i = 0; i < inputCount; i++)
            {
                var source = Connections[(processorNumber * 4) + i];
                source.Connect(cpu, i);
                automaticallyConnectedGPIOs.Add(source);
            }
        }

        private static uint GetProcessorNumber(ICPU cpu)
        {
            switch(cpu.Model)
            {
                // For armv8.2 core number is stored in affinity level 1.
                // In older CPUs (i.e. Cortex-A53) it's stored in affinity level 0.
                case "cortex-a55":
                case "cortex-a78":
                    return BitHelper.GetValue(cpu.MultiprocessingId, 8, 8);
                default:
                    return BitHelper.GetValue(cpu.MultiprocessingId, 0, 8);
            }
        }

        private CPUEntry ForcedTargettingCpuForAffinityRouting
        {
            get
            {
                if(forcedTargettedCpuForAffinityRouting == null)
                {
                    forcedTargettedCpuForAffinityRouting = cpuEntries.Values.Where(cpu => cpu.IsParticipatingInRouting).MinBy(cpu => cpu.affinity.AllLevels);
                }
                return forcedTargettedCpuForAffinityRouting;
            }
        }

        private bool ackControl;
        private bool enableFIQ;
        private bool disabledSecurity;
        private bool affinityRoutingEnabledSecure;
        private bool affinityRoutingEnabledNonSecure;
        private uint legacyCpusAttachedMask;
        private CPUEntry forcedTargettedCpuForAffinityRouting;

        private readonly IBusController busController;
        private readonly Object locker = new Object();
        private readonly Dictionary<uint, IARMSingleSecurityStateCPU> cpusByProcessorNumberCache = new Dictionary<uint, IARMSingleSecurityStateCPU>();
        private readonly Dictionary<IARMSingleSecurityStateCPU, CPUEntry> cpuEntries = new Dictionary<IARMSingleSecurityStateCPU, CPUEntry>();
        private readonly List<IGPIO> automaticallyConnectedGPIOs = new List<IGPIO>();
        private readonly InterruptSignalType[] supportedInterruptSignals;
        private readonly ReadOnlyDictionary<InterruptId, SharedInterrupt> sharedInterrupts;
        private readonly ReadOnlyDictionary<GroupType, InterruptGroup> groups;
        private readonly DoubleWordRegisterCollection distributorDoubleWordRegisters;
        private readonly QuadWordRegisterCollection distributorQuadWordRegisters;
        private readonly DoubleWordRegisterCollection distributorRegistersSecureView;
        private readonly DoubleWordRegisterCollection distributorRegistersNonSecureView;
        private readonly DoubleWordRegisterCollection distributorRegistersDisabledSecurityView;
        private readonly DoubleWordRegisterCollection cpuInterfaceRegisters;
        private readonly DoubleWordRegisterCollection cpuInterfaceRegistersSecureView;
        private readonly DoubleWordRegisterCollection cpuInterfaceRegistersNonSecureView;
        private readonly DoubleWordRegisterCollection cpuInterfaceRegistersDisabledSecurityView;
        private readonly QuadWordRegisterCollection cpuInterfaceSystemRegisters;

        private readonly uint affinityToIdMask;
        private readonly bool supportsTwoSecurityStates;
        private readonly InterruptsDecoder irqsDecoder;

        private const ARM_GenericInterruptControllerVersion DefaultArchitectureVersion = ARM_GenericInterruptControllerVersion.GICv3;
        private const uint DefaultCPUInterfaceProductIdentifier = 0x0;
        private const uint DefaultDistributorProductIdentifier = 0x0;
        private const uint DefaultRedistributorProductIdentifier = 0x0;
        private const byte DefaultVariantNumber = 0x0;
        private const byte DefaultRevisionNumber = 0x0;
        private const uint DefaultImplementerIdentification = 0x43B; // This value indicates the JEP106 code of the Arm as an implementer

        private const uint CPUsCountLegacySupport = 8;
        private const long RedistributorPrivateInterruptsFrameOffset = 0x10000;

        private const uint VirtualInterruptCount = 16;
        private const uint VirtualPriorityBits = 5;

        public class CPUEntry : IGPIOReceiver
        {
            public CPUEntry(ARM_GenericInterruptController gic, IARMSingleSecurityStateCPU cpu, IEnumerable<GroupType> groupTypes, IReadOnlyDictionary<InterruptSignalType, IGPIO> interruptConnections)
            {
                this.gic = gic;
                this.cpu = cpu;
                processorNumber = GetProcessorNumber(cpu);
                targetFieldFlag = processorNumber <= CPUsCountLegacySupport ? 1U << (int)processorNumber : 0;
                affinity = cpu.Affinity;
                Name = $"cpu{affinity}";
                interruptSignals = interruptConnections;

                var sgiIds = InterruptId.GetRange(gic.IrqsDecoder.SoftwareGeneratedFirst, gic.IrqsDecoder.SoftwareGeneratedLast);
                SoftwareGeneratedInterruptsConfig = new ReadOnlyDictionary<InterruptId, InterruptConfig>(sgiIds.ToDictionary(id => id, _ => new InterruptConfig()));
                SoftwareGeneratedInterruptsUnknownRequester = GenerateSGIs(SoftwareGeneratedInterruptsConfig, null);
                SoftwareGeneratedInterruptsLegacyRequester = new Dictionary<CPUEntry, ReadOnlyDictionary<InterruptId, SoftwareGeneratedInterrupt>>();

                redistributorDoubleWordRegisters = new DoubleWordRegisterCollection(this, BuildRedistributorDoubleWordRegisterMap());
                redistributorQuadWordRegisters = new QuadWordRegisterCollection(this, BuildRedistributorQuadWordRegisterMap());

                var ppiIds = InterruptId.GetRange(gic.IrqsDecoder.PrivatePeripheralFirst, gic.IrqsDecoder.PrivatePeripheralLast)
                    .Concat(InterruptId.GetRange(gic.IrqsDecoder.ExtendedPrivatePeripheralFirst, gic.IrqsDecoder.ExtendedPrivatePeripheralLast));
                PrivatePeripheralInterrupts = new ReadOnlyDictionary<InterruptId, Interrupt>(ppiIds.ToDictionary(id => id, id => new Interrupt(id)));

                Groups = new GroupCollection(this, groupTypes);
                RunningInterrupts = new RunningInterrupts(this);
                VirtualInterrupts = new VirtualInterrupt[VirtualInterruptCount];
                for(var i = 0; i < VirtualInterruptCount; ++i)
                {
                    VirtualInterrupts[i] = new VirtualInterrupt();
                }
            }

            public virtual void Reset()
            {
                foreach(var irq in AllPrivateAndSoftwareGeneratedInterrupts)
                {
                    irq.Reset();
                }
                foreach(var interrupt in VirtualInterrupts)
                {
                    interrupt.Reset();
                }
                for(var i = 0; i < NonSecureSGIAccess.Length; ++i)
                {
                    NonSecureSGIAccess[i] = NonSecureAccess.NotPermitted;
                }
                Groups.Reset();
                redistributorQuadWordRegisters.Reset();
                redistributorDoubleWordRegisters.Reset();
                VirtualCPUInterfaceEnabled = false;
                BestPending = null;
                BestPendingVirtual = null;
                EndOfInterruptModeEL1NonSecure = EndOfInterruptModes.PriorityDropAndDeactivation;
                EndOfInterruptModeEL1Secure = EndOfInterruptModes.PriorityDropAndDeactivation;
                EndOfInterruptModeVirtual = EndOfInterruptModes.PriorityDropAndDeactivation;
                VirtualFIQEnabled = false;
                IsSleeping = true;
                RunningInterrupts.Clear();
                PhysicalPriorityMask = InterruptPriority.Idle;
                VirtualPriorityMask = InterruptPriority.Idle;
                UpdateSignals();
            }

            // It's expected to pass handling of private interrupts to the ARM_GenericInterruptController class using an event handler
            public void OnGPIO(int number, bool value)
            {
                PrivateInterruptChanged?.Invoke(this, number, value);
            }

            public virtual InterruptId AcknowledgeBestPending(GroupTypeSecurityAgnostic groupTypeRegister, out CPUEntry sgiRequestingCPU)
            {
                var groupType = GetGroupTypeForRegisterSecurityAgnostic(groupTypeRegister);
                var isVirtualized = IsVirtualized(groupType);
                sgiRequestingCPU = null;
                var pendingIrq = isVirtualized ? BestPendingVirtual : BestPending;
                if(pendingIrq == null)
                {
                    return gic.IrqsDecoder.NoPending;
                }
                if(pendingIrq.Config.GroupType != groupType)
                {
                    // In GICv2, Secure (Group 0) access can acknowledge Group 1 interrupt if GICC_CTLR.AckCtl is set. Otherwise, the returned Interrupt ID is 1022 (NoPending=1023).
                    if(!isVirtualized && gic.ArchitectureVersion == ARM_GenericInterruptControllerVersion.GICv2 && groupTypeRegister == GroupTypeSecurityAgnostic.Group0)
                    {
                        if(!gic.ackControl)
                        {
                            gic.Log(LogLevel.Warning, "Trying to acknowledge pending Group 1 interrupt (#{0}) with secure GIC access while GICC_CTLR.AckCtl isn't set", (uint)pendingIrq.Identifier);
                            return gic.IrqsDecoder.NonMaskableInterruptOrGICv2GroupMismatch;
                        }
                    }
                    else
                    {
                        gic.Log(LogLevel.Warning, "Trying to acknowledge pending interrupt using register of an incorrect interrupt group ({0}), expected {1}.", groupType, pendingIrq.Config.GroupType);
                        return gic.IrqsDecoder.NoPending;
                    }
                }
                pendingIrq.State.Acknowledge();
                RunningInterrupts.Push(groupType, pendingIrq);
                if(isVirtualized)
                {
                    BestPendingVirtual = null;
                }
                else
                {
                    BestPending = null;
                }

                var pendingIrqAsSGI = pendingIrq as SoftwareGeneratedInterrupt;
                if(pendingIrqAsSGI != null)
                {
                    sgiRequestingCPU = pendingIrqAsSGI.Requester;
                }

                return pendingIrq.Identifier;
            }

            // Performs priority drop and, if independentEOIControls is false, IRQ deactivation.
            public void InterruptEnd(InterruptId id, GroupTypeSecurityAgnostic groupTypeRegister, CPUEntry sgiRequestingCPU)
            {
                var groupType = GetGroupTypeForRegisterSecurityAgnostic(groupTypeRegister);
                var isVirtualized = IsVirtualized(groupType);
                var shouldBeDeactivated = CurrentEndOfInterruptMode == EndOfInterruptModes.PriorityDropAndDeactivation;
                gic.Log(LogLevel.Debug, "Ending interrupt with id {0}{1}.", (uint)id, shouldBeDeactivated ? "" : " but it won't be deactivated");

                if(RunningInterrupts.Count(groupType) == 0)
                {
                    gic.Log(LogLevel.Warning, "Trying to end the running interrupt when no interrupt is running.");
                    return;
                }

                var runningIrq = RunningInterrupts.Peek(groupType);
                if(runningIrq.Config.GroupType != groupType)
                {
                    // In GICv2, Secure (Group 0) access can affect Group 1 interrupts if GICC_CTLR.AckCtl is set.
                    if(!isVirtualized && gic.ArchitectureVersion == ARM_GenericInterruptControllerVersion.GICv2 && groupTypeRegister == GroupTypeSecurityAgnostic.Group0)
                    {
                        if(!gic.ackControl)
                        {
                            gic.Log(LogLevel.Warning, "Trying to end the running Group 1 interrupt (#{0}) with secure GIC access while GICC_CTLR.AckCtl isn't set, request ignored.", (uint)runningIrq.Identifier);
                            return;
                        }
                    }
                    else
                    {
                        gic.Log(LogLevel.Warning, "Trying to end the running interrupt using the register of an incorrect interrupt group ({0}), expected {1}, request ignored.", groupType, runningIrq.Config.GroupType);
                        return;
                    }
                }

                if(!runningIrq.Identifier.Equals(id))
                {
                    gic.Log(LogLevel.Error, "Incorrect interrupt identifier to end the running interrupt, expected INTID {0}, given {1}, request ignored.", runningIrq.Identifier, id);
                    return;
                }

                if(!IsSoftwareGeneratedInterruptAccessValid(runningIrq, sgiRequestingCPU, "InterruptEnd"))
                {
                    return;
                }

                // The interrupt are just removed from stack of currently running interrupts
                // It's still accessible using one of the read only collections of interrupts (shared or private ones)
                // If the stack becomes empty, RunningPriority will return InterruptPriority.Idle; otherwise the priority of an interrupt on top of stack.
                RunningInterrupts.Pop(groupType);

                if(shouldBeDeactivated)
                {
                    DeactivateInterrupt(runningIrq, isVirtualized);
                }
            }

            public void InterruptDeactivate(InterruptId id, CPUEntry sgiRequestingCPU)
            {
                Action<string> logFailure = failureReason => gic.Log(
                    LogLevel.Warning, "Trying to deactivate interrupt with id {0} {1}, write ignored.", (uint)id, failureReason
                );

                var isVirtualized = IsVirtualized();

                if(CurrentEndOfInterruptMode == EndOfInterruptModes.PriorityDropAndDeactivation)
                {
                    logFailure($"with EOImode=0 in the current CPU state: {CurrentCPUSecurityStateString}");
                    return;
                }

                var interrupt = isVirtualized
                    ? RunningInterrupts.GetActiveVirtual(id)
                    : gic.GetAllInterrupts(this).SingleOrDefault(x => ((uint)x.Identifier) == ((uint)id) && x.State.Active);
                if(interrupt == null)
                {
                    logFailure("which is an invalid INTID");
                    return;
                }

                if(!gic.DisabledSecurity && !IsStateSecure)
                {
                    var interruptGroupType = interrupt.Config.GroupType;
                    if(interruptGroupType != GroupType.Group1NonSecure)
                    {
                        logFailure("belonging to " + (interruptGroupType == GroupType.Group0 ? "group 0" : "secure group 1") + " from a non-secure state");
                        return;
                    }
                }

                if(!IsSoftwareGeneratedInterruptAccessValid(interrupt, sgiRequestingCPU, "InterruptDeactivate"))
                {
                    return;
                }

                if(!interrupt.State.Active)
                {
                    logFailure("but it isn't active");
                    return;
                }
                DeactivateInterrupt(interrupt, isVirtualized);
            }

            public void UpdateSignals()
            {
                if(BestPending == null && BestPendingVirtual == null)
                {
                    foreach(var signal in interruptSignals.Values)
                    {
                        signal.Set(false);
                    }
                    return;
                }

                var signalType = GetBestPendingInterruptSignalType();
                foreach(var signal in interruptSignals)
                {
                    signal.Value.Set(signal.Key == signalType);
                }
            }

            public InterruptGroup GetGroupForRegisterSecurityAgnostic(GroupTypeSecurityAgnostic type)
            {
                return Groups[GetGroupTypeForRegisterSecurityAgnostic(type)];
            }

            public GroupType GetGroupTypeForRegisterSecurityAgnostic(GroupTypeSecurityAgnostic type)
            {
                if(type == GroupTypeSecurityAgnostic.Group0)
                {
                    return GroupType.Group0;
                }
                else if(type == GroupTypeSecurityAgnostic.Group1)
                {
                    var securityState = cpu.SecurityState;
                    if(gic.DisabledSecurity || securityState == SecurityState.NonSecure)
                    {
                        return GroupType.Group1NonSecure;
                    }
                    else if(securityState == SecurityState.Secure)
                    {
                        return GroupType.Group1Secure;
                    }
                }
                throw new ArgumentOutOfRangeException($"There is no valid InterruptGroupType for value: {type}.");
            }

            public void RegisterLegacySGIRequester(CPUEntry cpu)
            {
                if(SoftwareGeneratedInterruptsLegacyRequester.ContainsKey(cpu))
                {
                    throw new ArgumentException($"The CPU ({cpu}) was already registered as a legacy requester.");
                }
                SoftwareGeneratedInterruptsLegacyRequester[cpu] = GenerateSGIs(SoftwareGeneratedInterruptsConfig, cpu);
            }

            public bool IsVirtualized()
            {
                if(cpu.SecurityState != SecurityState.NonSecure || cpu.ExceptionLevel > ExceptionLevel.EL1_SystemMode)
                {
                    return false;
                }
                return cpu.FIQMaskOverride || cpu.IRQMaskOverride;
            }

            public bool IsVirtualized(GroupType type)
            {
                if(cpu.SecurityState != SecurityState.NonSecure || cpu.ExceptionLevel > ExceptionLevel.EL1_SystemMode)
                {
                    return false;
                }
                if(type == GroupType.Group0)
                {
                    return cpu.FIQMaskOverride;
                }
                if(type == GroupType.Group1)
                {
                    return cpu.IRQMaskOverride;
                }
                throw new ArgumentOutOfRangeException($"There is no valid InterruptGroupType for value: {type}.");
            }

            public QuadWordRegisterCollection RedistributorQuadWordRegisters => redistributorQuadWordRegisters;
            public DoubleWordRegisterCollection RedistributorDoubleWordRegisters => redistributorDoubleWordRegisters;

            public event Action<CPUEntry, int, bool> PrivateInterruptChanged;

            public IReadOnlyDictionary<InterruptId, Interrupt> PrivatePeripheralInterrupts { get; }
            public IReadOnlyDictionary<InterruptId, InterruptConfig> SoftwareGeneratedInterruptsConfig { get; }
            public IReadOnlyDictionary<InterruptId, SoftwareGeneratedInterrupt> SoftwareGeneratedInterruptsUnknownRequester;
            public Dictionary<CPUEntry, ReadOnlyDictionary<InterruptId, SoftwareGeneratedInterrupt>> SoftwareGeneratedInterruptsLegacyRequester { get; }

            public IEnumerable<Interrupt> AllPrivateAndSoftwareGeneratedInterrupts => PrivatePeripheralInterrupts.Values
                .Concat(SoftwareGeneratedInterruptsUnknownRequester.Values)
                .Concat(SoftwareGeneratedInterruptsLegacyRequester.Values.SelectMany(x => x.Values));
            public IEnumerable<InterruptConfig> AllPrivateAndSoftwareGeneratedInterruptsConfigs => PrivatePeripheralInterrupts.Values.Select(irq => irq.Config)
                .Concat(SoftwareGeneratedInterruptsConfig.Values);

            public GroupCollection Groups { get; }

            public bool IsSleeping
            {
                get => isSleeping;
                set
                {
                    var changed = isSleeping != value;
                    if(changed)
                    {
                        isSleeping = value;
                        gic.ClearForcedTargettingCache();
                    }
                }
            }

            public bool IsParticipatingInRouting => !IsSleeping;
            public virtual string CurrentCPUSecurityStateString => $"state: {cpu.SecurityState}";
            public virtual EndOfInterruptModes CurrentEndOfInterruptMode => EndOfInterruptModeEL1;

            public EndOfInterruptModes EndOfInterruptModeEL1
            {
                get => IsVirtualized()
                    ? EndOfInterruptModeVirtual
                    : gic.DisabledSecurity || IsStateSecure ? EndOfInterruptModeEL1Secure : EndOfInterruptModeEL1NonSecure;
                set
                {
                    if(IsVirtualized())
                    {
                        EndOfInterruptModeVirtual = value;
                    }
                    else if(gic.DisabledSecurity || IsStateSecure)
                    {
                        EndOfInterruptModeEL1Secure = value;
                    }
                    else
                    {
                        EndOfInterruptModeEL1NonSecure = value;
                    }
                }
            }

            public EndOfInterruptModes EndOfInterruptModeEL1NonSecure { get; set; }
            public EndOfInterruptModes EndOfInterruptModeEL1Secure { get; set; }
            public EndOfInterruptModes EndOfInterruptModeVirtual { get; set; }
            public bool IsStateSecure => cpu.SecurityState == SecurityState.Secure;
            public string Name { get; }
            public bool VirtualCPUInterfaceEnabled { get; set; }
            public bool VirtualFIQEnabled { get; set; }

            public Interrupt BestPending { get; set; }
            public Interrupt BestPendingVirtual { get; set; }
            public VirtualInterrupt[] VirtualInterrupts { get; }
            public InterruptPriority PriorityMask
            {
                get => IsVirtualized() ? VirtualPriorityMask : PhysicalPriorityMask;
                set
                {
                    if(IsVirtualized())
                    {
                        VirtualPriorityMask = value;
                    }
                    else
                    {
                        PhysicalPriorityMask = value;
                    }
                }
            }
            public InterruptPriority VirtualPriorityMask { get; set; }
            public InterruptPriority PhysicalPriorityMask { get; set; }
            public RunningInterrupts RunningInterrupts { get; }
            public uint EOICount { get; private set; }
            public NonSecureAccess[] NonSecureSGIAccess { get; } = new NonSecureAccess[InterruptsDecoder.SoftwareGeneratedCount];

            public readonly uint processorNumber;
            public readonly uint targetFieldFlag;
            public readonly Affinity affinity;

            public const int EOICountWidth = 5;
            public const int EOICountMask = (1 << EOICountWidth) - 1;

            protected virtual InterruptSignalType GetBestPendingInterruptSignalType()
            {
                if(BestPending == null)
                {
                    if(VirtualFIQEnabled && BestPendingVirtual.Config.GroupType == GroupType.Group0)
                    {
                        return InterruptSignalType.vFIQ;
                    }
                    return InterruptSignalType.vIRQ;
                }
                if(gic.enableFIQ && BestPending.Config.GroupType == GroupType.Group0)
                {
                    return InterruptSignalType.FIQ;
                }
                return InterruptSignalType.IRQ;
            }

            protected readonly ARM_GenericInterruptController gic;

            private ReadOnlyDictionary<InterruptId, SoftwareGeneratedInterrupt> GenerateSGIs(IReadOnlyDictionary<InterruptId, InterruptConfig> configs, CPUEntry requester)
            {
                return new ReadOnlyDictionary<InterruptId, SoftwareGeneratedInterrupt>(configs.ToDictionary(config => config.Key, config => new SoftwareGeneratedInterrupt(config.Key, config.Value, requester)));
            }

            private void DeactivateInterrupt(Interrupt interrupt, bool isVirtualized)
            {
                gic.Log(LogLevel.Debug, "Deactivating interrupt with id {0}", (uint)interrupt.Identifier);
                interrupt.State.Active = false;

                if(isVirtualized)
                {
                    var virtualInterrupt = (VirtualInterrupt)interrupt;
                    var lrEntry = VirtualInterrupts.SingleOrDefault(x => x.Identifier.Equals(interrupt.Identifier));
                    if(lrEntry == null)
                    {
                        EOICount = (EOICount + 1) & EOICountMask;
                    }
                    else
                    {
                        lrEntry.Sync(virtualInterrupt);
                    }
                    RunningInterrupts.RemoveActiveVirtual(virtualInterrupt.Identifier);
                    if(virtualInterrupt.Hardware)
                    {
                        var physicalIrq = gic.GetAllInterrupts(this).SingleOrDefault(x => x.Identifier.Equals(virtualInterrupt.HardwareIdentifier));
                        if(physicalIrq != null)
                        {
                            physicalIrq.State.Active = false;
                        }
                    }
                }
            }

            private bool IsSoftwareGeneratedInterruptAccessValid(Interrupt interrupt, CPUEntry accessingCPU, string registerTypeName)
            {
                if(interrupt is SoftwareGeneratedInterrupt sgi)
                {
                    if(sgi.Requester != null && sgi.Requester != accessingCPU && !gic.IsAffinityRoutingEnabled(sgi.Requester))
                    {
                        var logMessage = "{0}: Incorrect Processor Number {1} passed for SGI ({2}), expected to be {3}.";
                        if(gic.ArchitectureVersionAtLeast3)
                        {
                            logMessage += " Request will be ignored.";
                        }
                        gic.Log(LogLevel.Warning, logMessage, registerTypeName, accessingCPU.affinity, interrupt.Identifier, sgi.Requester.affinity);

                        if(gic.ArchitectureVersionAtLeast3)
                        {
                            /*
                             * The documentation is not clear what happens here, if we are in SMP system, and there is a CPUID mismatch
                             * -> For GICv3 the whole field of INTID (bits 23:0) should be passed, which contains CPUID in bits 12:10 - so it's safe to abort if they mismatch
                             * -> For GICv2 it's not clear, but "For every read of a valid Interrupt ID from the GICC_IAR, the connected processor must perform a matching write to the GICC_EOIR.
                             *    The value written to the GICC_EOIR must be the interrupt ID read from the GICC_IAR."
                             *    So we stay permissive, and still allow the request to go through
                             */
                            return false;
                        }
                    }
                }
                else if(accessingCPU != null && accessingCPU.processorNumber != 0)
                {
                    gic.Log(LogLevel.Debug, "{0}: Processor Number ({1}) passed for non-SGI interrupt ({2}).", registerTypeName, accessingCPU.processorNumber, interrupt.Identifier);
                }
                return true;
            }

            private Dictionary<long, QuadWordRegister> BuildRedistributorQuadWordRegisterMap()
            {
                var registerMap = new Dictionary<long, QuadWordRegister>
                {
                    {(long)RedistributorRegisters.ControllerType, new QuadWordRegister(this)
                        .WithValueField(32, 32, FieldMode.Read, name: "CPUAffinity",
                            valueProviderCallback: _ => this.affinity.AllLevels
                        )
                        .WithValueField(27, 5, FieldMode.Read, name: "MaximumPrivatePeripheralInterruptIdentifier",
                            valueProviderCallback: _ => 0b00 // The maximum PPI identifier is 31, because the GIC doesn't support an extended range of PPI
                        )
                        .WithFlag(26, FieldMode.Read, name: "DirectSoftwareGeneratedInterruptInjectionSupport",
                            valueProviderCallback: _ => false
                        )
                        .WithTag("LocalitySpecificInterruptConfigurationSharing", 24, 2)
                        .WithValueField(8, 16, FieldMode.Read, name: "ProcessorNumber",
                            valueProviderCallback: _ => this.processorNumber
                        )
                        .WithTaggedFlag("vPEResidentIndicator", 7)
                        .WithFlag(6, FieldMode.Read, name: "MPAMSupport",
                            valueProviderCallback: _ => false
                        )
                        .WithFlag(5, FieldMode.Read, name: "DPGSupport",
                            valueProviderCallback: _ => false
                        )
                        .WithFlag(4, FieldMode.Read, name: "HighestRedistributorInSeries", valueProviderCallback: _ =>
                        {
                            var redistributorsNotLowerThanCurrent = gic.GetRedistributorRegistrations().OrderBy(redist => redist.Range.StartAddress).SkipWhile(x => x.Cpu != cpu);
                            // there must exist a corresponding redistributor for this CPUEntry, as otherwise we wouldn't be able to access this register
                            var currentRedistributor = redistributorsNotLowerThanCurrent.First();
                            var nextRedistributor = redistributorsNotLowerThanCurrent.Skip(1).FirstOrDefault();
                            // return true for the highest redistrubutor in the contiguous region
                            return nextRedistributor == null || (nextRedistributor.Range.StartAddress - currentRedistributor.Range.EndAddress) > 1;
                        })
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

                return registerMap;
            }

            private Dictionary<long, DoubleWordRegister> BuildRedistributorDoubleWordRegisterMap()
            {
                var registerMap = new Dictionary<long, DoubleWordRegister>
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
                    {(long)RedistributorRegisters.ImplementerIdentification, new DoubleWordRegister(this)
                        .WithValueField(24, 8, FieldMode.Read, name: "ProductID", valueProviderCallback: _ => gic.RedistributorProductIdentifier)
                        .WithReservedBits(20, 4)
                        .WithValueField(16, 4, FieldMode.Read, name: "Variant", valueProviderCallback: _ => gic.RedistributorVariant)
                        .WithValueField(12, 4, FieldMode.Read, name: "Revision", valueProviderCallback: _ => gic.RedistributorRevision)
                        .WithValueField(0, 12, FieldMode.Read, name: "Implementer", valueProviderCallback: _ => gic.RedistributorImplementer)
                    },
                    {(long)RedistributorRegisters.Wake, new DoubleWordRegister(this, 0x1)
                        // "There is only one GICR_WAKER.Sleep and one GICR_WAKER.Quiescent bit that can be read and written through the GICR_WAKER register of any Redistributor."
                        .WithTaggedFlag("Quiescent", 31)
                        .WithReservedBits(3, 28)
                        .WithFlag(2, FieldMode.Read, name: "ChildrenAsleep",
                            valueProviderCallback: _ => this.IsSleeping
                        )
                        .WithFlag(1, name: "ProcessorSleep",
                            writeCallback: (_, val) => this.IsSleeping = val,
                            valueProviderCallback: _ => this.IsSleeping
                        )
                        .WithTaggedFlag("Sleep", 0)
                        // According to the reference manual this register should be RAZ/WI for an unsecure access, but we just show a warning in such a situation.
                        .WithWriteCallback((_, __) => { if(!gic.DisabledSecurity && !this.IsStateSecure) this.Log(LogLevel.Warning, "Writing to the GICR_WAKER register from wrong security state."); })
                        .WithReadCallback((_, __) => { if(!gic.DisabledSecurity && !this.IsStateSecure) this.Log(LogLevel.Warning, "Reading from the GICR_WAKER register from wrong security state."); })
                    },
                    {(long)RedistributorRegisters.NonSecureAccessControl, new DoubleWordRegister(this)
                        .WithEnumFields<DoubleWordRegister, NonSecureAccess>(0, 2, 16, name: "NS_access",
                            writeCallback: (i, _, val) =>
                            {
                                if(!gic.IsAffinityRoutingEnabled(this))
                                {
                                    NonSecureSGIAccess[i] = val;
                                }
                            },
                            valueProviderCallback: (i, _) => !gic.IsAffinityRoutingEnabled(this) ? (NonSecureAccess)0 : NonSecureSGIAccess[i]
                        )
                        // Those could be emitted in valueProvider/writeCallback instead,
                        // but we don't want to emit the same warning 16 times per access.
                        .WithWriteCallback((_, __) =>
                        {
                            if(!gic.IsAffinityRoutingEnabled(this))
                            {
                                this.Log(LogLevel.Warning, "Tried to write to GICR_NSACR when affinity routing is disabled. Access ignored, use GICD_NSACR0 instead.");
                            }
                        })
                        .WithReadCallback((_, __) =>
                        {
                            if(!gic.IsAffinityRoutingEnabled(this))
                            {
                                this.Log(LogLevel.Warning, "Tried to read from GICR_NSACR when affinity routing is disabled. Access ignored, use GICD_NSACR0 instead.");
                            }
                        })
                    },
                    {gic.PeripheralIdentificationOffset, new DoubleWordRegister(this)
                        .WithReservedBits(8, 24)
                        .WithEnumField<DoubleWordRegister, ARM_GenericInterruptControllerVersion>(4, 4, FieldMode.Read, name: "ArchitectureVersion",
                            valueProviderCallback: _ => gic.ArchitectureVersion
                        )
                        .WithTag("ImplementationDefinedIdentificator", 0, 4)
                    }
                };

                Utils.AddRegistersAtOffset(registerMap, (long)RedistributorRegisters.InterruptSetEnable_0,
                    gic.BuildInterruptSetEnableRegisters(gic.IrqsDecoder.SoftwareGeneratedFirst, gic.IrqsDecoder.PrivatePeripheralLast, "InterruptSetEnable",
                        cpuEntryProvider: () => this)
                );

                Utils.AddRegistersAtOffset(registerMap, (long)RedistributorRegisters.InterruptClearEnable_0,
                    gic.BuildInterruptClearEnableRegisters(gic.IrqsDecoder.SoftwareGeneratedFirst, gic.IrqsDecoder.PrivatePeripheralLast, "InterruptClearEnable",
                        cpuEntryProvider: () => this)
                );

                Utils.AddRegistersAtOffset(registerMap, (long)RedistributorRegisters.InterruptClearPending_0,
                    gic.BuildInterruptClearPendingRegisters(gic.IrqsDecoder.SoftwareGeneratedFirst, gic.IrqsDecoder.PrivatePeripheralLast, "InterruptClearPending",
                        cpuEntryProvider: () => this)
                );

                Utils.AddRegistersAtOffset(registerMap, (long)RedistributorRegisters.InterruptPriority_0,
                    gic.BuildInterruptPriorityRegisters(gic.IrqsDecoder.SoftwareGeneratedFirst, gic.IrqsDecoder.PrivatePeripheralLast, "InterruptPriority",
                        cpuEntryProvider: () => this)
                );

                Utils.AddRegistersAtOffset(registerMap, (long)RedistributorRegisters.PrivatePeripheralInterruptConfiguration,
                    gic.BuildInterruptConfigurationRegisters(gic.IrqsDecoder.PrivatePeripheralFirst, gic.IrqsDecoder.PrivatePeripheralLast, "PrivatePeripheralInterruptConfiguration",
                        cpuEntryProvider: () => this)
                );

                Utils.AddRegistersAtOffset(registerMap, (long)RedistributorRegisters.InterruptGroup_0,
                    gic.BuildInterruptGroupRegisters(gic.IrqsDecoder.SoftwareGeneratedFirst, gic.IrqsDecoder.PrivatePeripheralLast, "InterruptGroup",
                        cpuEntryProvider: () => this)
                );

                Utils.AddRegistersAtOffset(registerMap, (long)RedistributorRegisters.InterruptGroupModifier_0,
                    gic.BuildInterruptGroupModifierRegisters(gic.IrqsDecoder.SoftwareGeneratedFirst, gic.IrqsDecoder.PrivatePeripheralLast, "InterruptGroupModifier",
                        cpuEntryProvider: () => this)
                );

                return registerMap;
            }

            private bool isSleeping = true;
            private readonly IARMSingleSecurityStateCPU cpu;
            private readonly IReadOnlyDictionary<InterruptSignalType, IGPIO> interruptSignals;
            private readonly QuadWordRegisterCollection redistributorQuadWordRegisters;
            private readonly DoubleWordRegisterCollection redistributorDoubleWordRegisters;

            public class GroupCollection
            {
                public GroupCollection(CPUEntry cpu, IEnumerable<GroupType> groupTypes)
                {
                    this.cpu = cpu;
                    Physical = new ReadOnlyDictionary<GroupType, InterruptGroup>(groupTypes.ToDictionary(type => type, _ => new InterruptGroup()));
                    Virtual = new ReadOnlyDictionary<GroupType, InterruptGroup>(groupTypes.ToDictionary(type => type, _ => new InterruptGroup()));
                }

                public void Reset()
                {
                    foreach(var group in Physical.Values)
                    {
                        group.Reset();
                    }
                    foreach(var group in Virtual.Values)
                    {
                        group.Reset();
                    }
                }

                public InterruptGroup this[GroupType type] => cpu.IsVirtualized(type) ? Virtual[type] : Physical[type];
                public IReadOnlyDictionary<GroupType, InterruptGroup> Physical { get; }
                public IReadOnlyDictionary<GroupType, InterruptGroup> Virtual { get; }

                private readonly CPUEntry cpu;
            }
        }

        public class RunningInterrupts
        {
            public RunningInterrupts(CPUEntry cpu)
            {
                this.cpu = cpu;
                Physical = new Stack<Interrupt>();
                Virtual = new Stack<VirtualInterrupt>();
                activeVirtual = new Dictionary<InterruptId, VirtualInterrupt>();
            }

            public void Clear()
            {
                Physical.Clear();
                Virtual.Clear();
                activeVirtual.Clear();
            }

            public void Push(GroupType type, Interrupt interrupt)
            {
                if(!interrupt.State.Active)
                {
                    throw new ArgumentException("Trying to push inactive interrupt to interrupt stack");
                }
                if(cpu.IsVirtualized(type))
                {
                    if(interrupt is VirtualInterrupt virtualInterrupt)
                    {
                        // GICv3 only has a maximum of 16 List Registers.
                        // To allow the hypervisor to manage more virtual interrutps
                        // than the implementation has list registers,
                        // the LRs only act as a "cache" of the virtual interrupts.
                        // The only requirement is that for a pending virtual interrupt to be raised,
                        // it has to be in a list register.
                        // This means that once we're acknowledging the interrupt, we're "cutting it off"
                        // from the List Register, and the hypervisor is free to
                        // reuse that LR to schedule another interrupt.
                        // However, both the active and the list-register interrupts,
                        // provided they have the same virtual identifier set,
                        // have to be kept in sync, which is what the VirtualInterrupt.Sync method is used for.
                        var cloned = virtualInterrupt.Clone();
                        Virtual.Push(cloned);
                        activeVirtual.Add(cloned.Identifier, cloned);
                    }
                    else
                    {
                        throw new ArgumentException("Trying to push physical interrupt to virtual interrupt stack");
                    }
                }
                else
                {
                    if(interrupt is VirtualInterrupt)
                    {
                        throw new ArgumentException("Trying to push virtual interrupt to physical interrupt stack");
                    }
                    else
                    {
                        Physical.Push(interrupt);
                    }
                }
            }

            public Interrupt Pop(GroupType type)
            {
                return cpu.IsVirtualized(type) ? Virtual.Pop() : Physical.Pop();
            }

            public Interrupt Peek(GroupType type)
            {
                return cpu.IsVirtualized(type) ? Virtual.Peek() : Physical.Peek();
            }

            public int Count(GroupType type)
            {
                return cpu.IsVirtualized(type) ? Virtual.Count : Physical.Count;
            }

            public VirtualInterrupt GetActiveVirtual(InterruptId id)
            {
                VirtualInterrupt result;
                var present = activeVirtual.TryGetValue(id, out result);
                return present ? result : null;
            }

            public void RemoveActiveVirtual(InterruptId id)
            {
                activeVirtual.Remove(id);
            }

            public InterruptPriority Priority => cpu.IsVirtualized() ? VirtualPriority : PhysicalPriority;
            public Stack<Interrupt> Physical { get; }
            public InterruptPriority PhysicalPriority => Physical.Count > 0 ? Physical.Peek().Config.Priority : InterruptPriority.Idle;
            public Stack<VirtualInterrupt> Virtual { get; }
            public InterruptPriority VirtualPriority => Virtual.Count > 0 ? Virtual.Peek().Config.Priority : InterruptPriority.Idle;

            private readonly Dictionary<InterruptId, VirtualInterrupt> activeVirtual;
            private readonly CPUEntry cpu;
        }

        public class CPUEntryWithTwoSecurityStates : CPUEntry
        {
            public CPUEntryWithTwoSecurityStates(ARM_GenericInterruptController gic, IARMTwoSecurityStatesCPU cpu, IEnumerable<GroupType> groupTypes, IReadOnlyDictionary<InterruptSignalType, IGPIO> interruptConnections)
                : base(gic, cpu, groupTypes, interruptConnections)
            {
                this.cpu = cpu;
            }

            public override void Reset()
            {
                EndOfInterruptModeEL3 = EndOfInterruptModes.PriorityDropAndDeactivation;
                base.Reset();
            }

            public override InterruptId AcknowledgeBestPending(GroupTypeSecurityAgnostic groupTypeRegister, out CPUEntry sgiRequestingCPU)
            {
                sgiRequestingCPU = null;
                if(BestPending != null && groupTypeRegister == GroupTypeSecurityAgnostic.Group0 && cpu.ExceptionLevel == ExceptionLevel.EL3_MonitorMode)
                {
                    if(BestPending.Config.GroupType == GroupType.Group1Secure)
                    {
                        return gic.IrqsDecoder.ExpectedToHandleAtSecure;
                    }
                    else if(BestPending.Config.GroupType == GroupType.Group1NonSecure)
                    {
                        return gic.IrqsDecoder.ExpectedToHandleAtNonSecure;
                    }
                }
                return base.AcknowledgeBestPending(groupTypeRegister, out sgiRequestingCPU);
            }

            public override string CurrentCPUSecurityStateString => $"{cpu.ExceptionLevel}, {cpu.SecurityState}" + (gic.DisabledSecurity ? " (GIC security disabled)" : "");
            public override EndOfInterruptModes CurrentEndOfInterruptMode => cpu.ExceptionLevel == ExceptionLevel.EL3_MonitorMode ? EndOfInterruptModeEL3 : EndOfInterruptModeEL1;
            public EndOfInterruptModes EndOfInterruptModeEL3 { get; set; }

            protected override InterruptSignalType GetBestPendingInterruptSignalType()
            {
                // Based on the "4.6.2 Interrupt assignment to IRQ and FIQ signals" subsection of GICv3 and GICv4 Architecture Specification
                if(BestPending == null || cpu.HasSingleSecurityState || gic.DisabledSecurity)
                {
                    return base.GetBestPendingInterruptSignalType();
                }

                var groupType = BestPending.Config.GroupType;
                cpu.GetAtomicExceptionLevelAndSecurityState(out var exceptionLevel, out var securityState);
                if(gic.enableFIQ && (groupType == GroupType.Group0
                    || (groupType == GroupType.Group1NonSecure && securityState == SecurityState.Secure) // Includes the case when the current EL is EL3_MonitorMode
                    || (groupType == GroupType.Group1Secure && securityState == SecurityState.NonSecure)
                    || (groupType == GroupType.Group1Secure && exceptionLevel == ExceptionLevel.EL3_MonitorMode && !cpu.IsEL3UsingAArch32State)))
                {
                    return InterruptSignalType.FIQ;
                }
                return InterruptSignalType.IRQ;
            }

            private readonly IARMTwoSecurityStatesCPU cpu;
        }

        public class InterruptState
        {
            public InterruptState()
            {
                TriggerType = DefaultTriggerType;
            }

            public virtual void Reset()
            {
                Active = false;
                Pending = false;
                TriggerType = DefaultTriggerType;
            }

            public void Acknowledge()
            {
                if(!Pending || Active)
                {
                    throw new InvalidOperationException("It's invalid to acknowledge an interrupt not in the pending state.");
                }
                if(TriggerType == InterruptTriggerType.EdgeTriggered)
                {
                    Active = true;
                    Pending = false;
                }
                else if(TriggerType == InterruptTriggerType.LevelSensitive)
                {
                    Active = true;
                    Pending = true;
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

            public bool Active { get; set; }
            public bool IsInactive => !Active && !Pending;
            public bool Pending { get; set; }
            public ulong Bits
            {
                get => (Active ? 0b10ul : 0) | (Pending ? 0b01ul : 0);
                set
                {
                    bool active = (value & 0b10) != 0;
                    bool pending = (value & 1) != 0;
                    Active = active;
                    Pending = pending;
                }
            }
            public virtual InterruptTriggerType TriggerType { get; set; }

            protected virtual InterruptTriggerType DefaultTriggerType => default(InterruptTriggerType);
        }

        public class InterruptConfig
        {
            public virtual void Reset()
            {
                Enabled = false;
                GroupBit = false;
                GroupModifierBit = false;
                Priority = default(InterruptPriority);
            }

            public bool Enabled { get; set; }
            public bool GroupBit { get; set; }
            public bool GroupModifierBit { get; set; }
            public InterruptPriority Priority { get; set; }

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

        public class Interrupt
        {
            public Interrupt(InterruptId identifier) : base()
            {
                Identifier = identifier;
            }

            public virtual void Reset()
            {
                Config.Reset();
                State.Reset();
            }

            public InterruptId Identifier { get; protected set; }
            public virtual InterruptConfig Config { get; } = new InterruptConfig();
            public virtual InterruptState State { get; } = new InterruptState();
        }

        public class SoftwareGeneratedInterruptState : InterruptState
        {
            public override InterruptTriggerType TriggerType
            {
                get => DefaultTriggerType;
                set
                {
                    if(value != TriggerType)
                    {
                        throw new InvalidOperationException("SoftwareGeneratedInterrupt has constant trigger type.");
                    }
                }
            }

            protected override InterruptTriggerType DefaultTriggerType => InterruptTriggerType.EdgeTriggered;
        }

        public class VirtualInterrupt : Interrupt
        {
            public VirtualInterrupt() : base(new InterruptId(0))
            {
                Reset();
            }

            public override void Reset()
            {
                base.Reset();
                State.TriggerType = InterruptTriggerType.EdgeTriggered;
                Identifier = new InterruptId(0);
                HardwareIdentifier = new InterruptId(0);
                Hardware = false;
            }

            // Performs a deep clone of the Virtual Interrupt.
            public VirtualInterrupt Clone()
            {
                var cloned = new VirtualInterrupt();
                cloned.Sync(this);
                return cloned;
            }

            public void SetIdentifier(uint id)
            {
                Identifier = new InterruptId(id);
            }

            public void SetHardwareIdentifier(uint id)
            {
                HardwareIdentifier = new InterruptId(id);
            }

            public void Sync(VirtualInterrupt other)
            {
                this.Identifier = other.Identifier;
                this.HardwareIdentifier = other.HardwareIdentifier;
                this.Hardware = other.Hardware;
                this.State.Bits = other.State.Bits;
                this.Config.GroupBit = other.Config.GroupBit;
                this.Config.Priority = other.Config.Priority;
            }

            public InterruptId HardwareIdentifier { get; protected set; }
            public bool Hardware { get; set; }

            public bool EOIMaitenance => !Hardware && EOIMaintenanceBit;

            private bool EOIMaintenanceBit => ((uint)HardwareIdentifier & (1 << 9)) != 0;
        }

        public class SoftwareGeneratedInterrupt : Interrupt
        {
            public SoftwareGeneratedInterrupt(InterruptId irqId, InterruptConfig config, CPUEntry requester) : base(irqId)
            {
                // Software Generated Interrupts with a same requester share a config.
                Config = config;
                Requester = requester;
            }

            public override InterruptConfig Config { get; }
            public override InterruptState State { get; } = new SoftwareGeneratedInterruptState();
            // The null value indicates that the Requester is unknown, what is typical for a GICv3 with Affinity Routing enabled.
            public CPUEntry Requester { get; }
        }

        public class SharedInterrupt : Interrupt
        {
            public SharedInterrupt(InterruptId irqId) : base(irqId) { }

            public override void Reset()
            {
                base.Reset();
                TargetCPUs = 0;
            }

            public bool IsLegacyRoutingTargetingCPU(CPUEntry cpu)
            {
                return (TargetCPUs & cpu.targetFieldFlag) != 0;
            }

            public bool IsLowestLegacyRoutingTargettedCPU(CPUEntry cpu)
            {
                return (TargetCPUs & (cpu.targetFieldFlag - 1)) == 0;
            }

            public bool IsAffinityRoutingTargetingCPU(CPUEntry cpu)
            {
                if(RoutingMode == InterruptRoutingMode.AnyTarget || TargetAffinity.AllLevels == cpu.affinity.AllLevels)
                {
                    return cpu.IsParticipatingInRouting;
                }
                return false;
            }

            public bool IsLowestAffinityRoutingTargettedCPU(CPUEntry cpu, ARM_GenericInterruptController gic)
            {
                return cpu.IsParticipatingInRouting && (RoutingMode == InterruptRoutingMode.SpecifiedTarget ? TargetAffinity.AllLevels == cpu.affinity.AllLevels : cpu == gic.ForcedTargettingCpuForAffinityRouting);
            }

            public byte TargetCPUs { get; set; }
            public MutableAffinity TargetAffinity { get; } = new MutableAffinity();
            public InterruptRoutingMode RoutingMode { get; set; }
        }

        // This class will be extended at least for the Binary Point register support
        // It may be needed to separate it for CPUInterface and Distributor
        public class InterruptGroup
        {
            public void Reset()
            {
                Enabled = false;
            }

            public bool Enabled { get; set; }
        }

        public struct InterruptId
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

            public override string ToString()
            {
                return $"{id}";
            }

            private readonly uint id;
        }

        public class InterruptsDecoder
        {
            public InterruptsDecoder(uint sharedPeripheralCount, uint identifierBits)
            {
                this.sharedPeripheralCount = sharedPeripheralCount;
                this.identifierBits = identifierBits;

                sharedPeripheralLast = new InterruptId((uint)SharedPeripheralFirst + sharedPeripheralCount - 1);
                extendedSharedPeripheralLast = new InterruptId((uint)extendedSharedPeripheralFirst + SharedPeripheralExtendedCount - 1);
            }

            public InterruptType Type(InterruptId id)
            {
                if(IsSoftwareGenerated(id))
                {
                    return InterruptType.SoftwareGenerated;
                }
                else if(IsPrivatePeripheral(id))
                {
                    return InterruptType.PrivatePeripheral;
                }
                else if(IsSharedPeripheral(id))
                {
                    return InterruptType.SharedPeripheral;
                }
                else if(IsSpecial(id))
                {
                    return InterruptType.SpecialIdentifier;
                }
                else if(IsLocalitySpecificPeripheral(id))
                {
                    return InterruptType.LocalitySpecificPeripheral;
                }
                return InterruptType.Reserved;
            }

            public bool IsSoftwareGenerated(InterruptId id) => (uint)id <= (uint)SoftwareGeneratedLast;

            public bool IsPrivatePeripheral(InterruptId id) => ((uint)PrivatePeripheralFirst <= (uint)id && (uint)id <= (uint)PrivatePeripheralLast)
                || ((uint)ExtendedPrivatePeripheralFirst <= (uint)id && (uint)id <= (uint)ExtendedPrivatePeripheralLast);

            public bool IsSharedPeripheral(InterruptId id) => ((uint)SharedPeripheralFirst <= (uint)id && (uint)id <= (uint)SharedPeripheralLast)
                || ((uint)ExtendedSharedPeripheralFirst <= (uint)id && (uint)id <= (uint)ExtendedSharedPeripheralLast);

            public bool IsSpecial(InterruptId id) => (uint)ExpectedToHandleAtSecure <= (uint)id && (uint)id <= (uint)NoPending;

            public bool IsLocalitySpecificPeripheral(InterruptId id) => (uint)id <= (uint)LocalitySpecificPeripheralFirst;

            public InterruptId SoftwareGeneratedFirst => softwareGeneratedFirst;
            public InterruptId SoftwareGeneratedLast => softwareGeneratedLast;
            public InterruptId PrivatePeripheralFirst => privatePeripheralFirst;
            public InterruptId PrivatePeripheralLast => privatePeripheralLast;
            public InterruptId SharedPeripheralFirst => sharedPeripheralFirst;
            public InterruptId SharedPeripheralLast => sharedPeripheralLast;
            public InterruptId ExpectedToHandleAtSecure => expectedToHandleAtSecure;
            public InterruptId ExpectedToHandleAtNonSecure => expectedToHandleAtNonSecure;
            public InterruptId NonMaskableInterruptOrGICv2GroupMismatch => nonMaskableInterruptOrGICv2GroupMismatch;
            public InterruptId NoPending => noPending;
            public InterruptId ExtendedPrivatePeripheralFirst => extendedPrivatePeripheralFirst;
            public InterruptId ExtendedPrivatePeripheralLast => extendedPrivatePeripheralLast;
            public InterruptId ExtendedSharedPeripheralFirst => extendedSharedPeripheralFirst;
            public InterruptId ExtendedSharedPeripheralLast => extendedSharedPeripheralLast;
            public InterruptId LocalitySpecificPeripheralFirst => localitySpecificPeripheralFirst;
            public uint IdentifierBits => identifierBits;

            public readonly uint SharedPeripheralExtendedCount = 1024;

            private readonly InterruptId softwareGeneratedFirst = new InterruptId(0);
            private readonly InterruptId softwareGeneratedLast = new InterruptId(15);
            private readonly InterruptId privatePeripheralFirst = new InterruptId(16);
            private readonly InterruptId privatePeripheralLast = new InterruptId(31);
            private readonly InterruptId sharedPeripheralFirst = new InterruptId(32);
            private readonly InterruptId sharedPeripheralLast; // set in the constructor
            private readonly InterruptId expectedToHandleAtSecure = new InterruptId(1020);
            private readonly InterruptId expectedToHandleAtNonSecure = new InterruptId(1021);
            private readonly InterruptId nonMaskableInterruptOrGICv2GroupMismatch = new InterruptId(1022);
            private readonly InterruptId noPending = new InterruptId(1023);
            private readonly InterruptId extendedPrivatePeripheralFirst = new InterruptId(1056);
            private readonly InterruptId extendedPrivatePeripheralLast = new InterruptId(1119);
            private readonly InterruptId extendedSharedPeripheralFirst = new InterruptId(4096);
            private readonly InterruptId extendedSharedPeripheralLast; // set in the constructor
            private readonly InterruptId localitySpecificPeripheralFirst = new InterruptId(819);

            private readonly uint sharedPeripheralCount;
            private readonly uint identifierBits;

            public const uint MaximumSharedPeripheralCount = 988;
            public const uint SoftwareGeneratedCount = 16;
        }

        public struct SoftwareGeneratedInterruptRequest
        {
            public SoftwareGeneratedInterruptRequest(TargetType type, Affinity[] list, GroupType group, InterruptId id)
            {
                TargetCPUsType = type;
                TargetsList = list;
                TargetGroup = group;
                InterruptId = id;
            }

            public TargetType TargetCPUsType { get; }
            public Affinity[] TargetsList { get; }
            public GroupType TargetGroup { get; }
            public InterruptId InterruptId { get; }

            public enum TargetType
            {
                TargetList = 0b00,
                AllCPUs = 0b01,
                Loopback = 0b10
            }
        }

        public enum NonSecureAccess
        {
            NotPermitted = 0b00,
            SecureGroup0Permitted = 0b01,
            BothGroupsPermitted = 0b10,
            Reserved = 0b11,
        }

        public enum EndOfInterruptModes
        {
            PriorityDropAndDeactivation = 0,
            PriorityDropOnly = 1,
        }

        public enum InterruptPriority : byte
        {
            Highest = 0x00,
            Idle = 0xFF
        }

        public enum InterruptTriggerType : byte
        {
            LevelSensitive = 0b00,
            EdgeTriggered = 0b10
        }

        public enum InterruptRoutingMode : byte
        {
            SpecifiedTarget = 0b0,
            AnyTarget = 0b1
        }

        public enum InterruptType
        {
            SoftwareGenerated,
            PrivatePeripheral,
            SharedPeripheral,
            SpecialIdentifier,
            LocalitySpecificPeripheral,
            Reserved
        }

        public enum GroupType
        {
            Group0,
            Group1NonSecure,
            Group1Secure,
            Group1 = Group1NonSecure,
        }

        public enum GroupTypeSecurityAgnostic
        {
            Group0,
            Group1
        }

        public enum DistributorRegisters : long
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
            InterruptPriority_254 = 0x07F8, // GICD_IPRIORITYR<n>
            InterruptProcessorTargets_0 = 0x0800, // GICD_ITARGETSR<n>
            InterruptProcessorTargets_8 = 0x0820, // GICD_ITARGETSR<n>
            InterruptProcessorTargets_254 = 0x0AF8, // GICD_ITARGETSR<n>
            InterruptConfiguration_0 = 0x0C00, // GICD_ICFGR<n>
            InterruptConfiguration_1 = 0x0C04, // GICD_ICFGR<n>
            InterruptGroupModifier_0_PPIStatus = 0x0D00, // GICD_IGRPMODR0 on GICv3, GICD_PPISR on GICv1/2 
            InterruptGroupModifier_1_SPIStatus_0 = 0x0D04, // GICD_IGRPMODR<n> on GICv3, GICD_SPISR<n> on GICv1/2 
            NonSecureAccessControl_0 = 0x0E00, // GICD_NSACR<n>
            SoftwareGeneratedInterruptControl = 0x0F00, // GICD_SGI
            SoftwareGeneratedIntrruptClearPending_0 = 0x0F10, // GICD_CPENDSGIR<n>
            SoftwareGeneratedIntrruptClearPending_3 = 0x0F1C, // GICD_CPENDSGIR<n>
            SoftwareGeneratedIntrruptSetPending_0 = 0x0F20, // GICD_SPENDSGIR<n>
            SoftwareGeneratedIntrruptSetPending_3 = 0x0F2C, // GICD_SPENDSGIR<n>
            NonMaskableInterrupt_0 = 0x0F80, // GICD_INMIR<n>
            PeripheralIdentification2_v1v2 = 0xFE8, // GICD_PIDR2 for GICv1 and GICv2
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
            SharedPeripheralInterruptExtendedRouting_0 = 0x8000, // GICD_IROUTER<n>E
            PeripheralIdentification2_v3v4 = 0xFFE8, // GICD_PIDR2 for GICv3 and GICv4
        }

        public enum RedistributorRegisters : long
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
            PeripheralIdentification2_v1v2 = 0xFE8, // GICR_PIDR2 for GICv1 and GICv2

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
            InterruptPriority_7 = 0x041C + RedistributorPrivateInterruptsFrameOffset, // GICR_IPRIORITYR<n>
            PrivatePeripheralInterruptExtendedPriority_0 = 0x0420 + RedistributorPrivateInterruptsFrameOffset, // GICR_IPRIORITYR<n>E
            SoftwareGeneratedInterruptConfiguration = 0x0C00 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICFGR0
            PrivatePeripheralInterruptConfiguration = 0x0C04 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICFGR1
            PrivatePeripheralInterruptExtendedConfiguration_0 = 0x0C08 + RedistributorPrivateInterruptsFrameOffset, // GICR_ICFGR<n>E
            InterruptGroupModifier_0 = 0x0D00 + RedistributorPrivateInterruptsFrameOffset, // GICR_IGRPMODR0
            PrivatePeripheralInterruptExtendedGroupModifier_0 = 0x0D04 + RedistributorPrivateInterruptsFrameOffset, // GICR_IGRPMODR<n>E
            NonSecureAccessControl = 0x0E00 + RedistributorPrivateInterruptsFrameOffset, // GICR_NSACR
            PrivatePeripheralInterruptNonMaskable = 0x0F80 + RedistributorPrivateInterruptsFrameOffset, // GICR_INMIR0
            PrivatePeripheralInterruptExtendeNonMaskable_0 = 0x0F84 + RedistributorPrivateInterruptsFrameOffset, // GICR_INMIR<n>E
            PeripheralIdentification2_v3v4 = 0xFFE8, // GICR_PIDR2 for GICv3 and GICv4
        }

        public enum CPUInterfaceRegisters : long
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

        // Those are used for both AArch32 and AArch64 registers. For AArch32,
        // the encoding is slightly modified (see encode_as_aarch64_register
        // in tlib for details) and the names are mostly the same except for
        // the '_ELx' suffix.
        public enum CPUInterfaceSystemRegisters : long
        {
            // Enum values are created from op0, op1, CRn, CRm and op2 fields of the MRS instruction
            SystemRegisterEnableEL3 = 0xF665, // ICC_SRE_EL3 / ICC_MSRE
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
            ControlEL3 = 0xF664, // ICC_CTLR_EL3 / ICC_MCTLR
            InterruptDeactivate = 0xC659, // ICC_DIR_EL1
            InterruptEndGroup0 = 0xC641, // ICC_EOIR0_EL1
            InterruptEndGroup1 = 0xC661, // ICC_EOIR1_EL1
            HighestPriorityPendingInterruptGroup0 = 0xC642, // ICC_HPPIR0_EL1
            HighestPriorityPendingInterruptGroup1 = 0xC662, // ICC_HPPIR1_EL1
            InterruptAcknowledgeGroup0 = 0xC640, // ICC_IAR0_EL1
            InterruptAcknowledgeGroup1 = 0xC660, // ICC_IAR1_EL1
            GroupEnable0 = 0xC666, // ICC_IGRPEN0_EL1
            GroupEnable1EL3 = 0xF667, // ICC_IGRPEN1_EL3 / ICC_MGRPEN1
            InterruptAcknowladgeNonMaskable = 0xC64D, // ICC_NMIAR1_EL1
            RunningPriority = 0xC65B, // ICC_RPR_EL1
            SoftwareGeneratedInterruptGroup0Generate = 0xC65F, // ICC_SGI0R_EL1
            SoftwareGeneratedInterruptGroup1Generate = 0xC65D, // ICC_SGI1R_EL1
            SystemRegisterEnableEL2 = 0xE64D, // ICC_SRE_EL2
            HypControl = 0xE658, // ICH_HCR_EL2
            VGICType = 0xE659, // ICH_VTR_EL2
            VMControl = 0xE65F, // ICH_VMCR_EL2
            ListRegister_0 = 0xE660, // ICH_LR<n>_EL2
            ListRegisterUpper_0 = 0xE670, // ICH_LRC<n> (Aarch32 only)
        }
    }

    public enum ARM_GenericInterruptControllerVersion : byte
    {
        Default = 255,
        GICv1 = 1,
        GICv2 = 2,
        GICv3 = 3,
        GICv4 = 4
    }
}
