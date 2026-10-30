"""Verify current Unity resource bytes are present in the delivered APK."""
import hashlib
import json
import sys
import zipfile
from pathlib import Path

project = Path(__file__).resolve().parents[3]
apk = Path(sys.argv[1])
resource_files = {
    "settings_prefab": "2a3742a14875f8440bef51f34d49ff62",
    "diamond_progress_fill": "b5bf4b18c1b947278ed1038b8faac3bb",
    "resources_index": "globalgamemanagers",
}
result = {"apk": str(apk), "resources": []}
with zipfile.ZipFile(apk) as package:
    corrupt = package.testzip()
    if corrupt:
        raise RuntimeError(f"APK CRC failure: {corrupt}")
    for label, filename in resource_files.items():
        packaged = package.read("assets/bin/Data/" + filename)
        expected = (project / "Library/PlayerDataCache/Android/Data" / filename).read_bytes()
        if packaged != expected:
            raise RuntimeError(f"Packaged resource differs from current build: {label}")
        result["resources"].append({"resource": label, "bytes": len(packaged), "sha256": hashlib.sha256(packaged).hexdigest()})
    index = package.read("assets/bin/Data/globalgamemanagers")
    for path in (b"prefabs/panels/localsettings", b"atlas/octobergarden/diamond-progress-fill"):
        if path not in index:
            raise RuntimeError(f"Missing Resources index entry: {path!r}")
result["status"] = "PASS"
result["apk_crc"] = "PASS"
(Path(__file__).parent / "apk-resources.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result, indent=2))
