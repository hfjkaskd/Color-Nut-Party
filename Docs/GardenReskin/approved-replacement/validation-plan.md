# 四页替换后的验证方案

2026-09-22 只读审查；本文件不代表已执行验收。审查时此工程 Unity PID 30596 仍打开，未启动、关闭或控制 Unity，未改 Assets。

## 本轮入口与证据

| 页面 | 现有入口 | PASS 标记 | 运行截图 |
| --- | --- | --- | --- |
| Level withdrawal | LocalWithdrawalPlayValidation.Run | NUT_LOCAL_WITHDRAWAL_PASS | local-withdrawal-cash-BR.png / ID.png |
| Enter information | 同上 | 同上 | local-withdrawal-account-BR.png / ID.png |
| My diamonds | 同上 | 同上 | local-withdrawal-coin-BR.png / ID.png，以及 local-diamond-withdraw-feedback-BR.png / ID.png |
| Refresh | LocalToolOfferPlayValidation.Run | NUT_LOCAL_TOOL_OFFER_PASS | local-tool-offer-refresh.png |

所有入口前缀均为 `NutSort.Validation.`。runner 的这两组方法、标记及 9 个截图名与当前源码完全匹配。两组均以显式 Level 4 存档夹具启动实际游戏场景，并使用现有实际 Prefab。广告成功使用测试回调，不代表真实广告 SDK；提现无实际付款。

## 已确认缺口

