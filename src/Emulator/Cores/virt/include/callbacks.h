#pragma once

#include <stdint.h>

#define DEFAULT_VOID_HANDLER1(NAME, PARAM1)                                    \
    NAME(PARAM1) __attribute__((weak));                                        \
                                                                               \
    NAME(PARAM1)                                                               \
    {                                                                          \
    }

#define DEFAULT_VOID_HANDLER2(NAME, PARAM1, PARAM2)                            \
    NAME(PARAM1, PARAM2) __attribute__((weak));                                \
                                                                               \
    NAME(PARAM1, PARAM2)                                                       \
    {                                                                          \
    }

#define DEFAULT_INT_HANDLER1(NAME, PARAM1)                                     \
    NAME(PARAM1) __attribute__((weak));                                        \
                                                                               \
    NAME(PARAM1)                                                               \
    {                                                                          \
        return 0;                                                              \
    }

void kvm_log(int level, char *message);
void kvm_abort(char *message);

uint64_t kvm_io_port_read_byte(uint64_t address);
uint64_t kvm_io_port_read_word(uint64_t address);
uint64_t kvm_io_port_read_double_word(uint64_t address);

void kvm_io_port_write_byte(uint64_t address, uint64_t value);
void kvm_io_port_write_word(uint64_t address, uint64_t value);
void kvm_io_port_write_double_word(uint64_t address, uint64_t value);

uint64_t kvm_sysbus_read_byte(uint64_t address);
uint64_t kvm_sysbus_read_word(uint64_t address);
uint64_t kvm_sysbus_read_double_word(uint64_t address);
uint64_t kvm_sysbus_read_quad_word(uint64_t address);

void kvm_sysbus_write_byte(uint64_t address, uint64_t value);
void kvm_sysbus_write_word(uint64_t address, uint64_t value);
void kvm_sysbus_write_double_word(uint64_t address, uint64_t value);
void kvm_sysbus_write_quad_word(uint64_t address, uint64_t value);
