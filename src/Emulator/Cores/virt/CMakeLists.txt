cmake_minimum_required(VERSION 3.12)

project (virt LANGUAGES C)

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

add_definitions (
    -fPIC
    -Wall
    -Wextra
    -Wno-unused-parameter
    -Wno-sign-compare
    -Werror
)

include_directories (
    ../renode/include
    include
)

file (GLOB SOURCES
    "src/*.c"
    "../renode/virt/*.c"
)

add_library (virt SHARED ${SOURCES})

target_link_libraries (virt
    -fPIC
    -shared
)

set_target_properties(virt PROPERTIES
    PREFIX ""
    OUTPUT_NAME "kvm-i386"
    SUFFIX ".so"
)
