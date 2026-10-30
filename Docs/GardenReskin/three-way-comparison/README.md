# Unity 换皮三图对比

这组文件把设计参考、替换前运行结果、替换后 Unity 运行结果分开标注。左列是效果图，中列是旧皮肤运行截图，右列是工程实际替换后的运行截图。中列使用项目已有的 RewardEvidence 历史基线，右列使用本轮 Unity Play Mode 的实时截图裁切；BR 样本仅作为替换前视觉基线，金额和支付渠道属于动态内容，不是画死在素材中的文字。

- [Refresh 道具领取弹窗三图对比](output/refresh-three-way.png)
- [Level 提现面板三图对比](output/cash-three-way.png)
- [My Diamonds 面板三图对比](output/diamond-three-way.png)
- [Enter Information 表单三图对比](output/account-three-way.png)

当前 Unity Play Mode 的补充截图保存在上级目录：`comparison-current-refresh.png`、`comparison-current-cash.png`、`comparison-current-diamond.png`、`comparison-current-account.png`。这些是完整 Unity 编辑器截图，用于确认实际运行上下文；三图对比使用同一类运行窗口的裁切结果和已有历史基线，避免把效果图误称为工程结果。

Refresh 弹窗对应 `Prefabs/Panels/ItemGetPanel`，它是覆盖在主界面上的道具领取模态框，背景中仍能看到关卡和底部工具栏是原有层级关系。Claim、关闭、广告入口仍保留原 Button 与绑定。

