#pragma once

#include <stdbool.h>
#include <stdint.h>
#include <sys/queue.h>

#define TRAP_OPCODE 0xCC

typedef struct Breakpoint {
    uint64_t pc;
    uint8_t code_byte;  //  stores value overshadowed by TRAP_OPCODE
    uint8_t *host_code_position;
    LIST_ENTRY(Breakpoint) list;
} Breakpoint;

bool is_breakpoint_address(uint64_t address);

/* Translates guest's virtual address into guest physical address.
 * On failiure returns UINT64_MAX. */
uint64_t kvm_translate_guest_virtual_address(uint64_t address);
