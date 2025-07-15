/*
 * Copyright (c) 2010-2025 Antmicro
 *
 * This file is licensed under the MIT License.
 */

#include <stdint.h>
#include <stdio.h>
#include <sys/ioctl.h>
#include <errno.h>

#include "cpu.h"
#include "cpu_registers.h"
#include "utils.h"
#include "unwind.h"

uint64_t *get_reg_pointer_64(struct kvm_regs *regs, int reg)
{
    switch (reg) {
    case RAX_64:
        return (uint64_t *)&(regs->rax);
    case RCX_64:
        return (uint64_t *)&(regs->rcx);
    case RDX_64:
        return (uint64_t *)&(regs->rdx);
    case RBX_64:
        return (uint64_t *)&(regs->rbx);
    case RSP_64:
        return (uint64_t *)&(regs->rsp);
    case RBP_64:
        return (uint64_t *)&(regs->rbp);
    case RSI_64:
        return (uint64_t *)&(regs->rsi);
    case RDI_64:
        return (uint64_t *)&(regs->rdi);
    case RIP_64:
        return (uint64_t *)&(regs->rip);
    case EFLAGS_64:
        return (uint64_t *)&(regs->rflags);

    default:
        return NULL;
    }
}

uint64_t *get_sreg_pointer_64(struct kvm_sregs *sregs, int reg)
{
    switch (reg) {
    case CS_64:
        return (uint64_t *)&(sregs->cs.base);
    case SS_64:
        return (uint64_t *)&(sregs->ss.base);
    case DS_64:
        return (uint64_t *)&(sregs->ds.base);
    case ES_64:
        return (uint64_t *)&(sregs->es.base);
    case FS_64:
        return (uint64_t *)&(sregs->fs.base);
    case GS_64:
        return (uint64_t *)&(sregs->gs.base);

    case CR0_64:
        return (uint64_t *)&(sregs->cr0);
    case CR1_64:
        return (uint64_t *)&(sregs->cr0);
    case CR2_64:
        return (uint64_t *)&(sregs->cr2);
    case CR3_64:
        return (uint64_t *)&(sregs->cr3);
    case CR4_64:
        return (uint64_t *)&(sregs->cr4);
    case CR8_64:
        return (uint64_t *)&(sregs->cr8);
    case EFER_64:
        return (uint64_t *)&(sregs->efer);

    default:
        return NULL;
    }
}

static bool is_special_register(int reg_number) {
    return reg_number >= CS_64;
}

uint64_t kvm_get_register_value_64(int reg_number)
{
    uint64_t* ptr = NULL;

    if (is_special_register(reg_number)) {
        struct kvm_sregs sregs;
        get_sregs(&sregs);
        ptr = get_sreg_pointer_64(&sregs, reg_number);
    } else {
        struct kvm_regs regs;
        get_regs(&regs);
        ptr = get_reg_pointer_64(&regs, reg_number);
    }

    if (ptr == NULL) {
        kvm_abortf("Read from undefined CPU register number %d detected", reg_number);
    }

    return *ptr;
}
EXC_INT_1(uint64_t, kvm_get_register_value_64, int, reg_number)

uint32_t kvm_get_register_value_32(int reg_number)
{
    return kvm_get_register_value_64(reg_number);
}
EXC_INT_1(uint32_t, kvm_get_register_value_32, int, reg_number)

void kvm_set_register_value_64(int reg_number, uint64_t value)
{
    struct kvm_regs regs;
    struct kvm_sregs sregs;
    uint64_t *ptr = NULL;

    if (is_special_register(reg_number)) {
        get_sregs(&sregs);
        ptr = get_sreg_pointer_64(&sregs, reg_number);
    } else {
        get_regs(&regs);
        ptr = get_reg_pointer_64(&regs, reg_number);
    }

    if (ptr == NULL) {
        kvm_abortf("Write to undefined CPU register number %d detected", reg_number);
    }

    *ptr = value;

    if (is_special_register(reg_number)) {
        set_sregs(&sregs);
    } else {
        set_regs(&regs);
    }
}
EXC_VOID_2(kvm_set_register_value_64, int, reg_number, uint64_t, value)

void kvm_set_register_value_32(int reg_number, uint32_t value)
{
    kvm_set_register_value_64(reg_number, value);
}
EXC_VOID_2(kvm_set_register_value_32, int, reg_number, uint64_t, value)
