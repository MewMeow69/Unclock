cmake_minimum_required(VERSION 3.10)
project(amd_bridge LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable(amd_bridge main.cpp ADLX/ADLXHelper.cpp)
target_include_directories(amd_bridge PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/ADLX/include")
