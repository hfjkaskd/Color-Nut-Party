"""Apply the approved garden palette to existing serialized colour fields only.

Run from the Unity project root. PNG generation is handled independently.
This script preserves object IDs, components, hierarchy, text, fonts and bindings.
"""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / 'Docs/GardenReskin'
INVENTORY = json.loads((REPORT / 'palette-inventory.json').read_text(encoding='utf-8'))
BODY = (77 / 255, 47 / 255, 28 / 255)
HINT_BODY = (23 / 255, 19 / 255, 57 / 255)


def colour(rgb, alpha=1):
    return '{' + ', '.join(f'{k}: {v:.8g}' for k, v in zip('rgba', (*rgb, alpha))) + '}'


def rgba32(rgb, alpha):
    values = [round(v * 255) for v in (*rgb, alpha)]
    return sum(v << (8 * i) for i, v in enumerate(values))


def values(raw):
    return tuple(float(v) for v in re.findall(r'[rgba]: ([^,}]+)', raw))


changes = []
files = {}
before = {}


def read(path):
    path = ROOT / path
    if path not in files:
        raw = path.read_bytes()
        before[path] = raw
        files[path] = raw.decode('utf-8-sig')
    return path, files[path]


def patch_component(row, field, rgb, alpha):
    path, text = read(row['file'])
    pattern = rf'(^--- !u!114 &{row["id"]}[^\n]*\n)(.*?)(?=^--- !u!|\Z)'
    match = re.search(pattern, text, re.M | re.S)
    assert match, (path, row['id'])
    block = match[2]
    replacement = re.sub(rf'(^  {field}: )\{{[^\n]+\}}', lambda m: m[1] + colour(rgb, alpha), block, count=1, flags=re.M)
    if field == 'm_fontColor':
        replacement = re.sub(r'(  m_fontColor32:\r?\n    serializedVersion: 2\r?\n    rgba: )\d+', lambda m: m[1] + str(rgba32(rgb, alpha)), replacement, count=1)
    if replacement != block:
        files[path] = text[:match.start(2)] + replacement + text[match.end(2):]
        changes.append({'file': path.relative_to(ROOT).as_posix(), 'component': row['id'], 'path': row['path'], 'field': field, 'before': row.get(field), 'after': colour(rgb, alpha)})


for row in INVENTORY:
    path = Path(row['file'])
    if path.suffix != '.prefab':
        continue
    if 'm_fontColor' in row and row.get('m_sharedMaterial', '').endswith('zh_FZCYT SDF Material.mat'):
        r, g, b, a = values(row['m_fontColor'])
        # Preserve semantic red reward amounts, diamond purple, and PlayerInfo
        # category colours; preserve white text on green/brown title strips.
        dark_neutral = max(r, g, b) < .65 and max(r, g, b) - min(r, g, b) < .035
        blue_body = b > r and b >= g and r < .5
        on_cream = row['path'] in {
            'SuccessPanel/main/label_88',
            'TXUserInfoPanel/main/label_108',
            'TXCoinPanel/main/label_164',
            'LocalGameplay/Result/Card/Message',
        }
        excluded = path.name == 'PlayerInfo.prefab' or '/GM/' in row['path'] or path.name == 'GMButton.prefab'
        if row['path'] == 'NewbieGuidePanel/Pos/Tip/Value':
            # The generated teaching banner is dark wood, unlike cream panels.
            patch_component(row, 'm_fontColor', (1, 245 / 255, 217 / 255), a)
        elif re.search(r'(?:^|/)(?:GoldItem|CoinItem)/Hint/Text \(TMP\)$', row['path']):
            # Match the approved near-white currency hint strips. Rich-text
            # reward amounts keep their existing explicit red colour tags.
            patch_component(row, 'm_fontColor', HINT_BODY, a)
        elif not excluded and (dark_neutral or blue_body or on_cream):
            patch_component(row, 'm_fontColor', BODY, a)
    if row['path'] == 'LocalGameplay/Result/Card':
        patch_component(row, 'm_Color', (250 / 255, 237 / 255, 215 / 255), 1)
    if row['path'] == 'LocalGameplay/Result/Card/Dismiss':
        patch_component(row, 'm_Color', (73 / 255, 125 / 255, 49 / 255), 1)


