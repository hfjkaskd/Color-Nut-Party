# 原版奖励响应与重建版对照

采集日期：2026-09-09。原版 Android 包 `com.nut.sort.relax`，版本 `1.0.4`。

## 已验证的内容

通过原版的正常游戏操作触发响应，以只读方式提取原版进程中的 JSON。未替换响应、未修改奖励金额。原始响应逐字保存，SHA-256 见 [manifest.json](manifest.json)。这是客户端已收到的应用层响应，不是服务端数据库的完整导出。

| 场景 | 原版观察 | 重建版回放 |
|---|---|---|
| 第一关引导 | $0.40，Withdraw，无广告图标和普通领取 | 一致；金额来自配置第一项。第一关响应未单独保留，使用第二关的空金额响应检查相同配置回退分支 |
| 第二关结算 | `hg_amt` 为空；界面 $0.60，Withdraw | 一致；读取配置第二项，不将空金额当成零奖励 |
| 引导结束初始化 | 响应金额 500、余额 500 | 已保存独立响应，不能当作第三关通关奖励 |
| 第三关途中奖励 | $2.62，倍率 10；普通领取响应 2.62、余额 502.72 | 已保存预览和领取两种响应，不能混入通关奖励 |
| 第三关通关 | $4.98，Claim 10X，普通 Claim，广告图标 | 金额、倍率、文字和按钮可见性一致 |
| 第三关领取 | 普通 Claim 达到广告阈值；广告正常关闭后响应金额 49.8、余额 557.58 | 原版领取分支同样请求倍数奖励；按响应的绝对余额入账为 557.58 |

校验入口：`NutSort.Validation.CapturedRewardParityPlayValidation.Run`。校验使用已恢复的 `OriginalSuccessSettlementFlow`、`OriginalSuccessPanelFlow`、原版 `SuccessPanel` / `Item_Big` Prefab、`OriginalRewardGetFlow`、`OriginalSuccessRewardFlow` 和 `OriginalItemManager`。配置解密结果与独立 Python 解码结果逐字段比较。结果见 `parity-results.json`，日志见 `parity.log`。

这是同一份真实输入的回放校验，**不等于重建版已接通原版实时账号/奖励服务**。现有本地玩法的 `LocalGameplayController.Success.cs` 仍走 `InitLocal`；本报告不把回放测试当作生产接入完成。广告回调仅在校验中重放已经观察到的事件，不伪造原版广告结果。

## 能否一次取得所有奖励

已导出本次服务器返回的完整奖励配置 [reward-config.json](reward-config.json)，以及加密字段的完整明文 [reward-rules-decoded.json](reward-rules-decoded.json)。其中有现金目标、钻石目标、新手奖励、日关卡条件、倍率、浮动系数及 13 档衰减区间。

这份配置不是未来所有请求的预生成结果。通关预览、领取、幸运奖励、初始化奖励由不同请求产生。尚未取得的场景不能用当前样本金钱数值冒充，未来金额也不能单纯从一次观测推断。已解密的规则没有被证明等于服务端全部计算规则。

## 是否按国家、用户返回不同金额

**后续地区探测已观察到差异：巴西新身份的前两项配置为 0.4、0.6，印尼新身份为 400、600，解密规则也不同。** 详见 [巴西、印尼探测报告](CountryProbe/README.md)。完整奖励样本为巴西两个、印尼一个；第二个印尼只确认注册成功。原账号仅切换地区时完整奖励配置相同。尚未模拟真实网络出口或广告归因，不能认定为真实买量用户，亦不能由有限样本证明全部用户的选择规则。

- 原版 `RegisterInfo.Request`（原生 RVA `0x9F2374`）将 `CountryLssInfo.Area` 写入 `RegisterC2S.scrap_mark`。国家/地区并非仅在 UI 上使用。
- 通用 `InfoLSSBase.Request`（`0x9EF340`）把当前 `UserLocalData.UserId` 写入 `copp_uuid`，把 App 版本写入 `copp_toler`。通关请求另携带 `copp_count`、`notify_psi`、`hg_zs_psi`；领取请求另有 `adver_type`、`timeTag`。服务端具备区分用户与状态的输入，输入存在不等于已经证明采用了差异化奖励政策。
- 2026-09-07 的存档与本次原版属于**同一 UserId**。两份完整奖励配置仅 `gap_day_gates` 不同，解密后的奖励规则相同。比较见 [device-comparison.json](device-comparison.json)。不能将这两份记录视为两个不同账号。
- `ksCountry = ID-BR` 是一个配置字段；没有证据把它解释为本次玩家的真实国家。不能据此声称已完成美国、巴西、印尼的账号对照。
- 客户端格式化会改变显示：相同 4.98 在美国、巴西使用不同货币符号/小数分隔符；日本、印尼、巴基斯坦的原版格式化分支还会截去小数。只比较截图上的字符，无法判断原始奖励字段是否变化。

要进一步隔离国家、身份、广告归因对**实际结算金额**的影响，应固定版本、关卡、当前余额、账号阶段、领取次数、广告类型与时间，进行交叉实验并重复采样。后续探测补充了不同注册身份的初始化配置；各国后续关卡结算与真实买量归因仍未验证，不推断全服固定金额。

## 画面对照范围

[第三关原版与回放截图](comparison.html)按相同比例显示。金额、标题、按钮、奖励图位置可对照；不声明逐像素完全相同。彩纸粒子由游戏胜利流程触发，独立面板回放未触发该流程；第一、二关引导手势也未纳入这个面板级校验。

原始设备采集保存在 `C:/Projects/Golden  Dragon Legend/reconstruction-nut/reward-device-evidence`。本目录只收录奖励数据和不含账号标识的比较结果，不分发完整 PlayerPrefs。
