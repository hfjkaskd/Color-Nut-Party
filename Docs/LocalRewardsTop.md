# 主界面顶部奖励显示

实际 `LocalGameplayController.Bind` 现在挂载 `LocalMainPanelTop.prefab`，复用原版现金、钻石、提示气泡和五阶段标记。该 Prefab 保留原版静态层级与布局，关闭未接入的 GM、跑马灯等组件。按钮通过代码绑定。

- 现金读取 `User.Gold`，钻石读取 `User.Coin`，零钻石也显示。
- 现金图标和金额格式使用所选奖励国家：印尼 ID，其余默认 BR。
- 提示文字调用原有奖励进度逻辑，数值来自对应国家配置；不写死截图中的 557.58 或 500。
- 阶段派生字段从 `caliper_gates_rule` 和实际关卡表计算：当前两份配置得到第一阶段终点 22、起点 7，第二阶段终点 51、起点 47、显示关卡 30。五个标记按当前显示关卡更新；后期切回普通关卡文字。第一关也显示五个标记。
- 原有本地子回合进度条继续负责子回合展示，避免与顶部进度重复。
- 关卡刷新、领取入账和地区配置回调都会刷新顶部。领取后无需重启即可看到新余额。
- Cash 按钮已进一步接入完整提现面板，显示相应金额和条件，见 [LocalWithdrawalScreen.md](LocalWithdrawalScreen.md)。关闭后恢复操作，尚未连接远端支付。

验证入口：`NutSort.Validation.LocalRewardsTopPlayValidation.Run` 检查实际启动、BR/ID 货币与图标、五个数字可见、气泡提示、实时余额、阶段切换与标准 Button 交互。`LocalCashRewardPlayValidation.Run` 额外检查真实通关领取后顶部余额同步。
