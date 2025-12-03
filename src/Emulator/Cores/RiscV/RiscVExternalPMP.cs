//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    /// <summary>
    /// A C# implementation of the RiscV Physical Memory Protection Scheme specified in the specification
    /// </summary>
    /// <remarks>
    /// This is just a reimplementation of the PMP logic in <c>tlib/arch/riscv/pmp.c</c>.
    /// It is intended to be used as a base to simplify implementing cores with a modified
    /// PMP scheme.
    /// </remarks>
    public class RiscVExternalPMP : ExternalPMPBase
    {
        public RiscVExternalPMP(uint numberOfPMPEntries = 64)
        {
            this.numberOfPMPEntries = numberOfPMPEntries;
            isPMPDisabled = true;
        }

        public override void RegisterCPU(BaseRiscV cpu)
        {
            base.RegisterCPU(cpu);
            napotGrain = (int)cpu.MinimalPMPNapotInBytes >> 4;
            addressBits = cpu.PMPNumberOfAddrBits;

            entriesPerCSR = cpu is RiscV64 ? 8 : 4;

            configRegisters = new List<ConfigRegister>((int)numberOfPMPEntries);
            addressRegisters = new List<ulong>((int)numberOfPMPEntries);
            addressRanges = new List<Range>((int)numberOfPMPEntries);

            for(var i = 0; i < numberOfPMPEntries; i++)
            {
                addressRegisters.Add(0);
                configRegisters.Add(CreateConfigRegister());
                addressRanges.Add(new Range(0, UInt64.MaxValue));
            }
        }

        public override byte GetAccess(ulong address, ulong size, AccessType accessType)
        {
            var priv = GetEffectivePrivilege(accessType);

            if(isPMPDisabled)
            {
                return EncodePermissions(true, true, true);
            }

            // If no rules match, machine mode have full access, other modes have none
            var permissions = priv == PrivilegeLevel.Machine ? EncodePermissions(true, true, true) : (byte)0;

            for(var i = 0; i < numberOfPMPEntries; i++)
            {
                if(configRegisters[i].Mode == AddressMatchingMode.Off)
                {
                    continue;
                }
                var isFirstByteInRange = addressRanges[i].Contains(address);
                var isLastByteInRange = addressRanges[i].Contains(address + size - 1);
                if(isFirstByteInRange != isLastByteInRange)
                {
                    // If an access partially matches a pmp entry, it is always denied
                    return EncodePermissions(false, false, false);
                }
                if(isFirstByteInRange && isLastByteInRange)
                {
                    // Only locked entries are enforced in machine mode
                    if(priv != PrivilegeLevel.Machine || configRegisters[i].Lock)
                    {
                        permissions = configRegisters[i].EncodeAccess();
                    }
                    break;
                }
            }

            return permissions;
        }

        // This function is used by tlib to get a pmp entry that could potentially match
        // a full page. The actual page check is done at the tlib level using the values set by
        // SetPMPAddress
        public override bool TryGetOverlappingRegion(ulong address, ulong size, uint startingIndex, out uint overlappingIndex)
        {
            address = MaskAddress(address);
            var accessRange = new Range(address, size);
            for(var i = startingIndex; i < numberOfPMPEntries; i++)
            {
                if(configRegisters[(int)i].Mode == AddressMatchingMode.Off)
                {
                    continue;
                }
                if(addressRanges[(int)i].Contains(accessRange))
                {
                    overlappingIndex = i;
                    return true;
                }
            }
            overlappingIndex = UInt32.MaxValue;
            return false;
        }

        public override bool IsAnyRegionLocked()
        {
            return configRegisters.Any(config => config.Lock);
        }

        public override void Reset()
        {
            for(var i = 0; i < numberOfPMPEntries; i++)
            {
                addressRegisters[i] = 0;
                configRegisters[i].Reset();
                addressRanges[i] = new Range(0, UInt64.MaxValue);
            }
            isPMPDisabled = true;
        }

        public override void ConfigCSRWrite(uint registerIndex, ulong value)
        {
            var baseEntry = (int)registerIndex * entriesPerCSR;
            if(baseEntry + entriesPerCSR > numberOfPMPEntries)
            {
                throw new RecoverableException($"Attempted to access invalid PMP config register {registerIndex} (number of config registers: {numberOfPMPEntries / entriesPerCSR})");
            }
            if(entriesPerCSR == 8)
            {
                // On RV64 there are only even config CSRs
                DebugHelper.Assert(registerIndex % 2 == 0);
                baseEntry = baseEntry / 2;
            }
            for(var i = 0; i < entriesPerCSR; i++)
            {
                var entry = baseEntry + i;
                if(!IsEntryLocked(entry))
                {
                    configRegisters[entry].Update((byte)BitHelper.GetValue(value, i * 8, 8));
                    UpdateRule(entry);
                }
            }
            cpu.FlushTlb();
        }

        public override ulong ConfigCSRRead(uint registerIndex)
        {
            var baseEntry = (int)registerIndex * entriesPerCSR;
            if(baseEntry + entriesPerCSR > numberOfPMPEntries)
            {
                throw new RecoverableException($"Attempted to access invalid PMP config register {registerIndex} (number of config registers: {numberOfPMPEntries / entriesPerCSR})");
            }
            if(cpu is RiscV64)
            {
                // On RV64 there are only even config CSRs
                DebugHelper.Assert(registerIndex % 2 == 0);
                baseEntry = baseEntry / 2;
            }
            ulong res = 0;
            for(var i = 0; i < entriesPerCSR; i++)
            {
                var entry = baseEntry + i;
                res |= (uint)(configRegisters[(int)entry].ToByte() << i * 8);
            }
            return res;
        }

        public override void AddressCSRWrite(uint registerIndex, ulong value)
        {
            if(registerIndex >= numberOfPMPEntries)
            {
                throw new RecoverableException($"Attempted to access invalid PMP address register {registerIndex} (numberOfPMPEntries: {numberOfPMPEntries})");
            }
            if(IsEntryLocked((int)registerIndex))
            {
                return;
            }
            addressRegisters[(int)registerIndex] = MaskAddress(value);
            UpdateRule((int)registerIndex);
            cpu.FlushTlb();
        }

        public override ulong AddressCSRRead(uint registerIndex)
        {
            if(registerIndex >= numberOfPMPEntries)
            {
                throw new RecoverableException($"Attempted to access invalid PMP configuration register {registerIndex} (numberOfPMPEntries: {numberOfPMPEntries})");
            }
            return addressRegisters[(int)registerIndex];
        }

        protected virtual void UpdateRule(int index)
        {
            var cfg = configRegisters[index];
            var addr = addressRegisters[index];
            ulong prevAddr = 0;
            var range = new Range(0, UInt64.MaxValue);

            // The first pmp entry treats the previous address as 0
            if(index >= 1)
            {
                prevAddr = addressRegisters[index - 1];
            }

            switch(cfg.Mode)
            {
            case AddressMatchingMode.Off:
                break;
            case AddressMatchingMode.TopOfRange:
                var startAddress = MaskAddress(prevAddr << 2);
                range = startAddress.To(MaskAddress((addr << 2) - 1));
                break;
            case AddressMatchingMode.NaturallyAlignedFourByte:
                range = new Range(MaskAddress(addr << 2), 4);
                break;
            case AddressMatchingMode.NaturallyAlignedPowerOfTwo:
                range = DecodeNAPOT(addr);
                break;
            }

            this.DebugLog("Updating PMP rule {0}, start: 0x{1:X}, end: 0x{2:X}, config: {3}", index, range.StartAddress, range.EndAddress, cfg);

            addressRanges[index] = range;
            cpu.SetPMPAddress((uint)index, range.StartAddress, range.EndAddress);

            // If all rules are off the PMP is inactive
            isPMPDisabled = configRegisters.All(config => config.Mode == AddressMatchingMode.Off);
        }

        protected Range DecodeNAPOT(ulong addressReg)
        {
            if(addressReg == UInt64.MaxValue)
            {
                return new Range(0, UInt64.MaxValue);
            }

            // There is no intrinsic based count trailing ones function availible in mono
            // so hack it with a loop for now. This function is called infrequently so it does
            // not matter much
            var bits = BitHelper.GetBits(addressReg);
            var grain = 0;
            while(grain < 64)
            {
                if(!bits[grain])
                {
                    break;
                }
                grain++;
            }

            if(grain < napotGrain)
            {
                this.WarningLog("Grain size {0} is smaller than the minumum size {1}, setting it to the minumum value", grain, napotGrain);
                grain = napotGrain;
            }
            var size = (2UL << (grain + 2)) - 1;

            return new Range(MaskAddress(BitHelper.GetMaskedValue(addressReg, grain + 1, 64 - (grain + 1)) << 2), size);
        }

        protected virtual bool IsEntryLocked(int index)
        {
            if(configRegisters[index].Lock)
            {
                return true;
            }
            // No next entry for the top entry
            if(index + 1 >= numberOfPMPEntries)
            {
                return false;
            }
            // When in Top of range mode, we need to check the next entry's lock bit
            if(configRegisters[index + 1].Mode == AddressMatchingMode.TopOfRange)
            {
                return configRegisters[index + 1].Lock;
            }
            else
            {
                return false;
            }
        }

        /// <remarks>
        /// This function does not do anything more than 'new ConfigRegister()' on its own,
        /// but it is used to make it easier to modify part of the behavior in a derived class
        /// </remarks>
        protected virtual ConfigRegister CreateConfigRegister()
        {
            return new ConfigRegister();
        }

        protected ulong MaskAddress(ulong address)
        {
            return BitHelper.GetValue(address, 0, (byte)addressBits);
        }

        protected PrivilegeLevel GetEffectivePrivilege(AccessType accessType)
        {
            var priv = cpu.CurrentPrivilegeLevel;
            // If mstatus.MPRV is set the effective privilege for loads and stores are determined by mstatus.MPP
            ulong mstatus = cpu.GetRegister(MSTATUSIndex);
            // MPRV and MPP have the same offsets in both RiscV32 and RiscV64
            if(BitHelper.IsBitSet(mstatus, (byte)RiscV32.MstatusFieldOffsets.MPRV) && (accessType == AccessType.Read || accessType == AccessType.Write))
            {
                priv = (PrivilegeLevel)BitHelper.GetValue(mstatus, (byte)RiscV32.MstatusFieldOffsets.MPP, 2);
            }
            return priv;
        }

        protected List<ConfigRegister> configRegisters;
        protected List<ulong> addressRegisters;
        protected List<Range> addressRanges;

        protected bool isPMPDisabled;
        protected int napotGrain;
        protected uint addressBits;
        protected int entriesPerCSR;
        protected readonly uint numberOfPMPEntries;

        protected const int MSTATUSIndex = 833;

        protected class ConfigRegister
        {
            public ConfigRegister()
            {
                this.Reset();
            }

            public virtual void Update(byte value)
            {
                Lock = BitHelper.IsBitSet(value, ConfigLockOffset);
                Mode = (AddressMatchingMode)BitHelper.GetValue(value, ConfigAddressMatchingModeOffset, 2);
                Execute = BitHelper.IsBitSet(value, ConfigExecuteOffset);
                Write = BitHelper.IsBitSet(value, ConfigWriteOffset);
                Read = BitHelper.IsBitSet(value, ConfigReadOffset);
            }

            public virtual byte ToByte()
            {
                byte result = 0;
                BitHelper.SetBit(ref result, ConfigLockOffset, Lock);

                result |= (byte)((byte)Mode << ConfigAddressMatchingModeOffset);

                return (byte)(result | EncodeAccess());
            }

            public virtual void Reset()
            {
                Lock = false;
                Mode = AddressMatchingMode.Off;
                Execute = false;
                Write = false;
                Read = false;
            }

            public virtual byte EncodeAccess()
            {
                return EncodePermissions(Read, Write, Execute);
            }

            public override string ToString()
            {
                return $"{nameof(ConfigRegister)}<Lock: {Lock}, Mode: {Mode}, Execute: {Execute}, Write: {Write}, Read: {Read}>";
            }

            public bool Lock { get; set; }

            public AddressMatchingMode Mode { get; set; }

            public bool Execute { get; set; }

            public bool Write { get; set; }

            public bool Read { get; set; }

            protected const byte ConfigLockOffset = 7;
            protected const byte ConfigAddressMatchingModeOffset = 3;
            protected const byte ConfigExecuteOffset = 2;
            protected const byte ConfigWriteOffset = 1;
            protected const byte ConfigReadOffset = 0;
        }

        protected enum AddressMatchingMode : byte
        {
            Off = 0,
            TopOfRange = 1,
            NaturallyAlignedFourByte = 2,
            NaturallyAlignedPowerOfTwo = 3,
        }
    }
}
