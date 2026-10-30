# 已确认四页皮肤：工程替换结果

2026-09-22。已将 `previews-clean-boundaries` 中确认的浅木、奶油、绿色风格应用到现有 Prefab。19 张无字 Sprite 位于 `Assets/Resources/atlas/approvedgarden/`；四页及其统计/记录两个嵌套 Prefab 使用局部资源引用，未覆盖旧共享 PNG。

## 三图对比

每张从左到右为 **已确认 AI 效果图 / 替换前游戏截图 / 替换后 Unity Play Mode 截图**。原始金额、关卡、比例保留；右列使用明确的验证存档（Level 4、$1.76、钻石 0）。只做等比缩放与排版，不修饰运行截图。来源及 SHA-256 见 `comparisons/provenance.json`。

- [My Diamonds](comparisons/diamond-three-way.png)
- [Enter Information](comparisons/account-three-way.png)
- [Level Withdrawal](comparisons/cash-three-way.png)
- [Refresh](comparisons/refresh-three-way.png)

原始最终截图：`runtime-us-tall/`（720×1600）及 `runtime-us-wide/`（720×1280）。这些是 Unity 实际渲染，不是 Prefab 静态预览或 AI 图片。

## 已实施

- 去除输入区、道具说明区、现金进度区以及钻石档位区域的多余底板图像，保留原节点、组件、位置及运行时显隐。
- 换细木边主背景、标题、按钮和输入框；装饰烘焙进背景，未新增装饰节点。
- 修正标题与说明、统计文字的内部留白；原文案、本地化、金额与进度仍由原 TMP 和逻辑刷新。
- 钻石普通/选中按钮资源同时配置到原动态资源路径，选项切换后继续保持新皮肤。
- 账户页原底部关闭按钮保留；它在 AI 预览中缺失，但属于原功能。装饰按原尺寸适当简化，最终以右列工程画面为准。
- 修复账户 Prefab 一条既存非法组件引用：Get 错误引用 ChannelInfo 的反馈组件。仅移除错误引用，正确组件、owner、target、settings、按钮绑定均保留。后续导入不再出现该重复组件警告。

## 验证

| 检查 | 结果 / 证据 |
|---|---|
| 独立结构审查 | 通过。48 个备份 Prefab、3,394 对象块、831 个 Transform 核验；见 `independent-structure-review.md/.json` |
| USD / 720×1600 | 通过，20 次标准 Button 射线与 down/up/click 事件；`acceptance-us-tall.log` |
| USD / 720×1280 | 通过，同样 20 次事件；`acceptance-us-wide.log` |
| BR / ID 提现原回归 | 通过；`runtime-locales/runtime-validation.json`。长金额、渠道、本地化保持 |
| 四页视觉 | 两种比例的 8 张最终截图独立复核通过：未见底板越界、文字裁切或内容重叠 |
| 数据与状态 | 余额、棋盘、等级不变；四档选择、空输入提示、渠道切换、确认返回、关闭恢复、模拟广告回调去重通过。原用户存档恢复并验证 |
| 截图时序补丁 | 仅临时延迟原 Editor 提现测试截图后的点击，已逐字节恢复；`runtime-locales.patch-audit/capture-patch-audit.json` |

测试使用 Unity 原场景与 Prefab，并通过 EventSystem 的射线结果及标准事件验证 Button，没有替换生产交互。输入内容用测试数据设置；未验收真机触摸、系统键盘或生产广告 SDK。

已知既存素材问题：BR 的 MercadoPago 原 PNG 自身左右裁切，本轮未重绘商标或变更渠道资源。USD 和 ID 页面对应标识显示完整。

## 资源制作与回退

使用内置 ImageGen 生成无字素材，源图及最终提示规格见 `sources/`、`prompt-set.md`。机械裁切、九宫格缩放、Sprite 元数据及 Prefab 外观引用由 `Tools/GardenReskin/build_approved_skin.ps1` 和 `apply_approved_skin.py` 记录，具体像素来源见 `asset-build-report.json`。

`baseline.json` 保存本轮前文件哈希；`prefabs-all-before.zip` 保存原 Prefab 字节。原脚本、已有纹理、材质和 metadata 保持。新增 Editor 验收工具在 `Assets/NutSort/Editor/GardenReskin/`，不进入游戏运行时程序集。
