#!/usr/bin/env bash
set -euo pipefail

compat_dir=$(cd "$(dirname "$0")" && pwd)
framework=${FRAMEWORK:-net9.0}
backend=${BACKEND:-json}
old_version=${OLD_HARMONY_VERSION:-2.4.2}
runtime_host=${RUNTIME_HOST:?Set RUNTIME_HOST to the actual x64 dotnet host for the requested framework.}
current_input=${CURRENT_HARMONY:?Set CURRENT_HARMONY to a completed current Fat 0Harmony.dll build.}
sdk_host=${SDK_HOST:-dotnet}
output_dir=${REPORT_DIRECTORY:-"$compat_dir/reports/$framework-$backend-$old_version"}
host_extension=dll
if [[ "$framework" == net472 ]]; then
  host_extension=exe
  if [[ "$backend" != binary ]]; then printf 'The net472 Harmony asset uses BinaryFormatter. Set BACKEND=binary.\n' >&2; exit 2; fi
fi
manifest="$compat_dir/published-artifacts.json"
package_dir="$compat_dir/artifacts/$old_version"
package="$package_dir/lib.harmony.$old_version.nupkg"
old_fixture="Old${old_version//./}"

verify_hash() {
  local file=$1
  local expected=$2
  local actual
  actual=$(shasum -a 256 "$file" | cut -d ' ' -f 1)
  if [[ "$actual" != "$expected" ]]; then
    printf 'Hash mismatch for %s: expected %s, got %s\n' "$file" "$expected" "$actual" >&2
    exit 2
  fi
}

package_hash=$(jq -er --arg version "$old_version" '.versions[$version].packageSha256' "$manifest")
asset_hash=$(jq -er --arg version "$old_version" --arg framework "$framework" '.versions[$version].assets[$framework]' "$manifest")
mkdir -p "$package_dir"
if [[ ! -f "$package" ]]; then
  curl --fail --silent --show-error --location "https://api.nuget.org/v3-flatcontainer/lib.harmony/$old_version/lib.harmony.$old_version.nupkg" --output "$package"
fi
verify_hash "$package" "$package_hash"
unzip -q -o "$package" "lib/$framework/*" -d "$package_dir"
old_engine="$package_dir/lib/$framework/0Harmony.dll"
verify_hash "$old_engine" "$asset_hash"

# A snapshot prevents a concurrent main-project build from changing an engine mid-matrix.
# Every lane gets an isolated directory, retained alongside its reports for reproduction.
current_dir=$(cd "$(dirname "$current_input")" && pwd)
current_stage=$(mktemp -d "$compat_dir/artifacts/current-$framework-XXXXXX")
cp "$current_dir"/*.dll "$current_stage/"
current_engine="$current_stage/0Harmony.dll"
second_engine="$current_engine"
if [[ -n ${SECOND_CURRENT_HARMONY:-} ]]; then
  second_dir=$(cd "$(dirname "$SECOND_CURRENT_HARMONY")" && pwd)
  second_stage=$(mktemp -d "$compat_dir/artifacts/second-$framework-XXXXXX")
  cp "$second_dir"/*.dll "$second_stage/"
  second_engine="$second_stage/0Harmony.dll"
fi

"$sdk_host" build "$compat_dir/Host/Host.csproj" -c Release -p:CompatibilityTargetFramework="$framework" --nologo -v:q >&2
for fixture in "$old_fixture" Current CurrentSecond; do
  reference="$old_engine"
  if [[ "$fixture" != "$old_fixture" ]]; then reference="$current_engine"; fi
  if [[ "$fixture" == CurrentSecond ]]; then reference="$second_engine"; fi
  "$sdk_host" build "$compat_dir/OrdinaryFixture/OrdinaryFixture.csproj" -c Release -p:CompatibilityTargetFramework="$framework" \
    -p:FixtureName="$fixture" -p:HarmonyReference="$reference" --nologo -v:q >&2
done

feature_args=()
if [[ ${FEATURE_TESTS:-1} == 1 ]]; then
  "$sdk_host" build "$compat_dir/FeatureFixture/FeatureFixture.csproj" -c Release -p:CompatibilityTargetFramework="$framework" \
    -p:HarmonyReference="$current_engine" --nologo -v:q >&2
  feature_args=(--feature "$compat_dir/FeatureFixture/bin/Release/$framework/HarmonyCompatibility.Feature.dll")
fi

DOTNET_ROLL_FORWARD=LatestPatch "$runtime_host" "$compat_dir/Host/bin/Release/$framework/HarmonyCompatibility.Host.$host_extension" run \
  --current "$current_engine" --old "$old_engine" \
  --second-current "$second_engine" \
  --old-fixture "$compat_dir/OrdinaryFixture/bin/$old_fixture/Release/$framework/HarmonyCompatibility.Ordinary.$old_fixture.dll" \
  --new-fixture "$compat_dir/OrdinaryFixture/bin/Current/Release/$framework/HarmonyCompatibility.Ordinary.Current.dll" \
  --second-fixture "$compat_dir/OrdinaryFixture/bin/CurrentSecond/Release/$framework/HarmonyCompatibility.Ordinary.CurrentSecond.dll" \
  --framework "$framework" --backend "$backend" --output "$output_dir" --filter "${CASE_FILTER:-}" \
  --require-coexistence "${REQUIRE_COEXISTENCE:-0}" "${feature_args[@]}"
