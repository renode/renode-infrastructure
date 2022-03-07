//
// Copyright (c) 2010-2022 Antmicro
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
} __attribute__((packed)) host_memory_block_packed_t;

typedef struct {
  uint64_t start;
  uint64_t size;
  void *host_pointer;
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
      index = i;
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
