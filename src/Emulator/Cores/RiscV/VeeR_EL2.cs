//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class VeeR_EL2 : RiscV32
    {
        public VeeR_EL2(IMachine machine, IRiscVTimeProvider timeProvider = null, uint hartId = 0, PrivilegedArchitecture privilegedArchitecture = PrivilegedArchitecture.Priv1_12,
            Endianess endianness = Endianess.LittleEndian, string cpuType = "rv32imc_zicsr_zifencei_zba_zbb_zbc_zbs", PrivilegeLevels privilegeLevels = PrivilegeLevels.MachineUser)
            : base(machine, cpuType, timeProvider, hartId, privilegedArchitecture, endianness, allowUnalignedAccesses: true, privilegeLevels: privilegeLevels, pmpNumberOfAddrBits: 30)
        {
            RegisterCustomCSRs();
        }

        private void RegisterCustomCSRs()
        {
            CreateCSRStub(CustomCSR.RegionAccessControl, "mrac");
            CreateCSRStub(CustomCSR.CorePauseControl, "mcpc");
            CreateCSRStub(CustomCSR.MemorySynchronizationTrigger, "dmst");
            CreateCSRStub(CustomCSR.PowerManagementControl, "mpmc");
            CreateCSRStub(CustomCSR.ICacheArrayWayIndexSelection, "dicawics");
            CreateCSRStub(CustomCSR.ICacheArrayData0, "dicad0");
            CreateCSRStub(CustomCSR.ICacheArrayData1, "dicad1");
            CreateCSRStub(CustomCSR.ICacheArrayGo, "dicago");
            CreateCSRStub(CustomCSR.ICacheDataArray0High, "dicac0h");
            CreateCSRStub(CustomCSR.ForceDebugHaltThreshold, "mfdht");
            CreateCSRStub(CustomCSR.ForceDebugHaltStatus, "mfdhs");
            CreateCSRStub(CustomCSR.InternalTimerCounter0, "mitcnt0");
            CreateCSRStub(CustomCSR.InternalTimerBound0, "mitb0");
            CreateCSRStub(CustomCSR.InternalTimerControl0, "mitctl0");
            CreateCSRStub(CustomCSR.InternalTimerCounter1, "mitcnt1");
            CreateCSRStub(CustomCSR.InternalTimerBound1, "mitb1");
            CreateCSRStub(CustomCSR.InternalTimerControl1, "mitctl1");
            CreateCSRStub(CustomCSR.ICacheErrorCounterThreshold, "micect");
            CreateCSRStub(CustomCSR.ICCMCorrectableErrorCounterThreshold, "miccmect");
            CreateCSRStub(CustomCSR.DCCMCorrectableErrorCounterThreshold, "mdccmect");
            CreateCSRStub(CustomCSR.ClockGatingControl, "mcgc");
            CreateCSRStub(CustomCSR.FeatureDisableControl, "mfdc");
            CreateCSRStub(CustomCSR.MachineSecondaryCause, "mscause");
            CreateCSRStub(CustomCSR.DBUSErrorAddressUnlock, "mdeau");
            CreateCSRStub(CustomCSR.ExternalInterruptVectorTable, "meivt");
            CreateCSRStub(CustomCSR.ExternalInterruptPriorityThreshold, "meipt");
            CreateCSRStub(CustomCSR.ExternalInterruptClaimIDPriorityLevelCaptureTrigger, "meicpct");
            CreateCSRStub(CustomCSR.ExternalInterruptClaimIDPriorityLevel, "meicidpl");
            CreateCSRStub(CustomCSR.ExternalInterruptCurrentPriorityLevel, "meicurpl");
            CreateCSRStub(CustomCSR.DBUSFirstErrorAddressCapture, "mdseac");
            CreateCSRStub(CustomCSR.ExternalInterruptHandlerAddressPointer, "meihap");
        }

        private void CreateCSRStub(IConvertible csr, string name, ulong returnValue = 0)
        {
            var offset = Convert.ToUInt64(csr);
            RegisterCSR(Convert.ToUInt64(csr),
                readOperation: () =>
                {
                    this.WarningLog("Reading 0x{0:X} from an unimplemented CSR: {1} (0x{2:X})", returnValue, name, offset);
                    return returnValue;
                },
                writeOperation: value =>
                {
                    this.WarningLog("Writing 0x{0:X} to unimplemnted CSR: {1} (0x{2:X})", value, name, offset);
                });
        }

        private enum CustomCSR : ulong
        {
            RegionAccessControl = 0x7C0,                                    // mrac
            CorePauseControl = 0x7C2,                                       // mcpc
            MemorySynchronizationTrigger = 0x7C4,                           // dmst
            PowerManagementControl = 0x7C6,                                 // mpmc
            ICacheArrayWayIndexSelection = 0x7C8,                           // dicawics
            ICacheArrayData0 = 0x7C9,                                       // dicad0
            ICacheArrayData1 = 0x7CA,                                       // dicad1
            ICacheArrayGo = 0x7CB,                                          // dicago
            ICacheDataArray0High = 0x7CC,                                   // dicac0h
            ForceDebugHaltThreshold = 0x7CE,                                // mfdht
            ForceDebugHaltStatus = 0x7CF,                                   // mfdhs
            InternalTimerCounter0 = 0x7D2,                                  // mitcnt0
            InternalTimerBound0 = 0x7D3,                                    // mitb0
            InternalTimerControl0 = 0x7D4,                                  // mitctl0
            InternalTimerCounter1 = 0x7D5,                                  // mitcnt1
            InternalTimerBound1 = 0x7D6,                                    // mitb1
            InternalTimerControl1 = 0x7D7,                                  // mitctl1
            ICacheErrorCounterThreshold = 0x7F0,                            // micect
            ICCMCorrectableErrorCounterThreshold = 0x7F1,                   // miccmect
            DCCMCorrectableErrorCounterThreshold = 0x7F2,                   // mdccmect
            ClockGatingControl = 0x7F8,                                     // mcgc
            FeatureDisableControl = 0x7F9,                                  // mfdc
            MachineSecondaryCause = 0x7FF,                                  // mscause
            DBUSErrorAddressUnlock = 0xBC0,                                 // mdeau
            ExternalInterruptVectorTable = 0xBC8,                           // meivt
            ExternalInterruptPriorityThreshold = 0xBC9,                     // meipt
            ExternalInterruptClaimIDPriorityLevelCaptureTrigger = 0xBCA,    // meicpct
            ExternalInterruptClaimIDPriorityLevel = 0xBCB,                  // meicidpl
            ExternalInterruptCurrentPriorityLevel = 0xBCC,                  // meicurpl
            DBUSFirstErrorAddressCapture = 0xFC0,                           // mdseac
            ExternalInterruptHandlerAddressPointer = 0xFC8,                 // meihap
        }
    }
}
