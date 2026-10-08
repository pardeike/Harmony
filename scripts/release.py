#!/usr/bin/env python3
"""Stage and publish the exact Harmony packages produced by tested CI."""

import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid
import xml.etree.ElementTree as ET
import zipfile


IDS = {"Ref": "Lib.Harmony.Ref", "Fat": "Lib.Harmony", "Thin": "Lib.Harmony.Thin"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def command(*args, capture=True):
    return subprocess.run(args, check=True, text=True, stdout=subprocess.PIPE if capture else None).stdout


def api(path, **fields):
    args = ["gh", "api", path]
    for key, value in fields.items():
        if isinstance(value, bool):
            args.extend(["-F", f"{key}={str(value).lower()}"])
        else:
            args.extend(["-f", f"{key}={value}"])
    return json.loads(command(*args))


def version_info():
    root = ET.parse("Directory.Build.props").getroot()
    assembly = root.findtext(".//HarmonyVersion")
    suffix = root.findtext(".//HarmonyPrerelease") or ""
    require(re.fullmatch(r"\d+\.\d+\.\d+\.0", assembly or ""), "Expected a four-part HarmonyVersion ending in .0")
    require(re.fullmatch(r"(?:-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?", suffix), "Invalid prerelease suffix")
    version = assembly.removesuffix(".0") + suffix
    return {"version": version, "archive_version": assembly + suffix, "tag": "v" + version,
            "title": "Harmony " + version, "prerelease": bool(suffix),
            "frameworks": root.findtext(".//TargetFrameworks").split(";")}


def package_contents(data):
    # NuGet adds a repository signature after upload. Compare all original entries.
    with zipfile.ZipFile(io.BytesIO(data)) as z:
        require(len(z.namelist()) == len(set(z.namelist())), "Duplicate archive entries")
        return {n: z.read(n) for n in z.namelist() if not n.endswith("/") and n != ".signature.p7s"}


def validate_files(directory, info, source_sha):
    files = {}
    for variant, package_id in IDS.items():
        package = directory / f"{package_id}.{info['version']}.nupkg"
        archive = directory / f"Harmony-{variant}.{info['archive_version']}.zip"
        contents = package_contents(package.read_bytes())
        specs = [v for n, v in contents.items() if n.endswith(".nuspec")]
        require(len(specs) == 1, f"Expected one nuspec in {package.name}")
        root = ET.fromstring(specs[0])
        ns = {"n": root.tag.partition("}")[0].lstrip("{")}
        metadata = root.find("n:metadata", ns)
        require(metadata.findtext("n:id", namespaces=ns) == package_id, "Package ID mismatch")
        require(metadata.findtext("n:version", namespaces=ns) == info["version"], "Package version mismatch")
        repository = metadata.find("n:repository", ns)
        require(repository is not None and repository.get("commit") == source_sha, "Package source commit mismatch")
        dependencies = metadata.findall(".//n:dependency", ns)
        if variant != "Ref":
            refs = [d for d in dependencies if d.get("id") == IDS["Ref"]]
            require(refs and all(d.get("version") == info["version"] for d in refs), "Ref dependency version mismatch")
            core = [d for d in dependencies if d.get("id") == "MonoMod.Core"]
            require(bool(core) == (variant == "Thin"), "Unexpected MonoMod.Core dependency")
        frameworks = ["netstandard2.0"] if variant == "Ref" else info["frameworks"]
        prefix = "ref" if variant == "Ref" else "lib"
        zipped = package_contents(archive.read_bytes())
        for framework in frameworks:
            name = f"{prefix}/{framework}/0Harmony.dll"
            require(name in contents, f"Missing {name} in {package.name}")
            require(zipped.get(f"{framework}/0Harmony.dll") == contents[name], f"ZIP/package mismatch for {variant}/{framework}")
        for file in (package, archive):
            files[file.name] = hashlib.sha256(file.read_bytes()).hexdigest()
    return files


def successful_run(repo, workflow, sha, run_id=None):
    if run_id:
        run = api(f"repos/{repo}/actions/runs/{int(run_id)}")
    else:
        runs = api(f"repos/{repo}/actions/workflows/{workflow}/runs?head_sha={sha}&per_page=100")["workflow_runs"]
        matches = [r for r in runs if r["conclusion"] == "success" and r["event"] in ("push", "workflow_dispatch")]
        require(matches, f"No successful {workflow} run for {sha}")
        run = matches[0]
    require(run["path"] == f".github/workflows/{workflow}" and run["head_sha"] == sha
            and run["conclusion"] == "success" and run["event"] in ("push", "workflow_dispatch")
            and run["head_repository"]["full_name"] == repo, f"Run {run['id']} is not successful trusted CI for this commit")
    return run["id"]


def gates(repo, sha, ci_run=None):
    ci = successful_run(repo, "test.yml", sha, ci_run)
    compatibility = None
    if Path(".github/workflows/test-infix-compatibility.yml").exists():
        compatibility = successful_run(repo, "test-infix-compatibility.yml", sha)
    return {"ci_run": ci, "compatibility_run": compatibility}


def smoke(directory, info):
    source = '''using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
public static class Program {
    [MethodImpl(MethodImplOptions.NoInlining)] public static int Target(int value) => value + 1;
    public static void Prefix(ref int value) => value += 2;
    public static void Postfix(ref int __result) => __result *= 3;
#if HARMONY3
    [MethodImpl(MethodImplOptions.NoInlining)] public static int Outer(int value) => Target(value) * 2;
    [HarmonyInfix(typeof(Program), nameof(Target), typeof(int))]
    public static void InnerPrefix(ref int value) => value += 10;
#endif
    public static void Main() {
        var harmony = new Harmony("release.package.smoke");
        harmony.Patch(typeof(Program).GetMethod(nameof(Target)),
            new HarmonyMethod(typeof(Program).GetMethod(nameof(Prefix))),
            new HarmonyMethod(typeof(Program).GetMethod(nameof(Postfix))));
        if (Target(4) != 21) throw new Exception("Package patch failed");
        harmony.UnpatchAll(harmony.Id);
        if (Target(4) != 5) throw new Exception("Package unpatch failed");
#if HARMONY3
        harmony.CreateProcessor(typeof(Program).GetMethod(nameof(Outer)))
            .AddInnerPrefix(typeof(Program).GetMethod(nameof(InnerPrefix))).Patch();
        if (Outer(4) != 30 || Target(4) != 5) throw new Exception("Packaged Infix scope failed");
        harmony.UnpatchAll(harmony.Id);
        if (Outer(4) != 10) throw new Exception("Packaged Infix unpatch failed");
#endif
    }
}
'''
    with tempfile.TemporaryDirectory() as temp:
        folder = Path(temp)
        for variant, package_id in IDS.items():
            project = folder / variant
            project.mkdir()
            tfm = "netstandard2.0" if variant == "Ref" else "net10.0"
            output = "Library" if variant == "Ref" else "Exe"
            (project / "Smoke.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>{tfm}</TargetFramework><OutputType>{output}</OutputType><PlatformTarget>x64</PlatformTarget>
<DefineConstants>{'HARMONY3' if int(info['version'].split('.')[0]) >= 3 else ''}</DefineConstants>
</PropertyGroup><ItemGroup><PackageReference Include="{package_id}" Version="{info['version']}" /></ItemGroup></Project>''')
            (project / "Program.cs").write_text(source)
            command("dotnet", "restore", str(project / "Smoke.csproj"), "--packages", str(folder / "cache"),
                    "--source", str(directory.resolve()), "--source", "https://api.nuget.org/v3/index.json", capture=False)
            command("dotnet", "build", str(project / "Smoke.csproj"), "-c", "Release", "--no-restore", capture=False)
            if variant != "Ref":
                command("dotnet", str(project / "bin/Release/net10.0/Smoke.dll"), capture=False)


def save_manifest(directory, manifest):
    (directory / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")


def stage(args):
    info = version_info()
    destination = Path(args.directory)
    require(not destination.exists(), "Staging directory must be new")
    destination.mkdir(parents=True)
    sha = command("git", "rev-parse", "HEAD").strip()
    files = validate_files(Path("packages"), info, sha)
    for name in files:
        shutil.copy2(Path("packages") / name, destination / name)
    save_manifest(destination, {**info, "source_sha": sha, "files": files})


def prepare(args):
    sha = command("git", "rev-parse", "HEAD").strip()
    checked = gates(args.repo, sha, args.ci_run)
    directory = Path(args.directory)
    command("gh", "run", "download", str(checked["ci_run"]), "--repo", args.repo,
            "--name", "release-packages", "--dir", str(directory))
    manifest = verify(directory)
    require(manifest["source_sha"] == sha, "Artifact source commit mismatch")
    smoke(directory, manifest)
    notes = api(f"repos/{args.repo}/releases/generate-notes", tag_name=manifest["tag"], target_commitish=sha)
    intro = Path("docs/release-intro.md").read_text().strip() if Path("docs/release-intro.md").exists() else ""
    body = (intro + "\n\n" if intro else "") + notes["body"] + "\n"
    (directory / "release-notes.md").write_text(body)
    manifest.update(checked)
    manifest["notes_sha256"] = hashlib.sha256(body.encode()).hexdigest()
    manifest["repository"] = args.repo
    save_manifest(directory, manifest)
    print(json.dumps(manifest, indent=2))
    print(body)
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a") as out:
            out.write(f"# {manifest['title']}\n\nSource: `{sha}`\n\n{body}\n\nReview the `prepared-release` artifact before running publish.\n")


def verify(directory, prepared=False):
    manifest = json.loads((directory / "manifest.json").read_text())
    info = version_info()
    for key, value in info.items():
        require(manifest[key] == value, f"Manifest {key} mismatch")
    require(manifest["source_sha"] == command("git", "rev-parse", "HEAD").strip(), "Artifact is for another commit")
    require(validate_files(directory, info, manifest["source_sha"]) == manifest["files"], "Artifact checksum mismatch")
    if prepared:
        require(hashlib.sha256((directory / "release-notes.md").read_bytes()).hexdigest() == manifest["notes_sha256"], "Release notes changed")
    return manifest


def public_package(package_id, version):
    url = f"https://api.nuget.org/v3-flatcontainer/{package_id.lower()}/{version.lower()}/{package_id.lower()}.{version.lower()}.nupkg"
    try:
        with urllib.request.urlopen(url, timeout=60) as response:
            return response.read()
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None
        raise


def push_package(file):
    # Keep the short-lived key in memory and an HTTP header, never process arguments or files.
    boundary = uuid.uuid4().hex
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="package"; filename="{file.name}"\r\n'
            'Content-Type: application/octet-stream\r\n\r\n').encode() + file.read_bytes() + f"\r\n--{boundary}--\r\n".encode()
    request = urllib.request.Request("https://www.nuget.org/api/v2/package", data=body, method="PUT", headers={
        "X-NuGet-ApiKey": os.environ["NUGET_API_KEY"], "X-NuGet-Protocol-Version": "4.1.0",
        "User-Agent": "Harmony-Release-Workflow", "Content-Type": f"multipart/form-data; boundary={boundary}"})
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            require(response.status in (201, 202), f"Unexpected NuGet status {response.status}")
    except urllib.error.HTTPError as error:
        if error.code != 409:
            raise ValueError(f"NuGet upload failed with HTTP {error.code} for {file.name}") from None
        # Another accepted upload may still be indexing. Verify its contents before continuing.
    print(f"NuGet accepted {file.name}; waiting for public package validation", flush=True)


def publish(args):
    directory = Path(args.directory)
    manifest = verify(directory, prepared=True)
    require(manifest["repository"] == args.repo, "Prepared repository mismatch")
    successful_run(args.repo, "release.yml", manifest["source_sha"], args.preparation_run)
    gates(args.repo, manifest["source_sha"], manifest["ci_run"])
    github_state(directory, manifest, args.repo)
    # Fail before publishing anything when an existing immutable version differs.
    existing = {}
    for package_id in IDS.values():
        file = directory / f"{package_id}.{manifest['version']}.nupkg"
        data = public_package(package_id, manifest["version"])
        if data is not None:
            require(package_contents(data) == package_contents(file.read_bytes()), f"Existing {package_id} differs; choose a new version")
        existing[package_id] = data is not None
    for package_id in IDS.values():
        file = directory / f"{package_id}.{manifest['version']}.nupkg"
        if not existing[package_id]:
            push_package(file)
        for attempt in range(60):
            data = public_package(package_id, manifest["version"])
            if data is not None:
                require(package_contents(data) == package_contents(file.read_bytes()), f"Published {package_id} differs")
                break
            time.sleep(10)
        else:
            raise ValueError(f"NuGet indexing timed out for {package_id}; rerun publish to resume")
    publish_github(directory, manifest, args.repo)


def github_state(directory, manifest, repo):
    tag = manifest["tag"]
    refs = api(f"repos/{repo}/git/matching-refs/tags/{tag}")
    exact = [r for r in refs if r["ref"] == "refs/tags/" + tag]
    if exact:
        require(exact[0]["object"]["type"] == "commit" and exact[0]["object"]["sha"] == manifest["source_sha"], "Release tag already points elsewhere")
    releases = api(f"repos/{repo}/releases?per_page=100")
    matches = [r for r in releases if r["tag_name"] == tag]
    if matches:
        release = matches[0]
        require(release["name"] == manifest["title"] and release["body"].strip() == (directory / "release-notes.md").read_text().strip()
                and release["prerelease"] == manifest["prerelease"], "Existing release metadata differs")
        return bool(exact), release
    return bool(exact), None


def publish_github(directory, manifest, repo):
    tag = manifest["tag"]
    has_tag, release = github_state(directory, manifest, repo)
    # Create the tag only after NuGet has all three exact packages. Never move an existing tag.
    if not has_tag:
        api(f"repos/{repo}/git/refs", ref="refs/tags/" + tag, sha=manifest["source_sha"])
    if release is None:
        # Use the creation response directly: the release list can lag behind a newly created draft.
        release = api(f"repos/{repo}/releases", tag_name=tag, target_commitish=manifest["source_sha"],
                      name=manifest["title"], body=(directory / "release-notes.md").read_text(),
                      draft=True, prerelease=manifest["prerelease"])
    expected = {n for n in manifest["files"] if n.endswith(".zip")}
    assets = {a["name"]: a for a in release["assets"]}
    require(set(assets) <= expected, "Unexpected release assets")
    for name in sorted(expected):
        if name in assets:
            asset = assets[name]
            raw = subprocess.run(["gh", "api", f"repos/{repo}/releases/assets/{asset['id']}", "-H", "Accept: application/octet-stream"],
                                 check=True, stdout=subprocess.PIPE).stdout
            require(hashlib.sha256(raw).hexdigest() == manifest["files"][name], f"Existing release asset differs: {name}")
        else:
            require(release["draft"], "Published release is incomplete")
            command("gh", "release", "upload", tag, str(directory / name), "--repo", repo, capture=False)
    release = api(f"repos/{repo}/releases/{release['id']}")
    require({a["name"] for a in release["assets"]} == expected, "Incomplete GitHub assets")
    for asset in release["assets"]:
        require(asset.get("digest") == "sha256:" + manifest["files"][asset["name"]], f"GitHub asset checksum mismatch: {asset['name']}")
    if release["draft"]:
        command("gh", "release", "edit", tag, "--repo", repo, "--draft=false", "--latest=false" if manifest["prerelease"] else "--latest", capture=False)
    print(f"Published {manifest['title']}: https://github.com/{repo}/releases/tag/{tag}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["stage", "prepare", "verify", "publish"])
    parser.add_argument("--directory", default="artifacts/release")
    parser.add_argument("--repo", default="pardeike/Harmony")
    parser.add_argument("--ci-run")
    parser.add_argument("--preparation-run")
    args = parser.parse_args()
    if args.mode == "verify":
        verify(Path(args.directory), prepared=True)
    else:
        globals()[args.mode](args)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        # CalledProcessError includes command arguments, potentially the temporary publishing key.
        print(f"Release failed: {error if not isinstance(error, subprocess.CalledProcessError) else 'external command returned a nonzero exit code'}", file=sys.stderr)
        sys.exit(1)
