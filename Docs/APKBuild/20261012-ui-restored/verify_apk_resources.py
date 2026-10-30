"""Offline CRC and current Unity build-resource verification for the UI-restored APK.

Usage: python verify_apk_resources.py path/to/release.apk
Only the report beside this script is written. No Unity process is started.
"""

import argparse
import hashlib
import json
import re
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path


PREFABS = (
    "Assets/Resources/prefabs/panels/NewbieGuidePanel.prefab",
    "Assets/Resources/prefabs/panels/LocalLuckyReward.prefab",
    "Assets/Resources/prefabs/panels/LocalCashHistory.prefab",
    "Assets/Resources/prefabs/panels/LocalCashHistoryRow.prefab",
    "Assets/Resources/prefabs/panels/LocalSettings.prefab",
    "Assets/Resources/prefabs/panels/LocalEarningSequence.prefab",
)
TEXTURES = ("Assets/Resources/atlas/newbieguidepanel/yellow-screw.png",)
APK_DATA = "assets/bin/Data/"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def asset_guid(asset):
    meta = Path(str(asset) + ".meta")
    match = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", meta.read_text(encoding="utf-8-sig"), re.MULTILINE)
    if not match:
        raise ValueError(f"Missing or invalid Unity GUID: {meta}")
    return match.group(1).lower()


def resources_key(relative):
    return str(Path(relative).relative_to("Assets/Resources").with_suffix("")).replace("\\", "/").lower()


def verify(apk, project, result):
    cache = project / "Library/PlayerDataCache/Android/Data"
    errors = result["errors"]
    inputs = {}
    manifest = Path(__file__).resolve().parent / "ui-inputs.json"
    if manifest.is_file():
        inputs = {item["path"]: item["sha256"].lower() for item in json.loads(manifest.read_text(encoding="utf-8-sig"))}
        result["build_input_manifest"] = str(manifest)
    else:
        result["build_input_manifest"] = "Unavailable; source-to-build-start freshness was not checked."

    with zipfile.ZipFile(apk) as package:
        entries = package.namelist()
        names = set(entries)
        if len(entries) != len(names):
            raise RuntimeError("APK contains duplicate ZIP entry names; resource lookup is ambiguous")
        result["zip_entries"] = len(entries)
        corrupt = package.testzip()
        if corrupt:
            result["apk_crc"] = "FAIL"
            raise RuntimeError(f"APK CRC failure: {corrupt}")
        result["apk_crc"] = "PASS"

        def compare(filename, relative=None, optional=False):
            entry = APK_DATA + filename
            expected_path = cache / filename
            apk_present = entry in names
            cache_present = expected_path.is_file()
            record = {"file": filename, "apk_entry": entry, "cache_file": str(expected_path)}
            if relative:
                record["source_asset"] = relative
            result["resources"].append(record)
            if not apk_present or not cache_present:
                record.update({"apk_present": apk_present, "cache_present": cache_present})
                if optional and not apk_present and not cache_present:
                    record["status"] = "NOT_COMPARED"
                    record["reason"] = "No GUID-named serialized file exists in either APK or build cache; Resources index presence alone does not prove texture payload equality."
                else:
                    record["status"] = "FAIL"
                    errors.append(f"Missing resource {filename}: APK={apk_present}, current build cache={cache_present}")
                return None
            packaged = package.read(entry)
            expected = expected_path.read_bytes()
            record.update({"bytes": len(packaged), "apk_sha256": digest(packaged), "cache_sha256": digest(expected)})
            if packaged != expected:
                record["status"] = "FAIL"
                errors.append(f"Packaged resource differs from current build cache: {relative or filename}")
            else:
                record["status"] = "PASS"
            return packaged

        index = compare("globalgamemanagers")
        if index is None:
            errors.append("Cannot verify Resources index without packaged globalgamemanagers")

        for relative in PREFABS + TEXTURES:
            source = project / relative
            guid = asset_guid(source)
            source_hash = digest(source.read_bytes())
            input_check = {"asset": relative, "guid": guid, "source_sha256": source_hash}
            if relative in inputs:
                input_check["build_start_sha256"] = inputs[relative]
                input_check["status"] = "PASS" if inputs[relative] == source_hash else "FAIL"
                if input_check["status"] == "FAIL":
                    errors.append(f"Source asset changed since build-start manifest: {relative}")
            else:
                input_check["status"] = "NOT_CHECKED"
                input_check["reason"] = "No build-start hash for this asset."
            result["source_inputs"].append(input_check)

            key = resources_key(relative)
            indexed = index is not None and key.encode("utf-8") in index.lower()
            result["resources_index"].append({"path": key, "status": "PASS" if indexed else "FAIL"})
            if not indexed:
                errors.append(f"Missing Resources index entry: {key}")

            compare(guid, relative, optional=relative in TEXTURES)
            # Some Unity texture serialization stores external data beside the GUID
            # file. Compare any such companions present on either side as well.
            companion_names = {name[len(APK_DATA):] for name in names if name.startswith(APK_DATA + guid + ".")}
            companion_names.update(path.name for path in cache.glob(guid + ".*") if path.is_file())
            for filename in sorted(companion_names):
                compare(filename, relative)

        result["texture_payload_scope"] = "Compared GUID-named texture serialized files and any GUID-prefixed companion files found. Atlas/shared texture payloads without those names are not independently mapped by this script."


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path, help="Path to the completed APK")
    args = parser.parse_args()
    script = Path(__file__).resolve()
    project = script.parents[3]
    apk = args.apk.resolve()
    result = {
        "apk": str(apk),
        "project": str(project),
        "verified_at_utc": datetime.now(timezone.utc).isoformat(),
        "status": "FAIL",
        "apk_crc": "NOT_RUN",
        "resources": [],
        "resources_index": [],
        "source_inputs": [],
        "errors": [],
        "verification_scope": "Packaged serialized resource bytes must match the current Android PlayerDataCache. This check does not reconstruct Unity assets from source YAML/PNG or replace runtime visual validation.",
    }
    failure = None
    try:
        if not apk.is_file():
            raise FileNotFoundError(f"APK does not exist: {apk}")
        if not (project / "Assets").is_dir():
            raise RuntimeError(f"Cannot locate Unity project from script path: {project}")
        verify(apk, project, result)
        if result["errors"]:
            raise RuntimeError("APK resource verification failed:\n" + "\n".join(result["errors"]))
        result["status"] = "PASS"
    except Exception as exc:
        failure = exc
        if not result["errors"]:
            result["errors"].append(f"{type(exc).__name__}: {exc}")
        result["failure"] = f"{type(exc).__name__}: {exc}"
    finally:
        report = script.parent / "apk-resources.json"
        report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(json.dumps({"status": result["status"], "apk_crc": result["apk_crc"], "resource_files": len(result["resources"]), "report": str(report)}, ensure_ascii=False))
    if failure is not None:
        raise failure


if __name__ == "__main__":
    main()
