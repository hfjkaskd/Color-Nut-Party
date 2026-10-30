# 默认玩法中的原版子回合点阵

此前本地子回合已经按源逻辑自动续局，但默认界面只有同一个 Level 数字，原 HideLevel.RefreshSubRound 的点阵没有消费者。现在使用 HiddenLevel 的 LocalSubrounds 预制体变体，保留源布局和图标，仅在变体中关闭 Progress / Levels 两棵收益里程碑子树；原 HiddenLevel 预制体保持不变。根位于 MainPanel 原 Top 下，anchoredPosition (0,-248.1)，SubRounds 相对 (1.6,-91)，原 HorizontalLayoutGroup 与 round 模板不变。

本地控制器在 BoardReady 的 RefreshLevel 中调用 RefreshSubrounds，首次遇到 SubTotalRound>0 才加载该预制体，之后复用。Bind/Init 后仅执行已有 OriginalHiddenLevelView.RefreshSubRound；不调用需要收益文档的 RefreshLevels / RefreshRound，也不构造收益边界。无子回合时沿用原隐藏分支。四点增至五点使用原模板实例化，颜色、连接线、当前点 OutQuad 无限 Yoyo 闪烁由已恢复的场景动画驱动器执行。

这是授权本地玩法的独立子回合显示，不代表依赖服务端收益的整个里程碑条已接通。没有填充 GoldRewardTargetS2CData、余额或广告完成；没有新增 UI 伪按钮、世界 UI 化或 Editor 运行时分支。

LocalSubroundViewPlayValidation 从默认入口的 Level=8 显式夹具开始，通过真实 InitLevel 验证 8/9/10/12/13/22/23：首次子回合前不加载、位置与原 Sprite 引用、通过/未通过图标、端点连接线、当前点闪烁和缩放时间暂停/恢复、四到五点复用、之后隐藏、显示期间真实世界选择。此处改变 Level 是验证夹具，不宣称玩家通过该步骤通关。2026-10-09 起 LocalSubroundPlayValidation 按用户指定的“每小局结算”策略，使用真实 9/10/11/12 关连续操作，检查逐局结算领取后继续推进的点阵及下一组五点。以下旧日志属于此前自动续局策略的历史记录。

源层级和样式由预制体保存，构建入口 LocalSubroundViewBuilder 只负责 Editor 资源创作；LocalGameplayBuilder 也保存相同的资源路径，避免重新创作本地组合时丢失绑定。

验证日志：local-subround-dots-play-final.log 含 NUT_LOCAL_SUBROUND_VIEW_PLAY_PASS；local-subround-dots-progression-play.log 含 NUT_LOCAL_SUBROUND_PLAY_PASS，完成原内部 9/10/11/12 四局并到达下一组五点。新截图 local-subround-dots-9-current.png 与 local-subround-dots-13-current.png 已检查；后者补采于入场结束且清场之前，棋盘与五点均正常。用户存档由验证夹具备份并恢复。

Library/local-subround-dots-content-validation.log 完成 166 个 PASS 标记与最终 NUT_CONTENT_VALIDATION_PASS；Android ARM64 IL2CPP 开发包构建报告 0 错误、0 警告，APK 签名/元数据核验通过，哈希见 AndroidLocalBuild.md。仍未完成真机与原设备逐帧对照，也未接入完整收益里程碑显示。
