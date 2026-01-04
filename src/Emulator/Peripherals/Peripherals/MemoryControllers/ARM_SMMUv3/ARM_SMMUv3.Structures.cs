//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public partial class ARM_SMMUv3
    {
        private T ReadStruct<T>(ulong baseAddress, uint index = 0)
        {
            var length = Packet.CalculateLength<T>();
            var elementAddress = baseAddress + (ulong)length * index;
            return Packet.Decode<T>(sysbus.ReadBytes(elementAddress, length, context: Context));
        }

        // All subclasses must have the same length as the base
        private T ReadSubclass<T>(ulong baseAddress, Func<IList<byte>, Type> typeSelector, uint index = 0)
        {
            var length = Packet.CalculateLength<T>();
            var elementAddress = baseAddress + (ulong)length * index;
            return Packet.DecodeSubclass<T>(sysbus.ReadBytes(elementAddress, length, context: Context), typeSelector);
        }

        private const int CommandLength = 16; // bytes
        private const int StreamTableEntryLength = 64; // bytes
        private const int ContextDescriptorLength = 64; // bytes
        private const int PageTableEntryLength = 8; // bytes

#pragma warning disable renode_class_members_order // For fields like MSIAddress, where the private backing field is among other public fields
#pragma warning disable IDE1006 // Public field names like nG reflect the documentation
#pragma warning disable 649 // Fields will be assigned reflexively
        [LeastSignificantByteFirst, Width(bytes: CommandLength)]
        public class Command
        {
            public override string ToString() => this.ToDebugString();

            public CommandError ValidateAndRun(ARM_SMMUv3 parent, SecurityState securityState)
            {
                if(securityState != SecurityState.Secure && SSec)
                {
                    parent.WarningLog("SSec set on a command executed from the non-secure queue ({0})", this);
                    return CommandError.Illegal;
                }
                return Run(parent, securityState);
            }

            protected virtual CommandError Run(ARM_SMMUv3 parent, SecurityState securityState)
            {
                if(Enum.IsDefined(typeof(Opcode), Opcode))
                {
                    parent.ErrorLog("Unhandled command {0}: {1}", Opcode, this);
                    return CommandError.None;
                }
                parent.ErrorLog("Invalid command opcode: 0x{0:X}", Opcode);
                return CommandError.Illegal;
            }

            [PacketField, Offset(bits: 0), Width(bits: 8)]
            public Opcode Opcode;

            [PacketField, Offset(bits: 10), Width(bits: 1)]
            public bool SSec; // not quite common (for example sync doesn't have it)
        }

        [Command(Opcode.CMD_PREFETCH_CONFIG)]
        public class PrefetchConfigCommand : Command
        {
            protected override CommandError Run(ARM_SMMUv3 parent, SecurityState securityState)
            {
                parent.NoisyLog("Prefetch config: {0}", this);
                // Do nothing (valid implementation)
                return CommandError.None;
            }

            // RES0 8-9

            [PacketField, Offset(bits: 11), Width(bits: 1)]
            public bool SSV;

            [PacketField, Offset(bits: 12), Width(bits: 20)]
            public int SubstreamID;

            [PacketField, Offset(bits: 32), Width(bits: 32)]
            public uint StreamID;
        }

        [Command(Opcode.CMD_PREFETCH_ADDR)]
        public class PrefetchAddressCommand : PrefetchConfigCommand
        {
            protected override CommandError Run(ARM_SMMUv3 parent, SecurityState securityState)
            {
                parent.NoisyLog("Prefetch address: {0}", this);
                // Do nothing (valid implementation)
                return CommandError.None;
            }

            public ulong Address => address << 12;

            [PacketField, Offset(bits: 64), Width(bits: 5)]
            public int Size;

            [PacketField, Offset(bits: 69), Width(bits: 5)]
            public int Stride;

            // RES0 74

            [PacketField, Offset(bits: 75), Width(bits: 1)]
            public bool NS;

            [PacketField, Offset(bits: 76), Width(bits: 52)]
            private readonly ulong address;
        }

        [Command(Opcode.CMD_CFGI_STE)]
        public class InvalidateSteCommand : Command
        {
            protected override CommandError Run(ARM_SMMUv3 parent, SecurityState securityState)
            {
                // Security is checked in `ValidateAndRun`
                parent.SelectDomain(securityState).InvalidateSte(StreamID);
                return CommandError.None;
            }

            [PacketField, Offset(bits: 32), Width(bits: 32)]
            public uint StreamID;

            [PacketField, Offset(bits: 64), Width(bits: 1)]
            public bool Leaf;
        }

        [Command(Opcode.CMD_CFGI_STE_RANGE)]
        public class InvalidateSteRangeCommand : Command
        {
            protected override CommandError Run(ARM_SMMUv3 parent, SecurityState securityState)
            {
                var count = (2u << (Range + 1)) - 1;
                var start = StreamID & ~count;
                // Security is checked in `ValidateAndRun`
                var domain = parent.SelectDomain(securityState);
                for(var i = start; i <= start + count; ++i)
                {
                    domain.InvalidateSte(i);
                }
                return CommandError.None;
            }

            [PacketField, Offset(bits: 32), Width(bits: 32)]
            public uint StreamID;

            [PacketField, Offset(bits: 64), Width(bits: 5)]
            public int Range;
        }

        [Command(Opcode.CMD_TLBI_NH_ALL)]
        public class InvalidateTlbByVmidCommand : Command
        {
            protected override CommandError Run(ARM_SMMUv3 parent, SecurityState securityState)
            {
                parent.InvalidateTlb(); // TODO: More granular invalidation
                return CommandError.None;
            }

            [PacketField, Offset(bits: 32), Width(bits: 16)]
            public ushort VMID;
        }

        [Command(Opcode.CMD_TLBI_NH_ASID)]
        public class InvalidateTlbByVmidAsid : InvalidateTlbByVmidCommand
        {
            protected override CommandError Run(ARM_SMMUv3 parent, SecurityState securityState)
            {
                parent.InvalidateTlb(); // TODO: More granular invalidation
                return CommandError.None;
            }

            [PacketField, Offset(bits: 48), Width(bits: 16)]
            public ushort ASID;
        }

        [Command(Opcode.CMD_TLBI_NH_VA)]
        public class InvalidateTlbByVirtualAddress : InvalidateTlbByVmidCommand
        {
            protected override CommandError Run(ARM_SMMUv3 parent, SecurityState securityState)
            {
                parent.InvalidateTlb(Address); // TODO: Use other hints
                return CommandError.None;
            }

            public ulong Address => address << 12;

            [PacketField, Offset(bits: 12), Width(bits: 5)]
            public byte NUM;

            // RES0 17-19

            [PacketField, Offset(bits: 20), Width(bits: 6)]
            public byte SCALE;

            // RES0 26-31

            [PacketField, Offset(bits: 64), Width(bits: 1)]
            public bool Leaf;

            // RES0 65-70

            [PacketField, Offset(bits: 71), Width(bits: 1)]
            public bool TTL128;

            [PacketField, Offset(bits: 72), Width(bits: 2)]
            public byte TTL;

            [PacketField, Offset(bits: 74), Width(bits: 2)]
            public TlbInvalidationGranule TG;

            [PacketField, Offset(bits: 76), Width(bits: 52)]
            private readonly ulong address;
        }

        [Command(Opcode.CMD_TLBI_NH_VAA)]
        public class InvalidateTlbByVirtualAddressAsid : InvalidateTlbByVirtualAddress
        {
            protected override CommandError Run(ARM_SMMUv3 parent, SecurityState securityState)
            {
                return base.Run(parent, securityState); // TODO: Use ASID
            }

            [PacketField, Offset(bits: 48), Width(bits: 16)]
            public ushort ASID;
        }

        [Command(Opcode.CMD_SYNC)]
        public class SyncCommand : Command
        {
            protected override CommandError Run(ARM_SMMUv3 parent, SecurityState securityState)
            {
                parent.NoisyLog("Sync: {0}", this);
                // TODO: Raise completion sginal
                return CommandError.None;
            }

            public ulong MSIAddress => msiAddress << 2;

            [PacketField, Offset(bits: 12), Width(bits: 2)]
            public CompletionSignal CS;

            [PacketField, Offset(bits: 22), Width(bits: 2)]
            public Shareability MSH;

            [PacketField, Offset(bits: 24), Width(bits: 4)]
            public int MSIAttr;

            [PacketField, Offset(bits: 32), Width(bits: 32)]
            public uint MSIData;

            [PacketField, Offset(bits: 66), Width(bits: 54)]
            private readonly ulong msiAddress;

            [PacketField, Offset(bits: 127), Width(bits: 1)]
            public bool MSI_NS;
        }

        // VMSAv8-64/VMSAv8-32 LPAE
        [LeastSignificantByteFirst, Width(bytes: PageTableEntryLength)]
        public class PageTableEntry
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bits: 0), Width(bits: 1)]
            public bool Valid;

            [PacketField, Offset(bits: 1), Width(bits: 1)]
            public bool Table;
        }

        public class BlockDescriptor : PageTableEntry
        {
            // gap 2-11 IGNORED

            [PacketField, Offset(bits: 12), Width(bits: 36)]
            public ulong NextTableAddress;

            // RES0 48-50

            // gap 51 IGNORED

            [PacketField, Offset(bits: 52), Width(bits: 1)]
            public bool Protected;

            // gap 53-58 IGNORED

            [PacketField, Offset(bits: 59), Width(bits: 1)]
            public bool PXNTable;

            [PacketField, Offset(bits: 60), Width(bits: 1)]
            public bool UXNTable;

            [PacketField, Offset(bits: 61), Width(bits: 2)]
            public TableAccessPermission APTable;

            [PacketField, Offset(bits: 63), Width(bits: 1)]
            public bool NSTable;
        }

        // NOTE: This structure is used to represet both the VMSAv8-64
        // and VMSAv8-32 LPAE translation table formats.
        public class TableDescriptor : PageTableEntry
        {
            public BusAccessPrivileges GetPrivileges(bool privilegedAccess)
            {
                var apPrivilegedOnly = ((int)AP & 0b01) == 0;
                var apReadOnly = ((int)AP & 0b10) == 0b10;

                var allowed = BusAccessPrivileges.None;
                if(privilegedAccess || !apPrivilegedOnly)
                {
                    allowed = apReadOnly ? BusAccessPrivileges.Read : BusAccessPrivileges.Read | BusAccessPrivileges.Write;
                }

                if((privilegedAccess && !PXN) || (!privilegedAccess && !UXN))
                {
                    allowed |= BusAccessPrivileges.Other;
                }
                return allowed;
            }

            public ulong? GetOutputAddress(bool vmsa32)
            {
                if(vmsa32)
                {
                    // For VMSAv8-32 LPAE OutputAddress is stored in bits 39:12 instead of 47:12
                    // in VMSAv8-64. Having any of the extra bits set to 1 causes an address size fault.
                    const ulong vmsa32Mask = (1UL << 28) - 1;
                    if((vmsa32Mask & OutputAddress) == OutputAddress)
                    {
                        return OutputAddress;
                    }
                    return null;
                }
                return OutputAddress;
            }

            [PacketField, Offset(bits: 2), Width(bits: 3)]
            public int AttrIndx;

            [PacketField, Offset(bits: 5), Width(bits: 1)]
            public bool NS;

            [PacketField, Offset(bits: 6), Width(bits: 2)]
            public AccessPermission AP;

            [PacketField, Offset(bits: 8), Width(bits: 2)]
            public Shareability SH;

            [PacketField, Offset(bits: 10), Width(bits: 1)]
            public bool AccessFlag;

            [PacketField, Offset(bits: 11), Width(bits: 1)]
            public bool nG;

            [PacketField, Offset(bits: 12), Width(bits: 36)]
            public ulong OutputAddress; // On VMSAv8-32 LPAE the length is 28 bits

            // RES0 48-49

            [PacketField, Offset(bits: 50), Width(bits: 1)]
            public bool GP; // Reserved on VMSAv8-32 LPAE

            [PacketField, Offset(bits: 51), Width(bits: 1)]
            public bool DBM; // Reserved on VMSAv8-32 LPAE

            [PacketField, Offset(bits: 52), Width(bits: 1)]
            public bool Contiguous;

            [PacketField, Offset(bits: 53), Width(bits: 1)]
            public bool PXN;

            [PacketField, Offset(bits: 54), Width(bits: 1)]
            public bool UXN; // XN on VMSAv8-32 LPAE

            [PacketField, Offset(bits: 55), Width(bits: 4)]
            public int SoftwareReserved;

            [PacketField, Offset(bits: 59), Width(bits: 4)]
            public int PBHA; // or AttrIndx[3], POIndex[2:0]

            [PacketField, Offset(bits: 63), Width(bits: 1)]
            public bool AMEC;
        }

        [LeastSignificantByteFirst, Width(bytes: StreamTableEntryLength)]
        public struct StreamTableEntry
        {
            public override string ToString() => this.ToDebugString();

            public ulong S1ContextPtr => s1ContextPtr << 6;

            [PacketField, Offset(bits: 0), Width(bits: 1)]
            public bool V;

            [PacketField, Offset(bits: 1), Width(bits: 3)]
            public StreamConfiguration Config;

            [PacketField, Offset(bits: 4), Width(bits: 2)]
            public Stage1Format S1FMT;

            [PacketField, Offset(bits: 6), Width(bits: 50)]
            private readonly ulong s1ContextPtr;

            // RES0 56-58

            [PacketField, Offset(bits: 59), Width(bits: 5)]
            public int S1CDMax;

            [PacketField, Offset(bits: 64), Width(bits: 2)]
            public DefaultSubstreamBehavior S1DSS;

            [PacketField, Offset(bits: 66), Width(bits: 2)]
            public MemoryRegionAttribute S1CIR;

            [PacketField, Offset(bits: 68), Width(bits: 2)]
            public MemoryRegionAttribute S1COR;

            [PacketField, Offset(bits: 70), Width(bits: 2)]
            public Shareability S1CSH;

            [PacketField, Offset(bits: 72), Width(bits: 1)]
            public bool S2HWU59;

            [PacketField, Offset(bits: 73), Width(bits: 1)]
            public bool S2HWU60;

            [PacketField, Offset(bits: 74), Width(bits: 1)]
            public bool S2HWU61;

            [PacketField, Offset(bits: 75), Width(bits: 1)]
            public bool S2HWU62;

            [PacketField, Offset(bits: 76), Width(bits: 1)]
            public bool DRE;

            [PacketField, Offset(bits: 77), Width(bits: 4)]
            public int CONT;

            [PacketField, Offset(bits: 81), Width(bits: 1)]
            public bool DCP;

            [PacketField, Offset(bits: 82), Width(bits: 1)]
            public bool PPAR;

            [PacketField, Offset(bits: 83), Width(bits: 1)]
            public bool MEV;

            [PacketField, Offset(bits: 84), Width(bits: 4)]
            public int SW_RESERVED;

            [PacketField, Offset(bits: 88), Width(bits: 1)]
            public bool S1PIE;

            [PacketField, Offset(bits: 89), Width(bits: 1)]
            public bool S2FWB;

            [PacketField, Offset(bits: 90), Width(bits: 1)]
            public bool S1MPAM;

            [PacketField, Offset(bits: 91), Width(bits: 1)]
            public bool S1STALLD;

            [PacketField, Offset(bits: 92), Width(bits: 2)]
            public PCIeATS EATS;

            [PacketField, Offset(bits: 94), Width(bits: 2)]
            public StreamWorld STRW;

            [PacketField, Offset(bits: 96), Width(bits: 4)]
            public int MemAttr;

            [PacketField, Offset(bits: 100), Width(bits: 1)]
            public bool MTCFG;

            [PacketField, Offset(bits: 101), Width(bits: 4)]
            public int ALLOCCFG;

            // RES0 105-107

            [PacketField, Offset(bits: 108), Width(bits: 2)]
            public Shareability SHCFG;

            [PacketField, Offset(bits: 110), Width(bits: 2)]
            public NonSecureAttribute NSCFG;

            [PacketField, Offset(bits: 112), Width(bits: 2)]
            public Privilege PRIVCFG;

            [PacketField, Offset(bits: 114), Width(bits: 2)]
            public InstructionData INSTCFG;

            // gap imeplementation defined 116-127

            [PacketField, Offset(bits: 128), Width(bits: 16)]
            public ushort S2VMID;

            // gap imeplementation defined 144-159

            [PacketField, Offset(bits: 160), Width(bits: 6)]
            public int S2TOSZ;

            [PacketField, Offset(bits: 166), Width(bits: 2)]
            public int S2SLO;

            [PacketField, Offset(bits: 168), Width(bits: 2)]
            public Cacheability S2IR0;

            [PacketField, Offset(bits: 170), Width(bits: 2)]
            public Cacheability S2OR0;

            [PacketField, Offset(bits: 172), Width(bits: 2)]
            public Shareability S2SH0;

            [PacketField, Offset(bits: 174), Width(bits: 2)]
            public Stage2TranslationGranule S2TG;

            [PacketField, Offset(bits: 176), Width(bits: 3)]
            public AddressSize S2PS;

            [PacketField, Offset(bits: 179), Width(bits: 1)]
            public bool S2AA64;

            [PacketField, Offset(bits: 180), Width(bits: 1)]
            public bool S2ENDI;

            [PacketField, Offset(bits: 181), Width(bits: 1)]
            public bool S2AFFD;

            [PacketField, Offset(bits: 182), Width(bits: 1)]
            public bool S2PTW;

            [PacketField, Offset(bits: 183), Width(bits: 1)]
            public bool S2HD;

            [PacketField, Offset(bits: 184), Width(bits: 1)]
            public bool S2HA;

            [PacketField, Offset(bits: 185), Width(bits: 1)]
            public bool S2S;

            [PacketField, Offset(bits: 186), Width(bits: 1)]
            public bool S2R;

            [PacketField, Offset(bits: 187), Width(bits: 1)]
            public bool S2HAFT;

            [PacketField, Offset(bits: 188), Width(bits: 1)]
            public bool S2PIE;

            [PacketField, Offset(bits: 189), Width(bits: 1)]
            public bool S2POE;

            [PacketField, Offset(bits: 190), Width(bits: 2)]
            public DptVmidMatch DPT_VMATCH;

            [PacketField, Offset(bits: 192), Width(bits: 1)]
            public bool S2NSW;

            [PacketField, Offset(bits: 193), Width(bits: 1)]
            public bool S2NSA;

            [PacketField, Offset(bits: 194), Width(bits: 1)]
            public bool S2SL0_2;

            [PacketField, Offset(bits: 195), Width(bits: 1)]
            public bool S2DS;

            [PacketField, Offset(bits: 196), Width(bits: 52)]
            public ulong S2TTB;

            // RES0 248-252

            [PacketField, Offset(bits: 253), Width(bits: 2)]
            public SkipLevel S2SKL;

            // RES0 255

            // gap imeplementation defined 256-271

            [PacketField, Offset(bits: 272), Width(bits: 16)]
            public ushort PARTID;

            [PacketField, Offset(bits: 288), Width(bits: 6)]
            public int S_S2TOSZ;

            [PacketField, Offset(bits: 294), Width(bits: 2)]
            public int S_S2SLO;

            // RES0 296-301

            [PacketField, Offset(bits: 302), Width(bits: 2)]
            public Stage2TranslationGranule S_S2TG;

            [PacketField, Offset(bits: 304), Width(bits: 16)]
            public ushort MECID;

            [PacketField, Offset(bits: 320), Width(bits: 8)]
            public byte PMG;

            [PacketField, Offset(bits: 328), Width(bits: 1)]
            public bool MPAM_NS;

            [PacketField, Offset(bits: 329), Width(bits: 1)]
            public bool AssuredOnly;

            [PacketField, Offset(bits: 330), Width(bits: 1)]
            public bool TL0;

            [PacketField, Offset(bits: 331), Width(bits: 1)]
            public bool TL1;

            [PacketField, Offset(bits: 332), Width(bits: 44)]
            public ulong VMSPtr;

            // RES0 376-383

            [PacketField, Offset(bits: 384), Width(bits: 1)]
            public bool S2SW;

            [PacketField, Offset(bits: 385), Width(bits: 1)]
            public bool S2SA;

            [PacketField, Offset(bits: 386), Width(bits: 1)]
            public bool S_S2SL0_2;

            // RES0 387

            [PacketField, Offset(bits: 388), Width(bits: 52)]
            public ulong S_S2TTB;

            // RES0 440-444

            [PacketField, Offset(bits: 445), Width(bits: 2)]
            public SkipLevel S_S2SKL;

            // RES0 447;

            [PacketField, Offset(bits: 448), Width(bits: 4 * 16)]
            public byte[] S2POI; // TODO: this should really be an array of 16, 4-bit PermissionOverlay entries, but it currently ends up as 8 bytes
        }

        [LeastSignificantByteFirst, Width(bytes: ContextDescriptorLength)]
        public struct ContextDescriptor
        {
            public override string ToString() => this.ToDebugString();

            public ulong TTB0 => ttb0 << 4;

            public ulong TTB1 => ttb1 << 4;

            public TranslationParams? GetTranslationParams(ulong va, int vaBits, int level, ulong? tableAddr = null)
            {
                var use0 = (va & (1UL << 55)) == 0;
                if((use0 && EPD0) || (!use0 && EPD1))
                {
                    return null;
                }
                var ttb = tableAddr ?? (use0 ? TTB0 : TTB1);
                var tsz = use0 ? T0SZ : T1SZ;
                var maybeSizeShift = use0 ? GetPageSizeShiftForGranule(TG0) : GetPageSizeShiftForGranule(TG1);
                if(!maybeSizeShift.HasValue)
                {
                    return null;
                }
                var sizeShift = maybeSizeShift.Value;

                var vaShift = GetVaSizeShiftAtLevel(level, sizeShift);
                var indexBits = GetIndexBitsPerLevel(vaBits, level, sizeShift);
                var mask = (1UL << indexBits) - 1;
                var tableOffset = (va >> vaShift) & mask;
                var tableAddress = ttb + (tableOffset * PageTableEntryLength);
                return new TranslationParams
                {
                    TableAddress = tableAddress,
                    TSZ = tsz,
                    PageSizeShift = sizeShift,
                };
            }

            public int? GetPageSizeShiftForVa(ulong va)
            {
                var use0 = (va & (1UL << 55)) == 0;
                if((use0 && EPD0) || (!use0 && EPD1))
                {
                    return null;
                }
                var maybeSizeShift = use0 ? GetPageSizeShiftForGranule(TG0) : GetPageSizeShiftForGranule(TG1);
                if(maybeSizeShift is int shift)
                {
                    return shift;
                }
                return null;
            }

            [PacketField, Offset(bits: 0), Width(bits: 6)]
            public int T0SZ;

            [PacketField, Offset(bits: 6), Width(bits: 2)]
            public Stage2TranslationGranule TG0;

            [PacketField, Offset(bits: 8), Width(bits: 2)]
            public Cacheability IR0;

            [PacketField, Offset(bits: 10), Width(bits: 2)]
            public Cacheability OR0;

            [PacketField, Offset(bits: 12), Width(bits: 2)]
            public Shareability SH0;

            [PacketField, Offset(bits: 14), Width(bits: 1)]
            public bool EPD0;

            [PacketField, Offset(bits: 15), Width(bits: 1)]
            public bool ENDI;

            [PacketField, Offset(bits: 16), Width(bits: 6)]
            public int T1SZ;

            [PacketField, Offset(bits: 22), Width(bits: 2)]
            public Stage1TranslationGranule TG1;

            [PacketField, Offset(bits: 24), Width(bits: 2)]
            public Cacheability IR1;

            [PacketField, Offset(bits: 26), Width(bits: 2)]
            public Cacheability OR1;

            [PacketField, Offset(bits: 28), Width(bits: 2)]
            public Shareability SH1;

            [PacketField, Offset(bits: 30), Width(bits: 1)]
            public bool EPD1;

            [PacketField, Offset(bits: 31), Width(bits: 1)]
            public bool V;

            [PacketField, Offset(bits: 32), Width(bits: 3)]
            public AddressSize IPS;

            [PacketField, Offset(bits: 35), Width(bits: 1)]
            public bool AFFD;

            [PacketField, Offset(bits: 36), Width(bits: 1)]
            public bool WXN;

            [PacketField, Offset(bits: 37), Width(bits: 1)]
            public bool UWXN;

            [PacketField, Offset(bits: 38), Width(bits: 1)]
            public bool TBI0;

            [PacketField, Offset(bits: 39), Width(bits: 1)]
            public bool TBI1;

            [PacketField, Offset(bits: 40), Width(bits: 1)]
            public bool PAN;

            [PacketField, Offset(bits: 41), Width(bits: 1)]
            public bool AA64;

            [PacketField, Offset(bits: 42), Width(bits: 1)]
            public bool HD;

            [PacketField, Offset(bits: 43), Width(bits: 1)]
            public bool HA;

            [PacketField, Offset(bits: 44), Width(bits: 1)]
            public bool S; // Stall on fault

            [PacketField, Offset(bits: 45), Width(bits: 1)]
            public bool R; // Record event on fault

            [PacketField, Offset(bits: 46), Width(bits: 1)]
            public bool A; // Abort on transaction termination (0 = RAZ/WI)

            [PacketField, Offset(bits: 47), Width(bits: 1)]
            public bool ASET;

            [PacketField, Offset(bits: 48), Width(bits: 16)]
            public ushort ASID;

            [PacketField, Offset(bits: 64), Width(bits: 1)]
            public bool NSCFG0;

            [PacketField, Offset(bits: 65), Width(bits: 1)]
            public bool HAD0;

            [PacketField, Offset(bits: 66), Width(bits: 1)]
            public bool E0PD0;

            [PacketField, Offset(bits: 67), Width(bits: 1)]
            public bool HAFT;

            [PacketField, Offset(bits: 68), Width(bits: 52)]
            private readonly ulong ttb0;

            // RES0 120-121

            [PacketField, Offset(bits: 122), Width(bits: 1)]
            public bool PnCH;

            [PacketField, Offset(bits: 123), Width(bits: 1)]
            public bool EPAN;

            [PacketField, Offset(bits: 124), Width(bits: 1)]
            public bool HWU059;

            [PacketField, Offset(bits: 125), Width(bits: 1)]
            public bool HWU060;

            [PacketField, Offset(bits: 126), Width(bits: 2)]
            public SkipLevel SKL0;

            [PacketField, Offset(bits: 128), Width(bits: 1)]
            public bool NSCFG1;

            [PacketField, Offset(bits: 129), Width(bits: 1)]
            public bool HAD1;

            [PacketField, Offset(bits: 130), Width(bits: 1)]
            public bool E0PD1;

            [PacketField, Offset(bits: 131), Width(bits: 1)]
            public bool AIE;

            [PacketField, Offset(bits: 132), Width(bits: 52)]
            private readonly ulong ttb1;

            // RES0 184-185

            [PacketField, Offset(bits: 186), Width(bits: 1)]
            public bool DS;

            [PacketField, Offset(bits: 187), Width(bits: 1)]
            public bool PIE;

            [PacketField, Offset(bits: 188), Width(bits: 1)]
            public bool HWU159;

            [PacketField, Offset(bits: 189), Width(bits: 1)]
            public bool HWU160;

            [PacketField, Offset(bits: 190), Width(bits: 2)]
            public SkipLevel SKL1;

            [PacketField, Offset(bits: 192), Width(bits: 32)]
            public uint MAIR0;

            [PacketField, Offset(bits: 224), Width(bits: 32)]
            public uint MAIR1;

            [PacketField, Offset(bits: 256), Width(bits: 32)]
            public uint AMAIR0;

            [PacketField, Offset(bits: 288), Width(bits: 32)]
            public uint AMAIR1;

            // gap imeplementation defined 320-351

            [PacketField, Offset(bits: 352), Width(bits: 16)]
            public ushort PARTID;

            [PacketField, Offset(bits: 368), Width(bits: 8)]
            public byte PMG;

            // RES0 376-383

            [PacketField, Offset(bits: 384), Width(bits: 3 * 16)]
            public byte[] PIIU; // TODO: this should really be an array of 16, 3-bit PermissionInterpretation entries, but it currently ends up as 6 bytes

            // RES0 376-383

            [PacketField, Offset(bits: 448), Width(bits: 3 * 16)]
            public byte[] PIIP; // TODO: this should really be an array of 16, 3-bit PermissionInterpretation entries, but it currently ends up as 6 bytes

            // RES0 496-511

            private static int? GetPageSizeShiftForGranule(Stage1TranslationGranule granule)
            {
                switch(granule)
                {
                case Stage1TranslationGranule.KB4: return 12;
                case Stage1TranslationGranule.KB16: return 14;
                case Stage1TranslationGranule.KB64: return 16;
                default: return null;
                }
            }

            private static int? GetPageSizeShiftForGranule(Stage2TranslationGranule granule)
            {
                switch(granule)
                {
                case Stage2TranslationGranule.KB4: return 12;
                case Stage2TranslationGranule.KB16: return 14;
                case Stage2TranslationGranule.KB64: return 16;
                default: return null;
                }
            }
        }

        public struct TranslationParams
        {
            public override string ToString() => this.ToDebugString();

            public ulong TableAddress;
            public int TSZ;
            public int PageSizeShift;
        }

        public enum PageTableEntryType
        {
            Invalid0 = 0b00,
            Block = 0b01,
            Invalid2 = 0b10,
            Table = 0b11,
        }

        public enum StreamConfiguration
        {
            Abort = 0b000,
            // gap
            Bypass = 0b100,
            TranslateStage1 = 0b101,
            TranslateStage2 = 0b110,
            TranslateStage1And2 = 0b111,
        }

        public enum Stage1Format
        {
            Linear = 0b00,
            TwoLevel4KB = 0b01,
            TwoLevel64KB = 0b10,
        }

        public enum DefaultSubstreamBehavior
        {
            Terminate = 0b00,
            BypassStage1 = 0b01,
            UseSubstream0 = 0b10,
        }

        public enum MemoryRegionAttribute
        {
            NormalNonCacheable = 0b00,
            NormalWriteBackReadAllocate = 0b01,
            NormalWriteThroughReadAllocate = 0b10,
            NormalWriteBackNoReadAllocate = 0b11,
        }

        public enum PCIeATS
        {
            Disabled = 0b00,
            FullATS = 0b01,
            SplitStageATS = 0b10,
            FullATSWithDPT = 0b11,
        }

        public enum StreamWorld
        {
            NS_EL1 = 0b00,
            // EL3 = 0b01,
            NS_EL2 = 0b10,
        }

        public enum Shareability
        {
            NonShareable = 0b00,
            UseIncoming = 0b01, // sometimes (for example in CMD_SYNC.MSH) reserved, treated as 0
            OuterShareable = 0b10,
            InnerShareable = 0b11,
        }

        public enum NonSecureAttribute
        {
            UseIncoming = 0b00,
            // gap
            SecureOrRealm = 0b10,
            NonSecure = 0b11,
        }

        public enum Privilege
        {
            UseIncoming = 0b00,
            // gap
            Unprivileged = 0b10,
            Privileged = 0b11,
        }

        public enum InstructionData
        {
            UseIncoming = 0b00,
            // gap
            Data = 0b10,
            Instruction = 0b11,
        }

        public enum Cacheability
        {
            NonCacheable = 0b00,
            WriteBackReadWriteAllocate = 0b01,
            WriteThroughReadAllocate = 0b10,
            WriteBackReadAllocateNoWrite = 0b11,
        }

        public enum DptVmidMatch
        {
            MatchOrAc10 = 0b00,
            MatchOrAc01Or10 = 0b01,
            NoMatchRequired = 0b10,
        }

        public enum SkipLevel
        {
            Skip0 = 0b00,
            Skip1 = 0b01,
            Skip2 = 0b10,
            Skip3 = 0b11,
        }

        public enum PermissionOverlay : byte
        {
            NoAccess = 0b0000,
            // gap
            MRO = 0b0010,
            MRO_TL1 = 0b0011,
            WO = 0b0100,
            // gap
            MRO_TL0 = 0b0110,
            MRO_TL01 = 0b0111,
            RO = 0b1000,
            RO_uX = 0b1001,
            RO_pX = 0b1010,
            RO_puX = 0b1011,
            RW = 0b1100,
            RW_uX = 0b1101,
            RW_pX = 0b1110,
            RW_puX = 0b1111,
        }

        public enum TranslationTableFormat
        {
            // gap
            AArch32 = 0b01,
            AArch64 = 0b10,
            AArch32_64 = 0b11,
        }

        public enum AddressSize
        {
            Bits32 = 0,
            Bits36 = 1,
            Bits40 = 2,
            Bits42 = 3,
            Bits44 = 4,
            Bits48 = 5,
            Bits52 = 6,
            Bits56 = 7,
        }

        public enum HardwareTranslationTableUpdate
        {
            NoFlagUpdates = 0b00,
            AccessFlag = 0b01,
            AccessFlagAndDirtyState = 0b10,
            AccessFlagAndDirtyStateAndTableAccessFlag = 0b11,
        }

        public enum EndiannessSupport
        {
            MixedEndian = 0b00,
            // gap
            LittleEndian = 0b10,
            BigEndian = 0b11,
        }

        public enum StallModelSupport
        {
            StallAndTerminateSupported = 0b00,
            StallNotSupported = 0b01,
            StallForced = 0b10,
        }

        public enum StreamTableLevel
        {
            Linear = 0b00,
            TwoLevel = 0b01,
        }

        public enum BreakBeforeMakeLevel
        {
            Level0 = 0b00,
            Level1 = 0b01,
            Level2 = 0b10,
        }

        public enum VirtualAddressExtend
        {
            Bits48 = 0b00,
            Bits52 = 0b01,
            Bits56 = 0b10,
        }

        public enum Stage1TranslationGranule // also CD.TG1
        {
            // gap
            KB16 = 0b01,
            KB4 = 0b10,
            KB64 = 0b11,
        }

        public enum Stage2TranslationGranule // also CD.TG0
        {
            KB4 = 0b00,
            KB64 = 0b01,
            KB16 = 0b10,
        }

        public enum TlbInvalidationGranule
        {
            Any = 0b00,
            KB4 = 0b01,
            KB16 = 0b10,
            KB64 = 0b11,
        }

        public enum AddressTranslationType
        {
            // gap
            VAtoIPA = 0b01,
            IPAtoPA = 0b10, // RESERVED for VATOS
            VAtoPA = 0b11, // RESERVED for VATOS
        }

        public enum PermissionInterpretation : byte
        {
            NoAccess = 0b000,
            ReadOnly = 0b001,
            ExecuteOnly = 0b010,
            ReadExecute = 0b011,
            ReadWrite = 0b101,
            ReadWriteExecute = 0b111,
        }

        public enum AccessPermission
        {
            PrivReadWrite = 0b00,
            ReadWrite = 0b01,
            PrivRead = 0b10,
            ReadOnly = 0b11,
        }

        public enum TableAccessPermission
        {
            NoEffect = 0b00,
            RemoveUnprivReadWrite = 0b01,
            RemoveWrite = 0b10,
            RemoveUnprivReadWriteAndPrivWrite = 0b11,
        }

        public enum CompletionSignal
        {
            None = 0b00,
            Irq = 0b01,
            Sev = 0b10,
        }

        public enum Opcode : byte
        {
            // gap 0x00
            CMD_PREFETCH_CONFIG = 0x01,
            CMD_PREFETCH_ADDR = 0x02,
            CMD_CFGI_STE = 0x03,
            CMD_CFGI_STE_RANGE = 0x04, // alias for CMD_CFGI_ALL
            CMD_CFGI_CD = 0x05,
            CMD_CFGI_CD_ALL = 0x06,
            CMD_CFGI_VMS_PIDM = 0x07,
            // gap 0x08-0x0A
            CMD_TLBI_NH_ALL = 0x10,
            CMD_TLBI_NH_ASID = 0x11,
            CMD_TLBI_NH_VA = 0x12,
            CMD_TLBI_NH_VAA = 0x13,
            // gap 0x14-0x17
            CMD_TLBI_EL3_ALL = 0x18,
            // gap 0x19
            CMD_TLBI_EL3_VA = 0x1A,
            // gap 0x1B-0x1F
            CMD_TLBI_EL2_ALL = 0x20,
            CMD_TLBI_EL2_ASID = 0x21,
            CMD_TLBI_EL2_VA = 0x22,
            CMD_TLBI_EL2_VAA = 0x23,
            // gap 0x24-0x27
            CMD_TLBI_S12_VMALL = 0x28,
            // gap 0x29
            CMD_TLBI_S2_IPA = 0x2A,
            // gap 0x2B-0x2F
            CMD_TLBI_NSNH_ALL = 0x30,
            // gap 0x31-0x3F
            CMD_ATC_INV = 0x40,
            CMD_PRI_RESP = 0x41,
            // gap 0x42-0x43
            CMD_RESUME = 0x44,
            CMD_STALL_TERM = 0x45,
            CMD_SYNC = 0x46,
            // gap 0x47-0x4F
            CMD_TLBI_S_EL2_ALL = 0x50,
            CMD_TLBI_S_EL2_ASID = 0x51,
            CMD_TLBI_S_EL2_VA = 0x52,
            CMD_TLBI_S_EL2_VAA = 0x53,
            // gap 0x54-0x57
            CMD_TLBI_S_S12_VMALL = 0x58,
            // gap 0x59
            CMD_TLBI_S_S2_IPA = 0x5A,
            // gap 0x5B-0X5F
            CMD_TLBI_SNH_ALL = 0x60,
            // gap 0x61-0x6F
            CMD_DPTI_ALL = 0x70,
            // gap 0x71-0x72
            CMD_DPTI_PA = 0x73,
            // gap 0x74-0xff; 0x80-0x8f are IMPLEMENTATION DEFINED
        }

        public enum CommandError
        {
            None = 0x0,
            Illegal = 0x1,
            Abort = 0x2,
            ATCInvalidationSync = 0x3,
        }
#pragma warning restore

        [AttributeUsage(AttributeTargets.Class)]
        private class CommandAttribute : Attribute
        {
            public CommandAttribute(Opcode opcode)
            {
                Opcode = opcode;
            }

            public readonly Opcode Opcode;
        }
    }
}
