#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SERVER_DIR="$SCRIPT_DIR/RoRebuildServer"
CLIENT_DATA_DIR="$SCRIPT_DIR/RebuildClient/Assets/Data"

dotnet build "$SERVER_DIR/GameConfig/GameConfig.csproj" \
    -c Release \
    --property WarningLevel=0

dotnet build "$SERVER_DIR/RebuildSharedData/RebuildSharedData.csproj" \
    -c Release \
    --property WarningLevel=0

mkdir -p "$CLIENT_DATA_DIR"
cp -f "$SERVER_DIR/GameConfig/bin/Release/netstandard2.0/GameConfig.dll" \
    "$CLIENT_DATA_DIR/GameConfig.dll"
cp -f "$SERVER_DIR/RebuildSharedData/bin/Release/netstandard2.1/RebuildSharedData.dll" \
    "$CLIENT_DATA_DIR/RebuildSharedData.dll"

UTILITY_PROJECT="$SERVER_DIR/DataToClientUtility/DataToClientUtility.csproj"
UTILITY_DLL="$SERVER_DIR/DataToClientUtility/bin/Release/net9.0/DataToClientUtility.dll"

dotnet build "$UTILITY_PROJECT" \
    -c Release \
    --property WarningLevel=0
dotnet "$UTILITY_DLL"
