//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Antmicro.Renode.Core;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 4)]
    public abstract class BaseARMv8 : TranslationCPU, ICPUWithPSCI, IArmWithSystemRegisters
    {
        public BaseARMv8(uint cpuId, string cpuType, IMachine machine, Endianess endianness = Endianess.LittleEndian) : base(cpuId, cpuType, machine, endianness, CpuBitness.Bits64)
        {
            this.customFunctionHandlers = new Dictionary<ulong, Action>();
            this.rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        }

        public string[,] GetAllSystemRegisterValues()
        {
            var table = new Table().AddRow("Name", "Value");
            foreach(var indexSystemRegisterPair in SystemRegistersDictionary)
            {
                // Value is 0 if the attempt is unsuccessful so we don't need to care about the result.
                _ = TryGetSystemRegisterValue(indexSystemRegisterPair.Key, out var value, logUnhandledAccess: false);
                table.AddRow(indexSystemRegisterPair.Value.Name, $"0x{value:X}");
            }
            return table.ToArray();
        }

        public ulong GetSystemRegisterValue(string name)
        {
            ValidateSystemRegisterAccess(name, isWrite: false);

            return TlibGetSystemRegister(name, 1u /* log_unhandled_access: true */);
        }

        public void SetSystemRegisterValue(string name, ulong value)
        {
            ValidateSystemRegisterAccess(name, isWrite: true);

            TlibSetSystemRegister(name, value, 1u /* log_unhandled_access: true */);
        }

        public bool TryGetSystemRegisterValue(ArmSystemRegisterEncoding encoding, out ulong value)
        {
            value = 0;
            return TryGetSystemRegisterIndex(encoding, out var systemRegisterIndex)
                   && TryGetSystemRegisterValue(systemRegisterIndex, out value, logUnhandledAccess: false);
        }

        public bool TrySetSystemRegisterValue(ArmSystemRegisterEncoding encoding, ulong value)
        {
            return TryGetSystemRegisterIndex(encoding, out var systemRegisterIndex)
                   && TrySetSystemRegisterValue(systemRegisterIndex, value);
        }

        public void AddCustomPSCIHandler(ulong functionIdentifier, Action stub)
        {
            try
            {
                customFunctionHandlers.Add(functionIdentifier, stub);
            }
            catch(ArgumentException)
            {
                throw new RecoverableException(string.Format("There's already a handler for a function: 0x{0:X}", functionIdentifier));
            }
            this.Log(LogLevel.Debug, "Adding a handler for PSCI function: 0x{0:X}", functionIdentifier);
        }

        public PSCIConduitEmulationMethod PSCIEmulationMethod
        {
            get
            {
                return psciEmulationMethod;
            }
            set
            {
                psciEmulationMethod = value;
                TlibPsciHandlerEnable((uint)psciEmulationMethod);
            }
        }

        public bool RNDRSupported
        {
            set
            {
                TlibSetRndrSupported(value ? 1u : 0);
            }
        }

        public abstract ExecutionState ExecutionState { get; }
        public abstract ExecutionState[] SupportedExecutionStates { get; }

        protected void AddSystemRegistersFeature(List<GDBFeatureDescriptor> features, string featureName)
        {
            var systemRegistersFeature = new GDBFeatureDescriptor(featureName);
            foreach(var indexSystemRegisterPair in SystemRegistersDictionary)
            {
                var width = indexSystemRegisterPair.Value.Width;
                systemRegistersFeature.Registers.Add(new GDBRegisterDescriptor(indexSystemRegisterPair.Key, width, indexSystemRegisterPair.Value.Name, $"uint{width}"));
            }
            features.Add(systemRegistersFeature);
        }

        protected IEnumerable<CPURegister> GetNonMappedRegisters()
        {
            return SystemRegistersDictionary.Select(indexRegisterPair => new CPURegister((int)indexRegisterPair.Key, (int)indexRegisterPair.Value.Width, false, false));
        }

        protected bool TryGetNonMappedRegister(int index, out RegisterValue value)
        {
            // This method will be mostly used by GDB so let's prevent unhandled access logs.
            // Otherwise, 'info all-registers' generates a lot of warnings.
            var result = TryGetSystemRegisterValue((uint)index, out var ulongValue, logUnhandledAccess: false);
            var width = SystemRegistersDictionary.TryGetValue((uint)index, out var register) ? register.Width : (uint)bitness;

            value = RegisterValue.Create(ulongValue, width);
            return result;
        }

        protected bool TrySetNonMappedRegister(int index, RegisterValue value)
        {
            return TrySetSystemRegisterValue((uint)index, value);
        }

        protected Dictionary<uint, SystemRegister> SystemRegistersDictionary
        {
            get
            {
                if(systemRegisters == null)
                {
                    systemRegisters = new Dictionary<uint, SystemRegister>();

                    var array = IntPtr.Zero;
                    var arrayPointer = Marshal.AllocHGlobal(IntPtr.Size);
                    try
                    {
                        var count = TlibCreateSystemRegistersArray(arrayPointer);
                        if(count == 0)
                        {
                            return systemRegisters;
                        }
                        array = Marshal.ReadIntPtr(arrayPointer);

                        var ArmCpRegInfoPointersArray = new IntPtr[count];
                        Marshal.Copy(array, ArmCpRegInfoPointersArray, 0, (int)count);

                        var lastRegisterIndex = Enum.GetValues(RegistersEnum).Cast<uint>().Max();
                        systemRegisters = ArmCpRegInfoPointersArray
                            .Select(armCpRegInfoPointer => ARMCPRegInfo.FromIntPtr(armCpRegInfoPointer).ToSystemRegister())
                            // Currently, GIC and Generic Timer system registers can only be accessed by software.
                            // Let's not add them to the dictionary so that GDB won't fail on read until it's fixed.
                            .Where(systemRegister => !IsGICOrGenericTimerSystemRegister(systemRegister))
                            .OrderBy(systemRegister => systemRegister.Name)
                            .ToDictionary(_ => ++lastRegisterIndex);
                    }
                    finally
                    {
                        if(array != IntPtr.Zero)
                        {
                            Free(array);
                        }
                        Marshal.FreeHGlobal(arrayPointer);
                    }
                }
                return systemRegisters;
            }
        }

        protected abstract Type RegistersEnum { get; }

#pragma warning disable 649
        [Import]
        protected Action<GICCPUInterfaceVersion> TlibSetGicCpuRegisterInterfaceVersion;

        [Import]
        protected Func<string, uint, uint> TlibCheckSystemRegisterAccess;

        [Import]
        // The arguments are: char *name, bool log_unhandled_access.
        protected Func<string, uint, ulong> TlibGetSystemRegister;

        [Import]
        // The arguments are: char *name, uint64_t value, bool log_unhandled_access.
        protected Action<string, ulong, uint> TlibSetSystemRegister;
#pragma warning restore 649

        private bool IsGICOrGenericTimerSystemRegister(SystemRegister systemRegister)
        {
            return TlibIsGicOrGenericTimerSystemRegister(systemRegister.Name) == 1u;
        }

        private bool TryGetSystemRegisterIndex(ArmSystemRegisterEncoding encoding, out uint index)
        {
            index = uint.MaxValue;
            var matchingEntries = SystemRegistersDictionary.Where(entry => encoding.Equals(entry.Value.Encoding));
            DebugHelper.Assert(matchingEntries.Count() <= 1);

            if(!matchingEntries.Any())
            {
                this.Log(LogLevel.Warning, "Unknown {0}", encoding);
                return false;
            }
            index = matchingEntries.Single().Key;
            return true;
        }

        private bool TryGetSystemRegisterValue(uint index, out ulong value, bool logUnhandledAccess)
        {
            if(SystemRegistersDictionary.TryGetValue(index, out var systemRegister))
            {
                // ValidateSystemRegisterAccess isn't used because most of its checks aren't needed.
                // The register must exist at this point cause it's in the dictionary built based on tlib
                // and we don't really care about the invalid access type error for unreadable registers.
                value = TlibGetSystemRegister(systemRegister.Name, logUnhandledAccess ? 1u : 0u);
                return true;
            }
            value = 0;
            return false;
        }

        private bool TrySetSystemRegisterValue(uint index, ulong value)
        {
            if(SystemRegistersDictionary.TryGetValue(index, out var systemRegister))
            {
                ValidateSystemRegisterAccess(systemRegister.Name, isWrite: true);
                TlibSetSystemRegister(systemRegister.Name, value, 1u /* log_unhandled_access: true */);
                return true;
            }
            return false;
        }

        private void ValidateSystemRegisterAccess(string name, bool isWrite)
        {
            switch((SystemRegisterCheckReturnValue)TlibCheckSystemRegisterAccess(name, isWrite ? 1u : 0u))
            {
            case SystemRegisterCheckReturnValue.AccessValid:
                return;
            case SystemRegisterCheckReturnValue.AccessorNotFound:
                var accessName = isWrite ? "Writing" : "Reading";
                throw new RecoverableException($"{accessName} the {name} register isn't supported.");
            case SystemRegisterCheckReturnValue.RegisterNotFound:
                throw new RecoverableException($"No such register: {name}.");
            default:
                throw new ArgumentException("Invalid TlibCheckSystemRegisterAccess return value!");
            }
        }

        [Export]
        private void HandlePSCICall()
        {
            var x0 = (uint)GetRegister((int)ARMv8ARegisters.X0);
            var x1 = (ulong)GetRegister((int)ARMv8ARegisters.X1);
            var x2 = (ulong)GetRegister((int)ARMv8ARegisters.X2);
            var x3 = (ulong)GetRegister((int)ARMv8ARegisters.X3);

            this.Log(LogLevel.Debug, "PSCI call, function: 0x{0:X}, with parameters: x1=0x{1:X}, x2=0x{2:X}, x3=0x{3:X}", x0, x1, x2, x3);

            if(customFunctionHandlers.TryGetValue(x0, out var handler))
            {
                handler();
                return;
            }

            switch((Function)x0)
            {
                case Function.PSCIVersion:
                    GetPSCIVersion();
                    break;
                case Function.CPUOn:
                    UnhaltCpu((uint)x1, x2);
                    break;
                default:
                    this.Log(LogLevel.Error, "Encountered an unexpected PSCI call request: 0x{0:X}", x0);
                    SetRegister((int)ARMv8ARegisters.X0, PSCICallResultNotSupported);
                    return;
            }

            // Set return code to success
            SetRegister((int)ARMv8ARegisters.X0, PSCICallResultSuccess);
        }

        [Export]
        private ulong GetRandomUlong()
        {
            return rng.NextUlong();
        }

        private void GetPSCIVersion()
        {
            SetRegister((int)ARMv8ARegisters.X1, PSCIVersion);
        }

        private void UnhaltCpu(uint cpuId, ulong entryPoint)
        {
            var cpu = machine.SystemBus.GetCPUs().Where(x => x.MultiprocessingId == cpuId).SingleOrDefault();
            if(cpu == null)
            {
                this.Log(LogLevel.Error, "Could not find CPU with given ID: 0x{0:X}", cpuId);
                return;
            }
            this.Log(LogLevel.Info, "Starting {0} (MPIDR=0x{1:X}) with PSCI CPU_ON function at 0x{2:X}", cpu.GetName(), cpu.MultiprocessingId, entryPoint);
            cpu.PC = entryPoint;
            cpu.IsHalted = false;
        }

        private Dictionary<uint, SystemRegister> systemRegisters;

        private PSCIConduitEmulationMethod psciEmulationMethod;
        private PseudorandomNumberGenerator rng;

#pragma warning disable 649
        [Import]
        private Action<uint> TlibPsciHandlerEnable;

        [Import]
        private Action<uint> TlibSetRndrSupported;

        [Import]
        private Func<IntPtr, uint> TlibCreateSystemRegistersArray;

        [Import]
        private Func<string, uint> TlibIsGicOrGenericTimerSystemRegister;
#pragma warning restore 649

        private readonly Dictionary<ulong, Action> customFunctionHandlers;
        private const int PSCICallResultSuccess = 0;
        private const int PSCICallResultNotSupported = -1;
        private const int PSCIVersion = 2;

        public enum PSCIConduitEmulationMethod
        {
            None = 0, // No PSCI emulation - we have a firmware which handles the calls natively, or no PSCI interface
            SMC = 1,  // Emulate PSCI calls over SMC (Secure Monitor Call) instruction
            HVC = 2,  // Emulate PSCI calls over HVC (HyperVisor Call) instruction
        }

        protected struct SystemRegister
        {
            public uint Width => Encoding.Width;

            public string Name;
            public ArmSystemRegisterEncoding Encoding;
            public uint Type;

            // Keep in sync with tlib/arch/arm_common/system_registers_common.h
            public const byte TypeFlagShift64Bit = 8;  // ARM_CP_64BIT
        }

        protected enum GICCPUInterfaceVersion : uint
        {
            None = 0b000,
            Version30Or40 = 0b001,
            Version41 = 0b011,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ARMCPRegInfo
        {
            public static ARMCPRegInfo FromIntPtr(IntPtr pointer)
            {
                return (ARMCPRegInfo)Marshal.PtrToStructure(pointer, typeof(ARMCPRegInfo));
            }

            public SystemRegister ToSystemRegister()
            {
                var width = BitHelper.IsBitSet(Type, SystemRegister.TypeFlagShift64Bit) ? 64u : 32u;
                return new SystemRegister
                {
                    Name = Marshal.PtrToStringAnsi(Name),
                    Type = Type,
                    Encoding = new ArmSystemRegisterEncoding((ArmSystemRegisterEncoding.CoprocessorEnum)Coprocessor, crm: Crm, op1: Op1, crn: Crn, op0: Op0, op2: Op2, width: width),
                };
            }

            // These have to be in line with tlib/arch/arm_common/system_registers_common.h
            public IntPtr Name;
            public uint Coprocessor;
            public uint Type;

            public byte Op0;
            public byte Op1;
            public byte Crn;
            public byte Crm;
            public byte Op2;

            public uint FieldOffset;
            public ulong ResetValue;
            public IntPtr AccessFunction;
            public IntPtr ReadFunction;
            public IntPtr WriteFunction;
            public bool IsDynamic;
        };

        // Currently we support only a subset of available functions and return codes.
        // Full list can be found here: https://github.com/zephyrproject-rtos/zephyr/blob/main/drivers/pm_cpu_ops/pm_cpu_ops_psci.h
        private enum Function : uint
        {
            PSCIVersion = 0x84000000,
            CPUOn = 0xC4000003,
        }

        private enum SystemRegisterCheckReturnValue
        {
            RegisterNotFound = 1,
            AccessorNotFound = 2,
            AccessValid = 3,
        }
    }
}
