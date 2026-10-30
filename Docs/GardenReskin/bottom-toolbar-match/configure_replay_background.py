"""Configure only existing ReplayBg Images; PNG artwork is supplied separately.

Run with --apply to reproduce the change, or without arguments to verify it.
Does not run Unity, create PNG files, or alter transforms, components or events.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / 'Assets/Resources/atlas/mainpanel/replay_bg'
TEXTURE_GUID = '0513d34314154e4297b1d9f4ab2ffd98'
SPRITE_GUID = '9fa45da9c8c5439bb43c8de5dfc8ac0c'
SPRITE_ID = '33a17292082f4c53b2093267d9175670'
RENDER_DATA_ID = '75f4baad63cb4ebb882166bffb7849c1'
OLD_SPRITE = 'ab17f362ed05132409da8bc07e1740fe'
OLD_TEXTURE = '128cc1d04832f414e8f94a6f1335ac60'
IMAGE_ID = '114450063812975771'
PREFABS = [ROOT / ('Assets/Resources/prefabs/panels/' + name + '.prefab')
           for name in ('MainPanel', 'MainPanelBottom', 'MainPanelComplete')]
BLOCKS = re.compile(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', re.M | re.S)


def read(path):
    return path.read_bytes().decode('utf-8')


def digest(data):
    return hashlib.sha256(data.encode('utf-8')).hexdigest()


def replay_image(text):
    blocks = {m[2]: m for m in BLOCKS.finditer(text)}
    image = blocks[IMAGE_ID]
    assert 'guid: fe87c0e1cc204ed48ad3b37840f39efc' in image[3]
    go_id = re.search(r'm_GameObject: \{fileID: (\d+)\}', image[3])[1]
    go = blocks[go_id]
    assert re.search(r'^  m_Name: ReplayBg\r?$', go[3], re.M)
    return image


def normalize_image_fields(text):
    match = replay_image(text)
    body = re.sub(r'^  m_Color: [^\r\n]*', '  m_Color: CHECK', match[3], flags=re.M)
    body = re.sub(r'^  m_Sprite: [^\r\n]*', '  m_Sprite: CHECK', body, flags=re.M)
    return text[:match.start(3)] + body + text[match.end(3):]


def new_asset_texts():
    texture = read(ROOT / 'Assets/Texture2D/kuang.png.meta')
    texture = texture.replace('guid: ' + OLD_TEXTURE, 'guid: ' + TEXTURE_GUID, 1)
    texture = re.sub(r'(^    spriteID: )[^\r\n]*', r'\g<1>' + SPRITE_ID, texture, flags=re.M)
    sprite = read(ROOT / 'Assets/Sprite/kuang.asset')
    sprite = sprite.replace('  m_Name: kuang', '  m_Name: replay_bg', 1)
    sprite = sprite.replace('guid: ' + OLD_TEXTURE, 'guid: ' + TEXTURE_GUID)
    sprite = re.sub(r'(^  m_RenderDataKey:\r?\n    )[0-9a-f]+:',
                    r'\g<1>' + RENDER_DATA_ID + ':', sprite, count=1, flags=re.M)
    sprite_meta = read(ROOT / 'Assets/Sprite/kuang.asset.meta')
    sprite_meta = sprite_meta.replace('guid: ' + OLD_SPRITE, 'guid: ' + SPRITE_GUID, 1)
    return {Path(str(ART) + '.png.meta'): texture,
            Path(str(ART) + '.asset'): sprite,
            Path(str(ART) + '.asset.meta'): sprite_meta}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    generated = new_asset_texts()
    # Fail before writes if Unity or another author already claimed either GUID.
    expected = {TEXTURE_GUID: Path(str(ART) + '.png.meta'),
                SPRITE_GUID: Path(str(ART) + '.asset.meta')}
    for meta in (ROOT / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]+)', read(meta), re.M)
        if match and match[1] in expected:
            assert meta.resolve() == expected[match[1]].resolve(), str(meta)
    changes = []
    for path in PREFABS:
        before = read(path)
        match = replay_image(before)
        body = match[3]
        assert re.search(r'^  m_Sprite: \{fileID: 21300000, guid: (' + OLD_SPRITE + '|' + SPRITE_GUID + r'), type: 2\}\r?$', body, re.M)
        body = re.sub(r'^  m_Color: [^\r\n]*', '  m_Color: {r: 1, g: 1, b: 1, a: 1}', body, flags=re.M)
        body = re.sub(r'^  m_Sprite: [^\r\n]*', '  m_Sprite: {fileID: 21300000, guid: ' + SPRITE_GUID + ', type: 2}', body, flags=re.M)
        after = before[:match.start(3)] + body + before[match.end(3):]
        assert normalize_image_fields(before) == normalize_image_fields(after)
        assert len(list(BLOCKS.finditer(before))) == len(list(BLOCKS.finditer(after)))
        if args.apply:
            path.write_bytes(after.encode('utf-8'))
        else:
            assert before == after, 'Prefab not configured: ' + str(path)
        changes.append({'path': str(path.relative_to(ROOT)).replace('\\', '/'),
                        'imageFileID': IMAGE_ID, 'beforeSha256': digest(before),
                        'afterSha256': digest(after),
                        'onlyAllowedImageFieldsChanged': True})
    for path, content in generated.items():
        if args.apply:
            if path.exists():
                assert read(path) == content, 'Existing asset differs: ' + str(path)
            else:
                path.write_bytes(content.encode('utf-8'))
        else:
            assert read(path) == content, 'Asset differs: ' + str(path)
    png = Path(str(ART) + '.png')
    if png.exists():
        header = png.read_bytes()[:24]
        assert header[:8] == b'\x89PNG\r\n\x1a\n'
        assert (int.from_bytes(header[16:20], 'big'), int.from_bytes(header[20:24], 'big')) == (85, 85)
    report = {'scope': 'ReplayBg Image m_Sprite and m_Color only',
              'textureGuid': TEXTURE_GUID, 'spriteGuid': SPRITE_GUID,
              'artworkPath': str(png.relative_to(ROOT)).replace('\\', '/'),
              'artworkProvided': png.exists(), 'prefabs': changes,
              'newAssets': [str(p.relative_to(ROOT)).replace('\\', '/') for p in generated],
              'unityValidation': 'Not run; PNG artwork and Unity import/visual validation are separate.'}
    report_path = Path(__file__).with_name('replay-background-config-audit.json')
    if args.apply and not report_path.exists():
        report_path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
