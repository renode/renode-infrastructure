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
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ArmSignalsUnit : IEmulationElement
    {
        public ArmSignalsUnit(IMachine machine, int coresCount, ulong scuInitAddress)
        {
            if(coresCount <= 0)
            {
                throw new ConstructionException("coresCount <= 0");
            }
            CoresCount = coresCount;
            systemBus = machine.SystemBus;
            this.machine = machine;

            // SnoopControlUnit's address depends directly on PERIPHBASE (offset = 0).
            // Let's have one for each core so that CPUs update their own private timers.
            periphBasesAtLastReset = Enumerable.Repeat(PeriphBaseFromAddress(scuInitAddress), coresCount).ToArray();

            signals = new SignalsDictionary<ArmSignals>(coresCount, InitSignal);
        }

        [HideInMonitor]
        public void FillConfigurationStateStruct(IntPtr allocatedStructPointer, uint cpuId)
        {
            if (cpuId >= CoresCount)
            {
                throw new ArgumentException($"CPU ID ({cpuId}) greater than ArmSignalUnit's 'coresCount': {CoresCount}");
            }

            var state = new ConfigurationSignalsState
            {
                IncludedSignalsMask = IncludedConfigurationSignalsMask.Create(GetSignal(ArmSignals.DBGROMADDRV), GetSignal(ArmSignals.DBGSELFADDRV)),

                DebugROMAddress = GetSignal(ArmSignals.DBGROMADDRV) == 1 ? (uint)GetSignal(ArmSignals.DBGROMADDR) : 0,
                DebugSelfAddress = GetSignal(ArmSignals.DBGSELFADDRV) == 1 ? (uint)GetSignal(ArmSignals.DBGSELFADDR) : 0,

                Initram = GetSignal(ArmSignals.INITRAM, (byte)cpuId),
                PeripheralsBase = (uint)GetSignal(ArmSignals.PERIPHBASE),
                Vinithi = GetSignal(ArmSignals.VINITHI, (byte)cpuId),
            };

            Marshal.StructureToPtr(state, allocatedStructPointer, fDeleteOld: true);
        }

        public void OnResumeAfterReset(ARMv7R cpu)
        {
            if(GetSignal(ArmSignals.INITRAM, (byte)cpu.Id) && GetSignal(ArmSignals.VINITHI, (byte)cpu.Id))
            {
                cpu.PC = 0xFFFF0000;
            }

            // PERIPHBASE
            var periphBase = GetSignal(ArmSignals.PERIPHBASE);
            var periphBaseAddress = PeriphBaseToAddress(periphBase);
            if(periphBasesAtLastReset.All(x => x != periphBase))
            {
                periphBasesAtLastReset[cpu.Id] = periphBase;
                var pFilterStart = GetSignal(ArmSignals.PFILTERSTART) << 20;
                var pFilterEnd = GetSignal(ArmSignals.PFILTEREND) << 20;
                if(periphBaseAddress < pFilterStart || periphBaseAddress > pFilterEnd)
                {
                    this.Log(LogLevel.Warning, "PERIPHBASE address 0x{0:X} should be between PFILTERSTART address (0x{1:X}) and PFILTEREND address (0x{2:X})",
                        periphBaseAddress, pFilterStart, pFilterEnd);
                }
                MoveGlobalPeripherals(cpu, periphBaseAddress);
                MoveLocalPeripherals(cpu, periphBaseAddress);
            }

            // It's false for the first CPU that updates global peripherals cause
            // its periphBasesAtLastReset been already updated in the if above.
            if(periphBasesAtLastReset[cpu.Id] != periphBase)
            {
                periphBasesAtLastReset[cpu.Id] = periphBase;
                MoveLocalPeripherals(cpu, periphBaseAddress);
            }

            // ArmSnoopControlUnit applies configuration signals this way.
            ResumeAfterReset();
        }

        public ulong GetSignal(string name)
        {
            return signals.Parse(name).Value;
        }

        public bool GetSignal(string name, byte index)
        {
            return signals.Parse(name).IsBitSet(index);
        }

        [HideInMonitor]
        public ulong GetSignal(ArmSignals signal)
        {
            return signals[signal].Value;
        }

        [HideInMonitor]
        public bool GetSignal(ArmSignals signal, byte index)
        {
            return signals[signal].IsBitSet(index);
        }

        public void SetSignal(string name, bool state, uint index)
        {
            signals.Parse(name).SetBit(state, (byte)index);
        }

        public void SetSignal(string name, ulong value)
        {
            signals.Parse(name).Value = value;
        }

        [HideInMonitor]
        public void SetSignal(ArmSignals signal, bool state, uint index)
        {
            signals[signal].SetBit(state, (byte)index);
        }

        [HideInMonitor]
        public void SetSignal(ArmSignals signal, ulong value)
        {
            signals[signal].Value = value;
        }

        public int CoresCount { get; }
        public event Action ResumeAfterReset;

        private static ulong PeriphBaseFromAddress(ulong periphBaseAddress)
        {
            // PERIPHBASE contains only top bits of a 32-bit address.
            var offset = 32 - PeriphBaseBits;
            return periphBaseAddress >> offset;
        }

        private static ulong PeriphBaseToAddress(ulong periphBase)
        {
            // PERIPHBASE contains only top bits of a 32-bit address.
            var offset = 32 - PeriphBaseBits;
            return periphBase << offset;
        }

        private T GetSinglePeripheralOrNull<T>(ICPU context, bool isPeripheralCPULocal) where T : IBusPeripheral
        {
            var contextPeripherals = systemBus.GetPeripheralsForContext(isPeripheralCPULocal ? context : null).Select(busRegistered => busRegistered.Peripheral);
            var peripheralsT = contextPeripherals.Distinct().Where(peripheral => peripheral is T);
            if(peripheralsT.Count() > 1)
            {
                var message = string.Format("There can be only 1 peripheral of type {0} in the machine when using PERIPHBASE signal {1}",
                    typeof(T).Name, context == null ? "" : $"for the '{context.GetName()}' CPU");
                throw new RecoverableException(message);
            }
            return (T)peripheralsT.SingleOrDefault();
        }

        private Signal<ArmSignals> InitSignal(ArmSignals signal, int coresCount)
        {
            int bits;

            bool callSetterAtInit = false;
            Func<ulong> getter = null;
            ulong resetValue = 0;
            Action<ulong> setter = null;

            // All these are inputs.
            bool isInput = true;
            switch(signal)
            {
                case ArmSignals.DBGROMADDR:
                    bits = 20; break;
                case ArmSignals.DBGROMADDRV:
                    bits = 1; break;
                case ArmSignals.DBGSELFADDR:
                    bits = 15; break;
                case ArmSignals.DBGSELFADDRV:
                    bits = 1; break;
                case ArmSignals.INITRAM:
                    bits = coresCount; break;
                case ArmSignals.MFILTEREN:
                    bits = 1; break;
                case ArmSignals.MFILTEREND:
                    bits = 12; break;
                case ArmSignals.MFILTERSTART:
                    bits = 12; break;
                case ArmSignals.PERIPHBASE:
                    bits = PeriphBaseBits; resetValue = periphBasesAtLastReset[0]; break;
                case ArmSignals.PFILTEREND:
                    bits = 12; break;
                case ArmSignals.PFILTERSTART:
                    bits = 12; break;
                case ArmSignals.VINITHI:
                    bits = coresCount; break;
                default:
                    throw new ConstructionException("Invalid signal: {signal}");
            }

            if(getter != null)
            {
                return SignalWithImmediateEffect<ArmSignals>.CreateOutput(this, signal, bits, getter);
            }
            else if(setter != null)
            {
                return SignalWithImmediateEffect<ArmSignals>.CreateInput(this, signal, bits, setter, callSetterAtInit, resetValue);
            }
            else
            {
                return new Signal<ArmSignals>(this, signal, bits, isInput, resetValue);
            }
        }

        public static class IncludedConfigurationSignalsMask
        {
            public static ulong Create(ulong debugRomAddressValid, ulong debugSelfAddressValid)
            {
                var includeDebugRomAddress = debugRomAddressValid == 1u;
                var includeDebugSelfAddress = debugSelfAddressValid == 1u;

                return (includeDebugRomAddress ? 1u : 0u) << (int)SignalsEnumSharedWithTlib.DBGROMADDR
                    | (includeDebugSelfAddress ? 1u : 0u) << (int)SignalsEnumSharedWithTlib.DBGSELFADDR
                    | 1u << (int)SignalsEnumSharedWithTlib.INITRAM
                    | 1u << (int)SignalsEnumSharedWithTlib.PERIPHBASE
                    | 1u << (int)SignalsEnumSharedWithTlib.VINITHI;
            }

            // Copy of ConfigurationSignals enum in tlib's arm/configuration_signals.h
            private enum SignalsEnumSharedWithTlib
            {
                DBGROMADDR,
                DBGSELFADDR,
                INITRAM,
                PERIPHBASE,
                VINITHI,
            }
        }


        private void MoveGlobalPeripherals(ARMv7R cpu, ulong periphBaseAddress)
        {
            Func<PeriphbaseOffsets, ulong> getAddress = offset => periphBaseAddress + (ulong)offset;

            var gic = GetSinglePeripheralOrNull<ARM_GenericInterruptController>(cpu, isPeripheralCPULocal: false);
            if(gic != null)
            {
                systemBus.MoveBusMultiRegistrationWithinContext(gic, getAddress(PeriphbaseOffsets.GIC_CPUInterface), cpu, "cpuInterface");
                systemBus.MoveBusMultiRegistrationWithinContext(gic, getAddress(PeriphbaseOffsets.GIC_Distributor), cpu, "distributor");
            }
            MovePeripheral<ARM_GlobalTimer>(getAddress(PeriphbaseOffsets.GlobalTimer), cpu, isPeripheralCPULocal: false);
            MovePeripheral<ArmSnoopControlUnit>(getAddress(PeriphbaseOffsets.SnoopControlUnit), cpu, isPeripheralCPULocal: false);
        }

        private void MoveLocalPeripherals(ARMv7R cpu, ulong periphBaseAddress)
        {
            MovePeripheral<ARM_PrivateTimer>(periphBaseAddress + (ulong)PeriphbaseOffsets.PrivateTimersAndWatchdogs, cpu, isPeripheralCPULocal: true);
        }

        /// <remarks>
        /// Pass CPU only for the CPU-local peripherals like ARM Private Timer.
        /// </remarks>
        private void MovePeripheral<T>(ulong newAddress, ICPU cpu, bool isPeripheralCPULocal) where T : IBusPeripheral
        {
            var peripheral = GetSinglePeripheralOrNull<T>(cpu, isPeripheralCPULocal);
            if(peripheral != null)
            {
                systemBus.MoveRegistrationWithinContext(peripheral, newAddress, cpu);
            }
        }

        private ulong[] periphBasesAtLastReset;

        private readonly IMachine machine;
        private readonly SimpleCache peripheralsCache = new SimpleCache();
        private readonly SignalsDictionary<ArmSignals> signals;
        private readonly IBusController systemBus;

        private const int PeriphBaseBits = 19;

        // Keep in line with ConfigurationSignalsState struct in tlib's arm/configuration_signals.h
        [StructLayout(LayoutKind.Sequential)]
        private struct ConfigurationSignalsState
        {
            // Each bit says whether the signal should be analyzed.
            // Bit positions are based on the ConfigurationSignals enum.
            public ulong IncludedSignalsMask;

            public uint DebugROMAddress;
            public uint DebugSelfAddress;
            public bool Initram;
            public uint PeripheralsBase;
            public bool Vinithi;
        }

        private enum PeriphbaseOffsets : ulong
        {
            SnoopControlUnit = 0x0,
            GIC_CPUInterface = 0x100,
            GlobalTimer = 0x200,
            PrivateTimersAndWatchdogs = 0x600,
            GIC_Distributor = 0x1000,
        }

        public class SignalsDictionary<TEnum> : Dictionary<TEnum, Signal<TEnum>>
            where TEnum: struct
        {
            public SignalsDictionary(int coresCount, Func<TEnum, int, Signal<TEnum>> signalConstructor) : base()
            {
                if(!typeof(TEnum).IsEnum)  // System.Enum as a constraint isn't available in C# 7.0.
                {
                    throw new ConstructionException("T must be enum");
                }

                foreach(var signal in Enum.GetValues(typeof(TEnum)))
                {
                    Add((TEnum)signal, signalConstructor((TEnum)signal, coresCount));
                }
            }

            public Signal<TEnum> Parse(string name)
            {
                var ignoreCase = true;
                if(!Enum.TryParse<TEnum>(name, ignoreCase, out var signal))
                {
                    throw new RecoverableException(
                        $"No such signal: {name}\n" +
                        $"Available signals are: { string.Join(", ", Enum.GetNames(typeof(TEnum))) }"
                        );
                }
                return this[signal];
            }

            public void Reset()
            {
                foreach(var signal in Values)
                {
                    signal.Reset();
                }
            }
        }

        public class Signal<TEnum>
            where TEnum: struct
        {
            public Signal(IEmulationElement parent, TEnum signal, int bits, bool isInput, ulong resetValue = 0)
            {
                if(!typeof(TEnum).IsEnum)  // System.Enum as a constraint isn't available in C# 7.0.
                {
                    throw new ConstructionException("T must be enum");
                }
                var name = Enum.GetName(typeof(TEnum), signal) ?? throw new ConstructionException("Invalid signal");

                if(bits < 1 || bits > 64)
                {
                    throw new ConstructionException($"Invalid signal width for {name}: {bits}");
                }

                if (resetValue > (1u << bits) - 1)
                {
                    throw new ConstructionException($"Invalid reset value for {bits}-bit signal '{name}': {resetValue}");
                }

                IsInput = isInput;
                ResetValue = resetValue;
                Name = name;

                this.Bits = bits;
                this.parent = parent;
                this.value = ResetValue;
            }

            public bool IsBitSet(byte position)
            {
                if(position >= Bits)
                {
                    throw new ArgumentException($"Signal.GetBit: {Name} signal has {Bits} bits. Requested: {position}");
                }
                return BitHelper.IsBitSet(Value, position);
            }

            public virtual void Reset()
            {
                value = ResetValue;
            }

            public void SetBit(bool state, byte position)
            {
                var newValue = Value;
                BitHelper.SetBit(ref newValue, position, state);
                Value = newValue;
            }

            public virtual ulong Value
            {
                get
                {
                    return value;
                }

                set
                {
                    CheckSetValue(value);

                    this.value = value;
                }
            }

            protected void CheckSetValue(ulong value)
            {
                if(value > 1u << (Bits - 1))
                {
                    Log(LogLevel.Warning, "Tried to set {0}-bit signal to 0x{1:X}; available bits will be set", Bits, value);
                }
            }

            protected void Log(LogLevel level, string message, params object[] args)
            {
                parent.Log(level, $"{Name}: {message}", args);
            }

            public int Bits { get; }
            public bool IsInput { get; }
            public string Name { get; }
            public ulong ResetValue { get; }

            private ulong value;

            private readonly IEmulationElement parent;
        }

        public class SignalWithImmediateEffect<TEnum> : Signal<TEnum>
            where TEnum: struct
        {
            public static SignalWithImmediateEffect<TEnum> CreateInput(IEmulationElement parent, TEnum signal, int bits, Action<ulong> setter,
                                                                       bool callSetterAtInitAndReset = false, ulong resetValue = 0)
            {
                if(setter == null)
                {
                    throw new ConstructionException("Setter cannot be null for input signal with immediate effect.");
                }
                var isInput = true;
                return new SignalWithImmediateEffect<TEnum>(parent, signal, bits, isInput, null, setter, callSetterAtInitAndReset, resetValue);
            }

            public static SignalWithImmediateEffect<TEnum> CreateOutput(IEmulationElement parent, TEnum signal, int bits, Func<ulong> getter)
            {
                if(getter == null)
                {
                    throw new ConstructionException("Getter cannot be null for output signal with immediate effect.");
                }
                var isInput = false;
                return new SignalWithImmediateEffect<TEnum>(parent, signal, bits, isInput, getter);
            }

            private SignalWithImmediateEffect(IEmulationElement parent, TEnum signal, int bits, bool isInput, Func<ulong> getter = null,
                            Action<ulong> setter = null, bool callSetterAtInitAndReset = false, ulong resetValue = 0)
                            : base(parent, signal, bits, isInput, resetValue)
            {
                this.callSetterAtInitAndReset = callSetterAtInitAndReset;
                this.getter = getter;
                this.setter = setter;

                Reset();
            }

            public override void Reset()
            {
                if(IsInput && callSetterAtInitAndReset)
                {
                    SetValue(ResetValue);
                }

                // base.Reset ignored intentionally.
            }

            public override ulong Value
            {
                get
                {
                    return getter();
                }

                set
                {
                    SetValue(value);
                }
            }

            private void SetValue(ulong value)
            {
                CheckSetValue(value);

                setter(value);
            }

            private readonly bool callSetterAtInitAndReset;
            private readonly Func<ulong> getter;
            private readonly Action<ulong> setter;
        }
    }

    public enum ArmSignals
    {
        DBGROMADDR,
        DBGROMADDRV,
        DBGSELFADDR,
        DBGSELFADDRV,
        INITRAM,
        MFILTEREN,
        MFILTEREND,
        MFILTERSTART,
        PERIPHBASE,
        PFILTEREND,
        PFILTERSTART,
        VINITHI,
    }
}
