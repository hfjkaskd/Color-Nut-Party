"""Evidence layout only: copy original pixels, scale proportionally, label sources.

No generated artwork or original screenshot is retouched by this script.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import hashlib, html, json, shutil, zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Docs/GardenReskin/regenerated-20261008'
PAGES = [
    ('main','游戏主界面'), ('startup_loading','启动加载'), ('tutorial','新手教学'),
    ('cash','关卡提现'), ('diamond','钻石提现'), ('account','账户信息'),
    ('account_confirmation','账户确认'), ('channel_contact','其他提现渠道'),
    ('withdrawal_level','提现状态确认'), ('withdrawal_steps','提现处理进度'),
    ('withdrawal_verification','提现验证提示'), ('withdrawal_loading','提现加载'),
    ('withdrawal_guide','欢迎与提现说明'), ('target_complete','目标完成'),
    ('success','通关结算'), ('failure','失败提示'), ('replay','重开确认'),
    ('refresh','刷新道具'), ('undo','撤销道具'), ('add_screw','加螺丝道具'),
    ('reward','自动领取奖励'), ('unlock','新玩法说明'), ('message','通用提示')
]
FONT = 'C:/Windows/Fonts/msyh.ttc'
def f(size): return ImageFont.truetype(FONT, size)
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def fit(canvas, image, box):
    image = image.convert('RGB')
    x,y,w,h = box
    scale = min(w/image.width, h/image.height)
    resized = image.resize((round(image.width*scale),round(image.height*scale)),Image.Resampling.LANCZOS)
    canvas.paste(resized,(x+(w-resized.width)//2,y+(h-resized.height)//2))

def main():
    comparisons=OUT/'comparisons'; originals=OUT/'originals'; thumbs=OUT/'thumbs'
    for folder in [comparisons,originals,thumbs]: folder.mkdir(exist_ok=True)
    records=[]; missing=[]
    for index,(key,title) in enumerate(PAGES,1):
        effect=OUT/f'{key}-effect.png'
        metadata=OUT/f'generation-{key}.json'
        if not metadata.exists(): metadata=OUT/f'{key}-generation.json'
        if not effect.exists() or not metadata.exists(): missing.append(key);continue
        meta=json.loads(metadata.read_text(encoding='utf-8-sig'))
        source=Path(meta['source'])
        original=originals/f'{key}-original.png'
        shutil.copy2(source,original)
        kind=meta.get('sourceKind','')
        kind='原版设备截图' if ('device' in kind.lower() or '原版设备' in kind) else '换皮前 Unity 截图'
        note=''
        if key in ('undo','add_screw'):
            kind='换皮前共享道具弹窗（刷新态）'
            note='这两种道具共用原弹窗结构；新图标题、图标和说明来自现有道具配置。'
        if key=='unlock': note='展示隐藏螺母这一代表状态，其余玩法状态复用同一说明界面。'
        if key=='reward': note='AI横幅中心较原图低约1.6%屏高；实施时保持Prefab原位置，不按AI图移动控件。'
        pic=Image.new('RGB',(1500,1860),'#f7f2e9'); draw=ImageDraw.Draw(pic)
        draw.text((32,22),f'{index:02d}  {title}',font=f(36),fill='#432a16')
        draw.text((32,84),kind,font=f(25),fill='#655748')
        draw.text((775,84),'新效果图 · AI 生成 · 尚未实施',font=f(25),fill='#326327')
        with Image.open(original) as im: source_size=im.size;fit(pic,im,(30,134,704,1600))
        with Image.open(effect) as im: effect_size=im.size;fit(pic,im,(766,134,704,1600))
        draw.text((32,1760),'浅木 / 奶油 / 绿色 · 减少植物装饰 · 保留原控件与内容含义',font=f(23),fill='#655748')
        draw.text((32,1801),note or '左图保留原始像素；仅按比例缩放排版。右图为美术预览，不是游戏运行截图。',font=f(21),fill='#655748')
        target=comparisons/f'{index:02d}-{key}-comparison.png';pic.save(target)
        thumb=pic.copy();thumb.thumbnail((660,820),Image.Resampling.LANCZOS);thumb.save(thumbs/f'{key}.jpg',quality=91)
        records.append(dict(index=index,id=key,title=title,source=str(source),sourceKind=kind,sourceSha256=sha(source),
            sourceCopy=original.relative_to(OUT).as_posix(),sourceSize=source_size,effect=effect.relative_to(OUT).as_posix(),
            effectSha256=sha(effect),effectSize=effect_size,comparison=target.relative_to(OUT).as_posix(),note=note))
    (OUT/'comparison-manifest.json').write_text(json.dumps({'date':'2026-10-08','planned':23,'completed':len(records),'missing':missing,
        'mode':'Built-in ImageGen previews; originals unchanged; no Unity replacement','pages':records},ensure_ascii=False,indent=2),encoding='utf8')
    # Overview sheets: six small, individually labeled comparisons per sheet.
    for start in range(0,len(records),6):
        sheet=Image.new('RGB',(2040,1800),'#eee7db');d=ImageDraw.Draw(sheet)
        d.text((24,14),f'界面对比总览 {start//6+1} · 左：原界面 / 右：AI效果图',font=f(30),fill='#432a16')
        for n,r in enumerate(records[start:start+6]):
            with Image.open(thumbs/f'{r["id"]}.jpg') as im: sheet.paste(im,(20+(n%3)*675,80+(n//3)*850))
        sheet.save(OUT/f'overview-{start//6+1}.jpg',quality=94)
    nav=''.join(f'<a href="#{r["id"]}">{r["index"]:02d} {html.escape(r["title"])}</a>' for r in records)
    body=''
    for r in records:
        body+=f'''<section id="{r['id']}"><h2>{r['index']:02d} {html.escape(r['title'])}</h2>
<p class="note">{html.escape(r['note'])}</p><div class="pair">
<figure><figcaption>{r['sourceKind']}</figcaption><a href="{r['sourceCopy']}"><img loading="lazy" src="{r['sourceCopy']}"></a></figure>
<figure><figcaption>新效果图 · AI 生成 · 尚未实施</figcaption><a href="{r['effect']}"><img loading="lazy" src="{r['effect']}"></a></figure>
</div><p><a href="{r['comparison']}">打开完整左右对比图</a> · <a href="{r['effect']}">单张效果图</a> · <a href="#top">回到目录</a></p></section>'''
    page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Nut Sort Relax · 全界面效果图对比</title><style>
*{box-sizing:border-box}body{margin:0;background:#f7f2e9;color:#412b1b;font-family:'Microsoft YaHei',sans-serif}
header,main{max-width:1260px;margin:auto;padding:24px}h1{font-size:30px}p{line-height:1.7}nav{display:flex;flex-wrap:wrap;gap:8px}a{color:#286333}nav a{padding:8px 12px;border:1px solid #d6c4ac;border-radius:7px;text-decoration:none;background:#fffaf1}
section{padding:22px 0 32px;border-top:1px solid #d6c4ac}.pair{display:grid;grid-template-columns:1fr 1fr;gap:22px}figure{margin:0}figcaption{padding:12px;background:#eee1cd;font-weight:bold;border-radius:7px 7px 0 0}.pair img{width:100%;height:auto;display:block}.note{color:#785d42}h2{font-size:24px}footer{padding:24px;color:#785d42}html{scroll-behavior:smooth}
</style><header id="top"><h1>Nut Sort Relax · 全界面效果图对比</h1>
<p>2026-10-08 · 浅木色、奶油色和绿色 · 大幅减少树叶、花朵等装饰。以下均为新生成的美术预览，尚未替换 Unity 工程。</p>
<p>左侧按来源标明“原版设备截图”或“换皮前 Unity 截图”；右侧均为 AI 效果图。金额和地区沿用各自原图，不把不同状态冒充同一运行结果。点击任一图片可放大查看。</p><nav>'''+nav+'</nav></header><main>'+body+'''</main><footer>23 张代表页面；同一界面的金额/地区变化不重复出图。设置入口没有独立设置 Prefab，本轮不凭空新增页面。AI 图只用于美术确认；实际替换仍以现有 Prefab 为准。</footer></html>'''
    (OUT/'index.html').write_text(page,encoding='utf8')
    if not missing:
        with zipfile.ZipFile(OUT/'全部23页效果图与原图对比.zip','w',compression=zipfile.ZIP_STORED) as z:
            for path in sorted(OUT.rglob('*')):
                if path.is_file() and path.suffix.lower() in ('.png','.jpg','.json','.txt','.html','.md'):
                    z.write(path,path.relative_to(OUT))
    print(json.dumps({'completed':len(records),'missing':missing,'gallery':str(OUT/'index.html')},ensure_ascii=False))
if __name__=='__main__': main()
