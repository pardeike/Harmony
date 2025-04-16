#!/bin/sh
set -eu
extra_files=$(sed -n 's/.*extra-file: \(.*\)$/\1/p' "$1")
harmony=../../obj/Debug/"$netframework"/0Harmony.dll

if command -v csc > /dev/null 2> /dev/null ; then
	exec csc /nologo /reference:"$harmony" /target:library /out:/dev/null "$1" $extra_files
elif command -v dotnet > /dev/null 2> /dev/null ; then
	name=$(basename "$1" .cs)
	dir=test/"$name"
	mkdir -p "$dir"
	cp "$1" $extra_files "$dir"/
	(
		cd "$dir"
		cat <<EOF > "$name".csproj
			<Project Sdk="Microsoft.NET.Sdk">

				<PropertyGroup>
					<TargetFramework>$netframework</TargetFramework>
				</PropertyGroup>

				<ItemGroup>
					<Reference Include="Harmony">
						<HintPath>../../$harmony</HintPath>
					</Reference>
				</ItemGroup>
			</Project>
EOF
		dotnet build --nologo -v:q
	)
	rm -rf "$dir"
else
	echo 'No C# compiler detected.'
	exit 1
fi
