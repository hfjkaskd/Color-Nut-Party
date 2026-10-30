# 本地多回合关卡推进

2026-10-09 当前策略：按用户要求，小圆点对应的每个小局都显示普通结算。`CompleteLocalLevel` 每次棋盘完成均生成并保存 `LocalPendingReward`，显示原有 SuccessPanel；等待玩家领取后才经关闭动画进入下一棋盘。显示关卡、轮次、子回合点阵和原始关卡表不变。

现金与钻石逐小局使用现有余额分档规则生成，领取时各入账一次；末小局没有额外整组奖励。未领取重启恢复同一奖励，领取后重启不重复发放。广告仍按既有普通领取次数阈值判断，小局领取也计入；普通广告失败继续基础结算，额外激励仍要求成功。第一关教学步骤保持原逻辑。

`LocalSubroundPlayValidation` 已改为连续操作原内部 9/10/11/12 四个棋盘并分别领取，检查四次弹窗、现金与钻石、等待、重载、重复点击及晚到广告回调；当前验证和构建结果保存在 `Docs/SubroundSettlement/20261009`。

以下为此前“整组末局才结算”的历史实现与验证记录，已由上面的用户指定策略替代：

差异：默认 LocalGameplayController 之前忽略 OriginalSuccessFlow 捕获的关卡表项，每次核心棋盘完成都创建过关面板。源 Success 响应回调 0xA00684 比较捕获表项的 SubTotalRound / SubRound；中间子回合直接 InitLevel(true,false,false)，不会走结算面板。原表中内部 4/5/6 关的 TotalRound/Round 为普通回合，但 SubTotalRound/SubRound 均为 0，仍应逐局结算；内部 9/10/11/12 关才是 SubTotalRound=4 的 1/2/3/4 子回合，前三局自动续局。

修复将捕获表项传入本地完成入口。保留用户批准的本地 Level+1、LevelSeed=0、TodayPassLevelCount+1，并先以空 LevelInfo 保存待重建进度；如果是中间子回合，则按原参数自动初始化下一棋盘；末回合仍显示原版过关预制体变体，等待标准 Next Button 的关闭动画后进入下一关。原 SuccessFlow 的 0.6 秒请求边界和仅末回合庆祝保持原实现。没有构造服务器响应或奖励数据。

验证使用默认启动入口，从显式 Level=9、已确认首次玩法解锁的用户状态开始。棋盘由原关卡库实际加载，验证脚本只通过有界离线搜索选择输入，然后发送世界相机射线，等待真实搬移与完成动画。依次完成原内部 9/10/11/12 关，检查中间局没有瞬间弹出结算、显示关卡牌始终为 Level 5、三次自动重建与第四局末尾结算。第一次自动重建前立即重载场景，检查空棋盘检查点已写入、续局不重复计数。最后通过实际 UI 射线点击 Next，重复点击不重复推进，进入内部第 13 关。源脚本与数据均未被测试替换。

LocalSuccessExitPlayValidation 保留内部 4 的普通回合夹具，验证普通回合仍显示结算，并覆盖原版面板关闭期间退出后的恢复。LocalGameplayPlayValidation 的早期棋盘求解器仅增加同 Editor 程序集内的访问权限以供复用；运行时不包含该求解器。

此修复覆盖正常本地核心多局链路；奖励依赖的内部第六关新手奖励面板仍按既有 SDK 跳过范围处理，不把本地推进当作服务器奖励复刻完成。

Library/local-subround-play-final.log 记录 NUT_LOCAL_SUBROUND_PLAY_PASS：实际四局分别完成 36/35/68/41 次搬移输入后推进，所有关卡通过原库与原规则运行。早先 local-subround-play.log 的失败来自测试误将普通 Round 当作 SubRound；以直接读取原表后的最终验证为准，运行分支始终使用源 SubTotalRound/SubRound 字段。

Library/local-subround-ordinary-exit-play.log 包含 NUT_LOCAL_SUCCESS_EXIT_PLAY_PASS，保留原内部第 4 关普通回合的结算与关闭中退出恢复。Library/local-subround-content-validation.log 完成 166 个 PASS 标记和最终 NUT_CONTENT_VALIDATION_PASS；启动时另记录 UnityEditor.Android.AndroidDeploymentTargetsExtension.GetKnownTargets 在清理已退出设备扫描进程时的 InvalidOperationException，属于 Editor Android 扫描堆栈，不将该日志称为零异常。既有对象池退出诊断仍未处理。

Library/local-subround-android-build.log 构建成功，Unity 报告 0 错误、0 警告；新 APK 签名与包信息复核通过，校验值见 AndroidLocalBuild.md。最新 local-subround-success-current.png 已检查，原生标题、彩纸及 Next 按钮显示正常。本轮场景重载不能替代 Android 系统强杀/恢复或真机视觉对照。
