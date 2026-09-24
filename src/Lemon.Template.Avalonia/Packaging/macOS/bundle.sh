#!/usr/bin/env bash
# Publishes the app and wraps it in a macOS .app bundle.
#
#   bash Packaging/macOS/bundle.sh            # Apple Silicon (osx-arm64)
#   bash Packaging/macOS/bundle.sh osx-x64    # Intel
#
# Run on macOS: the icon is generated with sips and iconutil, which ship with the OS. The bundle is signed
# ad hoc so it launches locally; distributing it to other Macs needs a Developer ID signature and
# notarization, which this script does not do.
set -euo pipefail

RID="${1:-osx-arm64}"
CONFIGURATION="Release"
APP_NAME="Lemon.Template.Avalonia"

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$HERE/../.." && pwd)"
OUT_DIR="$PROJECT_DIR/bin/$CONFIGURATION/bundle/$RID"
APP="$OUT_DIR/$APP_NAME.app"

dotnet publish "$PROJECT_DIR/$APP_NAME.csproj" -c "$CONFIGURATION" -r "$RID" --self-contained -o "$OUT_DIR/publish"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$OUT_DIR/publish/." "$APP/Contents/MacOS/"
cp "$HERE/Info.plist" "$APP/Contents/Info.plist"

# AppIcon.icns from the app logo. The source is 256 px, so larger slots are left out rather than upscaled.
ICONSET="$OUT_DIR/AppIcon.iconset"
rm -rf "$ICONSET"
mkdir -p "$ICONSET"
LOGO="$PROJECT_DIR/Assets/Images/logo.png"
for size in 16 32 128 256; do
    sips -z "$size" "$size" "$LOGO" --out "$ICONSET/icon_${size}x${size}.png" > /dev/null
done
for size in 16 32 128; do
    double=$((size * 2))
    sips -z "$double" "$double" "$LOGO" --out "$ICONSET/icon_${size}x${size}@2x.png" > /dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"
rm -rf "$ICONSET"

chmod +x "$APP/Contents/MacOS/$APP_NAME"
codesign --force --deep --sign - "$APP"

echo "Created $APP"
