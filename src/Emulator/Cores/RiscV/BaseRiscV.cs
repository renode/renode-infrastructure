//
// Copyright (c) 2010-2024 Antmicro
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
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.CFU;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Migrant;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class BaseRiscV : TranslationCPU, IPeripheralContainer<ICFU, NumberRegistrationPoint<int>>, ICPUWithPostOpcodeExecutionHooks, ICPUWithPostGprAccessHooks, ICPUWithNMI
    {
        protected BaseRiscV(IRiscVTimeProvider timeProvider, uint hartId, string cpuType, IMachine machine, PrivilegeArchitecture privilegeArchitecture, Endianess endianness, CpuBitness bitness, ulong? nmiVectorAddress = null, uint? nmiVectorLength = null, bool allowUnalignedAccesses = false, InterruptMode interruptMode = InterruptMode.Auto, uint minimalPmpNapotInBytes = 8)
                : base(hartId, cpuType, machine, endianness, bitness)
        {
            HartId = hartId;
            this.timeProvider = timeProvider;
            this.privilegeArchitecture = privilegeArchitecture;
            shouldEnterDebugMode = true;
            nonstandardCSR = new Dictionary<ulong, NonstandardCSR>();
            customInstructionsMapping = new Dictionary<ulong, Action<UInt64>>();
            this.nmiVectorLength = nmiVectorLength;
            this.nmiVectorAddress = nmiVectorAddress;

            UserState = new Dictionary<string, object>();

            ChildCollection = new Dictionary<int, ICFU>();

            customOpcodes = new List<Tuple<string, ulong, ulong>>();
            postOpcodeExecutionHooks = new List<Action<ulong>>();
            postGprAccessHooks = new Action<bool>[NumberOfGeneralPurposeRegisters];

            architectureDecoder = new ArchitectureDecoder(machine, this, cpuType);
            EnableArchitectureVariants();

            UpdateNMIVector();

            AllowUnalignedAccesses = allowUnalignedAccesses;

            try
            {
                this.interruptMode = interruptMode;
                TlibSetInterruptMode((int)interruptMode);
            }
            catch(CpuAbortException)
            {
                throw new ConstructionException(string.Format("Unsupported interrupt mode: 0x{0:X}", interruptMode));
            }

            TlibSetNapotGrain(minimalPmpNapotInBytes);
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

        public virtual void OnNMI(int number, bool value, ulong? mcause = null)
        {
            if(this.NMIVectorLength == null || this.NMIVectorAddress == null)
            {
                this.Log(LogLevel.Warning, "Non maskable interrupt not supported on this CPU. {0} or {1} not set",
                        nameof(this.NMIVectorAddress), nameof(this.NMIVectorLength));
            }
            else
            {
                TlibSetNmi(number, value ? 1 : 0, mcause ?? (ulong)number);
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
            else if(IsUnimplementedInterrupt(number) && value)
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
            pcWrittenFlag = false;
            ShouldEnterDebugMode = true;
            EnableArchitectureVariants();
            foreach(var key in simpleCSRs.Keys.ToArray())
            {
                simpleCSRs[key] = 0;
            }
            UserState.Clear();
            SetPCFromResetVector();
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

        public void EnablePostOpcodeExecutionHooks(uint value)
        {
            TlibEnablePostOpcodeExecutionHooks(value != 0 ? 1u : 0u);
        }

        public void AddPostOpcodeExecutionHook(UInt64 mask, UInt64 value, Action<ulong> action)
        {
            var index = TlibInstallPostOpcodeExecutionHook(mask, value);
            if(index == UInt32.MaxValue)
            {
                throw new RecoverableException("Unable to register opcode hook. Maximum number of hooks already installed");
            }
            // Assert that the list index will match the one returned from the core
            if(index != postOpcodeExecutionHooks.Count)
            {
                throw new ApplicationException("Mismatch in the post-execution opcode hooks on the C# and C side." +
                                                " One of them miss at least one element");
            }
            postOpcodeExecutionHooks.Add(action);
        }

        public void EnablePostGprAccessHooks(uint value)
        {
            TlibEnablePostGprAccessHooks(value != 0 ? 1u : 0u);
        }

        public void InstallPostGprAccessHookOn(uint registerIndex, Action<bool> callback,  uint value)
        {
            postGprAccessHooks[registerIndex] = callback;
            TlibEnablePostGprAccessHookOn(registerIndex, value);
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

        public ulong ResetVector
        {
            get => resetVector;
            set
            {
                resetVector = value;
                SetPCFromResetVector();
            }
        }

        private void SetPCFromResetVector()
        {
            // Prevents overwriting PC if it's been set already (e.g. by LoadELF).
            // The pcWrittenFlag is automatically set when setting PC so let's unset it.
            // Otherwise, only the first ResetVector change would be propagated to PC.
            if(!pcWrittenFlag)
            {
                PC = ResetVector;
                pcWrittenFlag = false;
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

        public ulong? NMIVectorAddress
        {
            get
            {
                return nmiVectorAddress;
            }

            set
            {
                nmiVectorAddress = value;
                UpdateNMIVector();
            }
        }

        public uint? NMIVectorLength
        {
            get
            {
                return nmiVectorLength;
            }

            set
            {
                nmiVectorLength = value;
                UpdateNMIVector();
            }
        }

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

        public IEnumerable<InstructionSet> ArchitectureSets => architectureDecoder.InstructionSets;

        public bool AllowUnalignedAccesses
        {
            set => TlibAllowUnalignedAccesses(value ? 1 : 0);
        }

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
            foreach(var @set in architectureDecoder.InstructionSets)
            {
                if(set == InstructionSet.G)
                {
                    //G is a wildcard denoting multiple instruction sets
                    foreach(var gSet in new[] { InstructionSet.I, InstructionSet.M, InstructionSet.F, InstructionSet.D, InstructionSet.A })
                    {
                        TlibAllowFeature((uint)gSet);
                    }
                    TlibAllowAdditionalFeature((uint)StandardInstructionSetExtensions.ICSR);
                    TlibAllowAdditionalFeature((uint)StandardInstructionSetExtensions.IFENCEI);
                }
                else if(set == InstructionSet.B)
                {
                    //B is a wildcard denoting all bit manipulation instruction subsets
                    foreach(var gSet in new[] { StandardInstructionSetExtensions.BA, StandardInstructionSetExtensions.BB, StandardInstructionSetExtensions.BC, StandardInstructionSetExtensions.BS })
                    {
                        TlibAllowAdditionalFeature((uint)gSet);
                    }
                }
                else
                {
                    TlibAllowFeature((uint)set);
                }
            }

            foreach(var @set in architectureDecoder.StandardExtensions)
            {
                TlibAllowAdditionalFeature((uint)set);
            }

            TlibSetPrivilegeArchitecture((int)privilegeArchitecture);
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

        private void UpdateNMIVector()
        {
            if(NMIVectorAddress.HasValue && NMIVectorLength.HasValue && NMIVectorLength > 0)
            {
                this.Log(LogLevel.Noisy, "Non maskable interrupts enabled with parameters: {0} = {1}, {2} = {3}",
                        nameof(NMIVectorAddress), NMIVectorAddress, nameof(NMIVectorLength), NMIVectorLength);
                TlibSetNmiVector(NMIVectorAddress.Value, NMIVectorLength.Value);
            }
            else
            {
                this.Log(LogLevel.Noisy, "Non maskable interrupts disabled");
                TlibSetNmiVector(0, 0);
            }
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

        [Export]
        private void HandlePostOpcodeExecutionHook(UInt32 id, UInt64 pc)
        {
            this.NoisyLog($"Got opcode hook no {id} from PC {pc}");
            if(id < (uint)postOpcodeExecutionHooks.Count)
            {
                postOpcodeExecutionHooks[(int)id].Invoke(pc);
            }
            else
            {
                this.Log(LogLevel.Error, "Received opcode hook with non-existing id = {0}", id);
            }
        }

        [Export]
        private void HandlePostGprAccessHook(UInt32 registerIndex, UInt32 writeOrRead)
        {
            DebugHelper.Assert(registerIndex < 32, $"Index outside of range : {registerIndex}");
            if(postGprAccessHooks[(int)registerIndex] == null)
            {
                this.Log(LogLevel.Error, "No callback for register #{0} installed", registerIndex);
                return;
            }

            var isWrite = (writeOrRead != 0);

            this.NoisyLog("Post-GPR {0} hook for register #{1} triggered", isWrite ? "write" : "read", registerIndex);
            postGprAccessHooks[(int)registerIndex].Invoke(isWrite);
        }

        public readonly Dictionary<int, ICFU> ChildCollection;

        private ulong? nmiVectorAddress;
        private uint? nmiVectorLength;

        private bool pcWrittenFlag;
        private ulong resetVector = DefaultResetVector;

        private readonly IRiscVTimeProvider timeProvider;

        private readonly PrivilegeArchitecture privilegeArchitecture;

        private readonly Dictionary<ulong, NonstandardCSR> nonstandardCSR;

        private readonly Dictionary<ulong, Action<UInt64>> customInstructionsMapping;

        private readonly Dictionary<SimpleCSR, ulong> simpleCSRs = new Dictionary<SimpleCSR, ulong>();

        private List<GDBFeatureDescriptor> gdbFeatures = new List<GDBFeatureDescriptor>();

        private readonly ArchitectureDecoder architectureDecoder;

        [Constructor]
        private readonly List<Action<ulong>> postOpcodeExecutionHooks;

        [Transient]
        private readonly Action<bool>[] postGprAccessHooks;

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649
        [Import]
        private ActionUInt32 TlibAllowFeature;

        [Import]
        private ActionUInt32 TlibAllowAdditionalFeature;

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
        private ActionUInt32 TlibSetNapotGrain;

        [Import]
        private FuncUInt64UInt64UInt64UInt64 TlibInstallCustomInstruction;
        [Import(Name="tlib_install_custom_csr")]

        private FuncInt32UInt64 TlibInstallCustomCSR;

        [Import]
        private ActionUInt32UInt32 TlibMarkFeatureSilent;

        [Import]
        private ActionUInt64UInt32 TlibSetNmiVector;

        [Import]
        private ActionInt32Int32UInt64 TlibSetNmi;

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

        [Import]
        private ActionUInt32 TlibEnablePostOpcodeExecutionHooks;

        [Import]
        private FuncUInt32UInt64UInt64 TlibInstallPostOpcodeExecutionHook;

        [Import]
        private ActionUInt32 TlibEnablePostGprAccessHooks;

        [Import]
        private ActionUInt32UInt32 TlibEnablePostGprAccessHookOn;

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
            Priv1_11,
            Priv1_12,
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
            B = 'B' - 'A',
            G = 'G' - 'A',
        }

        public enum StandardInstructionSetExtensions
        {
            BA = 0x1 << AdditionalExtensionOffset,
            BB = 0x2 << AdditionalExtensionOffset,
            BC = 0x3 << AdditionalExtensionOffset,
            BS = 0x4 << AdditionalExtensionOffset,
            ICSR = 0x5 << AdditionalExtensionOffset,
            IFENCEI = 0x6 << AdditionalExtensionOffset,
            ZFH = 0x7 << AdditionalExtensionOffset,
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

        private class ArchitectureDecoder
        {
            public ArchitectureDecoder(IMachine machine, BaseRiscV parent, string architectureString)
            {
                this.parent = parent;
                this.machine = machine;
                instructionSets = new List<InstructionSet>();
                standardExtensions = new List<StandardInstructionSetExtensions>();

                Decode(architectureString);
            }

            public IEnumerable<InstructionSet> InstructionSets
            {
                get => instructionSets;
            }

            public IEnumerable<StandardInstructionSetExtensions> StandardExtensions
            {
                get => standardExtensions;
            }

            private void Decode(string architectureString)
            {
                // Example cpuType string we would like to handle here: "rv64gcv_zba_zbb_zbc_zbs_xcustom".
                var parts = architectureString.ToUpper().Split('_');
                var basicDescription = parts[0];

                if(!basicDescription.StartsWith("RV"))
                {
                    throw new ConstructionException($"Architecture string should start with rv, but is: {architectureString}");
                }

                var bits = string.Join("", basicDescription.Skip(2).TakeWhile(Char.IsDigit));
                if(bits.Length == 0 || int.Parse(bits) != parent.MostSignificantBit + 1)
                {
                    throw new ConstructionException($"Unexpected architecture width: {bits}");
                }

                //The architecture name is: RV{architecture_width}{list of letters denoting instruction sets}
                foreach(var @set in basicDescription.Skip(2 + bits.Length))
                {
                    switch(set)
                    {
                        case 'I': instructionSets.Add(InstructionSet.I); break;
                        case 'M': instructionSets.Add(InstructionSet.M); break;
                        case 'A': instructionSets.Add(InstructionSet.A); break;
                        case 'F': instructionSets.Add(InstructionSet.F); break;
                        case 'D': instructionSets.Add(InstructionSet.D); break;
                        case 'C': instructionSets.Add(InstructionSet.C); break;
                        case 'S': instructionSets.Add(InstructionSet.S); break;
                        case 'U': instructionSets.Add(InstructionSet.U); break;
                        case 'V': instructionSets.Add(InstructionSet.V); break;
                        case 'B': instructionSets.Add(InstructionSet.B); break;
                        case 'G': instructionSets.Add(InstructionSet.G); break;
                        default:
                            throw new ConstructionException($"Undefined instruction set: {set}.");
                    }
                }

                // skip the basic description
                foreach(var extension in parts.Skip(1))
                {
                    // standard extension
                    if(extension.StartsWith("Z"))
                    {
                        var set = extension.Substring(1);
                        switch(set)
                        {
                            case "BA": standardExtensions.Add(StandardInstructionSetExtensions.BA); break;
                            case "BB": standardExtensions.Add(StandardInstructionSetExtensions.BB); break;
                            case "BC": standardExtensions.Add(StandardInstructionSetExtensions.BC); break;
                            case "BS": standardExtensions.Add(StandardInstructionSetExtensions.BS); break;
                            case "ICSR": standardExtensions.Add(StandardInstructionSetExtensions.ICSR); break;
                            case "IFENCEI": standardExtensions.Add(StandardInstructionSetExtensions.IFENCEI); break;
                            case "FH": standardExtensions.Add(StandardInstructionSetExtensions.ZFH); break;
                            default:
                                throw new ConstructionException($"Undefined instruction set standard extension: {set}.");
                        }
                    }
                    // custom extesions
                    else if(extension.StartsWith("X"))
                    {
                        switch(extension.Substring(1))
                        {
                            case "ANDES": Andes_AndeStarV5Extension.RegisterIn(machine, (RiscV32)parent); break;
                            default:
                                throw new ConstructionException($"Unsupported custom instruction set extension: {extension}.");
                        }
                    }
                    // unexpected value
                    else
                    {
                        throw new ConstructionException($"Undefined instruction set extension: {extension}.");
                    }
                }
            }

            private IList<StandardInstructionSetExtensions> standardExtensions;
            private readonly IList<InstructionSet> instructionSets;
            private readonly BaseRiscV parent;
            private readonly IMachine machine;
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
        private static bool IsUnimplementedInterrupt(int irq)
        {
            return irq == (int)IrqType.UserExternalInterrupt
                || irq == (int)IrqType.UserSoftwareInterrupt
                || irq == (int)IrqType.UserTimerInterrupt;
        }

        private readonly InterruptMode interruptMode;

        private readonly List<Tuple<string, ulong, ulong>> customOpcodes;

        // In MISA register the extensions are encoded on bits [25:0] (see: https://five-embeddev.com/riscv-isa-manual/latest/machine.html),
        // but because these additional features are not there RISCV_ADDITIONAL_FEATURE_OFFSET allows to show that they are unrelated to MISA.
        private const int AdditionalExtensionOffset = 26;

        private const ulong DefaultResetVector = 0x1000;
        private const int NumberOfGeneralPurposeRegisters = 32;

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
