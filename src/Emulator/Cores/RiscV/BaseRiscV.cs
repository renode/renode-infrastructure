//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using Endianess = ELFSharp.ELF.Endianess;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class BaseRiscV : TranslationCPU
    {
        protected BaseRiscV(CoreLevelInterruptor clint, uint hartId, string cpuType, Machine machine, PrivilegeArchitecture privilegeArchitecture, Endianess endianness, CpuBitness bitness) : base(cpuType, machine, endianness, bitness)
        {
            HartId = hartId;
            clint?.RegisterCPU(this);
            this.clint = clint;
            this.privilegeArchitecture = privilegeArchitecture;
            ShouldEnterDebugMode = true;
            nonstandardCSR = new Dictionary<ulong, Tuple<Func<ulong>, Action<ulong>>>();

            var architectureSets = DecodeArchitecture(cpuType);
            foreach(var @set in architectureSets)
            {
                if(Enum.IsDefined(typeof(InstructionSet), set))
                {
                    TlibAllowFeature((uint)set);
                }
                else if((int)set == 'G' - 'A')
                {
                    //G is a wildcard denoting multiple instruction sets
                    foreach(var gSet in new[] { InstructionSet.I, InstructionSet.M, InstructionSet.F, InstructionSet.D, InstructionSet.A })
                    {
                        TlibAllowFeature((uint)gSet);
                    }
                }
                else
                {
                    this.Log(LogLevel.Warning, $"Undefined instruction set: {char.ToUpper((char)(set + 'A'))}.");
                }
            }
            TlibSetPrivilegeArchitecture109(privilegeArchitecture == PrivilegeArchitecture.Priv1_09 ? 1 : 0u);
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!IsValidInterrupt(number))
            {
                throw new ArgumentOutOfRangeException($"Unsupported exception #{number}");
            }

            // we don't log warning when value is false to handle gpio initial reset
            if(privilegeArchitecture == PrivilegeArchitecture.Priv1_10 && !IsValidInterruptInV10(number) && value)
            {
                this.Log(LogLevel.Warning, "Interrupt {0} not supported in Privileged ISA v1.09", (IrqType)number);
                return;
            }

            TlibSetMipBit((uint)number, value ? 1u : 0u);
            base.OnGPIO(number, value);
        }

        public bool SupportsInstructionSet(InstructionSet set)
        {
            return TlibIsFeatureAllowed((uint)set) == 1;
        }

        public bool IsInstructionSetEnabled(InstructionSet set)
        {
            return TlibIsFeatureEnabled((uint)set) == 1;
        }

        public override void Reset()
        {
            base.Reset();
            ShouldEnterDebugMode = true;
        }

        public void RegisterCSR(ulong csr, Func<ulong> readOperation, Action<ulong> writeOperation)
        {
            nonstandardCSR.Add(csr, Tuple.Create(readOperation, writeOperation));
        }

        public uint HartId
        {
            get
            {
                return TlibGetHartId();
            }

            set
            {
                TlibSetHartId(value);
            }
        }

        public bool ShouldEnterDebugMode { get; set; }

        protected override Interrupt DecodeInterrupt(int number)
        {
            return Interrupt.Hard;
        }

        private IEnumerable<InstructionSet> DecodeArchitecture(string architecture)
        {
            //The architecture name is: RV{architecture_width}{list of letters denoting instruction sets}
            return architecture.Skip(2).SkipWhile(x => Char.IsDigit(x))
                               .Select(x => (InstructionSet)(Char.ToUpper(x) - 'A'));
        }

        [Export]
        private uint IsInDebugMode()
        {
            return (gdbStub?.DebuggerConnected == true && ShouldEnterDebugMode && ExecutionMode == ExecutionMode.SingleStep) ? 1u : 0u;
        }

        [Export]
        private ulong GetCPUTime()
        {
            if(clint == null)
            {
                this.Log(LogLevel.Warning, "Trying to read CPU time from CLINT, but CLINT is not registered.");
                return 0;
            }

            var numberOfExecutedInstructions = checked((ulong)TlibGetExecutedInstructions());
            if(numberOfExecutedInstructions > 0)
            {
                var elapsed = TimeInterval.FromCPUCycles(numberOfExecutedInstructions, PerformanceInMips, out var residuum);
                TlibResetExecutedInstructions(checked((int)residuum));
                machine.HandleTimeProgress(elapsed);
            }
            return clint.TimerValue;
        }

        [Export]
        private int HasCSR(ulong csr)
        {
            if(nonstandardCSR.ContainsKey(csr))
            {
                return 1;
            }
            this.Log(LogLevel.Noisy, "Missing nonstandard CSR: 0x{0:X}", csr);
            return 0;
        }

        [Export]
        private ulong ReadCSR(ulong csr)
        {
            var readMethod = nonstandardCSR[csr].Item1;
            if(readMethod == null)
            {
                this.Log(LogLevel.Warning, "Read method is not implemented for CSR=0x{0:X}", csr);
                return 0;
            }
            return readMethod();
        }

        [Export]
        private void WriteCSR(ulong csr, ulong value)
        {
            var writeMethod = nonstandardCSR[csr].Item2;
            if(writeMethod == null)
            {
                this.Log(LogLevel.Warning, "Write method is not implemented for CSR=0x{0:X}", csr);
            }
            else
            {
                writeMethod(value);
            }
        }

        private readonly CoreLevelInterruptor clint;

        private readonly PrivilegeArchitecture privilegeArchitecture;

        private readonly Dictionary<ulong, Tuple<Func<ulong>, Action<ulong>>> nonstandardCSR;

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649
        [Import]
        private ActionUInt32 TlibAllowFeature;

        [Import]
        private FuncUInt32UInt32 TlibIsFeatureEnabled;

        [Import]
        private FuncUInt32UInt32 TlibIsFeatureAllowed;

        [Import]
        private ActionInt32 TlibResetExecutedInstructions;

        [Import(Name="tlib_set_privilege_architecture_1_09")]
        private ActionUInt32 TlibSetPrivilegeArchitecture109;

        [Import]
        private ActionUInt32UInt32 TlibSetMipBit;

        [Import]
        private ActionUInt32 TlibSetHartId;

        [Import]
        private FuncUInt32 TlibGetHartId;
