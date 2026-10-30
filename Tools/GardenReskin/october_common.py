"""Deterministic sprite import and serialized-only skin helpers (offline)."""
from pathlib import Path
import hashlib,json,re
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'Docs/GardenReskin/implementation-20261008'
OUT=ROOT/'Assets/Resources/atlas/octobergarden'
GUID=lambda name:hashlib.md5(('october-garden-20261008/'+name).encode()).hexdigest()

def sprite_ref(name):
    return '{fileID: 21300000, guid: '+GUID(name)+', type: 3}'

def write_meta(name,border=(0,0,0,0)):
    OUT.mkdir(exist_ok=True)
    (OUT.with_suffix('.meta')).write_text('fileFormatVersion: 2\nguid: '+GUID('folder')+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    t=(ROOT/'Assets/Resources/atlas/approvedgarden/panel-tool.png.meta').read_text()
    t=re.sub(r'(?m)^guid: .*','guid: '+GUID(name),t)
    t=re.sub(r'spriteID: \w+','spriteID: '+GUID(name+'-sprite'),t)
    t=re.sub(r'spriteBorder: \{.*?\}','spriteBorder: {x: %s, y: %s, z: %s, w: %s}'%tuple(border),t)
    t=t.replace('textureCompression: 2','textureCompression: 0')
    (OUT/(name+'.png.meta')).write_text(t)

def trim(im,cut=24):
    # Bounding-box crop only; preserve all generated alpha within bounds.
    bbox=im.getchannel('A').point(lambda x:255 if x>cut else 0).getbbox()
    return im.crop(bbox) if bbox else im

def sliced_resize(im,size,src_border,dst_border):
    sw,sh=im.size;dw,dh=size
    l,b,r,t=src_border;L,B,R,T=dst_border
    sx=(0,l,sw-r,sw); sy=(0,t,sh-b,sh)
    dx=(0,L,dw-R,dw); dy=(0,T,dh-B,dh)
    out=Image.new('RGBA',size)
    for y in range(3):
        for x in range(3):
            q=im.crop((sx[x],sy[y],sx[x+1],sy[y+1]))
            q=q.resize((dx[x+1]-dx[x],dy[y+1]-dy[y]),Image.Resampling.LANCZOS)
            out.paste(q,(dx[x],dy[y]))
    return out

def import_sprite(name,source,size,border=(0,0,0,0),src_border=None,dst_border=None,crop=None):
    im=Image.open(source).convert('RGBA');im=im.crop(crop) if crop else trim(im)
    if src_border:
        im=sliced_resize(im,size,src_border,dst_border or border)
    else: im=im.resize(size,Image.Resampling.LANCZOS)
    im.save(OUT/(name+'.png'));write_meta(name,border)
    return {'name':name,'source':str(source),'size':size,'border':border,'sha256':hashlib.sha256((OUT/(name+'.png')).read_bytes()).hexdigest()}

def blocks(raw):
    return re.split(r'(?=^--- !u!)',raw,flags=re.M)

def field(b,key,value):
    b,n=re.subn(r'(?m)^  '+re.escape(key)+r': .*','  '+key+': '+str(value),b)
    assert n==1,(key,n)
    return b

def indexed(raw):
    bs=blocks(raw); objects={};gos={};trs={};parents={}
    for b in bs:
        m=re.match(r'--- !u!(\d+) &(\d+)',b)
        if not m: continue
        typ,ident=m.groups();objects[ident]=(typ,b)
        if typ=='1':gos[ident]=re.search(r'(?m)^  m_Name: (.*)',b)[1]
        elif typ in ('224','4'):
            gm=re.search(r'm_GameObject: \{fileID: (\d+)\}',b)
            if not gm:continue
            g=gm[1];trs[ident]=g
            parents[g]=re.search(r'm_Father: \{fileID: (\d+)\}',b)[1]
    def path(g):
        p=parents.get(g,'0');return (path(trs[p])+'/' if p in trs else '')+gos[g]
    ans={}
    for ident,(typ,b) in objects.items():
        g=re.search(r'm_GameObject: \{fileID: (\d+)\}',b)
        if g and g[1] in gos:ans[ident]={'type':typ,'path':path(g[1]),'block':b}
    return ans

def patch_prefab(rel,edits,report_name):
    p=ROOT/rel;raw=p.read_text(encoding='utf8');bs=blocks(raw);before=indexed(raw);report=[]
    for i,b in enumerate(bs):
        h=re.match(r'--- !u!(\d+) &(\d+)',b)
        if not h or h[2] not in edits:continue
        old=b
        for k,v in edits[h[2]].items():b=field(b,k,v)
        bs[i]=b
        if old!=b:report.append({'path':rel,'id':h[2],'node':before[h[2]]['path'],'fields':edits[h[2]]})
    p.write_text(''.join(bs),encoding='utf8',newline='')
    (WORK/report_name).write_text(json.dumps(report,indent=2),encoding='utf8')
    return report
