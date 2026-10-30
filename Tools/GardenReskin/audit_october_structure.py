"""Independently compare the complete October skin against its exact baseline.

Read-only audit. This does not trust implementation scripts' reports. It checks
every baseline Prefab/Scene, all runtime C# source, config files, component IDs,
all transforms, live text, interaction fields, and newly referenced GUIDs.
"""
from pathlib import Path
import collections
import difflib
import hashlib
import json
import re
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'Docs/GardenReskin/implementation-20261008'
BASELINE = WORK / 'before-implementation.zip'
VISUAL_FIELDS = {
    'm_Sprite', 'm_Material', 'm_Color', 'm_fontColor', 'm_sharedMaterial',
    'm_fontAsset', 'm_Type', 'm_PixelsPerUnitMultiplier', 'm_PreserveAspect',
    'm_margin', 'm_fontSize', 'm_fontSizeBase', 'm_fontSizeMin', 'm_fontSizeMax',
    'm_enableAutoSizing', 'm_fontStyle', 'm_enableWordWrapping',
    # Existing paths are configuration for artwork loaded into existing Images.
    'selectedSpritePath', 'normalSpritePath', 'filledMarkerPath', 'emptyMarkerPath',
}
UNLOCK = 'Assets/NutSort/Runtime/UI/OriginalGameplayUnlockPanel.cs'
OLD_UNLOCK_LINE = '            backdrop.color=new Color(0,0,0,settings.BackdropAlpha);'
NEW_UNLOCK_LINES = ('            // The Prefab owns its backdrop tint; the panel settings own opacity.\n'
                    '            var backdropTint=backdrop.color;backdropTint.a=settings.BackdropAlpha;backdrop.color=backdropTint;')
APPROVED_RUNTIME_PATCHES = {
    UNLOCK: [(OLD_UNLOCK_LINE, NEW_UNLOCK_LINES)],
    # The all-pages runtime regression exposed an existing scene teardown bug.
    # Root authorized this exact cross-platform fix; no Editor-only bypass.
    'Assets/NutSort/Runtime/World/OriginalPrefabPool.cs': [
        ('        private readonly Dictionary<GameObject, Entry> rented = new Dictionary<GameObject, Entry>();',
         '        private readonly Dictionary<GameObject, Entry> rented = new Dictionary<GameObject, Entry>();\n'
         '        private bool shuttingDown;\n\n'
         "        // Scene teardown still releases the level's visual bindings, but its\n"
         '        // pooled objects must remain in the hierarchy Unity is already destroying.\n'
         '        public void BeginShutdown() => shuttingDown = true;\n'
         '        private void OnDestroy() => BeginShutdown();'),
        ('        public GameObject Rent(string resourcePath, Transform parent)\n        {',
         '        public GameObject Rent(string resourcePath, Transform parent)\n        {\n'
         '            if (shuttingDown) throw new InvalidOperationException("Cannot rent a prefab while its pool is shutting down.");'),
        ('            instance.SetActive(false);\n            instance.transform.SetParent(transform, false);',
         '            instance.SetActive(false);\n            if (shuttingDown) return;\n            instance.transform.SetParent(transform, false);'),
    ],
    'Assets/NutSort/Runtime/World/OriginalGameScene.cs': [
        ('        private void OnDestroy()\n        {\n            pendingInitializations.Clear();',
         '        private void OnDestroy()\n        {\n            pool?.BeginShutdown();\n            pendingInitializations.Clear();'),
    ],
    # Restore the original first-level teaching presentation: the existing
    # teaching markers and normal milestones must not occupy the same HUD area.
    # This changes existing-node visibility only, never progression or rewards.
    'Assets/NutSort/Runtime/UI/LocalGameplayController.Top.cs': [
        ('            rewardsTop.Coin.gameObject.SetActive(true);',
         '            bool teaching=game.User.Level<2&&!game.NewLevelMode;\n'
         '            rewardsTop.Coin.gameObject.SetActive(!teaching);'),
        ('            rewardsTop.Hidden.Refresh();',
         '            rewardsTop.Hidden.Refresh();\n'
         '            // The teaching progress uses this same HUD area; its existing nodes\n'
         '            // take precedence until normal level presentation resumes.\n'
         '            if(teaching)rewardsTop.Hidden.gameObject.SetActive(false);'),
    ],
}

def digest(data): return hashlib.sha256(data).hexdigest()
def decode(data): return data.decode('utf-8-sig').replace('\r\n', '\n')

