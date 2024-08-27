/*
 * Copyright (c) 2010-2025 Antmicro
 *
 * This file is licensed under the MIT License.
 */

#include "cpu.h"
#include "callbacks.h"

#include <string.h>
#include <stdarg.h>
#include <stdio.h>
#include <linux/kvm.h>
#include <sys/ioctl.h>
#include <errno.h>

void kvm_abortf(char *fmt, ...)
{
    char result[1024];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(result, 1024, fmt, ap);
    kvm_abort(result);
    va_end(ap);
}

void get_regs(struct kvm_regs *regs) {
    if (ioctl(cpu->vcpu_fd, KVM_GET_REGS, regs) < 0) {
        kvm_abortf("KVM_GET_REGS: %s", strerror(errno));
    }
}

void set_regs(struct kvm_regs *regs) {
    if (ioctl(cpu->vcpu_fd, KVM_SET_REGS, regs) < 0) {
        kvm_abortf("KVM_SET_REGS: %s", strerror(errno));
    }
}

void get_sregs(struct kvm_sregs *sregs) {
    if (ioctl(cpu->vcpu_fd, KVM_GET_SREGS, sregs) < 0) {
        kvm_abortf("KVM_GET_SREGS: %s", strerror(errno));
    }
}

void set_sregs(struct kvm_sregs *sregs) {
    if (ioctl(cpu->vcpu_fd, KVM_SET_SREGS, sregs) < 0) {
        kvm_abortf("KVM_SET_SREGS: %s", strerror(errno));
    }
}
