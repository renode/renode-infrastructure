//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class BaseRiscV : TranslationCPU
    {
        protected BaseRiscV(IRiscVTimeProvider timeProvider, uint hartId, string cpuType, Machine machine, PrivilegeArchitecture privilegeArchitecture, Endianess endianness, CpuBitness bitness, ulong? nmiVectorAddress = null, uint? nmiVectorLength = null)
                : base(hartId, cpuType, machine, endianness, bitness)
        {
            HartId = hartId;
            this.timeProvider = timeProvider;
            this.privilegeArchitecture = privilegeArchitecture;
            ShouldEnterDebugMode = true;
            nonstandardCSR = new Dictionary<ulong, Tuple<Func<ulong>, Action<ulong>>>();
            customInstructionsMapping = new Dictionary<ulong, Action<UInt64>>();
            this.NMIVectorLength = nmiVectorLength;
            this.NMIVectorAddress = nmiVectorAddress;

            architectureSets = DecodeArchitecture(cpuType);
            EnableArchitectureVariants();

            if(this.NMIVectorAddress.HasValue && this.NMIVectorLength.HasValue && this.NMIVectorLength > 0)
            {
                this.Log(LogLevel.Noisy, "Non maskable interrupts enabled with paramters: {0} = {1}, {2} = {3}",
                        nameof(this.NMIVectorAddress), this.NMIVectorAddress, nameof(this.NMIVectorLength), this.NMIVectorLength);
                TlibSetNmiVector(this.NMIVectorAddress.Value, this.NMIVectorLength.Value);
            }
            else
            {
                this.Log(LogLevel.Noisy, "Non maskable interrupts disabled");
                TlibSetNmiVector(0, 0);
            }

            UserState = new Dictionary<string, object>();
        }

        public virtual void OnNMI(int number, bool value)
        {
            if(this.NMIVectorLength == null || this.NMIVectorAddress == null)
            {
                this.Log(LogLevel.Warning, "Non maskable interrupt not supported on this CPU. {0} or {1} not set",
                        nameof(this.NMIVectorAddress) , nameof(this.NMIVectorLength));
            }
            else
            {
                TlibSetNmi(number, value ? 1 : 0);
            }
        }

        public override void OnGPIO(int number, bool value)
        {

            // we don't log warning when value is false to handle gpio initial reset
            if(privilegeArchitecture >= PrivilegeArchitecture.Priv1_10 && IsValidInterruptOnlyInV1_09(number) && value)
            {
                this.Log(LogLevel.Warning, "Interrupt {0} not supported since Privileged ISA v1.10", (IrqType)number);
                return;
            }
            else if(IsUniplementedInterrupt(number) && value)
            {
                this.Log(LogLevel.Warning, "Interrupt {0} not supported", (IrqType)number);
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
            EnableArchitectureVariants();
            foreach(var key in simpleCSRs.Keys.ToArray())
            {
                simpleCSRs[key] = 0;
            }
            UserState.Clear();
        }

        public void RegisterCustomCSR(string name, uint number, PrivilegeLevel mode)
        {
            var customCSR = new SimpleCSR(name, number, mode);
            if(simpleCSRs.Keys.Any(x => x.Number == customCSR.Number))
            {
                throw new ConstructionException($"Cannot register CSR {customCSR.Name}, because its number 0x{customCSR.Number:X} is already registered");
            }
            simpleCSRs.Add(customCSR, 0);
            RegisterCSR(customCSR.Number, () => simpleCSRs[customCSR], value => simpleCSRs[customCSR] = value);
        }

        public void RegisterCSR(ulong csr, Func<ulong> readOperation, Action<ulong> writeOperation)
        {
            nonstandardCSR.Add(csr, Tuple.Create(readOperation, writeOperation));
        }

        public void SilenceUnsupportedInstructionSet(InstructionSet set, bool silent = true)
        {
            TlibMarkFeatureSilent((uint)set, silent ? 1 : 0u);
        }

        public bool InstallCustomInstruction(string pattern, Action<UInt64> handler)
        {
            if(pattern == null)
            {
                throw new ArgumentException("Pattern cannot be null");
            }
            if(handler == null)
            {
                throw new ArgumentException("Handler cannot be null");
            }

            if(pattern.Length != 64 && pattern.Length != 32 && pattern.Length != 16)
            {
                throw new RecoverableException($"Unsupported custom instruction length: {pattern.Length}. Supported values are: 16, 32, 64 bits");
            }

            var currentBit = pattern.Length - 1;
            var bitMask = 0uL;
            var bitPattern = 0uL;

            foreach(var p in pattern)
            {
                switch(p)
                {
                    case '0':
                        bitMask |= (1uL << currentBit);
                        break;

                    case '1':
                        bitMask |= (1uL << currentBit);
                        bitPattern |= (1uL << currentBit);
                        break;

                    default:
                        // all characters other than '0' or '1' are treated as 'any-value'
                        break;
                }

                currentBit--;
            }

            var length = (ulong)pattern.Length / 8;
            var id = TlibInstallCustomInstruction(bitMask, bitPattern, length);
            if(id == 0)
            {
                throw new ConstructionException($"Could not install custom instruction handler for length {length}, mask 0x{bitMask:X} and pattern 0x{bitPattern:X}");
            }

            customInstructionsMapping[id] = handler;
            return true;
        }

        public CSRValidationLevel CSRValidation
        {
            get => (CSRValidationLevel)TlibGetCsrValidationLevel();

            set
            {
                TlibSetCsrValidationLevel((uint)value);
            }
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

        public ulong? NMIVectorAddress { get; }

        public uint? NMIVectorLength { get; }

        public bool ShouldEnterDebugMode { get; set; }

        public event Action<ulong> MipChanged;

        public Dictionary<string, object> UserState { get; }

        protected override Interrupt DecodeInterrupt(int number)
        {
            return Interrupt.Hard;
        }

        protected void PCWritten()
        {
            pcWrittenFlag = true;
        }

        protected override string GetExceptionDescription(ulong exceptionIndex)
        {
            var decoded = (exceptionIndex << 1) >> 1;
            var descriptionMap = IsInterrupt(exceptionIndex)
                ? InterruptDescriptionsMap
                : ExceptionDescriptionsMap;

            if(descriptionMap.TryGetValue(decoded, out var result))
            {
                return result;
            }
            return base.GetExceptionDescription(exceptionIndex);
        }

        private bool IsInterrupt(ulong exceptionIndex)
        {
            return BitHelper.IsBitSet(exceptionIndex, MostSignificantBit);
        }

        protected abstract byte MostSignificantBit { get; }

        private void EnableArchitectureVariants()
        {
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
            TlibSetPrivilegeArchitecture((int)privilegeArchitecture);
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
            return (DebuggerConnected == true && ShouldEnterDebugMode && ExecutionMode == ExecutionMode.SingleStep) ? 1u : 0u;
        }

        [Export]
        private ulong GetCPUTime()
        {
            if(timeProvider == null)
            {
                this.Log(LogLevel.Warning, "Trying to read time from CPU, but no time provider is registered.");
                return 0;
            }

            SyncTime();
            return timeProvider.TimerValue;
        }

        [Export]
        private int HasCSR(ulong csr)
        {
            return nonstandardCSR.ContainsKey(csr) ? 1 : 0;
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

        [Export]
        private void TlibMipChanged(ulong value)
        {
            MipChanged?.Invoke(value);
        }

        [Export]
        private int HandleCustomInstruction(UInt64 id, UInt64 opcode)
        {
            if(!customInstructionsMapping.TryGetValue(id, out var handler))
            {
                throw new CpuAbortException($"Unexpected instruction of id {id} and opcode 0x{opcode:X}");
            }

            pcWrittenFlag = false;
            handler(opcode);
            return pcWrittenFlag ? 1 : 0;
        }

        private bool pcWrittenFlag;

        private readonly IRiscVTimeProvider timeProvider;

        private readonly PrivilegeArchitecture privilegeArchitecture;

        private readonly Dictionary<ulong, Tuple<Func<ulong>, Action<ulong>>> nonstandardCSR;

        private readonly IEnumerable<InstructionSet> architectureSets;

        private readonly Dictionary<ulong, Action<UInt64>> customInstructionsMapping;

        private readonly Dictionary<SimpleCSR, ulong> simpleCSRs = new Dictionary<SimpleCSR, ulong>();

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649
        [Import]
        private ActionUInt32 TlibAllowFeature;

        [Import]
        private FuncUInt32UInt32 TlibIsFeatureEnabled;

        [Import]
        private FuncUInt32UInt32 TlibIsFeatureAllowed;

        [Import(Name="tlib_set_privilege_architecture")]
        private ActionInt32 TlibSetPrivilegeArchitecture;

        [Import]
        private ActionUInt32UInt32 TlibSetMipBit;

        [Import]
        private ActionUInt32 TlibSetHartId;

        [Import]
        private FuncUInt32 TlibGetHartId;

        [Import]
        private FuncUInt64UInt64UInt64UInt64 TlibInstallCustomInstruction;

        [Import]
        private ActionUInt32UInt32 TlibMarkFeatureSilent;

        [Import]
        private ActionUInt64UInt32 TlibSetNmiVector;

        [Import]
        private ActionInt32Int32 TlibSetNmi;

        [Import]
        private ActionUInt32 TlibSetCsrValidationLevel;

        [Import]
        private FuncUInt32 TlibGetCsrValidationLevel;

#pragma warning restore 649

        private readonly Dictionary<ulong, string> InterruptDescriptionsMap = new Dictionary<ulong, string>
        {
            {1, "Supervisor software interrupt"},
            {3, "Machine software interrupt"},
            {5, "Supervisor timer interrupt"},
            {7, "Machine timer interrupt"},
            {9, "Supervisor external interrupt"},
            {11, "Machine external interrupt"}
        };

        private readonly Dictionary<ulong, string> ExceptionDescriptionsMap = new Dictionary<ulong, string>
        {
            {0, "Instruction address misaligned"},
            {1, "Instruction access fault"},
            {2, "Illegal instruction"},
            {3, "Breakpoint"},
            {4, "Load address misaligned"},
            {5, "Load access fault"},
            {6, "Store address misaligned"},
            {7, "Store access fault"},
            {8, "Environment call from U-mode"},
            {9, "Environment call from S-mode"},
            {11, "Environment call from M-mode"},
            {12, "Instruction page fault"},
            {13, "Load page fault"},
            {15, "Store page fault"}
        };

        public enum PrivilegeArchitecture
        {
            Priv1_09,
            Priv1_10,
            Priv1_11
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

        /* Since Priv 1.10 all hypervisor interrupts descriptions were changed to 'Reserved'
         * Current state can be found in Table 3.6 of the specification (pg. 37 in version 1.11)
         */
        private static bool IsValidInterruptOnlyInV1_09(int irq)
        {
            return irq == (int)IrqType.HypervisorExternalInterrupt
                || irq == (int)IrqType.HypervisorSoftwareInterrupt
                || irq == (int)IrqType.HypervisorTimerInterrupt;
        }

        /* User-level interrupts support extension (N) is not implemented */
        private static bool IsUniplementedInterrupt(int irq)
        {
            return irq == (int)IrqType.UserExternalInterrupt
                || irq == (int)IrqType.UserSoftwareInterrupt
                || irq == (int)IrqType.UserTimerInterrupt;
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
