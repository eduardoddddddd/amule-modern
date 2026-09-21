#!/bin/bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet="$root/.tools/dotnet/dotnet"
test=0
publish=0
for arg in "$@"; do
  case "$arg" in
    -Test|--test) test=1 ;;
    -Publish|--publish) publish=1 ;;
    *) echo "Uso: scripts/Build.sh [-Test] [-Publish]" >&2; exit 64 ;;
  esac
done
if [[ ! -x "$dotnet" ]]; then
  echo "Ejecuta primero scripts/Setup.sh." >&2
  exit 1
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
cd "$root"
"$dotnet" restore AmuleModern.slnx --locked-mode
"$dotnet" build AmuleModern.slnx -c Release --no-restore
if [[ "$test" -eq 1 ]]; then
  "$dotnet" run --project tests/Smoke -c Release --no-build -- --integration
fi
if [[ "$publish" -eq 1 ]]; then
  "$root/scripts/Package-mac.sh"
fi
