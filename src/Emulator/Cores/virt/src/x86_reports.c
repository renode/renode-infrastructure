#ifdef TARGET_X86KVM

#include "x86_reports.h"
#include "utils.h"
#include "cpu.h"
#include "cpu_registers.h"
#include "callbacks.h"


// The return format string is expected to take following arguments:
// <access_length : string> <access_type : string> <address : uint64_t>
static const char* get_access_message(InvalidAccess invalid_access)
{
    switch(invalid_access) {
    case INVALID_ACCESS_64BIT_ADDRESS:
        return "Sysbus %s %s made with 64 bit address 0x%lx in 32 bit mode";
    case INVALID_ACCESS_64BIT_WIDTH:
        return "Sysbus %s %s on address 0x%lx in 32 bit mode";
    default:
        kvm_logf(LOG_LEVEL_ERROR, "Unknown invalid_access specifier selected");
        return "UNKNOWN invalid %s %s access on address 0x%lx";
    }
}

static const char* get_access_length_text(unsigned int len)
{
    switch(len) {
    case 1:
        return "Byte";
    case 2:
        return "Word";
    case 4:
        return "DoubleWord";
    case 8:
        return "QuadWord";
    default:
        return "ErroneusLength";
    }
}

static const char* get_access_type_text(bool is_write)
{
    if (is_write) {
        return "Write";
    } else {
        return "Read";
    }
}

void handle_64bit_access(InvalidAccess invalid_access, unsigned int access_len, bool is_write, uint64_t addr)
{
    switch (cpu->on64BitDetected)
    {
    case FAULT:
        kvm_runtime_abortf(get_access_message(invalid_access), get_access_length_text(access_len), get_access_type_text(is_write), addr);
        break;
    case WARN:
        kvm_logf(LOG_LEVEL_WARNING, get_access_message(invalid_access), get_access_length_text(access_len), get_access_type_text(is_write), addr);
        break;
    default:
        break;
    }
}

static const char* get_register_name(int reg_number)
{
#define REG_NAME_CASE_STR(name, name_str) case name: return name_str;
#define REG_NAME_CASE(name) REG_NAME_CASE_STR(name, #name)

    switch (reg_number)
    {
    case RAX:
        return "EAX";
    case RDX:
        return "EDX";
    case RBX:
        return "EBX";
    case RBP:
        return "EBP";
    case RSI:
        return "ESI";
    case RDI:
        return "EDI";
    case RIP:
        return "EIP";
    REG_NAME_CASE(EFLAGS)
    REG_NAME_CASE(CS)
    REG_NAME_CASE(SS)
    REG_NAME_CASE(DS)
    REG_NAME_CASE(ES)
    REG_NAME_CASE(FS)
    REG_NAME_CASE(GS)
    REG_NAME_CASE(CR0)
    REG_NAME_CASE(CR1)
    REG_NAME_CASE(CR2)
    REG_NAME_CASE(CR3)
    REG_NAME_CASE(CR4)
    default:
        return "UNKNOWN";
    }
#undef REG_NAME_CASE
}

void handle_64bit_register_value(int reg_number, uint64_t value)
{
    switch (cpu->on64BitDetected)
    {
    case FAULT:
        kvm_runtime_abortf("Register %s holds 64bit value 0x%lx in 32 bit mode", get_register_name(reg_number), value);
        break;
    case WARN:
        kvm_logf(LOG_LEVEL_WARNING, "Register %s holds 64bit value 0x%lx in 32 bit mode", get_register_name(reg_number), value);
        break;
    default:
        break;
    }
}

#endif
