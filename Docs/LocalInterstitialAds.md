# 插屏领取流程

原版依据：RewardGetButtonFlowAudit.md / OriginalRewardGetFlow，普通领取增加 NormalGetTimes；达到 LSSLRFP 后计数清零并增加 InterAdTimes，InterAdTimes <= LSSLRFV 时调用插屏，否则调用激励。已有采集值为 3、5，写入 LocalGameplay.prefab 可配置字段 normalClaimAdThreshold、interstitialClaimLimit。

当前接入通关奖励的普通领取入口。前两次直接领取，第三次进入广告回调流程；累计前五次广告走插屏，之后走激励。原版普通领取不依据广告结果布尔值取消基础奖励，现实现保留此行为，不因无广告或关闭失败吞掉已经获得的关卡奖励。奖励金额继续使用已配置的本地结算金额，不用广告修改数值。

当前正式广告由 QTGameAdsBridge 经 SDKAdapter.ShowInterstitial / ShowRewardVideo 接入，通过 BindSettlementAds 返回结果，不模拟广告成功。插屏真实成功显示 Interstitial ad completed 飘字。存在请求期间防重及重复回调防重，广告计数随结算持久化。

2026-10-09 修复：SDK 接入时曾把普通领取的失败回调改成回滚计数并停止领取。设备日志存在超时先到、关闭回调晚到的情况，这会让同一次结算留在界面并在下一次点击时重复请求广告。普通 Claim 现在在失败、无填充、超时后继续领取已获得的基础奖励，清空 LocalPendingReward 并关闭结算页，不再回滚本次计数或显示领取失败提示。广告的真实失败结果未被改写为成功。

MoreGetCallback 额外奖励和道具激励仍要求真实广告成功；失败不发放额外奖励。奖励金额、广告频次阈值、SDK 文件及界面结构不变。本修复不修改 SDK 的超时和原生关闭回调时序。

回归覆盖普通插屏/激励路由的成功和失败、重复点击、重复及晚到回调、关闭动画暂停后等待再点击、结算一次入账及重载存档。测试报告保存在 Docs/AdClaimFix/20261009。
