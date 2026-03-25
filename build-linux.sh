#!/bin/bash

# Configuration: Set the path to your Cities: Skylines installation directory.
REAL_GAME_DIR="${CITIES_SKYLINES_DIR:-/var/mnt/schijven/1TB SSD/Games/Cities - Skylines/drive_c/Program Files (x86)/Cities.Skylines.v1.21.1.F5}"

# Check if the game directory exists
if [ ! -d "$REAL_GAME_DIR" ]; then
    echo "Warning: Game directory not found at '$REAL_GAME_DIR'"
    echo "Please set the CITIES_SKYLINES_DIR environment variable to your game's installation path."
fi

MANAGED_DIR="$REAL_GAME_DIR/Cities_Data/Managed"
MODS_DIR="$REAL_GAME_DIR/Files/Mods"

echo "=== Locating Dependencies ==="

# Using specific known paths to speed up build on large directories
HARMONY_CORE_DIR="$MODS_DIR/Harmony 2.2.2-0"
HARMONY_API_DIR="$MODS_DIR/81 Tiles 2 1.0.5" # 81 Tiles usually bundle it, or check Harmony folder
MOVEIT_DIR="$MODS_DIR/Move It 2.10.9"
CSM_DIR="$MODS_DIR/CSM 2603.307"

# Resolve DLLs
HARMONY_DLL="$HARMONY_CORE_DIR/CitiesHarmony.Harmony.dll"
HARMONY_API_DLL="$HARMONY_API_DIR/CitiesHarmony.API.dll"
MOVEIT_DLL="$MOVEIT_DIR/MoveIt.dll"
CSM_API_DLL="$CSM_DIR/CSM.API.dll"
CSM_BASEGAME_DLL="$CSM_DIR/CSM.BaseGame.dll"
PROTOBUF_DLL="$CSM_DIR/protobuf-net.dll"

# Fallbacks if specific folders changed
if [ ! -f "$HARMONY_API_DLL" ]; then HARMONY_API_DLL=$(find "$MODS_DIR" -maxdepth 2 -name "CitiesHarmony.API.dll" | head -n 1); fi
if [ ! -f "$HARMONY_DLL" ]; then HARMONY_DLL=$(find "$MODS_DIR" -maxdepth 2 -name "CitiesHarmony.Harmony.dll" | head -n 1); fi
if [ ! -f "$MOVEIT_DLL" ]; then MOVEIT_DLL=$(find "$MODS_DIR" -maxdepth 2 -name "MoveIt.dll" | head -n 1); fi
if [ ! -f "$CSM_API_DLL" ]; then CSM_API_DLL=$(find "$MODS_DIR" -maxdepth 2 -name "CSM.API.dll" | head -n 1); fi
if [ ! -f "$CSM_BASEGAME_DLL" ]; then CSM_BASEGAME_DLL=$(find "$MODS_DIR" -maxdepth 2 -name "CSM.BaseGame.dll" | head -n 1); fi
if [ ! -f "$PROTOBUF_DLL" ]; then PROTOBUF_DLL=$(find "$MODS_DIR" -maxdepth 2 -name "protobuf-net.dll" | head -n 1); fi

echo "Using Harmony Core: $HARMONY_DLL"
echo "Using Harmony API:  $HARMONY_API_DLL"
echo "Using Move It:      $MOVEIT_DLL"
echo "Using CSM API:      $CSM_API_DLL"
echo "Using CSM BaseGame: $CSM_BASEGAME_DLL"
echo "Using ProtoBuf:     $PROTOBUF_DLL"

# Check if critical dependencies are missing
MISSING=0
if [ ! -f "$HARMONY_DLL" ] || [ ! -f "$HARMONY_API_DLL" ]; then echo "Error: Harmony components missing!"; MISSING=1; fi
if [ ! -f "$MOVEIT_DLL" ]; then echo "Error: MoveIt.dll not found!"; MISSING=1; fi
if [ ! -f "$CSM_API_DLL" ]; then echo "Error: CSM.API.dll not found!"; MISSING=1; fi

if [ $MISSING -eq 1 ]; then
    echo "=== Build Aborted due to missing dependencies ==="
    exit 1
fi

echo "=== Building Move It - CSM Synchronization Bridge ==="

# Build the bridge mod
dotnet build "src/CSM.MoveItSync/CSM.MoveItSync.csproj" -c Release \
    /p:ManagedDir="$MANAGED_DIR" \
    /p:HarmonyDll="$HARMONY_DLL" \
    /p:HarmonyApiDll="$HARMONY_API_DLL" \
    /p:MoveItDll="$MOVEIT_DLL" \
    /p:CsmApiDll="$CSM_API_DLL" \
    /p:CsmBaseGameDll="$CSM_BASEGAME_DLL" \
    /p:ProtoBufDll="$PROTOBUF_DLL"

if [ $? -eq 0 ]; then
    echo "=== Build Complete ==="
    echo "Mod DLL: src/CSM.MoveItSync/bin/Release/net35/CSM.MoveItSync.dll"
else
    echo "=== Build Failed ==="
    exit 1
fi
