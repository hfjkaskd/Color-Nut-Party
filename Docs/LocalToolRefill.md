# 道具激励广告流程

零库存点击道具后先请求激励广告；请求期间阻止重复点击。仅成功回调补充配置数量并执行道具，显示 Reward received 飘字。失败不发放，显示广告不可用提示。重复或过期回调不重复发奖，切换棋盘后不执行旧操作。

LocalGameplay.prefab 配置模拟成功模式 simulateRewardedToolAds=true、延迟 0.5 秒、localToolRefillCount=1。Editor 与真机共用此配置和流程，不包含 Editor 专用逻辑。目前没有真实广告 SDK 或广告播放，此模式用于跑通成功回调。

真实 SDK 可通过 BindRewardedToolAds 绑定提供者，在 Unity 主线程回调 true（完成且获得奖励）或 false（取消、失败、无广告）。接通正式 SDK 时关闭模拟模式。

LocalToolAdPlayValidation 覆盖请求防重、失败不发奖、重复成功回调只发一次、飘字，以及模拟成功后的实际螺杆解锁。

道具广告前置界面：零库存点击道具打开恢复的 ItemGetPanel.prefab。根据撤销、刷新、加杆显示不同标题、图标和说明；Claim 才请求广告，关闭不请求、不发奖。成功后延续原有道具操作与飘字。
