#!/bin/bash
# Prepara el SDK .NET fijado y el amuled 3.0.1 oficial de macOS dentro del repositorio.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
tools="$root/.tools"
dotnet="$tools/dotnet/dotnet"
manifest="$root/docs/engine-manifest.json"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

sdk="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["sdk"]["version"])' "$root/global.json")"
if [[ ! -x "$dotnet" ]] || [[ "$("$dotnet" --version | tr -d '[:space:]')" != "$sdk" ]]; then
  mkdir -p "$tools"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$tools/dotnet-install.sh"
  bash "$tools/dotnet-install.sh" --version "$sdk" --install-dir "$tools/dotnet" --no-path
fi
actual="$("$dotnet" --version | tr -d '[:space:]')"
if [[ "$actual" != "$sdk" ]]; then
  echo "SDK inesperado: $actual" >&2
  exit 1
fi

read -r version url sha < <(python3 -c 'import json,sys; m=json.load(open(sys.argv[1])); print(m["version"], m["macosUrl"], m["macosSha256"])' "$manifest")
vendor="$root/vendor"
archive="$vendor/aMule-${version}-macOS-universal2.dmg"
engine="$vendor/amule-${version}/macos/aMule.app/Contents/MacOS/amuled"
mkdir -p "$vendor"
if [[ ! -f "$archive" ]]; then
  curl -fL --retry 3 -o "$archive" "$url"
fi
actual_sha="$(shasum -a 256 "$archive" | awk '{print $1}')"
if [[ "$actual_sha" != "$sha" ]]; then
  echo "El paquete de aMule para macOS no coincide con el hash fijado." >&2
  exit 1
fi
if [[ ! -x "$engine" ]]; then
  mount="$(mktemp -d /tmp/amule-dmg.XXXXXX)"
  hdiutil attach "$archive" -nobrowse -readonly -mountpoint "$mount" >/dev/null
  dest="$vendor/amule-${version}/macos"
  rm -rf "$dest"
  mkdir -p "$dest"
  cp -R "$mount/aMule.app" "$dest/"
  xattr -cr "$dest/aMule.app" || true
  hdiutil detach "$mount" >/dev/null
  rmdir "$mount"
fi
if [[ ! -x "$engine" ]]; then
  echo "El DMG no dejó amuled dentro de aMule.app." >&2
  exit 1
fi
# amuled vive dentro de aMule.app y macOS lo muestra en el Dock con el icono de aMule.
# Esta copia es solo el motor: sin icono y con otro identificador, para no activar un aMule ya instalado.
app="$vendor/amule-${version}/macos/aMule.app"
plist="$app/Contents/Info.plist"
bundle_id="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$plist" 2>/dev/null || true)"
if [[ "$bundle_id" != "org.amule-modern.engine" ]]; then
  /usr/libexec/PlistBuddy -c 'Set :LSUIElement true' "$plist" 2>/dev/null \
    || /usr/libexec/PlistBuddy -c 'Add :LSUIElement bool true' "$plist"
  /usr/libexec/PlistBuddy -c 'Set :CFBundleIdentifier org.amule-modern.engine' "$plist"
  /usr/libexec/PlistBuddy -c 'Set :CFBundleName AmuleModernEngine' "$plist"
  codesign --force --deep --sign - "$app" >/dev/null
fi
echo "SDK $actual y aMule $version (macOS) preparados dentro del repositorio."
