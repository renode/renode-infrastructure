//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include "arch_callbacks.h"
#include "renode_imports.h"

EXTERNAL_AS(int32_t, FindBestInterrupt, tlib_find_best_interrupt)
EXTERNAL_AS(void, AcknowledgeInterrupt, tlib_acknowledge_interrupt, int32_t)
EXTERNAL_AS(void, OnCpuHalted, tlib_on_cpu_halted)
EXTERNAL_AS(void, OnCpuPowerDown, tlib_on_cpu_power_down)
