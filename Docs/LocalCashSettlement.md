# 实际通关奖励接入

修复原因：先前国家配置已加载，但 `LocalGameplayController.ShowLocalSuccess` 仍调用不展示奖励的 `InitLocal`。配置和独立结算测试通过，不代表实际游戏入口接通。现已改为 `InitSettlement`，使用原版 `Item_Big` Prefab 显示对应国家纸币图标、金额和普通 Claim 按钮。

## 本地结算规则

- 第一、二关读取所选配置的 `bear_list` 前两项。
- 后续完整关卡使用当前 Gold 查找解密配置的 `[le, lh)` 区间，读取 `value`，应用配置中的 `shuaiXcRatio` 浮动范围；区间外使用 `bear_torque`。巴西按两位小数、印尼按整数显示与入账。
- 这是根据已采集配置实现的本地奖励策略，尚未证明等于原版服务器所有逐关计算规则，不生成伪造的服务器响应。
- 中间子回合不结算；最后子回合才生成待领取奖励。
- 无广告 SDK 时只提供普通领取，不展示可直接获得的广告倍数按钮。

## 持久化与 UI

通关推进关卡时，将 `LocalPendingReward` 与下一关进度写入同一用户 JSON。它记录金额、国家和下一关序号。展示不入账；领取把金额加到 Gold，并清除待领取记录，再保存和关闭界面。重复点击只生效一次。未领就重启，恢复原金额和币种，不重新随机；领取后在关闭动画中重启，也不会重复发放。

新增 BR3、ID3 图标为原版已导出的 PNG，使用普通 Sprite 导入，通过 Resources 路径加载，没有运行时拼装静态 UI。`LocalGameplayBuilder` 同步使用 Claim 文案，重建 Prefab 不会退回 Next level。

## 验证

`NutSort.Validation.LocalCashRewardPlayValidation.Run` 在实际场景中操作两根世界螺杆，完成最后一步，检查真实通关面板的奖励金额、货币符号、图标和领取按钮。覆盖非印尼回退巴西、印尼、前两项奖励、两国末档、领取前重启、重复点击、关闭时重启以及下一关可操作性。截图为 `local-cash-BR.png` / `local-cash-ID.png`。

实测截图：[巴西](RewardEvidence/local-cash-BR.png)、[印尼](RewardEvidence/local-cash-ID.png)。图中金额是该次本地随机结果，不是固定的原版服务器关卡奖励。
