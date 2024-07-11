//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities.Binding;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class BaseARMv8 : TranslationCPU
    {
        public BaseARMv8(uint cpuId, string cpuType, IMachine machine, Endianess endianness = Endianess.LittleEndian) : base(cpuId, cpuType, machine, endianness, CpuBitness.Bits64)
        {
            psciCallsHandler = new PSCICallsHandler(this, machine);
        }

        public bool StubPSCICalls
        {
            get
            {
                return stubPSCICalls;
            }
            set
            {
                stubPSCICalls = value;
                TlibStubSmcCalls(stubPSCICalls ? 1u : 0);
            }
        }

        [Export]
        private void HandleSMCCall()
        {
            psciCallsHandler.HandleCall();
        }

        private bool stubPSCICalls;

        private readonly PSCICallsHandler psciCallsHandler;

        private class PSCICallsHandler
        {
            // Currently we support only a subset of available functions and return codes.
            // Full list can be found here: https://github.com/zephyrproject-rtos/zephyr/blob/main/drivers/pm_cpu_ops/pm_cpu_ops_psci.h
            public PSCICallsHandler(TranslationCPU parent, IMachine machine)
            {
                this.parent = parent;
                this.machine = machine;
            }

            public void HandleCall()
            {
                var x0 = (uint)parent.GetRegisterUnsafe((int)ARMv8ARegisters.X0);
                var x1 = (uint)parent.GetRegisterUnsafe((int)ARMv8ARegisters.X1);

                switch((Function)x0)
                {
                    case Function.PSCIVersion:
                        GetPSCIVersion();
                        parent.SetRegisterUnsafe((int)ARMv8ARegisters.X0, PSCICallResultSuccess);
                        break;
                    case Function.CPUOn:
                        UnhaltCpu(x1);
                        break;
                    default:
                        parent.Log(LogLevel.Error, "Encountered an unexpected PSCI call request: 0x{0:X}", x0);
                        parent.SetRegisterUnsafe((int)ARMv8ARegisters.X0, PSCICallResultNotSupported);
                        return;
                }

                // Set return code to success
                parent.SetRegisterUnsafe((int)ARMv8ARegisters.X0, PSCICallResultSuccess);
            }

            private void GetPSCIVersion()
            {
                parent.SetRegisterUnsafe((int)ARMv8ARegisters.X1, PSCIVersion);
            }

            private void UnhaltCpu(uint cpuId)
            {
                var cpu = machine.SystemBus.GetCPUs().Where(x => x.MultiprocessingId == cpuId).Single();
                cpu.IsHalted = false;
            }

            private const int PSCICallResultSuccess = 0;
            private const int PSCICallResultNotSupported = -1;

            private const int PSCIVersion = 2;

            private readonly TranslationCPU parent;
            private readonly IMachine machine;

            private enum Function : uint
            {
                PSCIVersion = 0x84000000,
                CPUOn = 0xC4000003,
            }
        }

#pragma warning disable 649
        [Import]
        private ActionUInt32 TlibStubSmcCalls;
#pragma warning restore 649
    }
}
