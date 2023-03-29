//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include "arch_callbacks.h"
#include "renode_imports.h"

EXTERNAL_AS(uint32_t, ReadTbl, tlib_read_tbl)
EXTERNAL_AS(uint32_t, ReadTbu, tlib_read_tbu)
EXTERNAL_AS(uint64_t, ReadDecrementer, tlib_read_decrementer)
EXTERNAL_AS(void, WriteDecrementer, tlib_write_decrementer, uint64_t)
EXTERNAL_AS(uint32_t, IsVleEnabled, tlib_is_vle_enabled)
