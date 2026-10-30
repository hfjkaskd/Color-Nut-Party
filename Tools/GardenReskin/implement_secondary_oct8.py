"""Apply approved October UI art to the original secondary-page components.

Textures come from built-in ImageGen, saved with provenance in the evidence
folder. The patch allowlist contains visual fields only; transforms, localized
copy, event wiring, component lists and gameplay fields remain byte-identical.
"""
from pathlib import Path
import hashlib
import json
import re
import sys
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'Docs/GardenReskin/implementation-20261008'
ART = ROOT / 'Assets/Resources/atlas/octobergarden'
GUID = lambda name: hashlib.md5(('october-garden-20261008/' + name).encode()).hexdigest()
BROWN = '{r: 0.30, g: 0.12, b: 0.025, a: 1}'
WHITE = '{r: 1, g: 1, b: 1, a: 1}'
CREAM = '{r: 1, g: 0.97, b: 0.87, a: 1}'
PLAIN_TEXT = '{fileID: 2100000, guid: f8d75af0502c5ce4e833fe267934fc89, type: 2}'
VISUAL_FIELDS = {'m_Sprite', 'm_Material', 'm_Color', 'm_fontColor', 'm_fontColor32',
                 'm_sharedMaterial', 'm_Type', 'm_PixelsPerUnitMultiplier'}
UNIQUE = {'boot-background': (853, 2048), 'boot-logo': (818, 413),
          'replay-title': (944, 485), 'notice-title': (961, 308),
          'success-title': (896, 362)}

def prepare_art():
    ART.mkdir(parents=True, exist_ok=True)
    template = (ROOT / 'Assets/Resources/atlas/txcoinpanel/bg.png.meta').read_text()
    for name, size in UNIQUE.items():
        source = WORK / 'secondary-sources' / (name + '-source.png')
        if name in ('boot-background', 'boot-logo'):
            source = ROOT / 'Docs/GardenReskin/loading-color-nut-party-20261009/implementation' / (name + '-source.png')
        assert source.exists(), source
        with Image.open(source) as im:
            im.convert('RGBA').resize(size, Image.Resampling.LANCZOS).save(ART / (name + '.png'))
        meta = re.sub(r'(?m)^guid: .*', 'guid: ' + GUID(name), template)
        meta = meta.replace('spriteMeshType: 1', 'spriteMeshType: 0')
        meta = re.sub(r'spriteID: \w+', 'spriteID: ' + GUID(name + '-sprite'), meta)
        (ART / (name + '.png.meta')).write_text(meta, encoding='utf8')
    # This material is shared by other approved pages; the original font atlas
    # stays unchanged. Its only difference is a fine ivory outline.
    source = (ROOT / 'Assets/Material/zh_Custom SDF_outline_brown.mat').read_text()
    source = source.replace('m_Name: zh_Custom SDF_outline_brown', 'm_Name: brown-title')
    source = re.sub(r'(_OutlineColor: )\{[^\n]+\}', r'\g<1>' + CREAM, source)
    source = re.sub(r'(_OutlineWidth: )[^\r\n]+', r'\g<1>0.14', source)
    (ART / 'brown-title.mat').write_text(source, encoding='utf8')
    (ART / 'brown-title.mat.meta').write_text(
        'fileFormatVersion: 2\nguid: ' + GUID('brown-title') +
        '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n'
        '  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf8')

def sprite(name, image_type=None, color=None):
    d = {'m_Sprite': '{fileID: 21300000, guid: ' + GUID(name) + ', type: 3}',
         'm_Material': '{fileID: 0}'}
    if image_type is not None: d['m_Type'] = str(image_type)
    if color: d['m_Color'] = color
    return d

def text_style(outline=False, color=BROWN):
    return {'m_fontColor': color, 'm_sharedMaterial':
            '{fileID: 2100000, guid: ' + GUID('brown-title') + ', type: 2}' if outline else PLAIN_TEXT}

