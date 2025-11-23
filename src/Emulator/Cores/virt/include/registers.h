#pragma once

#include <linux/kvm.h>
#include <stdint.h>

#ifdef TARGET_X86_64KVM
typedef uint64_t reg_t;
#else
typedef uint32_t reg_t;
#endif

#include "cpu_registers.h"

void kvm_registers_synchronize();
void kvm_registers_invalidate();

reg_t get_register_value(Registers reg_number);
void set_register_value(Registers reg_number, reg_t value);
