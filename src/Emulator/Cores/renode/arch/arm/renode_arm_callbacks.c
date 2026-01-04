//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include "arch_callbacks.h"
#include "renode_imports.h"

#ifdef TARGET_PROTO_ARM_M
EXTERNAL_AS(int32_t, AcknowledgeIRQ, tlib_nvic_acknowledge_irq)
EXTERNAL_AS(void, CompleteIRQ, tlib_nvic_complete_irq, int32_t)
EXTERNAL_AS(void, SetPendingIRQ, tlib_nvic_set_pending_irq, int32_t)
EXTERNAL_AS(int32_t, FindPendingIRQ, tlib_nvic_find_pending_irq)
EXTERNAL_AS(void, OnBASEPRIWrite, tlib_nvic_write_basepri, int32_t, uint32_t)
EXTERNAL_AS(int32_t, PendingMaskedIRQ, tlib_nvic_get_pending_masked_irq)
EXTERNAL_AS(uint32_t, HasEnabledTrustZone, tlib_has_enabled_trustzone)
EXTERNAL_AS(uint32_t, InterruptTargetsSecure, tlib_nvic_interrupt_targets_secure, int32_t)
EXTERNAL_AS(int32_t, CustomIdauHandler, tlib_custom_idau_handler, voidptr, voidptr, voidptr)
#endif

EXTERNAL_AS(uint32_t, Read32CP15, tlib_read_cp15_32, uint32_t)
EXTERNAL_AS(void, Write32CP15, tlib_write_cp15_32, uint32_t, uint32_t)
EXTERNAL_AS(uint64_t, Read64CP15, tlib_read_cp15_64, uint32_t)
EXTERNAL_AS(void, Write64CP15, tlib_write_cp15_64, uint32_t, uint64_t)
EXTERNAL_AS(uint32_t, IsWfiAsNop, tlib_is_wfi_as_nop)
EXTERNAL_AS(uint32_t, IsWfeAndSevAsNop, tlib_is_wfe_and_sev_as_nop)
EXTERNAL_AS(uint32_t, DoSemihosting, tlib_do_semihosting)
EXTERNAL_AS(void, SetSystemEvent, tlib_set_system_event, int32_t)
EXTERNAL_AS(void, ReportPMUOverflow, tlib_report_pmu_overflow, int32_t)
EXTERNAL_AS(void, FillConfigurationSignalsState, tlib_fill_configuration_signals_state, voidptr)
