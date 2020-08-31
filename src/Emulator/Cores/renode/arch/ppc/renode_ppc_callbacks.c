//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include "arch_callbacks.h"
#include "renode_imports.h"

EXTERNAL_AS(func_uint32, ReadTbl, tlib_read_tbl)
EXTERNAL_AS(func_uint32, ReadTbu, tlib_read_tbu)
EXTERNAL_AS(func_uint64, ReadDecrementer, tlib_read_decrementer)
EXTERNAL_AS(action_uint64, WriteDecrementer, tlib_write_decrementer)
EXTERNAL_AS(func_uint32, IsVleEnabled, tlib_is_vle_enabled)
