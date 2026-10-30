# 教学提示框细边调整

根据用户“边框太粗了”的反馈，只替换 `Assets/Resources/atlas/newbieguidepanel/bg.png`。新图使用更细的木边、更浅的阴影及平缓的深棕内面；四段教学与后续本地模式提示共用这一张图。

PNG保持809×200，Prefab提示框保持900×200，GUID、Sprite、Border、所有文字、位置、字号和运行时代码不变。进度槽及其他页面边框未联动修改。

已在用户打开的Unity中进入Play Mode，实际看到两行“Complete this level to continue in local mode”正常显示；保留Unity在该页面供查看。此次为单张美术资源修订，进行了实际画面检查、导入元数据检查和静态结构对比，没有重复全套玩法验证。

[Unity实际运行窗口截图](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/border-thinning/unity-live.png)。这是当前Editor Play Mode截图，非AI效果图，也非Prefab静态预览。

[最终游戏贴图](C:/Projects/com.nut.sort.relax/Assets/Resources/atlas/newbieguidepanel/bg.png)、[修改前贴图](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/border-thinning/before-bg.png)、[内置image_gen来源及完整提示词](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/border-thinning/generation.json)、[单图导入报告](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/border-thinning/import-report.json)。

此前`runtime-teaching-final`的四张截图记录的是上一版本的粗边背景；本次视觉结果以`unity-live.png`为准。
