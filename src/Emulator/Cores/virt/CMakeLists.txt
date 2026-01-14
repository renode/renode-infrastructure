cmake_minimum_required(VERSION 3.12)

project (virt LANGUAGES C)

if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    set(DEBUG_DEF "-DDEBUG -DDEBUG_ON -g3")
endif()

option (PROFILING_BUILD "Build optimized for profiling" OFF)
if(PROFILING_BUILD)
    message(STATUS "Profiling enhancements are enabled")
    add_definitions (
        # Forcing not to omit frame pointer can have negative impact on performance,
        # so the end profiling result will not exactly be equal to running on live system.
        # Unfortunately without frame pointers perf might have problem with unwinding stack
        # and call traces in reports will become less readable if using frame pointers for stack unwinding.
        -fno-omit-frame-pointer
        -g3
    )
else()
    add_definitions(-fomit-frame-pointer)
endif()

# Detect whether the host is 32- or 64-bit
math (EXPR WORD_SIZE_FOUND "${CMAKE_SIZEOF_VOID_P} * 8")
set (HOST_WORD_SIZE "${WORD_SIZE_FOUND}" CACHE STRING "Host word size")
message(VERBOSE "Using HOST_WORD_SIZE: ${HOST_WORD_SIZE}")

add_definitions (
    -fPIC
    -Wall
    -Wextra
    -Wno-unused-parameter
    -Wno-sign-compare
    -Werror

    -DHOST_LONG_BITS=${HOST_WORD_SIZE}
)

include_directories (
    ../renode/include
    include
)

file (GLOB SOURCES
    "src/*.c"
    "../renode/virt/*.c"
)

add_library (virt-x86 SHARED ${SOURCES})

target_link_libraries (virt-x86
    -fPIC
    -shared
)

target_compile_definitions(virt-x86 PRIVATE
    TARGET_X86KVM
)

set_target_properties(virt-x86 PROPERTIES
    PREFIX ""
    OUTPUT_NAME "kvm-x86"
    SUFFIX ".so"
)


add_library (virt-x86_64 SHARED ${SOURCES})

target_link_libraries (virt-x86_64
    -fPIC
    -shared
)

target_compile_definitions(virt-x86_64 PRIVATE
    TARGET_X86_64KVM
)

set_target_properties(virt-x86_64 PROPERTIES
    PREFIX ""
    OUTPUT_NAME "kvm-x86_64"
    SUFFIX ".so"
)
