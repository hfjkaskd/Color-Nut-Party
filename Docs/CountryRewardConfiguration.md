# 国家奖励配置

运行时选择规则：`ID` / `IDN`（不区分大小写、忽略首尾空格）使用印尼；巴西、美国、其他国家、空值及未知代码全部使用巴西。此规则是本项目指定的默认策略，不是对原版所有国家行为的推断。

配置入口为 `Assets/Resources/Configuration/CountryRewardCatalog.asset`，在 Inspector 中分别配置两国的奖励、解密规则和展示配置资源路径。六份 JSON 位于 `Assets/Resources/Configuration/CountryRewards`，来自 `Docs/RewardEvidence/CountryProbe/new-BR-1-*` 和 `new-ID-1-*` 的完整数据。只去掉响应封装，不缩放、插值或删减档位；巴西保留 13 档，印尼保留 15 档及各自末段。

`OriginalUserSession.Initialize()` 在加载用户存档后选择配置，将奖励文档安装到原有 `GoldRewardTargetS2CData`，已有 `OriginalSuccessSettlementFlow` 等消费者继续使用该字段。新配置覆盖旧存档的奖励政策，但保留已知用户进度字段 `ext_gap_logs`、`gap_day_gates`、`gap_all_gates` 和按 `bear_id` 匹配的 `caliper_count`；不修改余额、关卡、领取计数或发放奖励。现有存档写入流程负责持久化。

当前自动地区来源是系统区域 `RegionInfo.CurrentRegion`，并非 IP 定位或广告归因。未来地区 SDK 得到结果后，可调用用户会话的 `ConfigureRewardCountry(countryCode)`，同样遵循上述回退规则。语言和币种显示没有在本次配置任务中强制改成葡萄牙语。

完整解密规则与展示配置可通过 `session.Rewards.CreateRulesDocument()` / `CreateDisplayDocument()` 读取，每次返回独立副本。展示配置不混入结构不同的 `ServerConfigData`。奖励配置仍保留原始加密字段，验证时逐字段确认它与解密资源一致；修改配置时应保持二者一致。

后续修复已将实际本地通关入口改为 `InitSettlement`，展示配置计算出的本地奖励，并支持领取和存档，见 [LocalCashSettlement.md](LocalCashSettlement.md)。这不代表接通原版实时奖励或提现服务。

校验入口：`NutSort.Validation.CountryRewardConfigurationValidation.Run`。它检查两套完整配置与采集证据一致、所有解密区间一致、国家回退、实际用户会话初始化、旧进度保留，以及原有结算流程读取前两项奖励时分别得到巴西 0.4/0.6 和印尼 400/600。

Unity 2022.3.62f3 隔离验证项目中，配置校验 `NUT_COUNTRY_REWARD_CONFIG_PASS` 和实际启动回归 `NUT_LOCAL_WELCOME_PASS` 均通过。后者覆盖首次欢迎界面、开始按钮、教学衔接、存档重启和返回用户。结果及资源哈希见 [CountryRewardConfigurationValidation.json](CountryRewardConfigurationValidation.json)。
