//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include <callbacks.h>
#include "renode_imports.h"

EXTERNAL(action_uint64, touch_host_block)

typedef struct {
  uint64_t start;
  uint64_t size;
  void *host_pointer;
  uint32_t hb_start;
} __attribute__((packed)) host_memory_block_packed_t;

typedef struct {
  uint64_t start;
  uint64_t size;
  void *host_pointer;
  uint32_t hb_start;
  uint32_t last_used;
} host_memory_block_t;

static host_memory_block_t *host_blocks;
static int host_blocks_count;

void *tlib_guest_offset_to_host_ptr(uint64_t offset)
{
  host_memory_block_t *host_blocks_cached;
  int count_cached, i;
try_find_block:
  count_cached = host_blocks_count;
  host_blocks_cached = (host_memory_block_t*)host_blocks;
  for(i = 0; i < count_cached; i++) {
    if(offset <= (host_blocks_cached[i].start + host_blocks_cached[i].size - 1) && offset >= host_blocks_cached[i].start) {
      // marking last used
      host_blocks_cached[host_blocks_cached[i].hb_start].last_used = i;
      return host_blocks_cached[i].host_pointer + (offset - host_blocks_cached[i].start);
    }
  }
  touch_host_block(offset);
  goto try_find_block;
}

uint64_t tlib_host_ptr_to_guest_offset(void *ptr)
{
  int i, index, count_cached;
  host_memory_block_t *host_blocks_cached;
  count_cached = host_blocks_count;
  host_blocks_cached = (host_memory_block_t*)host_blocks;
  for(i = 0; i < count_cached; i++) {
    if(ptr <= (host_blocks_cached[i].host_pointer + host_blocks_cached[i].size - 1) && ptr >= host_blocks_cached[i].host_pointer) {
      index = host_blocks_cached[i].last_used;
      return host_blocks_cached[index].start + (ptr - host_blocks_cached[index].host_pointer);
    }
  }
  tlib_abort("Trying to translate pointer that was not alocated by us.");
  return 0;
}

void renode_set_host_blocks(host_memory_block_packed_t *blocks, int count)
{
  int old_count, i, j;
  host_memory_block_t *old_mappings;
  old_mappings = host_blocks;
  old_count = host_blocks_count;
  host_blocks_count = count;
  host_blocks = tlib_malloc(sizeof(host_memory_block_t)*count);
  for(i = 0; i < count; i++) {
    host_blocks[i].start = blocks[i].start;
    host_blocks[i].size = blocks[i].size;
    host_blocks[i].host_pointer = blocks[i].host_pointer;
    host_blocks[i].hb_start = blocks[i].hb_start;
    // guarding value, gives accessing via this offset will end in SIGSEGV almost for sure
  host_blocks[i].last_used = UINT32_MAX;
  }

  // every old mapping has to be in a new mappings as well
  i = 0;
  j = 0;
  void *last_pointer = 0;
  while(j < old_count) {
    if(last_pointer == old_mappings[j].host_pointer) {
      j++;
      continue;
    }
    while(host_blocks[i].host_pointer != old_mappings[j].host_pointer || host_blocks[i].start != old_mappings[j].start) {
      i++;
    }
    // fine, let's upgrade last_used accordingly
    host_blocks[host_blocks[i].hb_start].last_used = i + old_mappings[j].last_used - j;
    last_pointer = host_blocks[i].host_pointer;
    j++;
  }

  if(old_mappings != NULL) {
    tlib_free(old_mappings);
  }
}

void renode_free_host_blocks()
{
  if(host_blocks)
  {
    tlib_free(host_blocks);
  }
}
