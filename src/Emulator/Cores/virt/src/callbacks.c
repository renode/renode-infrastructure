/*
 * Copyright (c) 2010-2025 Antmicro
 *
 * This file is licensed under the MIT License.
 */

#include <stdint.h>

#include "callbacks.h"

DEFAULT_VOID_HANDLER2(void kvm_log, int log_level, char* message)

DEFAULT_VOID_HANDLER1(void kvm_abort, char* message)

DEFAULT_INT_HANDLER1(uint64_t kvm_io_port_read_byte, uint64_t address)

DEFAULT_INT_HANDLER1(uint64_t kvm_io_port_read_word, uint64_t address)

DEFAULT_INT_HANDLER1(uint64_t kvm_io_port_read_double_word, uint64_t address)

DEFAULT_INT_HANDLER1(uint64_t kvm_read_quad_word, uint64_t address)

DEFAULT_VOID_HANDLER2(void kvm_io_port_write_byte, uint64_t address, uint64_t value)

DEFAULT_VOID_HANDLER2(void kvm_io_port_write_word, uint64_t address, uint64_t value)

DEFAULT_VOID_HANDLER2(void kvm_io_port_write_double_word, uint64_t address,
                      uint64_t value)

DEFAULT_VOID_HANDLER2(void kvm_write_quad_word, uint64_t address,
                      uint64_t value)

DEFAULT_INT_HANDLER1(uint64_t kvm_sysbus_read_byte, uint64_t address)

DEFAULT_INT_HANDLER1(uint64_t kvm_sysbus_read_word, uint64_t address)

DEFAULT_INT_HANDLER1(uint64_t kvm_sysbus_read_double_word, uint64_t address)

DEFAULT_INT_HANDLER1(uint64_t kvm_sysbus_read_quad_word, uint64_t address)

DEFAULT_VOID_HANDLER2(void kvm_sysbus_write_byte, uint64_t address, uint64_t value)

DEFAULT_VOID_HANDLER2(void kvm_sysbus_write_word, uint64_t address, uint64_t value)

DEFAULT_VOID_HANDLER2(void kvm_sysbus_write_double_word, uint64_t address,
                      uint64_t value)

DEFAULT_VOID_HANDLER2(void kvm_sysbus_write_quad_word, uint64_t address,
                      uint64_t value)
