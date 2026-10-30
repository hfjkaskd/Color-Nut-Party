"""Capture/compare the pre-reskin project without opening Unity.

No existing baseline is overwritten. Unity hierarchy, transforms, scripts,
events, text, and import metadata remain protected; only serialized visual
colors/font/sprite/material references are omitted from presentation hashes.
"""
import argparse
import hashlib
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "Docs/GardenReskin/validation-baseline.json"
TEXTURES = {".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".exr"}
VISUAL_KEYS = {
    "m_Color", "m_Colors", "m_FontColor", "m_fontColor", "m_fontColor32",
    "m_enableVertexGradient", "m_fontColorGradient", "m_fontColorGradientPreset",
    "m_Sprite", "m_Material", "m_fontAsset", "m_sharedMaterial", "m_FontData",
    "m_OutlineColor", "m_EffectColor", "m_CaretColor", "m_SelectionColor",
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def without_visual_fields(data):
    """Skip complete indented Unity YAML fields, leaving hierarchy exact."""
    lines = data.decode("utf-8-sig").replace("\r\n", "\n").splitlines(True)
    output, skip_indent, class_id = [], None, None
    for line in lines:
        document = re.match(r"--- !u!(\d+) &", line)
        if document:
            class_id = int(document.group(1))
        indent = len(line) - len(line.lstrip(" "))
        if skip_indent is not None:
            if indent > skip_indent:
                continue
            skip_indent = None
        field = re.match(r"\s+([^ :]+):", line)
        # Never normalize GameObject or either Transform type.
        if class_id not in (1, 4, 224) and field and field.group(1) in VISUAL_KEYS:
            skip_indent = indent
            continue
        output.append(line)
    return "".join(output).encode("utf-8")


def inventory(root):
    result = {}
    for path in sorted((root / "Assets").rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(root).as_posix()
        suffix = path.suffix.lower()
        # Verification sources are intentionally outside the original game.
        if relative.startswith("Assets/NutSort/Editor/GardenReskin/"):
            continue
        kind = None
        if suffix in (".prefab", ".unity"):
            kind = "structure"
        elif suffix == ".mat":
            kind = "material"
        elif suffix in TEXTURES:
            kind = "texture"
        elif suffix == ".meta":
            kind = "metadata"
        elif suffix in (".cs", ".asmdef", ".asset", ".json", ".csv", ".bytes", ".shader", ".hlsl", ".anim", ".controller"):
            kind = "protected"
        if kind is None:
            continue
        data = path.read_bytes()
        record = {"kind": kind, "bytes": len(data), "sha256": sha(data)}
        if kind == "structure":
            record["structureSha256"] = sha(without_visual_fields(data))
            record["objects"] = len(re.findall(rb"^--- !u!", data, re.M))
        result[relative] = record
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("baseline", "check"))
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--baseline", type=Path, default=DEFAULT)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    current = inventory(args.root)
    if args.mode == "baseline":
        args.baseline.parent.mkdir(parents=True, exist_ok=True)
        # Exclusive create is deliberate: a second run cannot bless changes.
        with args.baseline.open("x", encoding="utf-8") as stream:
            json.dump({"schema": 1, "createdUtc": datetime.now(timezone.utc).isoformat(),
                       "root": str(args.root.resolve()), "files": current}, stream, indent=2)
        print(f"Captured {len(current)} files to {args.baseline}")
        return 0
    original = json.loads(args.baseline.read_text(encoding="utf-8"))["files"]
    missing, changed, forbidden = [], [], []
    for name, old in original.items():
        new = current.get(name)
        if new is None:
            missing.append(name)
            continue
        if new["sha256"] == old["sha256"]:
            continue
        changed.append({"path": name, "kind": old["kind"]})
        if old["kind"] in ("metadata", "protected"):
            forbidden.append({"path": name, "reason": "protected content changed"})
        elif old["kind"] == "structure" and old["structureSha256"] != new["structureSha256"]:
            forbidden.append({"path": name, "reason": "non-presentation prefab/scene content changed"})
    report = {"schema": 1, "checkedUtc": datetime.now(timezone.utc).isoformat(),
              "passed": not missing and not forbidden,
              "baselineFiles": len(original), "currentFiles": len(current),
              "missing": missing, "forbiddenChanges": forbidden, "changed": changed,
              "newFiles": sorted(set(current) - set(original)),
              "limits": "Material/texture changes require visual review. New files are listed for review. Color and sprite/font/material presentation fields are allowed; everything else in prefabs/scenes is protected."}
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2), encoding="utf-8")
    counts = {}
    for item in changed:
        counts[item["kind"]] = counts.get(item["kind"], 0) + 1
    print(json.dumps({"passed": report["passed"], "baselineFiles": len(original),
                      "changedByKind": counts, "missing": missing,
                      "forbiddenChanges": forbidden, "newFiles": len(report["newFiles"])}, indent=2))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
