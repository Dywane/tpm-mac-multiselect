cmake_minimum_required(VERSION 3.20)
project(tpm_ms_native CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(tpm_ms SHARED src/main.cpp)
target_compile_options(tpm_ms PRIVATE -Wall -Wextra)

# Dobby is statically linked (built from jmpews/Dobby, Apache-2.0) so the release ships one dylib.
set(DOBBY_STATIC ${CMAKE_SOURCE_DIR}/lib/libdobby.a)
if(NOT EXISTS ${DOBBY_STATIC})
    message(FATAL_ERROR "lib/libdobby.a missing — run ./build-dobby.sh first")
endif()
target_link_libraries(tpm_ms PRIVATE ${DOBBY_STATIC} "-framework Carbon")
set_target_properties(tpm_ms PROPERTIES
    BUILD_RPATH "@loader_path"
    INSTALL_RPATH "@loader_path")
