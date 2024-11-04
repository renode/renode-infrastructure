//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include <stdlib.h>
#include <stdint.h>
#include "include/renode_imports.h"
#include "../tlib/include/unwind.h"

typedef void (*translation_block_find_slow_handler)(uint64_t pc);
translation_block_find_slow_handler on_translation_block_find_slow;

void renode_attach_log_translation_block_fetch(void (handler)(uint64_t))
{
    on_translation_block_find_slow = handler;
}

EXC_VOID_1(renode_attach_log_translation_block_fetch, translation_block_find_slow_handler, handler);

void tlib_on_translation_block_find_slow(uint64_t pc)
{
  if(on_translation_block_find_slow)
  {
    (*on_translation_block_find_slow)(pc);
  }
}

EXTERNAL_AS(void, ReportAbort, tlib_abort, charptr)
EXTERNAL_AS(void, LogAsCpu, tlib_log, int32_t, charptr)

EXTERNAL_AS(uint64_t, ReadByteFromBus, tlib_read_byte, uint64_t, uint64_t)
EXTERNAL_AS(uint64_t, ReadWordFromBus, tlib_read_word, uint64_t, uint64_t)
EXTERNAL_AS(uint64_t, ReadDoubleWordFromBus, tlib_read_double_word, uint64_t, uint64_t)
EXTERNAL_AS(uint64_t, ReadQuadWordFromBus, tlib_read_quad_word, uint64_t, uint64_t)

EXTERNAL_AS(void, WriteByteToBus, tlib_write_byte, uint64_t, uint64_t, uint64_t)
EXTERNAL_AS(void, WriteWordToBus, tlib_write_word, uint64_t, uint64_t, uint64_t)
EXTERNAL_AS(void, WriteDoubleWordToBus, tlib_write_double_word, uint64_t, uint64_t, uint64_t)
EXTERNAL_AS(void, WriteQuadWordToBus, tlib_write_quad_word, uint64_t, uint64_t, uint64_t)

EXTERNAL_AS(uint32_t, OnBlockBegin, tlib_on_block_begin, uint64_t, uint32_t)

EXTERNAL_AS(void, OnBlockFinished, tlib_on_block_finished, uint64_t, uint32_t)

EXTERNAL_AS(voidptr, Allocate, tlib_allocate, voidptr)
void *tlib_malloc(size_t size)
{
  return tlib_allocate((void *)size);
}
EXTERNAL_AS(voidptr, Reallocate, tlib_reallocate, voidptr, voidptr)
void *tlib_realloc(void *ptr, size_t size)
{
  return tlib_reallocate(ptr, (void *)size);
}
EXTERNAL_AS(void, Free, tlib_free, voidptr)
EXTERNAL_AS(void, OnTranslationCacheSizeChange, tlib_on_translation_cache_size_change, uint64_t)

EXTERNAL(void, invalidate_tb_in_other_cpus, voidptr, voidptr)
void tlib_invalidate_tb_in_other_cpus(uintptr_t start, uintptr_t end)
{
  invalidate_tb_in_other_cpus((void*)start, (void*)end);
}

EXTERNAL_AS(uint32_t, GetMpIndex, tlib_get_mp_index)
EXTERNAL_AS(void, LogDisassembly, tlib_on_block_translation, uint64_t, uint32_t, uint32_t)
EXTERNAL_AS(void, OnInterruptBegin, tlib_on_interrupt_begin, uint64_t)
EXTERNAL_AS(void, OnInterruptEnd, tlib_on_interrupt_end, uint64_t)
EXTERNAL_AS(void, OnMemoryAccess, tlib_on_memory_access, uint64_t, uint32_t, uint64_t, uint64_t)
EXTERNAL_AS(uint32_t, IsInDebugMode, tlib_is_in_debug_mode)
EXTERNAL_AS(void, MmuFaultExternalHandler, tlib_mmu_fault_external_handler, uint64_t, int32_t, int32_t)
EXTERNAL_AS(void, OnStackChange, tlib_profiler_announce_stack_change, uint64_t, uint64_t, uint64_t, int32_t)
EXTERNAL_AS(void, OnContextChange, tlib_profiler_announce_context_change, uint64_t)
EXTERNAL_AS(void, OnMassBroadcastDirty, tlib_mass_broadcast_dirty, voidptr, int32_t)
EXTERNAL_AS(voidptr, GetDirty, tlib_get_dirty_addresses_list, voidptr)
EXTERNAL_AS(void, OnWfiStateChange, tlib_on_wfi_state_change, int32_t)
EXTERNAL_AS(uint32_t, IsMemoryDisabled, tlib_is_memory_disabled, uint64_t, uint64_t)
