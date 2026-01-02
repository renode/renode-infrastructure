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
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.DoubleWordToByte | AllowedTranslation.DoubleWordToWord)]
    public class NXP_XRDC : BasicDoubleWordPeripheral, IPeripheralContainer<IPeripheral, NumberRegistrationPoint<uint[]>>, IKnownSize
    {
        public NXP_XRDC(IMachine machine, IPeripheral[] busInitiators, uint[] numberOfMemoryRegionDescriptors, uint numberOfPeripheralsControllers, uint numberOfDomainIds) : base(machine)
        {
            if(numberOfDomainIds > MaxNumberOfDomainIds)
            {
                throw new ConstructionException($"'{nameof(numberOfDomainIds)}' no in [0, {MaxNumberOfDomainIds}] range");
            }
            this.busInitiators = busInitiators;
            this.numberOfMemoryControllers = (uint)numberOfMemoryRegionDescriptors.Length;
            this.numberOfMasterDomains = (uint)busInitiators.Length;
            this.numberOfPeripheralsControllers = numberOfPeripheralsControllers;
            this.numberOfDomainIds = numberOfDomainIds;

            memoryRegionControllers = new MemoryRegionController[numberOfMemoryControllers];
            for(var i = 0u; i < numberOfMemoryControllers; ++i)
            {
                memoryRegionControllers[i] = new MemoryRegionController(i, numberOfMemoryRegionDescriptors[i]);
            }

            busInitiatorTypes = new BusInitiatorType[numberOfMasterDomains];
            masterDomains = new MasterDomainDescriptor[numberOfMasterDomains];

            peripheralAccessControllers = new PeripheralAccessController[numberOfPeripheralsControllers];
            for(var i = 0u; i < numberOfPeripheralsControllers; ++i)
            {
                peripheralAccessControllers[i] = new PeripheralAccessController(i);
            }

            memoryRegionErrorAddress = new IValueRegisterField[numberOfMemoryControllers];
            peripheralAccessErrorAddress = new IValueRegisterField[numberOfPeripheralsControllers];

            for(var i = 0; i < numberOfMasterDomains; ++i)
            {
                busInitiatorTypes[i] = (busInitiators[i] is ICPU) ? BusInitiatorType.Core : BusInitiatorType.Noncore;
            }
            DefineRegisters();
        }

        public override void Reset()
        {
            for(var i = 0; i < NumberOfSemaphores; ++i)
            {
                semaphores[i] = null;
            }
            base.Reset();
        }

        public bool RequestTransactionWithDomainId(uint domainId, ulong address, MpuAccess access, bool isSecure = false, bool isPrivileged = false)
        {
            if(!globalValid.Value)
            {
                this.DebugLog("global valid not set");
                return true;
            }

            var target = machine.SystemBus.WhatPeripheralIsAt(address);
            this.DebugLog("target found: {0}", target.GetName());

            if(target is IMemory memory)
            {
                foreach(var controller in memoryRegionControllers)
                {
                    if(!controller.Contains(memory))
                    {
                        continue;
                    }
                    this.DebugLog("memory controller found");

                    foreach(var region in controller.Descriptors)
                    {
                        if(region.StartAddress > address || address >= region.EndAddress)
                        {
                            this.DebugLog("target 0x{0:X} not in region <0x{1:X}, 0x{2:X}>", address, region.StartAddress, region.EndAddress);
                            continue;
                        }

                        if(!region.valid.Value)
                        {
                            this.DebugLog("target 0x{0:X} in not valid region <0x{1:X}, 0x{2:X}>", address, region.StartAddress, region.EndAddress);
                            continue;
                        }
                        this.DebugLog("target 0x{0:X} in valid region <0x{1:X}, 0x{2:X}>", address, region.StartAddress, region.EndAddress);

                        var semaphoreNumber = region.semaphoreNumber.Value;
                        if(access == MpuAccess.Write && region.semaphoreEnable.Value)
                        {
                            if(semaphores[semaphoreNumber].HasValue)
                            {
                                this.DebugLog("memory region found and semaphore #{2} locked (owner: {0}, this: {1})", semaphores[semaphoreNumber], domainId, semaphoreNumber);
                                return semaphores[semaphoreNumber] == domainId;
                            }
                            else
                            {
                                this.DebugLog("semaphore not locked");
                            }
                        }

                        var acp = (uint)region.domainAccessControlPolicy[domainId].Value;
                        this.DebugLog("checking ACP 0x{0:X} for {1}, secure: {2}, privileged: {2}", acp, access, isSecure, isPrivileged);
                        if(AllowsAccess(acp, access, isSecure, isPrivileged))
                        {
                            this.DebugLog("memory region found and ACP passed");
                            return true;
                        }
                    }

                    break;
                }
            }
            else
            {
                foreach(var controller in peripheralAccessControllers)
                {
                    if(!controller.Contains(target))
                    {
                        continue;
                    }
                    this.DebugLog("peripheral controller found");

                    // TODO

                    break;
                }
            }

            this.DebugLog("target ACP not found");
            return true; // TODO ?
        }

        public bool RequestTransactionWithMasterId(uint masterId, ulong address, MpuAccess access, bool isSecure = false, bool isPrivileged = false)
        {
            if(masterId >= 16)
            {
                this.ErrorLog("invalid masterId {0}", masterId);
                return false;
            }
            if(!globalValid.Value)
            {
                this.DebugLog("global valid not set");
                return true;
            }
            if(!masterIdDomains[masterId].HasValue)
            {
                this.ErrorLog("doamin not defined for masterId {0}", masterId);
                return false;
            }
            var domainId = masterDomains[masterIdDomains[masterId].Value].domainId.Value;
            return RequestTransactionWithDomainId((uint)domainId, address, access, isSecure, isPrivileged);
        }


        public bool RequestTransactionWithInitiator(IPeripheral initiator, ulong address, MpuAccess access, bool isSecure = false, bool isPrivileged = false)
        {
            if(!globalValid.Value)
            {
                this.DebugLog("global valid not set");
                return true;
            }

            // get from MDAC initiator's DID, privileged and secure attributes
            if(!TryGetDomain(initiator, out var descriptor))
            {
                this.ErrorLog("Unexpected initiator ({0}) requested transaction", initiator.GetName());
            }
            var domainId = (uint)descriptor.domainId.Value;
            return RequestTransactionWithDomainId(domainId, address, access, isSecure, isPrivileged);
        }

        public bool TryGetMasterId(IPeripheral initiator, out uint masterId)
        {
            masterId = default(uint);
            for(var i = 0; i < busInitiators.Length; i++)
            {
                if(busInitiators[i] == initiator)
                {
                    for(var j = 0; j < masterIdDomains.Length; ++j)
                    {
                        if(i == masterIdDomains[j])
                        {
                            masterId = (uint)j;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void Register(IPeripheral peripheral, uint controller, uint slotId)
        {
            if(peripheral is IMemory memory)
            {
                if(controller >= numberOfMemoryControllers)
                {
                    throw new RecoverableException($"Controller ID out of [0-{numberOfMemoryControllers}) range");
                }

                memoryRegionControllers[controller].Register(memory, slotId);
                return;
            }

            if(controller >= numberOfPeripheralsControllers)
            {
                throw new RecoverableException($"Controller ID out of [0-{numberOfPeripheralsControllers}) range");
            }

            if(slotId >= SlotValueLimit)
            {
                throw new RecoverableException($"Slot value out of [0-{SlotValueLimit}) range");
            }

            peripheralAccessControllers[controller].Register(peripheral, slotId);
        }

        public void Register(IPeripheral peripheral, NumberRegistrationPoint<uint[]> registrationPoint)
        {
            if(registrationPoint.Address.Length != 2)
            {
                throw new ConstructionException($"Peripheral registration for `{nameof(NXP_XRDC)}` requires exactly 2 values");
            }
            Register(peripheral, registrationPoint.Address[0], registrationPoint.Address[1]);
        }

        public void Unregister(IPeripheral peripheral)
        {
            if(peripheral is IMemory memory)
            {
                Array.ForEach(memoryRegionControllers, controller => controller.Unregister(memory));
                return;
            }
            Array.ForEach(peripheralAccessControllers, controller => controller.Unregister(peripheral));
        }

        public void SetSemaphore(uint index, uint? domainId)
        {
            if(index >= NumberOfSemaphores)
            {
                this.WarningLog("Attempted to set non-existing semaphore (#{0})", index);
                return;
            }
            if(domainId.HasValue && domainId > numberOfDomainIds)
            {
                this.WarningLog("Attempted to set semaphore #{0} for non-existing domain ID (#{1})", index, domainId);
                return;
            }
            semaphores[index] = domainId;
        }

        public uint? GetDomainId(IPeripheral initiator)
        {
            if(!TryGetDomain(initiator, out var descriptor))
            {
                return null;
            }
            return (uint?)descriptor.domainId.Value;
        }

        public IEnumerable<NumberRegistrationPoint<uint[]>> GetRegistrationPoints(IPeripheral peripheral)
        {
            if(peripheral is IMemory memory)
            {
                return memoryRegionControllers.SelectMany(controller => controller.GetRegistrationPoints(memory));
            }
            return peripheralAccessControllers.SelectMany(controller => controller.GetRegistrationPoints(peripheral));
        }

        public long Size => 0x3000;

        public IEnumerable<IRegistered<IPeripheral, NumberRegistrationPoint<uint[]>>> Children
        {
            get => peripheralAccessControllers.SelectMany(controller => controller.Children)
                .Concat(memoryRegionControllers.SelectMany(controller => controller.Children));
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this, 0x0000008A)
                .WithFlag(0, out globalValid, name: "GVLD")
                .WithValueField(1, 4, FieldMode.Read, name: "HRL")
                .WithReservedBits(5, 2)
                .WithFlag(7, FieldMode.Read, name: "MRF")
                .WithFlag(8, FieldMode.Read, name: "VAW")
                .WithReservedBits(9, 21)
                .WithFlag(30, name: "LK1")
                .WithReservedBits(31, 1)
            ;

            Registers.HardwareConfiguration0.Define(this, 0x12000000)
                .WithValueField(0, 8, FieldMode.Read,
                    valueProviderCallback: _ => numberOfDomainIds - 1,
                    name: "NDID"
                )
                .WithValueField(8, 8, FieldMode.Read,
                    valueProviderCallback: _ => numberOfMasterDomains - 1,
                    name: "NMSTR"
                )
                .WithValueField(16, 8, FieldMode.Read,
                    valueProviderCallback: _ => numberOfMemoryControllers - 1,
                    name: "NMRC"
                )
                .WithValueField(24, 4, FieldMode.Read,
                    valueProviderCallback: _ => numberOfPeripheralsControllers - 1,
                    name: "NPAC"
                )
                .WithValueField(28, 4, FieldMode.Read, name: "MID")
            ;

            Registers.HardwareConfiguration1.Define(this)
                .WithValueField(0, 4, FieldMode.Read,
                    valueProviderCallback: _ => 0x0, // TODO: determine DID based on initiator (core?)
                    name: "DID"
                )
                .WithReservedBits(4, 28)
            ;

            Registers.HardwareConfiguration2.Define(this, 0x0) // NOTE: PID based domain assignment is not supported
                .WithFlags(0, 31, FieldMode.Read, name: "PIDP")
            ;

            // MDAC

            Registers.MasterDomainAssignmentConfiguration.DefineMany(this,
                count: (numberOfMasterDomains + 3) / 4,
                setup: (register, i) =>
            {
                for(var j = 0; j < 4 && i * 4 + j < numberOfMasterDomains; ++j)
                {
                    var offset = j * 8;
                    var isNoncore = busInitiatorTypes[j] == BusInitiatorType.Noncore;
                    register
                        .WithValueField(0 + offset, 4, FieldMode.Read,
                            valueProviderCallback: _ => 1, // NOTE: only 1 is supported
                            name: "NMDAR"
                        )
                        .WithReservedBits(4 + offset, 3)
                        .WithFlag(7 + offset, FieldMode.Read,
                            valueProviderCallback: _ => isNoncore,
                            name: "NCM"
                        )
                    ;
                }
            });

            Registers.MemoryRegionConfiguration.DefineMany(this,
                count: (numberOfMemoryControllers + 3) / 4,
                setup: (register, i) =>
            {
                for(var j = 0; j < 4 && i * 4 + j < numberOfMemoryControllers; ++j)
                {
                    var region_i = i * 4 + j;
                    var offset = j * 8;
                    register
                        .WithValueField(0 + offset, 5, FieldMode.Read,
                            valueProviderCallback: _ => memoryRegionControllers[region_i].NumberOfDescriptors,
                            name: "NMRGD"
                        )
                        .WithReservedBits(5 + offset, 3)
                    ;
                }
            });

            Registers.DomainErrorLocation.DefineMany(this, count: 5, setup: (reg, i) => reg
                .WithValueField(0, 16, FieldMode.Read, name: "MRCINST")
                .WithValueField(16, 4, FieldMode.Read, name: "PACINST")
                .WithReservedBits(20, 12)
            );

            // PAC error

            ((Registers)(Registers.DomainError + 0x000)).DefineMany(this,
                count: numberOfMemoryControllers,
                stepInBytes: 0x10,
                setup: (register, i) => register
                    .WithValueField(0, 32, out memoryRegionErrorAddress[i], FieldMode.Read, name: "EADDR")
            );

            ((Registers)(Registers.DomainError + 0x004)).DefineMany(this,
                count: numberOfMemoryControllers,
                stepInBytes: 0x10,
                setup: (register, i) => register
                    .WithValueField(0, 4, FieldMode.Read, name: "EDID")
                    .WithReservedBits(4, 4)
                    .WithValueField(8, 3, FieldMode.Read, name: "EATR")
                    .WithFlag(11, FieldMode.Read, name: "ERW")
                    .WithReservedBits(12, 12)
                    .WithValueField(24, 3, FieldMode.Read, name: "EPORT")
                    .WithReservedBits(27, 3)
                    .WithValueField(30, 2, FieldMode.Read, name: "EST")
            );

            // MRC error

            ((Registers)(Registers.DomainError + 0x100)).DefineMany(this,
                count: numberOfPeripheralsControllers,
                stepInBytes: 0x10,
                setup: (register, i) => register
                    .WithValueField(0, 32, out peripheralAccessErrorAddress[i], FieldMode.Read, name: "EADDR")
            );

            ((Registers)(Registers.DomainError + 0x104)).DefineMany(this,
                count: numberOfPeripheralsControllers,
                stepInBytes: 0x10,
                setup: (register, i) => register
                    .WithValueField(0, 4, FieldMode.Read, name: "EDID")
                    .WithReservedBits(4, 4)
                    .WithValueField(8, 3, FieldMode.Read, name: "EATR")
                    .WithFlag(11, FieldMode.Read, name: "ERW")
                    .WithReservedBits(12, 12)
                    .WithValueField(24, 3, FieldMode.Read, name: "EPORT")
                    .WithReservedBits(27, 3)
                    .WithValueField(30, 2, FieldMode.Read, name: "EST")
            );

            for(var i = 0; i < numberOfMasterDomains; ++i)
            {
                if(busInitiatorTypes[i] == BusInitiatorType.Noncore)
                {
                    continue;
                }
                ((Registers)(Registers.ProcessIdentifier + 0x4 * i)).Define(this)
                    .WithValueField(0, 6, name: "PID")
                    .WithReservedBits(6, 22)
                    .WithFlag(28, name: "TSM")
                    .WithValueField(29, 2, name: "LK2")
                    .WithReservedBits(31, 1)
                ;
            }

            Registers.MasterDomainAssignment.DefineMany(this,
                count: numberOfMasterDomains,
                stepInBytes: 0x20, setup: (register, i) =>
            {
                var isCore = busInitiatorTypes[i] == BusInitiatorType.Core;
                masterDomains[i] = isCore
                    ? (MasterDomainDescriptor)new CoreMasterDomainDescriptor()
                    : (MasterDomainDescriptor)new NoncoreMasterDomainDescriptor()
                ;
                register
                    .WithValueField(0, 3, out masterDomains[i].domainId, name: "DID")
                    .WithReservedBits(3, 1)
                    .If(isCore)
                    .Then(reg =>
                    {
                        var masterDomain = masterDomains[i] as CoreMasterDomainDescriptor;
                        reg
                            .WithValueField(4, 2, out masterDomain.domainIdSelect, name: "DIDS")
                            .WithValueField(6, 2, out masterDomain.processIdEnable, name: "PE")
                            .WithValueField(8, 6, out masterDomain.processIdMask, name: "PIDM")
                            .WithReservedBits(14, 2)
                            .WithValueField(16, 6, out masterDomain.processId, name: "PID")
                            .WithReservedBits(22, 7)
                        ;
                    })
                    .Else(reg =>
                    {
                        var masterDomain = masterDomains[i] as NoncoreMasterDomainDescriptor;
                        reg
                            .WithValueField(4, 2, out masterDomain.privilegedAttribute, name: "PA")
                            .WithValueField(6, 2, out masterDomain.secureAttribute, name: "SA")
                            .WithFlag(8, out masterDomain.domainIdBypass, name: "DIDB")
                            .WithReservedBits(9, 20)
                        ;
                    })
                    .WithFlag(29, FieldMode.Read,
                        valueProviderCallback: _ => !isCore,
                        name: "DFMT"
                    )
                    .WithFlag(30, out masterDomains[i].lockConfig, name: "LK1")
                    .WithFlag(31, out masterDomains[i].valid, name: "VLD")
                ;
            });

            // PAC

            ((Registers)(Registers.PeripheralDomainAccessControl + 0x0)).DefineMany(this,
                count: SlotValueLimit,
                stepInBytes: 0x8,
                setup: (register, slotId) => register
                    .WithValueFields(0, 3, 5, name: "DxACP")
                    .WithReservedBits(15, 9)
                    .WithValueField(24, 4, name: "SNUM")
                    .WithReservedBits(28, 2)
                    .WithFlag(30, name: "SE")
                    .WithReservedBits(31, 1)
            );

            ((Registers)(Registers.PeripheralDomainAccessControl + 0x4)).DefineMany(this,
                count: SlotValueLimit,
                stepInBytes: 0x8,
                setup: (register, slotId) => register
                    .WithReservedBits(0, 29)
                    .WithValueField(29, 2, name: "LK2")
                    .WithFlag(31, name: "VLD")
            );

            // MRC

            for(var i = 0u; i < numberOfMemoryControllers; ++i)
            {
                for(var j = 0u; j < memoryRegionControllers[i].NumberOfDescriptors; ++j)
                {
                    var n = i;
                    var m = j;
                    ((Registers)((uint)Registers.MemoryRegionDescriptor + n * 0x200 + m * 0x20 + 0)).Define(this)
                        .WithReservedBits(0, 5)
                        .WithValueField(5, 27, out memoryRegionControllers[n][m].startAddress,
                            changeCallback: (oldValue, _) =>
                            {
                                if(memoryRegionControllers[n][m].lockConfig.Value != LockConfig.Unlocked)
                                {
                                    memoryRegionControllers[n][m].startAddress.Value = oldValue;
                                }
                            },
                            name: "SRTADDR"
                        )
                    ;
                    ((Registers)((uint)Registers.MemoryRegionDescriptor + n * 0x200 + m * 0x20 + 0x4)).Define(this, 0x1f)
                        .WithReservedBits(0, 5)
                        .WithValueField(5, 27, out memoryRegionControllers[n][m].endAddress,
                            changeCallback: (oldValue, _) =>
                            {
                                if(memoryRegionControllers[n][m].lockConfig.Value != LockConfig.Unlocked)
                                {
                                    memoryRegionControllers[n][m].endAddress.Value = oldValue;
                                }
                            },
                            name: "ENDADDR"
                        )
                    ;
                    ((Registers)((uint)Registers.MemoryRegionDescriptor + n * 0x200 + m * 0x20 + 0x8)).Define(this) // 0x4000003F
                        .WithValueFields(0, 3, (int)numberOfDomainIds, out memoryRegionControllers[n][m].domainAccessControlPolicy,
                            changeCallback: (did, oldValue, _) =>
                            {
                                var currentDid = 0x0; // TODO
                                var lockConfig = memoryRegionControllers[n][m].lockConfig.Value;
                                if(lockConfig != LockConfig.Unlocked &&
                                    !(lockConfig == LockConfig.AccessControlPolicyUnlocked && did == currentDid))
                                {
                                    memoryRegionControllers[n][m].domainAccessControlPolicy[did].Value = oldValue;
                                }
                            },
                            name: "DxACP"
                        )
                        .WithReservedBits(3 * (int)numberOfDomainIds, 24 - 3 * (int)numberOfDomainIds)
                        .WithValueField(24, 4, out memoryRegionControllers[n][m].semaphoreNumber,
                            changeCallback: (oldValue, _) =>
                            {
                                if(memoryRegionControllers[n][m].lockConfig.Value != LockConfig.Unlocked)
                                {
                                    memoryRegionControllers[n][m].semaphoreNumber.Value = oldValue;
                                }
                            },
                            name: "SNUM"
                        )
                        .WithReservedBits(28, 2)
                        .WithFlag(30, out memoryRegionControllers[n][m].semaphoreEnable,
                            changeCallback: (oldValue, _) =>
                            {
                                if(memoryRegionControllers[n][m].lockConfig.Value != LockConfig.Unlocked)
                                {
                                    memoryRegionControllers[n][m].semaphoreEnable.Value = oldValue;
                                }
                            },
                            name: "SE"
                        )
                        .WithReservedBits(31, 1)
                    ;
                    ((Registers)((uint)Registers.MemoryRegionDescriptor + n * 0x200 + m * 0x20 + 0xC)).Define(this)
                        .WithReservedBits(0, 29)
                        .WithEnumField(29, 2, out memoryRegionControllers[n][m].lockConfig,
                            changeCallback: (oldValue, _) =>
                            {
                                if(oldValue != LockConfig.Unlocked)
                                {
                                    memoryRegionControllers[n][m].lockConfig.Value = oldValue;
                                }
                            },
                            name: "LK2"
                        )
                        .WithFlag(31, out memoryRegionControllers[n][m].valid,
                            changeCallback: (oldValue, _) =>
                            {
                                if(memoryRegionControllers[n][m].lockConfig.Value != LockConfig.Unlocked)
                                {
                                    memoryRegionControllers[n][m].valid.Value = oldValue;
                                }
                            },
                            name: "VLD"
                        )
                    ;
                }
            }
        }

        private bool TryGetDomain(IPeripheral initiator, out MasterDomainDescriptor descriptor)
        {
            // get from MDAC initiator's DID, privileged and secure attributes
            for (var i = 0; i < busInitiators.Length; i++)
            {
                if(busInitiators[i] == initiator)
                {
                    descriptor = masterDomains[i];
                    return true;
                }
            }

            descriptor = null;
            return false;
        }

        private bool AllowsAccess(uint domainAccessControlPolicy, MpuAccess access, bool isSecure, bool isPrivileged)
        {
            switch(domainAccessControlPolicy)
            {
            case 0b111:
                return true;
            case 0b110:
                return isSecure || isPrivileged;
            case 0b101:
                return access == MpuAccess.Read || isSecure;
            case 0b100:
                return isSecure || (access == MpuAccess.Read && isPrivileged);
            case 0b011:
                return isSecure;
            case 0b010:
                return isSecure && isPrivileged;
            case 0b001:
                return access == MpuAccess.Read && isSecure;
            case 0b000:
                return false;
            default:
                throw new Exception("Code unreachable");
            }
        }

        private IFlagRegisterField globalValid;

        private readonly MemoryRegionController[] memoryRegionControllers;
        // private readonly MemoryRegionDescriptor[][] memoryRegionDescriptors;
        private readonly MasterDomainDescriptor[] masterDomains;

        private readonly IValueRegisterField[] memoryRegionErrorAddress;
        private readonly IValueRegisterField[] peripheralAccessErrorAddress;

        private readonly uint numberOfMemoryControllers;
        private readonly uint numberOfPeripheralsControllers;
        private readonly uint numberOfMasterDomains;
        private readonly uint numberOfDomainIds;

        private readonly IPeripheral[] busInitiators;
        private readonly PeripheralAccessController[] peripheralAccessControllers;
        private readonly BusInitiatorType[] busInitiatorTypes;
        // private readonly SimpleContainerHelper<IPeripheral> peripheralsContainer;

        private readonly uint?[] semaphores = new uint?[NumberOfSemaphores];
        private readonly uint?[] masterIdDomains = new uint?[16]
        {
            null, null, 1, 3, 5, 8, 7, 0, 4, null, 6, null, null, null, null, null
        };

        private const int SlotValueLimit = 512;
        private const int MaxNumberOfDomainIds = 5;
        private const uint NumberOfSemaphores = 1 << 4;

        public enum Registers
        {
            Control = 0x0,
            HardwareConfiguration0 = 0xF0,
            HardwareConfiguration1 = 0xF4,
            HardwareConfiguration2 = 0xF8,
            MasterDomainAssignmentConfiguration = 0x100,
            MemoryRegionConfiguration = 0x140,
            DomainErrorLocation = 0x200,
            DomainError = 0x400,
            ProcessIdentifier = 0x700,
            MasterDomainAssignment = 0x800,
            PeripheralDomainAccessControl = 0x1000,
            MemoryRegionDescriptor = 0x2000,
        }

        private enum BusInitiatorType
        {
            Core,
            Noncore,
        }

        private enum LockConfig
        {
            Unlocked = 0b00,
            Reserved = 0b01,
            AccessControlPolicyUnlocked = 0b10,
            Locked = 0b11,
        }

        private class MasterDomainDescriptor
        {
            public IValueRegisterField domainId;
            public IFlagRegisterField lockConfig;
            public IFlagRegisterField valid;
        }

        private class CoreMasterDomainDescriptor : MasterDomainDescriptor
        {
            public IValueRegisterField processId;
            public IValueRegisterField processIdMask;
            public IValueRegisterField processIdEnable;
            public IValueRegisterField domainIdSelect;
        }

        private class NoncoreMasterDomainDescriptor : MasterDomainDescriptor
        {
            public IFlagRegisterField domainIdBypass;
            public IValueRegisterField secureAttribute;
            public IValueRegisterField privilegedAttribute;
        }

        private class Controller
        {}

        private class PeripheralAccessController : Controller
        {
            public PeripheralAccessController(uint id)
            {
                this.id = id;
                peripherals = new Dictionary<IPeripheral, uint>();
            }

            public bool Contains(IPeripheral peripheral)
            {
                return peripherals.ContainsKey(peripheral);
            }

            public IEnumerable<NumberRegistrationPoint<uint[]>> GetRegistrationPoints(IPeripheral peripheral)
            {
                return new NumberRegistrationPoint<uint[]>[] { new NumberRegistrationPoint<uint[]>(new uint[] { id, peripherals[peripheral] }) };
            }

            public void Register(IPeripheral peripheral, uint slotId)
            {
                peripherals.Add(peripheral, slotId);
            }

            public void Unregister(IPeripheral peripheral)
            {
                peripherals.Remove(peripheral);
            }

            public IEnumerable<IRegistered<IPeripheral, NumberRegistrationPoint<uint[]>>> Children
            {
                get => peripherals.Select(kv => new Registered<IPeripheral, NumberRegistrationPoint<uint[]>>(kv.Key, new NumberRegistrationPoint<uint[]>(new uint[] { id, kv.Value })));
            }

            public readonly IDictionary<IPeripheral, uint> peripherals;
            private readonly uint id;
        }

        private class MemoryRegionController : Controller
        {
            public MemoryRegionController(uint id, uint numberOfDescriptors)
            {
                this.id = id;
                NumberOfDescriptors = numberOfDescriptors;
                descriptors = new MemoryRegionDescriptor[numberOfDescriptors];
                for(var i = 0; i < numberOfDescriptors; ++i)
                {
                    descriptors[i] = new MemoryRegionDescriptor();
                }
                memories = new Dictionary<IMemory, uint>();
            }

            public bool Contains(IMemory memory)
            {
                return memories.ContainsKey(memory);
            }

            public IEnumerable<NumberRegistrationPoint<uint[]>> GetRegistrationPoints(IMemory memory)
            {
                return new NumberRegistrationPoint<uint[]>[] { new NumberRegistrationPoint<uint[]>(new uint[] { id, memories[memory] }) };
            }

            public void Register(IMemory memory, uint slotId)
            {
                memories.Add(memory, slotId);
            }

            public void Unregister(IMemory memory)
            {
                memories.Remove(memory);
            }

            public IEnumerable<IRegistered<IPeripheral, NumberRegistrationPoint<uint[]>>> Children
            {
                get => memories.Select(kv => new Registered<IPeripheral, NumberRegistrationPoint<uint[]>>(kv.Key, new NumberRegistrationPoint<uint[]>(new uint[] { id, kv.Value })));
            }

            public uint NumberOfDescriptors { get; }

            public IEnumerable<MemoryRegionDescriptor> Descriptors => descriptors;

            public MemoryRegionDescriptor this[uint i]
            {
                get => descriptors[i];
            }

            public readonly IDictionary<IMemory, uint> memories;
            private readonly MemoryRegionDescriptor[] descriptors;
            private readonly uint id;
        }

        private class MemoryRegionDescriptor
        {
            public ulong StartAddress => startAddress.Value << 5;
            public ulong EndAddress => endAddress.Value << 5 | 0x1fUL;

            public IValueRegisterField startAddress;
            public IValueRegisterField endAddress;
            public IValueRegisterField[] domainAccessControlPolicy;
            public IValueRegisterField semaphoreNumber;
            public IFlagRegisterField semaphoreEnable;
            public IEnumRegisterField<LockConfig> lockConfig;
            public IFlagRegisterField valid;
        }
    }
}

