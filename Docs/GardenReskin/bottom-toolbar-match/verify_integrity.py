"""Read-only Assets audit for the bottom toolbar correction; writes this audit's report."""
import hashlib
import json
import re
import struct
import zipfile
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SUFFIXES = {'.prefab', '.unity', '.cs', '.meta', '.asset', '.mat', '.png'}
IMAGE_ID = '114450063812975771'
TEXTURE_GUID = '0513d34314154e4297b1d9f4ab2ffd98'
SPRITE_GUID = '9fa45da9c8c5439bb43c8de5dfc8ac0c'
ART = 'Assets/Resources/atlas/mainpanel/'
PNG_PATHS = {ART + name + '.png' for name in ('item_bg', 'item_count_bg', 'item_add')}
PREFAB_PATHS = {'Assets/Resources/prefabs/panels/' + name + '.prefab'
                for name in ('MainPanel', 'MainPanelBottom', 'MainPanelComplete')}
ADDED_PATHS = {ART + 'replay_bg' + suffix for suffix in ('.png', '.png.meta', '.asset', '.asset.meta')}
BLOCKS = re.compile(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', re.M | re.S)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read(path):
    return (ROOT / path).read_bytes().decode('utf-8')


def dimensions(data):
    assert data[:8] == b'\x89PNG\r\n\x1a\n', 'Invalid PNG signature'
    return list(struct.unpack('>II', data[16:24]))


def image_block(text):
    matches = [m for m in BLOCKS.finditer(text) if m[2] == IMAGE_ID]
    assert len(matches) == 1 and matches[0][1] == '114'
    match = matches[0]
    assert 'guid: fe87c0e1cc204ed48ad3b37840f39efc' in match[3]
    go_id = re.search(r'm_GameObject: \{fileID: (\d+)\}', match[3])[1]
    go = next(m for m in BLOCKS.finditer(text) if m[2] == go_id)
    assert re.search(r'^  m_Name: ReplayBg\r?$', go[3], re.M)
    return match


def field(body, name):
    matches = re.findall(r'^  ' + name + r': [^\r\n]*', body, re.M)
    assert len(matches) == 1
    return matches[0]


def main():
    baseline = json.loads((HERE / 'before-hashes.json').read_text(encoding='utf-8'))
    before = baseline['files']
    config = json.loads((HERE / 'replay-background-config-audit.json').read_text(encoding='utf-8'))
    config_rows = {row['path']: row for row in config['prefabs']}
    current = {}
    for path in sorted((ROOT / 'Assets').rglob('*')):
        if path.is_file() and path.suffix.lower() in SUFFIXES:
            data = path.read_bytes()
            current[path.relative_to(ROOT).as_posix()] = {'bytes': len(data), 'sha256': sha(data)}
    changed = sorted(p for p in before.keys() & current.keys() if before[p]['sha256'] != current[p]['sha256'])
    added = sorted(current.keys() - before.keys())
    removed = sorted(before.keys() - current.keys())
    failures = []
    if set(changed) != PNG_PATHS | PREFAB_PATHS:
        failures.append('Changed paths do not exactly equal the six allowed existing files.')
    if set(added) != ADDED_PATHS:
        failures.append('Added paths do not exactly equal the four allowed new assets.')
    if removed:
        failures.append('Existing files were removed.')
    prefabs, pngs = [], []
    # Recover only the two previous Image field values from the original archive.
    # Reconstructing each current file with those values must hash to THIS stage's
    # baseline, proving every other byte (including later fixes) was preserved.
    with zipfile.ZipFile(ROOT / 'Docs/GardenReskin/OriginalAssets/before-reskin.zip') as archive:
        for path in sorted(PREFAB_PATHS):
            text = read(path)
            match = image_block(text)
            source = image_block(archive.read(path).decode('utf-8'))
            fields = {}
            body = match[3]
            for name in ('m_Color', 'm_Sprite'):
                original, final = field(source[3], name), field(body, name)
                fields[name] = {'before': original.split(': ', 1)[1], 'after': final.split(': ', 1)[1]}
                body = body.replace(final, original, 1)
            reconstructed = text[:match.start(3)] + body + text[match.end(3):]
            recovered_hash = sha(reconstructed.encode('utf-8'))
            row = config_rows[path]
            exact = recovered_hash == before[path]['sha256']
            audit_matches = row['beforeSha256'] == before[path]['sha256'] and row['afterSha256'] == current[path]['sha256']
            expected_fields = fields['m_Color']['after'] == '{r: 1, g: 1, b: 1, a: 1}' and fields['m_Sprite']['after'] == '{fileID: 21300000, guid: ' + SPRITE_GUID + ', type: 2}'
            if not (exact and audit_matches and expected_fields):
                failures.append('Prefab field-only or audit hash verification failed: ' + path)
            counts = Counter(m[1] for m in BLOCKS.finditer(text))
            prefabs.append({'path': path, 'imageFileID': IMAGE_ID, 'gameObject': 'ReplayBg',
                            'fields': fields, 'beforeSha256': before[path]['sha256'],
                            'afterSha256': current[path]['sha256'], 'reconstructedBeforeSha256': recovered_hash,
                            'onlyTwoAllowedFieldsChanged': exact, 'configAuditHashesMatch': audit_matches,
                            'allOtherSerializedBytesUnchanged': exact,
                            'hierarchyComponentsRectTransformsButtonsBindingsUnchanged': exact,
                            'gameObjects': counts['1'], 'rectTransforms': counts['224'],
                            'monoBehaviours': counts['114'],
                            'standardButtons': text.count('guid: 4e29b1a8efbd4b44bb3f3716e73f07ff')})
        for path in sorted(PNG_PATHS):
            size = dimensions((ROOT / path).read_bytes())
            original_size = dimensions(archive.read(path))
            if size != original_size:
                failures.append('Original PNG dimensions changed: ' + path)
            pngs.append({'path': path, 'size': size, 'originalSize': original_size,
                         'dimensionsPreserved': size == original_size,
                         'beforeSha256': before[path]['sha256'], 'afterSha256': current[path]['sha256']})
    guid_paths = defaultdict(list)
    for path in sorted((ROOT / 'Assets').rglob('*.meta')):
        match = re.search(r'^guid: ([0-9a-f]{32})\r?$', path.read_text(encoding='utf-8-sig'), re.M)
        if match:
            guid_paths[match[1]].append(path.relative_to(ROOT).as_posix())
    sprite = read(ART + 'replay_bg.asset')
    new_size = dimensions((ROOT / (ART + 'replay_bg.png')).read_bytes())
    references = {
        'textureGuid': TEXTURE_GUID, 'textureGuidOwners': guid_paths[TEXTURE_GUID],
        'spriteGuid': SPRITE_GUID, 'spriteGuidOwners': guid_paths[SPRITE_GUID],
        'newGuidsUnique': guid_paths[TEXTURE_GUID] == [ART + 'replay_bg.png.meta'] and guid_paths[SPRITE_GUID] == [ART + 'replay_bg.asset.meta'],
        'spriteFileID21300000Exists': bool(re.search(r'^--- !u!213 &21300000\r?$', sprite, re.M)),
        'spriteTextureReferencesResolve': set(re.findall(r'guid: ([0-9a-f]{32})', sprite)) == {TEXTURE_GUID},
        'spriteTextureFileID': bool(re.search(r'texture: \{fileID: 2800000, guid: ' + TEXTURE_GUID + r', type: 3\}', sprite)),
        'spriteRectMatchesPng': bool(re.search(r'm_Rect:\s+serializedVersion: 2\s+x: 0\s+y: 0\s+width: 85\s+height: 85', sprite)),
        'pngSize': new_size, 'png85x85': new_size == [85, 85],
        'prefabSpriteReferencesResolve': all(row['fields']['m_Sprite']['after'] == '{fileID: 21300000, guid: ' + SPRITE_GUID + ', type: 2}' for row in prefabs),
    }
    if not all(v for v in references.values() if isinstance(v, bool)):
        failures.append('A new asset GUID, fileID, or PNG size check failed.')
    unchanged = sorted(p for p in before.keys() & current.keys() if before[p]['sha256'] == current[p]['sha256'])
    unchanged_counts = dict(sorted(Counter(Path(p).suffix for p in unchanged).items()))
    script_paths = [p for p in before if p.endswith('.cs')]
    all_scripts_unchanged = all(p in current and before[p]['sha256'] == current[p]['sha256'] for p in script_paths)
    report = {
        'status': 'PASS' if not failures else 'FAIL', 'generatedUtc': datetime.now(timezone.utc).isoformat(),
        'scope': 'Bottom toolbar correction only; all previous runtime UI click fixes are part of the baseline.',
        'baseline': 'Docs/GardenReskin/bottom-toolbar-match/before-hashes.json',
        'baselineSha256': sha((HERE / 'before-hashes.json').read_bytes()),
        'baselineFileCount': len(before), 'currentFileCount': len(current),
        'changedExistingCount': len(changed), 'addedCount': len(added), 'removedCount': len(removed),
        'unchangedExistingCount': len(unchanged), 'unchangedByExtension': unchanged_counts,
        'changedExisting': changed, 'added': [{'path': p, **current[p]} for p in added], 'removed': removed,
        'allExistingScriptsUnchanged': all_scripts_unchanged, 'existingScriptCount': len(script_paths),
        'existingUIInputFixAndValidationTestPreserved': all_scripts_unchanged,
        'existingPngChecks': pngs, 'prefabChecks': prefabs, 'newAssetReferenceChecks': references,
        'failures': failures,
        'verificationMethod': 'SHA-256 every scoped asset; reconstruct the three pre-edit Prefabs by replacing only the specified Image fields and require exact stage-baseline SHA-256; resolve new asset GUIDs across all Assets metadata.',
        'limitations': 'Static integrity verification only. This audit does not start or control Unity and does not establish runtime appearance or interaction test results.',
    }
    (HERE / 'integrity-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: report[key] for key in ('status', 'baselineFileCount', 'currentFileCount', 'changedExistingCount', 'addedCount', 'removedCount', 'unchangedExistingCount', 'failures')}, ensure_ascii=False))
    raise SystemExit(0 if not failures else 1)


if __name__ == '__main__':
    main()
