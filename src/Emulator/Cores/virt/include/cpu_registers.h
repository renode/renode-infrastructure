#pragma once


typedef enum {
#ifdef TARGET_X86_64KVM
    RAX_64    = 0,
    RCX_64    = 2,
    RDX_64    = 3,
    RBX_64    = 1,
    RSP_64    = 4,
    RBP_64    = 5,
    RSI_64    = 6,
    RDI_64    = 7,
    RIP_64    = 8,
    EFLAGS_64 = 9,
    CS_64     = 10,
    SS_64     = 11,
    DS_64     = 12,
    ES_64     = 13,
    FS_64     = 14,
    GS_64     = 15,
    CR0_64    = 16,
    CR1_64    = 17,
    CR2_64    = 18,
    CR3_64    = 19,
    CR4_64    = 20,
    CR8_64    = 24,
    EFER_64   = 25,
#else
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
#endif
} Registers;


#ifdef TARGET_X86_64KVM
typedef uint64_t reg_t;
#define RAX RAX_64
#define RCX RCX_64
#define RDX RDX_64
#define RBX RBX_64
#define RSP RSP_64
#define RBP RBP_64
#define RSI RSI_64
#define RDI RDI_64
#define RIP RIP_64
#define EFLAGS EFLAGS_64
#define CS CS_64
#define SS SS_64
#define DS DS_64
#define ES ES_64
#define FS FS_64
#define GS GS_64
#define CR0 CR0_64
#define CR1 CR1_64
#define CR2 CR2_64
#define CR3 CR3_64
#define CR4 CR4_64
#define CR8 CR8_64
#define EFER EFER_64
#else
typedef uint32_t reg_t;
#define RAX EAX_32
#define RCX ECX_32
#define RDX EDX_32
#define RBX EBX_32
#define RSP ESP_32
#define RBP EBP_32
#define RSI ESI_32
#define RDI EDI_32
#define RIP EIP_32
#define EFLAGS EFLAGS_32
#define CS CS_32
#define SS SS_32
#define DS DS_32
#define ES ES_32
#define FS FS_32
#define GS GS_32
#define CR0 CR0_32
#define CR1 CR1_32
#define CR2 CR2_32
#define CR3 CR3_32
#define CR4 CR4_32
#endif
