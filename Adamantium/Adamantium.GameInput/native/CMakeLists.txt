cmake_minimum_required(VERSION 3.20)
project(gameinput_c_shared CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# GameInput location: the native/ folder of the Microsoft.GameInput NuGet package (build.ps1 fetches it).
if(NOT GAMEINPUT_DIR)
    message(FATAL_ERROR "GAMEINPUT_DIR is not set: pass the native/ folder of the Microsoft.GameInput package.")
endif()

find_path(GAMEINPUT_INCLUDE_DIR GameInput.h PATHS "${GAMEINPUT_DIR}/include" NO_DEFAULT_PATH)
find_library(GAMEINPUT_LIBRARY NAMES GameInput PATHS "${GAMEINPUT_DIR}/lib/x64" NO_DEFAULT_PATH)
if(NOT GAMEINPUT_INCLUDE_DIR OR NOT GAMEINPUT_LIBRARY)
    message(FATAL_ERROR "Could not find GameInput.h / GameInput.lib under '${GAMEINPUT_DIR}'.")
endif()
message(STATUS "GameInput headers: ${GAMEINPUT_INCLUDE_DIR}")
message(STATUS "GameInput library: ${GAMEINPUT_LIBRARY}")

# gameinput-c-shared.dll, matching the *-c-shared naming of the other shims.
add_library(gameinput-c-shared SHARED gameinput_c.cpp gameinput_c.h)
set_target_properties(gameinput-c-shared PROPERTIES OUTPUT_NAME "gameinput-c-shared")
target_include_directories(gameinput-c-shared PRIVATE "${GAMEINPUT_INCLUDE_DIR}")
target_link_libraries(gameinput-c-shared PRIVATE "${GAMEINPUT_LIBRARY}")
