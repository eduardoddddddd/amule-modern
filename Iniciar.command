#!/bin/bash
cd "$(dirname "$0")"
./scripts/Setup.sh
./scripts/Build.sh
exec ./scripts/Start.sh
