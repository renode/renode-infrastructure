/*
 * Copyright (c) 2011-2017 Fabrice Bellard
 * Copyright (c) 2021-2025 Antmicro <www.antmicro.com>
 *
 * This file is licensed under the MIT License.
 *
 * This code uses KVM API, documentation for it may be found on
 * https://docs.kernel.org/virt/kvm/api.html
 */
#include "callbacks.h"
#include "cpu.h"
#include "debug.h"
#include "memory_range.h"
#include "registers.h"
#include "utils.h"
#include "unwind.h"
#ifdef TARGET_X86KVM
#include "x86_reports.h"
#endif

#include <errno.h>
#include <fcntl.h>
#include <inttypes.h>
#include <linux/kvm.h>
#include <signal.h>
#include <stdint.h>
#include <string.h>
#include <sys/ioctl.h>
#include <sys/mman.h>
#include <sys/queue.h>
#include <sys/time.h>

#define USEC_IN_SEC 1000000

#define CPUID_APIC (1 << 9)
#define CPUID_ACPI (1 << 22)

#define CPUID_MAX_NUMBER_OF_ENTRIES 128
#define CPUID_FEATURE_INFO 0x1
#define CPUID_FEATURE_INFO_EXTENDED 0x80000001

#define DEFAULT_DEBUG_FLAGS (KVM_GUESTDBG_ENABLE | KVM_GUESTDBG_USE_SW_BP)
#define SINGLE_STEP_DEBUG_FLAGS (KVM_GUESTDBG_ENABLE | KVM_GUESTDBG_SINGLESTEP)

CpuState *cpu;
__thread struct unwind_state unwind_state;
static void kvm_set_cpuid(CpuState *s)
{
    struct kvm_cpuid2 *kvm_cpuid;

    kvm_cpuid = calloc(sizeof(struct kvm_cpuid2) + CPUID_MAX_NUMBER_OF_ENTRIES, sizeof(kvm_cpuid->entries[0]));
    if (kvm_cpuid == NULL) {
        kvm_abort("Calloc failed");
    }

    kvm_cpuid->nent = CPUID_MAX_NUMBER_OF_ENTRIES;
    if (ioctl(s->kvm_fd, KVM_GET_SUPPORTED_CPUID, kvm_cpuid) < 0) {
        kvm_abortf("KVM_GET_SUPPORTED_CPUID: %s", strerror(errno));
    }

    if (ioctl(s->vcpu_fd, KVM_SET_CPUID2, kvm_cpuid) < 0) {
        kvm_abortf("KVM_SET_CPUID2: %s", strerror(errno));
    }
    free(kvm_cpuid);
}

static void set_debug_flags(uint32_t flags)
{
    /* Changing debug flags may alter sregs, make sure they are up to date */
    kvm_registers_synchronize();
    struct kvm_guest_debug debug = {
        .control = flags,
    };
    if (ioctl(cpu->vcpu_fd, KVM_SET_GUEST_DEBUG, &debug) < 0) {
        kvm_runtime_abortf("KVM_SET_GUEST_DEBUG: %s", strerror(errno));
    }
}

