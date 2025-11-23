/*
 * Copyright (c) 2010-2025 Antmicro
 *
 * This file is licensed under the MIT License.
 */

#include <errno.h>
#include <linux/kvm.h>
#include <sys/ioctl.h>
#include <sys/queue.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#include "cpu.h"
#include "debug.h"
#include "memory_range.h"
#include "utils.h"
#include "unwind.h"


void kvm_add_breakpoint(uint64_t address)
{
    if (is_breakpoint_address(address)) {
        return;
    }

    uint64_t phys_address = kvm_translate_guest_virtual_address(address);
    if (phys_address == UINT64_MAX) {
        kvm_logf(LOG_LEVEL_WARNING, "Cannot add a breakpoint on address 0x%lx, it is outside mapped memory", address);
        return;
    }

    uint64_t size;
    void* host_address = kvm_translate_guest_physical_to_host(phys_address, &size);
    if (host_address == NULL) {
        kvm_logf(LOG_LEVEL_WARNING, "Cannot add a breakpoint on address 0x%lx, it does not map to memory", address);
        return;
    }

    Breakpoint *bp = malloc(sizeof(Breakpoint));
    if (bp == NULL) {
        kvm_abortf("Malloc failed");
    }

    bp->pc = address;
    bp->host_code_position = host_address;
    bp->code_byte = *(bp->host_code_position);

    *(bp->host_code_position) = TRAP_OPCODE;

    LIST_INSERT_HEAD(&cpu->breakpoints, bp, list);
}
EXC_VOID_1(kvm_add_breakpoint, uint64_t, address)

void kvm_remove_breakpoint(uint64_t address)
{
    Breakpoint* bp;
    LIST_FOREACH(bp, &cpu->breakpoints, list) {
        if (bp->pc == address) {
            *(bp->host_code_position) = bp->code_byte;
            LIST_REMOVE(bp, list);
            free(bp);
            return;
        }
    }

    kvm_logf(LOG_LEVEL_WARNING, "Breakpoint on address 0x%lx does not exist", address);
}
EXC_VOID_1(kvm_remove_breakpoint, uint64_t, address)

bool is_breakpoint_address(uint64_t address)
{
    Breakpoint* bp;
    LIST_FOREACH(bp, &cpu->breakpoints, list) {
        if (bp->pc == address) {
            return true;
        }
    }

    return false;
}

uint64_t kvm_translate_guest_virtual_address(uint64_t address)
{
    struct kvm_translation address_translation = (struct kvm_translation){
        .linear_address = address
    };

    /* Currently 'KVM_TRANSLATE' is only supported on x86 cpus */
    if (ioctl(cpu->vcpu_fd, KVM_TRANSLATE, &address_translation) < 0) {
        kvm_logf(LOG_LEVEL_WARNING, "KVM_TRANSLATE: %s", strerror(errno));
        return UINT64_MAX;
    }
    return address_translation.physical_address;
}
EXC_VALUE_1(uint64_t, kvm_translate_guest_virtual_address, 0, uint64_t, address)
