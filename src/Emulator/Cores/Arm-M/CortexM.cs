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
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Utilities.Binding;

using ELFSharp.ELF;
using ELFSharp.UImage;

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
                // Free unmanaged resources allocated by the base class constructor
                Dispose();
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
                // Rethrow attachment error as ConstructionException, so the CreationDriver doesn't crash. Also
                // free unmanaged resources allocated by the base class constructor
                Dispose();
                throw new ConstructionException("Exception occurred when attaching NVIC: ", e);
            }
        }

        public override void Reset()
        {
            pcNotInitialized = true;
            vtorInitialized = false;
            base.Reset();
        }

        public override void InitFromElf(IELF elf)
        {
            // do nothing
        }

        public override void InitFromUImage(UImage uImage)
        {
            // do nothing
        }

        public void SetSleepOnExceptionExit(bool value)
        {
            tlibSetSleepOnExceptionExit(value ? 1 : 0);
        }

        public uint GetFaultmask(bool secure)
        {
            return tlibGetFaultmask(secure ? 1u : 0u);
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

        /// <remark>Should only be used for TrustZone CPUs, <see cref="RecoverableException"/> is thrown otherwise.</remark>
        public bool TryRemoveImplementationDefinedExemptionRegion(uint startAddress, uint endAddress)
        {
            AssertTrustZoneEnabled($"remove implementation-defined exemption region 0x{startAddress:X8}-0x{endAddress:X8}");

            return tlibTryRemoveImplementationDefinedExemptionRegion(startAddress, endAddress) == 1u;
        }

        /// <remark>Should only be used for TrustZone CPUs, <see cref="RecoverableException"/> is thrown otherwise.</remark>
        public bool TryAddImplementationDefinedExemptionRegion(uint startAddress, uint endAddress)
        {
            AssertTrustZoneEnabled($"add implementation-defined exemption region 0x{startAddress:X8}-0x{endAddress:X8}");

            return tlibTryAddImplementationDefinedExemptionRegion(startAddress, endAddress) == 1u;
        }

        public uint GetPrimask(bool secure)
        {
            return tlibGetPrimask(secure ? 1u : 0u);
        }

        public void SetIDAURegion(uint regionIndex, uint baseAddress, uint limitAddress, bool enabled, bool nonSecureCallable)
        {
            SetIDAURegion(regionIndex, new IDAURegion(baseAddress, limitAddress, enabled, nonSecureCallable));
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

        /// <remark>Should only be used for TrustZone CPUs, <see cref="RecoverableException"/> is thrown otherwise.</remark>
        public IDAURegion GetIDAURegion(uint regionIndex)
        {
            AssertTrustZoneEnabled($"get IDAU region {regionIndex}");

            var rbar = tlibGetIdauRegionBaseAddressRegister(regionIndex);
            var rlar = tlibGetIdauRegionLimitAddressRegister(regionIndex);
            return new IDAURegion(rbar, rlar);
        }

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

        public void SetIDAURegion(uint regionIndex, uint rbar, uint rlar)
        {
            SetIDAURegion(regionIndex, new IDAURegion(rbar, rlar));
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

                // +++++ Important
                // tlibs implement VFP using DOUBLE PRECISION FP (64 bit) registers.
                // Each D (double) register holds two F (fp) registers! (arm/cpu.h)
                // To keep consistency with tlibs, the code below uses D registers.

                bool has32RegisterVfp = (GetArmFeature(ArmFeatures.ARM_FEATURE_VFP3)
                        || GetArmFeature(ArmFeatures.ARM_FEATURE_NEON)
                        || GetArmFeature(ArmFeatures.ARM_FEATURE_VFP4));

                bool has16RegisterVfp = ((GetArmFeature(ArmFeatures.ARM_FEATURE_VFP_FP16)
                        || GetArmFeature(ArmFeatures.ARM_FEATURE_VFP))
                        && !has32RegisterVfp);

                if(has32RegisterVfp || has16RegisterVfp)
                {
                    var mVfpFeature = new GDBFeatureDescriptor("org.gnu.gdb.arm.vfp");
                    const int vfpRegisterOffset = 42;
                    int vfpRegisterEnd = has32RegisterVfp ? 31 : 15;
                    for(var reg = 0u; reg <= vfpRegisterEnd; reg++)
                    {
                        mVfpFeature.Registers.Add(new GDBRegisterDescriptor(reg + vfpRegisterOffset, 64, $"d{reg}", "ieee_double", "float"));
                    }
                    // The fpscr is always reg# = 74
                    mVfpFeature.Registers.Add(new GDBRegisterDescriptor(74, 32, "fpscr", "int", "float"));
                    features.Add(mVfpFeature);
                }

                return features;
            }
        }

        public override MemorySystemArchitectureType MemorySystemArchitecture => NumberOfMPURegions > 0 ? MemorySystemArchitectureType.Physical_PMSA : MemorySystemArchitectureType.None;

        public override uint ExceptionVectorAddress
        {
            get => VectorTableOffset;
            set => VectorTableOffset = value;
        }

        public UInt32 PmsaV8Mair0
        {
            get
            {
                return tlibGetPmsav8Mair(0, ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Mair(0, value, ShouldAccessBeSecure());
            }
        }

        public UInt32 PmsaV8RbarAlias3_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Rbar_NS), () => tlibGetPmsav8Rbar(3, 0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Rbar_NS), val => tlibSetPmsav8Rbar(val, 3, 0), value);
        }

        public UInt32 PmsaV8Rlar
        {
            get
            {
                return tlibGetPmsav8Rlar(0, ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Rlar(value, 0, ShouldAccessBeSecure());
            }
        }

        public UInt32 PmsaV8RlarAlias1
        {
            get
            {
                return tlibGetPmsav8Rlar(1, ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Rlar(value, 1, ShouldAccessBeSecure());
            }
        }

        public UInt32 PmsaV8RlarAlias2
        {
            get
            {
                return tlibGetPmsav8Rlar(2, ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Rlar(value, 2, ShouldAccessBeSecure());
            }
        }

        public UInt32 PmsaV8RlarAlias3
        {
            get
            {
                return tlibGetPmsav8Rlar(3, ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Rlar(value, 3, ShouldAccessBeSecure());
            }
        }

        public UInt32 PmsaV8Rlar_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Rlar_NS), () => tlibGetPmsav8Rlar(0, 0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Rlar_NS), val => tlibSetPmsav8Rlar(val, 0, 0), value);
        }

        public UInt32 PmsaV8RlarAlias1_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Rlar_NS), () => tlibGetPmsav8Rlar(1, 0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Rlar_NS), val => tlibSetPmsav8Rlar(val, 1, 0), value);
        }

        public UInt32 PmsaV8RlarAlias2_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Rlar_NS), () => tlibGetPmsav8Rlar(2, 0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Rlar_NS), val => tlibSetPmsav8Rlar(val, 2, 0), value);
        }

        public UInt32 PmsaV8RlarAlias3_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Rlar_NS), () => tlibGetPmsav8Rlar(3, 0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Rlar_NS), val => tlibSetPmsav8Rlar(val, 3, 0), value);
        }

        public UInt32 PmsaV8Mair0_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Mair0_NS), () => tlibGetPmsav8Mair(0, 0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Mair0_NS), val => tlibSetPmsav8Mair(0, val, 0), value);
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

        public UInt32 PmsaV8Mair1_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Mair1_NS), () => tlibGetPmsav8Mair(1, 0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Mair1_NS), val => tlibSetPmsav8Mair(1, val, 0), value);
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

        public UInt32 PmsaV8RbarAlias2_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Rbar_NS), () => tlibGetPmsav8Rbar(2, 0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Rbar_NS), val => tlibSetPmsav8Rbar(val, 2, 0), value);
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

        public UInt32 PmsaV8Mair1
        {
            get
            {
                return tlibGetPmsav8Mair(1, ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Mair(1, value, ShouldAccessBeSecure());
            }
        }

        public UInt32 PmsaV8RbarAlias1_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Rbar_NS), () => tlibGetPmsav8Rbar(1, 0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Rbar_NS), val => tlibSetPmsav8Rbar(val, 1, 0), value);
        }

        public UInt32 PmsaV8Rbar_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Rbar_NS), () => tlibGetPmsav8Rbar(0, 0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Rbar_NS), val => tlibSetPmsav8Rbar(val, 0, 0), value);
        }

        public UInt32 PmsaV8RbarAlias3
        {
            get
            {
                return tlibGetPmsav8Rbar(3, ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Rbar(value, 3, ShouldAccessBeSecure());
            }
        }

        public IReadOnlyDictionary<string, int> StateBits { get { return stateBits; } }

        // Sets VTOR for the current Security State the CPU is in right now
        public uint VectorTableOffset
        {
            get
            {
                return tlibGetInterruptVectorBase(ShouldAccessBeSecure());
            }

            set
            {
                vtorInitialized = true;
                if(!machine.SystemBus.IsMemory(value, this))
                {
                    this.Log(LogLevel.Warning, "Tried to set VTOR address at 0x{0:X} which does not lay in memory. Aborted.", value);
                    return;
                }
                this.NoisyLog("VectorTableOffset set to 0x{0:X}.", value);
                tlibSetInterruptVectorBase(value, ShouldAccessBeSecure());
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
        public RegisterValue FPSCR_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(FPSCR_NS), () => GetRegisterValue32NonSecure((int)CortexMRegisters.FPSCR));
            set => SetTrustZoneRelatedRegister(nameof(FPSCR_NS), val => SetRegisterValue32NonSecure((int)CortexMRegisters.FPSCR, val), value);
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
                return tlibGetFaultStatus(ShouldAccessBeSecure());
            }

            set
            {
                tlibSetFaultStatus(value, ShouldAccessBeSecure());
            }
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

        public UInt32 MemoryFaultAddress
        {
            get
            {
                return tlibGetMemoryFaultAddress(ShouldAccessBeSecure());
            }
        }

        public UInt32 PmsaV8RbarAlias2
        {
            get
            {
                return tlibGetPmsav8Rbar(2, ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Rbar(value, 2, ShouldAccessBeSecure());
            }
        }

        public UInt32 PmsaV8RbarAlias1
        {
            get
            {
                return tlibGetPmsav8Rbar(1, ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Rbar(value, 1, ShouldAccessBeSecure());
            }
        }

        public UInt32 PmsaV8Rbar
        {
            get
            {
                return tlibGetPmsav8Rbar(0, ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Rbar(value, 0, ShouldAccessBeSecure());
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

        public UInt32 PmsaV8Rnr
        {
            get
            {
                return tlibGetPmsav8Rnr(ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Rnr(value, ShouldAccessBeSecure());
            }
        }

        public UInt32 PmsaV8Ctrl_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Ctrl_NS), () => tlibGetPmsav8Ctrl(0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Ctrl_NS), val => tlibSetPmsav8Ctrl(val, 0), value);
        }

        public UInt32 PmsaV8Rnr_NS
        {
            get => GetTrustZoneRelatedRegister(nameof(PmsaV8Rnr_NS), () => tlibGetPmsav8Rnr(0));
            set => SetTrustZoneRelatedRegister(nameof(PmsaV8Rnr_NS), val => tlibSetPmsav8Rnr(val, 0), value);
        }

        public bool IsV8
        {
            get
            {
                return tlibIsV8() > 0;
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

        public UInt32 SecureFaultAddress
        {
            get
            {
                AssertTrustZoneEnabled("get SecureFaultAddress");
                return tlibGetSecureFaultAddress();
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

        public UInt32 PmsaV8Ctrl
        {
            get
            {
                return tlibGetPmsav8Ctrl(ShouldAccessBeSecure());
            }

            set
            {
                tlibSetPmsav8Ctrl(value, ShouldAccessBeSecure());
            }
        }

        public const uint IDAU_SAURegionMinSize = 32u;

        public const uint IDAU_SAURegionAddressMask = ~(IDAU_SAURegionMinSize - 1u);

        protected override void OnResume()
        {
            // Suppress initialization when processor is turned off as binary may not even be loaded yet
            if(!IsHalted)
            {
                InitPCAndSP();
            }
            base.OnResume();
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

        [Export]
        private int FindPendingIRQ()
        {
            return nvic != null ? nvic.FindPendingInterrupt() : -1;
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

        [Export]
        private uint InterruptTargetsSecure(int interruptNumber)
        {
            return nvic.GetTargetInterruptSecurityState(interruptNumber) == NVIC.InterruptTargetSecurityState.Secure ? 1u : 0u;
        }

        [Export]
        private int PendingMaskedIRQ()
        {
            return nvic.MaskedInterruptPresent ? 1 : 0;
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
        private void CompleteIRQ(int number)
        {
            nvic.CompleteIRQ(number);
        }

        [Export]
        private int AcknowledgeIRQ()
        {
            var result = nvic.AcknowledgeIRQ();
            return result;
        }

        private uint ShouldAccessBeSecure()
        {
            var secure = 0u;
            if(TrustZoneEnabled)
            {
                secure = SecureState ? 1u : 0u;
            }
            return secure;
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

        /// <remarks>Use <see cref="GetTrustZoneRelatedRegister"/> and <see cref="SetTrustZoneRelatedRegister"/> to wrap accesses which have to succeed.</remarks>
        private void AssertTrustZoneEnabled(string actionName)
        {
            if(!TrustZoneEnabled)
            {
                throw new RecoverableException($"Tried to {actionName} in CPU with TrustZone disabled");
            }
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

        private void SetTrustZoneRelatedRegister(string registerName, Action<uint> setter, uint value)
        {
            if(!TrustZoneEnabled)
            {
                this.Log(LogLevel.Warning, "Tried to write to {0} (value: 0x{1:X}) in CPU without TrustZone implemented, write ignored", registerName, value);
                return;
            }
            setter(value);
        }

        private CortexMImplementationDefinedAttributionUnit idau;
        private bool pcNotInitialized = true;
        private bool vtorInitialized;

#pragma warning disable 649
        // 649:  Field '...' is never assigned to, and will always have its default value null
        [Import]
        private readonly Func<uint> tlibGetNumberOfSauRegions;

        /* TrustZone SAU */
        [Import]
        private readonly Action<uint> tlibSetNumberOfSauRegions;

        [Import]
        private readonly Func<uint, uint, uint> tlibTryRemoveImplementationDefinedExemptionRegion;

        [Import]
        private readonly Func<uint, uint, uint> tlibTryAddImplementationDefinedExemptionRegion;

        [Import]
        private readonly Func<uint, uint> tlibGetIdauRegionLimitAddressRegister;

        [Import]
        private readonly Action<uint, uint> tlibSetIdauRegionLimitAddressRegister;

        [Import]
        private readonly Func<uint, uint> tlibGetIdauRegionBaseAddressRegister;

        [Import]
        private readonly Action<uint> tlibSetSauControl;

        [Import]
        private readonly Action<uint, uint> tlibSetIdauRegionBaseAddressRegister;

        [Import]
        private readonly Func<uint> tlibGetIdauEnabled;

        [Import]
        private readonly Action<uint> tlibSetIdauEnabled;

        [Import]
        private readonly Func<uint> tlibGetNumberOfIdauRegions;

        [Import]
        private readonly Action<uint> tlibSetCustomIdauHandlerEnabled;

        [Import]
        private readonly Func<uint> tlibGetSauControl;

        [Import]
        private readonly Func<uint> tlibGetSauRegionBaseAddress;

        [Import]
        private readonly Func<uint> tlibGetSauRegionNumber;

        [Import]
        private readonly Action<uint> tlibSetSauRegionBaseAddress;

        /* TrustZone IDAU */
        [Import]
        private readonly Action<uint> tlibSetNumberOfIdauRegions;

        [Import]
        private readonly Action<uint> tlibSetSauRegionLimitAddress;

        [Import]
        private readonly Func<uint> tlibGetSauRegionLimitAddress;

        /* PMSAv8 MPU */
        [Import]
        private readonly Action<uint, uint> tlibSetPmsav8Ctrl;

        [Import]
        private readonly Action<uint, uint> tlibSetPmsav8Rnr;

        [Import]
        private readonly Action<uint, uint, uint> tlibSetPmsav8Rbar;

        [Import]
        private readonly Action<uint, uint, uint> tlibSetPmsav8Rlar;

        [Import]
        private readonly Action<uint, uint, uint> tlibSetPmsav8Mair;

        [Import]
        private readonly Func<uint, uint> tlibGetPmsav8Ctrl;

        [Import]
        private readonly Func<uint, uint> tlibGetPmsav8Rnr;

        [Import]
        private readonly Func<uint, uint, uint> tlibGetPmsav8Rbar;

        [Import]
        private readonly Action<uint> tlibSetSauRegionNumber;

        [Import(Name = "tlib_get_register_value_32_non_secure")]
        private readonly Func<int, uint> GetRegisterValue32NonSecure;

        [Import]
        private readonly Func<uint> tlibIsV8;

        [Import]
        private readonly Func<uint> tlibGetSecurityState;

        [Import]
        private readonly Action<int> tlibToggleFpu;

        [Import]
        private readonly Func<uint, uint> tlibGetFaultStatus;

        [Import]
        private readonly Action<uint, uint> tlibSetFaultStatus;

        [Import]
        private readonly Func<uint, uint> tlibGetMemoryFaultAddress;

        [Import]
        private readonly Func<uint> tlibGetSecureFaultAddress;

        [Import]
        private readonly Func<uint> tlibGetSecureFaultStatus;

        [Import]
        private readonly Action<uint> tlibSetSecureFaultStatus;

        [Import]
        private readonly Action<int> tlibEnableMpu;

        [Import]
        private readonly Func<int> tlibIsMpuEnabled;

        [Import(Name = "tlib_set_register_value_32_non_secure")]
        private readonly Action<int, uint> SetRegisterValue32NonSecure;

        [Import]
        private readonly Action<uint> tlibSetMpuRegionBaseAddress;

        [Import]
        private readonly Action<uint> tlibSetMpuRegionSizeAndEnable;

        [Import]
        private readonly Func<uint> tlibGetMpuRegionSizeAndEnable;

        [Import]
        private readonly Action<uint> tlibSetMpuRegionNumber;

        [Import]
        private readonly Func<uint> tlibGetMpuRegionNumber;

        [Import]
        private readonly Action<int> tlibSetFpuInterruptNumber;

        [Import]
        private readonly Func<uint, uint> tlibGetInterruptVectorBase;

        [Import]
        private readonly Action<uint, uint> tlibSetInterruptVectorBase;

        [Import]
        private readonly Func<uint> tlibGetXpsr;

        [Import]
        private readonly Action<int> tlibSetSleepOnExceptionExit;

        [Import]
        private readonly Func<uint, uint> tlibGetPrimask;

        [Import]
        private readonly Func<uint, uint> tlibGetFaultmask;

        [Import]
        private readonly Func<uint, uint, uint> tlibGetPmsav8Rlar;

        /* TrustZone */
        [Import]
        private readonly Action<uint> tlibSetSecurityState;

        [Import]
        private readonly Func<uint> tlibGetMpuRegionBaseAddress;

        [Import]
        private readonly Func<uint, uint, uint> tlibGetPmsav8Mair;
#pragma warning restore 649

        private readonly NVIC nvic;

        public class ContextState : IContextState
        {
            public bool Privileged;
            public bool CpuSecure;
            public bool AttributionSecure;
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
                    + $"  Enabled: {Enabled}\n"
                    + $"  From: 0x{BaseAddress:x}, To: 0x{LimitAddress:x}\n"
                    + $"  Is Non-secure Callable: {NonSecureCallable}\n"
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
    }
}