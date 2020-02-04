#!/bin/sh
exec csc /nologo /reference:"$(find ../../obj -name 0Harmony.dll | head -n 1)" /target:library /out:/dev/null "$1" $(sed -n 's/.*extra-arg: \(.*\)$/\1/p' "$1")