1. `LocalWithdrawalPlayValidation.cs:45` 请求 cash 截图后，第 46 行在同一帧点击 Withdraw。截图在帧末落图，能拍到账户切换/缩放中状态。必须使用已有 `capture_withdrawal_settled.ps1`，其精确替换块仍匹配当前代码：仅临时插入 phase 101，等待 0.2 秒后才点击。其余账户、钻石、反馈截图均已与后续操作间隔至少 0.3 秒。Refresh 截图与关闭间隔 0.3 秒，没有同帧切页问题。
2. 两组固定 `720×1280`（9:16），不能代表用户实际 `1080×2400`（9:20）。单纯给 Unity CLI 传屏幕参数不会覆盖测试内的 `PlayModeWindow.SetCustomRenderingResolution`。需补 1080×2400 的实际 Game View 截图；不得把已有 720 图标作 1080 图。最后恢复用户原 Game View 分辨率。
3. 两组所有交互均调用 `Button.onClick.Invoke()`，绕开 `EventSystem` 射线、`Button.IsInteractable()` 和 pointer down/up。PASS 证明绑定逻辑可执行，不能证明鼠标可点击、透明 Image 不遮挡。账户输入还直接设置 TMP 字段值，因此未测聚焦、输入、光标、剪裁和键盘流程。
4. 现有 `LocalTeachingClickFeedbackPlayValidation` 已有 `RaycastAll` 首命中检查及标准 `pointerClickHandler`，并固定 1080×2400。它覆盖教程 HUD、Level 2 Cash 打开/关闭和 Replay，但未覆盖账户、钻石选项、Refresh 领取按钮。不能用它的历史 PASS 代替本轮四页点击验收。该入口不在 runner 的 ValidateSet 中，需单独调用。
5. runner 验收 exit 0、PASS 和截图更新时间，但未读 PNG 尺寸、未判断画面空白/裁切，也未把所有非致命 Error/Exception 当失败。报告后须独立读新图和日志；旧图时间戳允许 -2 秒，最终还应记录本次复制的 SHA-256 与实际 IHDR 尺寸。
6. runner 的同工程进程判断未标准化 `/` 和 `\`，且遇到空 CommandLine 会跳过。当前 PID 的路径可被识别，但执行前仍须独立按标准化路径检查。settled 包装器已标准化分隔符且拒绝无法识别的 Unity 进程。runner 的 WaitForExit 没有进程外超时，测试的 90 秒截止仅在 Play 模式 Tick 生效；若导入/编译/进入 Play 卡住，须先查看当前日志，不得并行重开或清锁文件。

## 推荐执行顺序

1. 资源冻结后，先对照本轮替换前快照进行 SHA-256 差分。保护全部旧 Prefab/Scene、脚本、meta、配置、材质、动画和原按钮引用；只放行明确列出的目标图或已批准字段。不能使用最初全量换皮基线，把后来已批准的 UI 点击修复、底栏和教学进度条修改误报为新回归。
2. 由根代理保管当前 Editor 会话；确认未保存工程工作后，正常结束当前 Play 和工程 Editor，独立确认目标工程进程及导入 worker 已退出。此审查不执行关闭。不在交互中的 Editor 直接调用 `Run()`：每个验证器 `Finish` 都执行 `EditorApplication.Exit`，会关闭整个 Editor。
3. 使用新的输出目录顺序运行下方两个命令，不并行启动同一工程。第一条保留精确源备份、临时副本、补丁审计及 SHA 恢复证据；结束后必须确认 `sourceRestored=true`，且原 Editor 测试文件哈希与本轮基线相同。

```powershell
& 'C:\Projects\com.nut.sort.relax\Tools\GardenReskin\capture_withdrawal_settled.ps1' -OutputDirectory 'C:\Projects\com.nut.sort.relax\Docs\GardenReskin\approved-replacement\runtime-withdrawal'
& 'C:\Projects\com.nut.sort.relax\Tools\GardenReskin\run_validation.ps1' -Suites tooloffer -OutputDirectory 'C:\Projects\com.nut.sort.relax\Docs\GardenReskin\approved-replacement\runtime-tooloffer'
```

若目录已存在，换唯一新目录；不覆盖旧证据。两组成功/失败完成时 `OriginalPreferenceFixture.Restore()` 恢复 `OriginalUserStore.Key` 的原值。崩溃时备份保留 `Library/ValidationBackups/GameplayUserLocalData.bin`，应先恢复/确认，不删除它。

4. 独立检查 9 图：预览方向与实际 Unity 相符，动态金额/文字未烘焙进图，所有标题/数字/奖励完整、无边缘侵占按钮。检查账户长文本、BR/ID 两地金额格式，钻石四档的选中与未选中状态，Refresh 图标和 Claim。运行时面板开场当前为 0.25 秒，账户标题为延迟 0.15 + 0.25 秒；截图前至少等待 0.7 秒并确认缩放稳定。每次截图请求后到下一次状态修改至少跨一帧，现有分阶段 0.2～0.4 秒满足。
5. 在恢复的交互式 Editor 里，以实际 1080×2400 Game View 做下表的 OS 鼠标检查并保存四页截图；这能补最关键输入链与宽高比缺口。操作会改变夹具存档，须延续已有备份/恢复方式，不能拿用户正式余额做奖励测试。不改变教学门禁来进入这些页面，使用现有非教学存档夹具。
6. 最后再做静态差分，确认 settled 临时 Editor 源已恢复，Prefab/脚本/meta 无测试残留变化。将 AI 预览、Prefab 静态预览、Unity Play 截图明确区分。

## 1080×2400 点击检查最小矩阵

| 页面 | 必须真实命中的交互 | 不变量/结果 |
| --- | --- | --- |
| Level withdrawal | 主界面 Cash、Close、重新打开后 Withdraw | 单实例，面板出现时棋盘受阻；关闭后恢复；进度/金额正确；无扣款/奖励 |
| Enter information | 至少各可见输入框聚焦/输入；每个可见渠道；空表提交；关闭；测试数据提交到确认页再返回 | 不被背景/图标挡住；空输入有反馈；渠道选择与字段显隐保持；动态输入不越界；原余额不变 |
| My diamonds | Coin Cash、四个金额档、Withdraw、Close | 各档选中态/数额/提示随档变化；不足余额仅提示；不扣钱；关闭恢复棋盘 |
| Refresh | 零库存 Exchange、Close；重新打开、Claim；等待完成后取消交换 | 关闭不触发广告或补发；Claim 才请求广告；重复回调不重复发奖；原使用方式不变 |

若增加仅验证用途的自动化，复用已有教学测试 `Click(Button)` 的实现：`Canvas.ForceUpdateCanvases` → 按当前 Canvas 相机把 Button 中心转换到屏幕 → `EventSystem.RaycastAll` → 第一命中所对应的 `IPointerClickHandler` 必须正是目标 Button → `ExecuteEvents` 的标准 down/up/click。不能跳过命中遮挡直接把事件发到目标，也不能用 `onClick.Invoke` 声称“鼠标验收”。实际输入框和画面边缘仍需 OS 鼠标核对。本文不新建测试、不修改游戏逻辑。

## 不列作本轮通过项

旧 `tools` 和 `LocalToolAdPlayValidation` 仍假设零库存直接请求广告/发工具，缺少当前 ToolOffer Claim 步骤；旧 `gameplay` 成功奖励数量断言已过时。这些未运行，不能列为通过，也不为其旧断言改业务。

提现测试覆盖空输入、确认/返回、选渠道、余额不变和关闭释放；道具测试覆盖关闭无奖、Claim 才广告、成功只发一次。它们没有验证真实广告/支付服务、真机键盘、全部失败广告分支。这些限制应准确标注，不用放大成此次纯美术替换的新增功能要求。
