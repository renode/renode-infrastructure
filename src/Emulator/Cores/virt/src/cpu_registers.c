/*
 * Copyright (c) 2010-2025 Antmicro
 *
 * This file is licensed under the MIT License.
 */

#include <stdint.h>
#include <sys/ioctl.h>

#include "cpu.h"
#include "cpu_registers.h"
#include "utils.h"
#include "unwind.h"
#ifdef TARGET_X86KVM
#include "x86_reports.h"
#endif


uint64_t *get_reg_pointer(struct kvm_regs *regs, int reg)
{
    switch (reg) {
    case RAX:
        return (uint64_t *)&(regs->rax);
    case RCX:
        return (uint64_t *)&(regs->rcx);
    case RDX:
        return (uint64_t *)&(regs->rdx);
    case RBX:
        return (uint64_t *)&(regs->rbx);
    case RSP:
        return (uint64_t *)&(regs->rsp);
    case RBP:
        return (uint64_t *)&(regs->rbp);
    case RSI:
        return (uint64_t *)&(regs->rsi);
    case RDI:
        return (uint64_t *)&(regs->rdi);
    case RIP:
        return (uint64_t *)&(regs->rip);
    case EFLAGS:
        return (uint64_t *)&(regs->rflags);
        
    default:
        return NULL;
    }
}

uint64_t *get_sreg_pointer(struct kvm_sregs *sregs, int reg)
{
    switch (reg) {
    case CS:
        return (uint64_t *)&(sregs->cs.base);
    case SS:
        return (uint64_t *)&(sregs->ss.base);
    case DS:
        return (uint64_t *)&(sregs->ds.base);
    case ES:
        return (uint64_t *)&(sregs->es.base);
    case FS:
        return (uint64_t *)&(sregs->fs.base);
    case GS:
        return (uint64_t *)&(sregs->gs.base);
        
    case CR0:
        return (uint64_t *)&(sregs->cr0);
    case CR1:
        return (uint64_t *)&(sregs->cr0);
    case CR2:
        return (uint64_t *)&(sregs->cr2);
    case CR3:
        return (uint64_t *)&(sregs->cr3);
    case CR4:
        return (uint64_t *)&(sregs->cr4);
#ifdef TARGET_X86_64KVM
    case CR8:
        return (uint64_t *)&(sregs->cr8);
    case EFER:
        return (uint64_t *)&(sregs->efer);
#endif

    default:
        return NULL;
    }
}

static bool is_special_register(int reg_number) {
    return reg_number >= CS;
}

#define EXPAND_ARGUMENTS(macro, ...) macro(__VA_ARGS__)
#ifdef TARGET_X86_64KVM
#define kvm_get_register_value kvm_get_register_value_64
#define kvm_set_register_value kvm_set_register_value_64
#else
#define kvm_get_register_value kvm_get_register_value_32
#define kvm_set_register_value kvm_set_register_value_32
#endif


reg_t kvm_get_register_value(int reg_number)
{
    uint64_t* ptr = NULL;

    if (is_special_register(reg_number)) {
        struct kvm_sregs *sregs = &cpu->sregs;
        if (cpu->sregs_state == CLEAR)
        {
            get_sregs(sregs);
            cpu->sregs_state = PRESENT;
        }
        ptr = get_sreg_pointer(sregs, reg_number);
    } else {
        struct kvm_regs regs;
        get_regs(&regs);
        ptr = get_reg_pointer(&regs, reg_number);
    }

    if (ptr == NULL) {
        kvm_abortf("Read from undefined CPU register number %d detected", reg_number);
    }

#ifdef TARGET_X86KVM
    if (*ptr > UINT32_MAX) {
        handle_64bit_register_value(reg_number, *ptr);
    }
#endif

    return *ptr;
}
EXPAND_ARGUMENTS(EXC_INT_1, reg_t, kvm_get_register_value, int, reg_number)

void kvm_set_register_value(int reg_number, reg_t value)
{
    struct kvm_regs regs;
    struct kvm_sregs *sregs = &(cpu->sregs);
    uint64_t *ptr = NULL;

    if (is_special_register(reg_number)) {
        if (cpu->sregs_state == CLEAR) {
            get_sregs(sregs);
        }
        ptr = get_sreg_pointer(sregs, reg_number);
    } else {
        get_regs(&regs);
        ptr = get_reg_pointer(&regs, reg_number);
    }

    if (ptr == NULL) {
        kvm_abortf("Write to undefined CPU register number %d detected", reg_number);
    }

    *ptr = value;

    if (is_special_register(reg_number)) {
        cpu->sregs_state = DIRTY;
    } else {
        set_regs(&regs);
    }
}
EXPAND_ARGUMENTS(EXC_VOID_2, kvm_set_register_value, int, reg_number, reg_t, value)


#define GET_FIELD(val, offset, width) ((uint8_t)(((val) >> (offset)) & (0xff >> (8 - (width)))))

#define SECTOR_DESCRIPTOR_SETTER(name)                                                                              \
    void kvm_set_##name##_descriptor(uint64_t base, uint32_t limit, uint16_t selector, uint32_t flags)              \
    {                                                                                                               \
        struct kvm_sregs *sregs = &(cpu->sregs);                                                                    \
        if (cpu->sregs_state == CLEAR) {                                                                            \
            get_sregs(sregs);                                                                                       \
        }                                                                                                           \
                                                                                                                    \
        sregs->name.base = base;                                                                                    \
        sregs->name.limit = limit;                                                                                  \
        sregs->name.selector = selector;                                                                            \
        sregs->name.type = GET_FIELD(flags, 8, 4);                                                                  \
        sregs->name.present = GET_FIELD(flags, 15, 1);                                                              \
        sregs->name.dpl = GET_FIELD(flags, 13, 2);                                                                  \
        sregs->name.db = GET_FIELD(flags, 22, 1);                                                                   \
        sregs->name.s = GET_FIELD(flags, 12, 1);                                                                    \
        sregs->name.l = GET_FIELD(flags, 21, 1);                                                                    \
        sregs->name.g = GET_FIELD(flags, 23, 1);                                                                    \
        sregs->name.avl = GET_FIELD(flags, 20, 1);                                                                  \
                                                                                                                    \
        cpu->sregs_state = DIRTY;                                                                                   \
    }                                                                                                               \
                                                                                                                    \
    EXC_VOID_4(kvm_set_##name##_descriptor, uint64_t, base, uint32_t, limit, uint16_t, selector, uint32_t, flags)

/* Segment descriptor setters
 * For more info plase refer to Intel(R) 64 and IA-32 Architectures Software Developer’s Manual Volume 3 (3.4.3) */
SECTOR_DESCRIPTOR_SETTER(cs)
SECTOR_DESCRIPTOR_SETTER(ds)
SECTOR_DESCRIPTOR_SETTER(es)
SECTOR_DESCRIPTOR_SETTER(ss)
SECTOR_DESCRIPTOR_SETTER(fs)
SECTOR_DESCRIPTOR_SETTER(gs)
