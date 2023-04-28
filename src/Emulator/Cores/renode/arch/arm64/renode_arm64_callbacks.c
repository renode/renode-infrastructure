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

EXTERNAL_AS(func_uint64_uint32, ReadSystemRegisterGenericTimer, tlib_read_system_register_generic_timer)
EXTERNAL_AS(action_uint32_uint64, WriteSystemRegisterGenericTimer, tlib_write_system_register_generic_timer)

EXTERNAL_AS(action_uint32_uint32, OnExecutionModeChanged, tlib_on_execution_mode_changed)
