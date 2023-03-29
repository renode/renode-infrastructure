//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include "arch_callbacks.h"
#include "renode_imports.h"

EXTERNAL_AS(uint64_t, GetCPUTime, tlib_get_cpu_time)

EXTERNAL_AS(uint64_t, ReadCSR, tlib_read_csr, uint64_t)
EXTERNAL_AS(void, WriteCSR, tlib_write_csr, uint64_t, uint64_t)
EXTERNAL(void, tlib_mip_changed, uint64_t)

EXTERNAL_AS(int32_t, HandleCustomInstruction, tlib_handle_custom_instruction, uint64_t, uint64_t)
EXTERNAL_AS(void, HandlePostOpcodeExecutionHook, tlib_handle_post_opcode_execution_hook, uint32_t, uint64_t)
EXTERNAL_AS(void, HandlePostGprAccessHook, tlib_handle_post_gpr_access_hook, uint32_t, uint32_t)
EXTERNAL_AS(void, ClicClearEdgeInterrupt, tlib_clic_clear_edge_interrupt)
EXTERNAL_AS(void, ClicAcknowledgeInterrupt, tlib_clic_acknowledge_interrupt)
