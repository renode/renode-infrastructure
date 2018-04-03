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
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Renode.Utilities.Collections;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 3)]
    public partial class RiscV : TranslationCPU
    {
        public RiscV(string cpuType, long frequency, Machine machine, PrivilegeMode privilegeMode = PrivilegeMode.Priv1_10, Endianess endianness = Endianess.LittleEndian) : base(cpuType, machine, endianness)
        {
            InnerTimer = new ComparingTimer(machine.ClockSource, frequency, enabled: true, eventEnabled: true);

            intTypeToVal = new TwoWayDictionary<int, IrqType>();
            intTypeToVal.Add(0, IrqType.MachineTimerIrq);
            intTypeToVal.Add(1, IrqType.MachineExternalIrq);
            intTypeToVal.Add(2, IrqType.MachineSoftwareInterrupt);

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
                    foreach(var gSet in new [] { InstructionSet.I, InstructionSet.M, InstructionSet.F, InstructionSet.D, InstructionSet.A })
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
            if(!intTypeToVal.TryGetValue(number, out IrqType decodedType))
            {
                throw new ArgumentOutOfRangeException(nameof(number));
            }

            var mipState = TlibGetMip();
            BitHelper.SetBit(ref mipState, (byte)decodedType, value);
            TlibSetMip(mipState);

            base.OnGPIO(number, mipState != 0);
        }

        public bool SupportsInstructionSet(InstructionSet set)
        {
            return TlibIsFeatureAllowed((uint)set) == 1;
        }

        public bool IsInstructionSetEnabled(InstructionSet set)
        {
            return TlibIsFeatureEnabled((uint)set) == 1;
        }

        public override string Architecture { get { return "riscv"; } }

        public ComparingTimer InnerTimer { get; set; }

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
        private void MipChanged(uint mip)
        {
            var previousMip = BitHelper.GetBits(TlibGetMip());
            var currentMip = BitHelper.GetBits(mip);

            foreach(var gpio in intTypeToVal.Lefts)
            {
                intTypeToVal.TryGetValue(gpio, out IrqType decodedType);
                if(previousMip[(int)decodedType] != currentMip[(int)decodedType])
                {
                    OnGPIO(gpio, currentMip[(int)decodedType]);
                }
            }
        }

        [Export]
        private ulong GetCPUTime()
        {
            return InnerTimer.Value;
        }

        private TwoWayDictionary<int, IrqType> intTypeToVal;

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649
        [Import]
        private FuncUInt32 TlibGetMip;

        [Import]
        private ActionUInt32 TlibSetMip;

        [Import]
        private ActionUInt32 TlibAllowFeature;

        [Import]
        private FuncUInt32UInt32 TlibIsFeatureEnabled;

        [Import]
        private FuncUInt32UInt32 TlibIsFeatureAllowed;

        [Import(Name="tlib_set_privilege_mode_1_09")]
        private ActionUInt32 TlibSetPrivilegeMode109;
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

        private enum IrqType
        {
            MachineSoftwareInterrupt = 0x3,
            MachineTimerIrq = 0x7,
            MachineExternalIrq = 0xb
        }
    }
}

