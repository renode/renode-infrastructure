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

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class BaseRiscV : TranslationCPU
    {
        protected BaseRiscV(CoreLevelInterruptor clint, uint hartId, string cpuType, Machine machine, PrivilegeMode privilegeMode, Endianess endianness, CpuBitness bitness) : base(cpuType, machine, endianness, bitness)
        {
            HartId = hartId;
            clint.RegisterCPU(this);
            this.clint = clint;

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
            TlibSetPrivilegeMode109(privilegeMode == PrivilegeMode.Priv1_09 ? 1 : 0u);
        }

        public override void OnGPIO(int number, bool value)
        {

            TlibSetInterrupt((uint)number, value ? 1u : 0u);

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

        protected override Interrupt DecodeInterrupt(int number)
        {
            if(number == 0 || number == 1 || number == 2)
            {
                return Interrupt.Hard;
            }
            throw InvalidInterruptNumberException;
        }

        private IEnumerable<InstructionSet> DecodeArchitecture(string architecture)
        {
            //The architecture name is: RV{architecture_width}{list of letters denoting instruction sets}
            return architecture.Skip(2).SkipWhile(x => Char.IsDigit(x))
                               .Select(x => (InstructionSet)(Char.ToUpper(x) - 'A'));
        }

        [Export]
        private ulong GetCPUTime()
        {
            var numberOfExecutedInstructions = checked((ulong)TlibGetExecutedInstructions());
            if(numberOfExecutedInstructions > 0)
            {
                var elapsed = TimeInterval.FromCPUCycles(numberOfExecutedInstructions, PerformanceInMips, out var residuum);
                TlibResetExecutedInstructions(checked((int)residuum));
                machine.HandleTimeProgress(elapsed);
            }
            return clint.TimerValue;
        }


        private readonly CoreLevelInterruptor clint;

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

        [Import(Name="tlib_set_privilege_mode_1_09")]
        private ActionUInt32 TlibSetPrivilegeMode109;

        [Import]
        private ActionUInt32UInt32 TlibSetInterrupt;

        [Import]
        private ActionUInt32 TlibSetHartId;

        [Import]
        private FuncUInt32 TlibGetHartId;
#pragma warning restore 649

        public enum PrivilegeMode
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

        protected enum IrqType
        {
            SupervisorSoftwareInterrupt = 0x1,
            MachineSoftwareInterrupt = 0x3,
            SupervisorTimerIrq = 0x5,
            MachineTimerIrq = 0x7,
            SupervisorExternalIrq = 0x9,
            MachineExternalIrq = 0xb
        }
    }
}

