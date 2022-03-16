//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.CFU;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class BaseRiscV : TranslationCPU, IPeripheralContainer<ICFU, NumberRegistrationPoint<int>>
    {
        protected BaseRiscV(IRiscVTimeProvider timeProvider, uint hartId, string cpuType, Machine machine, PrivilegeArchitecture privilegeArchitecture, Endianess endianness, CpuBitness bitness, ulong? nmiVectorAddress = null, uint? nmiVectorLength = null, bool allowUnalignedAccesses = false, InterruptMode interruptMode = InterruptMode.Auto)
                : base(hartId, cpuType, machine, endianness, bitness)
        {
            HartId = hartId;
            this.timeProvider = timeProvider;
            this.privilegeArchitecture = privilegeArchitecture;
            shouldEnterDebugMode = true;
            nonstandardCSR = new Dictionary<ulong, NonstandardCSR>();
            customInstructionsMapping = new Dictionary<ulong, Action<UInt64>>();
            this.NMIVectorLength = nmiVectorLength;
            this.NMIVectorAddress = nmiVectorAddress;

            ArchitectureSets = DecodeArchitecture(cpuType);
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

            TlibAllowUnalignedAccesses(allowUnalignedAccesses ? 1 : 0);

            try
            {
                this.interruptMode = interruptMode;
                TlibSetInterruptMode((int)interruptMode);
            }
            catch(CpuAbortException)
            {
                throw new ConstructionException(string.Format("Unsupported interrupt mode: 0x{0:X}", interruptMode));
            }

            UserState = new Dictionary<string, object>();

            ChildCollection = new Dictionary<int, ICFU>();

            customOpcodes = new List<Tuple<string, ulong, ulong>>();            
        }

        public void Register(ICFU cfu, NumberRegistrationPoint<int> registrationPoint)
        {
            var isRegistered = ChildCollection.Where(x => x.Value.Equals(cfu)).Select(x => x.Key).ToList();
            if(isRegistered.Count != 0)
            {
                throw new RegistrationException("Can't register the same CFU twice.");
            }
            else if(ChildCollection.ContainsKey(registrationPoint.Address))
            {
                throw new RegistrationException("The specified registration point is already in use.");
            }

            ChildCollection.Add(registrationPoint.Address, cfu);
            machine.RegisterAsAChildOf(this, cfu, registrationPoint);
            cfu.ConnectedCpu = this;
        }

        public void Unregister(ICFU cfu)
        {
            var toRemove = ChildCollection.Where(x => x.Value.Equals(cfu)).Select(x => x.Key).ToList(); //ToList required, as we remove from the source
            foreach(var key in toRemove)
            {
                ChildCollection.Remove(key);
            }

            machine.UnregisterAsAChildOf(this, cfu);
        }

        public IEnumerable<NumberRegistrationPoint<int>> GetRegistrationPoints(ICFU cfu)
        {
            return ChildCollection.Keys.Select(x => new NumberRegistrationPoint<int>(x));
        }

        public IEnumerable<IRegistered<ICFU, NumberRegistrationPoint<int>>> Children
        {
            get
            {
                return ChildCollection.Select(x => Registered.Create(x.Value, new NumberRegistrationPoint<int>(x.Key)));
            }
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
            RegisterCSR(customCSR.Number, () => simpleCSRs[customCSR], value => simpleCSRs[customCSR] = value, name);
        }

        public void RegisterCSR(ulong csr, Func<ulong> readOperation, Action<ulong> writeOperation, string name = null)
        {
            nonstandardCSR.Add(csr, new NonstandardCSR(readOperation, writeOperation, name));
            if(TlibInstallCustomCSR(csr) == -1)
            {
                throw new ConstructionException($"CSR limit exceeded. Cannot register CSR 0x{csr:X}");
            }
        }

        public void SilenceUnsupportedInstructionSet(InstructionSet set, bool silent = true)
        {
            TlibMarkFeatureSilent((uint)set, silent ? 1 : 0u);
        }

        public bool InstallCustomInstruction(string pattern, Action<UInt64> handler, string name = null)
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

            // we know that the size is correct so the below method will alwyas succeed
            Misc.TryParseBitPattern(pattern, out var bitPattern, out var bitMask);

            var length = (ulong)pattern.Length / 8;
            var id = TlibInstallCustomInstruction(bitMask, bitPattern, length);
            if(id == 0)
            {
                throw new ConstructionException($"Could not install custom instruction handler for length {length}, mask 0x{bitMask:X} and pattern 0x{bitPattern:X}");
            }

            customOpcodes.Add(Tuple.Create(name ?? pattern, bitPattern, bitMask)); 
            customInstructionsMapping[id] = handler;
            return true;
        }

        public void EnableCustomOpcodesCounting()
        {
            foreach(var opc in customOpcodes)
            {
                InstallOpcodeCounterPattern(opc.Item1, opc.Item2, opc.Item3);
            }
            
            EnableOpcodesCounting = true;
        }

        public ulong Vector(uint registerNumber, uint elementIndex, ulong? value = null)
        {
            AssertVectorExtension();
            if(value.HasValue)
            {
                TlibSetVector(registerNumber, elementIndex, value.Value);
            }
            return TlibGetVector(registerNumber, elementIndex);
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

        public bool WfiAsNop
        {
            get => neverWaitForInterrupt;
            set
            {
                neverWaitForInterrupt = value;
            }
        }

        public uint VectorRegisterLength
        {
            set
            {
                if(!SupportsInstructionSet(InstructionSet.V))
                {
                    throw new RecoverableException("Attempted to set Vector Register Length (VLEN), but V extention is not enabled");
                }
                if(TlibSetVlen(value) != 0)
                {
                    throw new RecoverableException($"Attempted to set Vector Register Length (VLEN), but {value} is not a valid value");
                }
            }
        }

        public uint VectorElementMaxWidth
        {
            set
            {
                if(!SupportsInstructionSet(InstructionSet.V))
                {
                    throw new RecoverableException("Attempted to set Vector Element Max Width (ELEN), but V extention is not enabled");
                }
                if(TlibSetElen(value) != 0)
                {
                    throw new RecoverableException($"Attempted to set Vector Element Max Width (ELEN), but {value} is not a valid value");
                }
            }
        }

        public ulong? NMIVectorAddress { get; }

        public uint? NMIVectorLength { get; }

        public event Action<ulong> MipChanged;

        public Dictionary<string, object> UserState { get; }

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                if(gdbFeatures.Any())
                {
                    return gdbFeatures;
                }

                var registerWidth = (uint)MostSignificantBit + 1;
                RiscVRegisterDescription.AddCpuFeature(ref gdbFeatures, registerWidth);
                RiscVRegisterDescription.AddFpuFeature(ref gdbFeatures, registerWidth, false, SupportsInstructionSet(InstructionSet.F), SupportsInstructionSet(InstructionSet.D), false);
                RiscVRegisterDescription.AddCSRFeature(ref gdbFeatures, registerWidth, SupportsInstructionSet(InstructionSet.S), SupportsInstructionSet(InstructionSet.U), false, SupportsInstructionSet(InstructionSet.V));
                RiscVRegisterDescription.AddVirtualFeature(ref gdbFeatures, registerWidth);
                RiscVRegisterDescription.AddCustomCSRFeature(ref gdbFeatures, registerWidth, nonstandardCSR);
                if(SupportsInstructionSet(InstructionSet.V))
                {
                    RiscVRegisterDescription.AddVectorFeature(ref gdbFeatures, VLEN);
                }

                return gdbFeatures;
            }
        }
        
        public IEnumerable<InstructionSet> ArchitectureSets { get; }

        public abstract RegisterValue VLEN { get; }

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

        protected bool TrySetNonMappedRegister(int register, RegisterValue value)
        {
            if(SupportsInstructionSet(InstructionSet.V) && IsVectorRegisterNumber(register))
            {
                return TrySetVectorRegister((uint)register - RiscVRegisterDescription.StartOfVRegisters, value);
            }
            return TrySetCustomCSR(register, value);
        }

        protected bool TryGetNonMappedRegister(int register, out RegisterValue value)
        {
            if(SupportsInstructionSet(InstructionSet.V) && IsVectorRegisterNumber(register))
            {
                return TryGetVectorRegister((uint)register - RiscVRegisterDescription.StartOfVRegisters, out value);
            }
            return TryGetCustomCSR(register, out value);
        }

        protected bool TrySetCustomCSR(int register, RegisterValue value)
        {
            if(!nonstandardCSR.ContainsKey((ulong)register))
            {
                return false;
            }
            WriteCSR((ulong)register, value);
            return true;
        }

        protected bool TryGetCustomCSR(int register, out RegisterValue value)
        {
            value = default(RegisterValue);
            if(!nonstandardCSR.ContainsKey((ulong)register))
            {
                return false;
            }
            value = ReadCSR((ulong)register);
            return true;
        }

        protected IEnumerable<CPURegister> GetNonMappedRegisters()
        {
            var registers = GetCustomCSRs();
            if(SupportsInstructionSet(InstructionSet.V))
            {
                var vlen = VLEN;
                registers = registers.Concat(Enumerable.Range((int)RiscVRegisterDescription.StartOfVRegisters, (int)RiscVRegisterDescription.NumberOfVRegisters)
                    .Select(index => new CPURegister(index, vlen, false, false)));
            }
            return registers;
        }

        protected IEnumerable<CPURegister> GetCustomCSRs()
        {
            return nonstandardCSR.Keys.Select(index => new CPURegister((int)index, MostSignificantBit + 1, false, false));
        }

        private bool IsInterrupt(ulong exceptionIndex)
        {
            return BitHelper.IsBitSet(exceptionIndex, MostSignificantBit);
        }

        protected abstract byte MostSignificantBit { get; }

        private void EnableArchitectureVariants()
        {
            foreach(var @set in ArchitectureSets)
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

        private bool TrySetVectorRegister(uint registerNumber, RegisterValue value)
        {
            var vlenb = VLEN / 8;
            var valueArray = value.GetBytes(Endianess.BigEndian);

            if(valueArray.Length != vlenb)
            {
                return false;
            }

            var valuePointer = Marshal.AllocHGlobal(vlenb);
            Marshal.Copy(valueArray, 0, valuePointer, vlenb);
            
            var result = true;
            if(TlibSetWholeVector(registerNumber, valuePointer) != 0)
            {
                result = false;
            }

            Marshal.FreeHGlobal(valuePointer);
            return result;
        }

        private bool TryGetVectorRegister(uint registerNumber, out RegisterValue value)
        {
            var vlenb = VLEN / 8;
            var valuePointer = Marshal.AllocHGlobal(vlenb);
            if(TlibGetWholeVector(registerNumber, valuePointer) != 0)
            {
                Marshal.FreeHGlobal(valuePointer);
                value = default(RegisterValue);
                return false;
            }
            var bytes = new byte[vlenb];
            Marshal.Copy(valuePointer, bytes, 0, vlenb);
            value = bytes;
            Marshal.FreeHGlobal(valuePointer);
            return true;
        }

        private bool IsVectorRegisterNumber(int register)
        {
            return RiscVRegisterDescription.StartOfVRegisters <= register && register < RiscVRegisterDescription.StartOfVRegisters + RiscVRegisterDescription.NumberOfVRegisters; 
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
        private ulong ReadCSR(ulong csr)
        {
            var readMethod = nonstandardCSR[csr].ReadOperation;
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
            var writeMethod = nonstandardCSR[csr].WriteOperation;
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

        public readonly Dictionary<int, ICFU> ChildCollection;

        private bool pcWrittenFlag;

        private readonly IRiscVTimeProvider timeProvider;

        private readonly PrivilegeArchitecture privilegeArchitecture;

        private readonly Dictionary<ulong, NonstandardCSR> nonstandardCSR;

        private readonly Dictionary<ulong, Action<UInt64>> customInstructionsMapping;

        private readonly Dictionary<SimpleCSR, ulong> simpleCSRs = new Dictionary<SimpleCSR, ulong>();

        private List<GDBFeatureDescriptor> gdbFeatures = new List<GDBFeatureDescriptor>();

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
        
        [Import(Name="tlib_install_custom_csr")]
        private FuncInt32UInt64 TlibInstallCustomCSR;

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

        [Import]
        private ActionInt32 TlibAllowUnalignedAccesses;

        [Import]
        private ActionInt32 TlibSetInterruptMode;

        [Import]
        private FuncUInt32UInt32 TlibSetVlen;

        [Import]
        private FuncUInt32UInt32 TlibSetElen;

        [Import]
        private FuncUInt64UInt32UInt32 TlibGetVector;

        [Import]
        private ActionUInt32UInt32UInt64 TlibSetVector;

        [Import]
        private FuncUInt32UInt32IntPtr TlibGetWholeVector;

        [Import]
        private FuncUInt32UInt32IntPtr TlibSetWholeVector;

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
            V = 'V' - 'A',
        }

        public enum InterruptMode
        {
            // entries match values
            // in tlib, do not change
            Auto = 0,
            Direct = 1,
            Vectored = 2
        }

        protected void BeforeVectorExtensionRegisterRead()
        {
            AssertVectorExtension();
        }

        protected RegisterValue BeforeVectorExtensionRegisterWrite(RegisterValue value)
        {
            AssertVectorExtension();
            return value;
        }

        protected RegisterValue BeforeMTVECWrite(RegisterValue value)
        {
            return HandleMTVEC_STVECWrite(value, "MTVEC");
        }

        protected RegisterValue BeforeSTVECWrite(RegisterValue value)
        {
            return HandleMTVEC_STVECWrite(value, "STVEC");
        }

        private RegisterValue HandleMTVEC_STVECWrite(RegisterValue value, string registerName)
        {
            switch(interruptMode)
            {
                case InterruptMode.Direct:
                    if((value.RawValue & 0x3) != 0x0)
                    {
                        var originalValue = value;
                        value = RegisterValue.Create(BitHelper.ReplaceBits(value.RawValue, 0x0, width: 2), value.Bits);
                        this.Log(LogLevel.Warning, "CPU is configured in the Direct interrupt mode, modifying {2} to 0x{0:X} (tried to set 0x{1:X})", value.RawValue, originalValue.RawValue, registerName);
                    }
                    break;

                case InterruptMode.Vectored:
                    if((value.RawValue & 0x3) != 0x1)
                    {
                        var originalValue = value;
                        value = RegisterValue.Create(BitHelper.ReplaceBits(value.RawValue, 0x1, width: 2), value.Bits);
                        this.Log(LogLevel.Warning, "CPU is configured in the Vectored interrupt mode, modifying {2}  to 0x{0:X} (tried to set 0x{1:X})", value.RawValue, originalValue.RawValue, registerName);
                    }
                    break;
            }

            return value;
        }

        private void AssertVectorExtension()
        {
            if(!SupportsInstructionSet(InstructionSet.V))
            {
                throw new RegisterValueUnavailableException("Vector extention is not supported by this CPU");
            }
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

        private readonly InterruptMode interruptMode;
        
        private readonly List<Tuple<string, ulong, ulong>> customOpcodes;

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
