# 本地过关接入原版面板表现

原本默认本地过关使用通用卡片和立即下一关按钮。本轮改为基于原 SuccessPanel 的 LocalSuccess 预制体变体，复用源标题、按钮结构、背景和 OriginalSuccessPanel 的主面板/标题入场、关闭曲线与全局 UI 动画驱动。点击本地 Next level 后保持模态状态，关闭动画完成才调用既有本地下一关初始化。

LocalSuccess 关闭奖励 Items、广告、额外领取和关闭按钮，主要按钮使用序列化的本地 Next level 文案；其文案居中配置保存在预制体。此变体显式服务已获授权的本地推进，不生成奖励 ItemGetInfo、服务器响应或广告成功回调。源 SuccessPanel 预制体保持不变。

OriginalSuccessPanel 提取共有呈现初始化，并提供明确的 InitLocal 入口复用同一动画实现。完整原流程仍由 OriginalSuccessPanelFlow 驱动，保留原奖励、教学生命周期和开启动画完成回调；本地入口不执行奖励领取或提现引导。过关弹出音使用原场景配置 Pass_level，点击音使用原面板配置。

本地进度仍在弹出面板前保存 Level 加一、LevelSeed 归零及空 LevelInfo 的检查点。动画中、面板上或关闭中退出，重进从下一关恢复；下一关按钮不再次增加进度。关闭中的重复点击被拒绝，直到关闭完成才销毁面板并开始重建。此前通用卡片现仅用于本地不可用提示。

LocalGameplayPlayValidation 使用默认入口和真实世界操作，覆盖四段教学、连续过关、入场后面板显示、实际 UI 相机射线命中的下一关按钮、关闭期间模态和重复点击、结算/部分棋盘读档、失败重开及普通重开。测试等到主面板和标题完成入场后才截图。

此改动不代表源完整收益结算界面或全部游戏生命周期已达到 1:1；SDK 相关行为继续跳过。

Unity 2022.3.62f3 验证：

- `Library/local-native-success-core-play.log`：NUT_LOCAL_GAMEPLAY_PLAY_PASS；最新 `Library/local-gameplay-success-current.png` 在原主面板和标题入场结束后生成并检查。
- `Library/local-native-success-exit-play.log`：NUT_LOCAL_SUCCESS_EXIT_PLAY_PASS；实际最后一步过关，下一关按钮开始关闭后立即重载场景，恢复正确下一关和一次过关计数，没有残留模态。
- `Library/local-native-success-content-validation.log`：164 个 PASS 标记与 NUT_CONTENT_VALIDATION_PASS，包含原 SuccessPanel、SuccessPanelFlow 和教学/奖励流程检查。

原用户存档已恢复；既有退出对象池和编辑模式工具特效诊断未在本轮处理。本轮的场景重载测试不等同于 Android/iOS 系统强杀测试。

2026-10-09 用户指定新策略：原 SubTotalRound/SubRound 的每个小局均进入本结算面板，领取后才继续；不再跳过中间小局。每个小局沿用现有现金、钻石和广告领取流程，末局不额外发整组奖励。见 LocalSubroundProgression.md。
