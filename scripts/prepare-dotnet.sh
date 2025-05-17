#!/usr/bin/env bash
set -euo pipefail

curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0 --install-dir "$HOME/.dotnet"

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$PATH:$DOTNET_ROOT"

dotnet restore --runtime linux-x64 --verbosity minimal

mkdir -p .nuget && cp -r "$HOME/.nuget/packages" .nuget/

echo 'export DOTNET_ROOT="$HOME/.dotnet"' >> ~/.profile
echo 'export PATH="$PATH:$HOME/.dotnet"'  >> ~/.profile
echo 'export DOTNET_NOLOGO=1'            >> ~/.profile
echo 'export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1' >> ~/.profile

dotnet --info | head -n 20

exit 0