outlines = {
    '': '#57351F',
    '_b': '#57351F',
    '_blue': '#57351F',
    '_blue1': '#57351F',
    '_blue2': '#57351F',
    '_brown': '#694026',
    '_gray': '#595144',
    '_green': '#28582B',
    '_green1': '#28582B',
    '_orange': '#913C12',
    '_pink': '#694026',
    # Keep the diamond's colour cue while increasing contrast.
    '_purple': '#613B70',
    '_red_b': '#99442C',
}
for suffix, hex_colour in outlines.items():
    rgb = tuple(int(hex_colour[i:i+2], 16) / 255 for i in (1, 3, 5))
    path, text = read(f'Assets/Material/zh_Custom SDF_outline{suffix}.mat')
    old = re.search(r'_OutlineColor: (\{[^\n]+\})', text)[1]
    files[path] = re.sub(r'(_OutlineColor: )\{[^\n]+\}', lambda m: m[1] + colour(rgb), text, count=1)
    if files[path] != text:
        changes.append({'file': path.relative_to(ROOT).as_posix(), 'field': '_OutlineColor', 'before': old, 'after': colour(rgb)})


# The existing Gamma-space scene uses ambient intensity 5 and main light 1.2.
# These lower warm albedos retain shading after removing the old metallic base.
for name, rgb in [('DiZuoMaterial', (0.20, 0.17, 0.13)), ('DiZuoLockedMaterial', (0.12, 0.105, 0.082))]:
    path, text = read(f'Assets/Material/{name}.mat')
    for field in ('_BaseColor', '_Color'):
        old = re.search(rf'{field}: (\{{[^\n]+\}})', text)[1]
        text = re.sub(rf'({field}: )\{{[^\n]+\}}', lambda m: m[1] + colour(rgb), text, count=1)
        changes.append({'file': path.relative_to(ROOT).as_posix(), 'field': field, 'before': old, 'after': colour(rgb)})
    old = re.search(r'_Metallic: ([^\r\n]+)', text)[1]
    text = re.sub(r'(_Metallic: )[^\r\n]+', r'\g<1>0', text, count=1)
    changes.append({'file': path.relative_to(ROOT).as_posix(), 'field': '_Metallic', 'before': old, 'after': '0'})
    files[path] = text


def normalise(serialized):
    # Removing only approved visual fields must make the file byte-identical.
    serialized = re.sub(r'^(  m_fontColor: |  m_Color: |[ \t-]+_OutlineColor: |[ \t-]+_BaseColor: |[ \t-]+_Color: |[ \t-]+_Metallic: )[^\r\n]*', r'\1<COLOUR>', serialized, flags=re.M)
    serialized = re.sub(r'(  m_fontColor32:\r?\n    serializedVersion: 2\r?\n    rgba: )\d+', r'\1<COLOUR32>', serialized)
    return serialized


changed = []
for path, text in files.items():
    original = before[path].decode('utf-8-sig')
    assert normalise(original) == normalise(text), f'Non-palette edit: {path}'
    if original != text:
        encoded = text.encode('utf-8')
        if before[path].startswith(b'\xef\xbb\xbf'):
            encoded = b'\xef\xbb\xbf' + encoded
        path.write_bytes(encoded)
        changed.append(path.relative_to(ROOT).as_posix())

report = {'status': 'PASS', 'check': 'Only permitted serialized colour and base metallic fields differ; all other serialized bytes unchanged.', 'files': changed, 'changes': changes}
(REPORT / 'palette-validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'files': len(changed), 'properties': len(changes), 'status': 'PASS'}, ensure_ascii=False))
