# 底部道具栏修订

已按批准的 AI 效果图，减薄并淡化三个道具底板的木边；数量与加号角标去掉金铜圈，改为绿色细边。右侧重开按钮恢复奶油圆底。

![Unity 实际运行截图局部](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/bottom-toolbar-match/unity-toolbar-crop.png)

这张图是 Unity 2022.3.62f3 Play Mode 实际运行截图的无损裁剪。[完整截图](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/bottom-toolbar-match/unity-live.png)保留 Editor 及运行状态；不是 AI 效果图或 Prefab 静态预览。

原三个 PNG `item_bg`、`item_count_bg`、`item_add` 保持各自的尺寸、GUID 和 Sprite 数据。新增的 `replay_bg.png` 为 85×85，由已有 ReplayBg Image 使用；三个相关 Prefab 只改该 Image 的 Sprite 引用与颜色，修正原来黑色 tint 使底图变暗的问题。没有增加节点或组件，没有调整任何 RectTransform、按钮数量或绑定。共用 `kuang` 资源保持不变。

撤销“5”仍是实际库存动态文本。效果图中的灰色撤销“+”属于另一库存/可用状态，未将其画死或强制替换当前状态。

实际鼠标点击撤销、交换、加杆及重开，均显示当前教学提示；棋盘、余额及数量不变。详见 [4 次点击证据与文件哈希](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/bottom-toolbar-match/revision.json)及[结构差分](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/bottom-toolbar-match/integrity-report.json)。此轮没有改动运行时代码，也未重跑此前完整功能套件或做真机测试。Editor 原有 Android Build Settings 重绘异常仍可见，停止 Play 时另有原有对象销毁提示，本轮未将其计作“全工程无报错”。

美术使用 **built-in image_gen**，四张源图保存于 `Docs/GardenReskin/Generated/`：`tool-panel-light.png`、`tool-count-green.png`、`tool-add-green.png`、`replay-cream-disc.png`。[完整提示词与来源路径](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/bottom-toolbar-match/generation.json)。导入仅裁切透明边和缩放；未通过脚本绘制或调色。

复现使用当前主导入清单中的三个目标；新增圆底单独运行 `ImportReplayBackground.ps1`。`configure_replay_background.py --apply` 只配置现有 Image，无参数则只读核验。新增小纹理为 85×85，其余贴图尺寸与 UI 节点数量不变。
