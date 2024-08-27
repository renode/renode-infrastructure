#pragma once

#define TARGET_LONG_BITS 64
#define TARGET_SHORT_ALIGNMENT 2
#define TARGET_INT_ALIGNMENT 4
#define TARGET_LONG_ALIGNMENT 4
#define TARGET_LLONG_ALIGNMENT 4
#include "../../tlib/include/cpu-defs.h"

typedef enum {
    EAX_32    = 0,
    ECX_32    = 1,
    EDX_32    = 2,
    EBX_32    = 3,
    ESP_32    = 4,
    EBP_32    = 5,
    ESI_32    = 6,
    EDI_32    = 7,
    EIP_32    = 8,
    EFLAGS_32 = 9,
    CS_32     = 10,
    SS_32     = 11,
    DS_32     = 12,
    ES_32     = 13,
    FS_32     = 14,
    GS_32     = 15,
    CR0_32    = 16,
    CR1_32    = 17,
    CR2_32    = 18,
    CR3_32    = 19,
    CR4_32    = 20,
} Registers;
