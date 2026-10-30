"""Apply approved Oct 8 artwork only to eight existing withdrawal prefabs.

Preserves every GameObject, component, RectTransform, event and live label.
"""
from pathlib import Path
import hashlib, json, re, sys, zipfile

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'Docs/GardenReskin/implementation-20261008'
NAMES=['TXUserInfoSurePanel','TXChannelPanel','TXLevelPanel','TXProgress0Panel','TXTooFastHintPanel','TXGuideLoadingPanel','TXRecordGuidePanel','TXGuideTargetCompletePanel']
GUID=lambda name:hashlib.md5(('october-garden-20261008/'+name).encode()).hexdigest()
OUT=ROOT/'Assets/Resources/atlas/octobergarden'

def import_assets():
    from PIL import Image
    import shutil
    OUT.mkdir(parents=True,exist_ok=True)
    template=(ROOT/'Assets/Resources/atlas/txcoinpanel/bg.png.meta').read_text(encoding='utf8')
    entries={'aux-guide-title':(896,828),'aux-complete-title':(896,362),'aux-loading-band':(1080,505),'aux-steps-panel':(998,1550),'aux-level-band':(1080,234),'aux-verification-card':(896,246),'panel-heading':(1000,1400)}
    report=[]
    for name,size in entries.items():
        spec=json.loads((WORK/f'aux-sources/{name}-generation.json').read_text(encoding='utf8'))
        raw=Path(spec['raw_path'])
        source=WORK/f'aux-sources/{name}-raw.png'
        shutil.copy2(raw,source)
        im=Image.open(source).convert('RGBA')
        # ImageGen introduced empty outer canvas on the very wide assets.
        # Crop alpha bounds only, then resample to the original sprite canvas.
        if name in ('aux-level-band','aux-verification-card','panel-heading'):
            bbox=im.getchannel('A').point(lambda x:255 if x>16 else 0).getbbox()
            im=im.crop(bbox)
        im.resize(size,Image.Resampling.LANCZOS).save(OUT/f'{name}.png')
        meta=re.sub(r'(?m)^guid: .*','guid: '+GUID(name),template)
        meta=meta.replace('spriteMeshType: 1','spriteMeshType: 0')
        border=[64,64,64,235] if name=='panel-heading' else [0,0,0,0]
        meta=re.sub(r'spriteBorder: \{.*?\}',f'spriteBorder: {{x: {border[0]}, y: {border[1]}, z: {border[2]}, w: {border[3]}}}',meta)
        meta=re.sub(r'spriteID: \w+','spriteID: '+GUID(name+'-sprite'),meta)
        (OUT/f'{name}.png.meta').write_text(meta,encoding='utf8')
        report.append(dict(asset=name,guid=GUID(name),size=size,source=source.relative_to(ROOT).as_posix()))
    # Exact original semantic assets stay original: blue close, gold amount card,
    # and guide milestones. Copy to isolated new sprites rather than globally
    # undoing the prior reskin's shared texture modifications.
    with zipfile.ZipFile(ROOT/'Docs/GardenReskin/OriginalAssets/before-reskin.zip') as z:
        copies={'aux-level-card':'Assets/Texture2D/bg2_0.png','aux-blue-close':'Assets/Texture2D/close.png','aux-guide-node':'Assets/Resources/atlas/mainpanel/hintlevel_bg1.png','aux-guide-crown':'Assets/Resources/atlas/mainpanel/hintlevel_bg4.png','aux-guide-withdraw':'Assets/Resources/atlas/mainpanel/hintlevel_tx_bg.png','aux-level-title':'Assets/Texture2D/title3.png',
                'aux-status-line':'Assets/Texture2D/line.png','aux-status-line-pending':'Assets/Texture2D/line2.png','aux-status-pending':'Assets/Texture2D/loading.png','aux-status-hourglass':'Assets/Texture2D/loading1.png'}
        for name,original in copies.items():
            (OUT/f'{name}.png').write_bytes(z.read(original))
            meta=re.sub(r'(?m)^guid: .*','guid: '+GUID(name),template).replace('spriteMeshType: 1','spriteMeshType: 0')
            meta=re.sub(r'spriteID: \w+','spriteID: '+GUID(name+'-sprite'),meta)
            (OUT/f'{name}.png.meta').write_text(meta,encoding='utf8')
            report.append(dict(asset=name,guid=GUID(name),source='before-reskin.zip/'+original))
    (WORK/'aux-asset-imports.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    material=(ROOT/'Assets/Material/zh_Custom SDF_outline_gray.mat').read_text(encoding='utf8')
    material=re.sub(r'(?m)^  m_Name: .*','  m_Name: aux-progress-label',material)
    material=re.sub(r'_OutlineWidth: .*','_OutlineWidth: 0.18',material)
    material=re.sub(r'_OutlineColor: .*','_OutlineColor: {r: 0.30, g: 0.12, b: 0.025, a: 1}',material)
    (OUT/'aux-progress-label.mat').write_text(material,encoding='utf8')
    (OUT/'aux-progress-label.mat.meta').write_text('fileFormatVersion: 2\nguid: '+GUID('aux-progress-label')+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf8')
    print('Imported',len(report),'isolated assets without changing shared original resources.')

def blocks(raw):
    return re.split(r'(?=^--- !u!)',raw,flags=re.M)

def field(raw,key,default=''):
    m=re.search(r'(?m)^  '+re.escape(key)+r': (.*)$',raw)
    return m[1] if m else default

# Exact existing Image component IDs, inspected against all eight source Prefabs.
# A tuple also changes the Image drawing mode; filled progress bars retain theirs.
IMAGES={
 'TXUserInfoSurePanel':{
  '114002669891404536':('panel-heading',0),'114621510681728303':('cream-card',1),
  '114587397909062814':('title',1),'114733206282342372':('aux-blue-close',0),
  '114472185465350827':('primary-button',1),'114919942235722503':('primary-button',1)},
 'TXChannelPanel':{
  '114161607090078389':('panel-heading',0),'114634108722742392':('title',1),
  '114918264173551851':('aux-blue-close',0),'114320256170494724':('primary-button',1),
  '114060945828524476':('input',1)},
 'TXLevelPanel':{
  '114060945828524476':('panel',1),'114967328832014496':('aux-level-card',0),
  '114436073260064126':('aux-level-title',0),'114581786255902400':('primary-button',1),
  '114043919389829319':('aux-level-band',0),'114693062532245359':('aux-blue-close',0)},
 'TXProgress0Panel':{
  '114472185465350827':('aux-steps-panel',0),'114833703512093738':('title',1),
  '114477046003934300':('primary-button',1),'114328299215684997':('cream-card',1)},
 'TXTooFastHintPanel':{
  '114634108722742392':('panel-heading',0),'114642988549198181':('cream-card',1),
  '114918264173551851':('aux-verification-card',0),'114371939378208658':('cream-card',1),
  '114060945828524476':('title',1),'114967328832014496':('primary-button',1),
  '114719342369648492':('input',1),'114693329660128220':'progress-fill',
  '114477046003934300':('input',1),'114924050743934937':'progress-fill'},
 'TXGuideLoadingPanel':{
  '114372920208702427':('aux-loading-band',0),'114705390221481576':('progress-track',1),
  '114256069497284392':'progress-fill'},
 'TXRecordGuidePanel':{
  '114518489005915545':('aux-guide-title',0),'114919942235722503':('cream-card',1),
  '114801075752484240':('progress-track',1),'114571718729274231':'progress-fill',
  '114215879266689626':('aux-guide-node',0),'114574304967836189':('aux-guide-node',0),
  '114976365255789935':('aux-guide-node',0),'114123703392571528':('aux-guide-node',0),
  '114240571728440943':('aux-guide-crown',0),
  '114572839542275309':('aux-guide-withdraw',0),'114552309709192268':('aux-guide-withdraw',0),
  '114121005239445321':('aux-guide-withdraw',0),'114641631609468814':('aux-guide-withdraw',0),
  '114633296557590524':('aux-guide-withdraw',0),'114771092198208730':('primary-button',1)},
 'TXGuideTargetCompletePanel':{
  '114002669891404536':('panel',1),'114621510681728303':('cream-card',1),
  '114711914853252429':('primary-button',1),'114672306508523268':('aux-complete-title',0)}
}
HIDE={'TXLevelPanel':['114518489005915545'],'TXProgress0Panel':['114841364053301648']}
TITLES={
 'TXUserInfoSurePanel':['114694121525821480'],
 'TXChannelPanel':['114642988549198181'],
 'TXLevelPanel':['114000955326284023','114026196548002408'],
 'TXProgress0Panel':['114726790013014859'],
 'TXTooFastHintPanel':['114518489005915545'],
 'TXGuideLoadingPanel':['114470112387431263'],
 'TXRecordGuidePanel':['114967328832014496','114632746491512912'],
 'TXGuideTargetCompletePanel':['114900271049623953','114344419225485095']
}
BROWN='{r: 0.30, g: 0.12, b: 0.025, a: 1}'
WHITE='{r: 1, g: 1, b: 1, a: 1}'
TITLE_MAT='{fileID: 2100000, guid: dcf27bde9acc4e7d2f963953da8562cf, type: 2}'
PLAIN_MAT='{fileID: 2100000, guid: f8d75af0502c5ce4e833fe267934fc89, type: 2}'
INSETS={
 'TXChannelPanel':{'114642988549198181':'{x: 18, y: 4, z: 18, w: 4}', '114107629493147155':'{x: 8, y: 8, z: 8, w: 8}'},
 'TXLevelPanel':{'114000955326284023':'{x: 12, y: 4, z: 12, w: 4}'},
 'TXTooFastHintPanel':{'114518489005915545':'{x: 18, y: 4, z: 18, w: 4}'}
}
RESTORE_SPRITES={}
for old,new in [('line','aux-status-line'),('line2','aux-status-line-pending'),('loading','aux-status-pending'),('loading1','aux-status-hourglass')]:
    oldguid=re.search(r'(?m)^guid: (\w+)',(ROOT/f'Assets/Sprite/{old}.asset.meta').read_text())[1]
    RESTORE_SPRITES[oldguid]=new

def setfield(b,key,value):
    b,n=re.subn(r'(?m)^  '+re.escape(key)+r': .*','  '+key+': '+str(value),b)
    assert n==1,(key,n)
    return b

def protect(b):
    # Every remaining byte must remain identical: geometry, hierarchy,
    # component ownership, events, buttons, localized text and runtime bindings.
    allowed=('m_Sprite','m_Material','m_Type','m_PreserveAspect','m_Color','m_fontColor','m_sharedMaterial','m_margin')
    for key in allowed:b=re.sub(r'(?m)^  '+re.escape(key)+r': .*\n?','',b)
    return b

def apply():
    changes=[]; audit=[]
    with zipfile.ZipFile(WORK/'before-implementation.zip') as z:
        for name in NAMES:
            rel=f'Assets/Resources/prefabs/panels/{name}.prefab'
            baseline=z.read(rel).decode('utf8').replace('\r\n','\n')
            bs=blocks(baseline)
            touched=set()
            for i,b in enumerate(bs):
                head=re.match(r'--- !u!(\d+) &(\d+)',b)
                if not head or head[1]!='114':continue
                ident=head[2];before=b
                if ident in IMAGES[name]:
                    spec=IMAGES[name][ident];sprite,mode=spec if isinstance(spec,tuple) else (spec,None)
                    assert (OUT/f'{sprite}.png').is_file(),sprite
                    b=setfield(b,'m_Sprite',f'{{fileID: 21300000, guid: {GUID(sprite)}, type: 3}}')
                    b=setfield(b,'m_Material','{fileID: 0}')
                    b=setfield(b,'m_Color',WHITE)
                    b=setfield(b,'m_PreserveAspect','0')
                    if mode is not None:b=setfield(b,'m_Type',mode)
                    if name=='TXProgress0Panel' and ident=='114328299215684997':
                        b=setfield(b,'m_Color','{r: 1, g: 0.92, b: 0.88, a: 1}')
                    touched.add(ident)
                if ident in HIDE.get(name,[]):
                    b=setfield(b,'m_Color','{r: 1, g: 1, b: 1, a: 0}')
                    touched.add(ident)
                if ident in TITLES.get(name,[]):
                    # Shared title material has neutral face, cream outline;
                    # each existing TMP supplies the approved brown face tint.
                    b=setfield(b,'m_fontColor',BROWN)
                    b=setfield(b,'m_sharedMaterial',TITLE_MAT)
                    touched.add(ident)
                if name=='TXProgress0Panel' and ident=='114743393880019028':
                    b=setfield(b,'m_fontColor',BROWN)
                if name in ('TXLevelPanel','TXProgress0Panel'):
                    original_sprite=re.search(r'm_Sprite: \{fileID: \d+, guid: (\w+)',before)
                    if original_sprite and original_sprite[1] in RESTORE_SPRITES:
                        name_art=RESTORE_SPRITES[original_sprite[1]]
                        b=setfield(b,'m_Sprite',f'{{fileID: 21300000, guid: {GUID(name_art)}, type: 3}}')
                        b=setfield(b,'m_Color',WHITE)
                        b=setfield(b,'m_Type',0)
                        b=setfield(b,'m_Material','{fileID: 0}')
                if ident in INSETS.get(name,{}):
                    b=setfield(b,'m_margin',INSETS[name][ident])
                if name=='TXTooFastHintPanel' and ident in ('114397768327470545','114043919389829319'):
                    b=setfield(b,'m_sharedMaterial',f'{{fileID: 2100000, guid: {GUID("aux-progress-label")}, type: 2}}')
                # Use the original header geometry. Insets avoid live strings
                # touching the thin replacement rim; no amount/string changes.
                if name=='TXChannelPanel' and ident=='114313026266067498':
                    b=setfield(b,'m_margin','{x: 12, y: 6, z: 12, w: 6}')
                assert protect(before)==protect(b),(name,ident,'protected field changed')
                if b!=before:
                    changes.append(dict(prefab=rel,component=ident,before_sha256=hashlib.sha256(before.encode()).hexdigest(),after_sha256=hashlib.sha256(b.encode()).hexdigest()))
                bs[i]=b
            expected=set(IMAGES[name])|set(HIDE.get(name,[]))|set(TITLES.get(name,[]))
            assert expected==touched,(name,expected-touched)
            after=''.join(bs)
            assert protect(baseline)==protect(after),(name,'global structure changed')
            (ROOT/rel).write_text(after,encoding='utf8',newline='')
            audit.append(dict(prefab=rel,protected_content_sha256=hashlib.sha256(protect(after).encode()).hexdigest(),rect_transforms_unchanged=True,hierarchy_unchanged=True,buttons_and_bindings_unchanged=True,live_strings_unchanged=True,modified_components=len([c for c in changes if c['prefab']==rel])))
    (WORK/'aux-prefab-changes.json').write_text(json.dumps(changes,indent=2),encoding='utf8')
    (WORK/'aux-structure-validation.json').write_text(json.dumps(audit,indent=2),encoding='utf8')
    print('Applied',len(changes),'visual component changes in',len(audit),'existing prefabs; all protected bytes unchanged.')

def dump():
    refs={}
    for p in (ROOT/'Assets').rglob('*.meta'):
        m=re.search(r'(?m)^guid: (\w+)',p.read_text(encoding='utf8'))
        if m: refs[m[1]]=p.with_suffix('').relative_to(ROOT).as_posix()
    out=[]
    for n in NAMES:
        raw=(ROOT/f'Assets/Resources/prefabs/panels/{n}.prefab').read_text(encoding='utf8')
        bs={re.match(r'--- !u!(\d+) &(\d+)',b)[2]:b for b in blocks(raw) if b.startswith('---')}
        gos={i:b for i,b in bs.items() if b.startswith('--- !u!1 &')}
        rects={re.search(r'm_GameObject: \{fileID: (\d+)',b)[1]:(i,b) for i,b in bs.items() if b.startswith('--- !u!224 &')}
        def pathname(go):
            name=field(gos[go],'m_Name'); rid,r=rects.get(go,('',''))
            par=re.search(r'm_Father: \{fileID: (\d+)',r)
            if par and par[1]!='0' and par[1] in bs:
                pg=re.search(r'm_GameObject: \{fileID: (\d+)',bs[par[1]])[1]
                return pathname(pg)+'/'+name
            return name
        for i,b in bs.items():
            if not b.startswith('--- !u!114 &'):continue
            go=re.search(r'm_GameObject: \{fileID: (\d+)',b)[1]
            sg=re.search(r'm_Sprite: \{fileID: [-\d]+, guid: (\w+)',b)
            txt=field(b,'m_text',None)
            if sg or txt is not None or 'm_Interactable:' in b:
                rect=rects.get(go,('',''))[1]
                out.append(dict(prefab=n,path=pathname(go),id=i,type='image' if sg else ('text' if txt is not None else 'button'),sprite=refs.get(sg[1]) if sg else None,sprite_guid=sg[1] if sg else None,color=field(b,'m_Color'),font_color=field(b,'m_fontColor'),text=txt,size=field(rect,'m_SizeDelta'),position=field(rect,'m_AnchoredPosition'),scale=field(rect,'m_LocalScale'),image_type=field(b,'m_Type'),active=field(gos[go],'m_IsActive'),enabled=field(b,'m_Enabled'),font=field(b,'m_fontSize'),fontmax=field(b,'m_fontSizeMax'),material=field(b,'m_sharedMaterial')))
    WORK.mkdir(parents=True,exist_ok=True)
    (WORK/'aux-before-components.json').write_text(json.dumps(out,indent=2),encoding='utf8')
    for e in out:
        print(e['prefab'],e['id'],e['path'].replace(e['prefab']+'/',''),e['type'],e['sprite'],e['text'],e['size'],e['color'] or e['font_color'], 'active '+e['active'], 'font '+e['font'])

if __name__=='__main__':
    if '--dump' in sys.argv:dump()
    if '--assets' in sys.argv:import_assets()
    if '--apply' in sys.argv:apply()
