#!/bin/bash
# Crea AmuleModern.app en la raíz del repo, con el icono de la aplicación, lista para el Dock.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet="$root/.tools/dotnet/dotnet"
if [[ ! -x "$dotnet" ]]; then
  echo "Ejecuta primero scripts/Setup.sh." >&2
  exit 1
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
cd "$root"
"$dotnet" restore AmuleModern.slnx --locked-mode
stage="$(mktemp -d /tmp/amule-modern-publish.XXXXXX)"
"$dotnet" publish src/Desktop -c Release -r osx-arm64 --self-contained true -o "$stage"
app="$root/AmuleModern.app"
rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$stage"/. "$app/Contents/MacOS/"
rm -rf "$stage"

iconroot="$(mktemp -d /tmp/amule-icon.XXXXXX)"
iconset="$iconroot/AppIcon.iconset"
mkdir -p "$iconset"
src_png="$root/src/Desktop/Assets/amule-modern.png"
sips -z 16 16 "$src_png" --out "$iconset/icon_16x16.png" >/dev/null
sips -z 32 32 "$src_png" --out "$iconset/icon_16x16@2x.png" >/dev/null
sips -z 32 32 "$src_png" --out "$iconset/icon_32x32.png" >/dev/null
sips -z 64 64 "$src_png" --out "$iconset/icon_32x32@2x.png" >/dev/null
sips -z 128 128 "$src_png" --out "$iconset/icon_128x128.png" >/dev/null
sips -z 256 256 "$src_png" --out "$iconset/icon_128x128@2x.png" >/dev/null
sips -z 256 256 "$src_png" --out "$iconset/icon_256x256.png" >/dev/null
sips -z 512 512 "$src_png" --out "$iconset/icon_256x256@2x.png" >/dev/null
sips -z 512 512 "$src_png" --out "$iconset/icon_512x512.png" >/dev/null
sips -z 1024 1024 "$src_png" --out "$iconset/icon_512x512@2x.png" >/dev/null
iconutil -c icns "$iconset" -o "$app/Contents/Resources/AppIcon.icns"
rm -rf "$iconroot"

cat > "$app/Contents/Info.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>aMule Modern</string>
  <key>CFBundleDisplayName</key>
  <string>aMule Modern</string>
  <key>CFBundleIdentifier</key>
  <string>org.amule-modern.desktop</string>
  <key>CFBundleVersion</key>
  <string>0.9.2</string>
  <key>CFBundleShortVersionString</key>
  <string>0.9.2</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleExecutable</key>
  <string>AmuleModern</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
</dict>
</plist>
EOF
chmod +x "$app/Contents/MacOS/AmuleModern"
codesign --force --deep --sign - "$app" >/dev/null
echo "Aplicación lista: $app"
