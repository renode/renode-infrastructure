//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include "arch_callbacks.h"
#include "renode_imports.h"

EXTERNAL_AS(func_uint64, GetCPUTime, tlib_get_cpu_time)
EXTERNAL_AS(action_uint32_uint64, TimerMod, tlib_timer_mod)
