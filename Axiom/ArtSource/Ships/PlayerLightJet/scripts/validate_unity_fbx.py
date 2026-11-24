"""Check the serialized v15 FBX files against Unity's measured v14 import basis.

From the ship folder, run Blender with:
  -b --factory-startup --python-exit-code 1 -P scripts/validate_unity_fbx.py
  -- [export_directory] [--report validation.json]

The binary parser does not import a scene. These checks model Bake Axis Conversion
on with the exact v14 axis header, metres and identity transforms. Unity's v14
measurements establish (x, y, -z) for FBX points. A reflected coordinate basis
conjugates rotations, so an identity socket frame yields Unity +Z forward/+Y up.
The definitive import, rendering and runtime checks still take place in Unity.
"""

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys

import numpy as np
from io_scene_fbx import parse_fbx


REVIEWS = Path(__file__).resolve().parents[1] / "reviews"
BASELINE = REVIEWS / "v14/export"
FBX_FILES = ("SweptDart.fbx", "SweptDart_Interior.fbx", "SweptDart_Construction.fbx")
UNITY_BASIS = np.array([1.0, 1.0, -1.0])
TOLERANCE = 1e-5
HEADER = {
    b"UpAxis": 1, b"UpAxisSign": 1, b"FrontAxis": 2, b"FrontAxisSign": 1,
    b"CoordAxis": 0, b"CoordAxisSign": 1, b"UnitScaleFactor": 100,
}
SOCKETS = {
    "Muzzle_L": (-0.75, 0, 1.1), "Muzzle_R": (0.75, 0, 1.1),
    "Exhaust_L": (-0.85, 0, -1.64), "Exhaust_R": (0.85, 0, -1.64),
}


def child(element, field):
    return next(node for node in element.elems if node.id == field)


def value(element, field):
    return child(element, field).props[0]


def properties(element):
    return {p.props[0]: p.props[4:] for p in child(element, b"Properties70").elems}


def label(element):
    return element.props[1].split(b"\0", 1)[0].decode()


def close(actual, expected):
    a, b = np.asarray(actual), np.asarray(expected)
    return a.shape == b.shape and np.allclose(a, b, atol=TOLERANCE, rtol=0)


class ModelFile:
    def __init__(self, path):
        tree, _ = parse_fbx.parse(str(path))
        self.settings = properties(child(tree, b"GlobalSettings"))
        objects = child(tree, b"Objects").elems
        self.models = {}
        self.geometry = {}
        self.materials = {}
        self.parents = {}
        by_id = {obj.props[0]: obj for obj in objects}
        for obj in objects:
            if obj.id == b"Model":
                # Strip the old suffix only to match geometry for diagnostics.
                # check_file separately requires every serialized v15 name to be exact.
                name = re.sub(r"\.\d{3,}$", "", label(obj))
                if name in self.models:
                    raise ValueError(f"{path.name}: duplicate model {name}")
                self.models[name] = obj
        for link in child(tree, b"Connections").elems:
            kind, source, dest = link.props[:3]
            if kind != b"OO":
                continue
            obj = by_id[source]
            if obj.id == b"Model":
                self.parents[source] = dest
            elif obj.id == b"Geometry":
                self.geometry[dest] = obj
            elif obj.id == b"Material":
                self.materials.setdefault(dest, []).append(label(obj))


def loop_values(layer, field, index_field, width):
    if value(layer, b"MappingInformationType") != b"ByPolygonVertex":
        raise ValueError(f"Unsupported FBX mapping for {field!r}")
    direct = np.asarray(value(layer, field)).reshape(-1, width)
    reference = value(layer, b"ReferenceInformationType")
    if reference == b"IndexToDirect":
        direct = direct[np.asarray(value(layer, index_field))]
    elif reference != b"Direct":
        raise ValueError(f"Unsupported FBX reference for {field!r}")
    return direct.reshape(-1, 3, width)


