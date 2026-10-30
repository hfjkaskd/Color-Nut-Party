"""Package immutable render evidence and audited implementation metadata."""
from pathlib import Path
from PIL import Image
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Docs/GardenReskin/implementation-20261008'


def main():
    captures = json.loads((OUT / 'runtime/capture-report.json').read_text('utf8'))
    audit = json.loads((OUT / 'global-structure-audit.json').read_text('utf8'))
    assert captures['status'] == 'CAPTURE_COMPLETE' and len(captures['pages']) == 23
    assert audit['status'] == 'PASS'
    checks = [
        ('all-20261009-004505.log', 'OCTOBER_SKIN_CAPTURE_PASS 23'),
        ('interaction-20261009-003106.log', 'APPROVED_SKIN_PLAY_PASS US 720x1600'),
        ('interactionwide-20261009-004817.log', 'APPROVED_SKIN_PLAY_PASS US 720x1280'),
        ('teaching-view-20261009.log', 'NUT_LOCAL_TEACHING_VIEW_PASS'),
        ('teaching-click-20261009.log', 'NUT_LOCAL_TEACHING_CLICK_FEEDBACK_PASS'),
    ]
    for name, marker in checks:
        assert marker in (OUT / 'validation' / name).read_text('utf8'), name
    inventory = []
    for path in sorted((ROOT / 'Assets/Resources/atlas/octobergarden').glob('*.png')):
        with Image.open(path) as im:
            size = list(im.size)
        inventory.append({'file': path.relative_to(ROOT).as_posix(), 'size': size,
                          'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'bytes': path.stat().st_size})
    (OUT / 'final-sprite-inventory.json').write_text(json.dumps(inventory, indent=2), 'utf8')
    result = {
        'date': '2026-10-09', 'project': str(ROOT), 'pages': 23,
        'implementedIn': 'Existing Unity Prefabs, sprite references, serialized presentation parameters and existing base material',
        'newSpriteCount': len(inventory), 'newSpriteDiskBytes': sum(p['bytes'] for p in inventory),
        'runtimeCaptureUtc': captures['utc'], 'capturedTexts': sum(len(p['texts']) for p in captures['pages']),
        'buttonCenterRaycasts': sum(len(p['buttons']) for p in captures['pages']),
        'actualAllClickDispatches': captures['buttonClickDispatches'],
        'interactionSuites': '20 real Button dispatches at each of 720x1600 and 720x1280',
        'teaching': '4 explicit teaching seeds hide overlapping ordinary HUD; Level 2 restores ordinary HUD; world selection and current-instruction feedback checked',
        'structureAudit': audit['scope'], 'structureErrors': len(audit['errors']),
        'structureWarnings': '38 TMP float/byte serialized color cache differences; live float colors verified in runtime images',
        'productionCodeCorrections': ['Preserve serialized unlock backdrop tint', 'Prevent pool reparenting during scene destruction', 'Hide ordinary diamond/milestone row during teaching and restore it afterward'],
        'unchanged': ['Prefab hierarchy and RectTransforms', 'Button components, target graphics and code-bound events', 'Dynamic/localized text content', 'Reward amounts, economic configuration and gameplay rules'],
        'validationLogs': [name for name, _ in checks],
        'limits': ['Editor Play Mode captures, not Android device captures', 'No production payment or ad SDK calls; fixture callbacks only', 'Text boundary checks cover shown states and tested dimensions, not every translation or possible balance', 'Raycast reachability is not a business-logic test for every one of the 45 controls'],
        'visibleDifferences': ['Live balances, board layouts, hidden nuts and disabled states differ from historical reference fixtures', 'AI preview omits some live background HUD and tutorial elements; no new static controls were added to imitate those omissions', 'Original 3D geometry and lighting remain; their highlights and shadows are not pixel-identical to the AI illustration', 'Some frame thickness, text outline and icon shading differ from the AI mockup', 'Withdrawal guide retains original falling-cash animation, including transient text occlusion'],
        'apk': 'This delivery changes the Unity project; the existing September APK has not been rebuilt by this request.'
    }
    cash_report = OUT / 'cash-review/report.json'
    if cash_report.exists():
        review = json.loads(cash_report.read_text('utf8'))
        assert review['status'] == 'PASS'
        result['cashStateReview'] = review
    (OUT / 'acceptance-summary.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf8')
    text = '''Unity 换皮交付 · 2026-10-09

打开 runtime-comparison.html 可查看全部 23 页。
每张三栏图分别是：历史原图（注明来源）、已确认 AI 效果图、当前 Unity 实际运行截图。
runtime/ 保存未经绘制或修饰的实际截图，three-way/ 保存等比排版的对比图。

实施：在既有 Prefab 中更换背景、面板、按钮和图标，减少树叶，保留节点、布局、标准 Button、动态文字、本地化和奖励经济配置。
验证：23 页、132 条文本、45 个可用按钮中心射线；文字检查无越界、射线均可达。两种比例各通过 20 次标准 Button 点击分发。四个教学阶段和 Level 2 恢复通过。完整测试范围及限制见 acceptance-summary.json 和 validation/ 日志。

这不是逐像素一致声明。实时金额、棋盘、隐藏螺母和禁用状态由游戏控制；原有三维材质、少量边框与文字描边仍与 AI 图不同。教学图里额外画出的示例图标没有新增到原 Prefab。提现引导保留原钞票动画，因此截图中有瞬时遮挡。
这是 Unity Editor 运行验收，不是真机或线上广告/提现验收。原存档已恢复。此次没有重新构建 APK。
'''
    (OUT / 'DELIVERY.txt').write_text(text, 'utf8')
    if cash_report.exists():
        with (OUT / 'DELIVERY.txt').open('a', encoding='utf8') as stream:
            stream.write('\n现金页修正：去掉无记录状态遗留的空木质图标底框。主对比图右栏现在是相同状态测试数据（Level1/$0.40/1/1/25553/1/Venmo），原组件动态渲染，非线上统计。当前本地入口尚无统计服务数据，仍显示--，其独立截图在cash-review/cash-local-empty.png。\n')
    files = [OUT / 'runtime-comparison.html', OUT / 'runtime-comparison-manifest.json',
             OUT / 'acceptance-summary.json', OUT / 'final-sprite-inventory.json',
             OUT / 'global-structure-audit.json', OUT / 'DELIVERY.txt']
    files += list((OUT / 'three-way').glob('*.png')) + list(OUT.glob('three-way-overview-*.jpg'))
    files += list((OUT / 'runtime').glob('*'))
    files += [OUT / 'validation' / name for name, _ in checks]
    if cash_report.exists():
        files += [p for p in (OUT / 'cash-review').glob('*') if p.suffix in ('.png', '.json', '.log')]
    dest = OUT / 'Unity-reskin-23-pages-comparison.zip'
    with zipfile.ZipFile(dest, 'w', zipfile.ZIP_DEFLATED, compresslevel=5) as archive:
        for p in files:
            if p.is_file():
                archive.write(p, p.relative_to(OUT).as_posix())
    with zipfile.ZipFile(dest) as archive:
        assert archive.testzip() is None
    print(json.dumps({'pages': 23, 'archive': str(dest), 'bytes': dest.stat().st_size, 'sprites': len(inventory)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
