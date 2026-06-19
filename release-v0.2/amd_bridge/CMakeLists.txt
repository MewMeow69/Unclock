cmake_minimum_required(VERSION 3.10)
project(amd_bridge LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(NOT DEFINED ADLX_SDK_DIR)
    set(ADLX_SDK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../ADLX")
endif()

add_executable(amd_bridge main.cpp)
target_include_directories(amd_bridge PRIVATE "${ADLX_SDK_DIR}/include")

if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    target_link_directories(amd_bridge PRIVATE "${ADLX_SDK_DIR}/lib/win64")
    target_link_libraries(amd_bridge PRIVATE ADLX)
else()
    target_link_directories(amd_bridge PRIVATE "${ADLX_SDK_DIR}/lib/win32")
    target_link_libraries(amd_bridge PRIVATE ADLX)
endif()
