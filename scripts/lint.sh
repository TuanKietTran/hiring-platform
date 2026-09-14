#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

python3 scripts/format-csharp-types.py --check src tests
dotnet format HiringPlatform.slnx --verify-no-changes --no-restore

cd src/hiring-web
npm run lint
