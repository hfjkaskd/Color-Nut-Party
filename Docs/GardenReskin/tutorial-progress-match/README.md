# 教学进度圆点修订

已将灰色方块与亮绿细线改为圆形进度点、浅木色轨道，保留原四点的位置、34×34 尺寸及进度规则。

![Unity 实际运行截图局部](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/tutorial-progress-match/unity-progress-crop.png)

此前素材映射误将 `dian`（未到点）画成绿色、`dian1`（已到点）画成灰方框。现对应为奶油圆点和绿色圆点；原规则为 `index <= LevelSeed`，包含当前点。截图为当前 Seed 3，四点均绿色。[完整 Unity Play Mode 截图](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/tutorial-progress-match/unity-live.png)。没有将 AI 图或静态 Prefab 预览当作运行证据。

仅修改 `Assets/Resources/atlas/newbieguidepanel/` 下 `dian.png`、`dian1.png`（均34×34）及 `line.png`（131×5）。3816 个基准文件中，其余3813个文件字节不变；无新增Assets，无Prefab、脚本、元数据改动。详见[差分报告](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/tutorial-progress-match/integrity-report.json)。共用子关卡进度也沿用正确的未到/已到语义，原闪烁动画保留。

圆点复用已生成的 `Docs/GardenReskin/Generated/replay-cream-disc.png` 与 `tool-count-green.png`；轨道使用 **built-in image_gen** 生成，保存于 `Docs/GardenReskin/Generated/tutorial-track-wood.png`。[提示词、来源与对应关系](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/tutorial-progress-match/generation.json)。导入仅透明边裁切和缩放，没有脚本绘制或调色。

本轮验证当前Seed 3实际显示；其他状态检查了原资源路径与逻辑，未新增逐状态运行截图，也未重跑完整功能测试或做真机测试。
