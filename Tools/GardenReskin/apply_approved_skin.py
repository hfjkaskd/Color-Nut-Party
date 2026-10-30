"""Apply serialized art references and text space to existing components.

No hierarchy, control-position, event or logic edits. The stats follow-up widens
existing text rectangles. New sprites isolate this skin from other dialogs that
still use the legacy shared textures.
"""
import hashlib, json, re, sys, zipfile, subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'Docs/GardenReskin/approved-replacement'
OUT = ROOT / 'Assets/Resources/atlas/approvedgarden'
PANELS = ROOT / 'Assets/Resources/prefabs/panels'
GUID = lambda name: hashlib.md5(('approved-garden-20260922/'+name).encode()).hexdigest()

def entry(name,w,h,cell=0,source='controls',border=None,ratios=None,inset=0,meta_border=None):
    return dict(name=name,width=w,height=h,source=source,columns=2 if source=='controls' else 1,
        rows=4 if source=='controls' else (2 if source=='clean-controls' else 1),cell=cell,border=border or [0]*4,
        ratios=ratios or [.2]*4,inset=inset,meta_border=meta_border or [0]*4)

assets=[]
for name,w,h in [('panel-diamond',1048,1801),('panel-account',988,1421),('panel-cash',983,1034),('panel-tool',907,965)]:
    assets.append(entry(name,w,h,source='panel',border=[155]*4,ratios=[.26,.15,.26,.15]))
assets += [
    entry('ribbon',975,186,0),
    entry('primary-button',764,192,0,source='clean-controls'),
    entry('title',613,99,1,source='clean-controls'),
    entry('cash-title',457,84,1,source='clean-controls'),
    entry('selection-title',710,75,1,source='clean-controls'),
    entry('input',552,156,3,border=[40,40,40,40],ratios=[.15,.4,.15,.4],meta_border=[40,40,40,40]),
    entry('conversion',740,74,3),
    entry('amount-normal',365,221,4,border=[65,55,65,55],ratios=[.25,.30,.25,.30]),
    entry('amount-selected',365,221,5,border=[65,55,65,55],ratios=[.25,.30,.25,.30]),
    entry('cash-card',850,369,6,border=[100,65,100,65],ratios=[.22,.3,.22,.3]),
    entry('instructions',944,281,6,border=[100,35,100,35],ratios=[.22,.3,.22,.3],inset=30),
    entry('tool-icon-card',625,396,3,border=[60,60,60,60],ratios=[.15,.4,.15,.4]),
    entry('stats',968,334,source='stats'),
    entry('stats-card',256,117,3,border=[24,24,24,24],ratios=[.15,.4,.15,.4]),
    entry('records',1013,120,3,border=[40,20,40,20],ratios=[.15,.40,.15,.40]),
]

def prepare():
    WORK.mkdir(parents=True,exist_ok=True)
    (WORK/'asset-build.json').write_text(json.dumps(assets,indent=2),encoding='utf8')
    # Preserve exact prior prefab bytes for deterministic repeated adjustments.
    backup=WORK/'prefabs-all-before.zip'
    if not backup.exists():
        with zipfile.ZipFile(backup,'w',zipfile.ZIP_DEFLATED) as z:
            for p in list(PANELS.glob('*.prefab')) + [ROOT/'Assets/Resources/prefabs/playerinfo/PlayerInfo.prefab',ROOT/'Assets/Resources/prefabs/playerinfo/PMDSimple.prefab']:
                if p.exists(): z.write(p,p.relative_to(ROOT).as_posix())

