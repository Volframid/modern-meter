import hashlib
import json
import re
import sys
from pathlib import Path, PurePosixPath
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def package(tag, destination):
    root = Path(__file__).resolve().parents[1]
    descriptor = json.loads((root / "module.json").read_bytes())
    version = descriptor["version"]
    if not re.fullmatch(r"(0|[1-9][0-9]*)(\.(0|[1-9][0-9]*)){2}", version) or tag != f"v{version}":
        raise SystemExit(f"Release tag {tag!r} does not match module version {version!r}")

    paths = json.loads((root / "distribution-files.json").read_bytes())["files"]
    manifest = (root / "manifest.json").read_bytes()
    checksums = json.loads(manifest)["files"]
    if len(paths) != len(checksums) or set(paths) != set(checksums):
        raise SystemExit("Distribution list and Toolbox manifest differ")

    destination.mkdir(parents=True, exist_ok=True)
    archive_path = destination / f"modern-meter-{tag}.zip"
    temporary_archive = archive_path.with_suffix(".tmp")
    try:
        with ZipFile(temporary_archive, "w", ZIP_DEFLATED) as archive:
            for name in paths:
                path = PurePosixPath(name)
                normalized = name.lower()
                if (path.is_absolute() or path.as_posix() != name or ".." in path.parts or ":" in name
                    or "\\" in name or normalized.startswith(("resources/config/", "logs/", ".modern-ui-pending/"))
                    or path.name.lower() in {"local_updates.md", "module.config.json", "manifest.json"}
                    or normalized.endswith(".log")):
                    raise SystemExit(f"Invalid distribution path: {name}")
                source = root / path
                if not source.resolve().is_relative_to(root):
                    raise SystemExit(f"Distribution path escapes repository: {name}")
                data = source.read_bytes()
                if sha256(data) != checksums[name]:
                    raise SystemExit(f"Manifest hash does not match {name}")
                archive.writestr(ZipInfo(f"modern-meter/{name}"), data, ZIP_DEFLATED)
            archive.writestr(ZipInfo("modern-meter/manifest.json"), manifest, ZIP_DEFLATED)
        temporary_archive.replace(archive_path)
    finally:
        temporary_archive.unlink(missing_ok=True)

    digest = sha256(archive_path.read_bytes())
    archive_path.with_suffix(".sha256").write_bytes(f"{digest}  {archive_path.name}\n".encode("ascii"))
    print(f"Packaged {len(paths)} validated runtime files for {tag}. SHA-256 {digest}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: python scripts/package-release.py vMAJOR.MINOR.PATCH OUTPUT_DIRECTORY")
    package(sys.argv[1], Path(sys.argv[2]))
