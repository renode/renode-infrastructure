//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include <callbacks.h>
#include "renode_imports.h"
#include "../tlib/include/unwind.h"

EXTERNAL(void, touch_host_block, uint64_t)

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


typedef struct list_node_t {
    host_memory_block_t* element;

    struct list_node_t* prev;
    struct list_node_t* next;
} list_node_t;

typedef struct {
    list_node_t* guest_to_host_head;
    list_node_t* host_to_guest_head;
    
    uint32_t size;
    
    host_memory_block_t *elements;
    
    list_node_t *guest_to_host_nodes;
    list_node_t *host_to_guest_nodes;
} host_memory_block_lists_t;

static host_memory_block_lists_t *lists;

static void move_to_head(list_node_t** head, list_node_t* node)
{
    if(*head == node)
    {
        // it's already at the top
        return;
    }
    
    if(node->prev != NULL)
    {
        node->prev->next = node->next;
    }
      
    if(node->next != NULL)
    {
        node->next->prev = node->prev;
    }

    (*head)->prev = node;
    node->prev = NULL;
    node->next = *head;
    *head = node;
}

void *tlib_guest_offset_to_host_ptr(uint64_t offset)
{
  host_memory_block_lists_t *host_blocks_list_cached;
  list_node_t *current_block;
try_find_block:
  host_blocks_list_cached = lists;

  if(host_blocks_list_cached != NULL)
  {
      current_block = host_blocks_list_cached->guest_to_host_head;
      while(current_block != NULL)
      {
        if(offset >= current_block->element->start && offset <= (current_block->element->start + current_block->element->size - 1)) {
            move_to_head(&host_blocks_list_cached->guest_to_host_head, current_block);
            return current_block->element->host_pointer + (offset - current_block->element->start);
        }

        current_block = current_block->next;
      }
  }

  touch_host_block(offset);
  goto try_find_block;
}

uint64_t tlib_host_ptr_to_guest_offset(void *ptr)
{
  host_memory_block_lists_t *host_blocks_list_cached;
  list_node_t *current_block;

  host_blocks_list_cached = lists;

  if(host_blocks_list_cached != NULL)
  {
      current_block = host_blocks_list_cached->host_to_guest_head; 
      while(current_block != NULL)
      {
        if(ptr >= current_block->element->host_pointer && ptr <= (current_block->element->host_pointer + current_block->element->size - 1)) {
            move_to_head(&host_blocks_list_cached->host_to_guest_head, current_block);
            return current_block->element->start + (ptr - current_block->element->host_pointer);
        }

        current_block = current_block->next;
      }
  }

  tlib_abort("Trying to translate pointer that was not alocated by us.");
  return 0;
}

static void free_list(host_memory_block_lists_t **lists)
{
    if(*lists == NULL)
    {
        return;
    }

    tlib_free((*lists)->elements);
    tlib_free((*lists)->guest_to_host_nodes);
    tlib_free((*lists)->host_to_guest_nodes);
    tlib_free(*lists);

    *lists = NULL;
}

void renode_set_host_blocks(host_memory_block_packed_t *blocks, int count)
{
  int i;
  host_memory_block_lists_t *old_mappings;
  host_memory_block_lists_t *new_mappings;

  old_mappings = lists;

  new_mappings = tlib_malloc(sizeof(host_memory_block_lists_t));
  new_mappings->size = count;
  new_mappings->elements = tlib_malloc(sizeof(host_memory_block_t) * count);

  new_mappings->guest_to_host_nodes = tlib_malloc(sizeof(list_node_t) * count);
  new_mappings->guest_to_host_head = &new_mappings->guest_to_host_nodes[0];
  new_mappings->host_to_guest_nodes = tlib_malloc(sizeof(list_node_t) * count);
  new_mappings->host_to_guest_head = &new_mappings->host_to_guest_nodes[0];

  for(i = 0; i < count; i++) {
    new_mappings->elements[i].start = blocks[i].start;
    new_mappings->elements[i].size = blocks[i].size;
    new_mappings->elements[i].host_pointer = blocks[i].host_pointer;

    new_mappings->guest_to_host_nodes[i].element = &new_mappings->elements[i];
    new_mappings->host_to_guest_nodes[i].element = &new_mappings->elements[i];

    if(i == 0)
    {
        new_mappings->guest_to_host_nodes[i].prev = NULL;
        new_mappings->host_to_guest_nodes[i].prev = NULL;
    }
    else
    {
        new_mappings->guest_to_host_nodes[i].prev = &new_mappings->guest_to_host_nodes[i - 1];
        new_mappings->host_to_guest_nodes[i].prev = &new_mappings->host_to_guest_nodes[i - 1];
    }

    if(i == count - 1)
    {
        new_mappings->guest_to_host_nodes[i].next = NULL;
        new_mappings->host_to_guest_nodes[i].next = NULL;
    }
    else
    {
        new_mappings->guest_to_host_nodes[i].next = &new_mappings->guest_to_host_nodes[i + 1];
        new_mappings->host_to_guest_nodes[i].next = &new_mappings->host_to_guest_nodes[i + 1];
    }
  }

  lists = new_mappings;
  free_list(&old_mappings);
}

EXC_VOID_2(renode_set_host_blocks, host_memory_block_packed_t *, blocks, int, count)

void renode_free_host_blocks()
{
    free_list(&lists);
}

EXC_VOID_0(renode_free_host_blocks)
