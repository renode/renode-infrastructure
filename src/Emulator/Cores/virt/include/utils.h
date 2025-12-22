#pragma once

#include "cpu.h"

void kvm_abortf(const char *fmt, ...);

void kvm_runtime_abortf(const char *fmt, ...);

typedef enum {
    LOG_LEVEL_NOISY = -1,
    LOG_LEVEL_DEBUG = 0,
    LOG_LEVEL_INFO = 1,
    LOG_LEVEL_WARNING = 2,
    LOG_LEVEL_ERROR = 3,
} LogLevel;

void kvm_logf(LogLevel level, const char *fmt, ...);

#define IOCTL_RETRY_LIMIT 10

//  Wrapper that will retry ioctl on EINTR a maximum of IOCTL_RETRY_LIMIT times.
int ioctl_with_retry(int fd, unsigned long op, ...);