def triangles(geometry, reverse=False):
    """Canonicalize face order and cyclic corner order, retaining winding and UV seams."""
    indices = np.array(value(geometry, b"PolygonVertexIndex")).reshape(-1, 3)
    if (indices[:, :2] < 0).any() or (indices[:, 2] >= 0).any():
        raise ValueError("Non-triangular FBX polygon")
    indices[:, 2] = ~indices[:, 2]
    columns = np.arange(3) * (-1 if reverse else 1)
    columns = (np.argmin(indices, axis=1)[:, None] + columns) % 3
    rows = np.arange(len(indices))[:, None]
    ids = indices[rows, columns]
    order = np.lexsort(ids.T[::-1])
    vertices = np.asarray(value(geometry, b"Vertices")).reshape(-1, 3)
    normals = loop_values(child(geometry, b"LayerElementNormal"), b"Normals", b"NormalsIndex", 3)
    points = vertices[indices]
    faces = np.cross(points[:, 1] - points[:, 0], points[:, 2] - points[:, 0])
    lengths = np.linalg.norm(faces, axis=1)
    if np.any(lengths <= 0):
        raise ValueError(f"{label(geometry)}: degenerate triangle")
    face_normals = faces / lengths[:, None]
    min_agreement = float(np.einsum("ij,ikj->ik", face_normals, normals).min())
    layers = sorted((e for e in geometry.elems if e.id == b"LayerElementUV"), key=lambda e: e.props[0])
    uvs = {value(layer, b"Name"): loop_values(layer, b"UV", b"UVIndex", 2)[rows, columns][order]
           for layer in layers}
    material = child(geometry, b"LayerElementMaterial")
    slots = np.asarray(value(material, b"Materials"))
    if value(material, b"MappingInformationType") == b"AllSame":
        slots = np.full(len(indices), slots.item())
    return vertices, ids[order], uvs, slots[order], normals[rows, columns][order], min_agreement


