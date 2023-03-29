//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include "arch_callbacks.h"
#include "renode_imports.h"

EXTERNAL_AS(void, DoSemihosting, tlib_do_semihosting)
EXTERNAL_AS(uint64_t, GetCPUTime, tlib_get_cpu_time)
EXTERNAL_AS(void, TimerMod, tlib_timer_mod, uint32_t, uint64_t)
