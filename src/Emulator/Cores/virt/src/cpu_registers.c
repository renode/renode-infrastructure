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

uint32_t *get_reg_pointer_32(struct kvm_regs *regs, int reg)
{
    switch (reg) {
    case EAX_32:
        return (uint32_t *)&(regs->rax);
    case ECX_32:
        return (uint32_t *)&(regs->rcx);
    case EDX_32:
        return (uint32_t *)&(regs->rdx);
    case EBX_32:
        return (uint32_t *)&(regs->rbx);
    case ESP_32:
        return (uint32_t *)&(regs->rsp);
    case EBP_32:
        return (uint32_t *)&(regs->rbp);
    case ESI_32:
        return (uint32_t *)&(regs->rsi);
    case EDI_32:
        return (uint32_t *)&(regs->rdi);
    case EIP_32:
        return (uint32_t *)&(regs->rip);
    case EFLAGS_32:
        return (uint32_t *)&(regs->rflags);

    default:
        return NULL;
    }
}

uint32_t *get_sreg_pointer_32(struct kvm_sregs *sregs, int reg)
{
    switch (reg) {
    case CS_32:
        return (uint32_t *)&(sregs->cs.base);
    case SS_32:
        return (uint32_t *)&(sregs->ss.base);
    case DS_32:
        return (uint32_t *)&(sregs->ds.base);
    case ES_32:
        return (uint32_t *)&(sregs->es.base);
    case FS_32:
        return (uint32_t *)&(sregs->fs.base);
    case GS_32:
        return (uint32_t *)&(sregs->gs.base);

    case CR0_32:
        return (uint32_t *)&(sregs->cr0);
    case CR1_32:
        return (uint32_t *)&(sregs->cr0);
    case CR2_32:
        return (uint32_t *)&(sregs->cr2);
    case CR3_32:
        return (uint32_t *)&(sregs->cr3);
    case CR4_32:
        return (uint32_t *)&(sregs->cr4);

    default:
        return NULL;
    }
}

static bool is_special_register(int reg_number) {
    return reg_number >= CS_32;
}

uint32_t kvm_get_register_value_32(int reg_number)
{
    uint32_t* ptr = NULL;

    if (is_special_register(reg_number)) {
        struct kvm_sregs sregs;
        get_sregs(&sregs);
        ptr = get_sreg_pointer_32(&sregs, reg_number);
    } else {
        struct kvm_regs regs;
        get_regs(&regs);
        ptr = get_reg_pointer_32(&regs, reg_number);
    }

    if(ptr == NULL) {
        kvm_abortf("Read from undefined CPU register number %d detected", reg_number);
    }

    return *ptr;
}

EXC_INT_1(uint32_t, kvm_get_register_value_32, int, reg_number)

void kvm_set_register_value_32(int reg_number, uint32_t value)
{
    struct kvm_regs regs;
    struct kvm_sregs sregs;
    uint32_t *ptr = NULL;

    if (is_special_register(reg_number)) {
        get_sregs(&sregs);
        ptr = get_sreg_pointer_32(&sregs, reg_number);
    } else {
        get_regs(&regs);
        ptr = get_reg_pointer_32(&regs, reg_number);
    }

    if(ptr == NULL) {
        kvm_abortf("Write to undefined CPU register number %d detected", reg_number);
    }

    *ptr = value;

    if (is_special_register(reg_number)) {
        set_sregs(&sregs);
    } else {
        set_regs(&regs);
    }
}

EXC_VOID_2(kvm_set_register_value_32, int, reg_number, uint32_t, value)