static void cpu_init(CpuState *s)
{
    int ret;
    struct kvm_pit_config pit_config;
    uint64_t base_addr;

    s->kvm_fd = open("/dev/kvm", O_RDWR);
    if (s->kvm_fd < 0) {
        kvm_abort("KVM not available");
    }
    ret = ioctl(s->kvm_fd, KVM_GET_API_VERSION, 0);
    if (ret < 0) {
        kvm_abortf("KVM_GET_API_VERSION: %s", strerror(errno));
    }
    if (ret != 12) {
        close(s->kvm_fd);
        kvm_abort("Only version 12 of KVM is currently supported");
    }
    s->vm_fd = ioctl(s->kvm_fd, KVM_CREATE_VM, 0);
    if (s->vm_fd < 0) {
        kvm_abortf("KVM_CREATE_VM: %s", strerror(errno));
    }

    /* just before the BIOS */
    base_addr = 0xfffbc000;
    if (ioctl(s->vm_fd, KVM_SET_IDENTITY_MAP_ADDR, &base_addr) < 0) {
        kvm_abortf("KVM_SET_IDENTITY_MAP_ADDR: %s", strerror(errno));
    }

    if (ioctl(s->vm_fd, KVM_SET_TSS_ADDR, (long)(base_addr + 0x1000)) < 0) {
        kvm_abortf("KVM_SET_TSS_ADDR: %s", strerror(errno));
    }

    if (ioctl(s->vm_fd, KVM_CREATE_IRQCHIP, 0) < 0) {
        kvm_abortf("KVM_CREATE_IRQCHIP: %s", strerror(errno));
    }

    memset(&pit_config, 0, sizeof(pit_config));
    pit_config.flags = KVM_PIT_SPEAKER_DUMMY;
    if (ioctl(s->vm_fd, KVM_CREATE_PIT2, &pit_config)) {
        kvm_abortf("KVM_CREATE_PIT2: %s", strerror(errno));
    }

    s->vcpu_fd = ioctl(s->vm_fd, KVM_CREATE_VCPU, 0);
    if (s->vcpu_fd < 0) {
        kvm_abortf("KVM_CREATE_VCPU: %s", strerror(errno));
    }

    kvm_set_cpuid(s);

    /* map the kvm_run structure */
    s->kvm_run_size = ioctl(s->kvm_fd, KVM_GET_VCPU_MMAP_SIZE, NULL);
    if (s->kvm_run_size < 0) {
        kvm_abortf("KVM_GET_VCPU_MMAP_SIZE: %s", strerror(errno));
    }

    s->kvm_run = mmap(NULL, s->kvm_run_size, PROT_READ | PROT_WRITE,
                      MAP_SHARED, s->vcpu_fd, 0);
    if (!s->kvm_run) {
        kvm_abortf("mmap kvm_run: %s", strerror(errno));
    }

    set_debug_flags(DEFAULT_DEBUG_FLAGS);

    cpu->single_step = false;
    cpu->regs_state = cpu->sregs_state = CLEAR;
    cpu->is_executing = false;
}

static void kill_cpu_thread(int sig)
{
    if (!cpu->is_executing) {
        return;
    }

    if (tgkill(cpu->tgid, cpu->tid, sig) < 0) {
        /* ESRCH means there is no such process. Such situation may occur when cpu thread already exited. */
        if (errno != ESRCH) {
            kvm_runtime_abortf("tgkill: %s", strerror(errno));
        }
    }
}

static void sigalarm_handler(int sig)
{
    cpu->kvm_run->immediate_exit = true;

    if (gettid() != cpu->tid) {
        /* we are not the CPU thread, redirect signal */
        kill_cpu_thread(SIGALRM);
    }
}

void kvm_init()
{
    sigset_t new_set, old_set;
    sigemptyset(&new_set);
    sigaddset(&new_set, SIGALRM);
    sigprocmask(SIG_UNBLOCK, &new_set, &old_set);

    struct sigaction act;

    act.sa_handler = sigalarm_handler;
    sigemptyset(&act.sa_mask);
    act.sa_flags = 0;
    sigaction(SIGALRM, &act, NULL);

    cpu = calloc(1, sizeof(*cpu));
    if (cpu == NULL) {
        kvm_abort("Calloc failed");
    }

    cpu_init(cpu);
}
EXC_VOID_0(kvm_init)

/* Set interrupt with interrupt number to specific level.
 * Possible levels are 1 (active) and 0 (inactive). */
void kvm_set_irq(int level, int interrupt_number)
{
    struct kvm_irq_level irq_level;
    irq_level.irq = interrupt_number;
    irq_level.level = level;
    if (ioctl(cpu->vm_fd, KVM_IRQ_LINE, &irq_level) < 0) {
        kvm_runtime_abortf("KVM_IRQ_LINE");
    }
}
EXC_VOID_2(kvm_set_irq, int, level, int, interrupt_number)

#ifdef TARGET_X86KVM
void kvm_set64_bit_behaviour(uint32_t on64BitDetected)
{
    cpu->on64BitDetected = (Detected64BitBehaviour)on64BitDetected;
}
EXC_VOID_1(kvm_set64_bit_behaviour, uint32_t, on64BitDetected)
#endif

static void kvm_exit_io(CpuState *s, struct kvm_run *run)
{
    uint8_t *ptr;
    int i;
    ptr = (uint8_t *)run + run->io.data_offset;

    for(i = 0; i < run->io.count; i++) {
        if (run->io.direction == KVM_EXIT_IO_OUT) {
            switch(run->io.size) {
            case 1:
                kvm_io_port_write_byte(run->io.port, *(uint8_t *)ptr);
                break;
            case 2:
                kvm_io_port_write_word(run->io.port, *(uint16_t *)ptr);
                break;
            case 4:
                kvm_io_port_write_double_word(run->io.port, *(uint32_t *)ptr);
                break;
            default:
                kvm_runtime_abortf("invalid io access width: %d bytes", run->io.size);
            }
        } else {
            switch(run->io.size) {
            case 1:
                *(uint8_t *)ptr = kvm_io_port_read_byte(run->io.port);
                break;
            case 2:
                *(uint16_t *)ptr = kvm_io_port_read_word(run->io.port);
                break;
            case 4:
                *(uint32_t *)ptr = kvm_io_port_read_double_word(run->io.port);
                break;
            default:
                kvm_runtime_abortf("invalid io access width: %d bytes", run->io.size);
            }
        }
        ptr += run->io.size;
    }
}

