"""Check signed build payload against current Unity-produced Resources files."""
import hashlib
import json
import re
import sys
import zipfile
from pathlib import Path

project = Path(__file__).resolve().parents[3]
apk = Path(sys.argv[1]).resolve()
assets = [
    "Assets/Resources/Configuration/LocalEarningFlow.json",
    "Assets/Resources/Configuration/LocalEarningRewards.json",
    "Assets/Resources/Configuration/LocalWithdrawal.json",
    "Assets/Resources/Configuration/LocalLuckyRewardItem.asset",
    "Assets/Resources/prefabs/panels/LocalCashHistory.prefab",
    "Assets/Resources/prefabs/panels/LocalCashHistoryRow.prefab",
    "Assets/Resources/prefabs/panels/LocalCashHistoryEntry.prefab",
    "Assets/Resources/prefabs/panels/LocalEarningSequence.prefab",
    "Assets/Resources/prefabs/panels/LocalWithdrawalVerification.prefab",
    "Assets/Resources/prefabs/panels/LocalLuckyReward.prefab",
    "Assets/Resources/prefabs/items/LocalLuckyItem.prefab",
]
result = {"apk": str(apk), "resources": []}
with zipfile.ZipFile(apk) as package:
    corrupt = package.testzip()
    if corrupt:
        raise RuntimeError(f"APK CRC failure: {corrupt}")
    index = package.read("assets/bin/Data/globalgamemanagers")
    metadata = package.read("assets/bin/Data/Managed/Metadata/global-metadata.dat")
    required_code = ["LocalCashHistoryPanel", "LocalEarningSequencePanel", "RecoverScrewCompletions", "ResumeInterruptedScrewCompletions"]
    for symbol in required_code:
        if symbol.encode() + b"\0" not in metadata:
            raise RuntimeError(f"Missing updated runtime implementation: {symbol}")
    result["runtime_symbols"] = required_code
    for asset in assets:
        guid = re.search(r"^guid: (\w+)$", (project / (asset + ".meta")).read_text(), re.M)[1]
        compiled = (project / "Library/PlayerDataCache/Android/Data" / guid).read_bytes()
        packed = package.read("assets/bin/Data/" + guid)
        if packed != compiled:
            raise RuntimeError(f"Stale packaged resource: {asset}")
        resource_path = str(Path(asset).relative_to("Assets/Resources").with_suffix("")).replace("\\", "/").lower()
        if resource_path.encode() not in index:
            raise RuntimeError(f"Missing Resources index: {resource_path}")
        result["resources"].append({"asset": asset, "bytes": len(packed), "sha256": hashlib.sha256(packed).hexdigest()})
result.update(status="PASS", apk_crc="PASS", sha256=hashlib.sha256(apk.read_bytes()).hexdigest())
Path(__file__).with_name("apk-resources.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps({"status": "PASS", "resources": len(assets), "crc": "PASS"}))