def blocks(text):
    result = {}
    for block in re.split(r'(?=^--- !u!)', text, flags=re.M):
        header = re.match(r'--- !u!(\d+) &(-?\d+)', block)
        if header:
            key = (header[1], header[2])
            if key in result: raise ValueError('Duplicate serialized object ID ' + str(key))
            result[key] = block
    return result

def normalized_visuals(text):
    output = []
    for block in re.split(r'(?=^--- !u!)', text, flags=re.M):
        # Never remove anything from Transform, RectTransform, GameObject or
        # PrefabInstance records; every byte in these must match the baseline.
        if not re.match(r'--- !u!114 &', block):
            output.append(block); continue
        for key in VISUAL_FIELDS:
            block = re.sub(r'(?m)^  ' + re.escape(key) + r': .*$', '  ' + key + ': <VISUAL>', block)
        # Original main-progress component has a serialized array of 3 Sprite
        # states. Mask each external Sprite reference but preserve element count,
        # order, fileID type, and all component/UI object bindings above it.
        if 'guid: 42e2d4fb8c04460c951808a58c59ec81' in block:
            match = re.search(r'(?ms)^  backgrounds:\n(?:  - \{fileID: 21300000, guid: [0-9a-f]{32}, type: [23]\}\n)+', block)
            if match:
                value = re.sub(r'guid: [0-9a-f]{32}, type: [23]', 'guid: <SPRITE>, type: <EXTERNAL>', match[0])
                block = block[:match.start()] + value + block[match.end():]
            for key in ('pendingDot', 'passedDot'):
                block = re.sub(r'(?m)^(  ' + key + r': \{fileID: 21300000, )guid: [0-9a-f]{32}, type: [23]\}', r'\1guid: <SPRITE>, type: <EXTERNAL>}', block)
        block = re.sub(r'(  m_fontColor32:\n    serializedVersion: 2\n    rgba: )\d+', r'\1<VISUAL>', block)
        output.append(block)
    return ''.join(output)

def live_text(text):
    # Keep the whole (possibly multi-line) serialized YAML field.
    return re.findall(r'(?ms)^  m_text:.*?(?=^  [A-Za-z_]\w*:|^--- !u!|\Z)', text)

def interactions(text):
    names = ('m_RaycastTarget', 'm_TargetGraphic', 'm_Interactable', 'm_RaycastPadding',
             'm_BlocksRaycasts', 'm_Interactable', 'm_IgnoreParentGroups')
    result = {}
    for key, block in blocks(text).items():
        selected = []
        for name in names:
            selected += re.findall(r'(?m)^  ' + name + r': .*$', block)
        # Entire Button and EventSystem behavior blocks must remain unchanged.
        if '  m_OnClick:' in block or '  m_DragThreshold:' in block:
            selected.append(block)
        if selected: result[':'.join(key)] = selected
    return result

