# 教学阶段 UI 点击反馈

用户反馈主界面 UI 点击没有反应。真实鼠标复现：第一关最后一段教学中，螺母可选择、取消，Cash 和底栏按钮不响应。

原因有两层：教学 Prefab 的全屏透明 Image 接收射线；本地教学同时保留模态锁，Cash 与底栏回调原本直接拒绝操作。世界螺母使用原有教学例外继续操作。因此不能通过删除教学限制来修复反馈。

## 修改

- 仅在本地教学实例上关闭根透明 Image 的射线接收，让现有标准 Button 收到点击；共享 Prefab 与其他引导实例不变。
- Cash、钻石 Cash、道具和重开仍受原门禁约束，但教学中点击会复用 `Prefabs/Tip` 显示当前教学指令。提示只有一个实例，不堆叠，不申请全屏点击遮罩。
- 文字来自当前动态教学文字：前三段沿用原本地化 ID 70–72，最后一段沿用已有 Prefab 可配英文 `localFinalTeachingText`。没有将文字画入贴图。
- 关闭教学时立即隐藏提示，仍由其原有 1 秒生命周期销毁，避免延迟回调访问已销毁对象。

只修改 `LocalGameplayController.Teaching.cs`、`.Top.cs`、`.Tools.cs` 三个运行时文件；另新增 Editor 验证器及 meta。没有改层级、位置、按钮数量、静态素材、通关条件、经济或奖励规则，也没有 Editor 专用运行时分支。

## 验证

`NutSort.Validation.LocalTeachingClickFeedbackPlayValidation.Run` 在 Unity 2022.3.62f3 实际 Play Mode 执行，退出码 0，标记 `NUT_LOCAL_TEACHING_CLICK_FEEDBACK_PASS`。

- 四段教学、五类按钮：通过 EventSystem 的真实 UI 射线命中校验，再向标准 Button 派发 PointerClick；不用直接调用 `onClick.Invoke`。
- 检查提示内容、单实例、到期消失与再次出现，且关卡、棋盘、道具、余额、奖励不变。
- 提示出现时，世界螺母仍可正常选择、取消。
- 最后一段提示存活时关闭教学，同场景等待后无残留提示或 `MissingReferenceException`。
- 第二关 Cash、重开面板正常打开、关闭，教学限制及提示已解除。
- 原用户存档已由测试夹具备份并恢复。未测试 Android/iOS 真机。

[最终测试日志](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/ui-click-feedback/final-play-validation.log)；[1080×2400 Unity 测试存档截图](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/ui-click-feedback/unity-test-seed-3.png)。`initial-720x1280-*` 属于补查生命周期之前的首轮测试证据。

这些都是 Unity 实际运行证据，不是 AI 效果图。历史纯换皮的结构对比报告仍保留；其“没有源码改动”结论不适用于这次后续交互反馈修复。

恢复原存档后，实际鼠标点击 [Cash](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/ui-click-feedback/unity-live-cash.png) 与[底栏重开](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/ui-click-feedback/unity-live-replay.png)，均看到教学提示；Unity 保持 Play Mode。交互窗口日志另有 Unity Android Build Settings 的 `GUILayout` 重绘异常，堆栈位于 `UnityEditor.Android.AndroidBuildWindowExtension`，不属于本次游戏按钮回调；本轮批处理验证无该异常。
