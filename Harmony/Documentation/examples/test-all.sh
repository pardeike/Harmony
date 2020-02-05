#!/bin/sh
set -eu
cd "$(dirname "$0")"
exec find . -maxdepth 1 -name '*.cs' -print0 | xargs -P "$(nproc || sysctl -n hw.logicalcpu)" -0 -n 1 ./test-one.sh
