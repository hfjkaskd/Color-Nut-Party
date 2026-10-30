"""Map ImageGen artwork to existing texture slots; no pixel editing."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GEN = 'Docs/GardenReskin/Generated/'
entries = {}

def add(path, source='core-sheet', cell=0, cols=4, rows=4, **options):
    target = 'Assets/' + path
    assert (ROOT / target).exists(), target
    entries[target] = dict(target=target, source=GEN+source+'.png', mode='grid', columns=cols, rows=rows, cell=cell, **options)

def atlas(folder, names, cell, **options):
    for name in names.split(): add('Resources/atlas/'+folder+'/'+name+'.png', cell=cell, **options)

def tex(names, cell, **options):
    for name in names.split(): add('Texture2D/'+name+'.png', cell=cell, **options)

def single(path, source, **options): add(path, source, 0, 1, 1, **options)

# Every existing theme frame is changed in its original file, dimensions and GUID.
atlas('mainpanel','tx_bg tx_bg1',0)
single('Resources/atlas/mainpanel/item_bg.png','tool-panel-light')
atlas('mainpanel','item_bg1',11)
atlas('mainpanel','goldget',2)
single('Resources/atlas/mainpanel/hintlevel_bg1.png','pending-shield')
atlas('mainpanel','hintlevel_bg2',8)
atlas('mainpanel','hintlevel_bg3',9)
atlas('mainpanel','hintlevel_bg4',10)
single('Resources/atlas/mainpanel/hintlevel_tx_bg.png','withdraw-label-gold')
single('Resources/atlas/mainpanel/gold_tip_bg.png','cash-tip-flat')
single('Resources/atlas/mainpanel/coin_tip_bg.png','diamond-tip-flat')
atlas('mainpanel','tx_bar_bg tx_lv_bar_bg',4)
atlas('mainpanel','tx_bar tx_lv_bar',5)
atlas('mainpanel','level_bg tx_dj_bg PlayerGoldGetHint_bg TargetReward_bg target_tip_bg',4)

for path,cell in [('items/Revoke',0),('items/Exchange',1),('items/AddScrew',2),('mainpanel/setting',3),('mainpanel/replay',4),('tooloffer/close',7),('tooloffer/ad',8)]:
    add('Resources/atlas/'+path+'.png','icon-sheet',cell,3,3)
single('Resources/atlas/mainpanel/item_count_bg.png','tool-count-green')
single('Resources/atlas/mainpanel/item_add.png','tool-add-green')

atlas('appratingpanel','bg',12)
single('Resources/atlas/appratingpanel/title.png','replay-title',alphaThreshold=180)
atlas('localstats','bg2_3 bg_3',15)
single('Resources/atlas/newbieguidepanel/bg.png','teaching-thin-frame')
atlas('newbieguidepanel','bg1',8)
# SetMarker uses dian for empty and dian1 for filled. Keep the original slots
# and dimensions, using round art instead of the previous gray square.
single('Resources/atlas/newbieguidepanel/dian.png','replay-cream-disc')
single('Resources/atlas/newbieguidepanel/dian1.png','tool-count-green')
single('Resources/atlas/newbieguidepanel/line.png','tutorial-track-wood')
atlas('tooloffer','bg_0',12)
atlas('tooloffer','bg1_5 bg2_1',15)
atlas('tooloffer','btn1',2)
atlas('tooloffer','goldget_bg',3,preserveOriginalAlpha=True)
atlas('tooloffer','title_0',13)
atlas('txcoinpanel','bar',5)
atlas('txcoinpanel','bar_bg',4)
atlas('txcoinpanel','bg',12)
atlas('txcoinpanel','bg3',15)
atlas('txcoinpanel','bg2 bg4',15)
atlas('txcoinpanel','btn',2)
atlas('txcoinpanel','item_bg',11)
atlas('txcoinpanel','item_bg1',1)
atlas('txcoinpanel','item_bg2',2)
atlas('txcoinpanel','item_bg1_1',2,preserveOriginalAlpha=True)
atlas('txcoinpanel','tip_bg title1',4)

for path in ['Resources/game/spirte/bg.png','Texture2D/bg.png','Texture2D/bg_2.png']:
    single(path,'background'); entries['Assets/'+path]['mode']='full'
tex('bg0 bg0_0 bg_0 bg_10 bg_11 bg_12 bg_14 bg_5',12)
tex('bg1_2 bg1_5 bg1_8 bg2_1 bg3_0',15)
tex('bg1_4 bg1_7 bg2_4 bg3_1',15)
tex('bg1 bg1_1 bg_4 bg_7 bg2_0 pmd_dm_bg tip_bg',4)
tex('pmd_bg',0)
single('Texture2D/bg2_2.png','cash-title',alphaThreshold=180)
tex('bar bar_1 bar_2',5)
tex('bar_bg bar_bg_1 bar_bg_2',4)
tex('btn btn1 btn_0',2)
tex('goldget_bg tip_bg_0',3,preserveOriginalAlpha=True)
tex('title_0 title_9',13)
tex('title1',14)
tex('title2 title3 title_5',4)
single('Texture2D/title.png','replay-title',alphaThreshold=180)
single('Texture2D/title_1.png','mechanic-title',alphaThreshold=180)
for name in ['title_3','title_7']: single('Texture2D/'+name+'.png','cash-title',alphaThreshold=180)
for name,cell in [('close',7),('ad',8)]: add('Texture2D/'+name+'.png','icon-sheet',cell,3,3)
tex('line line2',5,preserveOriginalAlpha=True)
for name in ['loading','loading1']: add('Texture2D/'+name+'.png','icon-sheet',6,3,3,preserveOriginalAlpha=True)
# A mask used under settings/replay: preserve its existing circular alpha exactly.
tex('kuang',1,preserveOriginalAlpha=True)
single('Texture2D/logo_android.png','logo')
single('Resources/game/spirte/Mask.png','cover',preserveOriginalAlpha=True)
single('Resources/game/spirte/Mask1.png','cover',preserveOriginalAlpha=True)

# The first core-sheet panel crosses its nominal grid-cell boundary. The
# separately extracted source retains its complete right edge without changing
# any target texture or Sprite dimensions.
for entry in entries.values():
    if entry['source'] == GEN+'core-sheet.png' and entry['cell'] == 0:
        entry.update(source=GEN+'cream-panel.png', columns=1, rows=1, cell=0)

preserve = {
 'Texture2D/bg2_5.png':'Cream information strip with an embedded blue-white bank icon at its right edge; used by TXTooFastHintPanel/main/bg (2). Preserve the bank symbol and existing text-safe area instead of replacing it with a blank frame.',
 'Resources/atlas/txrecordpanel/bg1.png':'Bank building icon despite the generic bg1 filename; preserve the original blue-white bank, columns and roof. Used by TXCoinPanel/main/Image, not a blank status badge.',
 'Resources/atlas/mainpanel/house.png':'Bank entry icon already honey-gold; retains bank semantics.',
 'Resources/game/spirte/ALCI.png':'Neutral stone fixed-screw obstacle; preserve cracks/state readability.',
 'Resources/game/spirte/Stone.png':'Neutral broken stone state paired with ALCI; preserve exact fragments.',
 'Resources/game/spirte/Mask_done.png':'White completion check mark; preserve state meaning.',
 'Texture2D/ls.png':'Gameplay nut icon; preserve color semantics and hole geometry.',
 'Texture2D/Nut_Stand_Color.png':'Shared mesh UV/metal thread texture. Cream base is supplied by existing material palette changes.',
 'Texture2D/screwHide.png':'Tutorial demonstrates exact hidden-nut count/colors; preserve instructional content.',
}
inventory=json.loads((ROOT/'Docs/GardenReskin/asset-inventory.json').read_text(encoding='utf-8'))
covered=[]
uncovered=[]
for asset in inventory['assets']:
    if not asset['theme_replacement_candidate']: continue
    path=asset['path']
    if path in entries: covered.append(path)
    elif path.removeprefix('Assets/') not in preserve: uncovered.append(path)
assert not uncovered, uncovered
manifest={'schema':1,'artworkTool':'built-in image_gen','alphaThreshold':100,'padding':2,'sourceSliceRatio':0.12,'entries':list(entries.values())}
slice_ratios = {
 'bg2_4':[.12,.45,.12,.45], 'pmd_dm_bg':[.12,.40,.12,.40],
 'bg0':[.25,.20,.25,.45], 'bg_10':[.12,.38,.12,.25],
 'bg_0':.25,'bg_11':.25,'bg1_2':.25,'bg1_5':.25,'bg3_0':.25,
}
for name, ratios in slice_ratios.items():
    entries['Assets/Texture2D/'+name+'.png']['sourceSliceRatio']=ratios
# Bake a narrow frame into this tall existing Simple Image. The native Sprite
# remains unsliced; its fixed hint text must sit on the cream interior.
entries['Assets/Resources/atlas/tooloffer/bg1_5.png'].update(
    oldBorder=[32,28,32,28], sourceSliceRatio=[.12,.28,.12,.28])
(ROOT/'Docs/GardenReskin/import-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'Docs/GardenReskin/coverage.json').write_text(json.dumps({'candidateCount':125,'replacedThemeCandidates':len(covered),'totalTextureReplacements':len(entries),'preservedCandidates':preserve,'uncovered':uncovered,'semanticAndTechnicalPreserved':'Country banknotes, diamonds, payment logos, shader masks/font atlases/normal maps, exact puzzle colors and question marks.'},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(entries)} texture replacements; {len(preserve)} explicit gameplay/semantic preservations; no uncovered theme candidates.')
