cmake_minimum_required(VERSION 3.10)
project(amd_bridge LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# swap /MD for /MT (static MSVC runtime — no VCRUNTIME140.dll needed)
string(REPLACE "/MD" "/MT" CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE}")
string(REPLACE "/MDd" "/MTd" CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG}")

add_executable(amd_bridge main.cpp ADLX/ADLXHelper.cpp ADLX/WinAPIs.cpp)
target_include_directories(amd_bridge PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/ADLX/include")