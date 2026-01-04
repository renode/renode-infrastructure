#pragma once

#include <stdint.h>
#include <stdbool.h>

typedef enum {
    INVALID_ACCESS_64BIT_ADDRESS,
    INVALID_ACCESS_64BIT_WIDTH
} InvalidAccess;

void handle_64bit_access(InvalidAccess invalid_access, unsigned access_len, bool is_write, uint64_t addr);

void handle_64bit_register_value(int reg_number, uint64_t value);
