#pragma once

#include <time.h>
#include <linux/kvm.h>
#include <sys/types.h>
#include <stdbool.h>
#include <unistd.h>
#include <sys/queue.h>
#include <sys/syscall.h>
#include <stdint.h>

#include "debug.h"
#include "memory_range.h"

#ifndef SYS_gettid
#error "SYS_gettid unavailable on this system"
#endif

#define gettid() ((pid_t)syscall(SYS_gettid))

#ifndef SYS_tgkill
#error "SYS_tgkill unavailable on this system"
#endif

#define tgkill(tgid, tid, sig) ((int)syscall(SYS_tgkill, tgid, tid, sig))

typedef enum {
    OK = 0,
    INTERRUPTED = 1,
    WAITING_FOR_INTERRUPT = 2,
    STOPPED_AT_BREAKPOINT = 3,
    STOPPED_AT_WATCHPOINT = 4,
    EXTERNAL_MMU_FAULT = 5,
    ABORTED = UINT64_MAX
} ExecutionResult;

typedef enum {
    CLEAR,
    PRESENT,
    DIRTY
} RegisterState;

#ifdef TARGET_X86KVM
typedef enum
{
    FAULT = 0,
    WARN = 1,
    IGNORE = 2,
} Detected64BitBehaviour;
#endif

typedef struct CpuState {
    bool is_executing;
    pid_t tid;  /* id of cpu thread, valid when is_executing */
    pid_t tgid; /* id of cpu process, valid when is_executing */

    /* KVM specific file descriptors */
    int kvm_fd;
    int vm_fd;
    int vcpu_fd;

    int kvm_run_size;
    /* struct containing KVM execution details */
    struct kvm_run *kvm_run;

    /* Flag set when KVM is set to single stepping mode */
    bool single_step;

    /* cached special register state */
    struct kvm_regs regs;
    RegisterState regs_state;

    /* cached special register state */
    struct kvm_sregs sregs;
    RegisterState sregs_state;

    LIST_HEAD(, MemoryRegion) memory_regions;

    LIST_HEAD(, Breakpoint) breakpoints;

#ifdef TARGET_X86KVM
    Detected64BitBehaviour on64BitDetected;
#endif
} CpuState;

extern struct CpuState *cpu;
