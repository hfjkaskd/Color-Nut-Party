import html
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
main = json.loads((HERE / "validation/report.json").read_text(encoding="utf-8"))
jp_path = HERE / "validation-ja/report.json"
jp = json.loads(jp_path.read_text(encoding="utf-8")) if jp_path.exists() else None
updated_jp = bool(jp and jp["passed"])
home_path = HERE / "validation-home/report.json"
home = json.loads(home_path.read_text(encoding="utf-8")) if home_path.exists() else None
updated_home = bool(home and home["passed"])
verified_standby = {(item["capture"], item["path"]) for item in (home or {}).get("excludedHiddenBanners", [])} if updated_home else set()
diagnostics, failures = [], []
for issue in main["issues"]:
    key = (issue["capture"], issue.get("label", {}).get("path", ""))
    (diagnostics if key in verified_standby else failures).append(issue)
matrix_complete = len(main["captures"]) == 320 and len({item["id"] for item in main["captures"]}) == 320
passed = matrix_complete and main["fatal"] is None and not failures and (main["passed"] or updated_home)
acceptance = {
    "passed": passed,
    "verificationEnvironment": "Unity Editor actual GameView, production scene and prefabs",
    "physicalDeviceRetested": False,
    "deviceRetestNote": "This request does not wait for the physical device; this report covers Editor visual validation only.",
    "matrix": "validation/report.json",
    "matrixCaptures": len(main["captures"]),
    "homeVerification": "validation-home/report.json" if updated_home else None,
    "homeVerificationCaptures": len(home["captures"]) if updated_home else 0,
    "visibleLayoutFailures": failures,
    "hiddenStandbyDiagnostics": diagnostics,
    "diagnosticReason": "OriginalTargetRewardBanner.Init intentionally parks the inactive animation off screen. Exclusion requires this exact production component, no active motion, and wholly off-screen glyph bounds. Partially visible or unrelated off-screen text still fails.",
}
(HERE / "final-acceptance.json").write_text(json.dumps(acceptance, ensure_ascii=False, indent=2), encoding="utf-8")
names = {"home":"主界面", "loading":"Loading", "settings":"设置", "user-level":"玩家等级", "cash":"现金提现与统计", "diamond":"钻石提现", "account":"提现信息", "assessment":"奖励评估提示", "activating":"奖励开启提示", "activated":"奖励开启完成", "summary":"奖励记录提示", "diamond-notice":"钻石解锁提示", "bonuses":"五项奖励权益", "history":"提现记录", "cash-conditions":"现金提现条件", "diamond-conditions":"钻石提现条件", "undo":"撤回道具", "shuffle":"刷新道具", "bolt":"增加空螺杆", "settlement":"关卡结算"}
languages = {"en":"English · 英语", "ja":"日本語 · 日语", "pt":"Português · 葡语", "de":"Deutsch · 德语", "fr":"Français · 法语", "id":"Bahasa Indonesia · 印尼语", "ru":"Русский · 俄语", "es":"Español · 西语"}
cards = []
for capture in main["captures"]:
    language, ratio, page = capture["id"].split("-", 2)
    folder = "../validation-home/" if updated_home and page in ("home", "loading") else "../validation-ja/" if updated_jp and language == "ja" else ""
    source = folder + capture["screenshot"]
    resolution = "720 × 1600" if ratio == "tall" else "720 × 1280"
    cards.append(f'<article data-language="{language}" data-ratio="{ratio}"><h2>{html.escape(names[page])} <small>{resolution}</small></h2><a href="{source}" target="_blank"><img loading="lazy" src="{source}" alt="{html.escape(names[page])} {language} {resolution}"></a></article>')
status = "排版检查通过" if passed else "仍有排版问题"
extra = f' · 日文最终文案另有 <a href="../validation-ja/report.json">{len(jp["captures"])} 帧重验</a>' if updated_jp else ""
if updated_home:
    extra += f' · <a href="../validation-home/report.json">主界面和 Loading {len(home["captures"])} 帧复验</a>'
document = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Color Nut Party 多语言实装验收</title><style>
*{box-sizing:border-box}body{margin:0;background:#faf4e7;color:#51331d;font:16px system-ui,sans-serif}header{padding:24px max(20px,5vw) 18px;background:#fffaf1}h1{font-size:25px;margin:0 0 10px}p{margin:7px 0;line-height:1.65}a{color:#725000}.tools{position:sticky;top:0;z-index:2;background:#f4dfb7ed;padding:14px max(20px,5vw);display:flex;gap:12px;align-items:center;flex-wrap:wrap;backdrop-filter:blur(12px)}select{border:1px solid #be9050;background:#fffaf1;color:#51331d;border-radius:8px;padding:9px 12px;font:inherit}main{padding:20px max(20px,5vw);display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:22px;max-width:1520px;margin:auto}article{padding:14px;background:#fffdf8;border:1px solid #dbc098;border-radius:15px}article[hidden]{display:none}h2{font-size:17px;margin:0 0 12px}small{display:block;font-size:12px;color:#8c765d;margin-top:4px}img{display:block;width:100%;height:auto;border-radius:9px;background:#dec798}.status{color:#267b32;font-weight:700}footer{padding:24px;text-align:center;font-size:13px;color:#806b56}</style><header><h1>Color Nut Party 多语言实装验收</h1>'''
page_count = len({item["id"].split("-", 2)[2] for item in main["captures"]})
document += f'<p class="status">{status}：8 种语言 × 2 种屏幕比例 × {page_count} 个实际界面，共 {len(main["captures"])} 帧，可见文字问题 {len(failures)} 项。</p>'
document += '<p>检查真实 Prefab 的缺字、截断、文字超框和文字重叠；截图来自 Unity Editor 实际 GameView。按本次要求不等待实体手机，本报告不包含新的真机复测。测试使用隔离存档，未播放广告或提交提现。</p>'
document += '<p><a href="../final-acceptance.json">最终验收结果</a> · <a href="report.json">完整原始排版数据</a>' + extra + ' · <a href="../font-report.json">字体覆盖报告</a></p>'
if diagnostics:
    document += '<p>主界面存在原有奖励动画的离屏待机文字，已单独确认未显示；仅排除这个明确处于隐藏状态的动画组件，原始诊断完整保留。</p>'
document += '</header><div class="tools"><label>语言 <select id="language">'
document += ''.join(f'<option value="{code}">{html.escape(name)}</option>' for code, name in languages.items())
document += '</select></label><label>屏幕比例 <select id="ratio"><option value="tall">720 × 1600</option><option value="short">720 × 1280</option><option value="all">两种比例</option></select></label><span id="count"></span></div><main>'
document += ''.join(cards)
document += '''</main><footer>每张图可点击查看原始分辨率。不同国家的金额和支付渠道来自各自配置。</footer><script>
const language=document.querySelector('#language'),ratio=document.querySelector('#ratio');function show(){let count=0;document.querySelectorAll('article').forEach(card=>{const visible=card.dataset.language===language.value&&(ratio.value==='all'||card.dataset.ratio===ratio.value);card.hidden=!visible;if(visible)count++});document.querySelector('#count').textContent=count+' 个界面'}language.addEventListener('change',show);ratio.addEventListener('change',show);show();</script></html>'''
(HERE / "validation/index.html").write_text(document, encoding="utf-8")
print("Created validation/index.html", "with revised Japanese captures" if updated_jp else "with full original matrix")