static void kvm_exit_mmio(CpuState *s, struct kvm_run *run)
{
    uint8_t *data = run->mmio.data;
    uint64_t addr = run->mmio.phys_addr;

    #ifdef TARGET_X86KVM
    if (addr > UINT32_MAX) {
        handle_64bit_access(INVALID_ACCESS_64BIT_ADDRESS, run->mmio.len, run->mmio.is_write, addr);
    }
#endif

    if (run->mmio.is_write) {
        switch(run->mmio.len) {
        case 1:
            kvm_sysbus_write_byte(addr, *(uint8_t *)data);
            break;
        case 2:
            kvm_sysbus_write_word(addr, *(uint16_t *)data);
            break;
        case 4:
            kvm_sysbus_write_double_word(addr, *(uint32_t *)data);
            break;
        case 8:
#ifdef TARGET_X86KVM
            handle_64bit_access(INVALID_ACCESS_64BIT_WIDTH, 8, true, addr);
#endif
            kvm_sysbus_write_quad_word(addr, *(uint64_t *)data);
            break;
        default:
            kvm_runtime_abortf("invalid mmio access width: %d bytes", run->mmio.len);
        }
    } else {
        switch(run->mmio.len) {
        case 1:
            *(uint8_t *)data = kvm_sysbus_read_byte(addr);
            break;
        case 2:
            *(uint16_t *)data = kvm_sysbus_read_word(addr);
            break;
        case 4:
            *(uint32_t *)data = kvm_sysbus_read_double_word(addr);
            break;
        case 8:
#ifdef TARGET_X86KVM
            handle_64bit_access(INVALID_ACCESS_64BIT_WIDTH, 8, false, addr);
#endif
            *(uint64_t *)data = kvm_sysbus_read_quad_word(addr);
            break;
        default:
            kvm_runtime_abortf("invalid mmio access width: %d bytes", run->mmio.len);
        }
    }
}

static void execution_timer_set(uint64_t timeout_in_us)
{
    struct itimerval ival;
    ival.it_interval.tv_sec = 0;
    ival.it_interval.tv_usec = 0;

    if (timeout_in_us > 0) {
        ival.it_value.tv_sec = timeout_in_us / USEC_IN_SEC;
        ival.it_value.tv_usec = timeout_in_us % USEC_IN_SEC;
    } else {
        /* timeout_in_us == 0 means the time quantum assigned was smaller than one micro second.
         * Assign one us to interrupt execution as soon as possible. */
        ival.it_value.tv_sec = 0;
        ival.it_value.tv_usec = 1;
    }

    if (setitimer(ITIMER_REAL, &ival, NULL) < 0)
        kvm_runtime_abortf("setitimer: %s", strerror(errno));
}

static void execution_timer_disarm()
{
    struct itimerval ival;
    ival.it_value.tv_sec = 0;
    ival.it_value.tv_usec = 0;
    ival.it_interval.tv_sec = 0;
    ival.it_interval.tv_usec = 0;
    if (setitimer(ITIMER_REAL, &ival, NULL) < 0)
        kvm_runtime_abortf("setitimer: %s", strerror(errno));
}

/* Run KVM. Returns true if run was interrupted by planned timer. */
static bool kvm_run()
{
    if (ioctl(cpu->vcpu_fd, KVM_RUN, 0) < 0) {
        if (errno == EINTR) {
            /* We were interrupted by the signal.
             * If it was SIGALRM, cpu->timer_expired is set and we will finish the execution.
             * Otherwise, signal is ignored. */
            return true;
        }
        kvm_runtime_abortf("KVM_RUN: %s", strerror(errno));
    }

    return false;
}

