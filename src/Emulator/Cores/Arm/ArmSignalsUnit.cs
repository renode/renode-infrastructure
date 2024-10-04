//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class CortexR5SignalsUnit : ArmSignalsUnit
    {
        public CortexR5SignalsUnit(IMachine machine) : base(machine, UnitType.CortexR5)
        {
            // Intentionally left empty.
        }
    }

    public class CortexR8SignalsUnit : ArmSignalsUnit
    {
        public CortexR8SignalsUnit(IMachine machine, ArmSnoopControlUnit snoopControlUnit)
            : base(machine, UnitType.CortexR8, snoopControlUnit)
        {
            // Intentionally left empty.
        }
    }

    public class ArmSignalsUnit : IPeripheral, ISignalsUnit
    {
        protected ArmSignalsUnit(IMachine machine, UnitType unitType, ArmSnoopControlUnit snoopControlUnit = null)
        {
            this.machine = machine;
            this.unitType = unitType;
            InitSignals(unitType);

            // SCU is required for Cortex-R8 PERIPHBASE logic but, for example, Cortex-R5 has neither PERIPHBASE nor is used with SCU.
            if(unitType == UnitType.CortexR8)
            {
                this.snoopControlUnit = snoopControlUnit
                    ?? throw new ConstructionException($"{nameof(snoopControlUnit)} is required in {unitType}SignalsUnit");

                // PeripheralsBase initialization is postponed to the moment of adding SnoopControlUnit to the platform.
                machine.PeripheralsChanged += OnMachinePeripheralsChanged;
            }
            else if(snoopControlUnit != null)
            {
                throw new ConstructionException($"{nameof(snoopControlUnit)} can't be used in {unitType}SignalsUnit");
            }
        }

        public void FillConfigurationStateStruct(IntPtr allocatedStructPointer, Arm cpu)
        {
            var registeredCPU = GetRegisteredCPU(cpu);
            registeredCPU.FillConfigurationStateStruct(allocatedStructPointer);
        }

        public ulong GetAddress(string name)
        {
            return GetAddress(signals.Parse(name));
        }

        public ulong GetAddress(ArmSignals armSignal)
        {
            var signal = signals[armSignal];
            AssertSignalNotCPUIndexed(signal, inSetMethod: false);

            return signal.GetAddress(AddressWidth);
        }

        public ulong GetSignal(string name)
        {
            return GetSignal(signals.Parse(name));
        }

        public ulong GetSignal(ArmSignals armSignal)
        {
            var signal = signals[armSignal];
            AssertSignalNotCPUIndexed(signal, inSetMethod: false);

            return signal.Value;
        }

        public bool IsSignalEnabled(string name)
        {
            return IsSignalEnabled(signals.Parse(name));
        }

        public bool IsSignalEnabled(ArmSignals armSignal)
        {
            var signal = signals[armSignal];
            AssertSignalNotCPUIndexed(signal, inSetMethod: false);

            return signal.IsEnabled();
        }

        public bool IsSignalEnabledForCPU(string name, ICPU cpu)
        {
            return IsSignalEnabledForCPU(signals.Parse(name), cpu);
        }

        public bool IsSignalEnabledForCPU(ArmSignals armSignal, ICPU cpu)
        {
            var signal = signals[armSignal];
            AssertSignalCPUIndexed(signal, inSetMethod: false);

            var cpuIndex = GetRegisteredCPU(cpu).Index;
            return signals[armSignal].IsEnabled(cpuIndex);
        }

        // Called in Arm constructors if ArmSignalsUnit passed.
        public void RegisterCPU(Arm cpu)
        {
            lock(registeredCPUs)
            {
                AssertCPUModelIsSupported(cpu.Model);

                // Bit offset for this CPU in CPU-indexed signals.
                var cpuIndex = registeredCPUs.Count;
                registeredCPUs[cpu] = new RegisteredCPU(machine, cpu, this, cpuIndex);
                signals.SetCPUIndexedSignalsWidth((uint)registeredCPUs.Count);
            }

            cpu.StateChanged += (_, oldState, __) => {
                if(oldState == CPUState.InReset)
                {
                    if(unitType == UnitType.CortexR8)
                    {
                        lock(registeredCPUs)
                        {
                            if(firstSCURegistration is NullRegistrationPoint && !scuRegisteredAtBus)
                            {
                                RegisterSCU();
                                scuRegisteredAtBus = true;
                            }
                        }
                    }
                    registeredCPUs[cpu].OnCPUOutOfReset();
                };
            };
        }

        public void Reset()
        {
            // Intentionally left blank. Signal values should be preserved across machine resets.
        }

        public void ResetSignals()
        {
            signals.Reset();
        }

        public void SetSignal(string name, ulong value)
        {
            SetSignal(signals.Parse(name), value);
        }

        public void SetSignal(ArmSignals armSignal, ulong value)
        {
            var signal = signals[armSignal];
            AssertSignalNotCPUIndexed(signal, inSetMethod: true);

            signal.Value = value;
        }

        // Convenience methods for signals which are meant to hold top bits of address.
        // There's no need to check if such a signal was chosen, a RecoverableException
        // is thrown if the signal can't hold the given address.
        public void SetSignalFromAddress(string name, ulong address)
        {
            SetSignalFromAddress(signals.Parse(name), address);
        }

        public void SetSignalFromAddress(ArmSignals armSignal, ulong address)
        {
            var signal = signals[armSignal];
            AssertSignalNotCPUIndexed(signal, inSetMethod: true);

            signal.SetFromAddress(AddressWidth, address);
        }

        public void SetSignalState(string name, bool state, uint index)
        {
            SetSignalState(signals.Parse(name), state, index);
        }

        public void SetSignalState(ArmSignals armSignal, bool state, uint index)
        {
            var signal = signals[armSignal];
            AssertSignalNotCPUIndexed(signal, inSetMethod: true);

            signal.SetState(checked((byte)index), state);
        }

        public void SetSignalStateForCPU(string name, bool state, ICPU cpu)
        {
            SetSignalStateForCPU(signals.Parse(name), state, cpu);
        }

        public void SetSignalStateForCPU(ArmSignals armSignal, bool state, ICPU cpu)
        {
            var signal = signals[armSignal];
            AssertSignalCPUIndexed(signal, inSetMethod: true);

            var cpuIndex = GetRegisteredCPU(cpu).Index;
            signal.SetState(cpuIndex, state);
        }

        public uint AddressWidth { get; } = 32;
        public IEnumerable<ICPU> RegisteredCPUs => registeredCPUs.Keys;

        private void AssertCPUModelIsSupported(string cpuModel)
        {
            var supportedModels = ModelsToUnitTypes.Where(kvPair => kvPair.Value == unitType).Select(kvPair => kvPair.Key);

            if(!supportedModels.Contains(cpuModel))
            {
                var message = $"Tried to register unsupported CPU model to {unitType}SignalsUnit: {cpuModel}; supported CPUs are: {string.Join(", ", supportedModels)}";
                throw new RecoverableException(message);
            }
        }

        private void AssertSignalCPUIndexed(Signal<ArmSignals> signal, bool inSetMethod)
        {
            if(!signals.IsSignalCPUIndexed(signal))
            {
                var alternativeMethodNames = inSetMethod
                    ? new string[] { nameof(SetSignalFromAddress), nameof(SetSignal), nameof(SetSignalState) }
                    : new string[] { nameof(GetAddress), nameof(GetSignal), nameof(IsSignalEnabled) };
                throw new RecoverableException($"Signal is not CPU-indexed. Use '{string.Join("' or '", alternativeMethodNames)}' to access it.");
            }
        }

        private void AssertSignalNotCPUIndexed(Signal<ArmSignals> signal, bool inSetMethod)
        {
            if(signals.IsSignalCPUIndexed(signal))
            {
                var alternativeMethodName = inSetMethod ? nameof(SetSignalStateForCPU) : nameof(IsSignalEnabledForCPU);
                throw new RecoverableException($"Signal is CPU-indexed. Use '{alternativeMethodName}' to access it.");
            }
        }

        private RegisteredCPU GetRegisteredCPU(ICPU cpu)
        {
            if(!registeredCPUs.TryGetValue(cpu, out var registeredCPU))
            {
                // The exception isn't always expected to be caught, e.g., when called by CPU through 'FillConfigurationStateStruct'.
                throw new RecoverableException($"CPU '{cpu.GetName()}' isn't registered to this signals unit '{this.GetName()}'.");
            }
            return registeredCPU;
        }

        private void InitSignals(UnitType type)
        {
            signals.InitSignal(this, "DBGROMADDR", ArmSignals.DebugROMAddress, width: 20);
            signals.InitSignal(this, "DBGROMADDRV", ArmSignals.DebugROMAddressValid, width: 1);
            signals.InitSignal(this, "DBGSELFADDR", ArmSignals.DebugSelfAddress, width: 15);
            signals.InitSignal(this, "DBGSELFADDRV", ArmSignals.DebugSelfAddressValid, width: 1);

            // CPU-indexed signals have width equal to CPUs count since there's a single bit per CPU.
            signals.InitSignal(this, "INITRAM", ArmSignals.InitializeInstructionTCM, cpuIndexedSignal: true);
            signals.InitSignal(this, "VINITHI", ArmSignals.HighExceptionVectors, cpuIndexedSignal: true);

            switch(type)
            {
            case UnitType.CortexR5:
                // Cortex-R5 AHB/AXI peripheral interface signals, Virtual AXI has only base and size.
                signals.InitSignal(this, "INITPPH", ArmSignals.AHBInitEnabled, cpuIndexedSignal: true);
                signals.InitSignal(this, "INITPPX", ArmSignals.AXIInitEnabled, cpuIndexedSignal: true);

                // Currently both CPUs share the same base and size values (R5 can only be dual-core).
                signals.InitSignal(this, "PPHBASE", ArmSignals.AHBBaseAddress, width: 20);
                signals.InitSignal(this, "PPXBASE", ArmSignals.AXIBaseAddress, width: 20);
                signals.InitSignal(this, "PPVBASE", ArmSignals.VirtualAXIBaseAddress, width: 20);

                signals.InitSignal(this, "PPHSIZE", ArmSignals.AHBSize, width: 5);
                signals.InitSignal(this, "PPXSIZE", ArmSignals.AXISize, width: 5);
                signals.InitSignal(this, "PPVSIZE", ArmSignals.VirtualAXISize, width: 5);
                break;
            case UnitType.CortexR8:
                signals.InitSignal(this, "MFILTEREN", ArmSignals.MasterFilterEnable, width: 1);
                signals.InitSignal(this, "MFILTEREND", ArmSignals.MasterFilterEnd, width: 12);
                signals.InitSignal(this, "MFILTERSTART", ArmSignals.MasterFilterStart, width: 12);
                signals.InitSignal(this, "PERIPHBASE", ArmSignals.PeripheralsBase, width: PeripheralsBaseBits);
                signals.InitSignal(this, "PFILTEREND", ArmSignals.PeripheralFilterEnd, width: 12);
                signals.InitSignal(this, "PFILTERSTART", ArmSignals.PeripheralFilterStart, width: 12);
                break;
            default:
                throw new RecoverableException($"Invalid {nameof(type)} value: {type}");
            }
        }

        private void OnMachinePeripheralsChanged(IMachine machine, PeripheralsChangedEventArgs args)
        {
            if(args.Peripheral == snoopControlUnit && args is PeripheralsAddedEventArgs addedArgs)
            {
                var peripheralsBase = signals[ArmSignals.PeripheralsBase];
                lock(peripheralsBase)
                {
                    OnSnoopControlUnitAdded(peripheralsBase, addedArgs.RegistrationPoint);
                }
            }
        }

        private void OnSnoopControlUnitAdded(Signal<ArmSignals> peripheralsBase, IRegistrationPoint registrationPoint)
        {
            this.DebugLog("Handling SCU's registration: {0}", registrationPoint);

            if(registrationPoint is IBusRegistration busRegistration)
            {
                OnSnoopControlUnitAdded(peripheralsBase, busRegistration);
            }
            else if(registrationPoint is NullRegistrationPoint)
            {
                // This method can be called multiple times with IRegistrationPoint but at most once with NullRegistrationPoint.
                DebugHelper.Assert(firstSCURegistration == null);
            }
            else
            {
                throw new RecoverableException($"{this.GetName()}: Added {nameof(ArmSnoopControlUnit)} with unsupported registration point!");
            }

            if(firstSCURegistration == null)
            {
                firstSCURegistration = registrationPoint;
            }
        }

        private void OnSnoopControlUnitAdded(Signal<ArmSignals> peripheralsBase, IBusRegistration busRegistration)
        {
            var context = busRegistration.CPU;
            if(context != null && !registeredCPUs.ContainsKey(context))
            {
                this.DebugLog("Ignoring {0} registration for CPU unregistered in {1}: {2}",
                    nameof(ArmSnoopControlUnit), nameof(ArmSignalsUnit), context
                );
                return;
            }

            if(firstSCURegistration == null)
            {
                // SnoopControlUnit's address indicates PeripheralsBase address cause its offset is 0x0.
                peripheralsBase.SetFromAddress(AddressWidth, busRegistration.StartingPoint);
                peripheralsBase.ResetValue = peripheralsBase.Value;
            }
            // Let's make sure the address has been the same in all bus registrations.
            else if(firstSCURegistration is IBusRegistration firstSCUBusRegistration
                    && busRegistration.StartingPoint != firstSCUBusRegistration.StartingPoint)
            {
                throw new RecoverableException("All SCU registrations must use the same address");
            }

            // Casting must be successful because `context` is in `registeredCPUs.Keys`.
            var cpus = context == null ? registeredCPUs.Keys.ToArray() : new[] { (Arm)context };
            foreach(var cpu in cpus)
            {
                if(context == null && registeredCPUs[cpu].PeripheralsBaseAtLastReset.HasValue)
                {
                    // It must've been already set using CPU-specific registration point.
                    continue;
                }
                registeredCPUs[cpu].PeripheralsBaseAtLastReset = peripheralsBase.Value;
            }
        }

        private void RegisterSCU()
        {
            // The name is lost when the peripheral gets unregistered. It's unregistered because there
            // are no SCU registrations in `peripherals` command with `NullRegistrationPoint` left.
            var scuName = machine.GetLocalName(snoopControlUnit);
            machine.SystemBus.Unregister(snoopControlUnit);

            foreach(var registeredCPU in registeredCPUs.Values)
            {
                registeredCPU.RegisterSCU(snoopControlUnit);
            }
            machine.SetLocalName(snoopControlUnit, scuName);
        }

        private IRegistrationPoint firstSCURegistration;
        private bool scuRegisteredAtBus;

        private readonly IMachine machine;
        private readonly Dictionary<ICPU, RegisteredCPU> registeredCPUs = new Dictionary<ICPU, RegisteredCPU>();
        private readonly SignalsDictionary<ArmSignals> signals = new SignalsDictionary<ArmSignals>();
        private readonly ArmSnoopControlUnit snoopControlUnit;
        private readonly UnitType unitType;

        private const int PeripheralsBaseBits = 19;

        private static readonly Dictionary<string, UnitType> ModelsToUnitTypes = new Dictionary<string, UnitType>
        {
            {"cortex-r5", UnitType.CortexR5},
            {"cortex-r5f", UnitType.CortexR5},
            {"cortex-r8", UnitType.CortexR8},
        };

        public enum UnitType
        {
            CortexR5,
            CortexR8,
        }

        private class RegisteredCPU
        {
            public RegisteredCPU(IMachine machine, Arm cpu, ArmSignalsUnit signalsUnit, int index)
            {
                this.cpu = cpu ?? throw new ArgumentNullException(nameof(cpu));
                this.machine = machine;
                this.signalsUnit = signalsUnit;

                Index = (byte)index;
            }

            public void FillConfigurationStateStruct(IntPtr allocatedStructPointer)
            {
                var state = new ConfigurationSignalsState
                {
                    IncludedSignalsMask = IncludedConfigurationSignalsMask.Create(
                        signalsUnit.unitType,
                        signalsUnit.IsSignalEnabled(ArmSignals.DebugROMAddressValid),
                        signalsUnit.IsSignalEnabled(ArmSignals.DebugSelfAddressValid)
                    ),

                    DebugROMAddress = signalsUnit.IsSignalEnabled(ArmSignals.DebugROMAddressValid)
                        ? (uint)signalsUnit.GetSignal(ArmSignals.DebugROMAddress) : 0u,
                    DebugSelfAddress = signalsUnit.IsSignalEnabled(ArmSignals.DebugSelfAddressValid)
                        ? (uint)signalsUnit.GetSignal(ArmSignals.DebugSelfAddress) : 0u,

                    HighExceptionVectors = signalsUnit.IsSignalEnabledForCPU(ArmSignals.HighExceptionVectors, cpu) ? 1u : 0u,
                    InitializeInstructionTCM = signalsUnit.IsSignalEnabledForCPU(ArmSignals.InitializeInstructionTCM, cpu) ? 1u : 0u,
                };

                switch(signalsUnit.unitType)
                {
                case UnitType.CortexR5:
                    state.AHBRegionRegister = GetBusRegionRegister(ArmSignals.AHBBaseAddress, ArmSignals.AHBSize, ArmSignals.AHBInitEnabled);
                    state.AXIRegionRegister = GetBusRegionRegister(ArmSignals.AXIBaseAddress, ArmSignals.AXISize, ArmSignals.AXIInitEnabled);
                    state.VirtualAXIRegionRegister = GetBusRegionRegister(ArmSignals.VirtualAXIBaseAddress, ArmSignals.VirtualAXISize, initSignal: null);
                    break;
                case UnitType.CortexR8:
                    state.PeripheralsBase = (uint)signalsUnit.GetSignal(ArmSignals.PeripheralsBase);
                    break;
                default:
                    throw new RecoverableException($"Invalid {nameof(signalsUnit.unitType)} value: {signalsUnit.unitType}");
                }
                Marshal.StructureToPtr(state, allocatedStructPointer, fDeleteOld: true);
            }

            public void OnCPUOutOfReset()
            {
                if(signalsUnit.IsSignalEnabledForCPU(ArmSignals.InitializeInstructionTCM, cpu)
                    && signalsUnit.IsSignalEnabledForCPU(ArmSignals.HighExceptionVectors, cpu))
                {
                    cpu.PC = 0xFFFF0000;
                }

                if(signalsUnit.unitType == UnitType.CortexR8)
                {
                    var peripheralsBase = signalsUnit.GetSignal(ArmSignals.PeripheralsBase);
                    if(PeripheralsBaseAtLastReset != peripheralsBase)
                    {
                        PeripheralsBaseChanged();
                        PeripheralsBaseAtLastReset = peripheralsBase;
                    }
                }
            }

            public void PeripheralsBaseChanged()
            {
                var peripheralsBaseAddress = signalsUnit.GetAddress(ArmSignals.PeripheralsBase);

                var pFilterStart = signalsUnit.GetAddress(ArmSignals.PeripheralFilterStart);
                var pFilterEnd = signalsUnit.GetAddress(ArmSignals.PeripheralFilterEnd);

                // The signals are just uninitialized if pFilterEnd is 0 so let's not log warnings then.
                if(pFilterEnd != 0 && (peripheralsBaseAddress < pFilterStart || peripheralsBaseAddress > pFilterEnd))
                {
                    signalsUnit.Log(LogLevel.Warning, "{0} address 0x{1:X} should be between {2} address (0x{3:X}) and {4} address (0x{5:X})",
                        Enum.GetName(typeof(ArmSignals), ArmSignals.PeripheralsBase), peripheralsBaseAddress,
                        Enum.GetName(typeof(ArmSignals), ArmSignals.PeripheralFilterStart), pFilterStart,
                        Enum.GetName(typeof(ArmSignals), ArmSignals.PeripheralFilterEnd), pFilterEnd);
                }
                MovePeripherals(peripheralsBaseAddress);
            }

            public void RegisterSCU(ArmSnoopControlUnit scu)
            {
                var address = signalsUnit.GetAddress(ArmSignals.PeripheralsBase) + (ulong)PeriphbaseOffsets.SnoopControlUnit;
                var registrationPoint = new BusRangeRegistration(address.By(checked((ulong)scu.Size)), cpu: cpu);
                machine.SystemBus.Register(scu, registrationPoint);
            }

            public byte Index { get; }

            public ulong? PeripheralsBaseAtLastReset;

            private uint GetBusRegionRegister(ArmSignals baseSignal, ArmSignals sizeSignal, ArmSignals? initSignal)
            {
                var value = 0u;
                BitHelper.SetMaskedValue(ref value, (uint)signalsUnit.GetSignal(baseSignal), 12, 20);
                BitHelper.SetMaskedValue(ref value, (uint)signalsUnit.GetSignal(sizeSignal), 2, 5);
                if(initSignal != null)
                {
                    BitHelper.SetBit(ref value, 0, signalsUnit.IsSignalEnabledForCPU(initSignal.Value, cpu));
                }
                return value;
            }

            private void MovePeripherals(ulong peripheralsBaseAddress)
            {
                signalsUnit.DebugLog("Moving GIC, SCU and timers for CPU {0} relatively to new PERIPHBASE value: 0x{1:X}", cpu, peripheralsBaseAddress);

                Func<PeriphbaseOffsets, ulong> getAddress = offset => peripheralsBaseAddress + (ulong)offset;
                MoveOrRegisterPeripheralWithinContext<ARM_GenericInterruptController>(peripheralsBaseAddress + (ulong)PeriphbaseOffsets.GIC_CPUInterface, "cpuInterface");
                MoveOrRegisterPeripheralWithinContext<ARM_GenericInterruptController>(peripheralsBaseAddress + (ulong)PeriphbaseOffsets.GIC_Distributor, "distributor");
                MoveOrRegisterPeripheralWithinContext<ARM_GlobalTimer>(peripheralsBaseAddress + (ulong)PeriphbaseOffsets.GlobalTimer);
                MoveOrRegisterPeripheralWithinContext<ArmSnoopControlUnit>(peripheralsBaseAddress + (ulong)PeriphbaseOffsets.SnoopControlUnit);
                MoveOrRegisterPeripheralWithinContext<ARM_PrivateTimer>(peripheralsBaseAddress + (ulong)PeriphbaseOffsets.PrivateTimersAndWatchdogs);
            }

            /// <remarks>Either registers or moves registration for peripheral of type <c>T</c> and the given CPU. Global registrations are left untouched.</remarks>
            private void MoveOrRegisterPeripheralWithinContext<T>(ulong newAddress, string region = null) where T : IBusPeripheral
            {
                if(!TryGetSingleBusRegistered<T>(region, out var busRegistered))
                {
                    // Peripheral not found or there are multiple peripherals of the specified type in the given context.
                    var registrationName = string.IsNullOrWhiteSpace(region) ? "" : $"region {region} of" + typeof(T).Name;
                    signalsUnit.DebugLog("No registration found for {0}; won't be moved to 0x{1:X}", registrationName, newAddress);
                    return;
                }
                var peripheral = busRegistered.Peripheral;
                var registrationPoint = busRegistered.RegistrationPoint;
                var size = registrationPoint.Range.Size;

                var newRegistration = registrationPoint is BusMultiRegistration
                    ? new BusMultiRegistration(newAddress, size, region, cpu)
                    : new BusRangeRegistration(newAddress, size, cpu: cpu);

                if(registrationPoint.CPU == cpu)
                {
                    if(newRegistration is BusMultiRegistration newMultiRP)
                    {
                        machine.SystemBus.MoveBusMultiRegistrationWithinContext(peripheral, newMultiRP, cpu);
                    }
                    else
                    {
                        machine.SystemBus.MoveRegistrationWithinContext(peripheral, newRegistration, cpu);
                    }
                }
                else
                {
                    machine.SystemBus.Register(peripheral, newRegistration);
                }
            }

            /// <remarks>It will be a BusRegistered with CPU-local registration point, if exists. Global registration points are checked only if there are no CPU-local ones.</remarks>
            private bool TryGetSingleBusRegistered<T>(string region, out IBusRegistered<IBusPeripheral> busRegistered) where T : IBusPeripheral
            {
                busRegistered = null;
                var busRegisteredEnumerable = machine.SystemBus.GetRegisteredPeripherals(cpu).Where(_busRegistered => _busRegistered.Peripheral is T);

                if(busRegisteredEnumerable.Any() && !string.IsNullOrEmpty(region))
                {
                    busRegisteredEnumerable = busRegisteredEnumerable.Where(
                        _busRegistered => _busRegistered.RegistrationPoint is BusMultiRegistration multiRegistration
                        && multiRegistration.ConnectionRegionName == region
                    );
                }

                // Choose cpu-local registrations if there are still multiple matching ones.
                if(busRegisteredEnumerable.Count() > 1)
                {
                    busRegisteredEnumerable = busRegisteredEnumerable.Where(_busRegistered => _busRegistered.RegistrationPoint.CPU == cpu);
                }

                var count = (uint)busRegisteredEnumerable.Count();
                if(count > 1)
                {
                    var logLine = "Multiple matching {0}"
                        .AppendIf(!string.IsNullOrEmpty(region), $" (region: {region})")
                        .Append(" registration points")
                        .AppendIf(cpu != null, $" for {cpu}")
                        .ToString();
                    signalsUnit.Log(LogLevel.Warning, logLine, typeof(T).Name);
                }
                busRegistered = busRegisteredEnumerable.SingleOrDefault();
                return busRegistered != default(IBusRegistered<IBusPeripheral>);
            }

            private readonly Arm cpu;
            private readonly IMachine machine;
            private readonly ArmSignalsUnit signalsUnit;

            private static class IncludedConfigurationSignalsMask
            {
                public static ulong Create(UnitType unitType, bool debugRomAddressValid, bool debugSelfAddressValid)
                {
                    var mask = (debugRomAddressValid ? 1u : 0u) << (int)SignalsEnumSharedWithTlib.DebugROMAddress
                        | (debugSelfAddressValid ? 1u : 0u) << (int)SignalsEnumSharedWithTlib.DebugSelfAddress
                        | 1u << (int)SignalsEnumSharedWithTlib.HighExceptionVectors
                        | 1u << (int)SignalsEnumSharedWithTlib.InitializeInstructionTCM
                    ;

                    switch(unitType)
                    {
                    case UnitType.CortexR5:
                        mask |= 1u << (int)SignalsEnumSharedWithTlib.AHBRegionRegister;
                        mask |= 1u << (int)SignalsEnumSharedWithTlib.AXIRegionRegister;
                        mask |= 1u << (int)SignalsEnumSharedWithTlib.VirtualAXIRegionRegister;
                        break;
                    case UnitType.CortexR8:
                        mask |= 1u << (int)SignalsEnumSharedWithTlib.PeripheralsBase;
                        break;
                    default:
                        throw new RecoverableException($"Invalid {nameof(unitType)} value: {unitType}");
                    }
                    return mask;
                }

                // Copy of ConfigurationSignals enum in tlib's arm/configuration_signals.h
                private enum SignalsEnumSharedWithTlib
                {
                    DebugROMAddress,
                    DebugSelfAddress,
                    HighExceptionVectors,
                    InitializeInstructionTCM,
                    PeripheralsBase,
                    AHBRegionRegister,
                    AXIRegionRegister,
                    VirtualAXIRegionRegister,
                }
            }

            // Keep in line with ConfigurationSignalsState struct in tlib's arm/configuration_signals.h
            [StructLayout(LayoutKind.Sequential)]
            private struct ConfigurationSignalsState
            {
                // Each bit says whether the signal should be analyzed.
                // Bit positions are based on the ConfigurationSignals enum.
                // Bools can be used here but not in tlib's enum.
                public ulong IncludedSignalsMask;

                public uint DebugROMAddress;
                public uint DebugSelfAddress;
                public uint HighExceptionVectors;
                public uint InitializeInstructionTCM;
                public uint PeripheralsBase;

                public uint AHBRegionRegister;
                public uint AXIRegionRegister;
                public uint VirtualAXIRegionRegister;
            }

            private enum PeriphbaseOffsets : ulong
            {
                SnoopControlUnit = 0x0,
                GIC_CPUInterface = 0x100,
                GlobalTimer = 0x200,
                PrivateTimersAndWatchdogs = 0x600,
                GIC_Distributor = 0x1000,
            }
        }

        private class SignalsDictionary<TEnum>
            where TEnum: struct
        {
            public SignalsDictionary()
            {
                if(!typeof(TEnum).IsEnum)  // System.Enum as a constraint isn't available in C# 7.2.
                {
                    throw new ConstructionException("T must be enum");
                }
            }

            public void InitSignal(IPeripheral parent, string name, TEnum signal, uint width = 0,
                ulong resetValue = 0x0, Func<ulong, ulong> getter = null, Action<ulong, ulong> setter = null,
                bool callSetterAtInitAndReset = false, bool cpuIndexedSignal = false)
            {
                // Non-negative width is asserted when the signal gets created.
                if(width == 0 && !cpuIndexedSignal)
                {
                    throw new ConstructionException($"Invalid init for '{name}' signal. Non-CPU-indexed signals must have positive width.");
                }

                signalNames.Add(name, signal);
                Signal<TEnum> signalObject;
                if(getter != null)
                {
                    signalObject = SignalWithImmediateEffect<TEnum>.CreateOutput(parent, signal, width, getter);
                }
                else if(setter != null)
                {
                    signalObject = SignalWithImmediateEffect<TEnum>.CreateInput(parent, signal, width, setter, callSetterAtInitAndReset, resetValue);
                }
                else
                {
                    signalObject = new Signal<TEnum>(parent, signal, width, resetValue);
                }
                dictionary.Add(signal, signalObject);

                if(cpuIndexedSignal)
                {
                    cpuIndexedSignals.Add(signalObject);
                }
            }

            public bool IsSignalCPUIndexed(Signal<TEnum> signal)
            {
                return cpuIndexedSignals.Contains(signal);
            }

            public TEnum Parse(string name)
            {
                if(!signalNames.TryGetValue(name, out var signal) && !Enum.TryParse(name, ignoreCase: true, out signal))
                {
                    var allNames = signalNames.Keys.Select(_name => $"{_name} ({signalNames[_name]})");
                    throw new RecoverableException(
                        $"No such signal: '{name}'\n" +
                        $"Available signals are:\n * {string.Join("\n * ", allNames)}"
                        );
                }
                return signal;
            }

            public void Reset()
            {
                foreach(var signal in dictionary.Values)
                {
                    signal.Reset();
                }
            }

            public void SetCPUIndexedSignalsWidth(uint width)
            {
                foreach(var signal in cpuIndexedSignals)
                {
                    signal.Width = width;
                }
            }

            public Signal<TEnum> this[TEnum key] => dictionary[key];

            private readonly List<Signal<TEnum>> cpuIndexedSignals = new List<Signal<TEnum>>();
            private readonly Dictionary<TEnum, Signal<TEnum>> dictionary = new Dictionary<TEnum, Signal<TEnum>>();
            private readonly Dictionary<string, TEnum> signalNames = new Dictionary<string, TEnum>(StringComparer.InvariantCultureIgnoreCase);
        }

        private class Signal<TEnum>
            where TEnum: struct
        {
            public Signal(IPeripheral parent, TEnum signal, uint width, ulong resetValue = 0x0)
            {
                if(!typeof(TEnum).IsEnum)  // System.Enum as a constraint isn't available in C# 7.2.
                {
                    throw new ConstructionException("T must be enum");
                }
                var name = Enum.GetName(typeof(TEnum), signal) ?? throw new ConstructionException("Invalid signal");
                this.parent = parent;

                if(width < 0 || width > 64)
                {
                    throw new ConstructionException($"Invalid signal width for {name}: {width}");
                }
                Width = width;

                Name = name;
                ResetValue = resetValue;

                Reset();
            }

            /// <returns>Top <c>Value</c> bits shifted as top bits up to <c>addressWidth</c>.</returns>
            public ulong GetAddress(uint addressWidth)
            {
                AssertAddressWidth(addressWidth, Width);

                var offset = addressWidth - Width;
                return Value << (int)offset;
            }

            public bool IsEnabled()
            {
                if(Width != 1)
                {
                    throw new RecoverableException($"Signal.IsEnabled: {Name} signal has more than 1 bit, specify which to return");
                }
                return IsEnabled(index: 0);
            }

            public bool IsEnabled(byte index)
            {
                if(index >= Width)
                {
                    throw new RecoverableException($"Signal.IsEnabled: {Name} signal has {Width} bits, requested: {Name}[{index}]");
                }
                return BitHelper.IsBitSet(Value, index);
            }

            public virtual void Reset()
            {
                value = ResetValue;
            }

            /// <summary>Top <c>Value</c> bits will be set from the address bits up to <c>Width</c>.</summary>
            public void SetFromAddress(uint addressWidth, ulong address)
            {
                AssertAddressWidth(addressWidth, Width);

                var offset = addressWidth - Width;
                if((address & BitHelper.CalculateMask((int)Width, (int)offset)) != address)
                {
                    ThrowException($"{Width}-bit signal in a {addressWidth}-bit unit shouldn't be set from 0x{address:X} address");
                }
                Value = address >> (int)offset;
            }

            public void SetState(byte index, bool state)
            {
                var newValue = Value;
                BitHelper.SetBit(ref newValue, index, state);
                Value = newValue;
            }

            public string Name { get; }
            public ulong ResetValue { get => resetValue; set => SetValue(ref resetValue, value); }
            public virtual ulong Value { get => value; set => SetValue(ref this.value, value); }
            public uint Width
            {
                get => width;
                set
                {
                    width = value;
                    // Re-set values after changing width; old values might be invalid now.
                    SetValue(ref resetValue, resetValue);
                    SetValue(ref this.value, this.value);
                }
            }

            private void AssertAddressWidth(uint addressWidth, uint valueWidth)
            {
                if(addressWidth < valueWidth)
                {
                    ThrowException($"Can't convert {valueWidth}-bit signal from or to {addressWidth}-bit address");
                }
            }

            private void SetValue(ref ulong destination, ulong value)
            {
                if(BitHelper.GetMaskedValue(value, 0, (int)Width) != value)
                {
                    ThrowException($"Tried to set {Width}-bit signal to 0x{value:X}");
                }
                destination = value;
            }

            private void ThrowException(string message)
            {
                throw new RecoverableException($"{parent.GetName()}: {Name}: {message}");
            }

            private ulong resetValue;
            private ulong value;
            private uint width;

            private readonly IPeripheral parent;
        }

        private class SignalWithImmediateEffect<TEnum> : Signal<TEnum>
            where TEnum: struct
        {
            public static SignalWithImmediateEffect<TEnum> CreateInput(IPeripheral parent, TEnum signal, uint width,
                            Action<ulong, ulong> setter, bool callSetterAtInitAndReset = false, ulong resetValue = 0)
            {
                if(setter == null)
                {
                    throw new ConstructionException("Setter cannot be null for input signal with immediate effect.");
                }
                return new SignalWithImmediateEffect<TEnum>(parent, signal, width, null, setter, callSetterAtInitAndReset, resetValue);
            }

            public static SignalWithImmediateEffect<TEnum> CreateOutput(IPeripheral parent, TEnum signal, uint width, Func<ulong, ulong> getter)
            {
                if(getter == null)
                {
                    throw new ConstructionException("Getter cannot be null for output signal with immediate effect.");
                }
                return new SignalWithImmediateEffect<TEnum>(parent, signal, width, getter);
            }

            private SignalWithImmediateEffect(IPeripheral parent, TEnum signal, uint width, Func<ulong, ulong> getter = null,
                            Action<ulong, ulong> setter = null, bool callSetterAtInitAndReset = false, ulong resetValue = 0)
                            : base(parent, signal, width, resetValue)
            {
                if(getter != null && setter != null)
                {
                    throw new ConstructionException("Signal cannot have both getter and setter");
                }
                this.callSetterAtInitAndReset = callSetterAtInitAndReset;
                this.getter = getter;
                this.setter = setter;

                Reset();
            }

            public override void Reset()
            {
                var oldValue = Value;
                base.Reset();

                if(callSetterAtInitAndReset)
                {
                    setter?.Invoke(oldValue, Value);
                }
            }

            public override ulong Value
            {
                get
                {
                    return getter?.Invoke(base.Value) ?? base.Value;
                }

                set
                {
                    var oldValue = base.Value;
                    base.Value = value;
                    setter?.Invoke(oldValue, value);
                }
            }

            private readonly bool callSetterAtInitAndReset;
            private readonly Func<ulong, ulong> getter;
            private readonly Action<ulong, ulong> setter;
        }
    }

    public enum ArmSignals
    {
        DebugROMAddress,
        DebugROMAddressValid,
        DebugSelfAddress,
        DebugSelfAddressValid,
        HighExceptionVectors,
        InitializeInstructionTCM,
        MasterFilterEnable,
        MasterFilterEnd,
        MasterFilterStart,
        PeripheralsBase,
        PeripheralFilterEnd,
        PeripheralFilterStart,

        // Cortex-R5 AHB/AXI peripheral interface region signals based on:
        //   https://developer.arm.com/documentation/ddi0460/d/Signal-Descriptions/Configuration-signals
        // There's no "enabled out-of-reset" signal for virtual AXI peripheral interface
        AHBBaseAddress,
        AHBInitEnabled,
        AHBSize,
        AXIBaseAddress,
        AXIInitEnabled,
        AXISize,
        VirtualAXIBaseAddress,
        VirtualAXISize,
    }
}