PANELS = 'Assets/Resources/prefabs/panels/'
PATCHES = {
    'BootLoadingPanel': {
        '386': sprite('boot-background'), '387': sprite('boot-logo'),
        '397': sprite('progress-track'), '410': sprite('progress-fill'),
        '377': {'m_sharedMaterial': '{fileID: 2100000, guid: c7c2bfca7db84794686330d1d4ff10bc, type: 2}'},
    },
    'NewbieGuidePanel': {
        '114472185465350827': sprite('cream-card', 1),
        '114841364053301648': text_style(),
    },
    'FailPanel': {
        '114161607090078389': sprite('background', 0, WHITE),
        '114621510681728303': sprite('primary-button', 1),
        '114060945828524476': sprite('primary-button', 1),
        '114291844617409189': sprite('video-ad'),
        '114107629493147155': text_style(True),
    },
    'ReplayPanel': {
        '114705390221481576': sprite('panel', 1),
        '114256069497284392': sprite('replay-title'),
        '114632457200066912': sprite('primary-button', 1),
        '114470112387431263': text_style(True),
        '114918264173551851': text_style(),
        '114371939378208658': text_style(),
    },
    'RewardPanel': {'114587765826080250': sprite('reward-band')},
    'SuccessPanel': {
        '114977153420339851': sprite('background'),
        '114002669891404536': sprite('primary-button', 1),
        '114969106005120503': sprite('video-ad'),
        '114089843903986396': sprite('success-title'),
        '114060945828524476': text_style(True),
        '114371939378208658': text_style(),
    },
    'MessagePanel': {
        '384': sprite('notice-title'), '385': sprite('panel', 1),
        '389': sprite('primary-button', 1),
        '378': text_style(True), '379': text_style(),
    },
    'UnlockGameplayPanel': {
        '114025901758691103': sprite('background', 0, WHITE),
        '114256069497284392': sprite('primary-button', 1),
        '114634108722742392': text_style(),
        '114918264173551851': text_style(True),
        '114642988549198181': {'m_Color': '{r: 1, g: 1, b: 1, a: 0.25}'},
    },
}

def normalize(raw):
    raw = re.sub(r'(?m)^  (' + '|'.join(re.escape(x) for x in VISUAL_FIELDS - {'m_fontColor32'}) + r'): .*', r'  \1: <VISUAL>', raw)
    return re.sub(r'(  m_fontColor32:\n    serializedVersion: 2\n    rgba: )\d+', r'\1<VISUAL>', raw)

def patch_one(path, operations, changes):
    source = path.read_text(encoding='utf8')
    chunks = re.split(r'(?=^--- !u!)', source, flags=re.M)
    found = set()
    for index, block in enumerate(chunks):
        header = re.match(r'--- !u!114 &(\d+)', block)
        if not header or header[1] not in operations: continue
        ident = header[1]; found.add(ident)
        for field, value in operations[ident].items():
            assert field in VISUAL_FIELDS, field
            block, n = re.subn(r'(?m)^  ' + re.escape(field) + r': .*', '  ' + field + ': ' + value, block)
            assert n == 1, (path, ident, field)
            if field == 'm_fontColor':
                rgb = [float(x) for x in re.findall(r'[rgba]: ([0-9.]+)', value)]
                color32 = sum(round(v * 255) << (i * 8) for i, v in enumerate(rgb))
                block = re.sub(r'(  m_fontColor32:\n    serializedVersion: 2\n    rgba: )\d+', r'\g<1>' + str(color32), block)
        chunks[index] = block
    assert found == set(operations), (path, set(operations) - found)
    result = ''.join(chunks)
    assert normalize(source) == normalize(result), 'NON VISUAL EDIT: ' + str(path)
    if result != source:
        path.write_text(result, encoding='utf8', newline='')
    changes.append({'file': path.relative_to(ROOT).as_posix(), 'components': operations,
                    'sha256': hashlib.sha256(result.encode()).hexdigest(),
                    'geometry_hierarchy_text_bindings_unchanged': True})

def apply():
    assert (WORK / 'before-implementation.zip').exists(), 'Current baseline must exist first'
    needed = {m.group(1) for ops in PATCHES.values() for fields in ops.values()
              for key, value in fields.items() if key == 'm_Sprite'
              for m in [re.search(r'guid: (\w+)', value)]}
    existing = {re.search(r'^guid: (\w+)', p.read_text(), re.M).group(1)
                for p in ART.glob('*.png.meta')}
    assert needed <= existing, 'Shared sprites are not installed yet: ' + str(needed - existing)
    changes = []
    for name, operations in PATCHES.items():
        patch_one(ROOT / PANELS / (name + '.prefab'), operations, changes)
    # The shipped startup uses the scene's existing OriginalLoadingView, not the
    # standalone BootLoadingPanel used by gallery captures. Keep both in sync.
    patch_one(ROOT / 'Assets/Scenes/LuoSiSortGame.unity', PATCHES['BootLoadingPanel'], changes)
    for rel in ['Assets/Resources/prefabs/items/Item.prefab', 'Assets/Resources/prefabs/items/Item_Big.prefab']:
        raw = (ROOT / rel).read_text()
        ids = {re.match(r'--- !u!114 &(\d+)', b)[1]: text_style(True)
               for b in re.split(r'(?=^--- !u!)', raw, flags=re.M)
               if re.match(r'--- !u!114 &(\d+)', b) and '  m_fontColor:' in b}
        patch_one(ROOT / rel, ids, changes)
    (WORK / 'secondary-prefab-changes.json').write_text(json.dumps(changes, indent=2), encoding='utf8')
    print('PASS: visual-only patch applied to', len(changes), 'Prefabs; no hierarchy/geometry/content/binding changes')

if __name__ == '__main__':
    if '--prepare' in sys.argv: prepare_art()
    if '--apply' in sys.argv: apply()
