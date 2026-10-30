# 顶部双货币提示与效果图对齐

用户指出两条提示与此前确认的AI效果图不一致。对照发现实际版本使用偏黄底色、双层立体边和深棕文字；效果图使用近白底、单层绿/淡紫细边和深靛蓝普通文字。

此次只调整这两类提示的视觉：

- `mainpanel/gold_tip_bg.png`：近白底、单层柔和绿边。
- `mainpanel/coin_tip_bg.png`：近白底、单层淡紫边。
- 普通TMP文字恢复为 `#171339`，5份相关Prefab中的8个同角色文字组件保持一致。

两PNG仍为527×99，GUID、Sprite元数据、节点、RectTransform、字体字号、动态文本/本地化、红色富文本及金额计算未改变。未修改进度槽或其他面板。AI效果图只用于比对美术风格；当前实际布局仍以原Prefab为准。

[Unity实际运行窗口截图](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/top-hints-match/unity-live.png)、[原AI效果图](<C:/Projects/Golden  Dragon Legend/design-previews/main-garden-20260921/main-garden-AI-preview.png>)、[本次图像生成来源与完整提示词](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/top-hints-match/generation.json)、[单图导入报告](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/top-hints-match/import-report.json)、[结构对比](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/top-hints-match/structure-report.json)。

本轮使用当前Unity Editor Play Mode画面验收及静态结构/元数据检查。之前的主界面截图保留为历史证据，不能代表本次修正后的顶部提示样式。
