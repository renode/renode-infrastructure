//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.DoubleWordToByte | AllowedTranslation.DoubleWordToWord)]
    public class NXP_XRDC : BasicDoubleWordPeripheral, IPeripheralContainer<IPeripheral, NumberRegistrationPoint<uint[]>>, IKnownSize
    {
        public NXP_XRDC(IMachine machine, IPeripheral[] busInitiators, uint[] numberOfMemoryRegionDescriptors, uint numberOfPeripheralsControllers, uint numberOfDomainIds, uint?[] masterIdDomains) : base(machine)
        {
            if(numberOfDomainIds > MaxNumberOfDomainIds)
            {
                throw new ConstructionException($"'{nameof(numberOfDomainIds)}' not in allowed range ([0, {MaxNumberOfDomainIds}])");
            }
            this.busInitiators = busInitiators;
            this.numberOfMemoryControllers = (uint)numberOfMemoryRegionDescriptors.Length;
            this.numberOfMasterDomains = (uint)busInitiators.Length;
            this.numberOfPeripheralsControllers = numberOfPeripheralsControllers;
            this.numberOfDomainIds = numberOfDomainIds;
            this.masterIdDomains = masterIdDomains;

            peripheralAccessControlDescriptors = new PeripheralAccessControlDescriptor[MaxSlotId];
            for(var i = 0u; i < MaxSlotId; ++i)
            {
                peripheralAccessControlDescriptors[i] = new PeripheralAccessControlDescriptor();
                peripheralAccessControlDescriptors[i].DomainAccessControlPolicy = new IEnumRegisterField<AccessControlPolicy>[numberOfDomainIds];
            }

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
                peripheralAccessControllers[i] = new PeripheralAccessController(i, peripheralAccessControlDescriptors);
            }

            for(var i = 0; i < numberOfMasterDomains; ++i)
            {
                busInitiatorTypes[i] = (busInitiators[i] is ICPU) ? BusInitiatorType.Core : BusInitiatorType.Noncore;
            }
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            for(var i = 0; i < NumberOfSemaphores; ++i)
            {
                semaphores[i] = null;
            }
            base.Reset();
        }

        public bool CheckTransactionLegalWithDomainId(uint domainId, ulong address, MpuAccess access, bool isSecure = false, bool isPrivileged = false)
        {
            if(!globalValid.Value)
            {
                // All transactions allowed
                this.DebugLog("Global valid flag not set");
                return true;
            }

            foreach(var descriptor in GetTargetDescriptors(address))
            {
                if(TryCheckAccessControlPolicy(out var allowed, descriptor, domainId, access, isSecure, isPrivileged))
                {
                    return allowed;
                }
            }

            this.DebugLog("Target access control policy not found");
            return true;
        }

        public bool CheckTransactionLegalWithMasterId(uint masterId, ulong address, MpuAccess access, bool isSecure = false, bool isPrivileged = false)
        {
            if(masterId >= masterIdDomains.Length)
            {
                this.ErrorLog("Attempted transaction for invalid master Id: {0}", masterId);
                return false;
            }
            if(!globalValid.Value)
            {
                // All transactions allowed
                this.DebugLog("Global valid flag not set");
                return true;
            }
            if(!masterIdDomains[masterId].HasValue)
            {
                this.ErrorLog("Attempted transaction, but doamin is not defined for master Id {0}", masterId);
                return false;
            }
            var domainId = masterDomains[masterIdDomains[masterId].Value].DomainId.Value;
            return CheckTransactionLegalWithDomainId((uint)domainId, address, access, isSecure, isPrivileged);
        }

        public bool CheckTransactionLegalWithInitiator(IPeripheral initiator, ulong address, MpuAccess access, bool isSecure = false, bool isPrivileged = false)
        {
            if(!globalValid.Value)
            {
                // All transactions allowed
                this.DebugLog("Global valid flag not set");
                return true;
            }

            if(!TryGetDomain(initiator, out var descriptor))
            {
                this.ErrorLog("Attempted transaction for nexpected initiator: {0}", initiator.GetName());
                return false;
            }
            var domainId = (uint)descriptor.DomainId.Value;
            return CheckTransactionLegalWithDomainId(domainId, address, access, isSecure, isPrivileged);
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

        public bool TryGetDomainId(IPeripheral initiator, out uint domainId)
        {
            if(TryGetDomain(initiator, out var descriptor))
            {
                domainId = (uint)descriptor.DomainId.Value;
                return true;
            }

            domainId = default(uint);
            return false;
        }

        public bool TryGetCurrentDomainId(out uint domainId)
        {
            if(sysbus.TryGetTransactionInitiator(out var initiator))
            {
                return TryGetDomainId(initiator, out domainId);
            }

            domainId = default(uint);
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

            if(slotId >= MaxSlotId)
            {
                throw new RecoverableException($"Slot value out of [0-{MaxSlotId}) range");
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
                .WithTag("HRL", 1, 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("MRF", 7)
                .WithTaggedFlag("VAW", 8)
                .WithReservedBits(9, 21)
                .WithTaggedFlag("LK1", 30)
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
                .WithTag("MID", 28, 4)
            ;

            Registers.HardwareConfiguration1.Define(this)
                .WithValueField(0, 4, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        if(!sysbus.TryGetTransactionInitiator(out var initiator))
                        {
                            this.WarningLog("Initiator not found, returning DID 0x0");
                            return 0x0;
                        }

                        if(!TryGetDomainId(initiator, out var domainId))
                        {
                            this.WarningLog("Domain ID not found for the initiator, returning DID 0x0");
                            return 0x0;
                        }

                        return domainId;
                    },
                    name: "DID"
                )
                .WithReservedBits(4, 28)
            ;

            Registers.HardwareConfiguration2.Define(this, 0x0) // NOTE: PID based domain assignment is not supported
                .WithTaggedFlags("PIDP", 0, 31)
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
                .WithTag("MRCINST", 0, 16)
                .WithTag("PACINST", 16, 4)
                .WithReservedBits(20, 12)
            );

            // PAC error

            ((Registers)(Registers.DomainError + 0x000)).DefineMany(this,
                count: numberOfMemoryControllers,
                stepInBytes: 0x10,
                setup: (register, i) => register
                    .WithTag("EADDR", 0, 32)
            );

            ((Registers)(Registers.DomainError + 0x004)).DefineMany(this,
                count: numberOfMemoryControllers,
                stepInBytes: 0x10,
                setup: (register, i) => register
                    .WithTag("EDID", 0, 4)
                    .WithReservedBits(4, 4)
                    .WithTag("EATR", 8, 3)
                    .WithTaggedFlag("ERW", 11)
                    .WithReservedBits(12, 12)
                    .WithTag("EPORT", 24, 3)
                    .WithReservedBits(27, 3)
                    .WithTag("EST", 30, 2)
            );

            // MRC error

            ((Registers)(Registers.DomainError + 0x100)).DefineMany(this,
                count: numberOfPeripheralsControllers,
                stepInBytes: 0x10,
                setup: (register, i) => register
                    .WithTag("EADDR", 0, 32)
            );

            ((Registers)(Registers.DomainError + 0x104)).DefineMany(this,
                count: numberOfPeripheralsControllers,
                stepInBytes: 0x10,
                setup: (register, i) => register
                    .WithTag("EDID", 0, 4)
                    .WithReservedBits(4, 4)
                    .WithTag("EATR", 8, 3)
                    .WithTaggedFlag("ERW", 11)
                    .WithReservedBits(12, 12)
                    .WithTag("EPORT", 24, 3)
                    .WithReservedBits(27, 3)
                    .WithTag("EST", 30, 2)
            );

            for(var i = 0; i < numberOfMasterDomains; ++i)
            {
                if(busInitiatorTypes[i] == BusInitiatorType.Noncore)
                {
                    continue;
                }
                ((Registers)(Registers.ProcessIdentifier + 0x4 * i)).Define(this)
                    .WithTag("PID", 0, 6)
                    .WithReservedBits(6, 22)
                    .WithTaggedFlag("TSM", 28)
                    .WithTag("LK2", 29, 2)
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
                    .WithValueField(0, 3, out masterDomains[i].DomainId, name: "DID")
                    .WithReservedBits(3, 1)
                    .If(isCore)
                    .Then(reg =>
                    {
                        var masterDomain = masterDomains[i] as CoreMasterDomainDescriptor;
                        reg
                            .WithValueField(4, 2, out masterDomain.DomainIdSelect, name: "DIDS")
                            .WithValueField(6, 2, out masterDomain.ProcessIdEnable, name: "PE")
                            .WithValueField(8, 6, out masterDomain.ProcessIdMask, name: "PIDM")
                            .WithReservedBits(14, 2)
                            .WithValueField(16, 6, out masterDomain.ProcessId, name: "PID")
                            .WithReservedBits(22, 7)
                        ;
                    })
                    .Else(reg =>
                    {
                        var masterDomain = masterDomains[i] as NoncoreMasterDomainDescriptor;
                        reg
                            .WithValueField(4, 2, out masterDomain.PrivilegedAttribute, name: "PA")
                            .WithValueField(6, 2, out masterDomain.SecureAttribute, name: "SA")
                            .WithFlag(8, out masterDomain.DomainIdBypass, name: "DIDB")
                            .WithReservedBits(9, 20)
                        ;
                    })
                    .WithFlag(29, FieldMode.Read,
                        valueProviderCallback: _ => !isCore,
                        name: "DFMT"
                    )
                    .WithFlag(30, out masterDomains[i].LockConfig, name: "LK1")
                    .WithFlag(31, out masterDomains[i].Valid, name: "VLD")
                ;
            });

            // PAC

            ((Registers)(Registers.PeripheralDomainAccessControl + 0x0)).DefineMany(this,
                count: MaxSlotId,
                stepInBytes: 0x8,
                setup: (register, slotId) => register
                    .WithEnumFields(0, 3, (int)numberOfDomainIds, out peripheralAccessControlDescriptors[slotId].DomainAccessControlPolicy,
                        changeCallback: (did, oldValue, _) =>
                        {
                            TryGetCurrentDomainId(out var currentDid);
                            var lockConfig = peripheralAccessControlDescriptors[slotId].LockConfig.Value;
                            if(lockConfig == LockConfig.Locked ||
                                (lockConfig == LockConfig.AccessControlPolicyUnlocked && did != currentDid))
                            {
                                peripheralAccessControlDescriptors[slotId].DomainAccessControlPolicy[did].Value = oldValue;
                            }
                        },
                        name: "DxACP"
                    )
                    .WithValueField(3 * (int)numberOfDomainIds, 24 - 3 * (int)numberOfDomainIds, // It's a value field to silence unhandled write logs
                        FieldMode.WriteToClear | FieldMode.Read,
                        name: "RESERVED"
                    )
                    .WithValueField(24, 4, out peripheralAccessControlDescriptors[slotId].SemaphoreNumber,
                        changeCallback: (oldValue, _) =>
                        {
                            var lockConfig = peripheralAccessControlDescriptors[slotId].LockConfig.Value;
                            if(lockConfig != LockConfig.Unlocked
                                && lockConfig != LockConfig.Unlocked2)
                            {
                                peripheralAccessControlDescriptors[slotId].SemaphoreNumber.Value = oldValue;
                            }
                        },
                        name: "SNUM"
                    )
                    .WithReservedBits(28, 2)
                    .WithFlag(30, out peripheralAccessControlDescriptors[slotId].SemaphoreEnable,
                        changeCallback: (oldValue, _) =>
                        {
                            var lockConfig = peripheralAccessControlDescriptors[slotId].LockConfig.Value;
                            if(lockConfig != LockConfig.Unlocked
                                && lockConfig != LockConfig.Unlocked2)
                            {
                                peripheralAccessControlDescriptors[slotId].SemaphoreEnable.Value = oldValue;
                            }
                        },
                        name: "SE"
                    )
                    .WithReservedBits(31, 1)
                    .WithChangeCallback((_, __) =>
                    {
                        if(!peripheralAccessControlDescriptors[slotId].Active)
                        {
                            this.DebugLog("Configuration changed for a non-registered peripheral (slot ID #{0})", slotId);
                        }
                    })
            );

            ((Registers)(Registers.PeripheralDomainAccessControl + 0x4)).DefineMany(this,
                count: MaxSlotId,
                stepInBytes: 0x8,
                setup: (register, slotId) => register
                    .WithReservedBits(0, 29)
                    .WithEnumField(29, 2, out peripheralAccessControlDescriptors[slotId].LockConfig,
                        changeCallback: (oldValue, _) =>
                        {
                            if(oldValue != LockConfig.Unlocked
                                && oldValue != LockConfig.Unlocked2)
                            {
                                peripheralAccessControlDescriptors[slotId].LockConfig.Value = oldValue;
                            }
                        },
                        name: "LK2"
                    )
                    .WithFlag(31, out peripheralAccessControlDescriptors[slotId].Valid,
                        changeCallback: (oldValue, _) =>
                        {
                            var lockConfig = peripheralAccessControlDescriptors[slotId].LockConfig.Value;
                            if(lockConfig != LockConfig.Unlocked
                                && lockConfig != LockConfig.Unlocked2)
                            {
                                peripheralAccessControlDescriptors[slotId].Valid.Value = oldValue;
                            }
                        },
                        name: "VLD"
                    )
                    .WithChangeCallback((_, __) =>
                    {
                        if(!peripheralAccessControlDescriptors[slotId].Active)
                        {
                            this.DebugLog("Configuration changed for a non-registered peripheral (slot ID #{0})", slotId);
                        }
                    })
            );

            // MRC

            for(var i = 0u; i < numberOfMemoryControllers; ++i)
            {
                for(var j = 0u; j < memoryRegionControllers[i].NumberOfDescriptors; ++j)
                {
                    // Switch to local variables for lambdas' capture
                    var n = i;
                    var m = j;
                    ((Registers)((uint)Registers.MemoryRegionDescriptor + n * 0x200 + m * 0x20 + 0)).Define(this)
                        .WithReservedBits(0, 5)
                        .WithValueField(5, 27, out memoryRegionControllers[n][m].StartAddress,
                            changeCallback: (oldValue, _) =>
                            {
                                if(memoryRegionControllers[n][m].LockConfig.Value != LockConfig.Unlocked)
                                {
                                    memoryRegionControllers[n][m].StartAddress.Value = oldValue;
                                }
                            },
                            name: "SRTADDR"
                        )
                    ;
                    ((Registers)((uint)Registers.MemoryRegionDescriptor + n * 0x200 + m * 0x20 + 0x4)).Define(this, 0x1f)
                        .WithReservedBits(0, 5)
                        .WithValueField(5, 27, out memoryRegionControllers[n][m].EndAddress,
                            changeCallback: (oldValue, _) =>
                            {
                                if(memoryRegionControllers[n][m].LockConfig.Value != LockConfig.Unlocked)
                                {
                                    memoryRegionControllers[n][m].EndAddress.Value = oldValue;
                                }
                            },
                            name: "ENDADDR"
                        )
                    ;
                    ((Registers)((uint)Registers.MemoryRegionDescriptor + n * 0x200 + m * 0x20 + 0x8)).Define(this)
                        .WithEnumFields(0, 3, (int)numberOfDomainIds, out memoryRegionControllers[n][m].DomainAccessControlPolicy,
                            changeCallback: (did, oldValue, _) =>
                            {
                                TryGetCurrentDomainId(out var currentDid);
                                var lockConfig = memoryRegionControllers[n][m].LockConfig.Value;
                                if(lockConfig != LockConfig.Unlocked &&
                                    !(lockConfig == LockConfig.AccessControlPolicyUnlocked && did == currentDid))
                                {
                                    memoryRegionControllers[n][m].DomainAccessControlPolicy[did].Value = oldValue;
                                }
                            },
                            name: "DxACP"
                        )
                        .WithReservedBits(3 * (int)numberOfDomainIds, 24 - 3 * (int)numberOfDomainIds)
                        .WithValueField(24, 4, out memoryRegionControllers[n][m].SemaphoreNumber,
                            changeCallback: (oldValue, _) =>
                            {
                                if(memoryRegionControllers[n][m].LockConfig.Value != LockConfig.Unlocked)
                                {
                                    memoryRegionControllers[n][m].SemaphoreNumber.Value = oldValue;
                                }
                            },
                            name: "SNUM"
                        )
                        .WithReservedBits(28, 2)
                        .WithFlag(30, out memoryRegionControllers[n][m].SemaphoreEnable,
                            changeCallback: (oldValue, _) =>
                            {
                                if(memoryRegionControllers[n][m].LockConfig.Value != LockConfig.Unlocked)
                                {
                                    memoryRegionControllers[n][m].SemaphoreEnable.Value = oldValue;
                                }
                            },
                            name: "SE"
                        )
                        .WithReservedBits(31, 1)
                    ;
                    ((Registers)((uint)Registers.MemoryRegionDescriptor + n * 0x200 + m * 0x20 + 0xC)).Define(this)
                        .WithReservedBits(0, 29)
                        .WithEnumField(29, 2, out memoryRegionControllers[n][m].LockConfig,
                            changeCallback: (oldValue, newValue) =>
                            {
                                if(oldValue != LockConfig.Unlocked || newValue == LockConfig.Reserved)
                                {
                                    memoryRegionControllers[n][m].LockConfig.Value = oldValue;
                                }
                            },
                            name: "LK2"
                        )
                        .WithFlag(31, out memoryRegionControllers[n][m].Valid,
                            changeCallback: (oldValue, _) =>
                            {
                                if(memoryRegionControllers[n][m].LockConfig.Value != LockConfig.Unlocked)
                                {
                                    memoryRegionControllers[n][m].Valid.Value = oldValue;
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
            for(var i = 0; i < busInitiators.Length; i++)
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

        private IEnumerable<AccessDescriptor> GetTargetDescriptors(ulong address)
        {
            var target = machine.SystemBus.WhatPeripheralIsAt(address);
            if(target == null)
            {
                return Enumerable.Empty<AccessDescriptor>();
            }

            var isMemory = target is IMemory;
            var controllers = isMemory
                ? memoryRegionControllers.AsEnumerable<Controller>()
                : peripheralAccessControllers.AsEnumerable<Controller>()
            ;

            foreach(var controller in controllers)
            {
                if(!controller.Contains(target))
                {
                    continue;
                }

                this.DebugLog("{0} controller found", isMemory ? "Memory" : "Peripheral");
                return controller.GetDescriptors(target, address);
            }

            return Enumerable.Empty<AccessDescriptor>();
        }

        private bool TryCheckAccessControlPolicy(out bool allowed, AccessDescriptor descriptor, uint domainId, MpuAccess access, bool isSecure, bool isPrivileged)
        {
            allowed = default(bool);
            if(!descriptor.Valid.Value)
            {
                this.DebugLog("Target's configuration is not valid");
                return false;
            }
            this.DebugLog("Target's configuration is valid");

            var semaphoreNumber = descriptor.SemaphoreNumber.Value;
            if(access == MpuAccess.Write && descriptor.SemaphoreEnable.Value)
            {
                if(semaphores[semaphoreNumber].HasValue)
                {
                    this.DebugLog("Configuration found and semaphore #{2} locked (owner: {0}, initiator: {1})", semaphores[semaphoreNumber], domainId, semaphoreNumber);
                    allowed = semaphores[semaphoreNumber] == domainId;
                    return true;
                }
                else
                {
                    this.DebugLog("Configuration found and semaphore not locked");
                }
            }

            var acp = descriptor.DomainAccessControlPolicy[domainId].Value;
            this.DebugLog("Checking {0} for {1}, secure: {2}, privileged: {2}", acp, access, isSecure, isPrivileged);
            if(AllowsAccess(acp, access, isSecure, isPrivileged))
            {
                this.DebugLog("Configuration found and access allowed");
                allowed = true;
                return true;
            }

            return false;
        }

        private bool AllowsAccess(AccessControlPolicy domainAccessControlPolicy, MpuAccess access, bool isSecure, bool isPrivileged)
        {
            switch(domainAccessControlPolicy)
            {
            case AccessControlPolicy.AllAllowed:
                return true;
            case AccessControlPolicy.NonsecureUnprivilegedNotAllowed:
                return isSecure || isPrivileged;
            case AccessControlPolicy.NonsecureWritesNotAllowed:
                return access == MpuAccess.Read || isSecure;
            case AccessControlPolicy.SecureAllowedAndNonsecurePrivilegedReadAllowed:
                return isSecure || (access == MpuAccess.Read && isPrivileged);
            case AccessControlPolicy.SecureAllowed:
                return isSecure;
            case AccessControlPolicy.SecurePriviledgedAllowed:
                return isSecure && isPrivileged;
            case AccessControlPolicy.SecureReadAllowed:
                return access == MpuAccess.Read && isSecure;
            case AccessControlPolicy.AllNotAllowed:
                return false;
            default:
                throw new UnreachableException();
            }
        }

        private IFlagRegisterField globalValid;

        private readonly uint numberOfMemoryControllers;
        private readonly uint numberOfPeripheralsControllers;
        private readonly uint numberOfMasterDomains;
        private readonly uint numberOfDomainIds;

        private readonly uint?[] masterIdDomains;
        private readonly IPeripheral[] busInitiators;
        private readonly BusInitiatorType[] busInitiatorTypes;
        private readonly MasterDomainDescriptor[] masterDomains;
        private readonly MemoryRegionController[] memoryRegionControllers;
        private readonly PeripheralAccessController[] peripheralAccessControllers;
        private readonly PeripheralAccessControlDescriptor[] peripheralAccessControlDescriptors;

        private readonly uint?[] semaphores = new uint?[NumberOfSemaphores];

        private const int MaxSlotId = 512;
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

        private class CoreMasterDomainDescriptor : MasterDomainDescriptor
        {
            public IValueRegisterField ProcessId;
            public IValueRegisterField ProcessIdMask;
            public IValueRegisterField ProcessIdEnable;
            public IValueRegisterField DomainIdSelect;
        }

        private class NoncoreMasterDomainDescriptor : MasterDomainDescriptor
        {
            public IFlagRegisterField DomainIdBypass;
            public IValueRegisterField SecureAttribute;
            public IValueRegisterField PrivilegedAttribute;
        }

        private class PeripheralAccessController : Controller
        {
            public PeripheralAccessController(uint id, PeripheralAccessControlDescriptor[] descriptors) : base(id)
            {
                this.descriptors = descriptors;
            }

            public override void Register(IPeripheral peripheral, uint slotId)
            {
                if(descriptors[slotId].Active)
                {
                    throw new RegistrationException($"Peripheral with Slot ID #{slotId} already registered");
                }
                descriptors[slotId].Active = true;
                base.Register(peripheral, slotId);
            }

            public override void Unregister(IPeripheral peripheral)
            {
                descriptors[peripherals[peripheral]].Active = false;
                base.Unregister(peripheral);
            }

            public override IEnumerable<AccessDescriptor> GetDescriptors(IPeripheral target, ulong _)
            {
                yield return descriptors[peripherals[target]];
            }

            private readonly PeripheralAccessControlDescriptor[] descriptors;
        }

        private class MemoryRegionController : Controller
        {
            public MemoryRegionController(uint id, uint numberOfDescriptors) : base(id)
            {
                NumberOfDescriptors = numberOfDescriptors;
                descriptors = new MemoryRegionDescriptor[numberOfDescriptors];

                for(var i = 0; i < numberOfDescriptors; ++i)
                {
                    descriptors[i] = new MemoryRegionDescriptor();
                }
            }

            public override IEnumerable<AccessDescriptor> GetDescriptors(IPeripheral _, ulong address)
            {
                foreach(var region in descriptors)
                {
                    if(region.Contains(address))
                    {
                        yield return region;
                    }
                }
            }

            public uint NumberOfDescriptors { get; }

            public MemoryRegionDescriptor this[uint i]
            {
                get => descriptors[i];
            }

            private readonly MemoryRegionDescriptor[] descriptors;
        }

        private class PeripheralAccessControlDescriptor : AccessDescriptor
        {
            public bool Active;
        }

        private class MemoryRegionDescriptor : AccessDescriptor
        {
            public bool Contains(ulong address)
            {
                address >>= 5;
                return StartAddress.Value <= address && address <= EndAddress.Value;
            }

            public IValueRegisterField StartAddress;
            public IValueRegisterField EndAddress;
        }

        private abstract class MasterDomainDescriptor
        {
            public IValueRegisterField DomainId;
            public IFlagRegisterField LockConfig;
            public IFlagRegisterField Valid;
        }

        private abstract class Controller
        {
            public Controller(uint id)
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

            public virtual void Register(IPeripheral peripheral, uint slotId)
            {
                peripherals.Add(peripheral, slotId);
            }

            public virtual void Unregister(IPeripheral peripheral)
            {
                peripherals.Remove(peripheral);
            }

            public abstract IEnumerable<AccessDescriptor> GetDescriptors(IPeripheral target, ulong address);

            public IEnumerable<IRegistered<IPeripheral, NumberRegistrationPoint<uint[]>>> Children
            {
                get => peripherals.Select(kv => new Registered<IPeripheral, NumberRegistrationPoint<uint[]>>(kv.Key, new NumberRegistrationPoint<uint[]>(new uint[] { id, kv.Value })));
            }

            protected readonly IDictionary<IPeripheral, uint> peripherals;
            private readonly uint id;
        }

        private abstract class AccessDescriptor
        {
            public IEnumRegisterField<AccessControlPolicy>[] DomainAccessControlPolicy;
            public IValueRegisterField SemaphoreNumber;
            public IFlagRegisterField SemaphoreEnable;
            public IEnumRegisterField<LockConfig> LockConfig;
            public IFlagRegisterField Valid;
        }

        private enum BusInitiatorType
        {
            Core,
            Noncore,
        }

        private enum AccessControlPolicy
        {
            AllAllowed = 0b111,
            NonsecureUnprivilegedNotAllowed = 0b110,
            NonsecureWritesNotAllowed = 0b101,
            SecureAllowedAndNonsecurePrivilegedReadAllowed = 0b100,
            SecureAllowed = 0b011,
            SecurePriviledgedAllowed = 0b010,
            SecureReadAllowed = 0b001,
            AllNotAllowed = 0b000,
        }

        private enum LockConfig
        {
            Unlocked = 0b00,
            Reserved = 0b01, // for MDAC
            Unlocked2 = 0b01, // for PDAC
            AccessControlPolicyUnlocked = 0b10,
            Locked = 0b11,
        }
    }
}

