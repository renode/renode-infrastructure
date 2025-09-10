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


void kvm_map_range(int32_t slot, uint64_t address, uint64_t size, uint64_t pointer)
{
    struct kvm_userspace_memory_region region;

    region.slot = slot;
    region.flags = 0;
    region.guest_phys_addr = address;
    region.memory_size = size;
    region.userspace_addr = (uintptr_t)pointer;

    if (ioctl(cpu->vm_fd, KVM_SET_USER_MEMORY_REGION, &region) < 0) {
        kvm_abortf("KVM_SET_USER_MEMORY_REGION: %s", strerror(errno));
    }
}
EXC_VOID_4(kvm_map_range, int32_t, slot, uint64_t, address, uint64_t, size, uint64_t, pointer)

void kvm_unmap_range(int32_t slot)
{
    struct kvm_userspace_memory_region region;

    region.slot = slot;

    // according to the KVM docs, memory region is removed by setting memory_size to 0
    region.memory_size = 0;

    if (ioctl(cpu->vm_fd, KVM_SET_USER_MEMORY_REGION, &region) < 0) {
        kvm_abortf("KVM_SET_USER_MEMORY_REGION: %s", strerror(errno));
    }
}
EXC_VOID_1(kvm_unmap_range, int32_t, slot)