static ExecutionResult kvm_run_loop()
{
    kvm_registers_synchronize();
    kvm_registers_invalidate();

    cpu->tgid = getpid();
    cpu->tid = gettid();
    cpu->is_executing = true;

    ExecutionResult execution_result = OK;
    bool override_exception_capture = false;

    /* timer_expired flag will be set by the SIGALRM handler */
    while(true) {
        if (kvm_run()) {
            execution_result = OK;
            goto finalize;
        }

        struct kvm_run *run = cpu->kvm_run;

        /* KVM exited - check for possible reasons */
        switch(run->exit_reason) {
        case KVM_EXIT_IO:
            /* handle IN / OUT instructions */
            kvm_exit_io(cpu, run);
            break;
        case KVM_EXIT_MMIO:
            /* handle sysbus accesses */
            kvm_exit_mmio(cpu, run);
            break;
        case KVM_EXIT_DEBUG:
            /* this case occurs when single-stepping is enabled or a software event was triggered */
            if (is_breakpoint_address(run->debug.arch.pc)) {
                execution_result = STOPPED_AT_BREAKPOINT;
                goto finalize;
            }
            if (cpu->single_step) {
                execution_result = OK;
                goto finalize;
            }
            if (override_exception_capture) {
                set_debug_flags(DEFAULT_DEBUG_FLAGS);
                override_exception_capture = false;
                break;
            }

            /* KVM_GUESTDBG_USE_SW_BP causes us to capture all exceptions, even ones guest software
             * would expect to handle by itself. If we encounter an exception and do not expect
             * it then this instruction will be single-stepped with exception capture turned off.
             * This will allow guest to jump to exception handler from which we can continue to
             * capture exceptions */
            kvm_logf(LOG_LEVEL_DEBUG, "KVM_EXIT_DEBUG: exception=0x%lx at pc 0x%lx, turning off interrupt capture for this instruction", run->debug.arch.exception, run->debug.arch.pc);
            set_debug_flags(SINGLE_STEP_DEBUG_FLAGS);
            override_exception_capture = true;
            break;
        case KVM_EXIT_FAIL_ENTRY:
            kvm_runtime_abortf("KVM_EXIT_FAIL_ENTRY: reason=0x%" PRIx64 "\n",
                        (uint64_t)run->fail_entry.hardware_entry_failure_reason);
            break;
        case KVM_EXIT_INTERNAL_ERROR:
            kvm_runtime_abortf("KVM_EXIT_INTERNAL_ERROR: suberror=0x%x\n",
                        (uint32_t)run->internal.suberror);
            break;
        case KVM_EXIT_SHUTDOWN:
            kvm_runtime_abortf("KVM shutdown requested");
            break;
        default:
            kvm_runtime_abortf("KVM: unsupported exit_reason=%d\n", run->exit_reason);
        }
    }

finalize:
    cpu->is_executing = false;
    return execution_result;
}

/* Run KVM execution for time_in_us microseconds. */
uint64_t kvm_execute(uint64_t time_in_us)
{
    cpu->single_step = false;
    cpu->kvm_run->immediate_exit = false;

    execution_timer_set(time_in_us);

    ExecutionResult result = kvm_run_loop();
    if (result != OK) {
        /* Disarm timer if it did not cause the exit */
        execution_timer_disarm();
    }
    return result;
}
EXC_VALUE_1(uint64_t, kvm_execute, 0, uint64_t, time)

/* Run KVM execution for single instruction. */
uint64_t kvm_execute_single_step()
{
    cpu->single_step = true;
    cpu->kvm_run->immediate_exit = false;

    set_debug_flags(SINGLE_STEP_DEBUG_FLAGS);
    ExecutionResult result = kvm_run_loop();
    set_debug_flags(DEFAULT_DEBUG_FLAGS);
    return (uint64_t)result;
}
EXC_VALUE_0(uint64_t, kvm_execute_single_step, 0)

void kvm_interrupt_execution()
{
    execution_timer_disarm();
    cpu->kvm_run->immediate_exit = true;
    kill_cpu_thread(SIGALRM);
}
EXC_VOID_0(kvm_interrupt_execution)

void kvm_dispose()
{
    /* Make sure we are not executing KVMCPU before disposing */
    kvm_interrupt_execution();

    munmap(cpu->kvm_run, cpu->kvm_run_size);

    close(cpu->vcpu_fd);
    close(cpu->vm_fd);
    close(cpu->kvm_fd);

    Breakpoint *bp = LIST_FIRST(&cpu->breakpoints);
    while (bp != NULL) {
        Breakpoint* next = LIST_NEXT(bp, list);
        free(bp);
        bp = next;
    }

    MemoryRegion *mr = LIST_FIRST(&cpu->memory_regions);
    while (mr != NULL) {
        MemoryRegion* next = LIST_NEXT(mr, list);
        free(mr);
        mr = next;
    }

    free(cpu);
}
EXC_VOID_0(kvm_dispose)
