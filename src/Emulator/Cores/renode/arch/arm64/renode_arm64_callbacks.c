//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include "arch_callbacks.h"
#include "renode_imports.h"

EXTERNAL_AS(func_uint64_uint32, ReadSystemRegisterInterruptCPUInterface, tlib_read_system_register_interrupt_cpu_interface)
EXTERNAL_AS(action_uint32_uint64, WriteSystemRegisterInterruptCPUInterface, tlib_write_system_register_interrupt_cpu_interface)

EXTERNAL_AS(func_uint64_uint32, ReadSystemRegisterGenericTimer64, tlib_read_system_register_generic_timer_64)
EXTERNAL_AS(action_uint32_uint64, WriteSystemRegisterGenericTimer64, tlib_write_system_register_generic_timer_64)

EXTERNAL_AS(func_uint32_uint32, ReadSystemRegisterGenericTimer32, tlib_read_system_register_generic_timer_32)
EXTERNAL_AS(action_uint32_uint32, WriteSystemRegisterGenericTimer32, tlib_write_system_register_generic_timer_32)

EXTERNAL_AS(action_uint32_uint32, OnExecutionModeChanged, tlib_on_execution_mode_changed)
EXTERNAL_AS(action, HandleSMCCall, tlib_handle_smc_call)
