#pragma once

#include "cpu.h"

void kvm_abortf(char *fmt, ...);

void get_regs(struct kvm_regs *regs);

void set_regs(struct kvm_regs *regs);

void get_sregs(struct kvm_sregs *sregs);

void set_sregs(struct kvm_sregs *sregs);
