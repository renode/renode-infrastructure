#pragma once

#include <linux/kvm.h>
#include <stdint.h>

#ifdef TARGET_X86_64KVM
typedef uint64_t reg_t;
#else
typedef uint32_t reg_t;
#endif


void get_regs(struct kvm_regs *regs);
void set_regs(struct kvm_regs *regs);

void get_sregs(struct kvm_sregs *sregs);
void set_sregs(struct kvm_sregs *sregs);
