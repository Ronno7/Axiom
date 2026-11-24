"""Assemble the v15 handoff from validated FBXs and the v14 runtime data."""

import hashlib
import json
from pathlib import Path
from tempfile import NamedTemporaryFile
from zipfile import ZIP_DEFLATED, ZipFile


REVIEWS = Path(__file__).resolve().parents[1] / "reviews"
V14 = REVIEWS / "v14" / "export"
V15 = REVIEWS / "v15" / "export"
FBXS = ("SweptDart.fbx", "SweptDart_Interior.fbx", "SweptDart_Construction.fbx")
INHERITED = (
    "HullArrival.exr",
    "TraceTimeline.exr",
    "material-bindings.json",
    "timeline.json",
    "Shaders/SweptDartSurface.shader",
    "Shaders/SweptDartTrace.shader",
)

README = """# Swept Dart v15

This import package carries the v13 three-second construction sequence, the v14
runtime data, and three revised FBXs. The v15 FBX measurements are in
[the validation report](validation.json).

| File | Contents |
| --- | --- |
| `SweptDart.fbx` | Hull, permanent lights, shield segments and four sockets |
| `SweptDart_Interior.fbx` | Seven interior groups shown during construction |
| `SweptDart_Construction.fbx` | The 91 construction traces |
| `HullArrival.exr`, `TraceTimeline.exr`, `Shaders/` | Build timing and surface and trace shaders |
| `material-bindings.json`, `timeline.json` | Material recipes and trace timing |

For the isolated reimport, replace the three FBXs in the existing model folder
and retain their .meta files. Keep Scale Factor 1, Convert Units on and Bake Axis
Conversion on. The nose belongs at local Z +2.5 m and the left muzzle at
(−0.75, 0, +1.10) m. Both muzzles face +Z. Use the
[manifest](package-manifest.json) for all socket frames, mesh names and counts.
The [SHA-256 list](checksums.json) covers the files in the export folder.
"""


def json_bytes(value):
    return (json.dumps(value, indent=2) + "\n").encode("utf-8")


def assemble():
    previous_hashes = json.loads((V14 / "checksums.json").read_bytes())
    inputs = {}
    for name in (*INHERITED, "package-manifest.json"):
        data = (V14 / name).read_bytes()
        if hashlib.sha256(data).hexdigest() != previous_hashes[name]:
            raise ValueError(f"v14 source checksum mismatch: {name}")
        inputs[name] = data

    manifest = json.loads(inputs["package-manifest.json"])
    source = REVIEWS / "v13" / "SweptDart_v13.blend"
    if hashlib.sha256(source.read_bytes()).hexdigest() != manifest["source_sha256"]:
        raise ValueError("Accepted v13 source checksum mismatch")
    manifest["revision"] = "v15"
    manifest["provenance"] = {
        "parent_revision": "v14",
        "parent_manifest_sha256": previous_hashes["package-manifest.json"],
        "byte_identical_parent_files": list(INHERITED),
    }
    manifest["verification"] = (
        "validation.json records measurements of the serialized v15 FBXs. "
        "Check the import in Axiom before scene integration and runtime profiling."
    )
    inputs["package-manifest.json"] = json_bytes(manifest)

    fbx_hashes = {}
    for name in FBXS:
        inputs[name] = (V15 / name).read_bytes()
        fbx_hashes[name] = hashlib.sha256(inputs[name]).hexdigest()
    inputs["validation.json"] = (V15 / "validation.json").read_bytes()
    validation = json.loads(inputs["validation.json"])
    if validation["passed"] is not True or validation.get("fbx_sha256") != fbx_hashes:
        raise ValueError("Generate a passing validation.json for the current v15 FBXs")

    # Keep measured Editor evidence with the specific files that were imported.
    proof_path = V15.parent / "qa/unity-import-proof.json"
    proof_bytes = None
    readme = README
    if proof_path.is_file():
        data = proof_path.read_bytes()
        proof = json.loads(data)
        measured = {name: info["sha256"] for name, info in proof["v15"]["files"].items()}
        if proof["passed"] is True and measured == fbx_hashes:
            proof_bytes = data
            readme += "\n[Isolated Unity import results](../qa/unity-import-proof.json) for these FBXs.\n"
    inputs["README.md"] = readme.encode("utf-8")
    digests = {name: hashlib.sha256(data).hexdigest() for name, data in sorted(inputs.items())}
    inputs["checksums.json"] = json_bytes(digests)
    for name in (*INHERITED, "package-manifest.json", "README.md", "checksums.json"):
        destination = V15 / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(inputs[name])

    bundle = V15.parent / "SweptDart-v15-import.zip"
    with NamedTemporaryFile(dir=bundle.parent, suffix=".zip", delete=False) as tmp:
        temporary = Path(tmp.name)
    archive_files = {"export/" + name: data for name, data in inputs.items()}
    if proof_bytes is not None:
        archive_files["qa/unity-import-proof.json"] = proof_bytes
    try:
        with ZipFile(temporary, "w", ZIP_DEFLATED, compresslevel=6) as archive:
            for name in sorted(archive_files):
                archive.writestr(name, archive_files[name])
        with ZipFile(temporary) as archive:
            for name, expected in archive_files.items():
                if archive.read(name) != expected:
                    raise ValueError(f"Packaged file differs from validated input: {name}")
        temporary.replace(bundle)
    finally:
        temporary.unlink(missing_ok=True)
    print(f"Verified {len(archive_files)} files in {bundle}")


if __name__ == "__main__":
    assemble()
