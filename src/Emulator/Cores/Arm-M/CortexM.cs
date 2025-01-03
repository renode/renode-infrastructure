//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Renode.Logging;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Exceptions;
using ELFSharp.ELF;
using ELFSharp.UImage;
using Machine = Antmicro.Renode.Core.Machine;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class CortexM : Arm, IPeripheralWithTransactionState
    {
        public CortexM(string cpuType, IMachine machine, NVIC nvic, [NameAlias("id")] uint cpuId = 0, Endianess endianness = Endianess.LittleEndian,
            uint? fpuInterruptNumber = null, uint? numberOfMPURegions = null, bool enableTrustZone = false, uint? numberOfSAURegions = null, uint? numberOfIDAURegions = null)
            : base(cpuType, machine, cpuId, endianness, numberOfMPURegions)
        {
            if(nvic == null)
            {
                throw new RecoverableException(new ArgumentNullException("nvic"));
            }

            tlibSetFpuInterruptNumber((int?)fpuInterruptNumber ?? -1);

            if(!numberOfMPURegions.HasValue)
            {
                // FIXME: This is not correct for M7 and v8M cores
                // Setting 8 regions backward-compatibility for now
                this.NumberOfMPURegions = 8;
            }

            TrustZoneEnabled = enableTrustZone;
            if(TrustZoneEnabled)
            {
                // Set CPU to start in Secure State
                // this also enables TrustZone in the translation library
                tlibSetSecurityState(1u);

                NumberOfSAURegions = numberOfSAURegions ?? 8;  // TODO: Determine default number
                if(!numberOfSAURegions.HasValue)
                {
                    this.Log(LogLevel.Info, "Configuring Security Attribution Unit regions to default: {0}", NumberOfSAURegions);
                }

                NumberOfIDAURegions = numberOfIDAURegions ?? 8;  // TODO: Determine default number
                if(!numberOfIDAURegions.HasValue)
                {
                    this.Log(LogLevel.Info, "Configuring Implementation-Defined Attribution Unit regions to default: {0}", NumberOfIDAURegions);
                }
            }

            this.nvic = nvic;
            try
            {
                nvic.AttachCPU(this);
            }
            catch(RecoverableException e)
            {
                // Rethrow attachment error as ConstructionException, so the CreationDriver doesn't crash
                throw new ConstructionException("Exception occurred when attaching NVIC: ", e);
            }
        }

        public class ContextState : IContextState
        {
            public bool Privileged;
            public bool CpuSecure;
            public bool AttributionSecure;
        }

        public override void Reset()
        {
            pcNotInitialized = true;
            vtorInitialized = false;
            base.Reset();
        }

        public void SetSleepOnExceptionExit(bool value)
        {
            tlibSetSleepOnExceptionExit(value ? 1 : 0);
        }

        protected override void OnResume()
        {
            // Suppress initialization when processor is turned off as binary may not even be loaded yet
            if(!IsHalted)
            {
                InitPCAndSP();
            }
            base.OnResume();
        }

        public override string Architecture { get { return "arm-m"; } }

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                var features = new List<GDBFeatureDescriptor>();

                var mProfileFeature = new GDBFeatureDescriptor("org.gnu.gdb.arm.m-profile");
                for(var index = 0u; index <= 12; index++)
                {
                    mProfileFeature.Registers.Add(new GDBRegisterDescriptor(index, 32, $"r{index}", "uint32", "general"));
                }
                mProfileFeature.Registers.Add(new GDBRegisterDescriptor(13, 32, "sp", "data_ptr", "general"));
                mProfileFeature.Registers.Add(new GDBRegisterDescriptor(14, 32, "lr", "uint32", "general"));
                mProfileFeature.Registers.Add(new GDBRegisterDescriptor(15, 32, "pc", "code_ptr", "general"));
                mProfileFeature.Registers.Add(new GDBRegisterDescriptor(25, 32, "xpsr", "uint32", "general"));
                features.Add(mProfileFeature);

                var mSystemFeature = new GDBFeatureDescriptor("org.gnu.gdb.arm.m-system");
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(26, 32, "msp", "uint32", "general"));
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(27, 32, "psp", "uint32", "general"));
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(28, 32, "primask", "uint32", "general"));
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(29, 32, "basepri", "uint32", "general"));
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(30, 32, "faultmask", "uint32", "general"));
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(31, 32, "control", "uint32", "general"));
                features.Add(mSystemFeature);

                return features;
            }
        }

        public IReadOnlyDictionary<string, int> StateBits { get { return stateBits; } }

        /// <remark>Should only be used for TrustZone CPUs, <see cref="RecoverableException"/> is thrown otherwise.</remark>
        public IDAURegion GetIDAURegion(uint regionIndex)
        {
            AssertTrustZoneEnabled($"get IDAU region {regionIndex}");

            var rbar = tlibGetIdauRegionBaseAddressRegister(regionIndex);
            var rlar = tlibGetIdauRegionLimitAddressRegister(regionIndex);
            return new IDAURegion(rbar, rlar);
        }

        /// <remarks>
        /// Should only be used for TrustZone CPUs, <see cref="RecoverableException"/> is thrown otherwise.
        /// It's also thrown in case <paramref name="region"/>'s index is greater than <see cref="NumberOfIDAURegions"/>.
        /// </remarks>
        public void SetIDAURegion(uint regionIndex, IDAURegion region)
        {
            AssertTrustZoneEnabled($"set IDAU region {regionIndex}");

            if(regionIndex >= NumberOfIDAURegions)
            {
                throw new RecoverableException($"Invalid IDAU region: {regionIndex}, {nameof(NumberOfIDAURegions)}: {NumberOfIDAURegions}");
            }

            tlibSetIdauRegionBaseAddressRegister(regionIndex, region.ToRBAR());
            tlibSetIdauRegionLimitAddressRegister(regionIndex, region.ToRLAR());
        }

        public void SetIDAURegion(uint regionIndex, uint baseAddress, uint limitAddress, bool enabled, bool nonSecureCallable)
        {
            SetIDAURegion(regionIndex, new IDAURegion(baseAddress, limitAddress, enabled, nonSecureCallable));
        }

        public void SetIDAURegion(uint regionIndex, uint rbar, uint rlar)
        {
            SetIDAURegion(regionIndex, new IDAURegion(rbar, rlar));
        }

        /// <remark>Should only be used for TrustZone CPUs, <see cref="RecoverableException"/> is thrown otherwise.</remark>
        public bool TryAddImplementationDefinedExemptionRegion(uint startAddress, uint endAddress)
        {
            AssertTrustZoneEnabled($"add implementation-defined exemption region 0x{startAddress:X8}-0x{endAddress:X8}");

            return tlibTryAddImplementationDefinedExemptionRegion(startAddress, endAddress) == 1u;
        }

        /// <remark>Should only be used for TrustZone CPUs, <see cref="RecoverableException"/> is thrown otherwise.</remark>
        public bool TryRemoveImplementationDefinedExemptionRegion(uint startAddress, uint endAddress)
        {
            AssertTrustZoneEnabled($"remove implementation-defined exemption region 0x{startAddress:X8}-0x{endAddress:X8}");

            return tlibTryRemoveImplementationDefinedExemptionRegion(startAddress, endAddress) == 1u;
        }

        public override MemorySystemArchitectureType MemorySystemArchitecture => NumberOfMPURegions > 0 ? MemorySystemArchitectureType.Physical_PMSA : MemorySystemArchitectureType.None;

        public override uint ExceptionVectorAddress
        {
            get => VectorTableOffset;
            set => VectorTableOffset = value;
        }

        // Sets VTOR for the current Security State the CPU is in right now
        public uint VectorTableOffset
        {
            get
            {
                var secure = 0u;
                if(TrustZoneEnabled)
                {
                    secure = SecureState ? 1u : 0u;
                }
                return tlibGetInterruptVectorBase(secure);
            }
            set
            {
                var secure = 0u;
                if(TrustZoneEnabled)
                {
                    secure = SecureState ? 1u : 0u;
                }
                vtorInitialized = true;
                if(!machine.SystemBus.IsMemory(value, this))
                {
                    this.Log(LogLevel.Warning, "Tried to set VTOR address at 0x{0:X} which does not lay in memory. Aborted.", value);
                    return;
                }
                this.NoisyLog("VectorTableOffset set to 0x{0:X}.", value);
                tlibSetInterruptVectorBase(value, secure);
            }
        }

        // NS alias for VTOR
        public uint VectorTableOffsetNonSecure
        {
            get
            {
                if(!TrustZoneEnabled)
                {
                    throw new RecoverableException("You need to enable TrustZone to use VTOR_NS");
                }
                return tlibGetInterruptVectorBase(0u);
            }
            set
            {
                if(!TrustZoneEnabled)
                {
                    throw new RecoverableException("You need to enable TrustZone to use VTOR_NS");
                }
                vtorInitialized = true;
                if(machine.SystemBus.FindMemory(value, this) == null)
                {
                    this.Log(LogLevel.Warning, "Tried to set VTOR_NS address at 0x{0:X} which does not lay in memory. Aborted.", value);
                    return;
                }
                this.NoisyLog("VectorTableOffset_NS set to 0x{0:X}.", value);
                tlibSetInterruptVectorBase(value, 0u);
            }
        }

        [Register]
        public RegisterValue FPCAR_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(FPCAR_NS), () => GetRegisterValue32NonSecure((int)CortexMRegisters.FPCAR));
            set => SetTrustZoneRelatedRegister(nameof(FPCAR_NS), val => SetRegisterValue32NonSecure((int)CortexMRegisters.FPCAR, val), value);
        }

        [Register]
        public RegisterValue FPDSCR_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(FPDSCR_NS), () => GetRegisterValue32NonSecure((int)CortexMRegisters.FPDSCR));
            set => SetTrustZoneRelatedRegister(nameof(FPDSCR_NS), val => SetRegisterValue32NonSecure((int)CortexMRegisters.FPDSCR, val), value);
        }

        [Register]
        public RegisterValue FPCCR_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(FPCCR_NS), () => GetRegisterValue32NonSecure((int)CortexMRegisters.FPCCR));
            set => SetTrustZoneRelatedRegister(nameof(FPCCR_NS), val => SetRegisterValue32NonSecure((int)CortexMRegisters.FPCCR, val), value);
        }
        [Register]
        public RegisterValue CPACR_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(CPACR_NS), () => GetRegisterValue32NonSecure((int)CortexMRegisters.CPACR));
            set => SetTrustZoneRelatedRegister(nameof(CPACR_NS), val => SetRegisterValue32NonSecure((int)CortexMRegisters.CPACR, val), value);
        }

        public bool IDAUEnabled
        {
            get => GetTrustZoneRelatedRegister(nameof(IDAUEnabled), () => tlibGetIdauEnabled()) == 1u;
            set => SetTrustZoneRelatedRegister(nameof(IDAUEnabled), val => tlibSetIdauEnabled(val), value ? 1u : 0u);
        }

        public uint NumberOfIDAURegions
        {
            get => GetTrustZoneRelatedRegister(nameof(NumberOfIDAURegions), () => tlibGetNumberOfIdauRegions());
            set => SetTrustZoneRelatedRegister(nameof(NumberOfIDAURegions), val => tlibSetNumberOfIdauRegions(val), value);
        }

        public uint NumberOfSAURegions
        {
            get => GetTrustZoneRelatedRegister(nameof(NumberOfSAURegions), () => tlibGetNumberOfSauRegions());
            set => SetTrustZoneRelatedRegister(nameof(NumberOfSAURegions), val => tlibSetNumberOfSauRegions(val), value);
        }

        public bool SecureState
        {
            get
            {
                AssertTrustZoneEnabled("get SecurityState");
                return tlibGetSecurityState() > 0;
            }
            set
            {
                AssertTrustZoneEnabled("modify SecurityState");
                tlibSetSecurityState(value ? 1u : 0u);
            }
        }

        public bool FpuEnabled
        {
            set
            {
                tlibToggleFpu(value ? 1 : 0);
            }
        }

        public bool TrustZoneEnabled { get; }

        public UInt32 FaultStatus
        {
            get
            {
                var secure = 0u;
                if(TrustZoneEnabled)
                {
                    secure = SecureState ? 1u : 0u;
                }
                return tlibGetFaultStatus(secure);
            }
            set
            {
                var secure = 0u;
                if(TrustZoneEnabled)
                {
                    secure = SecureState ? 1u : 0u;
                }
                tlibSetFaultStatus(value, secure);
            }
        }

        public UInt32 FaultStatusNonSecure
        {
            get
            {
                AssertTrustZoneEnabled("get FaultStatus_NS");
                return tlibGetFaultStatus(0u);
            }
            set
            {
                AssertTrustZoneEnabled("set FaultStatus_NS");
                tlibSetFaultStatus(value, 0u);
            }
        }

        public UInt32 MemoryFaultAddress
        {
            get
            {
                var secure = 0u;
                if(TrustZoneEnabled)
                {
                    secure = SecureState ? 1u : 0u;
                }
                return tlibGetMemoryFaultAddress(secure);
            }
        }

        public UInt32 MemoryFaultAddressNonSecure
        {
            get
            {
                AssertTrustZoneEnabled("get MemoryFaultAddress_NS");
                return tlibGetMemoryFaultAddress(0u);
            }
        }

        public UInt32 SecureFaultAddress
        {
            get
            {
                AssertTrustZoneEnabled("get SecureFaultAddress");
                return tlibGetSecureFaultAddress();
            }
        }


        public UInt32 SecureFaultStatus
        {
            get
            {
                AssertTrustZoneEnabled("get SecureFaultStatus");
                return tlibGetSecureFaultStatus();
            }
            set
            {
                AssertTrustZoneEnabled("set SecureFaultStatus");
                tlibSetSecureFaultStatus(value);
            }
        }

        public bool IsV8
        {
            get
            {
                return tlibIsV8() > 0;
            }
        }

        public UInt32 PmsaV8Ctrl
        {
            get
            {
                return tlibGetPmsav8Ctrl();
            }
            set
            {
                tlibSetPmsav8Ctrl(value);
            }
        }

        public UInt32 PmsaV8Rnr
        {
            get
            {
                return tlibGetPmsav8Rnr();
            }
            set
            {
                tlibSetPmsav8Rnr(value);
            }
        }

        public UInt32 PmsaV8Rbar
        {
            get
            {
                return tlibGetPmsav8Rbar();
            }
            set
            {
                tlibSetPmsav8Rbar(value);
            }
        }

        public UInt32 PmsaV8Rlar
        {
            get
            {
                return tlibGetPmsav8Rlar();
            }
            set
            {
                tlibSetPmsav8Rlar(value);
            }
        }

        public UInt32 PmsaV8Mair0
        {
            get
            {
                return tlibGetPmsav8Mair(0);
            }
            set
            {
                tlibSetPmsav8Mair(0, value);
            }
        }

        public UInt32 PmsaV8Mair1
        {
            get
            {
                return tlibGetPmsav8Mair(1);
            }
            set
            {
                tlibSetPmsav8Mair(1, value);
            }
        }

        public bool MPUEnabled
        {
            get
            {
                return tlibIsMpuEnabled() != 0;
            }
            set
            {
                tlibEnableMpu(value ? 1 : 0);
            }
        }

        public UInt32 MPURegionBaseAddress
        {
            set
            {
                tlibSetMpuRegionBaseAddress(value);
            }
            get
            {
                return tlibGetMpuRegionBaseAddress();
            }
        }

        public UInt32 MPURegionAttributeAndSize
        {
            set
            {
                tlibSetMpuRegionSizeAndEnable(value);
            }
            get
            {
                return tlibGetMpuRegionSizeAndEnable();
            }
        }

        public UInt32 MPURegionNumber
        {
            set
            {
                tlibSetMpuRegionNumber(value);
            }
            get
            {
                return tlibGetMpuRegionNumber();
            }
        }

        public uint SAUControl
        {
            get => GetTrustZoneRelatedRegister(nameof(SAUControl), () => tlibGetSauControl());
            set => SetTrustZoneRelatedRegister(nameof(SAUControl), val => tlibSetSauControl(val), value);
        }

        public uint SAURegionNumber
        {
            get => GetTrustZoneRelatedRegister(nameof(SAURegionNumber), () => tlibGetSauRegionNumber());
            set => SetTrustZoneRelatedRegister(nameof(SAURegionNumber), val => tlibSetSauRegionNumber(val), value);
        }

        public uint SAURegionBaseAddress
        {
            get => GetTrustZoneRelatedRegister(nameof(SAURegionBaseAddress), () => tlibGetSauRegionBaseAddress());
            set => SetTrustZoneRelatedRegister(nameof(SAURegionBaseAddress), val => tlibSetSauRegionBaseAddress(val), value);
        }

        public uint SAURegionLimitAddress
        {
            get => GetTrustZoneRelatedRegister(nameof(SAURegionLimitAddress), () => tlibGetSauRegionLimitAddress());
            set => SetTrustZoneRelatedRegister(nameof(SAURegionLimitAddress), val => tlibSetSauRegionLimitAddress(val), value);
        }

        public CortexMImplementationDefinedAttributionUnit ImplementationDefinedAttributionUnit
        {
            set
            {
                if(value != null)
                {
                   tlibSetCustomIdauHandlerEnabled(1);
                }
                else
                {
                   tlibSetCustomIdauHandlerEnabled(0);
                }
                idau = value;
            }
        }

        public uint XProgramStatusRegister
        {
            get
            {
                return tlibGetXpsr();
            }
        }

        public uint GetPrimask(bool secure)
        {
            return tlibGetPrimask(secure ? 1u : 0u);
        }

        public uint GetFaultmask(bool secure)
        {
            return tlibGetFaultmask(secure ? 1u : 0u);
        }

        public override void InitFromElf(IELF elf)
        {
            // do nothing
        }

        public override void InitFromUImage(UImage uImage)
        {
            // do nothing
        }

        protected override UInt32 BeforePCWrite(UInt32 value)
        {
            if(value % 2 == 0)
            {
                this.Log(LogLevel.Warning, "Patching PC 0x{0:X} for Thumb mode.", value);
                value += 1;
            }
            pcNotInitialized = false;
            return base.BeforePCWrite(value);
        }

        protected override void OnLeavingResetState()
        {
            if(EmulationState == EmulationCPUState.Running)
            {
                InitPCAndSP();
            }
            base.OnLeavingResetState();
        }

        /// <remarks>Use <see cref="GetTrustZoneRelatedRegister"/> and <see cref="SetTrustZoneRelatedRegister"/> to wrap accesses which have to succeed.</remarks>
        private void AssertTrustZoneEnabled(string actionName)
        {
            if(!TrustZoneEnabled)
            {
                throw new RecoverableException($"Tried to {actionName} in CPU with TrustZone disabled");
            }
        }

        private uint GetTrustZoneRelatedRegister(string registerName, Func<uint> getter)
        {
            if(!TrustZoneEnabled)
            {
                this.Log(LogLevel.Warning, "Tried to read from {0} in CPU without TrustZone implemented, returning 0x0", registerName);
                return 0x0;
            }
            return getter();
        }

        private void InitPCAndSP()
        {
            var firstNotNullSection = machine.SystemBus.GetLookup(this).FirstNotNullSectionAddress;
            if(!vtorInitialized && firstNotNullSection.HasValue)
            {
                if((firstNotNullSection.Value & (2 << 6 - 1)) > 0)
                {
                    this.Log(LogLevel.Warning, "Alignment of VectorTableOffset register is not correct.");
                }
                else
                {
                    var value = firstNotNullSection.Value;
                    this.Log(LogLevel.Info, "Guessing VectorTableOffset value to be 0x{0:X}.", value);
                    if(value > uint.MaxValue)
                    {
                        this.Log(LogLevel.Error, "Guessed VectorTableOffset doesn't fit in 32-bit address space: 0x{0:X}.", value);
                        return; // Keep VectorTableOffset uninitialized in the case of error condition
                    }
                    VectorTableOffset = checked((uint)value);
                }
            }
            if(pcNotInitialized)
            {
                // stack pointer and program counter are being sent according
                // to VTOR (vector table offset register)
                var sysbus = machine.SystemBus;
                var pc = sysbus.ReadDoubleWord(VectorTableOffset + 4, this);
                var sp = sysbus.ReadDoubleWord(VectorTableOffset, this);
                if(!sysbus.IsMemory(pc, this) || (pc == 0 && sp == 0))
                {
                    this.Log(LogLevel.Error, "PC does not lay in memory or PC and SP are equal to zero. CPU was halted.");
                    IsHalted = true;
                    return; // Keep PC and SP uninitialized in the case of error condition
                }
                this.Log(LogLevel.Info, "Setting initial values: PC = 0x{0:X}, SP = 0x{1:X}.", pc, sp);
                PC = pc;
                SP = sp;
            }
        }

        private void SetTrustZoneRelatedRegister(string registerName, Action<uint> setter, uint value)
        {
            if(!TrustZoneEnabled)
            {
                this.Log(LogLevel.Warning, "Tried to write to {0} (value: 0x{1:X}) in CPU without TrustZone implemented, write ignored", registerName, value);
                return;
            }
            setter(value);
        }

        [Export]
        private uint HasEnabledTrustZone()
        {
            return TrustZoneEnabled ? 1u : 0u;
        }

        [Export]
        private void SetPendingIRQ(int number)
        {
            nvic.SetPendingIRQ(number);
        }

        [Export]
        private int AcknowledgeIRQ()
        {
            var result = nvic.AcknowledgeIRQ();
            return result;
        }

        [Export]
        private void CompleteIRQ(int number)
        {
            nvic.CompleteIRQ(number);
        }

        [Export]
        private void OnBASEPRIWrite(int value, uint secure)
        {
            if(secure > 0)
            {
                nvic.BASEPRI_S = (byte)value;
            }
            else
            {
                nvic.BASEPRI_NS = (byte)value;
            }
        }

        [Export]
        private int FindPendingIRQ()
        {
            return nvic != null ? nvic.FindPendingInterrupt() : -1;
        }

        [Export]
        private int PendingMaskedIRQ()
        {
            return nvic.MaskedInterruptPresent ? 1 : 0;
        }

        [Export]
        private uint InterruptTargetsSecure(int interruptNumber)
        {
            return nvic.GetTargetInterruptSecurityState(interruptNumber) == NVIC.InterruptTargetSecurityState.Secure ? 1u : 0u;
        }

        [Export]
        private int CustomIdauHandler(IntPtr request, IntPtr region, IntPtr attribution)
        {
            if(idau == null)
            {
                return 0;
            }

            var parsedRequest = (ExternalIDAURequest)Marshal.PtrToStructure(request, typeof(ExternalIDAURequest));
            var attributionFound = idau.AttributionCheckCallback(parsedRequest.Address, parsedRequest.Secure != 0, (AccessType)parsedRequest.AccessType, parsedRequest.AccessWidth, out var reg, out var attrib);
            if(attributionFound)
            {
                Marshal.WriteInt32(region, reg);
                Marshal.WriteInt32(attribution, (int)attrib);
            }
            return attributionFound ? 1 : 0;
        }

        public const uint IDAU_SAURegionAddressMask = ~(IDAU_SAURegionMinSize - 1u);
        public const uint IDAU_SAURegionMinSize = 32u;

        private NVIC nvic;
        private CortexMImplementationDefinedAttributionUnit idau;
        private bool pcNotInitialized = true;
        private bool vtorInitialized;

        // Keep in line with ExternalIDAURequest struct in tlib's arm/arch_callbacks.h
        [StructLayout(LayoutKind.Sequential)]
        private struct ExternalIDAURequest
        {
            public uint Address;
            public int Secure;
            public int AccessType;
            public int AccessWidth;
        }

        private static readonly IReadOnlyDictionary<string, int> stateBits = new Dictionary<string, int>
        {
            ["privileged"] = 0,
            ["cpuSecure"] = 1,
            ["attributionSecure"] = 2,
        };

        public bool TryConvertStateObjToUlong(IContextState stateObj, out ulong? state)
        {
            state = null;
            if((stateObj == null) || !(stateObj is ContextState cortexMStateObj))
            {
                return false;
            }
            state = 0u;
            state |= (cortexMStateObj.Privileged ? 1u : 0) & 1u;
            state |= (cortexMStateObj.CpuSecure ? 2u : 0) & 2u;
            state |= (cortexMStateObj.AttributionSecure ? 4u : 0) & 4u;
            return true;
        }

        public bool TryConvertUlongToStateObj(ulong? state, out IContextState stateObj)
        {
            stateObj = null;
            if(!state.HasValue)
            {
                return false;
            }
            var cortexMStateObj = new ContextState
            {
                Privileged = (state & 1u) == 1u,
                CpuSecure = (state & 2u) == 2u,
                AttributionSecure = (state & 4u) == 4u
            };
            stateObj = cortexMStateObj;
            return true;
        }

        public struct IDAURegion
        {
            public static bool IsBaseAddressValid(uint address)
            {
                return address == (address & IDAU_SAURegionAddressMask);
            }

            public static bool IsLimitAddressValid(uint address)
            {
                return address == (address | ~IDAU_SAURegionAddressMask);
            }

            public IDAURegion(uint baseAddress, uint limitAddress, bool enabled, bool nonSecureCallable)
            {
                if(!IsBaseAddressValid(baseAddress) || !IsLimitAddressValid(limitAddress))
                {
                    throw new RecoverableException($"IDAU region must be {IDAU_SAURegionMinSize}B-aligned and the limit is inclusive, "
                        + $"e.g. 0x20-0x7F (limit=0x80 isn't valid); was: 0x{baseAddress:X8}-0x{limitAddress:X8}");
                }
                BaseAddress = baseAddress;
                LimitAddress = limitAddress;
                Enabled = enabled;
                NonSecureCallable = nonSecureCallable;
            }

            // tlib's IDAU configuration is currently similar to SAU. There are indexed regions for which registers contain
            // base/limit addresses at bits 5-31 and the remaining bits might be some flags. Currently only used in RLAR.
            public IDAURegion(uint rbar, uint rlar)
            {
                BaseAddress = rbar & IDAU_SAURegionAddressMask;
                LimitAddress = rlar | ~IDAU_SAURegionAddressMask;
                Enabled = (rlar & IDAURlarEnabledFlag) == IDAURlarEnabledFlag;
                NonSecureCallable = (rlar & IDAURlarNonSecureCallableFlag) == IDAURlarNonSecureCallableFlag;
            }

            public uint ToRBAR()
            {
                return BaseAddress;
            }

            public uint ToRLAR()
            {
                uint rlar = LimitAddress & IDAU_SAURegionAddressMask;
                rlar |= Enabled ? IDAURlarEnabledFlag : 0u;
                rlar |= NonSecureCallable ? IDAURlarNonSecureCallableFlag : 0u;
                return rlar;
            }

            public override string ToString()
            {
                return $"{nameof(IDAURegion)} [\n"
                    +$"  Enabled: {Enabled}\n"
                    +$"  From: 0x{BaseAddress:x}, To: 0x{LimitAddress:x}\n"
                    +$"  Is Non-secure Callable: {NonSecureCallable}\n"
                    + "]";
            }

            // The struct is intentionally immutable so that nobody tries to just modify the struct and call it a day
            // without passing the modified region to tlib.
            public uint BaseAddress { get; private set; }
            public bool Enabled { get; private set; }
            public uint LimitAddress { get; private set; }
            public bool NonSecureCallable { get; private set; }

            private const uint IDAURlarEnabledFlag = 1u << 0;
            private const uint IDAURlarNonSecureCallableFlag = 1u << 1;
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import]
        private Action<int> tlibToggleFpu;

        [Import]
        private Func<uint, uint> tlibGetFaultStatus;

        [Import]
        private Action<uint, uint> tlibSetFaultStatus;

        [Import]
        private Func<uint, uint> tlibGetMemoryFaultAddress;

        [Import]
        private Func<uint> tlibGetSecureFaultAddress;

        [Import]
        private Func<uint> tlibGetSecureFaultStatus;

        [Import]
        private Action<uint> tlibSetSecureFaultStatus;

        [Import]
        private Action<int> tlibEnableMpu;

        [Import]
        private Func<int> tlibIsMpuEnabled;

        [Import]
        private Action<uint> tlibSetMpuRegionBaseAddress;

        [Import]
        private Func<uint> tlibGetMpuRegionBaseAddress;

        [Import]
        private Action<uint> tlibSetMpuRegionSizeAndEnable;

        [Import]
        private Func<uint> tlibGetMpuRegionSizeAndEnable;

        [Import]
        private Action<uint> tlibSetMpuRegionNumber;

        [Import]
        private Func<uint> tlibGetMpuRegionNumber;

        [Import]
        private Action<int> tlibSetFpuInterruptNumber;

        [Import]
        private Func<uint, uint> tlibGetInterruptVectorBase;

        [Import]
        private Action<uint, uint> tlibSetInterruptVectorBase;

        [Import]
        private Func<uint> tlibGetXpsr;

        [Import]
        private Action<int> tlibSetSleepOnExceptionExit;

        [Import]
        private Func<uint, uint> tlibGetPrimask;

        [Import]
        private Func<uint, uint> tlibGetFaultmask;

        [Import]
        private Func<uint> tlibIsV8;

        /* TrustZone */
        [Import]
        private Action<uint> tlibSetSecurityState;

        [Import]
        private Func<uint> tlibGetSecurityState;

        [Import(Name = "tlib_set_register_value_32_non_secure")]
        private Action<int, uint> SetRegisterValue32NonSecure;

        [Import(Name = "tlib_get_register_value_32_non_secure")]
        private Func<int, uint> GetRegisterValue32NonSecure;

        /* TrustZone IDAU */
        [Import]
        private Action<uint> tlibSetNumberOfIdauRegions;

        [Import]
        private Func<uint> tlibGetNumberOfIdauRegions;

        [Import]
        private Action<uint> tlibSetIdauEnabled;

        [Import]
        private Func<uint> tlibGetIdauEnabled;

        [Import]
        private Action<uint, uint> tlibSetIdauRegionBaseAddressRegister;

        [Import]
        private Action<uint> tlibSetCustomIdauHandlerEnabled;

        [Import]
        private Func<uint, uint> tlibGetIdauRegionBaseAddressRegister;

        [Import]
        private Action<uint, uint> tlibSetIdauRegionLimitAddressRegister;

        [Import]
        private Func<uint, uint> tlibGetIdauRegionLimitAddressRegister;

        [Import]
        private Func<uint, uint, uint> tlibTryAddImplementationDefinedExemptionRegion;

        [Import]
        private Func<uint, uint, uint> tlibTryRemoveImplementationDefinedExemptionRegion;

        /* TrustZone SAU */
        [Import]
        private Action<uint> tlibSetNumberOfSauRegions;

        [Import]
        private Func<uint> tlibGetNumberOfSauRegions;

        [Import]
        private Action<uint> tlibSetSauControl;

        [Import]
        private Func<uint> tlibGetSauControl;

        [Import]
        private Action<uint> tlibSetSauRegionNumber;

        [Import]
        private Func<uint> tlibGetSauRegionNumber;

        [Import]
        private Action<uint> tlibSetSauRegionBaseAddress;

        [Import]
        private Func<uint> tlibGetSauRegionBaseAddress;

        [Import]
        private Action<uint> tlibSetSauRegionLimitAddress;

        [Import]
        private Func<uint> tlibGetSauRegionLimitAddress;

        /* PMSAv8 MPU */
        [Import]
        private Action<uint> tlibSetPmsav8Ctrl;

        [Import]
        private Action<uint> tlibSetPmsav8Rnr;

        [Import]
        private Action<uint> tlibSetPmsav8Rbar;

        [Import]
        private Action<uint> tlibSetPmsav8Rlar;

        [Import]
        private Action<uint, uint> tlibSetPmsav8Mair;

        [Import]
        private Func<uint> tlibGetPmsav8Ctrl;

        [Import]
        private Func<uint> tlibGetPmsav8Rnr;

        [Import]
        private Func<uint> tlibGetPmsav8Rbar;

        [Import]
        private Func<uint> tlibGetPmsav8Rlar;

        [Import]
        private Func<uint, uint> tlibGetPmsav8Mair;

        #pragma warning restore 649
    }
}

