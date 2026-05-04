#!/bin/bash
# Creates a proper macOS .app bundle from dotnet publish output.
# Usage: ./create-macos-bundle.sh <publish_output_dir> <version> <output_dir> [arch]
# arch defaults to "x64" if not specified.
#
# Written by Derek Pascarella (ateam)

set -e

# Ensure ~/.local/bin is in PATH (non-interactive shells don't source .bashrc)
export PATH="$HOME/.local/bin:$PATH"

PUBLISH_DIR=$1
VERSION=$2
OUTPUT_DIR=$3
ARCH=${4:-x64}

if [ -z "$PUBLISH_DIR" ] || [ -z "$VERSION" ] || [ -z "$OUTPUT_DIR" ]; then
    echo "Usage: $0 <publish_output_dir> <version> <output_dir> [arch]"
    exit 1
fi

APP_NAME="UniversalDreamcastPatcher"
BUNDLE_NAME="${APP_NAME}.app"
BUNDLE_PATH="${OUTPUT_DIR}/${BUNDLE_NAME}"

echo "Creating macOS app bundle: ${BUNDLE_NAME}"
echo "Version: ${VERSION}"
echo "Architecture: ${ARCH}"

mkdir -p "${BUNDLE_PATH}/Contents/MacOS"
mkdir -p "${BUNDLE_PATH}/Contents/Resources"

echo "Copying application files..."
cp -r "${PUBLISH_DIR}"/* "${BUNDLE_PATH}/Contents/MacOS/"

echo "Creating Info.plist..."
if [ -f "src/UniversalDreamcastPatcher.App/Info.plist" ]; then
    cp "src/UniversalDreamcastPatcher.App/Info.plist" "${BUNDLE_PATH}/Contents/Info.plist"
    if [ "$(uname)" == "Darwin" ]; then
        sed -i '' "s/<string>1.0<\/string>/<string>${VERSION}<\/string>/g" "${BUNDLE_PATH}/Contents/Info.plist"
    else
        sed -i "s/<string>1.0<\/string>/<string>${VERSION}<\/string>/g" "${BUNDLE_PATH}/Contents/Info.plist"
    fi
else
    echo "Warning: Info.plist template not found at src/UniversalDreamcastPatcher.App/Info.plist"
fi

echo "Setting executable permissions..."
chmod +x "${BUNDLE_PATH}/Contents/MacOS/${APP_NAME}"
find "${BUNDLE_PATH}/Contents/MacOS" -name "*.dylib" -exec chmod +x {} \;

# Copy icon resource if available. If only a .ico or .png exists, fall back silently.
if [ -f "src/UniversalDreamcastPatcher.App/Assets/icon.icns" ]; then
    cp "src/UniversalDreamcastPatcher.App/Assets/icon.icns" "${BUNDLE_PATH}/Contents/Resources/"
    echo "Icon file copied."
else
    echo "Warning: No .icns icon file found. macOS bundle will use default icon."
fi

echo "Ad-hoc code signing the bundle..."
if command -v rcodesign &> /dev/null; then
    rcodesign sign "${BUNDLE_PATH}" 2>&1 | grep -v "non Mach-O file\|we do not know how\|if the bundle signs" || true
elif command -v codesign &> /dev/null; then
    codesign --force --deep -s - "${BUNDLE_PATH}"
else
    echo "ERROR: No code signing tool found (rcodesign or codesign)."
    echo "Apple Silicon Macs require signed binaries. Install rcodesign:"
    echo "  https://github.com/indygreg/apple-platform-rs"
    exit 1
fi

echo "macOS app bundle created at: ${BUNDLE_PATH}"

echo "Creating tar.gz archive..."
cd "${OUTPUT_DIR}"
tar -czf "${APP_NAME}.v${VERSION}-osx-${ARCH}-AppBundle.tar.gz" "${BUNDLE_NAME}"
cd - > /dev/null

# Clean up the .app directory (archive is the deliverable)
rm -rf "${BUNDLE_PATH}"

echo "Archive created: ${OUTPUT_DIR}/${APP_NAME}.v${VERSION}-osx-${ARCH}-AppBundle.tar.gz"
echo "Done!"
