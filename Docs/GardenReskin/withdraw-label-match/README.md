# Withdraw 标签对齐效果图

本轮仅重做 `Assets/Resources/atlas/mainpanel/hintlevel_tx_bg.png`。此前通用胶囊素材带较厚棕色立体边，并遗漏原素材的顶部中央小尖角；现改为接近已确认效果图的橙红底、细金色边框、圆角矩形和顶部小尖角。

PNG 仍为 **206×77**，不改 Sprite、GUID、Prefab、文字、材质、节点位置或功能代码。标签本来就是动态状态提示；原第一关显示 1/2/4/5 四处，第 3/4 关显示 4/5 两处。效果图为第四关状态，不能为了凑成两个而删除当前关卡标签。

图像由**内置 image_gen** 生成：[最终生成源图](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/Generated/withdraw-label-gold.png)，[完整提示词和来源](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/withdraw-label-match/generation.json)。源图无文字，动态本地化文字继续由原组件显示。机械导入只裁剪、缩放生成素材，不重画图案。

[Unity 实际运行截图](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/withdraw-label-match/unity-live.png) 为当前第一关存档的编辑器 Play Mode 画面；不是 AI 效果图，也不是 Prefab 静态预览。

[单 PNG 实际导入报告](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/withdraw-label-match/import-report.json) 保留元数据哈希与尺寸。根目录 `import-report.json` 已更新为全部 118 个目标的只读重新核验，所有预期输出 SHA-256 与实际 Assets 一致；未重新写入其他 117 张素材。

本轮使用运行画面检查和文件完整性对比，没有重跑此前的功能测试。上轮教学点击修复保持不变。

[完整性核验](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/withdraw-label-match/integrity-report.json) 已通过：3812 个文件中只有目标 PNG 改变，其余 Prefab、Scene、脚本、meta、配置和材质逐字节不变。
