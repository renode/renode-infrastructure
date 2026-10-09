//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Peripherals.PCI.Capabilities;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.PCI;

// Low part of DBI space can be accessed by byte or word length reads (containing PCIe header)
[AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
// Ideally, we'd inherit from `PCIeRootComplex`, but this peripheral can also function in the endpoint mode
public class DesignWare_PCIe : PCIeBasePeripheral, IPCIeRouter, IDoubleWordPeripheral, IHasMappedRegisters, IAbsoluteAddressAware, IPeripheralContainer<PCIeBasePeripheral, NumberRegistrationPoint<int>>
{
    public DesignWare_PCIe(IMachine machine, uint iATUInboundRegions = 8, uint iATUOutboundRegions = 8, uint iATUAlignmentBits = 12, uint iATUHighLimitBits = 8, bool isEndpoint = false, bool hasVendorStubs = false)
        : base(isEndpoint ? HeaderType.Endpoint : HeaderType.Bridge)
    {
        if(isEndpoint)
        {
            throw new ConstructionException("Endpoint mode is not yet supported");
        }

        if(iATUHighLimitBits > 31)
        {
            throw new ConstructionException($"UpperLimit address is too high: {iATUHighLimitBits}, needs to be less than 32 bits");
        }
        if(iATUAlignmentBits == 0)
        {
            throw new ConstructionException("Alignment has to be non-zero");
        }

        this.machine = machine;
        // If unimplemented registers behave as Write-Readback
        this.hasVendorStubs = hasVendorStubs;

        iatuRegistersCollection = new(this);
        dbiRegistersCollection = new(this);
        registeredPCIePeripherals = new();

        dbiRegisterMapper = new(this.GetType(), DbiRegion);
        // Add PCIe registers
        dbiRegisterMapper.RegisterEnumMapping(typeof(RegistersCommon));
        dbiRegisterMapper.RegisterEnumMapping(typeof(RegistersBridge));
        dbiRegisterMapper.RegisterEnumMapping(typeof(PCIeCapability.PCIeCapabilityRegisters), PCIeCapabilitiesOffset);

        iAtuRegisterMapper = new(this.GetType(), AtuRegion);
        mapperProvider = new(dbiRegisterMapper);

        deviceRegisterMapper = new(this.GetType(), DevicesRegion);

        inboundATUs = new InternalAddressTranslationUnit[iATUInboundRegions];
        outboundATUs = new InternalAddressTranslationUnit[iATUOutboundRegions];
        barContainer = new();

        BaseClassCode = PCIBaseClass.Bridge;
        SubClassCode = PCISubClass.BridgePCI;
        ProgrammingInterface = 0x0;
        VendorId = 0x16C3; // Synopsys
        DeviceId = 0xC0DE; // Dummy ID (public ID not known)

        SetupPCIeSpace();
        DefineRegisters();
        DefineATUs(iATUInboundRegions, iATUOutboundRegions, iATUAlignmentBits, iATUHighLimitBits);
        Reset();
    }

    public string[,] PrintBars()
    {
        var table = new Table();
        table.AddRow("Number", "Range", "Peripheral");
        table.AddRows(barContainer,
            k => k.BarNumber.ToString(),
            k => k.BarRange.ToString(),
            k => k.TargetPeripheral.GetName()
        );
        return table.ToArray();
    }

    public void SetAbsoluteAddress(ulong address)
    {
        currentAccessAbsoluteAddress = address;
    }

    public override void Reset()
    {
        base.Reset();
        vendorRegisters.Clear();
        currentAccessAbsoluteAddress = 0;
        iatuRegistersCollection.Reset();
        dbiRegistersCollection.Reset();
        foreach(var item in inboundATUs.Concat(outboundATUs))
        {
            item.Reset();
        }
        LinkInTraining = false;
        LinkIsUp = true;
        LinkStatus = LinkStateL0;
    }

    public IEnumerable<NumberRegistrationPoint<int>> GetRegistrationPoints(PCIeBasePeripheral peripheral)
    {
        return registeredPCIePeripherals.Keys.Select(e => new NumberRegistrationPoint<int>(e));
    }

    public void Register(PCIeBasePeripheral peripheral, NumberRegistrationPoint<int> registrationPoint)
    {
        if(!registeredPCIePeripherals.TryAdd(registrationPoint.Address, peripheral))
        {
            throw new RegistrationException($"The specified registration point: {registrationPoint} is already in use.");
        }
        machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
    }

    public void Unregister(PCIeBasePeripheral peripheral)
    {
        var toRemove = registeredPCIePeripherals.Where(x => x.Value.Equals(peripheral)).Select(x => x.Key).ToList();
        if(toRemove.Count == 0)
        {
            throw new RegistrationException($"The specified peripheral: {peripheral} was never registered.");
        }
        foreach(var key in toRemove)
        {
            registeredPCIePeripherals.Remove(key);
        }
        machine.UnregisterAsAChildOf(this, peripheral);
    }

    public override string OffsetToString(long offset) => mapperProvider.CurrentMapper?.ToString(offset) ?? "<undefined>";

    public RegisterMapper GetRegisterMapperForRegion(string tag) => tag switch
    {
        AtuRegion => iAtuRegisterMapper,
        DbiRegion => dbiRegisterMapper,
        DevicesRegion => deviceRegisterMapper,
        _ => null,
    };

    // Unhandled access to iATU is not particularly worrisome,
    // since the SW uses this method to check the number of available iATUs
    [ConnectionRegion(AtuRegion)]
    public void WriteDoubleWordIATU(long offset, uint value)
    {
        if(!iatuRegistersCollection.TryWrite(offset, value))
        {
            this.Log(LogLevel.Debug, "Unhandled write to iATU region, offset 0x{0:X}, value 0x{1:X}.", offset, value);
        }
    }

    [ConnectionRegion(AtuRegion)]
    public uint ReadDoubleWordIATU(long offset)
    {
        if(!iatuRegistersCollection.TryRead(offset, out var ret))
        {
            this.Log(LogLevel.Debug, "Unhandled read from iATU region, offset 0x{0:X}.", offset);
        }
        return ret;
    }

    [ConnectionRegion(DbiRegion)]
    public void WriteDoubleWord(long offset, uint value)
    {
        using var mapper = mapperProvider.WithCurrent(dbiRegisterMapper);
        if(offset < DBIRegionPCIeBoundary)
        {
            ConfigurationWriteDoubleWord(offset, value);
        }
        else
        {
            if(!dbiIsWriteUnlocked.Value && (offset != (long)RegistersDBI.PCIE_MISC_CONTROL_1_OFF))
            {
                this.Log(LogLevel.Error, "Tried to write 0x{0:X} to DBI region at 0x{1:X} ({2}) but writing to DBI region is locked",
                    value, offset, dbiRegisterMapper.ToString(offset));
                return;
            }
            if(!dbiRegistersCollection.TryWrite(offset, value))
            {
                if(hasVendorStubs)
                {
                    vendorRegisters[offset] = value;
                }
                else
                {
                    this.LogUnhandledWrite(offset, Misc.CastToULong(value));
                }
            }
        }
    }

    [ConnectionRegion(DbiRegion)]
    public uint ReadDoubleWord(long offset)
    {
        using var mapper = mapperProvider.WithCurrent(dbiRegisterMapper);
        if(offset < DBIRegionPCIeBoundary)
        {
            return ConfigurationReadDoubleWord(offset);
        }
        else
        {
            uint result = 0;
            if(!dbiRegistersCollection.TryRead(offset, out result))
            {
                if(hasVendorStubs)
                {
                    result = vendorRegisters.GetOrDefault(offset);
                }
                else
                {
                    this.LogUnhandledRead(offset);
                }
            }
            return result;
        }
    }

    [ConnectionRegion(DevicesRegion)]
    public void WriteDoubleWordPCIeLink(long offset, uint value)
    {
        var region = FindMatchingOutboundATU(currentAccessAbsoluteAddress, value);
        if(region is null)
        {
            return;
        }

        if(region.TargetSpace == PCIeSpace.Configuration)
        {
            // The cast is safe
            if(!registeredPCIePeripherals.TryGetValue((int)region.Bus, out var peripheral))
            {
                this.Log(LogLevel.Warning, "No peripheral registered for iATU target: {0}, configured for 0x{1:X}, bus 0x{2:X}, write failed",
                    region.TargetSpace, currentAccessAbsoluteAddress, region.Bus);
                return;
            }
            peripheral.ConfigurationWriteDoubleWord(offset, value);
        }
        else if(region.TargetSpace == PCIeSpace.Memory)
        {
            var translatedAddress = region.Translate(currentAccessAbsoluteAddress);
            if(!barContainer.TryFindBar(translatedAddress, out var targetBarEntry))
            {
                this.Log(LogLevel.Warning, "Trying to write to a BAR at offset 0x{0:X}, translated 0x{1:X} (base: 0x{2:X}, target: 0x{3:X}), but nothing is registered there, value 0x{4:X}",
                    offset, translatedAddress, region.BaseAddress, region.LimitAddress, value);
                return;
            }
            this.Log(LogLevel.Noisy, "Write access at offset 0x{0:X}, matched with BAR: {1}", offset, targetBarEntry);
            targetBarEntry.TargetPeripheral.MemoryWriteDoubleWord(targetBarEntry.BarNumber, (long)(translatedAddress - targetBarEntry.BarRange.StartAddress), value);
        }
        else
        {
            this.Log(LogLevel.Error, "Unsupported iATU target: {0}, configured for 0x{1:X}", region.TargetSpace, currentAccessAbsoluteAddress);
            return;
        }
    }

    [ConnectionRegion(DevicesRegion)]
    public uint ReadDoubleWordPCIeLink(long offset)
    {
        var region = FindMatchingOutboundATU(currentAccessAbsoluteAddress, null);
        if(region is null)
        {
            return 0;
        }

        if(region.TargetSpace == PCIeSpace.Configuration)
        {
            // The cast is safe
            if(!registeredPCIePeripherals.TryGetValue((int)region.Bus, out var peripheral))
            {
                this.Log(LogLevel.Warning, "No peripheral registered for iATU target: {0}, configured for 0x{1:X}, bus 0x{2:X}, read failed",
                    region.TargetSpace, currentAccessAbsoluteAddress, region.Bus);
                return 0x0;
            }
            return peripheral.ConfigurationReadDoubleWord(offset);
        }
        else if(region.TargetSpace == PCIeSpace.Memory)
        {
            var translatedAddress = region.Translate(currentAccessAbsoluteAddress);
            if(!barContainer.TryFindBar(translatedAddress, out var targetBarEntry))
            {
                this.Log(LogLevel.Warning, "Trying to read from a BAR at offset 0x{0:X}, translated 0x{1:X} (base: 0x{2:X}, target: 0x{3:X}), but nothing is registered there, returning 0",
                    offset, translatedAddress, region.BaseAddress, region.LimitAddress);
                return 0;
            }
            this.Log(LogLevel.Noisy, "Read access at offset 0x{0:X}, matched with BAR: {1}", offset, targetBarEntry);
            return targetBarEntry.TargetPeripheral.MemoryReadDoubleWord(targetBarEntry.BarNumber, (long)(translatedAddress - targetBarEntry.BarRange.StartAddress));
        }
        else
        {
            this.Log(LogLevel.Error, "Unsupported iATU target: {0}, configured for 0x{1:X}", region.TargetSpace, currentAccessAbsoluteAddress);
            return 0;
        }
    }

    public void RegisterBar(Core.Range range, IPCIePeripheral peripheral, uint bar)
    {
        if(!barContainer.TryRegisterBar(range, peripheral, bar))
        {
            // This is a valid path, that is triggered by SW discovering BAR sizes
            this.Log(LogLevel.Debug, "Cannot register BAR: {0} for peripheral: {1} at range: {2}, because it overlaps with an existing BAR",
                bar, peripheral.GetName(), range);
        }
        else
        {
            this.Log(LogLevel.Debug, "Registered BAR: {0} for peripheral: {1} at range: {2}",
                bar, peripheral.GetName(), range);
        }
    }

    // By default, Link is up and pretrained (see: `Reset()` method)
    // For debug purposes it's possible to override this behavior before starting the simulation
    // e.g. when emulating custom PHY connection
    public bool LinkInTraining { get; set; }

    public bool LinkIsUp { get; set; }

    public byte LinkStatus { get; set; }

    public uint NegotiatedLinkWidth
    {
        set => PCIeCapabilityStructure.SetNegotiatedLinkWidth(value);
    }

    public PCIeCapability.PCIeLinkSpeeds CurrentLinkSpeed
    {
        set => PCIeCapabilityStructure.SetCurrentLinkSpeed(value);
    }

    // We support one specific version of the IP
    public VersionID Version
    {
        get => version;
        set
        {
            if(value != version)
            {
                this.Log(LogLevel.Info, "Changing the IP version currently only affects value provided in the {0} register, other than that it will still act as the 0x{1:X} version",
                    nameof(RegistersDBI.PCIE_VERSION_NUMBER), SupportedVersion);
            }
            version = value;
        }
    }

    // This value can be arbitrary, and is presumably set by the SoC designer/integrator
    public uint Type { get; set; }

    // Exposed, so the user can override e.g. supported link speed
    public PCIeCapability PCIeCapabilityStructure { get; private set; }

    public IEnumerable<IRegistered<PCIeBasePeripheral, NumberRegistrationPoint<int>>> Children => registeredPCIePeripherals.Select(x => Registered.Create(x.Value, new NumberRegistrationPoint<int>(x.Key)));

    public const VersionID SupportedVersion = VersionID.DW_PCIE_VER_562A;

    protected readonly Dictionary<long, uint> vendorRegisters = new();

    private InternalAddressTranslationUnit FindMatchingOutboundATU(ulong absoluteAddress, uint? value)
    {
        var matchedRegion = outboundATUs.Where(e => e.DoesAddressHit(absoluteAddress));
        if(matchedRegion.Count() > 1)
        {
            this.Log(LogLevel.Error, "Overlapping iATU regions found for 0x{0:X}, indices: {1}, when {2}",
                absoluteAddress, Misc.PrettyPrintCollectionHex(matchedRegion.Select(e => e.Idx)), value is not null ? "writing: 0x{value:X}" : "reading");
            return null;
        }

        var region = matchedRegion.SingleOrDefault();
        if(region is null)
        {
            this.Log(LogLevel.Warning, "No outbound and enabled iATU region configured for 0x{0:X}, when {1}",
                absoluteAddress, value is not null ? $"writing: 0x{value:X}" : "reading");
            return null;
        }
        return region;
    }

    private void SetupPCIeSpace()
    {
        PCIeCapabilityStructure = GetPCIeCapability();

        PCIeCapabilityStructure.SetSupportedLinkSpeeds(
            PCIeCapability.PCIeLinkSpeeds.GTs_2_5 |
            PCIeCapability.PCIeLinkSpeeds.GTs_5_0 |
            PCIeCapability.PCIeLinkSpeeds.GTs_8_0 |
            PCIeCapability.PCIeLinkSpeeds.GTs_16_0 |
            PCIeCapability.PCIeLinkSpeeds.GTs_32_0 |
            PCIeCapability.PCIeLinkSpeeds.GTs_64_0
        );

        PCIeCapabilityStructure.SetMaximumLinkSpeed(PCIeCapability.PCIeLinkSpeeds.GTs_64_0);
        PCIeCapabilityStructure.SetMaximumLinkWidth(16);

        // Set maximum permitted defaults, but it can be manually overridden
        // Normally, these should be negotiated with the link partner
        CurrentLinkSpeed = PCIeCapability.PCIeLinkSpeeds.GTs_64_0;
        NegotiatedLinkWidth = 16;
    }

    // These registers are specific to the Controller/Endpoint only
    private void DefineRegisters()
    {
        RegistersDBI.PCIE_VERSION_NUMBER.Define(dbiRegistersCollection)
            .WithEnumField(0, 32, FieldMode.Read, valueProviderCallback: (VersionID _) => Version, name: "Version");

        RegistersDBI.PCIE_VERSION_TYPE.Define(dbiRegistersCollection)
            .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => Type, name: "Type");

        // ATU viewport is always unrolled for our IP version
        RegistersDBI.PCIE_ATU_VIEWPORT.Define(dbiRegistersCollection)
            .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => AtuViewportUnrolled, name: "ATU Viewport");

        RegistersDBI.PCIE_PORT_DEBUG1.Define(dbiRegistersCollection)
            .WithReservedBits(30, 2)
            .WithFlag(29, FieldMode.Read, valueProviderCallback: _ => LinkInTraining, name: "Link in training")
            .WithReservedBits(5, 24)
            .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => LinkIsUp, name: "Link up")
            .WithReservedBits(0, 4);

        RegistersDBI.PCIE_PORT_DEBUG0.Define(dbiRegistersCollection)
            .WithReservedBits(6, 26)
            .WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ => LinkStatus, name: "Link status");

        RegistersDBI.PCIE_MISC_CONTROL_1_OFF.Define(dbiRegistersCollection)
            .WithReservedBits(1, 31)
            .WithFlag(0, out dbiIsWriteUnlocked, name: "DBI Write Unlock");

        if(!hasVendorStubs)
        {
            // These registers below are no-op, since there is no Link Layer to talk with
            // Just replay back what the driver wrote
            RegistersDBI.PCIE_PORT_LINK_CONTROL.Define(dbiRegistersCollection)
                .WithReservedBits(22, 10)
                .WithValueField(16, 6, name: "Link Mode")
                .WithReservedBits(8, 8)
                .WithFlag(7, name: "Fast Link Mode")
                .WithReservedBits(6, 1)
                .WithFlag(5, name: "DLL Link Enable")
                .WithReservedBits(0, 5);

            RegistersDBI.PCIE_PORT_LANE_SKEW.Define(dbiRegistersCollection)
                .WithReservedBits(24, 8)
                .WithValueField(0, 24, name: "Lane Skew Insert");

            RegistersDBI.PCIE_LINK_WIDTH_SPEED_CONTROL.Define(dbiRegistersCollection)
                .WithReservedBits(18, 14)
                .WithFlag(17, name: "Speed change")
                .WithReservedBits(13, 4)
                .WithValueField(8, 5, name: "Link Width")
                .WithValueField(0, 8, name: "Number of Fast Training Sequences (N_FTS)");

            RegistersDBI.PCIE_PORT_AFR.Define(dbiRegistersCollection)
                .WithFlag(30, name: "Enter ASPM")
                .WithValueField(27, 3, name: "L1 Entrance Latency")
                .WithValueField(24, 3, name: "L0s Entrance Latency")
                .WithValueField(16, 8, name: "CC Number of Fast Training Sequences (CC_N_FTS)")
                .WithValueField(8, 8, name: "Number of Fast Training Sequences (N_FTS)")
                .WithReservedBits(0, 8);

            RegistersDBI.GEN3_RELATED.Define(dbiRegistersCollection)
                .WithReservedBits(26, 6)
                .WithValueField(24, 2, name: "Rate Shadow Select")
                .WithReservedBits(17, 7)
                .WithFlag(16, name: "GEN3 EQ Disable")
                .WithReservedBits(14, 2)
                .WithFlag(13, name: "RXEQ RGRDLESS RXTS")
                .WithReservedBits(10, 3)
                .WithFlag(9, name: "EQ Phase 2,3")
                .WithReservedBits(1, 8)
                .WithFlag(0, name: "GEN3 ZRXDC NONCOMPL");

            RegistersDBI.GEN4_LANE_MARGINING_1_OFF.Define(dbiRegistersCollection)
                .WithReservedBits(30, 2)
                .WithValueField(24, 6, name: "Margining Max Voltage Offset")
                .WithReservedBits(23, 1)
                .WithValueField(16, 7, name: "Margining Num Voltage Steps")
                .WithReservedBits(14, 2)
                .WithValueField(8, 6, name: "Margining Max Timing Offset")
                .WithReservedBits(6, 2)
                .WithValueField(0, 6, name: "Margining Num Timing Steps");

            RegistersDBI.GEN4_LANE_MARGINING_2_OFF.Define(dbiRegistersCollection)
                .WithReservedBits(29, 3)
                .WithFlag(28, name: "Margining Independent Error Sampler")
                .WithFlag(27, name: "Margining Sample Reporting Method")
                .WithFlag(26, name: "Margining Independent Left Right Timing")
                .WithFlag(25, name: "Margining Independent Up Down Voltage")
                .WithFlag(24, name: "Margining Voltage Supported")
                .WithReservedBits(22, 2)
                .WithValueField(16, 5, name: "Max Lanes")
                .WithValueField(8, 6, name: "Sample Rate Timing")
                .WithValueField(0, 6, name: "Sample Rate Voltage");
        }
    }

    // This function instantiates the internal Address Translation Units
    private void DefineATUs(uint iATUInboundRegions, uint iATUOutboundRegions, uint iATUAlignmentBits, uint iATUHighLimitBits)
    {
        for(uint i = 0; i < iATUInboundRegions; ++i)
        {
            inboundATUs[i] = new InternalAddressTranslationUnit(
                this, iatuRegistersCollection, outbound: false, index: i, iATUAlignmentBits, iATUHighLimitBits
            );
        }

        for(uint i = 0; i < iATUOutboundRegions; ++i)
        {
            outboundATUs[i] = new InternalAddressTranslationUnit(
                this, iatuRegistersCollection, outbound: true, index: i, iATUAlignmentBits, iATUHighLimitBits
            );
        }
    }

    private VersionID version = SupportedVersion;
    private ulong currentAccessAbsoluteAddress;

    private IFlagRegisterField dbiIsWriteUnlocked;

    private readonly InternalAddressTranslationUnit[] inboundATUs;
    private readonly InternalAddressTranslationUnit[] outboundATUs;
    private readonly DoubleWordRegisterCollection iatuRegistersCollection;
    private readonly DoubleWordRegisterCollection dbiRegistersCollection;

    private readonly Dictionary<int, PCIeBasePeripheral> registeredPCIePeripherals;
    private readonly BarContainer barContainer;
    private readonly IMachine machine;
    private readonly bool hasVendorStubs;

    private readonly RegisterMapper dbiRegisterMapper;
    private readonly RegisterMapper iAtuRegisterMapper;
    private readonly RegisterMapper deviceRegisterMapper;
    private readonly MapperProvider mapperProvider;

    private const string AtuRegion = "iatu";
    private const string DbiRegion = "dbi";
    private const string DevicesRegion = "devices";

    private const uint AtuViewportUnrolled = 0xFFFFFFFF;
    private const byte LinkStateL0 = 0x11;
    private const int InboudATURegistersOffset = 0x100;

    // A boundary below which the PCIe registers live, accessible in DBI region
    private const long DBIRegionPCIeBoundary = 0x100;

    public enum VersionID : uint
    {
        DW_PCIE_VER_365A = 0x3336352a,
        DW_PCIE_VER_460A = 0x3436302a,
        DW_PCIE_VER_470A = 0x3437302a,
        DW_PCIE_VER_480A = 0x3438302a,
        DW_PCIE_VER_490A = 0x3439302a,
        DW_PCIE_VER_500A = 0x3530302a,
        DW_PCIE_VER_510A = 0x3531302a,
        DW_PCIE_VER_520A = 0x3532302a,
        DW_PCIE_VER_540A = 0x3534302a,
        DW_PCIE_VER_562A = 0x3536322a,
    }

    protected class InternalAddressTranslationUnit
    {
        public static long Size => 0x100;

        public InternalAddressTranslationUnit(IPeripheral parent, DoubleWordRegisterCollection rc,
            bool outbound, uint index, uint iATUAlignmentBits, uint iATUHighLimitBits)
        {
            this.parent = parent;
            Idx = index;
            Outbound = outbound;
            alignmentMask = BitHelper.CalculateMask((int)iATUAlignmentBits, 0);
            highLimitMask = BitHelper.CalculateMask((int)iATUHighLimitBits, 0);

            ulong baseAddr = (outbound ? 0UL : InboudATURegistersOffset) | (ulong)(2 * InternalAddressTranslationUnit.Size * index);
            DefineRegisters(rc, baseAddr);
        }

        public void Reset()
        {
            TargetSpace = 0;
        }

        public bool DoesAddressHit(ulong absoluteAddress)
        {
            if(!Enabled)
            {
                return false;
            }
            // inbound address not handled for now
            if(!Outbound)
            {
                throw new InvalidOperationException("Inbound iATUs are not functional");
            }
            if(LimitAddress < BaseAddress)
            {
                parent.Log(LogLevel.Warning, "{0} is misconfigured, has Limit < Base (0x{1:X} < 0x{2:X}). Is \"Increase Region Size\" in CTRL1 enabled?",
                    Name, LimitAddress, BaseAddress);
                return false;
            }
            if(absoluteAddress < BaseAddress || absoluteAddress > LimitAddress)
            {
                return false;
            }
            return true;
        }

        public ulong Translate(ulong absoluteAddress)
        {
            return absoluteAddress - BaseAddress + TargetAddress;
        }

        public bool Outbound { get; }

        public uint Idx { get; }

        public bool Enabled => enabled.Value;

        // Not implemented. Will be used by an internal DMA engine
        public bool DMABypass => dmaBypass.Value;

        public string Name => $"{(Outbound ? "Outbound" : "Inbound")} iATU_{Idx}";

        public ulong BaseAddress => lowerBase.Value | (upperBase.Value << 32);

        public ulong TargetAddress => lowerTarget.Value | (upperTarget.Value << 32);

        public ulong LimitAddress => (limit.Value | alignmentMask) | (!enableUpperLimit.Value ? 0 : ((upperLimit.Value & highLimitMask) << 32));

        // Aliases for accessing the Configuration Space
        public ulong Bus => GetConfigurationField(lowerTarget.Value, 24, 8);

        public ulong Device => GetConfigurationField(lowerTarget.Value, 19, 5);

        public ulong Function => GetConfigurationField(lowerTarget.Value, 16, 3);

        public PCIeSpace TargetSpace { get; private set; }

        protected void DefineRegisters(DoubleWordRegisterCollection registerCollection, ulong baseAddr)
        {
            (RegistersIATU.ATU_REGION_CTRL1 + baseAddr).Define(registerCollection)
                .WithTag("FUNC_NUM", 20, 12)
                .WithReservedBits(14, 6)
                .WithFlag(13, out enableUpperLimit, name: "Increase Region Size")
                .WithReservedBits(9, 4)
                .WithTaggedFlag("TD (Append TLP Digest)", 8)
                .WithEnumField(0, 8, out tlpType, writeCallback: (_, val) =>
                {
                    switch(val)
                    {
                    // Currently, Renode does not make distinction between Type 0 and 1 configuration requests,
                    // because we do not operate on the TLP packet level.
                    // Either request will be just routed to the Configuration Space of the attached peripheral
                    case TLPType.PCIE_TLP_TYPE_CFG0_RDWR:
                    case TLPType.PCIE_TLP_TYPE_CFG1_RDWR:
                        TargetSpace = PCIeSpace.Configuration;
                        break;
                    case TLPType.PCIE_TLP_TYPE_MEM_RDWR:
                        TargetSpace = PCIeSpace.Memory;
                        break;
                    default:
                        TargetSpace = 0;
                        parent.Log(LogLevel.Warning, "Not supported TLP type selected: {0} defaulting to {1}, for {2}", val, TargetSpace, Name);
                        break;
                    }
                }, name: "TLP Type");

            (RegistersIATU.ATU_REGION_CTRL2 + baseAddr).Define(registerCollection)
                .WithFlag(31, out enabled, name: "Enable")
                .WithFlag(30, name: "BAR Mode Enable")
                .WithReservedBits(29, 1)
                // Used in ECAM mode. Unimplemented for now
                .WithTaggedFlag("CFG Shift Mode Enable", 28)
                .WithFlag(27, out dmaBypass, name: "DMA Bypass")
                .WithReservedBits(23, 4)
                .WithTaggedFlag("Inhibit Payload", 22)
                .WithReservedBits(20, 2)
                .WithTaggedFlag("Func Num Match Enable", 19)
                .WithReservedBits(8, 11)
                .WithTag("Code", 0, 8);

            (RegistersIATU.ATU_LOWER_BASE + baseAddr).Define(registerCollection)
                .WithValueField(0, 32, out lowerBase, name: "Lower Base Address");

            (RegistersIATU.ATU_UPPER_BASE + baseAddr).Define(registerCollection)
                .WithValueField(0, 32, out upperBase, name: "Upper Base Address");

            // Lower bits are masked to determine alignment
            (RegistersIATU.ATU_LIMIT + baseAddr).Define(registerCollection)
                .WithValueField(0, 32, out limit, valueProviderCallback: val => val | alignmentMask, name: "Lower Limit Address");

            (RegistersIATU.ATU_UPPER_LIMIT + baseAddr).Define(registerCollection)
                .WithValueField(0, 32, out upperLimit, valueProviderCallback: val => val & highLimitMask, name: "Upper Limit Address");

            (RegistersIATU.ATU_LOWER_TARGET + baseAddr).Define(registerCollection)
                // When used to write to Configuration Space, this field is used to encode Bus/Device/Function
                .WithValueField(0, 32, out lowerTarget, name: "Lower Target Address");

            (RegistersIATU.ATU_UPPER_TARGET + baseAddr).Define(registerCollection)
                .WithValueField(0, 32, out upperTarget, name: "Upper Target Address");
        }

        private ulong GetConfigurationField(ulong register, byte offset, byte width)
        {
            if(TargetSpace != PCIeSpace.Configuration)
            {
                throw new InvalidOperationException("Target needs to be a Configuration Space");
            }
            return BitHelper.GetValue(register, offset, width);
        }

        private IValueRegisterField lowerBase;
        private IValueRegisterField upperBase;

        private IValueRegisterField lowerTarget;
        private IValueRegisterField upperTarget;

        private IValueRegisterField limit;
        private IValueRegisterField upperLimit;

        private IFlagRegisterField enableUpperLimit;
        private IFlagRegisterField enabled;

        private IFlagRegisterField dmaBypass;

        private IEnumRegisterField<TLPType> tlpType;

        private readonly IPeripheral parent;
        private readonly uint alignmentMask;
        private readonly uint highLimitMask;

        // Define taken from Linux kernel, likely one to one match with field TLP type
        public enum TLPType
        {
            PCIE_TLP_TYPE_MEM_RDWR = 0x00, /* Memory Read/Write Request */
            PCIE_TLP_TYPE_IO_RDWR = 0x02, /* I/O Read/Write Request */
            PCIE_TLP_TYPE_CFG0_RDWR = 0x04, /* Config Type 0 Read/Write Request */
            PCIE_TLP_TYPE_CFG1_RDWR = 0x05, /* Config Type 1 Read/Write Request */
            PCIE_TLP_TYPE_MSG = 0x10, /* Message With/Without data Request */
        }
    }

    [RegistersDescription(DevicesRegion)]
    protected enum RegistersDevices
    {
        // Dummy Registers, to clearly show where the PCIe <-> host mappings are
        PCIeDeviceSpace = 0,
    }

    [RegistersDescription(AtuRegion)]
    protected enum RegistersIATU : ulong
    {
        ATU_REGION_CTRL1 = 0x00,
        ATU_REGION_CTRL2 = 0x04,
        ATU_LOWER_BASE = 0x08,
        ATU_UPPER_BASE = 0x0C,
        ATU_LIMIT = 0x10,
        ATU_LOWER_TARGET = 0x14,
        ATU_UPPER_TARGET = 0x18,
        ATU_UPPER_LIMIT = 0x20,
    }

    [RegistersDescription(DbiRegion)]
    protected enum RegistersDBI
    {
        PCIE_PORT_FORCE = 0x708,
        PCIE_PORT_AFR = 0x70C,
        PCIE_PORT_LINK_CONTROL = 0x710,
        PCIE_PORT_LANE_SKEW = 0x714,

        PCIE_PORT_DEBUG0 = 0x728,
        PCIE_PORT_DEBUG1 = 0x72C,

        PCIE_LINK_WIDTH_SPEED_CONTROL = 0x80C,

        PCIE_MSI_ADDR_LO = 0x820,
        PCIE_MSI_ADDR_HI = 0x824,

        PCIE_MSI_INTR0_ENABLE = 0x828,
        PCIE_MSI_INTR0_STATUS = 0x830,
        PCIE_MSI_INTR0_MASK = 0x82C,

        GEN3_RELATED = 0x890,
        GEN3_EQ_CONTROL = 0x8A8,
        GEN3_EQ_FB_MODE_DIR_CHANGE = 0x8AC,

        PCIE_MISC_CONTROL_1_OFF = 0x8BC,

        PCIE_PORT_MULTI_LANE_CTRL = 0x8C0,

        COHERENCY_CONTROL_1 = 0x8E0,
        COHERENCY_CONTROL_2 = 0x8E4,
        COHERENCY_CONTROL_3 = 0x8E8,

        PCIE_VERSION_NUMBER = 0x8F8,
        PCIE_VERSION_TYPE = 0x8FC,

        GEN4_LANE_MARGINING_1_OFF = 0xB80,
        GEN4_LANE_MARGINING_2_OFF = 0xB84,

        PCIE_ATU_VIEWPORT = 0x900,

        PCIE_MSIX_DOORBELL = 0x948,
    }

    // elbi/app region is left out (it's a vendor's glue region)
}
