/*
 * Copyright (c) 2010-2025 Antmicro
 *
 * This file is licensed under the MIT License.
 */

#include <linux/kvm.h>
#include <string.h>
#include <inttypes.h>
#include <errno.h>
#include <sys/ioctl.h>

#include "utils.h"
#include "unwind.h"
#include "cpu.h"
#include "memory_range.h"

void kvm_map_range(int32_t slot, uint64_t address, uint64_t size, uint64_t pointer)
{
    MemoryRegion *memory_region = malloc(sizeof(MemoryRegion));
    if(memory_region == NULL) {
        kvm_abortf("Malloc failed");
    }

    memory_region->kvm_memory_region = (struct kvm_userspace_memory_region) { .slot = slot,
                                                                              .flags = 0,
                                                                              .guest_phys_addr = address,
                                                                              .memory_size = size,
                                                                              .userspace_addr = (uintptr_t)pointer };

    if(ioctl(cpu->vm_fd, KVM_SET_USER_MEMORY_REGION, &memory_region->kvm_memory_region) < 0) {
        free(memory_region);
        kvm_abortf("KVM_SET_USER_MEMORY_REGION: %s", strerror(errno));
    }
    LIST_INSERT_HEAD(&cpu->memory_regions, memory_region, list);
}
EXC_VOID_4(kvm_map_range, int32_t, slot, uint64_t, address, uint64_t, size, uint64_t, pointer)

void kvm_unmap_range(int32_t slot)
{
    MemoryRegion *memory_region = LIST_FIRST(&cpu->memory_regions);
    while(memory_region != NULL && memory_region->kvm_memory_region.slot != slot) {
        memory_region = LIST_NEXT(memory_region, list);
    }

    if(memory_region == NULL) {
        kvm_logf(LOG_LEVEL_ERROR, "KVM unmap range: Unknown KVM memory slot %d", slot);
        return;
    }

    //  according to the KVM docs, memory region is removed by setting memory_size to 0
    memory_region->kvm_memory_region.memory_size = 0;

    if(ioctl(cpu->vm_fd, KVM_SET_USER_MEMORY_REGION, &memory_region->kvm_memory_region) < 0) {
        kvm_abortf("KVM_SET_USER_MEMORY_REGION: %s", strerror(errno));
    }

    LIST_REMOVE(memory_region, list);
    free(memory_region);
}
EXC_VOID_1(kvm_unmap_range, int32_t, slot)

void *kvm_translate_guest_physical_to_host(uint64_t address, uint64_t *size)
{
    MemoryRegion *memory_region = LIST_FIRST(&cpu->memory_regions);
    while(memory_region != NULL) {
        uint64_t guest_phys_address = memory_region->kvm_memory_region.guest_phys_addr;
        uint64_t memory_size = memory_region->kvm_memory_region.memory_size;
        if(guest_phys_address <= address && address < guest_phys_address + memory_size) {
            uint64_t offset = address - guest_phys_address;
            *size = memory_size - offset;
            return (void *)(memory_region->kvm_memory_region.userspace_addr + offset);
        }
        memory_region = LIST_NEXT(memory_region, list);
    }

    return NULL;
}
