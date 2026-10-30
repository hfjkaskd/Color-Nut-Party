# 浅木色花园主题换皮交付

工程：`C:\Projects\com.nut.sort.relax`。日期：2026-09-21。

教学进度修订：纠正未到/已到素材对应关系，灰方块改为圆点，连接线改浅木色。只替换3张小PNG，原逻辑与布局不变。[最新实际运行截图及核验](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/tutorial-progress-match/README.md)。

后续修订：已根据“边框太粗了”的反馈，将教学提示背景改为细木边。尺寸、位置及动态文字不变；[最新Unity运行截图与修订记录](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/border-thinning/README.md)。下表教学截图仍保留上一版验收记录。

顶部提示修订：两条双货币提示已按原效果图调整为近白底、单层细描边和深靛蓝文字；[本次运行画面与修改范围](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/top-hints-match/README.md)。下表主界面截图保留的是修正前版本。

Withdraw 标签修订：已恢复共用背景的顶部小尖角，调整为橙红底与细金边；原动态文字、显示条件和位置保持不变。[最新运行截图与单图修改记录](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/withdraw-label-match/README.md)。

底栏修订：道具底板改为浅色细木边，角标改为细绿边；已有 ReplayBg 使用新增 85×85 奶油圆底，修正旧黑色染色。原节点、尺寸、数量、绑定与动态库存不变。[最新 Unity 运行截图和点击核验](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/bottom-toolbar-match/README.md)。此修订新增一张纹理及一个 Sprite 资源，未新增 UI 节点；下述“无新 Assets”仅指此前纯换皮基线。

交互反馈修订：针对“UI 点击没有反应”，在保留教学限制的前提下增加按钮拒绝提示；改动 3 个运行时脚本并新增专用 Editor 验证器。见[原因与交互验证](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/ui-click-feedback/README.md)。下述结构基线及 7 组验证属于此前纯换皮阶段，不包含这次功能修复。

已将浅木色、奶油色、绿色主题应用到现有界面资源。保留原 Prefab 骨架、游戏对象、玩法及现金/钻石奖励含义。纯换皮阶段修改为 **118 张现有贴图、24 个 Prefab 的颜色、15 个材质的配色/金属度**；该阶段没有新增 Assets 文件、UI 节点、Shader 或运行时代码。

## 范围与保留项

替换覆盖主界面、启动/欢迎、教学、奖励/失败/重试、道具领取、提现/账户、进度与提示等页面及其共用素材。标题、金额、数量、提示、按钮文案仍由原动态文本和本地化代码提供；静态 Logo 保留原游戏名称。

背景降低细节和对比，按钮采用绿色与橙色，面板采用奶油色与木质边。装饰绘制在原贴图内。已修正阶段状态区分、教学文字对比、道具弹窗正文区域及边框裁切。

现金、钻石、银行和支付标识、玩法颜色、技术遮罩与字体资源保留原含义。125 项主题候选中，116 项替换、9 项为保留语义或技术用途而保留；另替换 2 张共用广告图标。候选无遗漏。具体原因见[覆盖清单](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/coverage.json)。

## 结构与功能验证

与修改前 3853 个 Assets 文件比较，层级、组件、RectTransform 坐标/尺寸/锚点、Button 绑定、文本值、动画、脚本、配置及 Scene 保持不变。原贴图尺寸、GUID、Sprite 元数据、Pivot 与 Border 保持不变。所有变更限定在贴图像素和序列化视觉颜色/材质字段。

7 组 Unity 2022.3.62f3 Play Mode 验证通过，共保存 19 张实际运行截图：

