from pathlib import Path
from collections import defaultdict, Counter, deque
from PIL import Image
import re, json, hashlib, datetime, sys
sys.stdout.reconfigure(encoding='utf-8')

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'Assets'
OUT = Path(__file__).parent
rel = lambda p: p.relative_to(ROOT).as_posix()
meta, guid_path, path_guid = {}, {}, {}
for p in ASSETS.rglob('*.meta'):
    text = p.read_text(encoding='utf-8-sig', errors='replace')
    m = re.search(r'^guid: (\w+)', text, re.M)
    if m:
        r = rel(p.with_suffix('')); meta[r] = text
        guid_path[m[1]] = r; path_guid[r] = m[1]

def field(s, key, default=None):
    m = re.search(r'^\s*' + re.escape(key) + r':\s*(.+)$', s, re.M)
    return m[1].strip() if m else default

def vec(s, key):
    return {k: float(v) for k, v in re.findall(r'(\w+):\s*([-+\deE.]+)', field(s, key, ''))}

def resource_key(r):
    return r.split('/Resources/', 1)[1].rsplit('.', 1)[0] if '/Resources/' in r else None

texts, reverse, sprites, consumers, dynamic = {}, defaultdict(set), defaultdict(list), defaultdict(list), []
for p in ASSETS.rglob('*'):
    if not p.is_file() or p.suffix not in {'.prefab', '.unity', '.asset', '.mat', '.cs'}: continue
    try: s = p.read_text(encoding='utf-8-sig')
    except (UnicodeError, OSError): continue
    r = rel(p); texts[r] = s
    if p.suffix != '.cs':
        for g in set(re.findall(r'guid: ([0-9a-f]{32})', s)):
            if g in guid_path: reverse[guid_path[g]].add(r)
    for ln, line in enumerate(s.splitlines(), 1):
        if re.search(r'(?i)atlas/|game/spirte/', line) or 'Resources.Load<Sprite>' in line:
            dynamic.append({'file': r, 'line': ln, 'text': line.strip()})
    if p.suffix == '.asset' and re.search(r'^Sprite:', s, re.M):
        tx = re.search(r'^\s+texture: \{fileID: ([^,}]+), guid: (\w+)', s, re.M)
        if tx and tx[2] in guid_path:
            a = {'path': r, 'guid': path_guid.get(r), 'file_id': int(re.search(r'--- !u!213 &(\d+)', s)[1]), 'name': field(s, 'm_Name'), 'border': vec(s, 'm_Border'), 'pivot': vec(s, 'm_Pivot'), 'pixels_per_unit': field(s, 'm_PixelsToUnits'), 'texture_guid': tx[2], 'texture_file_id': tx[1]}
            for key in ['m_Rect', 'textureRect']:
                m = re.search(r'^\s+' + key + r':\n((?:\s+[^\n]*\n){1,8})', s, re.M)
                if m: a[key] = {k: float(v) for k, v in re.findall(r'^\s+(x|y|width|height): ([\deE.+-]+)', m[1], re.M)}
            sprites[guid_path[tx[2]]].append(a)
    if p.suffix not in {'.prefab', '.unity'}: continue
    blocks = {i: (t, b) for t, i, b in re.findall(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', s, re.M | re.S)}
    gos = {i: field(b, 'm_Name', '?') for i, (t, b) in blocks.items() if t == '1'}
    ts = {i: b for i, (t, b) in blocks.items() if t in {'4', '224'} and re.search(r'm_GameObject: \{fileID: (\d+)', b)}
    gts = {re.search(r'm_GameObject: \{fileID: (\d+)', b)[1]: i for i, b in ts.items()}
    def node(tid, seen=None):
        b = ts.get(tid); seen = seen or set()
        if not b or tid in seen: return ''
        seen.add(tid); g = re.search(r'm_GameObject: \{fileID: (\d+)', b)[1]
        par = re.search(r'm_Father: \{fileID: (\d+)', b)
        head = node(par[1], seen) if par and par[1] != '0' else ''
        return (head + '/' if head else '') + gos.get(g, '?')
    for bid, (typ, b) in blocks.items():
        for m in re.finditer(r'^\s+m_Sprite: \{fileID: ([^,}]+), guid: (\w+)', b, re.M):
            target = guid_path.get(m[2])
            if not target: continue
            go = re.search(r'm_GameObject: \{fileID: (\d+)', b)
            consumers[target].append({'file': r, 'component_file_id': bid, 'node': node(gts.get(go[1], '')) if go else '', 'sprite_guid': m[2], 'sprite_file_id': m[1], 'image_type': field(b, 'm_Type'), 'use_sprite_mesh': field(b, 'm_UseSpriteMesh'), 'sprite_source_kind': 'standalone_sprite_asset' if target.endswith('.asset') else 'imported_texture_sprite', 'referenced_asset': target})

def ancestors(r, suffixes):
    seen = {r}; queue = deque([r]); result = []
    while queue:
        for ref in reverse.get(queue.popleft(), []):
            if ref in seen: continue
            seen.add(ref); queue.append(ref)
            if Path(ref).suffix in suffixes: result.append(ref)
    return sorted(result)

def classify(r):
    n = Path(r).stem.lower(); p = r.lower()
    if 'sdf atlas' in n or '/textmesh pro/' in p:
        return '遮罩/字体/技术贴图', 'preserve_technical', '字体距离场/技术纹理，禁止主题滤色或重绘。', False
    if '/cubemap/' in p:
        return '遮罩/字体/技术贴图', 'preserve_technical', '反射Cubemap影响整体世界光照/金属感；保留现有采样，不当背景图重绘。', False
    if '/atlas/pay/' in p or '/atlas/paysimple/' in p:
        return '奖励/支付logo', 'preserve_semantic', '支付品牌标志保留原识别、拼写和比例。', False
    if '/atlas/golds/' in p or n in {'coin', 'coin2', 'coin_max', 'people'}:
        return '奖励/支付logo', 'preserve_semantic', '地区钞票、粉钻、MAX与人物身份含义必须保留；可直接复用。', False
    if n in {'hide', 'hide_0', 'nut_stand_normal_03'} or (n == 'mask' and '/texture2d/' in p):
        return '遮罩/字体/技术贴图', 'preserve_technical', '遮罩/隐藏色/法线参与玩法或渲染；不做全图主题化。', False
    if '/resources/game/spirte/bg.' in p or n in {'bg_2', 'bg'} and '/texture2d/' in p:
        return '背景', 'replace_theme', '低饱和浅木纹/花园背景；沿用尺寸、UV、alpha和引用。', True
    if '/resources/game/spirte/' in p and (n.isdigit() or n in {'guide_correct', 'guide_error', 'miaozi'}):
        return '玩法纹理', 'preserve_gameplay_semantic', '原12色悬挂螺母/正确错误标记/交互覆盖；保留颜色映射与状态轮廓，可直接复用。', False
    if '/resources/game/spirte/' in p and n in {'mask', 'mask1', 'mask_done', 'stone', 'alci'}:
        return '玩法纹理', 'restyle_preserve_gameplay', '这是可见玩法遮挡罩/石块/特殊杆图形，不是alpha技术mask；可调整紫色装饰以融入主题，但保持遮挡范围、解锁状态与透明轮廓。', True
    if '/resources/game/' in p or n in {'nut_stand_color', 'screwhide', 'donemove', 'done', 'undone', 'wenhao', 'ls'}:
        return '玩法纹理', 'restyle_preserve_gameplay', '保持12色、隐藏问号、锁定/完成状态及UV和alpha；基底可木质化，数字/问号不改。', n not in {'wenhao', 'donemove', 'done', 'undone'}
    if '/effects/' in p or n in {'glow1', 'star', 'tx_glow', 'title_2'}:
        return '遮罩/字体/技术贴图', 'preserve_effect', '粒子辉光/星光/柔边alpha可复用，避免纹理扩容。', False
    if '/atlas/items/' in p:
        return '功能icon', 'restyle_preserve_semantics', '保留撤销/交换/加螺杆图形与状态，可调整材质/描边。', True
    if n in {'replay', 'setting', 'close', 'ad', 'ads', 'item_add', 'duigou', 'max', '？', 'hand', 'house', 'logo_android'}:
        if n == 'logo_android': return '标题装饰', 'restyle_preserve_semantics', '产品Logo可调整外观，保持产品身份/原文字，不写入动态数值。', True
        return '功能icon', 'restyle_preserve_semantics', '保留功能、广告、加号、帮助或勾选识别；MAX/广告文字不可改义。', n not in {'ad', 'ads', 'duigou', 'max', '？', 'hand'}
    if n.startswith('title') or n == 'bg2_2' or n in {'playergoldgethint_bg', 'targetreward_bg'}:
        return '标题装饰', 'replace_theme', '标题底/装饰改为奶油木框；钞票/钻石装饰保持原语义，文字仍由TMP显示。', True
    if n.startswith('btn') or n == 'goldget': return '按钮', 'replace_theme', '仅替换既有按钮sprite外观；保留Button、状态、尺寸与绑定。', True
    if n.startswith('bar') or n in {'tx_lv_bar', 'tx_lv_bar_bg', 'tx_bar', 'tx_bar_bg', 'loading', 'loading1', 'dian', 'dian1', 'line', 'line2'}:
        return '面板/框', 'replace_theme', '保留进度填充方向、透明边缘和当前/未达成状态对比。', True
    if n.startswith('bg') or n.endswith('_bg') or n.startswith('hintlevel') or n in {'kuang', 'item_bg1', 'tx_bg1', 'tx_bg', 'tx_dj_bg', 'tip_bg', 'tip_bg_0', 'item_count_bg', 'item_bg2', 'item_bg1_1'}:
        return '面板/框', 'replace_theme', '奶油内容面与木质边框；保留边界、切片、圆角和文字安全区。', True
    return '待人工复核', 'review', '根据实际Prefab节点确认，不依据通用文件名直接覆盖。', False

items, sha_groups = [], defaultdict(list)
for p in sorted(p for p in ASSETS.rglob('*') if p.is_file() and p.suffix.lower() in {'.png', '.jpg', '.jpeg', '.tga', '.psd'}):
    r = rel(p); mt = meta.get(r, '')
    with Image.open(p) as im:
        w, h = im.size; mode = im.mode; has_alpha = 'A' in im.getbands() or 'transparency' in im.info
        alpha = im.convert('RGBA').getchannel('A'); hist = alpha.histogram()
        ai = {'channel_present': has_alpha, 'min': next(i for i, x in enumerate(hist) if x), 'max': max(i for i, x in enumerate(hist) if x), 'transparent_pixels': hist[0], 'partial_alpha_pixels': sum(hist[1:255]), 'opaque_pixels': hist[255], 'nonzero_bbox': list(alpha.getbbox()) if alpha.getbbox() else None, 'transparent_fraction': round(hist[0] / (w * h), 6), 'partial_fraction': round(sum(hist[1:255]) / (w * h), 6)}
    sha = hashlib.sha256(p.read_bytes()).hexdigest(); sha_groups[sha].append(r)
    cat, action, reason, candidate = classify(r); ss = sprites.get(r, []); nodes = []
    for src in [r] + [a['path'] for a in ss]: nodes += consumers.get(src, [])
    key = resource_key(r)
    candidates = [q for q in texts if resource_key(q) and resource_key(q).lower() == key.lower()] if key else []
    evidence = []
    if key:
        for e in dynamic:
            t = e['text'].lower(); low = r.lower()
            if key.lower() in t or any(prefix in low and prefix.replace('/resources/', '') in t for prefix in ['/resources/atlas/golds/', '/resources/atlas/paysimple/', '/resources/atlas/pay/']): evidence.append(e)
            elif '/atlas/items/' in low and 'itemprefix: atlas/items/' in t: evidence.append(e)
    importer = {k: field(mt, k) for k in ['spriteMode', 'spriteMeshType', 'spritePixelsToUnits', 'spriteBorder', 'spritePivot', 'textureType', 'alphaIsTransparency', 'isReadable', 'enableMipMap', 'sRGBTexture', 'maxTextureSize', 'textureFormat', 'filterMode', 'wrapU', 'wrapV']}
    platforms = []
    for b in re.split(r'(?m)^  - serializedVersion:', mt)[1:]:
        if field(b, 'buildTarget'): platforms.append({k: field(b, k) for k in ['buildTarget', 'maxTextureSize', 'textureFormat', 'textureCompression', 'compressionQuality', 'overridden']})
    items.append({'path': r, 'absolute_path': str(p), 'scope': 'requested_ui_roots' if r.startswith(('Assets/Resources/atlas/', 'Assets/Texture2D/')) else 'additional_project_texture', 'width': w, 'height': h, 'mode': mode, 'file_bytes': p.stat().st_size, 'sha256': sha, 'texture_guid': path_guid.get(r), 'alpha': ai, 'category': cat, 'action': action, 'theme_replacement_candidate': candidate, 'rationale': reason, 'importer': importer, 'platform_settings': platforms, 'standalone_sprite_assets': ss, 'nonzero_nine_slice': any(any(v != 0 for v in b.values()) for b in [vec(mt, 'spriteBorder')] + [a['border'] for a in ss]), 'resources_key': key, 'same_resource_key_asset_candidates': candidates, 'resources_same_name_sprite_ambiguity': bool(candidates and field(mt, 'spriteMode') not in {None, '0'}), 'dynamic_resource_evidence': evidence, 'direct_referrers': sorted(reverse.get(r, [])), 'prefab_scene_sprite_consumers': sorted(nodes, key=lambda x: (x['file'], x['node'])), 'referencing_prefabs_transitive': ancestors(r, {'.prefab'}), 'referencing_scenes_transitive': ancestors(r, {'.unity'}), 'referencing_materials': ancestors(r, {'.mat'}), 'referencing_nonprefab_assets': ancestors(r, {'.asset'})})

counts = Counter(i['category'] for i in items); actions = Counter(i['action'] for i in items)
duplicates = [{'sha256': s, 'paths': ps} for s, ps in sha_groups.items() if len(ps) > 1]
summary = {'texture_count': len(items), 'requested_ui_texture_count': sum(i['scope'] == 'requested_ui_roots' for i in items), 'additional_texture_count': sum(i['scope'] != 'requested_ui_roots' for i in items), 'category_counts': dict(counts), 'action_counts': dict(actions), 'theme_replacement_candidate_count': sum(i['theme_replacement_candidate'] for i in items), 'textures_with_standalone_sprite_assets': sum(bool(i['standalone_sprite_assets']) for i in items), 'textures_with_nonzero_nine_slice': sum(i['nonzero_nine_slice'] for i in items), 'same_resource_key_candidate_texture_count': sum(i['resources_same_name_sprite_ambiguity'] for i in items), 'identical_png_groups': len(duplicates)}
notes = [
    '本清单为磁盘静态审计，不等同当前运行中可见性或功能验收。',
    '默认入口 LuoSiSortGame.unity；OriginalMainLevel.asset 加载 MainPanel 与 LocalGameplay，后者加载 LocalMainPanelTop、LocalSubrounds 并嵌套 MainPanelBottom。MainPanelComplete 是恢复/验证组合，不能代替默认入口。',
    '两类引用：Prefab Image.m_Sprite -> 独立 Sprite .asset GUID -> m_RD.texture -> PNG GUID；或直接指向 PNG.meta 导入Sprite GUID/fileID。逐项区分，不可只看同名PNG。',
    '9-slice 以实际引用Sprite的 m_Border 为准；PNG.meta.spriteBorder 是导入Sprite的边框，两者可能不同。',
    'Resources 同路径可能同时有 .asset Sprite 与PNG导入Sprite。静态审计仅列候选，不断言运行时选择顺序；同尺寸替换原PNG可作用于两者，避免换GUID/删.asset。',
    '保持PNG尺寸、GUID、alpha轮廓、Sprite rect/pivot/border/custom mesh、Prefab层级/组件/位置/按钮/交互。主题纹理建议原位替换PNG内容，不把动态TMP文字画入图。',
    '现金/地区钞票、粉钻、支付品牌、广告符号、问号、工具含义保留；可复用语义素材。字体SDF/法线/遮罩和技术效果不做木纹重绘。',
    '相同PNG哈希只证明当前像素相同；副本有各自GUID和用途，修改须覆盖各引用，不能删除或合并引用。',
    '类别结合路径、原素材接触表和引用关系整理；review项须再看原图/节点。direct consumers记录Image/SpriteRenderer的sprite槽，材料/变体的间接关系在transitive字段。无静态Prefab引用不代表绝对不使用，还需看动态路径证据。',
    'Resources/game/spirte/Mask.png 是紫色可见玩法罩，Texture2D/mask.png 才属于技术遮罩；不得只按文件名Mask批量排除或木纹化。',
    '性能建议：复用原分辨率、压缩和不可读设置，无需4K/新装饰节点。1024x2048 RGBA32约8MiB，原背景ASTC10x10约0.32MiB；保持现有压缩路线。'
]
payload = {'schema_version': 1, 'created_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'project_root': str(ROOT), 'audit_scope': 'All project textures; requested Resources/atlas and Texture2D roots tagged separately. Static dependency graph and direct prefab/scene Sprite slots; no asset/runtime modifications.', 'summary': summary, 'notes': notes, 'runtime_sprite_loader_and_path_evidence': dynamic, 'identical_content_groups': duplicates, 'assets': items}
(OUT / 'asset-inventory.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding='utf-8')
md = ['# 花园木质换皮素材审计', '', f'工程：`{ROOT}`。审计时间：{payload["created_utc"]}。仅新增本目录审计文件；未修改纹理、Prefab、场景、材质或代码。', '', f'共 **{len(items)}** 张项目纹理；指定UI目录 **{summary["requested_ui_texture_count"]}** 张，其他玩法/效果/技术贴图 **{summary["additional_texture_count"]}** 张。主题替换候选 **{summary["theme_replacement_candidate_count"]}** 张。', '', '## 引用与实施约束', '']
md += ['- ' + n for n in notes]
md += ['', '## 分类统计', '', '| 类别 | 数量 |', '|---|---:|'] + [f'| {k} | {v} |' for k, v in sorted(counts.items())]
md += ['', f'独立Sprite关联纹理 {summary["textures_with_standalone_sprite_assets"]}；非零9-slice {summary["textures_with_nonzero_nine_slice"]}；Resources同名Sprite候选 {summary["same_resource_key_candidate_texture_count"]}；相同像素副本组 {len(duplicates)}。', '', '## 全量纹理清单', '', '动作：replace_theme=主题替换；restyle_preserve_semantics=保留功能语义调外观；restyle_preserve_gameplay=受玩法状态约束；preserve_semantic=可复用语义资产；preserve_technical/effect=不改；review=先人工确认。', '', '| 路径 | 尺寸 | alpha透明/半透明% | 9-slice | 分类 / 动作 | Sprite资产数 | 关联Prefab |', '|---|---:|---:|---|---|---:|---|']
for i in items:
    prefs = ', '.join(Path(p).stem for p in i['referencing_prefabs_transitive']) or '—（见动态/材质引用）'
    md.append(f'| `{i["path"]}` | {i["width"]}×{i["height"]} | {i["alpha"]["transparent_fraction"]*100:.1f}/{i["alpha"]["partial_fraction"]*100:.1f} | {"有" if i["nonzero_nine_slice"] else "无"} | {i["category"]}<br>`{i["action"]}` | {len(i["standalone_sprite_assets"])} | {prefs} |')
md += ['', '## 非零9-slice边框', '', '| PNG | 导入Sprite边框 | 独立Sprite边框 |', '|---|---|---|']
for i in items:
    if i['nonzero_nine_slice']:
        borders = '<br>'.join(f'`{s["path"]}`: {s["border"]}' for s in i['standalone_sprite_assets'])
        md.append(f'| `{i["path"]}` | `{i["importer"]["spriteBorder"]}` | {borders} |')
md += ['', '## 相同像素副本', '', '文件内容相同但GUID不同，保留各自引用路径。', '']
for group in duplicates: md.append('- ' + '；'.join('`' + r + '`' for r in group['paths']))
md += ['', '## 机器可读明细', '', '`asset-inventory.json`逐张记录尺寸/哈希、alpha范围/包围盒、PNG.meta参数/平台压缩、纹理GUID、独立Sprite GUID/fileID/rect/border/pivot、直接Image节点、传递Prefab/Scene/Material关联、Resources同名候选和动态路径证据。', '']
(OUT / 'asset-inventory.md').write_text('\n'.join(md), encoding='utf-8')
print(json.dumps(summary, ensure_ascii=False, indent=2))
print('REVIEW:', [i['path'] for i in items if i['action'] == 'review'])