def check_file(path, baseline, manifest, report, errors):
    actual = ModelFile(path)
    expected = ModelFile(baseline)

    def check(condition, detail):
        if not condition:
            errors.append(f"{path.name}: {detail}")

    for key, wanted in HEADER.items():
        check(actual.settings.get(key) == [wanted], f"unexpected {key.decode()} (v14 basis required)")
    check(set(actual.models) == set(expected.models), "model set differs from v14")
    root_id = actual.models["SweptDart"].props[0]
    surface_points = []
    for name, node in actual.models.items():
        node_id = node.props[0]
        check(label(node) == name, f"binding name {label(node)!r} must be {name!r}")
        check(actual.parents[node_id] == (0 if name == "SweptDart" else root_id),
              f"{name}: unexpected parent")
        check(node.props[2] == (b"Mesh" if name in manifest["meshes"] else b"Null"),
              f"{name}: unexpected model type")
        props = properties(node)
        for field in (b"Lcl Rotation", b"PreRotation", b"PostRotation", b"GeometricRotation",
                      b"RotationOffset", b"RotationPivot", b"ScalingOffset", b"ScalingPivot",
                      b"GeometricTranslation", b"Lcl Scaling", b"GeometricScaling"):
            default = (1, 1, 1) if field.endswith(b"Scaling") else (0, 0, 0)
            check(close(props.get(field, default), default), f"{name}: nonidentity {field.decode()}")
        position = np.array(props.get(b"Lcl Translation", (0, 0, 0))) * UNITY_BASIS
        check(close(position, SOCKETS.get(name, (0, 0, 0))), f"{name}: Unity local position {position}")
        if name in SOCKETS:
            report["unity_socket_positions"][name] = position.tolist()
        if name not in manifest["meshes"] or name not in expected.models:
            continue

        old_id = expected.models[name].props[0]
        vertices, ids, uvs, slots, normals, agreement = triangles(actual.geometry[node_id])
        old_vertices, old_ids, old_uvs, old_slots, old_normals, _ = triangles(expected.geometry[old_id], reverse=True)
        unity_points = vertices * UNITY_BASIS
        # v14 raw vertex coordinates numerically match the intended Unity geometry.
        # Comparing every vertex keeps all three files aligned to the accepted exterior.
        check(close(unity_points, old_vertices), f"{name}: Unity vertices differ from accepted geometry")
        check(np.array_equal(ids, old_ids), f"{name}: triangle winding or topology changed")
        check(list(uvs) == list(old_uvs) and len(uvs) == 4 and
              all(close(uvs[k], old_uvs[k]) for k in old_uvs if k in uvs),
              f"{name}: UV channel or per-corner UV mismatch")
        check(actual.materials[node_id] == manifest["meshes"][name]["materials"]
              and np.array_equal(slots, old_slots), f"{name}: material binding mismatch")
        # Blender recalculates flat normals on the reflected triangles. Compare
        # directions as angles; position tolerances in metres do not apply here.
        lengths = np.linalg.norm(normals, axis=2)
        dots = np.einsum("ijk,ijk->ij", normals * UNITY_BASIS, old_normals)
        cosines = dots / (lengths * np.linalg.norm(old_normals, axis=2))
        deviation = float(np.rad2deg(np.arccos(np.clip(cosines.min(), -1, 1))))
        report["max_normal_delta_degrees"] = max(report["max_normal_delta_degrees"], deviation)
        check(np.allclose(lengths, 1, atol=1e-5, rtol=0) and agreement >= 0.99999 and deviation < 0.05,
              f"{name}: flat normals disagree with face winding or the approved facets")
        report["triangle_count"] += len(ids)
        report["mesh_count"] += 1

        if path.name != FBX_FILES[0]:
            continue
        surface_points.append(unity_points)
        low, high = unity_points.min(axis=0), unity_points.max(axis=0)
        center = (low + high) / 2
        if name == "Hull_Unified":
            check(close(low, (-2, -0.28, -1.64)) and close(high, (2, 0.51, 2.5)),
                  f"hull bounds in Unity {low} to {high}, expected nose +2.5, tail -1.64")
        if name.startswith(("Weapon_Aperture_", "Drive_Light_")):
            sign = -1 if name.endswith("_L") else 1
            wanted = (sign * 0.75, 0, 1.103) if name.startswith("Weapon") else (sign * 0.85, 0, -1.643)
            check(close(center, wanted), f"{name}: Unity aperture/light center {center}, expected {wanted}")
            report["unity_aperture_and_drive_centers"][name] = center.tolist()
    if surface_points:
        points = np.concatenate(surface_points)
        bounds = [points.min(axis=0), points.max(axis=0)]
        check(close(bounds, [[-2, -0.28, -1.644], [2, 0.51, 2.5]]), f"surface bounds in Unity {bounds}")
        report["unity_surface_bounds"] = [v.tolist() for v in bounds]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("export_directory", type=Path, nargs="?", default=REVIEWS / "v15/export")
    parser.add_argument("--report", type=Path, help="Write the hash-bound JSON report after all checks pass")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    manifest = json.loads((BASELINE / "package-manifest.json").read_text(encoding="utf-8"))
    source_hashes = json.loads((BASELINE / "checksums.json").read_text(encoding="utf-8"))
    report = {"passed": False, "scope": "Serialized FBX in the basis measured in Unity 6000.3.24f1 with Bake Axis Conversion on.",
              "fbx_sha256": {}, "mesh_count": 0, "triangle_count": 0, "unity_socket_positions": {},
              "unity_aperture_and_drive_centers": {}, "max_normal_delta_degrees": 0.0}
    errors = []
    for filename in FBX_FILES:
        baseline = BASELINE / filename
        if hashlib.sha256(baseline.read_bytes()).hexdigest() != source_hashes[filename]:
            raise ValueError(f"Unexpected reference file: {baseline}")
        path = args.export_directory / filename
        report["fbx_sha256"][filename] = hashlib.sha256(path.read_bytes()).hexdigest()
        check_file(path, baseline, manifest, report, errors)
    if errors:
        raise ValueError("\n".join(errors))
    report["passed"] = True
    document = json.dumps(report, indent=2) + "\n"
    if args.report:
        args.report.write_text(document, encoding="utf-8")
    print(document)


if __name__ == "__main__":
    main()
