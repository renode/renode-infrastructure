//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities.Binding;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 4)]
    public abstract class BaseARMv8 : TranslationCPU, ICPUWithPSCI
    {
        public BaseARMv8(uint cpuId, string cpuType, IMachine machine, Endianess endianness = Endianess.LittleEndian) : base(cpuId, cpuType, machine, endianness, CpuBitness.Bits64)
        {
            this.customFunctionHandlers = new Dictionary<ulong, Action>();
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

        private PSCIConduitEmulationMethod psciEmulationMethod;

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

        protected enum GICCPUInterfaceVersion : uint
        {
            None = 0b000,
            Version30Or40 = 0b001,
            Version41 = 0b011,
        }

        // Currently we support only a subset of available functions and return codes.
        // Full list can be found here: https://github.com/zephyrproject-rtos/zephyr/blob/main/drivers/pm_cpu_ops/pm_cpu_ops_psci.h
        private enum Function : uint
        {
            PSCIVersion = 0x84000000,
            CPUOn = 0xC4000003,
        }

#pragma warning disable 649
        [Import]
        private Action<uint> TlibPsciHandlerEnable;
#pragma warning restore 649
    }
}
