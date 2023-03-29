//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include "arch_callbacks.h"
#include "renode_imports.h"

EXTERNAL_AS(uint64_t, ReadSystemRegisterInterruptCPUInterface, tlib_read_system_register_interrupt_cpu_interface, uint32_t)
EXTERNAL_AS(void, WriteSystemRegisterInterruptCPUInterface, tlib_write_system_register_interrupt_cpu_interface, uint32_t, uint64_t)

EXTERNAL_AS(uint64_t, ReadSystemRegisterGenericTimer64, tlib_read_system_register_generic_timer_64, uint32_t)
EXTERNAL_AS(void, WriteSystemRegisterGenericTimer64, tlib_write_system_register_generic_timer_64, uint32_t, uint64_t)

EXTERNAL_AS(uint32_t, ReadSystemRegisterGenericTimer32, tlib_read_system_register_generic_timer_32, uint32_t)
EXTERNAL_AS(void, WriteSystemRegisterGenericTimer32, tlib_write_system_register_generic_timer_32, uint32_t, uint32_t)

EXTERNAL_AS(void, OnExecutionModeChanged, tlib_on_execution_mode_changed, uint32_t, uint32_t)
EXTERNAL_AS(void, HandleSMCCall, tlib_handle_smc_call)
