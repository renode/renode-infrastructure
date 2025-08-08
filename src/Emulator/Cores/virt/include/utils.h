#pragma once

#include "cpu.h"

void kvm_abortf(const char *fmt, ...);


typedef enum {
    LOG_LEVEL_NOISY = -1,
    LOG_LEVEL_DEBUG = 0,
    LOG_LEVEL_INFO = 1,
    LOG_LEVEL_WARNING = 2,
    LOG_LEVEL_ERROR = 3,
} LogLevel;

void kvm_logf(LogLevel level, const char *fmt, ...);

void get_regs(struct kvm_regs *regs);

void set_regs(struct kvm_regs *regs);

void get_sregs(struct kvm_sregs *sregs);

void set_sregs(struct kvm_sregs *sregs);
