# 换皮验证入口与证据类型

`validation-baseline.json` 在开始换皮前读取 Assets 中 3853 个 Prefab、Scene、材质、纹理、元数据、脚本、配置和动画的 SHA-256。使用独占创建，不覆盖已有基线。

## 静态结构比较（不启动 Unity）

在工程根目录运行：

```powershell
python Tools/GardenReskin/validate_structure.py check --report Docs/GardenReskin/structure-report.json
```

退出码 0 表示未删除基线文件、原元数据/脚本/配置/动画没有改变，Prefab 和 Scene 除明确的视觉字段外保持不变。GameObject、Transform、RectTransform 完整比较，因此包括节点名、节点/组件 ID、层级、位置、尺寸、锚点；同时保护 Button 绑定、文本值、业务组件参数等。颜色、Sprite、字体及材质引用可改变；每个实际修改文件仍列在报告中。材质/纹理本体允许变化，须另外检查视觉效果。新增文件单独列出，不能由此宣称新增资源已人工检查。

## Unity 实际运行验证

复用项目已有的测试入口，没有修改游戏核心逻辑，也没有添加仅 Editor 生效的运行兜底。以下测试通过既有存档夹具和真实启动、Prefab、Button/世界输入路径执行。每个测试在结束时恢复原 Editor 存档并退出 Unity；中断时原存档备份保留在 `Library/ValidationBackups/GameplayUserLocalData.bin`，下次原验证入口会先恢复。它们不是物理手机验收，也不是独立 AI 效果图或 Prefab 静态预览。

关闭此工程的 Unity Editor 后，在工程根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/GardenReskin/run_validation.ps1
```

脚本顺序执行六组验证，拒绝与此工程已有 Unity 进程竞争，将新截图、日志和摘要复制到新的 `Docs/GardenReskin/runtime-validation-时间戳/`。仅接受本次执行后更新的截图，避免把 Library 里的旧图当作换皮后的结果。没有 `-quit`（验证入口自行退出）或 `-nographics`（必须真实渲染）。默认 Unity 路径是已安装且匹配 ProjectVersion 的 `C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe`。

| 验证页面 | 现有 executeMethod（前缀 NutSort.Validation.） | 主要 Library 截图 | 额外验证 |
| --- | --- | --- | --- |
| 游戏主界面 | LocalRewardsTopPlayValidation.Run | local-rewards-top-BR.png、local-rewards-top-ID.png | 双货币金额、五阶段、Cash Button、状态刷新 |
| 欢迎 | LocalWelcomePlayValidation.Run | welcome-startup.png | 延时 Start、真实 Button 命中、重复点击与引导继续 |
| 教学 | LocalTeachingViewPlayValidation.Run | local-teaching-0-current.png 至 3 | 四段动态文案、真实棋盘操作、后续关卡退出教学 |
| 成功与奖励 | LocalCashRewardPlayValidation.Run | local-cash-BR.png、local-cash-ID.png | 真实最后一步、现金与钻石、一次领取、重载和国家格式 |
| 失败 | LocalFailurePlayValidation.Run | local-native-failure-current.png | 真实死锁、重试 Button、原布局、存档恢复 |
| 提现与账户 | LocalWithdrawalPlayValidation.Run | local-withdrawal-cash-*.png、account-*.png、coin-*.png | 两地区金额、账户表单、空输入反馈、余额不变、关闭恢复操作 |

可只运行一组（同样需要关闭工程 Unity）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/GardenReskin/run_validation.ps1 -Suites main
```

## 可选道具验证与旧测试限制

新增两个可选 suite，默认六组保持不变：

| suite | 现有 executeMethod（前缀 NutSort.Validation.） | 必须匹配的 PASS | Library 截图 |
| --- | --- | --- | --- |
| tools | LocalToolsPlayValidation.Run | NUT_LOCAL_TOOLS_PLAY_PASS | local-tools-exchange-current.png、local-tools-bottom-current.png |
| tooloffer | LocalToolOfferPlayValidation.Run | NUT_LOCAL_TOOL_OFFER_PASS | local-tool-offer-refresh.png |

`tooloffer` 验证零库存点击后出现原生道具补充面板，关闭不请求广告、不发奖励；点击领取才请求广告，显式测试回调完成后只发一次道具并进入交换状态。此处广告是既有验证夹具的回调，不证明实际广告 SDK 播放。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/GardenReskin/run_validation.ps1 -Suites tooloffer
```

`tools` 映射已核对且可单独调用，但其既有测试假定零库存点击后立即补充并执行道具。当前业务流程先打开 ToolOffer，用户点击 Claim 才请求广告并补充。旧测试没有这一步，在 phase 1 撤销断言存在已知不匹配；添加入口不表示测试已通过，也不将其列入本轮通过项。换皮不修改该测试或道具业务流程。

可选 `gameplay` 保留映射用于历史诊断，**未列入本轮验证或通过项**。现有 `LocalGameplayPlayValidation.Run` 仍断言成功面板 `Views.Count == 1`，已过时；当前现金与钻石双奖励由 `LocalCashRewardPlayValidation.Run` 明确验证 `Views.Count == 2`。不要把旧测试的奖励数断言失败当成换皮回归，也不要为了使旧测试通过而修改奖励或经济逻辑。

## 单独命令与限制

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe' -batchmode -force-d3d11 -projectPath 'C:\Projects\com.nut.sort.relax' -executeMethod NutSort.Validation.LocalRewardsTopPlayValidation.Run -logFile 'C:\Projects\com.nut.sort.relax\Library\garden-main-validation.log'
```

必须同时满足退出码 0、对应 PASS 标记和新截图。查看新截图确认文字、点击区域、背景对比与图标语义；程序通过不代替视觉验收。`OriginalRenderingValidation.Validate` 和 `OriginalBackgroundTextureValidation.Validate` 专门锁定原版颜色/纹理，不适合把其“原美术完全相同”断言当作换皮成功条件；勿修改这些既有断言来让测试变绿。

若需要 Prefab 静态预览，应明确另标 `Prefab 静态预览`。此文档推荐的截图全部是 Unity Play Mode 真实渲染，并带明确测试数据夹具，不应标为已发布产品截图或 Android/iOS 运行证据。
