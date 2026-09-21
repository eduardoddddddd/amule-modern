#!/bin/bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
"$root/scripts/Setup.sh"
app="$root/AmuleModern.app"
if [[ ! -x "$app/Contents/MacOS/AmuleModern" ]]; then
  "$root/scripts/Package-mac.sh"
fi
open "$app"
