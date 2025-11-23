/*
 * Copyright (c) 2010-2025 Antmicro
 *
 * This file is licensed under the MIT License.
 */

#include "callbacks.h"
#include "registers.h"
#include "utils.h"

#include <stdarg.h>
#include <stdio.h>
#include <linux/kvm.h>
#include <sys/ioctl.h>

#define VSNPRINTF_BUFFER_SIZE 1024


void kvm_abortf(const char *fmt, ...)
{
    char result[VSNPRINTF_BUFFER_SIZE];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(result, VSNPRINTF_BUFFER_SIZE, fmt, ap);
    kvm_abort(result);
    va_end(ap);
}

void kvm_runtime_abortf(const char *fmt, ...)
{
    uint64_t pc = get_register_value(RIP);

    char result[VSNPRINTF_BUFFER_SIZE];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(result, VSNPRINTF_BUFFER_SIZE, fmt, ap);
    kvm_runtime_abort(result, pc);
    va_end(ap);
}

void kvm_logf(LogLevel level, const char *fmt, ...)
{
    char result[VSNPRINTF_BUFFER_SIZE];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(result, VSNPRINTF_BUFFER_SIZE, fmt, ap);
    kvm_log(level, result);
    va_end(ap);
}
