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
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ArmSignalsUnit : IPeripheral
    {
        public ArmSignalsUnit(IMachine machine)
        {
            InitSignals();
            this.machine = machine;

            // PeripheralsBase initialization is postponed to the moment of adding SnoopControlUnit to the platform.
            machine.PeripheralsChanged += OnMachinePeripheralsChanged;
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

        public bool IsSignalEnabledForCPU(string name, Arm cpu)
        {
            return IsSignalEnabledForCPU(signals.Parse(name), cpu);
        }

        public bool IsSignalEnabledForCPU(ArmSignals armSignal, Arm cpu)
        {
            var signal = signals[armSignal];
            AssertSignalCPUIndexed(signal, inSetMethod: false);

            var cpuIndex = GetRegisteredCPU(cpu).Index;
            return signals[armSignal].IsEnabled(cpuIndex);
        }

        public void RegisterCPU(Arm cpu)
        {
            lock(registeredCPUs)
            {
                // Bit offset for this CPU in CPU-indexed signals.
                var cpuIndex = registeredCPUs.Count;
                registeredCPUs[cpu] = new RegisteredCPU(cpu, this, cpuIndex, signals[ArmSignals.PeripheralsBase].ResetValue);
                signals.SetCPUIndexedSignalsWidth(registeredCPUs.Count);
            }
            cpu.StateChanged += (_, oldState, __) => { if(oldState == CPUState.InReset) registeredCPUs[cpu].OnCPUOutOfReset(); };
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

        public void SetSignalStateForCPU(string name, bool state, Arm cpu)
        {
            SetSignalStateForCPU(signals.Parse(name), state, cpu);
        }

        public void SetSignalStateForCPU(ArmSignals armSignal, bool state, Arm cpu)
        {
            var signal = signals[armSignal];
            AssertSignalCPUIndexed(signal, inSetMethod: true);

            var cpuIndex = GetRegisteredCPU(cpu).Index;
            signal.SetState(cpuIndex, state);
        }

        public int AddressWidth { get; } = 32;

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

        private RegisteredCPU GetRegisteredCPU(Arm cpu)
        {
            if(!registeredCPUs.TryGetValue(cpu, out var registeredCPU))
            {
                // The exception isn't always expected to be caught, e.g., when called by CPU through 'FillConfigurationStateStruct'.
                throw new RecoverableException($"CPU '{cpu.GetName()}' isn't registered to this signals unit '{this.GetName()}'.");
            }
            return registeredCPU;
        }

        private void InitSignals()
        {
            signals.InitSignal(this, "DBGROMADDR", ArmSignals.DebugROMAddress, width: 20);
            signals.InitSignal(this, "DBGROMADDRV", ArmSignals.DebugROMAddressValid, width: 1);
            signals.InitSignal(this, "DBGSELFADDR", ArmSignals.DebugSelfAddress, width: 15);
            signals.InitSignal(this, "DBGSELFADDRV", ArmSignals.DebugSelfAddressValid, width: 1);
            signals.InitSignal(this, "MFILTEREN", ArmSignals.MasterFilterEnable, width: 1);
            signals.InitSignal(this, "MFILTEREND", ArmSignals.MasterFilterEnd, width: 12);
            signals.InitSignal(this, "MFILTERSTART", ArmSignals.MasterFilterStart, width: 12);
            signals.InitSignal(this, "PERIPHBASE", ArmSignals.PeripheralsBase, width: PeripheralsBaseBits);
            signals.InitSignal(this, "PFILTEREND", ArmSignals.PeripheralFilterEnd, width: 12);
            signals.InitSignal(this, "PFILTERSTART", ArmSignals.PeripheralFilterStart, width: 12);

            // CPU-indexed signals have width equal to CPUs count since there's a single bit per CPU.
            signals.InitSignal(this, "INITRAM", ArmSignals.InitializeInstructionTCM, cpuIndexedSignal: true);
            signals.InitSignal(this, "VINITHI", ArmSignals.HighExceptionVectors, cpuIndexedSignal: true);
        }

        private void OnMachinePeripheralsChanged(IMachine machine, PeripheralsChangedEventArgs args)
        {
            if(args.Peripheral is ArmSnoopControlUnit snoopControlUnit
                && args.Operation == PeripheralsChangedEventArgs.PeripheralChangeType.Addition)
            {
                OnSnoopControlUnitAdded(snoopControlUnit);
            }
        }

        private void OnSnoopControlUnitAdded(ArmSnoopControlUnit snoopControlUnit)
        {
            lock(signals[ArmSignals.PeripheralsBase])
            {
                if(peripheralsBaseInitializedFromSCU)
                {
                    // Don't log anything, it might be called when adding new registration points for CPU contexts.
                    return;
                }
                peripheralsBaseInitializedFromSCU = true;
            }

            var scus = machine.GetSystemBus(snoopControlUnit).GetRegistrationPoints(snoopControlUnit);
            if(!scus.Any())
            {
                throw new RecoverableException($"{this.GetName()}: Tried to register '{snoopControlUnit.GetName()}' but it has no registration points.");
            }

            if(scus.Count() > 1)
            {
                this.Log(LogLevel.Warning,
                    "Multiple SnoopControlUnit registration points ({0})\n" +
                    "using only the first one to calculate Peripherals Base Address.",
                    string.Join(", ", scus));
            }
            var scuRegistrationPoint = scus.First();
            var scuAddress = scuRegistrationPoint.Range.StartAddress;

            // SnoopControlUnit's address depends directly on PeripheralsBase (offset = 0).
            var peripheralsBase = signals[ArmSignals.PeripheralsBase];
            peripheralsBase.SetFromAddress(AddressWidth, scuAddress);
            peripheralsBase.ResetValue = peripheralsBase.Value;

            lock(registeredCPUs)
            {
                foreach(var registeredCPU in registeredCPUs.Values)
                {
                    registeredCPU.PeripheralsBaseAtLastReset = peripheralsBase.Value;
                }
            }
        }

        private bool peripheralsBaseInitializedFromSCU;

        private readonly IMachine machine;
        private readonly Dictionary<Arm, RegisteredCPU> registeredCPUs = new Dictionary<Arm, RegisteredCPU>();
        private readonly SignalsDictionary<ArmSignals> signals = new SignalsDictionary<ArmSignals>();

        private const int PeripheralsBaseBits = 19;

        private class RegisteredCPU
        {
            public RegisteredCPU(Arm cpu, ArmSignalsUnit signalsUnit, int index, ulong peripheralsBaseAtLastReset)
            {
                this.cpu = cpu ?? throw new ArgumentNullException(nameof(cpu));
                this.signalsUnit = signalsUnit;

                Index = (byte)index;
                PeripheralsBaseAtLastReset = peripheralsBaseAtLastReset;
            }

            public void FillConfigurationStateStruct(IntPtr allocatedStructPointer)
            {
                var state = new ConfigurationSignalsState
                {
                    IncludedSignalsMask = IncludedConfigurationSignalsMask.Create(
                        signalsUnit.IsSignalEnabled(ArmSignals.DebugROMAddressValid),
                        signalsUnit.IsSignalEnabled(ArmSignals.DebugSelfAddressValid)
                    ),

                    DebugROMAddress = signalsUnit.IsSignalEnabled(ArmSignals.DebugROMAddressValid)
                        ? (uint)signalsUnit.GetSignal(ArmSignals.DebugROMAddress) : 0u,
                    DebugSelfAddress = signalsUnit.IsSignalEnabled(ArmSignals.DebugSelfAddressValid)
                        ? (uint)signalsUnit.GetSignal(ArmSignals.DebugSelfAddress) : 0u,

                    HighExceptionVectors = signalsUnit.IsSignalEnabledForCPU(ArmSignals.HighExceptionVectors, cpu) ? 1u : 0u,
                    InitializeInstructionTCM = signalsUnit.IsSignalEnabledForCPU(ArmSignals.InitializeInstructionTCM, cpu) ? 1u : 0u,
                    PeripheralsBase = (uint)signalsUnit.GetSignal(ArmSignals.PeripheralsBase),
                };

                Marshal.StructureToPtr(state, allocatedStructPointer, fDeleteOld: true);
            }

            public void OnCPUOutOfReset()
            {
                if(signalsUnit.IsSignalEnabledForCPU(ArmSignals.InitializeInstructionTCM, cpu)
                    && signalsUnit.IsSignalEnabledForCPU(ArmSignals.HighExceptionVectors, cpu))
                {
                    cpu.PC = 0xFFFF0000;
                }

                // PeripheralsBase
                var peripheralsBase = signalsUnit.GetSignal(ArmSignals.PeripheralsBase);
                var peripheralsBaseAddress = signalsUnit.GetAddress(ArmSignals.PeripheralsBase);
                if(PeripheralsBaseAtLastReset != peripheralsBase)
                {
                    PeripheralsBaseAtLastReset = peripheralsBase;
                    var pFilterStart = signalsUnit.GetAddress(ArmSignals.PeripheralFilterStart);
                    var pFilterEnd = signalsUnit.GetAddress(ArmSignals.PeripheralFilterEnd);
                    if(peripheralsBaseAddress < pFilterStart || peripheralsBaseAddress > pFilterEnd)
                    {
                        signalsUnit.Log(LogLevel.Warning, "{0} address 0x{1:X} should be between {2} address (0x{3:X}) and {4} address (0x{5:X})",
                            Enum.GetName(typeof(ArmSignals), ArmSignals.PeripheralsBase), peripheralsBaseAddress,
                            Enum.GetName(typeof(ArmSignals), ArmSignals.PeripheralFilterStart), pFilterStart,
                            Enum.GetName(typeof(ArmSignals), ArmSignals.PeripheralFilterEnd), pFilterEnd);
                    }
                    MovePeripherals(peripheralsBaseAddress);
                }
            }

            public byte Index { get; }
            public ulong PeripheralsBaseAtLastReset { get; set; }

            private void MovePeripherals(ulong peripheralsBaseAddress)
            {
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
                    return;
                }
                var peripheral = busRegistered.Peripheral;
                var registrationPoint = busRegistered.RegistrationPoint;
                var size = registrationPoint.Range.Size;

                var newRegistrationPoint = registrationPoint is BusMultiRegistration
                    ? new BusMultiRegistration(newAddress, size, region, cpu)
                    : new BusRangeRegistration(newAddress, size, cpu: cpu);

                if(registrationPoint.CPU == cpu)
                {
                    if(registrationPoint is BusMultiRegistration multi)
                    {
                        SystemBus.MoveBusMultiRegistrationWithinContext(peripheral, newAddress, cpu, region);
                    }
                    else
                    {
                        SystemBus.MoveRegistrationWithinContext(peripheral, newAddress, cpu);
                    }
                }
                else
                {
                    SystemBus.Register(peripheral, newRegistrationPoint);
                }
            }

            /// <remarks>It will be a BusRegistered with CPU-local registration point, if exists. Global registration points are checked only if there are no CPU-local ones.</remarks>
            private bool TryGetSingleBusRegistered<T>(string region, out IBusRegistered<IBusPeripheral> busRegistered) where T : IBusPeripheral
            {
                busRegistered = null;
                var busRegisteredEnumerable = SystemBus.GetRegisteredPeripherals(cpu).Where(_busRegistered => _busRegistered.Peripheral is T);

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

                if(busRegisteredEnumerable.Count() > 1)
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

            // `RegisteredCPU` constructor is called before its CPU gets registered in the machine.
            // Since `GetMachine` won't work there, `SystemBus` is set when it's first used.
            private IBusController SystemBus
            {
                get
                {
                    if(systemBus == null)
                    {
                        systemBus = cpu.GetMachine().SystemBus;
                    }
                    return systemBus;
                }
            }

            private IBusController systemBus;

            private readonly Arm cpu;
            private readonly ArmSignalsUnit signalsUnit;

            private static class IncludedConfigurationSignalsMask
            {
                public static ulong Create(bool debugRomAddressValid, bool debugSelfAddressValid)
                {
                    return (debugRomAddressValid ? 1u : 0u) << (int)SignalsEnumSharedWithTlib.DebugROMAddress
                        | (debugSelfAddressValid ? 1u : 0u) << (int)SignalsEnumSharedWithTlib.DebugSelfAddress
                        | 1u << (int)SignalsEnumSharedWithTlib.HighExceptionVectors
                        | 1u << (int)SignalsEnumSharedWithTlib.InitializeInstructionTCM
                        | 1u << (int)SignalsEnumSharedWithTlib.PeripheralsBase;
                }

                // Copy of ConfigurationSignals enum in tlib's arm/configuration_signals.h
                private enum SignalsEnumSharedWithTlib
                {
                    DebugROMAddress,
                    DebugSelfAddress,
                    HighExceptionVectors,
                    InitializeInstructionTCM,
                    PeripheralsBase,
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

            public void InitSignal(IEmulationElement parent, string name, TEnum signal, int width = 0,
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
                        $"Available signals are:\n * { string.Join("\n * ", allNames) }"
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

            public void SetCPUIndexedSignalsWidth(int width)
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
            public Signal(IEmulationElement parent, TEnum signal, int width, ulong resetValue = 0x0)
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
            public ulong GetAddress(int addressWidth)
            {
                AssertAddressWidth(addressWidth, Width);

                var offset = addressWidth - Width;
                return Value << offset;
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

            public void Log(LogLevel level, string message, params object[] args)
            {
                parent.Log(level, $"{Name}: {message}", args);
            }

            public virtual void Reset()
            {
                Value = ResetValue;
            }

            /// <summary>Top <c>Value</c> bits will be set from the address bits up to <c>Width</c>.</summary>
            public void SetFromAddress(int addressWidth, ulong address)
            {
                AssertAddressWidth(addressWidth, Width);

                if((address & BitHelper.CalculateMask(Width, 0)) != 0)
                {
                    Log(LogLevel.Warning, $"{Width}-bit value shouldn't be created from 0x{address:X} address");
                }
                var offset = addressWidth - Width;
                Value = address >> offset;
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
            public int Width
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

            private static void AssertAddressWidth(int addressWidth, int valueWidth)
            {
                if(addressWidth < valueWidth)
                {
                    throw new ArgumentException($"Can't convert {valueWidth}-bit signal from or to address with lower address width", nameof(addressWidth));
                }
            }

            private void SetValue(ref ulong destination, ulong value)
            {
                destination = BitHelper.GetMaskedValue(value, 0, Width);
                if(destination != value)
                {
                    Log(LogLevel.Warning, "Tried to set {0}-bit signal to 0x{1:X}, it will be set to 0x{2:X}", Width, value, destination);
                }
            }

            private ulong resetValue;
            private ulong value;
            private int width;

            private readonly IEmulationElement parent;
        }

        private class SignalWithImmediateEffect<TEnum> : Signal<TEnum>
            where TEnum: struct
        {
            public static SignalWithImmediateEffect<TEnum> CreateInput(IEmulationElement parent, TEnum signal, int width,
                            Action<ulong, ulong> setter, bool callSetterAtInitAndReset = false, ulong resetValue = 0)
            {
                if(setter == null)
                {
                    throw new ConstructionException("Setter cannot be null for input signal with immediate effect.");
                }
                return new SignalWithImmediateEffect<TEnum>(parent, signal, width, null, setter, callSetterAtInitAndReset, resetValue);
            }

            public static SignalWithImmediateEffect<TEnum> CreateOutput(IEmulationElement parent, TEnum signal, int width, Func<ulong, ulong> getter)
            {
                if(getter == null)
                {
                    throw new ConstructionException("Getter cannot be null for output signal with immediate effect.");
                }
                return new SignalWithImmediateEffect<TEnum>(parent, signal, width, getter);
            }

            private SignalWithImmediateEffect(IEmulationElement parent, TEnum signal, int width, Func<ulong, ulong> getter = null,
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
    }
}
