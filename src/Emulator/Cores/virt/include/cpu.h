#pragma once

#include <time.h>
#include <linux/kvm.h>
#include <sys/types.h>
#include <stdbool.h>
#include <unistd.h>
#include <sys/syscall.h>
#include <stdint.h>

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
} execution_result;

typedef struct cpu_state {
    pid_t tid;  /* id of cpu thread */
    pid_t tgid; /* id of cpu process */

    /* number of microseconds since calling kvm_execute */
    ulong execution_time_in_us;

    /* KVM specific file descriptors */
    int kvm_fd;
    int vm_fd;
    int vcpu_fd;
    /* struct containing KVM execution details */
    struct kvm_run *kvm_run;

    /* flag set when time limit for execution is reached */
    bool timer_expired;
    /* flag set when there is exit request from C# */
    bool exit_request;
} cpu_state;

extern struct cpu_state *cpu;
