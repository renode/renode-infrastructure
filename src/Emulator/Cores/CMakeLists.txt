cmake_minimum_required(VERSION 3.12)
include (ExternalProject)

project (renode-cores)

# Contains definitions that are used both here and in tlib
include (tlib/common.cmake)

add_definitions (
    -DTLIB_LIB_TYPE=OBJECT
)
add_subdirectory(tlib)

add_definitions (
    -fPIC
    -Wall
    -Wextra
    -Wno-unused-parameter
    -Wno-sign-compare 

    -DHOST_BITS_${HOST_WORD_SIZE}
    -DHOST_${HOST_ARCH_U}=1
    -DHOST_LONG_BITS=${HOST_WORD_SIZE}

    -DTARGET_SHORT_ALIGNMENT=2
    -DTARGET_INT_ALIGNMENT=4
    -DTARGET_LONG_ALIGNMENT=4
    -DTARGET_LLONG_ALIGNMENT=4

    -DTARGET_LONG_BITS=${TARGET_WORD_SIZE}
    -DTARGET_INSN_START_EXTRA_WORDS=0
)

file (GLOB SOURCES
    "renode/*.c"
    "renode/arch/${TARGET_ACTUAL_ARCH}/*.c"
)
# several tlib and tcg headers are needed by Cores/renode
include_directories (
    tlib/arch/${TARGET_ACTUAL_ARCH}
    tlib/arch/arm_common
    tlib/tcg/${HOST_ARCH}
    tlib/include
    tlib/softfloat-2
    tlib/softfloat-3/source/include
    tlib/tcg
    renode/include
)
add_library (renode-core SHARED ${SOURCES} $<TARGET_OBJECTS:tlib> $<TARGET_OBJECTS:tcg> $<TARGET_OBJECTS:softfloat-3>)

set (ENDIAN_STR "le")
if (TARGET_BIG_ENDIAN)
    set (ENDIAN_STR "be")
endif()

set_target_properties(renode-core PROPERTIES 
    PREFIX ""
    OUTPUT_NAME "translate-${TARGET_ARCH}-${ENDIAN_STR}"
    SUFFIX ".so"
)

if("${TARGET_ACTUAL_ARCH}" STREQUAL "i386")
    set (MATH_LIB_LINK_ARG "-lm" CACHE STRING
      "Argument pointing linker to a math functions library. It's required to translate i386 code.")
endif()

# On x86_64 Linux, the memcpy function was modified in GNU libc v2.14.
# It'd be impossible to run tlib without this wrapping with older libc.
if(${CMAKE_HOST_SYSTEM_NAME} STREQUAL Linux AND ${HOST_ARCH} STREQUAL i386 AND ${HOST_WORD_SIZE} EQUAL 64)
    set(WRAP_MEMCPY_OPT -Wl,--wrap=memcpy)
endif()

add_dependencies (renode-core tlib)

target_link_libraries (renode-core
    ${WRAP_MEMCPY_OPT}
    ${MATH_LIB_LINK_ARG}
    pthread
    tlib
    tcg
    softfloat-3
    -fPIC
)
