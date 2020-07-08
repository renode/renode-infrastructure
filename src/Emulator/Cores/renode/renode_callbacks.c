//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include <stdlib.h>
#include "cpu.h"
#include "renode_imports.h"

extern CPUState *cpu;

void (*on_translation_block_find_slow)(uint64_t pc);

void renode_attach_log_translation_block_fetch(void (handler)(uint64_t))
{
    on_translation_block_find_slow = handler;
}

void tlib_on_translation_block_find_slow(uint64_t pc)
{
  if(on_translation_block_find_slow)
  {
    (*on_translation_block_find_slow)(pc);
  }
}

EXTERNAL_AS(action_string, ReportAbort, tlib_abort)
EXTERNAL_AS(action_int32_string, LogAsCpu, tlib_log)

EXTERNAL_AS(func_uint32_uint64, ReadByteFromBus, tlib_read_byte)
EXTERNAL_AS(func_uint32_uint64, ReadWordFromBus, tlib_read_word)
EXTERNAL_AS(func_uint32_uint64, ReadDoubleWordFromBus, tlib_read_double_word)

EXTERNAL_AS(action_uint64_uint32, WriteByteToBus, tlib_write_byte)
EXTERNAL_AS(action_uint64_uint32, WriteWordToBus, tlib_write_word)
EXTERNAL_AS(action_uint64_uint32, WriteDoubleWordToBus, tlib_write_double_word)

EXTERNAL_AS(func_int32_uint64, IsIoAccessed, tlib_is_io_accessed)

EXTERNAL_AS(func_uint32_uint64_uint32, OnBlockBegin, tlib_on_block_begin)

EXTERNAL_AS(action_uint64_uint32, OnBlockFinished, tlib_on_block_finished)

EXTERNAL_AS(func_intptr_int32, Allocate, tlib_allocate)
void *tlib_malloc(size_t size)
{
  return tlib_allocate(size);
}
EXTERNAL_AS(func_intptr_intptr_int32, Reallocate, tlib_reallocate)
void *tlib_realloc(void *ptr, size_t size)
{
  return tlib_reallocate(ptr, size);
}
EXTERNAL_AS(action_intptr, Free, tlib_free)
EXTERNAL_AS(action_uint64, OnTranslationCacheSizeChange, tlib_on_translation_cache_size_change)

EXTERNAL(action_intptr_intptr, invalidate_tb_in_other_cpus)
void tlib_invalidate_tb_in_other_cpus(uintptr_t start, uintptr_t end)
{
  invalidate_tb_in_other_cpus((void*)start, (void*)end);
}

EXTERNAL_AS(func_int32, GetCpuIndex, tlib_get_cpu_index)
EXTERNAL_AS(action_uint64_uint32_uint32, LogDisassembly, tlib_on_block_translation)
EXTERNAL_AS(action_uint64, OnInterruptBegin, tlib_on_interrupt_begin)
EXTERNAL_AS(action_uint64, OnInterruptEnd, tlib_on_interrupt_end)
EXTERNAL_AS(action_uint32_uint64, OnMemoryAccess, tlib_on_memory_access)
