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
#include "utils.h"
#include "unwind.h"
#ifdef TARGET_X86KVM
#include "x86_reports.h"
#endif

#include <linux/kvm.h>
#include <string.h>
#include <inttypes.h>
#include <fcntl.h>
#include <errno.h>
#include <sys/mman.h>
#include <sys/ioctl.h>
#include <signal.h>
#include <sys/time.h>

#define USEC_IN_SEC 1000000

#define CPUID_APIC (1 << 9)
#define CPUID_ACPI (1 << 22)

#define CPUID_MAX_NUMBER_OF_ENTRIES 128
#define CPUID_FEATURE_INFO 0x1
#define CPUID_FEATURE_INFO_EXTENDED 0x80000001

CpuState *cpu;
__thread struct unwind_state unwind_state;
static void kvm_set_cpuid(CpuState *s)
{
    struct kvm_cpuid2 *kvm_cpuid;

    kvm_cpuid = calloc(sizeof(struct kvm_cpuid2) + CPUID_MAX_NUMBER_OF_ENTRIES, sizeof(kvm_cpuid->entries[0]));

    kvm_cpuid->nent = CPUID_MAX_NUMBER_OF_ENTRIES;
    if (ioctl(s->kvm_fd, KVM_GET_SUPPORTED_CPUID, kvm_cpuid) < 0) {
        kvm_abortf("KVM_GET_SUPPORTED_CPUID: %s", strerror(errno));
    }

    if (ioctl(s->vcpu_fd, KVM_SET_CPUID2, kvm_cpuid) < 0) {
        kvm_abortf("KVM_SET_CPUID2: %s", strerror(errno));
    }
    free(kvm_cpuid);
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
    int kvm_run_size = ioctl(s->kvm_fd, KVM_GET_VCPU_MMAP_SIZE, NULL);
    if (kvm_run_size < 0) {
        kvm_abortf("KVM_GET_VCPU_MMAP_SIZE: %s", strerror(errno));
    }

    s->kvm_run = mmap(NULL, kvm_run_size, PROT_READ | PROT_WRITE,
                      MAP_SHARED, s->vcpu_fd, 0);
    if (!s->kvm_run) {
        kvm_abortf("mmap kvm_run: %s", strerror(errno));
    }

    cpu->exit_requested = false;
    cpu->sregs_state = CLEAR;
}

static void kill_cpu_thread(int sig)
{
    if (tgkill(cpu->tgid, cpu->tid, sig) < 0) {
        /* ESRCH means there is no such process. Such situation may occur when cpu thread already exited. */
        if (errno != ESRCH) {
            kvm_runtime_abortf("tgkill: %s", strerror(errno));
        }
    }
}

static void sigalarm_handler(int sig)
{
    if (gettid() == cpu->tid) {
        cpu->exit_requested = true;
    } else {
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

void kvm_dispose()
{
    free(cpu);
}
EXC_VOID_0(kvm_dispose)

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

/* Get execution time since calling kvm_execute */
uint64_t kvm_get_execution_time_in_us()
{
    return cpu->execution_time_in_us;
}
EXC_VALUE_0(uint64_t, kvm_get_execution_time_in_us, 0)

static void set_next_run_as_single_step() {
    int ret;
    struct kvm_guest_debug debug = {
        .control = KVM_GUESTDBG_ENABLE | KVM_GUESTDBG_SINGLESTEP,
    };
    ret = ioctl(cpu->vcpu_fd, KVM_SET_GUEST_DEBUG, &debug);
    if (ret < 0) {
        kvm_runtime_abortf("KVM_SET_GUEST_DEBUG: %s", strerror(errno));
        exit(1);
    }
}

/* Run KVM and handle it's exit reason */
void kvm_run()
{
    if (cpu->sregs_state == DIRTY) {
        set_sregs(&(cpu->sregs));
    }
    cpu->sregs_state = CLEAR;

    struct kvm_run *run = cpu->kvm_run;
    int ret;

    ret = ioctl(cpu->vcpu_fd, KVM_RUN, 0);
    if (ret < 0) {
        if (errno == EINTR) {
            /* We were interrupted by the signal.
             * If it was SIGALRM, cpu->timer_expired is set and we will finish the execution.
             * Otherwise, signal is ignored. */
            return;
        }
        kvm_runtime_abortf("KVM_RUN: %s", strerror(errno));
    }

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
        /* this case occurs when single-stepping is enabled */
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

/* Run KVM execution for time_in_us microseconds. */
uint64_t kvm_execute(uint64_t time_in_us)
{
    cpu->tgid = getpid();
    cpu->tid = gettid();

    cpu->exit_requested = false;

    execution_timer_set(time_in_us);

    /* timer_expired flag will be set by the SIGALRM handler */
    while(!cpu->exit_requested) {
        kvm_run();
    }

    return OK;
}
EXC_VALUE_1(uint64_t, kvm_execute, 0, uint64_t, time)

/* Run KVM execution for single instruction. */
uint64_t kvm_execute_single_step()
{
    set_next_run_as_single_step();

    kvm_run();

    return OK;
}
EXC_VALUE_0(uint64_t, kvm_execute_single_step, 0)

void kvm_interrupt_execution()
{
    execution_timer_disarm();
    kill_cpu_thread(SIGALRM);
}
EXC_VOID_0(kvm_interrupt_execution)
