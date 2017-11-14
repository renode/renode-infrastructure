//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under MIT License.
// Full license text is available in 'licenses/MIT.txt' file.
//

#include "arch_callbacks.h"
#include "renode_imports.h"

EXTERNAL_AS(func_int32, FindBestInterrupt, tlib_find_best_interrupt)
EXTERNAL_AS(action_int32, AcknowledgeInterrupt, tlib_acknowledge_interrupt)
EXTERNAL_AS(action, OnCpuHalted, tlib_on_cpu_halted)
EXTERNAL_AS(action, OnCpuPowerDown, tlib_on_cpu_power_down)
