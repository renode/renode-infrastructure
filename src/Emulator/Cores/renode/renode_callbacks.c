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

void (*on_translation_block_find_slow)(uint32_t pc);

void renode_attach_log_translation_block_fetch(void (handler)(uint32_t))
{
    on_translation_block_find_slow = handler;
}

void tlib_on_translation_block_find_slow(uint32_t pc)
{
  if(on_translation_block_find_slow)
  {
    (*on_translation_block_find_slow)(pc);
  }
}

EXTERNAL_AS(action_string, ReportAbort, tlib_abort)
EXTERNAL_AS(action_int32_string, LogAsCpu, tlib_log)

EXTERNAL_AS(func_uint32_uint32, ReadByteFromBus, tlib_read_byte)
EXTERNAL_AS(func_uint32_uint32, ReadWordFromBus, tlib_read_word)
EXTERNAL_AS(func_uint32_uint32, ReadDoubleWordFromBus, tlib_read_double_word)

EXTERNAL_AS(action_uint32_uint32, WriteByteToBus, tlib_write_byte)
EXTERNAL_AS(action_uint32_uint32, WriteWordToBus, tlib_write_word)
EXTERNAL_AS(action_uint32_uint32, WriteDoubleWordToBus, tlib_write_double_word)

EXTERNAL_AS(func_int32_uint32, IsIoAccessed, tlib_is_io_accessed)

EXTERNAL_AS(action_uint32_uint32, OnBlockBegin, tlib_on_block_begin)
EXTERNAL_AS(func_uint32, IsBlockBeginEventEnabled, tlib_is_block_begin_event_enabled)

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
EXTERNAL_AS(action_int32, OnTranslationCacheSizeChange, tlib_on_translation_cache_size_change)

EXTERNAL(action_intptr_intptr, invalidate_tb_in_other_cpus)
void tlib_invalidate_tb_in_other_cpus(unsigned long start, unsigned long end)
{
  invalidate_tb_in_other_cpus((void*)start, (void*)end);
}

EXTERNAL_AS(action_int32, UpdateInstructionCounter, update_instruction_counter_inner)
EXTERNAL_AS(func_uint32, IsInstructionCountEnabled, tlib_is_instruction_count_enabled)

void renode_set_count_threshold(int32_t value)
{
  cpu->instructions_count_threshold = value;
}

int32_t block_trimming_enabled;

void renode_set_block_trimming(int32_t value)
{
  block_trimming_enabled = value;
}

int32_t renode_get_block_trimming()
{
  return block_trimming_enabled;
}

void tlib_update_instruction_counter(int32_t value)
{
  // if trimming is enabled we want to trim block so that it does not saturate
  // the instruction count value; but - if such block has size of one
  // instruction, the we naturally have to let him saturate the count value
  if(cpu->instructions_count_value + value >= cpu->instructions_count_threshold && value != 1 && block_trimming_enabled)
  {
    size_of_next_block_to_translate = cpu->instructions_count_threshold - cpu->instructions_count_value - 1;
    // it might happen that even first instruction of the current block would
    // saturate counter - in such case create block with that instruction only
    if(size_of_next_block_to_translate == 0)
    {
      size_of_next_block_to_translate = 1;
    }
    cpu->tb_restart_request = 1;
    tb_phys_invalidate(cpu->current_tb, -1);
    cpu->current_tb = NULL;
  }
  else
  {
    cpu->instructions_count_value += value;
    if(cpu->instructions_count_value < cpu->instructions_count_threshold)
    {
      return;
    }
    update_instruction_counter_inner(cpu->instructions_count_value);
    cpu->instructions_count_value = 0;
  }
}

EXTERNAL_AS(func_int32, GetCpuIndex, tlib_get_cpu_index)
EXTERNAL_AS(action_uint32_uint32_uint32, LogDisassembly, tlib_on_block_translation)
