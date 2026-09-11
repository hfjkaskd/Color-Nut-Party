# 插屏领取流程

原版依据：RewardGetButtonFlowAudit.md / OriginalRewardGetFlow，普通领取增加 NormalGetTimes；达到 LSSLRFP 后计数清零并增加 InterAdTimes，InterAdTimes <= LSSLRFV 时调用插屏，否则调用激励。已有采集值为 3、5，写入 LocalGameplay.prefab 可配置字段 normalClaimAdThreshold、interstitialClaimLimit。

当前接入通关奖励的普通领取入口。前两次直接领取，第三次进入广告回调流程；累计前五次广告走插屏，之后走激励。原版普通领取不依据广告结果布尔值取消基础奖励，现实现保留此行为，不因无广告或关闭失败吞掉已经获得的关卡奖励。奖励金额继续使用已配置的本地结算金额，不用广告修改数值。

模拟模式 simulateSettlementAds=true，与道具激励共用配置延迟后触发成功；插屏成功显示 Interstitial ad completed 飘字。当前没有真实广告 SDK 或视频播放。正式 SDK 在 Unity 主线程通过 BindSettlementAds 提供插屏、激励关闭/失败回调。存在请求期间防重及重复回调防重，广告计数持久化。

集成验证通过实际通关棋盘、领取前重启，分别测试第三次领取插屏与第六次广告转激励，失败回调继续基础领取、成功回调、重复回调不多发、领奖与重启存档。
