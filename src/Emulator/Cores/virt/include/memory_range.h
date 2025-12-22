#pragma once

#include <linux/kvm.h>
#include <stdint.h>
#include <sys/queue.h>

typedef struct MemoryRegion {
    struct kvm_userspace_memory_region kvm_memory_region;
    LIST_ENTRY(MemoryRegion) list;
} MemoryRegion;

void kvm_map_range(int32_t slot, uint64_t address, uint64_t size, uint64_t pointer);

void kvm_unmap_range(int32_t slot);

void *kvm_translate_guest_physical_to_host(uint64_t address, uint64_t *size);