#pragma warning restore 649

        public enum PrivilegeArchitecture
        {
            Priv1_09,
            Priv1_10
        }

        /* The enabled instruction sets are exposed via a register. Each instruction bit is represented
         * by a single bit, in alphabetical order. E.g. bit 0 represents set 'A', bit 12 represents set 'M' etc.
         */
        public enum InstructionSet
        {
            I = 'I' - 'A',
            M = 'M' - 'A',
            A = 'A' - 'A',
            F = 'F' - 'A',
            D = 'D' - 'A',
            C = 'C' - 'A',
            S = 'S' - 'A',
            U = 'U' - 'A',
        }

        private static bool IsValidInterrupt(int irq)
        {
            return irq >= (int)IrqType.UserSoftwareInterrupt && irq <= (int)IrqType.MachineExternalInterrupt;
        }

        private static bool IsValidInterruptInV10(int irq)
        {
            return irq != (int)IrqType.HypervisorExternalInterrupt
                && irq != (int)IrqType.HypervisorSoftwareInterrupt
                && irq != (int)IrqType.HypervisorTimerInterrupt;
        }

        protected enum IrqType
        {
            UserSoftwareInterrupt = 0x0,
            SupervisorSoftwareInterrupt = 0x1,
            HypervisorSoftwareInterrupt = 0x2,
            MachineSoftwareInterrupt = 0x3,
            UserTimerInterrupt = 0x4,
            SupervisorTimerInterrupt = 0x5,
            HypervisorTimerInterrupt = 0x6,
            MachineTimerInterrupt = 0x7,
            UserExternalInterrupt = 0x8,
            SupervisorExternalInterrupt = 0x9,
            HypervisorExternalInterrupt = 0xa,
            MachineExternalInterrupt = 0xb
        }
    }
}

