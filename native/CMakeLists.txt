cmake_minimum_required(VERSION 3.20)
project(tpm_ms_native CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(tpm_ms SHARED src/main.cpp)
target_compile_options(tpm_ms PRIVATE -Wall -Wextra)

find_library(DOBBY_LIB dobby PATHS ${CMAKE_SOURCE_DIR}/lib NO_DEFAULT_PATH)
if(NOT DOBBY_LIB)
    set(DOBBY_LIB ${CMAKE_SOURCE_DIR}/lib/libdobby.dylib)
endif()
target_link_libraries(tpm_ms PRIVATE ${DOBBY_LIB})
set_target_properties(tpm_ms PROPERTIES
    BUILD_RPATH "@loader_path"
    INSTALL_RPATH "@loader_path")