def audit():
    errors = []; warnings = []; files = []; missing = []
    # Scan real Assets meta files, not Library/importer cache.
    guids = collections.defaultdict(list)
    for p in (ROOT / 'Assets').rglob('*.meta'):
        match = re.search(r'(?m)^guid: ([a-fA-F0-9]{32})$', p.read_text(encoding='utf-8-sig', errors='replace'))
        if match: guids[match[1]].append(p.with_suffix('').relative_to(ROOT).as_posix())
    stats = collections.Counter()
    with zipfile.ZipFile(BASELINE) as z:
        baseline_names = set(z.namelist())
        for rel in sorted(baseline_names):
            p = ROOT / rel
            if not p.exists():
                errors.append({'file': rel, 'reason': 'Baseline file deleted'}); continue
            old_bytes = z.read(rel); new_bytes = p.read_bytes()
            ext = p.suffix.lower()
            if ext in ('.prefab', '.unity'):
                stats['prefabs' if ext == '.prefab' else 'scenes'] += 1
                before = decode(old_bytes); after = decode(new_bytes)
                b = blocks(before); a = blocks(after)
                checks = {
                    'component_ids_and_types': list(b) == list(a),
                    'hierarchy_and_transforms': {k: v for k, v in b.items() if k[0] in ('1','4','224','1001')} ==
                                               {k: v for k, v in a.items() if k[0] in ('1','4','224','1001')},
                    'dynamic_text_strings': live_text(before) == live_text(after),
                    'button_raycast_targetGraphic_bindings': interactions(before) == interactions(after),
                    'only_allowlisted_visual_fields': normalized_visuals(before) == normalized_visuals(after),
                }
                if before != after:
                    stats['changed_prefabs_and_scenes'] += 1
                    fields = collections.Counter()
                    diff = list(difflib.unified_diff(before.splitlines(), after.splitlines()))
                    for line in diff:
                        if line.startswith(('+','-')) and not line.startswith(('+++','---')):
                            match = re.search(r'^[-+]\s*(\w+):', line)
                            if match: fields[match[1]] += 1
                    files.append({'file': rel, 'sha256': digest(new_bytes), 'checks': checks, 'changed_fields': dict(fields)})
                for name, passed in checks.items():
                    if not passed: errors.append({'file': rel, 'reason': name, 'diff': list(difflib.unified_diff(normalized_visuals(before).splitlines(), normalized_visuals(after).splitlines()))[:100]})
                # Resolve any new sprite/material reference introduced by skin.
                new_refs = set(re.findall(r'guid: ([0-9a-f]{32})', after)) - set(re.findall(r'guid: ([0-9a-f]{32})', before))
                for guid in sorted(new_refs):
                    matches = guids.get(guid, [])
                    if len(matches) != 1:
                        errors.append({'file': rel, 'reason': 'New asset GUID missing or ambiguous', 'guid': guid, 'assets': matches})
                    elif not (ROOT / matches[0]).exists():
                        errors.append({'file': rel, 'reason': 'New asset metadata has no asset file', 'asset': matches[0]})
                # TMP stores a float color and byte color. Record inconsistency
                # separately from structural invariants; runtime rendering is QA.
                for key, block in a.items():
                    if key not in b or b[key] == block or '  m_fontColor:' not in block: continue
                    rgba = re.search(r'  m_fontColor: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}', block)
                    packed = re.search(r'  m_fontColor32:\n    serializedVersion: 2\n    rgba: (\d+)', block)
                    if rgba and packed:
                        expected = sum(round(float(v)*255) << (8*i) for i,v in enumerate(rgba.groups()))
                        if expected != int(packed[1]): warnings.append({'file': rel, 'component': key[1], 'reason': 'TMP float/byte color differ', 'expected': expected, 'stored': int(packed[1])})
            elif '/Runtime/' in rel and ext in ('.cs', '.asmdef', '.asmref'):
                stats['runtime_files'] += 1
                before = decode(old_bytes); after = decode(new_bytes)
                if before != after:
                    expected = before
                    exact_patches = APPROVED_RUNTIME_PATCHES.get(rel, [])
                    for old, new in exact_patches:
                        if expected.count(old) != 1:
                            errors.append({'file': rel, 'reason': 'Authorized runtime patch baseline is ambiguous or absent'})
                        expected = expected.replace(old, new)
                    allowed = bool(exact_patches) and expected == after
                    files.append({'file': rel, 'sha256': digest(new_bytes), 'only_exact_authorized_runtime_change': allowed})
                    if not allowed: errors.append({'file': rel, 'reason': 'Unexpected runtime source change', 'diff': list(difflib.unified_diff(before.splitlines(), after.splitlines()))[:100]})
            elif '/Configuration/' in rel or rel.startswith('ProjectSettings/'):
                stats['configuration_files'] += 1
                if old_bytes != new_bytes:
                    # Ignore line-ending-only serialization.
                    try: same = decode(old_bytes) == decode(new_bytes)
                    except UnicodeError: same = False
                    if not same: errors.append({'file': rel, 'reason': 'Economy/game/settings configuration changed'})
        for p in (ROOT / 'Assets/NutSort/Runtime').rglob('*'):
            if p.is_file() and p.suffix in ('.cs', '.asmdef', '.asmref') and p.relative_to(ROOT).as_posix() not in baseline_names:
                errors.append({'file': p.relative_to(ROOT).as_posix(), 'reason': 'New runtime file outside approved skin scope'})
    for guid, paths in guids.items():
        if len(paths) > 1 and any('/octobergarden/' in p for p in paths):
            errors.append({'guid': guid, 'assets': paths, 'reason': 'New skin duplicate GUID'})
    report = {'status': 'PASS' if not errors else 'FAIL', 'baseline': str(BASELINE),
              'scope': dict(stats), 'checks': 'All baseline Prefabs/Scenes inspected independently; C# production code and configuration checked against backup.',
              'files': files, 'errors': errors, 'warnings': warnings,
              'limitations': 'This is structural validation, not a screenshot or functional runtime assertion. Editor validation scripts are intentionally excluded.'}
    (WORK / 'global-structure-audit.json').write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf8')
    print(json.dumps({'status': report['status'], 'scope': dict(stats), 'errors': len(errors), 'warnings': len(warnings)}, ensure_ascii=False))
    if errors:
        print(json.dumps(errors[:8], ensure_ascii=False, indent=2)); return 1
    return 0

if __name__ == '__main__': sys.exit(audit())
