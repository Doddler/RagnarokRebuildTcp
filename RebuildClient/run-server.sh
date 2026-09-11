#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SERVER_PROJECT="$SCRIPT_DIR/../RoRebuildServer/RoRebuildServer/RoRebuildServer.csproj"
SERVER_WORKING_DIR="$SCRIPT_DIR/../RoRebuildServer/RoRebuildServer"
PROFILE="${1:-RoRebuildServer}"

echo "Launch Profile: $PROFILE"
echo ".NET SDK in use:"
dotnet --version
echo "------------------------------------"

(
    cd "$SERVER_WORKING_DIR"
    dotnet run \
        --project "$SERVER_PROJECT" \
        --launch-profile "$PROFILE"
)

echo "------------------------------------"
