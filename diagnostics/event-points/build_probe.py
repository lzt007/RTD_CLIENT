#!/usr/bin/env python3
"""Build a read-only point probe APK. Never installs, runs ADB, or contacts a server."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile

HERE = Path(__file__).resolve().parent
MANAGED = "assets/bin/Data/Managed/"
FLOW = MANAGED + "RTDFlow.dll"
PROBE = MANAGED + "RTDPointProbe.dll"
GAME = MANAGED + "Assembly-CSharp.dll"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def file_path(value: str) -> Path:
    path = Path(value).expanduser().resolve()
    if not path.is_file():
        raise ValueError("Required input file is missing: " + str(path))
    return path


def run(command: list[str | Path], *, env: dict[str, str] | None = None) -> None:
    # Signing command arguments contain environment variable NAMES, never secrets.
    subprocess.run([str(part) for part in command], env=env, check=True)


def entries(archive: zipfile.ZipFile) -> set[str]:
    return {name for name in archive.namelist() if not name.upper().startswith("META-INF/")}


def compare_apks(baseline: Path, candidate: Path) -> dict:
    with zipfile.ZipFile(baseline) as old, zipfile.ZipFile(candidate) as new:
        old_names, new_names = entries(old), entries(new)
        if len(new.namelist()) != len(set(new.namelist())):
            raise ValueError("Duplicate output ZIP member")
        changed = sorted(name for name in old_names & new_names if old.read(name) != new.read(name))
        added, removed = sorted(new_names - old_names), sorted(old_names - new_names)
        if changed != [FLOW] or added != [PROBE] or removed:
            raise ValueError("APK changes exceed the two approved helper entries")
        if old.read(GAME) != new.read(GAME):
            raise ValueError("Gameplay assembly bytes changed")
        return {
            "changed_entries": changed,
            "added_entries": added,
            "removed_entries": removed,
            "gameplay_assembly_sha256": sha(new.read(GAME)),
            "all_other_original_entries_equal": True,
        }


def build(args: argparse.Namespace, work: Path) -> dict:
    apk, dotnet = file_path(args.apk), file_path(args.dotnet)
    java, zipalign, signer = file_path(args.java), file_path(args.zipalign), file_path(args.apksigner)
    key = file_path(args.keystore)
    for name in (args.store_pass_env, args.key_pass_env):
        if not os.environ.get(name):
            raise ValueError("Required signing environment variable is absent: " + name)
    if args.csc:
        compiler = file_path(args.csc)
    else:
        compilers = sorted((dotnet.parent / "sdk").glob("*/Roslyn/bincore/csc.dll"))
        if not compilers:
            raise ValueError("Cannot discover csc.dll; pass --csc")
        compiler = compilers[-1]
    managed = work / "managed"
    managed.mkdir()
    with zipfile.ZipFile(apk) as source:
        if PROBE in source.namelist():
            raise ValueError("Input already contains RTDPointProbe; start from an uninstrumented APK")
        for name in ("mscorlib.dll", "UnityEngine.dll", "Assembly-CSharp.dll", "RTDFlow.dll"):
            (managed / name).write_bytes(source.read(MANAGED + name))
    helper = work / "RTDPointProbe.dll"
    response = work / "compile.rsp"
    arguments = ["/nostdlib", "/target:library", "/optimize+", "/deterministic+",
                 "/out:" + str(helper), "/reference:" + str(managed / "mscorlib.dll"),
                 "/reference:" + str(managed / "UnityEngine.dll"), str(HERE / "PointProbe.cs")]
    response.write_text("\n".join('"' + value + '"' for value in arguments), encoding="utf-8")
    run([dotnet, compiler, "/nologo", "/noconfig", "@" + str(response)])

    if args.patcher:
        patcher = file_path(args.patcher)
    else:
        if not args.cecil:
            raise ValueError("Pass --cecil to build the patcher, or --patcher for a prebuilt patcher DLL")
        cecil = file_path(args.cecil)
        # Build beside temporary inputs, never leave obj/bin under tracked source.
        project = work / "patcher-project"
        project.mkdir()
        for name in ("Program.cs", "PointProbePatcher.csproj"):
            shutil.copyfile(HERE / "PointProbePatcher" / name, project / name)
        run([dotnet, "build", project / "PointProbePatcher.csproj", "--nologo", "--verbosity", "quiet",
             "-p:RTDCecilPath=" + str(cecil), "--output", project / "out"])
        patcher = project / "out" / "PointProbePatcher.dll"
    patched_flow, il_report = work / "RTDFlow-point.dll", work / "il-verification.json"
    run([dotnet, patcher, managed / "RTDFlow.dll", helper, managed / "Assembly-CSharp.dll",
         managed / "UnityEngine.dll", patched_flow, il_report, "1" if args.restore_flow_logs else "0"])
    unsigned, aligned, signed = work / "unsigned.apk", work / "aligned.apk", work / "signed.apk"
    with zipfile.ZipFile(apk) as source, zipfile.ZipFile(unsigned, "w", zipfile.ZIP_DEFLATED) as output:
        for item in source.infolist():
            if item.filename.upper().startswith("META-INF/"):
                continue
            output.writestr(item, patched_flow.read_bytes() if item.filename == FLOW else source.read(item))
        output.writestr(PROBE, helper.read_bytes())
    run([zipalign, "-f", "-p", "4", unsigned, aligned])
    run([java, "-jar", signer, "sign", "--ks", key, "--ks-key-alias", args.alias,
         "--ks-pass", "env:" + args.store_pass_env, "--key-pass", "env:" + args.key_pass_env,
         "--min-sdk-version", "14", "--out", signed, aligned])
    run([zipalign, "-c", "-p", "4", signed])
    run([java, "-jar", signer, "verify", signed])
    result = compare_apks(apk, signed)
    if args.baseline_apk:
        result["baseline_comparison"] = compare_apks(file_path(args.baseline_apk), signed)
    result.update({
        "input_apk_sha256": sha(apk.read_bytes()),
        "apk_sha256": sha(signed.read_bytes()),
        "il_verification": json.loads(il_report.read_text(encoding="utf-8")),
        "zipalign": "passed",
        "apksigner": "passed",
        "runtime_validation": "not installed or executed by this tool",
    })
    with Path(args.output).expanduser().resolve().open("xb") as output, signed.open("rb") as source:
        shutil.copyfileobj(source, output)
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apk", required=True, help="Existing RTD APK containing RTDFlow.Tick(object)")
    parser.add_argument("--output", required=True, help="New signed APK; overwriting is refused")
    parser.add_argument("--report", help="New verification JSON, defaults beside the output")
    parser.add_argument("--baseline-apk", help="Optional earlier APK to verify all original entries against")
    parser.add_argument("--dotnet", required=True, help="Existing .NET SDK 10 dotnet executable")
    parser.add_argument("--csc", help="Existing Roslyn csc.dll; otherwise discovered beside dotnet")
    parser.add_argument("--cecil", help="Existing Mono.Cecil.dll used to build the patcher")
    parser.add_argument("--patcher", help="Prebuilt PointProbePatcher.dll, skipping patcher compilation")
    parser.add_argument("--java", required=True, help="Existing java executable")
    parser.add_argument("--zipalign", required=True, help="Existing Android zipalign executable")
    parser.add_argument("--apksigner", required=True, help="Existing Android apksigner.jar")
    parser.add_argument("--keystore", required=True, help="Existing signing key; never created or copied")
    parser.add_argument("--alias", required=True, help="Existing signing key alias")
    parser.add_argument("--store-pass-env", default="RTD_DIAG_STORE_PASS", help="Environment variable NAME")
    parser.add_argument("--key-pass-env", default="RTD_DIAG_KEY_PASS", help="Environment variable NAME")
    parser.add_argument("--restore-flow-logs", action="store_true",
                        help="Restore exactly two known suppressed RTDFlow UI log calls")
    parser.add_argument("--work-dir", help="New directory to retain intermediate files; default is temporary")
    args = parser.parse_args()
    output = Path(args.output).expanduser().resolve()
    report = Path(args.report).expanduser().resolve() if args.report else output.with_suffix(".verification.json")
    if output.exists() or report.exists() or output == report:
        parser.error("Output/report must be distinct new files")
    output.parent.mkdir(parents=True, exist_ok=True)
    report.parent.mkdir(parents=True, exist_ok=True)
    if args.work_dir:
        work = Path(args.work_dir).expanduser().resolve()
        work.mkdir(parents=True, exist_ok=False)
        result = build(args, work)
    else:
        with tempfile.TemporaryDirectory(prefix="rtd-point-probe-") as directory:
            result = build(args, Path(directory))
    with report.open("x", encoding="utf-8") as target:
        json.dump(result, target, ensure_ascii=False, indent=2)
        target.write("\n")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
