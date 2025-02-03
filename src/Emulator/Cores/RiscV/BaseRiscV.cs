//
// Copyright (c) 2010-2025 Antmicro
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
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.CFU;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Migrant;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class BaseRiscV : TranslationCPU, IPeripheralContainer<ICFU, NumberRegistrationPoint<int>>, IPeripheralContainer<IIndirectCSRPeripheral, BusRangeRegistration>, ICPUWithPostOpcodeExecutionHooks, ICPUWithPostGprAccessHooks, ICPUWithNMI
    {
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
            PrivilegeLevels privilegeLevels = PrivilegeLevels.MachineSupervisorUser
        )
            : base(hartId, cpuType, machine, endianness, bitness)
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
            postOpcodeExecutionHooks = new List<Action<ulong>>();
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

            TlibSetPmpaddrBits(pmpNumberOfAddrBits);
            TlibSetNapotGrain(minimalPmpNapotInBytes);

            RegisterCSR((ulong)StandardCSR.Miselect, () => miselectValue, s => miselectValue = (uint)s, "miselect");
            for(uint i = 0; i < 6; ++i)
            {
                var j = i;
                RegisterCSR((ulong)StandardCSR.Mireg + i, () => ReadIndirectCSR(miselectValue, j), v => WriteIndirectCSR(miselectValue, j, (uint)v), $"mireg{i + 1}");
            }

            RegisterCSR((ulong)StandardCSR.Siselect, () => siselectValue, s => siselectValue = (uint)s, "siselect");
            for(uint i = 0; i < 6; ++i)
            {
                var j = i;
                RegisterCSR((ulong)StandardCSR.Sireg + i, () => ReadIndirectCSR(siselectValue, j), v => WriteIndirectCSR(siselectValue, j, (uint)v), $"sireg{i + 1}");
            }
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

        public void Register(IIndirectCSRPeripheral peripheral, BusRangeRegistration registrationPoint)
        {
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            indirectCsrPeripherals.Add(registrationPoint, peripheral);
        }

        public void Unregister(IIndirectCSRPeripheral peripheral)
        {
            foreach(var point in GetRegistrationPoints(peripheral).ToList())
            {
                indirectCsrPeripherals.Remove(point);
            }
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public IEnumerable<BusRangeRegistration> GetRegistrationPoints(IIndirectCSRPeripheral peripheral)
        {
            return indirectCsrPeripherals.Where(p => p.Value == peripheral).Select(p => p.Key);
        }

        IEnumerable<IRegistered<IIndirectCSRPeripheral, BusRangeRegistration>> IPeripheralContainer<IIndirectCSRPeripheral, BusRangeRegistration>.Children
        {
            get
            {
                return indirectCsrPeripherals.Select(x => Registered.Create(x.Value, x.Key));
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

        public void RegisterLocalInterruptController(CoreLocalInterruptController clic)
        {
            if(this.clic != null)
            {
                throw new ArgumentException($"{nameof(CoreLocalInterruptController)} is already registered");
            }
            this.clic = clic;
        }

        public void ClicPresentInterrupt(int index, bool vectored, int level, PrivilegeLevel mode)
        {
            TlibSetClicInterruptState(index, vectored ? 1u : 0, (uint)level, (uint)mode);
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

        private static void ReportInvalidCustomInstructionFormat(ulong pattern, int bitsLength, string format)
        {
            throw new RecoverableException($"Pattern 0x{pattern:X} is invalid for {bitsLength} bits long instruction. Expected instruction in format: {format}");
        }

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

        public readonly Dictionary<int, ICFU> ChildCollection;

        private ulong? nmiVectorAddress;
        private uint? nmiVectorLength;
        private uint miselectValue;
        private uint siselectValue;

        private CoreLocalInterruptController clic;

        private bool pcWrittenFlag;
        private ulong resetVector = DefaultResetVector;

        private readonly IRiscVTimeProvider timeProvider;

        private readonly PrivilegedArchitecture privilegedArchitecture;

        private readonly Dictionary<ulong, NonstandardCSR> nonstandardCSR;

        private readonly Dictionary<ulong, Action<UInt64>> customInstructionsMapping;

        private readonly Dictionary<SimpleCSR, ulong> simpleCSRs = new Dictionary<SimpleCSR, ulong>();

        private List<GDBFeatureDescriptor> gdbFeatures = new List<GDBFeatureDescriptor>();

        private readonly ArchitectureDecoder architectureDecoder;

        private readonly Dictionary<BusRangeRegistration, IIndirectCSRPeripheral> indirectCsrPeripherals;

        [Constructor]
        private readonly List<Action<ulong>> postOpcodeExecutionHooks;

        [Transient]
        private readonly Action<bool>[] postGprAccessHooks;

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649
        [Import]
        private Action<uint> TlibAllowFeature;

        [Import]
        private Action<uint> TlibAllowAdditionalFeature;

        [Import]
        private Func<uint, uint> TlibIsFeatureEnabled;

        [Import]
        private Func<uint, uint> TlibIsFeatureAllowed;

        [Import(Name="tlib_set_privilege_architecture")]
        private Action<int> TlibSetPrivilegeArchitecture;

        [Import]
        private Action<uint, uint> TlibSetMipBit;

        [Import]
        private Action<uint> TlibSetHartId;

        [Import]
        private Func<uint> TlibGetHartId;

        [Import]
        private Action<uint> TlibSetNapotGrain;

        [Import]
        private Action<uint> TlibSetPmpaddrBits;

        [Import]
        private Func<ulong, ulong, ulong, ulong> TlibInstallCustomInstruction;

        [Import(Name="tlib_install_custom_csr")]
        private Func<ulong, int> TlibInstallCustomCSR;

        [Import]
        private Action<uint, uint> TlibMarkFeatureSilent;

        [Import]
        private Action<ulong, uint> TlibSetNmiVector;

        [Import]
        private Action<int, int, ulong> TlibSetNmi;

        [Import]
        private Action<uint> TlibSetCsrValidationLevel;

        [Import]
        private Func<uint> TlibGetCsrValidationLevel;

        [Import]
        private Action<int> TlibAllowUnalignedAccesses;

        [Import]
        private Action<int> TlibSetInterruptMode;

        [Import]
        private Func<uint, uint> TlibSetVlen;

        [Import]
        private Func<uint, uint> TlibSetElen;

        [Import]
        private Func<uint, uint, ulong> TlibGetVector;

        [Import]
        private Action<uint, uint, ulong> TlibSetVector;

        [Import]
        private Func<uint, IntPtr, uint> TlibGetWholeVector;

        [Import]
        private Func<uint, IntPtr, uint> TlibSetWholeVector;

        [Import]
        private Action<uint> TlibEnablePostOpcodeExecutionHooks;

        [Import]
        private Func<ulong, ulong, uint> TlibInstallPostOpcodeExecutionHook;

        [Import]
        private Action<uint> TlibEnablePostGprAccessHooks;

        [Import]
        private Action<uint, uint> TlibEnablePostGprAccessHookOn;

        [Import]
        private Action<int, uint, uint, uint> TlibSetClicInterruptState;

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

        protected enum StandardCSR
        {
            Siselect = 0x150,
            Sireg = 0x151, // sireg, sireg2, ..., sireg6 (0x156)
            Miselect = 0x350,
            Mireg = 0x351, // mireg, mireg2, ..., mireg6 (0x356)
        }
    }
}
