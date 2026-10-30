# 延迟单杆奖励的重启恢复修复建议

本文件和同目录 `rod-recovery-review.patch` 是待应用草稿；审查期间未修改 Assets、Packages 或 ProjectSettings。

## 已确认的窗口

`OriginalMoveCompletionFlow.Run` 先累加 `ScrewDoneCount`、再排队延迟回调。`OriginalLevelView.Operate` 随后通过 `OriginalGameScene.ForwardOperation` 保存棋盘和计数。如果此时退出，完成杆已经保存，但现金、钻石和 `lastProcessedScrewDone` 尚未提交。恢复后没有原延迟回调，仅靠下一次回调消耗一个计数不能追回之前欠的一次奖励。

另外 `OriginalGameScene.RebuildBoard` 当前在 `BoardReady` 前自动重建已完成棋盘。因此必须同时保留本地待结算的完成棋盘，不能只在 `OnBoardReady` 检查成功。

## 最小修复

1. 增加 `LocalLuckyRewardPolicy.RecoverScrewCompletions(user, completedShowLevel, cashPerScrew, diamondsPerScrew)`，只恢复已有 marker 与当前完成计数的正差额。没有 marker 的旧档只建立基线，不追补历史。
2. 现金和钻石仍分别使用现有 `bear_torque`、`zs_ea`。展示关卡 >= 3 才补现金，>= 4 才补钻石。有 `LocalPendingReward` 时按它的 `nextLevel - 1` 计算已完成关卡，不能拿已前进的 `User.Level` 提前发钻石。
3. 新方法只更新余额和计数，不调用 `OriginalScrewDoneFlow.Run`，避免恢复时重复触发 Lucky、关卡成功或其他弹窗。余额和计数通过原用户存档一起保存。
4. 在首次 `OnBoardReady`、`BindBoardCallbacks` 和 HUD 初始化之后恢复；本次进程后续换关不能把仍在排队的正常回调提前结算。
5. `OriginalGameScene.PreserveCompletedBoardForSettlement` 默认为 false，由本地 controller 的 `Bind` 设置为 true（同帧早于重建）。只改变本地已完成棋盘恢复，空棋盘及原生 host 保持原重建行为。
6. 无现有 `LocalPendingReward` 且恢复棋盘已成功时，再调用已绑定的 `game.Success()` 并退出当前初始化。即使奖励差额为 0 也恢复胜利，覆盖“补奖励已保存、胜利待回调期间再退出”的窗口。有 pending 时只恢复原结算，不再递增关卡或另建结算。

现有 `PrepareEarningBoard` 在首次可操作前建立计数基线，原有首次移动保存会将该 marker 一起写盘。它不得直接调用成功流程，因为 `BindSuccess` 在它之后。

## 验证建议

- 已有 marker 差额 1、2：恢复配置金额和 marker，一次保存后重载不再增加。
- 旧档只有 `ScrewDoneCount` 没有 marker：不补历史余额。
- 第三关完成且已有 nextLevel=4 的 pending：仅补现金，pending 金额和 nextLevel 不变，不提前补钻石。
- 第四关部分完成保存：按差额补现金、钻石，但不触发 Lucky UI。
- 在真实杆移动后、延迟 DoneEvent 前重载：恢复棋盘同一杆仍完成，金额只补一次。
- 完成整局的该窗口重载：保留完成棋盘，进入同一局结算一次，不要求重新解整局。
- 余额已恢复、成功回调前再次重载：不重复加单杆奖励，仍能恢复结算。
- 现有 Lucky 双回调防重和通关优先测试继续通过。

本次文档对照：`implementation.md` 的记录、归档、激活、10 倍回调、无支付接口及 SDK 文件不改动等声明与代码和已生成验收报告一致；APK 报告在本次签名构建完成后生成。上述恢复窗口是最终审查新增发现，需要补丁及新增验收通过后再交付。
