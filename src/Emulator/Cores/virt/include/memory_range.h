#pragma once

#include <stdint.h>

void kvm_map_range(int32_t slot, uint64_t address, uint64_t size, uint64_t pointer);

void kvm_unmap_range(int32_t slot);
