#!/usr/bin/env python3
"""Run isolated net9/x64 observations against two explicitly supplied Harmony builds."""

import argparse
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess


FIX_EXPECTATIONS = {
    "emitted-callback": {"Plain.value": 99, "Handlers.value": 99},
    "inner-state": {"expected": 3, "actual": 3},
    "named-state": {"expected": 3, "actual": 3},
    "filter-constructor": {"result.value": 7},
    "filter-initializer": {"result.value": 7},
    "ordinary-names": {"Prefix.value": 42, "Postfix.value": 3, "Finalizer.value": 0},
    "ordinary-control": {"Prefix.value": 42, "Postfix.value": 3, "Finalizer.value": 0},
    "reverse-name": {"result.value": 1},
    "reverse-control": {"result.value": 1},
    "assembly-retention": {"after10Cycles": 1, "after20Cycles": 1, "collectible": 1},
    "proxy-retention": {"after10Cycles": 1, "after20Cycles": 1, "collectible": 1},
    "wrapper-lifetime": {"retiredResult": 42, "currentLookup": True, "olderSharedLookup": True,
                         "unpatchedResult": 1, "releasedCollected": True},
    "inflight-wrapper": {"result": 1, "originalMapped": True},
    "remove-prefix": {"beforeRemoval": 11, "removeByMethod.value": 10, "afterMethodRemoval": 10, "removeByOwner.value": 10},
    "remove-postfix": {"beforeRemoval": 11, "removeByMethod.value": 10, "afterMethodRemoval": 10, "removeByOwner.value": 10},
    "remove-finalizer": {"beforeRemoval": 11, "removeByMethod.value": 10, "afterMethodRemoval": 10, "removeByOwner.value": 10},
    "json-unloaded": {"readableOwner": "review.json", "callbackUnavailable": True, "roundtripSame": True},
    "json-public-unloaded": {"readableOwner": "review.json", "callbackUnavailable": True, "roundtripSame": True},
    "method-selector-equality": {"lookup.value": "present", "equalsSelf.value": True, "positionsUnchanged": True},
    "selector-equality": {"innerMethodLookup.value": "present", "innerTargetLookup.value": "present",
                          "innerMethodEqualsSelf.value": True, "innerTargetEqualsSelf.value": True,
                          "fieldLookup.value": "field", "fieldEqualsSelf.value": True, "positionsUnchanged": True},
    "exception-markers": {"result.value": [7, 11, 3]},
}


