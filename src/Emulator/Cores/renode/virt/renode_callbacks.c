//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//
#include <stdint.h>
#include "renode_imports.h"

EXTERNAL_AS(void, LogAsCpu, kvm_log, int32_t, charptr)
EXTERNAL_AS(void, ReportAbort, kvm_abort, charptr)

EXTERNAL_AS(uint32_t, ReadByteFromPort, kvm_io_port_read_byte, uint32_t)
EXTERNAL_AS(uint32_t, ReadWordFromPort, kvm_io_port_read_word, uint32_t)
EXTERNAL_AS(uint32_t, ReadDoubleWordFromPort, kvm_io_port_read_double_word, uint32_t)

EXTERNAL_AS(void, WriteByteToPort, kvm_io_port_write_byte, uint32_t, uint32_t)
EXTERNAL_AS(void, WriteWordToPort, kvm_io_port_write_word, uint32_t, uint32_t)
EXTERNAL_AS(void, WriteDoubleWordToPort,
            kvm_io_port_write_double_word, uint32_t, uint32_t)

EXTERNAL_AS(uint64_t, ReadByteFromBus, kvm_sysbus_read_byte, uint64_t)
EXTERNAL_AS(uint64_t, ReadWordFromBus, kvm_sysbus_read_word, uint64_t)
EXTERNAL_AS(uint64_t, ReadDoubleWordFromBus, kvm_sysbus_read_double_word, uint64_t)
EXTERNAL_AS(uint64_t, ReadQuadWordFromBus, kvm_sysbus_read_quad_word, uint64_t)

EXTERNAL_AS(void, WriteByteToBus, kvm_sysbus_write_byte, uint64_t, uint64_t)
EXTERNAL_AS(void, WriteWordToBus, kvm_sysbus_write_word, uint64_t, uint64_t)
EXTERNAL_AS(void, WriteDoubleWordToBus,
            kvm_sysbus_write_double_word, uint64_t, uint64_t)
EXTERNAL_AS(void, WriteQuadWordToBus, kvm_sysbus_write_quad_word, uint64_t, uint64_t)