| 页面 | 已验证的主要行为 | 最终截图 |
| --- | --- | --- |
| 主界面 | BR/ID 金额刷新、阶段状态、现金/钻石入口 | [主界面 ID](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/runtime-polish-final/main-unity-play-local-rewards-top-ID.png) |
| 欢迎 | Start Button、重复点击防护、继续教学 | [欢迎](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/runtime-final/welcome-unity-play-welcome-startup.png) |
| 教学 | 四段文案、真实棋盘操作、后续关卡退出教学 | [教学](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/runtime-teaching-final/teaching-unity-play-local-teaching-0-current.png) |
| 成功/奖励 | 最后一步过关、现金与钻石、一次领取、读档与地区格式 | [奖励 ID](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/runtime-final/success-unity-play-local-cash-ID.png) |
| 失败 | 真实死锁、重试 Button、重复操作防护、存档恢复 | [失败](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/runtime-final/failure-unity-play-local-native-failure-current.png) |
| 提现/账户 | BR/ID 表单、空输入提示、档位选择、余额不变、关闭恢复操作 | [现金提现](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/runtime-withdrawal-final/withdrawal-unity-play-local-withdrawal-cash-ID.png)、[钻石提现](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/runtime-withdrawal-final/withdrawal-unity-play-local-withdrawal-coin-ID.png)、[账户](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/runtime-withdrawal-final/withdrawal-unity-play-local-withdrawal-account-ID.png) |
| 道具领取 | 关闭不发奖、领取请求、成功回调仅发放一次 | [道具领取](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/runtime-tooloffer-final/tooloffer-unity-play-local-tool-offer-refresh.png) |

上述图片均为 **Unity Play Mode 实际运行截图，使用明确的测试存档**。AI 素材源保存在 Generated 中；之前的 AI 效果图属于设计预览，不能作为工程验收截图。

现金提现原测试在截图后同一帧立即切换页面，截到过渡动画。最终截图使用可审计的临时 Editor 测试时序修正，仅将下一次点击延迟 0.2 秒；原断言和业务行为不变。运行结束后测试源码已按原字节恢复并核验 SHA-256。见[截图时序审计](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/runtime-withdrawal-final.patch-audit/capture-patch-audit.json)。

## 验收范围与资源成本

本次没有 Android/iOS 真机测试，也没有对真实广告 SDK、实际付款服务作线上验证；广告使用既有模拟回调测试。其他低频弹窗复用已替换资源并通过静态结构校验，并非每个状态均有运行截图。原 BR 账户页 MercadoPago 图标边缘裁切属于原布局现象，本次未重新布局。

原纯换皮阶段没有增加纹理数量、尺寸、运行时资源引用或 UI 绘制节点；后续底栏修订新增一张 85×85 圆底纹理及 Sprite，供已有 Image 引用。PNG 源文件在原阶段约增加 20 MiB；压缩后的安装包大小、加载峰值和真机帧率需要构建后实测。AI 大图、导入暂存和备份均在 Assets 外，不会作为游戏资源打包。

## 复查与回退

- [最终交付机器报告](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/delivery-report.json)：7 组结果、19 张截图与 SHA-256。
- [结构对比报告](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/structure-report.json)：无禁止变更、无新 Assets 文件。
- [导入报告](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/import-report.json)：原图/生成图/输出图哈希、尺寸和边框处理。
- [素材来源](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/generation-provenance.json)：ImageGen 来源与提示词。
- [验证入口说明](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/VALIDATION.md)、[导入说明](C:/Projects/com.nut.sort.relax/Tools/GardenReskin/IMPORT.md)。
- [修改前素材备份](C:/Projects/com.nut.sort.relax/Docs/GardenReskin/OriginalAssets/before-reskin.zip)。回退应按修改清单恢复对应文件，避免覆盖后续编辑。

以下命令针对原纯换皮基线；加入交互反馈修复后，结构检查会报告新增的运行时源码改动及 Editor 测试文件，不能将这些已授权的功能修复当作未改动。当前交互回归入口与证据见上方交互修订记录。

```powershell
python Tools/GardenReskin/validate_structure.py check --report Docs/GardenReskin/structure-report.json
python Tools/GardenReskin/finalize_report.py
```

旧版 gameplay/tools 验证器有过时的奖励数量/道具领取流程断言，未计作本轮通过项；原断言没有为本次换皮而改写。

## 三图对比验收

每个主题弹窗均分开标注：

1. **参考效果图**：用户提供的视觉目标，只用于配色、材质、比例和装饰对照。
2. **替换前游戏截图**：Unity 运行时的旧皮肤基线。
3. **实际替换后 Unity 截图**：替换素材后在 Unity Play Mode 中运行得到的结果，动态金额、国家/支付渠道和关卡数据仍由原逻辑提供。

本轮四个弹窗的三图对比在 `three-way-comparison/output/`。对比生成脚本和输入清单在 `three-way-comparison/build-comparison.ps1` 与 `three-way-comparison/comparison-manifest.json`。