def verify_fixes(results):
    failures = []
    for result in results:
        if result["engine"] != "current":
            continue
        case = result["case"]
        observations = result.get("observations", {})
        for path, expected in FIX_EXPECTATIONS[case].items():
            actual = observations
            for key in path.split("."):
                actual = actual.get(key) if isinstance(actual, dict) else None
            if actual != expected or type(actual) is not type(expected):
                failures.append(f"{case}: {path} expected {expected!r}, got {actual!r}")
        if case == "selector-equality":
            errors = observations.get("registration", {}).get("errors", [])
            if not any("SerializationException" in error and "2 loaded matches" in error for error in errors):
                failures.append("selector-equality: ambiguous registration was not rejected")
    return failures


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime", required=True, type=Path)
    parser.add_argument("--current", required=True, type=Path)
    parser.add_argument("--baseline", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--sdk", default="dotnet")
    parser.add_argument("--case", action="append", dest="selected")
    parser.add_argument("--verify-fixes", action="store_true",
                        help="Assert fixed behavior in the current engine; defaults to all fixed cases and controls")
    args = parser.parse_args()
    if args.verify_fixes:
        args.selected = args.selected or list(FIX_EXPECTATIONS)
        if set(args.selected) - FIX_EXPECTATIONS.keys():
            parser.error("--verify-fixes requires cases with fixed-behavior assertions")
    project = Path(__file__).resolve().parent
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    current, baseline = args.current.resolve(), args.baseline.resolve()
    for path in (args.runtime, current, baseline):
        if not path.is_file():
            parser.error(f"Missing input: {path}")

    def build(project_file, name, *properties):
        artifacts = output / ("build-" + name)
        command = [args.sdk, "build", str(project_file), "-c", "Release", "-v:q",
                   f"-p:HarmonyPath={current}", f"-p:BaseIntermediateOutputPath={artifacts}/obj/",
                   "--artifacts-path", str(artifacts), *properties]
        result = subprocess.run(command, capture_output=True, text=True, timeout=120)
        (output / (name + "-build.log")).write_text(result.stdout + result.stderr)
        if result.returncode:
            raise RuntimeError(f"Build failed; see {output / (name + '-build.log')}")
        return artifacts / "bin" / project_file.stem / "release"

    host = build(project / "ReviewProbes.csproj", "host")
    first = build(project / "Plugin/Plugin.csproj", "plugin-first", "-p:Variant=First") / "ReviewPatchPlugin.dll"
    second = build(project / "Plugin/Plugin.csproj", "plugin-second", "-p:Variant=Second") / "ReviewPatchPlugin.dll"
    for label, engine in (("current", current), ("baseline", baseline)):
        destination = output / label
        shutil.copytree(host, destination, dirs_exist_ok=True)
        shutil.copy2(engine, destination / "0Harmony.dll")

    both = ["emitted-callback", "filter-constructor", "filter-initializer", "ordinary-names",
            "ordinary-control", "reverse-name", "reverse-control", "assembly-retention",
            "exception-markers", "extern-unpatch", "json-unloaded", "method-selector-equality"]
    current_only = ["inner-state", "named-state", "remove-prefix", "remove-postfix", "remove-finalizer",
                    "selector-equality", "json-public-unloaded", "shared-state-current", "shared-state-baseline",
                    "proxy-retention", "wrapper-lifetime", "inflight-wrapper"]
    known = set(both + current_only)
    if args.selected and set(args.selected) - known:
        parser.error("Unknown case: " + ", ".join(sorted(set(args.selected) - known)))
    jobs = [(label, case) for case in both for label in ("current", "baseline")]
    jobs += [("current", case) for case in current_only]
    if args.selected:
        jobs = [(label, case) for label, case in jobs if case in args.selected]
    environment = dict(os.environ, DOTNET_TieredCompilation="0", DOTNET_ROLL_FORWARD="LatestPatch")

    def run(job):
        label, case = job
        command = [str(args.runtime.resolve()), str(output / label / "ReviewProbes.dll"), case,
                   str(first), str(second), str(baseline)]
        result = subprocess.run(command, env=environment, capture_output=True, text=True, timeout=45)
        log_name = label + "-" + case
        (output / (log_name + ".log")).write_text(result.stdout + result.stderr)
        observation = json.loads(result.stdout)
        observation["engine"] = label
        if result.returncode or "probeError" in observation:
            observation["incomplete"] = True
        (output / (log_name + ".json")).write_text(json.dumps(observation, indent=2) + "\n")
        print(f"{log_name}: {'INCOMPLETE' if observation.get('incomplete') else 'observed'}", flush=True)
        return observation

    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
        results = list(executor.map(run, jobs))
    report = {
        "inputs": {label: {"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                   for label, path in (("current", current), ("baseline", baseline))},
        "results": results,
    }
    failures = verify_fixes(results) if args.verify_fixes else []
    if args.verify_fixes:
        report["verification"] = {"passed": not failures and not any(result.get("incomplete") for result in results),
                                  "currentCases": sum(result["engine"] == "current" for result in results), "failures": failures}
        for failure in failures:
            print(f"FAIL: {failure}", flush=True)
        print(f"Fix verification: {'FAILED' if not report['verification']['passed'] else 'PASSED'}", flush=True)
    (output / "results.json").write_text(json.dumps(report, indent=2) + "\n")
    return 1 if failures or any(result.get("incomplete") for result in results) else 0


if __name__ == "__main__":
    raise SystemExit(main())
