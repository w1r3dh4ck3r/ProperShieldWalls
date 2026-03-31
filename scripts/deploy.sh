#!/usr/bin/env bash
# Deploy ProperShieldWalls to the Bannerlord Modules folder (WSL → Windows)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
MOD_NAME="ProperShieldWalls"
GAME_MODULES="/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules"
TARGET_DIR="$GAME_MODULES/$MOD_NAME"
DLL_DIR="$TARGET_DIR/bin/Win64_Shipping_Client"

echo "=== Deploying $MOD_NAME ==="

# Create module folder structure
mkdir -p "$DLL_DIR"

# Copy SubModule.xml
cp "$PROJECT_DIR/SubModule.xml" "$TARGET_DIR/"
echo "  SubModule.xml -> $TARGET_DIR/"

# Find and copy the built DLL (check x64/Release first, then other output paths)
DLL=""
for candidate in \
    "$PROJECT_DIR/bin/x64/Release/$MOD_NAME.dll" \
    "$PROJECT_DIR/bin/x64/Debug/$MOD_NAME.dll" \
    "$PROJECT_DIR/bin/Release/$MOD_NAME.dll" \
    "$PROJECT_DIR/bin/Debug/$MOD_NAME.dll"; do
    if [ -f "$candidate" ]; then
        DLL="$candidate"
        break
    fi
done

if [ -z "$DLL" ]; then
    echo "ERROR: No built DLL found. Run the build first."
    exit 1
fi

cp "$DLL" "$DLL_DIR/"
echo "  $(basename "$DLL") -> $DLL_DIR/"

echo "=== Deployed $MOD_NAME ==="
