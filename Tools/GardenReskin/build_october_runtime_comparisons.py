"""Compose evidence only; does not alter approved artwork or game screenshots."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import hashlib
import html
import json

ROOT = Path(__file__).resolve().parents[2]
PREVIEW = ROOT / 'Docs/GardenReskin/regenerated-20261008'
OUT = ROOT / 'Docs/GardenReskin/implementation-20261008'
FONT = 'C:/Windows/Fonts/msyh.ttc'


def font(size):
    return ImageFont.truetype(FONT, size)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fit(canvas, path, box):
    x, y, w, h = box
    with Image.open(path) as source:
        pic = source.convert('RGB')
        ratio = min(w / pic.width, h / pic.height)
        pic = pic.resize((round(pic.width * ratio), round(pic.height * ratio)), Image.Resampling.LANCZOS)
        canvas.paste(pic, (x + (w - pic.width) // 2, y + (h - pic.height) // 2))


def main():
    manifest = json.loads((PREVIEW / 'comparison-manifest.json').read_text('utf8'))
    report = json.loads((OUT / 'runtime/capture-report.json').read_text('utf8'))
    cash_report_path = OUT / 'cash-review/report.json'
    cash_review = json.loads(cash_report_path.read_text('utf8')) if cash_report_path.exists() else None
    if cash_review:
        assert cash_review['status'] == 'PASS', 'Cash state comparison must pass'
    assert report['status'] == 'CAPTURE_COMPLETE' and report['runtimeError'] is None, 'Runtime capture must pass'
    assert {p['id'] for p in report['pages']} == {p['id'] for p in manifest['pages']}, 'All 23 runtime pages required'
    assert all(not t['issues'] for p in report['pages'] for t in p['texts']), 'Unresolved text bounds findings'
    assert all(b['centerRaycastReachesButton'] for p in report['pages'] for b in p['buttons']), 'Unreachable UI Button'
    missing = [p['id'] for p in manifest['pages'] if not (OUT / 'runtime' / (p['id'] + '.png')).exists()]
    if missing:
        raise SystemExit('Missing actual runtime screenshots; no partial gallery emitted: ' + ', '.join(missing))
    target = OUT / 'three-way'
    target.mkdir(exist_ok=True)
    records = []
    for p in manifest['pages']:
        original = PREVIEW / p['sourceCopy']
        effect = PREVIEW / p['effect']
        actual = OUT / 'runtime' / (p['id'] + '.png')
        matched_cash = cash_review is not None and p['id'] == 'cash'
        if matched_cash:
            actual = OUT / 'cash-review/cash-matched-fixture.png'
        assert digest(original) == p['sourceSha256'], 'Original bytes changed: ' + p['id']
        assert digest(effect) == p['effectSha256'], 'Approved preview changed: ' + p['id']
        image = Image.new('RGB', (2220, 1840), '#f7f2e9')
        draw = ImageDraw.Draw(image)
        draw.text((28, 20), f"{p['index']:02d}  {p['title']}", font=font(36), fill='#432a16')
        labels = [p['sourceKind'], '已确认效果图 · AI 生成', 'Unity 运行 · 同状态测试数据' if matched_cash else '替换后实际截图 · Unity 运行']
        for x, label, path in zip((25, 755, 1485), labels, (original, effect, actual)):
            draw.text((x, 82), label, font=font(26), fill='#432a16')
            fit(image, path, (x, 132, 710, 1560))
        draw.text((28, 1720), '三栏均保留完整图像、仅按比例缩放；中栏为AI预览，右栏为真实Unity渲染。', font=font(25), fill='#655748')
        footnote = ('现金页右栏的统计和记录由测试数据驱动；本地入口尚无统计数据，另附无数据状态截图。' if matched_cash else
                    '右栏使用明确的测试账户与关卡数据；金额、计数及背景关卡可能与左侧历史截图不同。')
        draw.text((28, 1765), footnote, font=font(25), fill='#655748')
        path = target / f"{p['index']:02d}-{p['id']}.png"
        image.save(path)
        records.append({**p, 'actual': actual.relative_to(OUT).as_posix(), 'actualSha256': digest(actual),
                        'actualState': 'Matched visual fixture, not live statistics' if matched_cash else 'Runtime fixture',
                        'comparison': path.relative_to(OUT).as_posix()})
    for start in range(0, len(records), 4):
        overview = Image.new('RGB', (2220, 1940), '#eee7db')
        draw = ImageDraw.Draw(overview)
        draw.text((24, 14), f'三图对比 {start // 4 + 1} · 原游戏 / AI效果图 / Unity实际替换', font=font(32), fill='#432a16')
        for n, record in enumerate(records[start:start + 4]):
            fit(overview, OUT / record['comparison'], (20 + (n % 2) * 1100, 80 + (n // 2) * 920, 1080, 900))
        overview.save(OUT / f'three-way-overview-{start // 4 + 1}.jpg', quality=94)
    sections = ''
    for p in records:
        sections += f'<section id="{p["id"]}"><h2>{p["index"]:02d} {html.escape(p["title"])}</h2><a href="{p["comparison"]}"><img loading="lazy" src="{p["comparison"]}"></a>'
        if p['id'] == 'cash' and cash_review:
            sections += ('<p><strong>此页已按相同数据状态重拍。</strong>右栏为原运行组件使用明确测试数据的画面：第1关结算、$0.40、1/1、25553人、1次、Venmo记录。'
                         '这些数值没有写入正式游戏，也不代表统计服务已接通。</p>'
                         '<details><summary>查看当前本地入口：3/5、无统计和记录（已去掉空图标底框）</summary>'
                         '<p>本地入口仍使用缺数据占位，统计显示 --；进度3/5对应60%填充。</p>'
                         '<img style="max-width:720px" loading="lazy" src="cash-review/cash-local-empty.png"></details>')
        sections += '</section>'
    page = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Unity实际换皮三图对比</title>
<style>body{margin:0 auto;padding:24px;max-width:1800px;background:#f7f2e9;color:#432a16;font-family:'Microsoft YaHei',sans-serif}p{line-height:1.7}img{width:100%;height:auto}section{margin:30px 0}a{color:#326327}</style>
<h1>原游戏 / 已确认效果图 / Unity 实际替换</h1><p>2026-10-09 · 23个代表页面。左栏标明历史原图来源，中栏为AI效果图，右栏为重新运行Unity后截取的实际画面。右栏使用测试状态，不代表发生了真实奖励或提现。</p>
<p>各栏原始像素未重绘或修饰，只为对比按比例缩放；点击图片可放大。</p>''' + sections + '</html>'
    (OUT / 'runtime-comparison.html').write_text(page, 'utf8')
    (OUT / 'runtime-comparison-manifest.json').write_text(json.dumps({'count': len(records), 'captureUtc': report['utc'], 'captureReportSha256': digest(OUT / 'runtime/capture-report.json'), 'cashStateReview': cash_review, 'kind': 'historical original / approved AI / current Unity Editor Play Mode', 'pages': records}, ensure_ascii=False, indent=2), 'utf8')
    print(json.dumps({'count': len(records), 'gallery': str(OUT / 'runtime-comparison.html')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