def metadata():
    template=(ROOT/'Assets/Resources/atlas/txcoinpanel/bg.png.meta').read_text()
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT.with_suffix('.meta')).write_text('fileFormatVersion: 2\nguid: '+GUID('folder')+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    for e in assets:
        meta=re.sub(r'(?m)^guid: .*', 'guid: '+GUID(e['name']),template)
        meta=meta.replace('spriteMeshType: 1','spriteMeshType: 0')
        a,b,c,d=e['meta_border']
        meta=re.sub(r'spriteBorder: \{.*?\}',f'spriteBorder: {{x: {a}, y: {b}, z: {c}, w: {d}}}',meta)
        meta=re.sub(r'spriteID: \w+','spriteID: '+GUID(e['name']+'-sprite'),meta)
        (OUT/(e['name']+'.png.meta')).write_text(meta,encoding='utf8')

changes=[]
def patch(rel, ids=None, old_guids=None, hide_ids=(), hide_guids=(), extra=None):
    path=ROOT/rel
    with zipfile.ZipFile(WORK/'prefabs-all-before.zip') as z: raw=z.read(rel).decode('utf8')
    chunks=re.split(r'(?=^--- !u!)',raw,flags=re.M)
    for i,b in enumerate(chunks):
        header=re.match(r'--- !u!(\d+) &(\d+)',b)
        if (rel.endswith('/TXUserInfoPanel.prefab') and header and header[1]=='1'
                and header[2]=='1587765826080250'):
            # Pre-existing invalid duplicate: this feedback component belongs to
            # ChannelInfo. Keep its definition and correct owner reference.
            b,n=re.subn(r'(?m)^  - component: \{fileID: 114990000000000169\}\r?\n','',b)
            assert n==1
            chunks[i]=b
        if not header or header[1]!='114': continue
        ident=header[2]; original=b
        sg=re.search(r'm_Sprite: \{fileID: \d+, guid: (\w+)',b)
        sprite=sg[1] if sg else None
        name=(ids or {}).get(ident) or (old_guids or {}).get(sprite)
        if name:
            b=re.sub(r'(?m)^  m_Sprite: .*',f'  m_Sprite: {{fileID: 21300000, guid: {GUID(name)}, type: 3}}',b)
            b=re.sub(r'(?m)^  m_Material: .*','  m_Material: {fileID: 0}',b)
        if ident in hide_ids or sprite in hide_guids:
            b=re.sub(r'(?m)^  m_Color: .*','  m_Color: {r: 1, g: 1, b: 1, a: 0}',b)
        for key,value in (extra or {}).get(ident,{}).items():
            b,n=re.subn(r'(?m)^  '+re.escape(key)+r': .*','  '+key+': '+value,b)
            assert n==1,(rel,ident,key,n)
        if b!=original:
            changes.append(dict(path=rel,fileID=ident,before=hashlib.sha256(original.encode()).hexdigest(),after=hashlib.sha256(b.encode()).hexdigest()))
        chunks[i]=b
    path.write_text(''.join(chunks),encoding='utf8',newline='')

def apply():
    metadata()
    base='Assets/Resources/prefabs/panels/'
    patch(base+'TXPanel.prefab',ids={
        '114841364053301648':'panel-cash','114833703512093738':'cash-card',
        '114726790013014859':'ribbon','114694121525821480':'ribbon','114808113985169905':'ribbon',
        '114572839542275309':'primary-button','114703931505913403':'cash-title'},hide_ids=['114344419225485095'],extra={
            '114704330047573733':{'m_margin':'{x: 18, y: 4, z: 18, w: 4}','m_fontColor':'{r: 0.3, g: 0.14, b: 0.06, a: 1}'}
        })
    patch(base+'TXUserInfoPanel.prefab',ids={
        '114220653531496868':'panel-account','114693329660128220':'title','114689364072382846':'primary-button',
        '114384403120170545':'input','114571878917323452':'input','114121005239445321':'input','114291632710222552':'input'},hide_ids=['114291844617409189'])
    patch(base+'ItemGetPanel.prefab',ids={
        '114621510681728303':'panel-tool','114946936116752871':'tool-icon-card',
        '114587397909062814':'ribbon','114733206282342372':'primary-button'},hide_ids=['114302127217294661'])
    patch(base+'TXCoinPanel.prefab',ids={'114397768327470545':'title'},old_guids={
        '393e0001bdc489f40857dbec5ac936f9':'panel-diamond','47c80652793b83443a20e8c87092eef7':'conversion',
        '8b63430dfeacc5143ad59c4b247dc73f':'selection-title','a302f6d71f2231e41868fbeb99534a3d':'amount-selected',
        '478de0d414050f7448bf0f954b7170c5':'primary-button','13e72ae3d373a734199475b4e6b6e4cf':'instructions'},
        hide_guids=['759065bdee3746249b8a5da2d5d3fa7f','ff75f1fafa64e5448b7308c3324b608a'],extra={
            '114397768327470545':{'m_Enabled':'1','m_RaycastTarget':'0'},
            '114217368724061974':{'selectedSpritePath':'Atlas/approvedgarden/amount-selected','normalSpritePath':'Atlas/approvedgarden/amount-normal'},
            '114561126880652180':{'m_margin':'{x: 20, y: 0, z: 20, w: 0}'},
            '114695600491528212':{'m_margin':'{x: 20, y: 12, z: 20, w: 0}'},
            '114132862189046193':{'m_margin':'{x: 24, y: 6, z: 24, w: 6}'}
        })
    patch('Assets/Resources/prefabs/playerinfo/PlayerInfo.prefab',ids={
        '114977153420339851':'stats','114313026266067498':'stats-card',
        '114918264173551851':'stats-card','114089843903986396':'stats-card'},extra={
            '114634108722742392':{'m_margin':'{x: 12, y: 10, z: 12, w: 10}'},
            '114371939378208658':{'m_margin':'{x: 12, y: 10, z: 12, w: 10}'},
            '114060945828524476':{'m_margin':'{x: 12, y: 10, z: 12, w: 10}'}
        })
    # Locate the existing records prefab rather than creating a new UI node.
    records=list((ROOT/'Assets/Resources/prefabs').rglob('PMDSimple.prefab'))
    assert len(records)==1,records
    patch(records[0].relative_to(ROOT).as_posix(),ids={'114589667821646929':'records'})
    (WORK/'prefab-changes.json').write_text(json.dumps(changes,indent=2),encoding='utf8')
    subprocess.run([sys.executable,str(Path(__file__).with_name('expand_stats_labels.py'))],check=True)
    print('Applied visual fields to',len(changes),'existing components and expanded three statistics columns; positions and runtime code unchanged.')

if __name__=='__main__':
    prepare()
    if '--apply' in sys.argv: apply()
