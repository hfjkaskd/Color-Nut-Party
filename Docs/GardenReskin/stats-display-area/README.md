# 统计区显示空间修正

2026-09-22，针对三列统计区文字空间不足的反馈。

## 已实施

- 三块标题小底框的 Image alpha 设为 0，保留原节点与组件。
- 三列标题与数值的 Text Rect 宽度从 251.7 调整为 282，中心、锚点、高度保持原值；列间仍有 18 的间隔。
- 标题内边距从左右 12、上下 10 调整为四边 4，可用宽度从 227.7 增至 274，增加约 20%。
- 数值留白设为四边 4，保留原字号上下限和自动字号，禁止金额换行。
- 标题、数值内容和本地化、组件引用、按钮事件、玩法与经济逻辑未修改。

修改对象仅为 `Assets/Resources/prefabs/playerinfo/PlayerInfo.prefab` 的现有显示组件。应用脚本为 `Tools/GardenReskin/expand_stats_labels.py`，已接入原换皮应用脚本，防止重新应用皮肤时丢失本次修正。

## 验收

Unity 2022.3.62f3 / D3D11 / US / 720×1600 实际运行通过，日志见 `validation-final.log`。

- 三列当前标题均保持字号 30，全部字形在各自 Text Rect 与独立列安全区域内。
- 临时测试 `123456`、`128.5`、`Rp8.000.000`，完整显示、单行、无 overflow、无跨列；测试后恢复原显示内容再截图。
- 数值自动字号分别为 64.1、64.1、40.3，长金额未截断。
- 原四页验收通过：20 次标准 Button 射线/down/up/click、渠道与输入验证、四档选择、确认返回、关闭恢复、模拟广告回调去重；余额和棋盘不变，原用户存档逐项恢复。
- 此处验证的是 Editor 实际运行；未测试真机触摸、系统键盘或生产广告 SDK。

最初检查发现长金额字形略超出原数值 Rect（见 `validation.log`），扩大数值 Rect 并预留字形边缘空间后复验通过。TMP 的排版 margin 预留给加粗/SDF 网格扩边，几何检查使用真实 Text Rect 和列安全范围，未省略网格边界检查。

独立结构复核见 `independent-review.md`、`structure-diff.json`；原始 Prefab 备份为 `PlayerInfo-before.prefab`。

## 图像来源

- `cash-after-unity.png`：完整 Unity 实际运行截图，验证存档 Level 4、$1.76，统计数据保持原占位符。
- `stats-before-unity.png` / `stats-after-unity.png`：修改前后实际运行截图的同区域原尺寸裁切，无修饰。
- `cash-three-way.png`：已确认 AI 效果图 / 本次修改前 Unity 截图 / 本次修改后 Unity 截图，明确分栏，仅等比缩放和排版。
- 修改前截图来自 `../approved-replacement/runtime-us-tall/approved-skin-cash.png`；AI 图来自 `../previews-clean-boundaries/level-withdrawal.png`。

图像生成脚本：`Tools/GardenReskin/capture_stats_comparison.ps1`。本轮未生成新的 AI 效果图，也未将 AI 图当作工程截图。
