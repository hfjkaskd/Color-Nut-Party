"""Apply approved 4 core modal families on Oct8 baseline, visual fields only."""
from october_common import *
import zipfile
WHITE='{r: 1, g: 1, b: 1, a: 1}'
BROWN='{r: 0.30, g: 0.12, b: 0.025, a: 1}'
PLAIN='{fileID: 2100000, guid: f8d75af0502c5ce4e833fe267934fc89, type: 2}'
TITLE='{fileID: 2100000, guid: dcf27bde9acc4e7d2f963953da8562cf, type: 2}'
def art(n,mode=0):return {'m_Sprite':sprite_ref(n),'m_Material':'{fileID: 0}','m_Color':WHITE,'m_Type':mode}
def text(outline=False):return {'m_fontColor':BROWN,'m_sharedMaterial':TITLE if outline else PLAIN}
HIDE={'m_Color':'{r: 1, g: 1, b: 1, a: 0}'}

import_sprite('diamond-panel',WORK/'sources/diamond-panel.png',(1048,1801))
import_sprite('video-ad',WORK/'sources/original-tooloffer-ad.png',(113,101))
import_sprite('amount-normal',WORK/'sources/amount-normal.png',(365,221),(45,45,45,45))
im=trim(Image.open(WORK/'sources/cash-card.png').convert('RGBA')).resize((850,329),Image.Resampling.LANCZOS)
p=Image.new('RGBA',(850,369));p.paste(im,(0,40));p.save(OUT/'cash-card.png');write_meta('cash-card')
# Keep original title node width while matching its central wooden plaque footprint.
im=Image.open(OUT/'title.png').convert('RGBA').resize((770,163),Image.Resampling.LANCZOS)
p=Image.new('RGBA',(975,163));p.paste(im,(102,0));p.save(OUT/'cash-heading.png');write_meta('cash-heading')
# Stats header keeps the same quarter-height wood area without squeezing its labels.
im=Image.open(OUT/'panel-heading.png').convert('RGBA')
im=sliced_resize(im,(968,334),(64,64,64,235),(32,32,32,105))
im.save(OUT/'stats-panel.png');write_meta('stats-panel')

MAP={
'TXPanel':{
'114841364053301648':art('panel-heading'),
'114833703512093738':art('cash-card'),
'114694121525821480':art('cash-heading'),'114808113985169905':art('cash-heading'),'114726790013014859':art('cash-heading'),
'114733206282342372':text(True),'114472185465350827':text(True),'114801075752484240':text(True),
'114571718729274231':art('close'),'114572839542275309':art('primary-button',1),
'114370222726513266':art('progress-track',1),
'114393105877652414':{'m_Sprite':sprite_ref('progress-fill'),'m_Material':'{fileID: 0}'},
'114534704304255049':art('input',1),'114431740505123533':text(),
'114344419225485095':HIDE,'114703931505913403':HIDE,
'114704330047573733':text(),'114699617135816954':text(),
},
'TXUserInfoPanel':{
'114220653531496868':art('panel-heading'),'114291844617409189':HIDE,
'114693329660128220':art('title',1),'114397768327470545':text(True),
'114689364072382846':art('primary-button',1),'114477046003934300':art('aux-blue-close'),
'114384403120170545':art('input',1),'114571878917323452':art('input',1),'114121005239445321':art('input',1),'114291632710222552':art('input',1),
'114107629493147155':text(),'114719342369648492':text(True),
'114589652054545887':art('help'),
},
'ItemGetPanel':{
'114621510681728303':art('panel',1),'114302127217294661':HIDE,'114946936116752871':HIDE,
'114587397909062814':art('ribbon'),'114694121525821480':text(True),
'114733206282342372':art('primary-button',1),'114841364053301648':art('close'),
'114472185465350827':art('video-ad'),
'114924050743934937':text(),'114632746491512912':art('input',1),
},
'TXCoinPanel':{
'114060945828524476':art('diamond-panel'),
'114397768327470545':HIDE,'114477046003934300':text(True),
'114967328832014496':art('cream-ribbon'),'114919942235722503':art('cream-card',1),
'114632746491512912':HIDE,'114436073260064126':art('close'),
'114043919389829319':text(),'114149520701623706':text(),'114630643969539439':text(),
'114393105877652414':art('amount-selected',1),'114431740505123533':art('amount-normal',1),'114704330047573733':art('amount-normal',1),'114880619614767448':art('amount-normal',1),
'114552309709192268':text(),'114264357844037074':text(),'114354824845356902':text(),'114601984538237284':text(),
'114240571728440943':art('primary-button',1),'114132862189046193':text(),
'114386943702330414':art('cream-card',1),'114561126880652180':text(),'114695600491528212':text(),
'114187454977326620':art('progress-track',1),
'114592460329389196':{'m_Sprite':sprite_ref('progress-fill'),'m_Material':'{fileID: 0}'},
'114217368724061974':{'selectedSpritePath':'Atlas/octobergarden/amount-selected','normalSpritePath':'Atlas/octobergarden/amount-normal'},
}}
allreport=[]
with zipfile.ZipFile(WORK/'before-implementation.zip') as z:
 for name,edits in MAP.items():
  rel='Assets/Resources/prefabs/panels/'+name+'.prefab'
  raw=z.read(rel).decode('utf8');bs=blocks(raw);seen=set()
  for i,b in enumerate(bs):
   h=re.match(r'--- !u!(\d+) &(\d+)',b)
   if not h or h[2] not in edits:continue
   seen.add(h[2])
   for k,v in edits[h[2]].items():b=field(b,k,v)
   bs[i]=b
  assert seen==set(edits),(name,set(edits)-seen)
  result=''.join(bs);(ROOT/rel).write_text(result,encoding='utf8',newline='')
  allreport.append({'prefab':rel,'visualChanges':edits,'baselineSha256':hashlib.sha256(raw.encode()).hexdigest()})
 # Stats retain already expanded text rects and no column card visuals.
 for name in ['PlayerInfo','PMDSimple']:
  rel='Assets/Resources/prefabs/playerinfo/'+name+'.prefab';raw=z.read(rel).decode('utf8');idx=indexed(raw)
  edits={'114977153420339851':art('stats-panel')} if name=='PlayerInfo' else {'114589667821646929':art('input',1),'114632457200066912':HIDE}
  if name=='PlayerInfo':
   for ident,v in idx.items():
    if 'm_text:' not in v['block']:continue
    # Label headers (numeric values keep semantic green/purple/orange).
    if '/label_' in v['path'] or 'Title' in v['path'] or v['path'].endswith('/Time'):edits[ident]=text()
  bs=blocks(raw)
  for i,b in enumerate(bs):
   h=re.match(r'--- !u!(\d+) &(\d+)',b)
   if h and h[2] in edits:
    for k,v in edits[h[2]].items():b=field(b,k,v)
    bs[i]=b
  (ROOT/rel).write_text(''.join(bs),encoding='utf8',newline='')
  allreport.append({'prefab':rel,'visualChanges':edits})
(WORK/'core-prefab-changes.json').write_text(json.dumps(allreport,indent=2),encoding='utf8')
print('Applied core visual changes to',len(allreport),'prefabs. RectTransforms and runtime bindings untouched.')
