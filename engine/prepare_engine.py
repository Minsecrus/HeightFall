"""Prepare the pinned Godot/2dog source with the MIT WebGPU patch series.

Only the disposable .engine-cache tree is changed. The game scenes and scripts
are compiled by the original Godot runtime, not converted into another client.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import time
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LOCK_PATH = ROOT / "engine" / "engine-lock.json"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(*args: str, cwd: Path) -> None:
    subprocess.run(args, cwd=cwd, check=True)


def fetch(url: str, target: Path, expected: str) -> None:
    if target.is_file() and digest(target) == expected:
        return
    target.parent.mkdir(parents=True, exist_ok=True)
    for attempt in range(3):
        try:
            with urllib.request.urlopen(url, timeout=60) as response:
                payload = response.read()
            break
        except OSError:
            if attempt == 2:
                raise
            time.sleep(2 * (attempt + 1))
    actual = hashlib.sha256(payload).hexdigest()
    if actual != expected:
        raise RuntimeError(f"Checksum mismatch for {target.name}: {actual}")
    target.write_bytes(payload)


def source_signature() -> str:
    fingerprint = hashlib.sha256(LOCK_PATH.read_bytes())
    for patch in sorted((ROOT / "engine" / "patches").glob("*.patch")):
        fingerprint.update(patch.name.encode())
        fingerprint.update(patch.read_bytes())
    return fingerprint.hexdigest()


def patched_files(patches: list[Path]) -> list[str]:
    paths = set()
    for patch in patches:
        for line in patch.read_text(encoding="utf-8").splitlines():
            if line.startswith("+++ b/") or line.startswith("--- a/"):
                paths.add(line[6:].split("\t", 1)[0])
    return sorted(paths)


def fingerprint_files(source: Path, paths: list[str]) -> dict[str, str | None]:
    result = {}
    for name in paths:
        path = (source / name).resolve()
        if not path.is_relative_to(source):
            raise RuntimeError(f"Patch path escapes engine source: {name}")
        result[name] = digest(path) if path.is_file() else None
    return result


def stamp_source(source: Path, patches: list[Path]) -> None:
    spec = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    (source / ".heightfall-webgpu-source.json").write_text(json.dumps({
        "source_sha256": source_signature(),
        "base_commit": spec["godot"]["commit"],
        "files": fingerprint_files(source, patched_files(patches)),
    }, indent=2) + "\n", encoding="utf-8")


def prepare(source: Path) -> None:
    spec = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    source = source.resolve()
    cache = (ROOT / ".engine-cache").resolve()
    if not source.is_relative_to(cache):
        raise RuntimeError("The engine source must be inside .engine-cache")
    stamp = source / ".heightfall-webgpu-source.json"
    signature = source_signature()
    if stamp.is_file():
        saved = json.loads(stamp.read_text(encoding="utf-8"))
        head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=source, text=True).strip()
        if head != spec["godot"]["commit"]:
            raise RuntimeError(f"Unexpected cached engine commit: {head}")
        if saved.get("source_sha256") != signature:
            raise RuntimeError("Engine patch specification changed; prepare a fresh .engine-cache checkout")
        if fingerprint_files(source, list(saved["files"])) != saved["files"]:
            raise RuntimeError("Prepared engine source differs from its verified patch fingerprint")
        print(f"Godot WebGPU source already prepared and verified: {source}")
        return
    if not (source / ".git").is_dir():
        source.mkdir(parents=True, exist_ok=True)
        run("git", "init", str(source), cwd=ROOT)
        run("git", "remote", "add", "origin", spec["godot"]["repo"], cwd=source)
        run("git", "fetch", "--depth", "1", "origin", spec["godot"]["commit"], cwd=source)
        run("git", "checkout", "--detach", spec["godot"]["commit"], cwd=source)
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=source, text=True).strip()
    if head != spec["godot"]["commit"]:
        raise RuntimeError(f"Unexpected engine source commit: {head}")
    dirty = subprocess.check_output(["git", "status", "--porcelain"], cwd=source, text=True)
    if dirty.strip():
        raise RuntimeError("Engine source has local changes; refusing to reapply patches")

    patch_cache = cache / "patches"
    upstream = spec["webgpu"]
    upstream_patches = []
    for entry in spec["patches"]:
        target = patch_cache / Path(entry["file"]).name
        url = f"https://raw.githubusercontent.com/{upstream['repository']}/{upstream['commit']}/engine/{entry['file']}"
        fetch(url, target, entry["sha256"])
        upstream_patches.append(target)
    for target in upstream_patches:
        command = ["git", "apply", "--whitespace=nowarn"]
        if target.name.startswith("0001-"):
            # 2dog adds mono/library to supported drivers. Our patch preserves
            # those while adding WebGPU; the upstream hunk would replace them.
            command.append("--exclude=platform/web/detect.py")
        run(*command, str(target), cwd=source)
        print(f"Applied {target.name}")
    local_patches = sorted((ROOT / "engine" / "patches").glob("*.patch"))
    for patch in local_patches:
        run("git", "apply", "--whitespace=nowarn", str(patch), cwd=source)
        print(f"Applied {patch.name}")
    stamp_source(source, upstream_patches + local_patches)
    print(f"Godot WebGPU source ready: {source}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=ROOT / ".engine-cache" / "2dog" / "godot")
    options = parser.parse_args()
    prepare(options.source)
