"""Apply approved main-screen art to existing serialized visual references only."""
from october_common import *
import zipfile

SRC = WORK / 'sources'
assets = []
for state in ('normal', 'pending', 'active', 'crown'):
    name = 'main-milestone-' + state
    assets.append(import_sprite(name, SRC / (name + '.png'), (115, 137 if state == 'crown' else 118)))
assets.append(import_sprite('main-tip', SRC / 'main-tip.png', (527, 99)))
assets.append(import_sprite('main-toolbar-card', SRC / 'main-toolbar-card-final.png', (243, 192), (24, 24, 24, 24)))
assets.append(import_sprite('main-currency-card', OUT / 'input.png', (477, 126), (20, 20, 20, 20), (36, 36, 36, 36), (20, 20, 20, 20)))
assets.append(import_sprite('main-cash-button', SRC / 'main-cash-button-final.png', (193, 117), (30, 30, 30, 30)))
for icon in ('revoke', 'replay', 'setting'):
    source = SRC / ('main-' + icon + '-original.png')
    assets.append(import_sprite('main-' + icon, source, Image.open(source).size))

# Keep the world background texture's native dimensions, importer and all material
# references. No gameplay geometry, shader or color palette is changed.
background = ROOT / 'Assets/Resources/game/spirte/bg.png'
old_size = Image.open(background).size
Image.open(OUT / 'background.png').convert('RGBA').resize(old_size, Image.Resampling.LANCZOS).save(background)

def old_guid(stem):
    return re.search(r'^guid: (\w+)', (ROOT / ('Assets/Resources/atlas/' + stem + '.asset.meta')).read_text(), re.M)[1]

mapping = {
    old_guid('mainpanel/item_bg'): 'main-toolbar-card',
    old_guid('mainpanel/tx_bg'): 'main-currency-card',
    old_guid('mainpanel/goldget'): 'main-cash-button',
    old_guid('mainpanel/gold_tip_bg'): 'main-tip',
    old_guid('mainpanel/coin_tip_bg'): 'main-tip',
    old_guid('mainpanel/hintlevel_bg1'): 'main-milestone-pending',
    old_guid('mainpanel/hintlevel_bg2'): 'main-milestone-normal',
    old_guid('mainpanel/hintlevel_bg3'): 'main-milestone-active',
    old_guid('mainpanel/hintlevel_bg4'): 'main-milestone-crown',
    old_guid('items/Revoke'): 'main-revoke',
    old_guid('mainpanel/replay'): 'main-replay',
    old_guid('mainpanel/setting'): 'main-setting',
    old_guid('mainpanel/tx_bg1'): 'input',
}

report = []
files = ['MainPanel', 'LocalMainPanelTop', 'MainPanelBottom', 'GoldItem', 'CoinItem', 'RevokeItem', 'ExchangeItem', 'AddScrewItem', 'LocalSubrounds']
for name in files:
    path = ROOT / ('Assets/Resources/prefabs/panels/' + name + '.prefab')
    raw = path.read_text(encoding='utf8')
    before = indexed(raw)
    parts = blocks(raw)
    for i, block in enumerate(parts):
        match = re.match(r'--- !u!(\d+) &(\d+)', block)
        if not match:
            continue
        original = block
        # Replaces both serialized Image sprite and the existing milestone state
        # Sprite array. It never moves or creates a control, or changes its state.
        for guid, target in mapping.items():
            pattern = r'\{fileID: 21300000, guid: ' + guid + r', type: [23]\}'
            block = re.sub(pattern, sprite_ref(target), block)
        if original != block:
            ident = match[2]
            report.append({'file': str(path.relative_to(ROOT)), 'id': ident,
                           'node': before.get(ident, {}).get('path', 'existing variant reference'), 'change': 'existing visual sprite reference'})
        parts[i] = block
    path.write_text(''.join(parts), encoding='utf8', newline='')

report = []
with zipfile.ZipFile(WORK / 'before-implementation.zip') as baseline:
    for name in files:
        rel = 'Assets/Resources/prefabs/panels/' + name + '.prefab'
        if rel not in baseline.namelist():
            continue
        original = indexed(baseline.read(rel).decode('utf-8-sig').replace('\r\n', '\n'))
        current = indexed((ROOT / rel).read_text(encoding='utf8'))
        for ident, component in current.items():
            if ident in original and component['block'] != original[ident]['block']:
                report.append({'file': rel, 'id': ident, 'node': component['path'], 'change': 'existing visual sprite reference'})
(WORK / 'main-asset-build.json').write_text(json.dumps(assets, indent=2), encoding='utf8')
(WORK / 'main-prefab-changes.json').write_text(json.dumps(report, indent=2), encoding='utf8')
print('Main UI:', len(assets), 'assets;', len(report), 'existing components updated; background native size', old_size)
