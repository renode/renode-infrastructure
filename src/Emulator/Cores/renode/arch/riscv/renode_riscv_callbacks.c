//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include "arch_callbacks.h"
#include "renode_imports.h"

EXTERNAL_AS(func_uint64, GetCPUTime, tlib_get_cpu_time)
EXTERNAL_AS(func_uint32, IsInDebugMode, tlib_is_in_debug_mode)

EXTERNAL_AS(func_int32_uint64, HasCSR, tlib_has_nonstandard_csr)
EXTERNAL_AS(func_uint64_uint64, ReadCSR, tlib_read_csr)
EXTERNAL_AS(action_uint64_uint64, WriteCSR, tlib_write_csr)
EXTERNAL(action_uint64, tlib_mip_changed)

EXTERNAL_AS(func_int32_uint64_uint64, HandleCustomInstruction, tlib_handle_custom_instruction)
