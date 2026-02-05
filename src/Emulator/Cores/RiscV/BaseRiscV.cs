//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CFU;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class BaseRiscV : TranslationCPU, IPeripheralContainer<ICFU, NumberRegistrationPoint<int>>, IPeripheralContainer<IIndirectCSRPeripheral, BusRangeRegistration>, IRegisterablePeripheral<ExternalPMPBase, NullRegistrationPoint>, ICPUWithPostOpcodeExecutionHooks, ICPUWithPreOpcodeExecutionHooks, ICPUWithPostGprAccessHooks, ICPUWithNMI
    {
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

        public void RaiseInterrupt(uint interruptId)
        {
            TlibRaiseInterrupt(interruptId);
        }

        public void ClicPresentInterrupt(int index, bool vectored, int level, PrivilegeLevel mode)
        {
            TlibSetClicInterruptState(index, vectored ? 1u : 0, (uint)level, (uint)mode);
        }

        public void RegisterLocalInterruptController(CoreLocalInterruptController clic)
        {
            if(this.clic != null)
            {
                throw new ArgumentException($"{nameof(CoreLocalInterruptController)} is already registered");
            }
            this.clic = clic;
        }

        public void SetPMPAddress(uint index, ulong start_address, ulong end_address)
        {
            TlibSetPmpaddr(index, start_address, end_address);
        }

        public void EnablePreStackAccessHook(bool value)
        {
            TlibEnablePreStackAccessHook(value);
        }

        public void InstallPostGprAccessHookOn(uint registerIndex, Action<bool> callback, uint value)
        {
            postGprAccessHooks[registerIndex] = callback;
            TlibEnablePostGprAccessHookOn(registerIndex, value);
        }

        public void EnablePostGprAccessHooks(uint value)
        {
            TlibEnablePostGprAccessHooks(value != 0 ? 1u : 0u);
        }

        public void EnablePostOpcodeExecutionHooks(uint value)
        {
            TlibEnablePostOpcodeExecutionHooks(value != 0 ? 1u : 0u);
        }

        public void EnablePreOpcodeExecutionHooks(uint value)
        {
            TlibEnablePreOpcodeExecutionHooks(value != 0 ? 1u : 0u);
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

        public void EnableCustomOpcodesCounting()
        {
            foreach(var opc in customOpcodes)
            {
                InstallOpcodeCounterPattern(opc.Item1, opc.Item2, opc.Item3);
            }

            EnableOpcodesCounting = true;
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

            CheckCustomInstructionLengthPattern(bitPattern, pattern.Length);

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

        public void SilenceUnsupportedInstructionSet(InstructionSet set, bool silent = true)
        {
            TlibMarkFeatureSilent((uint)set, silent ? 1 : 0u);
        }

        public void RegisterCustomInternalInterrupt(ulong id, bool mipTriggered = false, bool sipTriggered = false)
        {
            if(TlibInstallCustomInterrupt(id, mipTriggered, sipTriggered) == -1)
            {
                throw new ConstructionException($"Failed to install custom internal interrupt because it clashes with a standard interrupt. Id {id}");
            }
        }

        public void AddPostOpcodeExecutionHook(UInt64 mask, UInt64 value, Action<ulong, ulong> action)
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

        public void AddPreOpcodeExecutionHook(UInt64 mask, UInt64 value, Action<ulong, ulong> action)
        {
            var index = TlibInstallPreOpcodeExecutionHook(mask, value);
            if(index == UInt32.MaxValue)
            {
                throw new RecoverableException("Unable to register opcode hook. Maximum number of hooks already installed");
            }
            // Assert that the list index will match the one returned from the core
            if(index != preOpcodeExecutionHooks.Count)
            {
                throw new ApplicationException("Mismatch in the pre-execution opcode hooks on the C# and C side." +
                                                " One of them miss at least one element");
            }
            preOpcodeExecutionHooks.Add(action);
        }

        public void RegisterCustomCSR(string name, ushort number, PrivilegeLevel mode)
        {
            var customCSR = new SimpleCSR(name, number, mode);
            if(simpleCSRs.Keys.Any(x => x.Number == customCSR.Number))
            {
                throw new ConstructionException($"Cannot register CSR {customCSR.Name}, because its number 0x{customCSR.Number:X} is already registered");
            }
            simpleCSRs.Add(customCSR, 0);
            RegisterCSR(customCSR.Number, () => simpleCSRs[customCSR], value => simpleCSRs[customCSR] = value, name);
        }

        public void RegisterCSR(ushort csr, Func<ulong> readOperation, Action<ulong> writeOperation, string name = null)
        {
            nonstandardCSR.Add(csr, new NonstandardCSR(readOperation, writeOperation, name));
            if(TlibInstallCustomCSR(csr) == -1)
            {
                throw new ConstructionException($"CSR limit exceeded. Cannot register CSR 0x{csr:X}");
            }
        }

        public void RegisterCSRStub(IConvertible csr, string name, ulong returnValue = 0)
        {
            var csrEncoding = Convert.ToUInt16(csr);
            RegisterCSR(csrEncoding, name: name,
                readOperation: () =>
                {
                    this.WarningLog("Tried to read from an unimplemented {0} CSR (0x{1:X}), returning 0x{2:X}", name, csrEncoding, returnValue);
                    return returnValue;
                },
                writeOperation: value =>
                {
                    this.WarningLog("Tried to write 0x{0:X} to an unimplemented {1} CSR (0x{2:X}), write ignored", value, name, csrEncoding);
                });
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

        public void Register(IIndirectCSRPeripheral peripheral, BusRangeRegistration registrationPoint)
        {
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            indirectCsrPeripherals.Add(registrationPoint, peripheral);
        }

        public IEnumerable<BusRangeRegistration> GetRegistrationPoints(IIndirectCSRPeripheral peripheral)
        {
            return indirectCsrPeripherals.Where(p => p.Value == peripheral).Select(p => p.Key);
        }

        public void Unregister(IIndirectCSRPeripheral peripheral)
        {
            foreach(var point in GetRegistrationPoints(peripheral).ToList())
            {
                indirectCsrPeripherals.Remove(point);
            }
            machine.UnregisterAsAChildOf(this, peripheral);
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
            if(privilegedArchitecture >= PrivilegedArchitecture.Priv1_10 && IsValidInterruptOnlyInV1_09(number) && value)
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

        public bool SupportsExtensionSet(StandardInstructionSetExtensions set)
        {
            return TlibIsAdditionalFeatureEnabled((uint)set) == 1;
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
            TlibSetPmpaddrBits(PMPNumberOfAddrBits);
            TlibSetNapotGrain(MinimalPMPNapotInBytes);
        }

        public void Register(ExternalPMPBase externalPMP, NullRegistrationPoint registrationPoint)
        {
            if(this.externalPMP != null)
            {
                throw new RegistrationException($"{nameof(ExternalPMPBase)} is already registered");
            }
            machine.RegisterAsAChildOf(this, externalPMP, registrationPoint);
            this.InfoLog("Enabling External PMP");
            this.externalPMP = externalPMP;
            externalPMP.RegisterCPU(this);
            TlibEnableExternalPmp(true);
        }

        public void Unregister(ExternalPMPBase peripheral)
        {
            this.externalPMP = null;
            TlibEnableExternalPmp(false);
            machine.UnregisterAsAChildOf(this, peripheral);
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

        public bool AllowUnalignedAccesses
        {
            set => TlibAllowUnalignedAccesses(value ? 1 : 0);
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

        public Dictionary<string, object> UserState { get; }

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

        public CSRValidationLevel CSRValidation
        {
            get => (CSRValidationLevel)TlibGetCsrValidationLevel();

            set
            {
                TlibSetCsrValidationLevel((uint)value);
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

        public PrivilegeLevel CurrentPrivilegeLevel => (PrivilegeLevel)TlibGetCurrentPriv();

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

        public IEnumerable<IRegistered<ICFU, NumberRegistrationPoint<int>>> Children
        {
            get
            {
                return ChildCollection.Select(x => Registered.Create(x.Value, new NumberRegistrationPoint<int>(x.Key)));
            }
        }

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
                RiscVRegisterDescription.AddCSRFeature(ref gdbFeatures, registerWidth, SupportsInstructionSet(InstructionSet.S), SupportsInstructionSet(InstructionSet.U), false, SupportsInstructionSet(InstructionSet.V), SupportsExtensionSet(StandardInstructionSetExtensions.ZCMT));

                RiscVRegisterDescription.AddVirtualFeature(ref gdbFeatures, registerWidth);
                RiscVRegisterDescription.AddCustomCSRFeature(ref gdbFeatures, registerWidth, nonstandardCSR);
                if(SupportsInstructionSet(InstructionSet.V))
                {
                    RiscVRegisterDescription.AddVectorFeature(ref gdbFeatures, VLEN);
                }

                return gdbFeatures;
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

        public uint MinimalPMPNapotInBytes { get; private set; }

        public uint PMPNumberOfAddrBits { get; private set; }

        public IEnumerable<InstructionSet> ArchitectureSets => architectureDecoder.InstructionSets;

        public abstract RegisterValue VLEN { get; }

        // FFLAGS and FRM are accessible standalone as CSR1 and CSR2, but they can also be accessed at once via FCSR (CSR3).
        // Only FFLAGS and FRM registers are mapped in tlib, so we emulate FCSR in C# as non-mapped register.
        public abstract RegisterValue FFLAGSField { get; set; }

        public abstract RegisterValue FRMField { get; set; }

        // Needs to be abstract because it uses MTVEC defined in RiscVxxRegisters.cs.
        public abstract bool InClicMode { get; }

        public event Action<ulong> MipChanged;

        public event Action<ulong, uint, bool> PreStackAccess;

        public readonly Dictionary<int, ICFU> ChildCollection;

        protected BaseRiscV(
            IRiscVTimeProvider timeProvider,
            uint hartId,
            string cpuType,
            IMachine machine,
            PrivilegedArchitecture privilegedArchitecture,
            Endianess endianness,
            CpuBitness bitness,
            ulong? nmiVectorAddress = null,
            uint? nmiVectorLength = null,
            bool allowUnalignedAccesses = false,
            InterruptMode interruptMode = InterruptMode.Auto,
            uint minimalPmpNapotInBytes = 8,
            uint pmpNumberOfAddrBits = 32,
            PrivilegeLevels privilegeLevels = PrivilegeLevels.MachineSupervisorUser,
            bool useMachineAtomicState = true
        )
            : base(hartId, cpuType, machine, endianness, bitness, useMachineAtomicState)
        {
            HartId = hartId;
            this.timeProvider = timeProvider;
            this.privilegedArchitecture = privilegedArchitecture;
            shouldEnterDebugMode = true;
            nonstandardCSR = new Dictionary<ulong, NonstandardCSR>();
            customInstructionsMapping = new Dictionary<ulong, Action<UInt64>>();
            indirectCsrPeripherals = new Dictionary<BusRangeRegistration, IIndirectCSRPeripheral>();
            this.nmiVectorLength = nmiVectorLength;
            this.nmiVectorAddress = nmiVectorAddress;

            UserState = new Dictionary<string, object>();

            ChildCollection = new Dictionary<int, ICFU>();

            customOpcodes = new List<Tuple<string, ulong, ulong>>();
            postOpcodeExecutionHooks = new List<Action<ulong, ulong>>();
            preOpcodeExecutionHooks = new List<Action<ulong, ulong>>();
            postGprAccessHooks = new Action<bool>[NumberOfGeneralPurposeRegisters];

            architectureDecoder = new ArchitectureDecoder(machine, this, cpuType, privilegeLevels);
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
                // Free unmanaged resources allocated by the base class constructor
                Dispose();
                throw new ConstructionException(string.Format("Unsupported interrupt mode: 0x{0:X}", interruptMode));
            }
            PMPNumberOfAddrBits = pmpNumberOfAddrBits;
            TlibSetPmpaddrBits(pmpNumberOfAddrBits);
            MinimalPMPNapotInBytes = minimalPmpNapotInBytes;
            TlibSetNapotGrain(minimalPmpNapotInBytes);

            RegisterCSR((ushort)StandardCSR.Miselect, () => miselectValue, s => miselectValue = (uint)s, "miselect");
            for(ushort i = 0; i < 6; ++i)
            {
                var j = i;
                RegisterCSR((ushort)(StandardCSR.Mireg + i), () => ReadIndirectCSR(miselectValue, j), v => WriteIndirectCSR(miselectValue, j, (uint)v), $"mireg{i + 1}");
            }

            RegisterCSR((ushort)StandardCSR.Siselect, () => siselectValue, s => siselectValue = (uint)s, "siselect");
            for(ushort i = 0; i < 6; ++i)
            {
                var j = i;
                RegisterCSR((ushort)(StandardCSR.Sireg + i), () => ReadIndirectCSR(siselectValue, j), v => WriteIndirectCSR(siselectValue, j, (uint)v), $"sireg{i + 1}");
            }
        }

        IEnumerable<IRegistered<IIndirectCSRPeripheral, BusRangeRegistration>> IPeripheralContainer<IIndirectCSRPeripheral, BusRangeRegistration>.Children
        {
            get
            {
                return indirectCsrPeripherals.Select(x => Registered.Create(x.Value, x.Key));
            }
        }

        /// <remarks>
        /// Modifies value read using STVEC property, see Riscv(32|64)Registers for details.
        /// </remarks>
        protected RegisterValue AfterSTVECRead(RegisterValue value)
        {
            // Based on tlib/arch/riscv/op_helper.c:csr_read_helper:
            //   case CSR_STVEC:
            //       return env->stvec | (cpu_in_clic_mode(env) ? MTVEC_MODE_CLIC : 0);
            return value.RawValue | (InClicMode ? 3u : 0u);
        }

        protected RegisterValue BeforeSTVECWrite(RegisterValue value)
        {
            return HandleMTVEC_STVECWrite(value, "STVEC");
        }

        protected RegisterValue BeforeMTVECWrite(RegisterValue value)
        {
            return HandleMTVEC_STVECWrite(value, "MTVEC");
        }

        protected RegisterValue BeforeVectorExtensionRegisterWrite(RegisterValue value)
        {
            AssertVectorExtension();
            return value;
        }

        protected void BeforeVectorExtensionRegisterRead()
        {
            AssertVectorExtension();
        }

        protected IEnumerable<CPURegister> GetCustomCSRs()
        {
            return nonstandardCSR.Keys.Select(index => new CPURegister((int)index, MostSignificantBit + 1, false, false));
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
            if(SupportsInstructionSet(InstructionSet.F))
            {
                registers = registers.Append(new CPURegister((int)RiscVRegisterDescription.IndexOfFcsrRegister, 32, false, false));
            }
            return registers;
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

        protected override Interrupt DecodeInterrupt(int number)
        {
            return Interrupt.Hard;
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

        protected virtual ulong ReadCSRInner(ulong csr)
        {
            var readMethod = nonstandardCSR[csr].ReadOperation;
            if(readMethod == null)
            {
                this.Log(LogLevel.Warning, "Read method is not implemented for CSR=0x{0:X}", csr);
                return 0;
            }
            return readMethod();
        }

        protected virtual void WriteCSRInner(ulong csr, ulong value)
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

        protected bool TrySetNonMappedRegister(int register, RegisterValue value)
        {
            if(SupportsInstructionSet(InstructionSet.V) && IsVectorRegisterNumber(register))
            {
                return TrySetVectorRegister((uint)register - RiscVRegisterDescription.StartOfVRegisters, value);
            }
            else if(SupportsInstructionSet(InstructionSet.F) && register == (int)RiscVRegisterDescription.IndexOfFcsrRegister)
            {
                FFLAGSField = BitHelper.GetValue(value, 0, 5);
                FRMField = BitHelper.GetValue(value, 5, 3);
                return true;
            }
            return TrySetCustomCSR(register, value);
        }

        protected bool TryGetNonMappedRegister(int register, out RegisterValue value)
        {
            if(SupportsInstructionSet(InstructionSet.V) && IsVectorRegisterNumber(register))
            {
                return TryGetVectorRegister((uint)register - RiscVRegisterDescription.StartOfVRegisters, out value);
            }
            else if(SupportsInstructionSet(InstructionSet.F) && register == (int)RiscVRegisterDescription.IndexOfFcsrRegister)
            {
                var fflagsMasked = BitHelper.GetMaskedValue(FFLAGSField, 0, 5);
                var frmMasked = BitHelper.GetMaskedValue(FRMField, 0, 3);
                value = frmMasked << 5 | fflagsMasked;
                return true;
            }
            return TryGetCustomCSR(register, out value);
        }

        protected void PCWritten()
        {
            pcWrittenFlag = true;
        }

        protected virtual void PreStackAccessHook(ulong address, uint width, bool isWrite)
        {
            PreStackAccess?.Invoke(address, width, isWrite);
        }

        protected bool IsInterrupt(ulong exceptionIndex)
        {
            return BitHelper.IsBitSet(exceptionIndex, MostSignificantBit);
        }

        protected abstract byte MostSignificantBit { get; }

        [Import]
        protected Action<uint, uint> TlibSetMipBit;

        // These patterns are defined in RISC-V User-Level ISA V2.2, section 1.2 Instruction Length Encoding
        // there are more, but we support only 16, 32 and 64 bit long custom instructions
        private static void CheckCustomInstructionLengthPattern(ulong pattern, int bitLength)
        {
            if(bitLength == 16 && ((pattern & 0b11) == 0b11))
            {
                ReportInvalidCustomInstructionFormat(pattern, bitLength, "AA".PadLeft(bitLength, 'x') + ", AA != 11");
            }
            else if(bitLength == 32 && (
                ((pattern & 0b11) != 0b11) ||
                ((pattern & 0b11100) == 0b11100))
            )
            {
                ReportInvalidCustomInstructionFormat(pattern, bitLength, "BBB11".PadLeft(bitLength, 'x') + ", BBB != 111");
            }
            else if(bitLength == 64 && ((pattern & 0b1111111) != 0b0111111))
            {
                ReportInvalidCustomInstructionFormat(pattern, bitLength, "0111111".PadLeft(bitLength, 'x'));
            }
        }

        private static void ReportInvalidCustomInstructionFormat(ulong pattern, int bitsLength, string format)
        {
            throw new RecoverableException($"Pattern 0x{pattern:X} is invalid for {bitsLength} bits long instruction. Expected instruction in format: {format}");
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

        [Export]
        private void HandlePreStackAccessHook(ulong address, uint width, uint isWrite)
        {
            PreStackAccessHook(address, width, isWrite > 0);
        }

        // Similar logic to mtvec_stvec_write_handler from tlib/arch/riscv/op_helper.c
        private RegisterValue HandleMTVEC_STVECWrite(RegisterValue value, string registerName)
        {
            var modifiedValue = value.RawValue;
            var interruptModeBits = BitHelper.GetValue(value.RawValue, offset: 0, size: 2);

            switch(interruptMode)
            {
            case InterruptMode.Auto:
                if(interruptModeBits != 0x0)
                {
                    switch(privilegedArchitecture)
                    {
                    case PrivilegedArchitecture.PrivUnratified:
                        break;
                    case PrivilegedArchitecture.Priv1_09:
                        BitHelper.ClearBits(ref modifiedValue, position: 0, width: 2);
                        break;
                    default:
                        BitHelper.ClearBits(ref modifiedValue, position: 1, width: 1);
                        break;
                    }
                }
                break;
            case InterruptMode.Direct:
                if(interruptModeBits != 0x0)
                {
                    BitHelper.ClearBits(ref modifiedValue, position: 0, width: 2);
                }
                break;
            case InterruptMode.Vectored:
                if(interruptModeBits != 0x1)
                {
                    modifiedValue = BitHelper.ReplaceBits(value.RawValue, 0x1, width: 2);
                }
                break;
            }

            if(modifiedValue != value.RawValue)
            {
                this.Log(LogLevel.Warning, "CPU is configured in the {3} interrupt mode, modifying {2} to 0x{0:X} (tried to set 0x{1:X})",
                        modifiedValue, value.RawValue, registerName, interruptMode);
                value = RegisterValue.Create(modifiedValue, value.Bits);
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

            TlibSetPrivilegeArchitecture((int)privilegedArchitecture);
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

        private IIndirectCSRPeripheral GetIndirectCsrPeripheral(uint iselect)
        {
            return indirectCsrPeripherals.SingleOrDefault(p => p.Key.Range.Contains(iselect)).Value;
        }

        private uint ReadIndirectCSR(uint iselect, uint ireg)
        {
            var peripheral = GetIndirectCsrPeripheral(iselect);
            if(peripheral == null)
            {
                this.WarningLog("Unknown indirect CSR 0x{0:x}", iselect);
                return 0;
            }
            return peripheral.ReadIndirectCSR(iselect - (uint)GetRegistrationPoints(peripheral).Single().Range.StartAddress, ireg);
        }

        private void WriteIndirectCSR(uint iselect, uint ireg, uint value)
        {
            var peripheral = GetIndirectCsrPeripheral(iselect);
            if(peripheral == null)
            {
                this.WarningLog("Unknown indirect CSR 0x{0:x}", iselect);
                return;
            }
            peripheral.WriteIndirectCSR(iselect - (uint)GetRegistrationPoints(peripheral).Single().Range.StartAddress, ireg, value);
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
        private void HandlePostOpcodeExecutionHook(UInt32 id, UInt64 pc, UInt64 opcode)
        {
            this.NoisyLog($"Got post-opcode hook for opcode `0x{opcode:X}` with id {id} from PC {pc}");
            if(id < (uint)postOpcodeExecutionHooks.Count)
            {
                postOpcodeExecutionHooks[(int)id].Invoke(pc, opcode);
            }
            else
            {
                this.ErrorLog("Received post-opcode hook for opcode `0x{0:X}` with non-existing id = {1}", opcode, id);
            }
        }

        [Export]
        private void HandlePreOpcodeExecutionHook(UInt32 id, UInt64 pc, UInt64 opcode)
        {
            this.NoisyLog($"Got pre-opcode hook for opcode `0x{opcode:X}` with id {id} from PC {pc}");
            if(id < (uint)preOpcodeExecutionHooks.Count)
            {
                preOpcodeExecutionHooks[(int)id].Invoke(pc, opcode);
            }
            else
            {
                this.ErrorLog("Received pre-opcode hook for opcode `0x{0:X}` with non-existing id = {1}", opcode, id);
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

        [Export]
        private void ClicClearEdgeInterrupt()
        {
            if(clic == null)
            {
                this.ErrorLog("Attempting to clear CLIC edge interrupt, but there is no CLIC peripheral connected to this core.");
                return;
            }
            clic.ClearEdgeInterrupt();
        }

        [Export]
        private void ClicAcknowledgeInterrupt()
        {
            if(clic == null)
            {
                this.ErrorLog("Attempting to acknowledge CLIC interrupt, but there is no CLIC peripheral connected to this core.");
                return;
            }
            clic.AcknowledgeInterrupt();
        }

        [Export]
        private void ExternalPMPConfigCSRWrite(uint registerIndex, ulong value)
        {
            if(externalPMP == null)
            {
                this.ErrorLog("Attempted to write ExternalPMP config register {0} but no external pmp is attached to the core, write ignored", registerIndex);
                return;
            }
            externalPMP.ConfigCSRWrite(registerIndex, value);
        }

        [Export]
        private ulong ExternalPMPConfigCSRRead(uint registerIndex)
        {
            if(externalPMP == null)
            {
                this.ErrorLog("Attempted to read ExternalPMP config register {0} but no external PMP is attached to the core, returning 0", registerIndex);
                return 0;
            }
            return externalPMP.ConfigCSRRead(registerIndex);
        }

        [Export]
        private void ExternalPMPAddressCSRWrite(uint registerIndex, ulong value)
        {
            if(externalPMP == null)
            {
                this.ErrorLog("Attempted to write ExternalPMP address register {0} but no external PMP is attached to the core, write ignored", registerIndex);
                return;
            }
            externalPMP.AddressCSRWrite(registerIndex, value);
        }

        [Export]
        private ulong ExternalPMPAddressCSRRead(uint registerIndex)
        {
            if(externalPMP == null)
            {
                this.ErrorLog("Attempted to read ExternalPMP address register {0} but no external PMP is attached to the core, returning 0", registerIndex);
                return 0;
            }
            return externalPMP.AddressCSRRead(registerIndex);
        }

        [Export]
        private int ExternalPMPGetAccess(ulong address, ulong size, int access_type)
        {
            if(externalPMP == null)
            {
                this.ErrorLog("Attempted to get permissions for address 0x{0:X} from external PMP but no external PMP is attached to the core, returning 0", address);
                return 0;
            }
            return externalPMP.GetAccess(address, size, (AccessType)access_type);
        }

        [Export]
        private int ExternalPMPGetOverlappingRegion(ulong address, ulong size, int startingIndex)
        {
            if(externalPMP == null)
            {
                this.ErrorLog("Attempted to get overlapping region for address 0x{0:X} from external PMP but no external PMP is attached to the core, returning -1", address);
                return -1;
            }
            if(externalPMP.TryGetOverlappingRegion(address, size, (uint)startingIndex, out var overlappingIndex))
            {
                return (int)overlappingIndex;
            }
            else
            {
                return -1;
            }
        }

        [Export]
        private int ExternalPMPIsAnyRegionLocked()
        {
            if(externalPMP == null)
            {
                this.ErrorLog("Attempted to get if any region is locked from external PMP but no external PMP is attached to the core, returning false");
                // Exported methods can't return bools
                return 0;
            }
            return externalPMP.IsAnyRegionLocked() ? 1 : 0;
        }

        [Export]
        private ulong ReadCSR(ulong csr)
        {
            return ReadCSRInner(csr);
        }

        [Export]
        private void WriteCSR(ulong csr, ulong value)
        {
            WriteCSRInner(csr, value);
        }

        private List<GDBFeatureDescriptor> gdbFeatures = new List<GDBFeatureDescriptor>();

        private ulong? nmiVectorAddress;
        private uint? nmiVectorLength;
        private uint miselectValue;
        private uint siselectValue;

        private CoreLocalInterruptController clic;

        private ExternalPMPBase externalPMP;

        private bool pcWrittenFlag;
        private ulong resetVector = DefaultResetVector;

        private readonly InterruptMode interruptMode;

        private readonly List<Tuple<string, ulong, ulong>> customOpcodes;

        private readonly IRiscVTimeProvider timeProvider;

        private readonly PrivilegedArchitecture privilegedArchitecture;

        private readonly Dictionary<ulong, NonstandardCSR> nonstandardCSR;

        private readonly Dictionary<ulong, Action<UInt64>> customInstructionsMapping;

        private readonly Dictionary<SimpleCSR, ulong> simpleCSRs = new Dictionary<SimpleCSR, ulong>();

        private readonly ArchitectureDecoder architectureDecoder;

        private readonly Dictionary<BusRangeRegistration, IIndirectCSRPeripheral> indirectCsrPeripherals;

        [Constructor]
        private readonly List<Action<ulong, ulong>> postOpcodeExecutionHooks;

        [Constructor]
        private readonly List<Action<ulong, ulong>> preOpcodeExecutionHooks;

        [Transient]
        private readonly Action<bool>[] postGprAccessHooks;

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649
        [Import]
        private readonly Action<uint> TlibAllowFeature;

        [Import]
        private readonly Action<uint> TlibAllowAdditionalFeature;

        [Import]
        private readonly Func<uint, uint> TlibIsFeatureEnabled;

        [Import]
        private readonly Func<uint, uint> TlibIsFeatureAllowed;

        [Import]
        private readonly Func<uint, uint> TlibIsAdditionalFeatureEnabled;

        [Import(Name="tlib_set_privilege_architecture")]
        private readonly Action<int> TlibSetPrivilegeArchitecture;

        [Import]
        private readonly Action<uint> TlibSetHartId;

        [Import]
        private readonly Func<uint> TlibGetHartId;

        [Import]
        private readonly Func<uint> TlibGetCurrentPriv;

        [Import]
        private readonly Action<uint> TlibSetNapotGrain;

        [Import]
        private readonly Action<uint> TlibSetPmpaddrBits;

        [Import]
        private readonly Action<bool> TlibEnableExternalPmp;

        [Import]
        private readonly Action<uint, ulong, ulong> TlibSetPmpaddr;

        [Import]
        private readonly Func<ulong, ulong, ulong, ulong> TlibInstallCustomInstruction;

        [Import(Name="tlib_install_custom_csr")]
        private readonly Func<ushort, int> TlibInstallCustomCSR;

        [Import]
        private readonly Func<ulong, bool, bool, int> TlibInstallCustomInterrupt;

        [Import]
        private readonly Action<uint> TlibRaiseInterrupt;

        [Import]
        private readonly Action<uint, uint> TlibMarkFeatureSilent;

        [Import]
        private readonly Action<ulong, uint> TlibSetNmiVector;

        [Import]
        private readonly Action<int, int, ulong> TlibSetNmi;

        [Import]
        private readonly Action<uint> TlibSetCsrValidationLevel;

        [Import]
        private readonly Func<uint> TlibGetCsrValidationLevel;

        [Import]
        private readonly Action<int> TlibAllowUnalignedAccesses;

        [Import]
        private readonly Action<int> TlibSetInterruptMode;

        [Import]
        private readonly Func<uint, uint> TlibSetVlen;

        [Import]
        private readonly Func<uint, uint> TlibSetElen;

        [Import]
        private readonly Func<uint, uint, ulong> TlibGetVector;

        [Import]
        private readonly Action<uint, uint, ulong> TlibSetVector;

        [Import]
        private readonly Func<uint, IntPtr, uint> TlibGetWholeVector;

        [Import]
        private readonly Func<uint, IntPtr, uint> TlibSetWholeVector;

        [Import]
        private readonly Action<uint> TlibEnablePostOpcodeExecutionHooks;

        [Import]
        private readonly Func<ulong, ulong, uint> TlibInstallPostOpcodeExecutionHook;

        [Import]
        private readonly Action<uint> TlibEnablePreOpcodeExecutionHooks;

        [Import]
        private readonly Func<ulong, ulong, uint> TlibInstallPreOpcodeExecutionHook;

        [Import]
        private readonly Action<uint> TlibEnablePostGprAccessHooks;

        [Import]
        private readonly Action<uint, uint> TlibEnablePostGprAccessHookOn;

        [Import]
        private readonly Action<bool> TlibEnablePreStackAccessHook;

        [Import]
        private readonly Action<int, uint, uint, uint> TlibSetClicInterruptState;

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

        // In MISA register the extensions are encoded on bits [25:0] (see: https://five-embeddev.com/riscv-isa-manual/latest/machine.html),
        // but because these additional features are not there RISCV_ADDITIONAL_FEATURE_OFFSET allows to show that they are unrelated to MISA.
        private const int AdditionalExtensionOffset = 26;

        private const ulong DefaultResetVector = 0x1000;
        private const int NumberOfGeneralPurposeRegisters = 32;

        [NameAlias("PrivilegeArchitecture")]
        public enum PrivilegedArchitecture
        {
            Priv1_09,
            Priv1_10,
            Priv1_11,
            Priv1_12,
            /* Keep last.
             * For features that are not yet part of a ratified privileged specification.
             * As new specs become ratified, we should substitute uses of Unratified to the new spec value.
             */
            PrivUnratified
        }

        /* The enabled instruction sets are exposed via a register. Each instruction bit is represented
         * by a single bit, in alphabetical order. E.g. bit 0 represents set 'A', bit 12 represents set 'M' etc.
         */
        public enum InstructionSet
        {
            I = 'I' - 'A',
            E = 'E' - 'A',
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
            BA = 0,
            BB = 1,
            BC = 2,
            BS = 3,
            ICSR = 4,
            IFENCEI = 5,
            ZFH = 6,
            ZVFH = 7,
            SMEPMP = 8,
            ZVE32X = 9,
            ZVE32F = 10,
            ZVE64X = 11,
            ZVE64F = 12,
            ZVE64D = 13,
            ZACAS = 14,
            SSCOFPMF = 15,
            ZCB = 16,
            ZCMP = 17,
            ZCMT = 18,
        }

        public enum InterruptMode
        {
            // entries match values
            // in tlib, do not change
            Auto = 0,
            Direct = 1,
            Vectored = 2
        }

        public enum PrivilegeLevels
        {
            Machine,
            MachineUser,
            MachineSupervisorUser,
        }

        public enum ExceptionCodes
        {
            InstructionAddressMisaligned = 0x0,
            InstructionAccessFault = 0x1,
            IllegalInstruction = 0x2,
            Breakpoint = 0x3,
            LoadAddressMisaligned = 0x4,
            LoadAccessFault = 0x5,
            StoreAddressMisaligned = 0x6,
            StoreAccessFault = 0x7,
            UModeEnvironmentCall = 0x8,
            SModeEnvironmentCall = 0x9,
            HModeEnvironmentCall = 0xA,
            MModeEnvironmentCall = 0xB,
            InstructionPageFault = 0xC,
            LoadPageFault = 0xD,
            StorePageFault = 0xF,
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

        protected enum StandardCSR : ushort
        {
            Siselect = 0x150,
            Sireg = 0x151, // sireg, sireg2, ..., sireg6 (0x156)
            Miselect = 0x350,
            Mireg = 0x351, // mireg, mireg2, ..., mireg6 (0x356)
        }

        private class ArchitectureDecoder
        {
            public ArchitectureDecoder(IMachine machine, BaseRiscV parent, string architectureString, PrivilegeLevels privilegeLevels)
            {
                this.parent = parent;
                this.machine = machine;
                instructionSets = new List<InstructionSet>();
                standardExtensions = new List<StandardInstructionSetExtensions>();

                Decode(architectureString);
                DecodePrivilegeLevels(privilegeLevels);
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
                architectureString = architectureString.ToUpper();

                if(!architectureString.StartsWith("RV"))
                {
                    throw new ConstructionException($"Architecture string should start with rv, but is: {architectureString}");
                }
                var instructionSetsString = architectureString.Skip(2);

                var bits = string.Join("", instructionSetsString.TakeWhile(Char.IsDigit));
                if(bits.Length == 0 || int.Parse(bits) != parent.MostSignificantBit + 1)
                {
                    throw new ConstructionException($"Unexpected architecture width: {bits}");
                }
                instructionSetsString = instructionSetsString.Skip(bits.Length);

                while(instructionSetsString.Count() != 0)
                {
                    if(instructionSetsString.First() == '_')
                    {
                        if(instructionSetsString.Count() == 1)
                        {
                            break;
                        }
                        instructionSetsString = instructionSetsString.Skip(1);
                    }

                    string isaStringPart = "";
                    if(TryHandleSingleCharInstructionSetName(instructionSetsString.First()))
                    {
                        isaStringPart = instructionSetsString.First().ToString();
                    }
                    else
                    {
                        isaStringPart = String.Join("", instructionSetsString.TakeWhile(Char.IsLetterOrDigit));
                        HandleLongInstructionSetName(isaStringPart);
                    }

                    parent.DebugLog("Matched ISA String :'{0}'", isaStringPart);
                    // Consume used characters
                    instructionSetsString = instructionSetsString.Skip(isaStringPart.Length);
                }
            }

            private bool TryHandleSingleCharInstructionSetName(char isaChar)
            {
                switch(isaChar)
                {
                case 'I':
                    if(instructionSets.Contains(InstructionSet.E))
                    {
                        throw new ConstructionException($"ISA string cannot contain both I and E base instruction sets at the same time.");
                    }
                    instructionSets.Add(InstructionSet.I);
                    break;
                case 'E':
                    if(instructionSets.Contains(InstructionSet.I))
                    {
                        throw new ConstructionException($"ISA string cannot contain both I and E base instruction sets at the same time.");
                    }
                    instructionSets.Add(InstructionSet.E);
                    break;
                case 'M':
                    instructionSets.Add(InstructionSet.M);
                    break;
                case 'A':
                    instructionSets.Add(InstructionSet.A);
                    break;
                case 'F':
                    instructionSets.Add(InstructionSet.F);
                    break;
                case 'D':
                    instructionSets.Add(InstructionSet.D);
                    break;
                case 'C':
                    instructionSets.Add(InstructionSet.C);
                    break;
                case 'V':
                    instructionSets.Add(InstructionSet.V);
                    break;
                case 'B':
                    instructionSets.Add(InstructionSet.B);
                    break;
                case 'G':
                    instructionSets.Add(InstructionSet.G);
                    break;
                case 'U':
                    parent.WarningLog("Enabling privilege level extension '{0}' using 'cpuType' is not supported. " +
                        "Privilege levels should be specified using the 'privilegeLevels' constructor parameter. " +
                        "Extension will not be enabled", isaChar);
                    break;
                default:
                    return false;
                }
                ValidateInstructionSetForBaseE();
                return true;
            }

            private void HandleLongInstructionSetName(string name)
            {
                switch(name)
                {
                case "S":
                    parent.WarningLog("Enabling privilege level extension '{0}' using 'cpuType' is not supported. " +
                        "Privilege levels should be specified using the 'privilegeLevels' constructor parameter. " +
                        "Extension will not be enabled", name);
                    break;
                case "SMEPMP": standardExtensions.Add(StandardInstructionSetExtensions.SMEPMP); break;
                case "SSCOFPMF": standardExtensions.Add(StandardInstructionSetExtensions.SSCOFPMF); break;
                case "XANDES": Andes_AndeStarV5Extension.RegisterIn(machine, (RiscV32)parent); break;
                case "ZBA": standardExtensions.Add(StandardInstructionSetExtensions.BA); break;
                case "ZBB": standardExtensions.Add(StandardInstructionSetExtensions.BB); break;
                case "ZBC": standardExtensions.Add(StandardInstructionSetExtensions.BC); break;
                case "ZBS": standardExtensions.Add(StandardInstructionSetExtensions.BS); break;
                case "ZICSR": standardExtensions.Add(StandardInstructionSetExtensions.ICSR); break;
                case "ZIFENCEI": standardExtensions.Add(StandardInstructionSetExtensions.IFENCEI); break;
                case "ZFH": standardExtensions.Add(StandardInstructionSetExtensions.ZFH); break;
                case "ZVFH": standardExtensions.Add(StandardInstructionSetExtensions.ZVFH); break;
                case "ZVE32X": standardExtensions.Add(StandardInstructionSetExtensions.ZVE32X); break;
                case "ZVE32F": standardExtensions.Add(StandardInstructionSetExtensions.ZVE32F); break;
                case "ZVE64X": standardExtensions.Add(StandardInstructionSetExtensions.ZVE64X); break;
                case "ZVE64F": standardExtensions.Add(StandardInstructionSetExtensions.ZVE64F); break;
                case "ZVE64D": standardExtensions.Add(StandardInstructionSetExtensions.ZVE64D); break;
                case "ZACAS": standardExtensions.Add(StandardInstructionSetExtensions.ZACAS); break;
                case "ZCA":
                    instructionSets.Add(InstructionSet.C); // ZCA maps to base C extension
                    break;
                case "ZCB": standardExtensions.Add(StandardInstructionSetExtensions.ZCB); break;
                case "ZCMP":
                    if(!instructionSets.Contains(InstructionSet.C))
                    {
                        throw new ConstructionException("Zcmp extension requires C instruction set");
                    }
                    if(instructionSets.Contains(InstructionSet.D))
                    {
                        throw new ConstructionException($"ISA string cannot contain both Zcmp extension and D instruction set at the same time.");
                    }
                    standardExtensions.Add(StandardInstructionSetExtensions.ZCMP);
                    break;
                case "ZCMT":
                    if(!instructionSets.Contains(InstructionSet.C))
                    {
                        throw new ConstructionException("Zcmt extension requires C instruction set");
                    }
                    if(instructionSets.Contains(InstructionSet.D))
                    {
                        throw new ConstructionException($"ISA string cannot contain both Zcmt extension and D instruction set at the same time.");
                    }
                    standardExtensions.Add(StandardInstructionSetExtensions.ZCMT);
                    break;
                default:
                    throw new ConstructionException($"Undefined instructions set extension: '{name}'");
                }
            }

            private void DecodePrivilegeLevels(PrivilegeLevels privilegeLevels)
            {
                switch(privilegeLevels)
                {
                case PrivilegeLevels.Machine:
                    break; // Nothing to do
                case PrivilegeLevels.MachineUser:
                    instructionSets.Add(InstructionSet.U);
                    break;
                case PrivilegeLevels.MachineSupervisorUser:
                    instructionSets.Add(InstructionSet.S);
                    instructionSets.Add(InstructionSet.U);
                    break;
                default:
                    throw new Exception("Unreachable");
                }
            }

            private void ValidateInstructionSetForBaseE()
            {
                if(instructionSets.Contains(InstructionSet.E)
                    && (instructionSets.Any(x => (x != InstructionSet.E)
                                                && (x != InstructionSet.M)
                                                && (x != InstructionSet.A)
                                                && (x != InstructionSet.C))))
                {
                    throw new ConstructionException($"RV32E can only have M, A and C standard extensions");
                }
            }

            private readonly IList<StandardInstructionSetExtensions> standardExtensions;
            private readonly IList<InstructionSet> instructionSets;
            private readonly BaseRiscV parent;
            private readonly IMachine machine;
        }
    }
}
