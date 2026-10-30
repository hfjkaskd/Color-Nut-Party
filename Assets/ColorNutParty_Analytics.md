# Color Nut Party 业务打点

实施日期：2026-10-14。字段版本：1。状态：24 个业务事件已接入运行时，验证结果见文末。

本文是 Assets 下唯一的业务打点主文档，替代此前“待确认、尚未实施”的方案状态。保留 withdraw_submit、withdraw_submit_result 的独立事件名称，保留异常退出；不增加签到、皮肤、商店等新功能或事件。

## 上报入口与配置

- 所有业务事件：游戏程序集 → LocalAnalyticsService → 入口场景 SDK 节点上的 LocalAnalyticsBridge → SDKAdapter.Track。SDKAdapter 再转发到 Bizza 和数数。
- 新业务用户属性：仅通过 LocalAnalyticsBridge 调用 BizzaAnalyticsAgent.BUserProp，设置绝对值，不调用数数属性接口。
- 配置：Assets/Resources/Configuration/LocalAnalytics.asset。默认开启，前台汇总间隔 60 秒，后台满 1800 秒切分会话，初始化缓冲上限 256 条，异常指纹上限 20 个、聚合窗口 60 秒。
- DiagnosticLog 可在 Inspector 关闭。开启时最多记录 2048 条本进程事件到 Application.persistentDataPath/cnp_analytics_diagnostic.jsonl，文件超过 2 MiB 后在下次启动轮换。记录 forwarded 表示已调用 SDKAdapter，不能据此宣称后台已入库。
- SDK、场景、资源加载和业务逻辑使用同一运行路径。没有基于 Editor 的业务兜底。测试通过相同运行代码及独立存档执行，明确配置 is_test=true。
- Assets/SDK 的原代码未修改。MAX 既有编译问题用 Assets/MaxSdk/Scripts/MaxSdk.Scripts.asmref 解决，使补充类型进入原 MAX 程序集。

## 公共字段

| 字段 | 类型 | 含义 |
|---|---|---|
| schema_version | int | 当前为 1 |
| event_id | string | 每个业务事件的唯一 ID；同一份 payload 转发至两个后端 |
| event_utc_ms | long | 实际事件时刻，UTC 毫秒；启动缓冲不改变它 |
| app_session_id | string | 冷启动或后台超过会话阈值生成的会话 ID |
| sequence | long | 本会话递增序号 |
| app_version | string | Application.version |
| build_id | string | 配置的 Android version code，当前为 1 |
| is_test | bool | 配置中的测试标记；独立设备测试包设为 true |
| current_page | string | 当前已打开的白名单弹窗或 gameplay/loading |

SDK 的 uid、playerID、ts、dt、batch_id、归因等字段由 SDK 提供，业务不伪造或覆盖。

关卡上下文包含 attempt_id、level_id、display_level、content_level_id、sub_round、sub_total_round、observation_scope；教学尝试附带 step_id。完整新尝试另有 attempt_no、初始螺杆数/螺母数/颜色数。旧存档已有棋盘首次观测为 partial，不回填历史开始、首操作或初始棋盘。

## 24 个事件及实际触发点

| 事件 | 触发点 | 主要专属字段和边界 |
|---|---|---|
| first_open | 本地分析标记首次不存在 | first_open_basis=local_marker_absent；清数据后可以再次发生，不等于商店首次安装 |
| session_start | 冷启动，或实际后台 ≥1800 秒后返回 | reason=cold_start/background_timeout；焦点变化不单独切会话 |
| startup_result | 主界面和新棋盘/恢复待领奖已就绪；启动阶段游戏异常 | result、phase、duration_seconds、失败时 error_code；每进程一次 |
| session_summary | 前台每 60 秒、进入后台、正常退出 | summary_seq、window_start_ms、window_end_ms、report_reason、foreground_seconds、gameplay_seconds；只记录本窗口增量 |
| tutorial_start | 首次欢迎/教学步骤实际展示 | guide_id=initial_guide、guide_version=1；老存档不补报历史 |
| tutorial_step_show | 欢迎以及 teaching_0～3 实际进入 | guide_id、step_id、step_index；同一步首曝一次 |
| tutorial_step_complete | 欢迎接受或教学棋盘真实成功 | guide_id、step_id、step_index、elapsed_seconds；每步完成一次 |
| tutorial_complete | 最后一阶段教学成功 | guide_id、elapsed_seconds；当前本地引导一次 |
| level_start | 新尝试的棋盘已就绪 | 关卡上下文、origin、initial_screw_count、initial_nut_count、color_count；恢复同一棋盘不再发送 |
| level_first_action | OriginalGameScene 收到 Moved 操作 | first_action_type=move、time_to_first_action_seconds；选中、回退和非法目标不算；partial 恢复不补报 |
| level_result | 首次成功/失败或确认重开 | result=win/fail/restart、reason、elapsed_seconds、active_seconds、valid_move_count、undo_used、refresh_used、add_screw_used；同 attempt 只保留一个终局结果 |
| prop_use | OriginalItemManager 的道具消费保存成功 | operation_id、prop=undo/refresh/add_screw、source；打开弹窗或取消刷新选杆不算使用，无库存金额字段 |
| popup_show | 白名单 Prefab 实例启用 | popup_id、popup_instance_id、entry=game_flow；每次显示实例一次 |
| popup_action | 现有 Button 回调通过原交互防重后 | popup_id、popup_instance_id、action_id、可选 selected_id/operation_id；不记录输入框内容 |
| popup_close | 已展示实例关闭/隐藏 | popup_id、popup_instance_id、reason、visible_seconds；用户关闭与流程替换区分，销毁不重复记录 |
| reward_claim_result | 关卡/幸运奖励入账保存成功，或激励领取结果明确 | operation_id、claim_id、reward_source、result=granted/not_granted、reason、道具时 prop；一次领取多资源只报一个结果，不附带金额 |
| withdraw_eligibility_change | 首次进入现金/钻石档位，或已观察资格变化 | flow_id、withdraw_type、tier_id、observation=initial/change、eligible、blocking_condition、可选 previous_eligible、status_source=client |
| withdraw_account_result | 原资料验证错误或资料本地保存成功 | flow_id、operation_id、channel、result=validation_error/local_saved、validation_reason、is_edit；不发送姓名、邮箱、电话或哈希 |
| withdraw_submit | 最终提现提交；前两次流程为最终本地验证确认 | operation_id、flow_id、withdraw_type、tier_id、channel、amount、value_unit、status_source=client、request_sent=false；不在查看/选档/填写时发送 |
| withdraw_submit_result | 同一次提交的本地处理返回 | 同一 operation_id、result=not_submitted、reason=client_requirements_not_met/service_unavailable、status_source=client、request_sent=false；前两次另带 local_record_status=pending_local_record |
| withdraw_task_change | 首次条件快照、真实条件改变的保存边界 | flow_id、withdraw_type、tier_id、condition_id、state=unknown/waived/complete/pending、已知时 progress/target、source；相同 Evaluate/Refresh 不重复发送 |
| runtime_exception | 游戏命名空间可识别异常 | fingerprint、component、error_code、exception_type、count、first_utc_ms、last_utc_ms；先发一次，随后 60 秒聚合；不发送原始异常消息/栈以防资料泄漏 |
| abnormal_exit | 冷启动发现上次运行标记未正常闭合 | previous_run_id、previous_session、previous_page、previous_build_id、last_app_state、last_observed_utc_ms、observed_utc_ms、reason=unclosed_run、diagnostic_source=local_run_marker、confirmed=false |
| resource_change | OriginalUserStore.SaveData 成功后，实际资源差值非零 | transaction_id、operation_id、mutation_index、source、resource、value_unit、balance_before、delta、balance_after；UI 刷新、载入、预生成奖励和属性同步不产生流水 |

## 唯一资源统计口径

资源枚举是显式字符串：soft_cash/game_cash、diamond/diamond_count、undo/refresh/add_screw/item_count。现金是游戏账本余额，不代表银行付款；数值按实际存储单位上报，不固定乘 100，也不把本地化显示当汇率换算。

资源流水在存档保存处统一比较前后余额。transaction_id、operation_id 和 mutation_index 在同一次资源保存前分配，并把最近交易与递增序号写入 LocalAnalyticsData。游戏实际变更和保存成功后才转发事件。SDK 没有逐事件送达确认，不能承诺崩溃任意时刻均无丢失或后端绝对只收到一次；后台仍应按 event_id 或交易键去重。

| 来源 source | 实际变更 |
|---|---|
| level_reward | 关卡领取的现金及钻石，保存后分别发对应资源流水 |
| lucky_reward | 幸运奖励领取后的实际现金增量 |
| screw_completion_reward | 螺母完成奖励的现金及钻石增量 |
| screw_reward_recovery | 既有奖励恢复逻辑实际补入的差值 |
| tutorial_balance_adjustment | max(旧余额,配置目标) 的真实差额；余额已高于目标不记增加 500 |
| early_reward_record_transfer | 前两份赚取的现金转入本地记录时的余额减少；不表示打款 |
| rewarded_tool / prop_reward | 激励实际获得道具或既有道具获得入口 |
| prop_use | 实际道具消费 |
| game_state_change | 其他现有保存入口的实际资源变化；用于发现未专门标注来源的路径 |

先得到道具再消费，即使最终库存不变，仍有 +1 和 -1 两条流水，使用次数只查 prop_use。关卡和幸运领取行为只查 reward_claim_result。禁止把二者数量、申请提现金额、广告收入和资源 delta 相加。SDK 双端转发不代表业务发生两次。

## 11 个 Bizza 业务属性

cnp_schema_version、cnp_current_level、cnp_display_level、cnp_tutorial_completed、cnp_cash_balance、cnp_diamond_balance、cnp_undo_count、cnp_refresh_count、cnp_add_screw_count、cnp_user_rank、cnp_has_withdraw_account。

存档及配置加载完成后首次同步。后续只更新实际变化的绝对值。前述账户属性只记录是否已有本地资料，数据不包含资料文本。Bizza 的 user_profile_change 不作为另一份资源流水累计。

## 弹窗白名单

入口场景 SDK 节点已配置桥接组件，23 个现有 Prefab 已挂载 LocalAnalyticsPopup：关卡结算、幸运奖励、失败、重开、设置、用户等级、三种道具、现金/钻石提现、提现条件、资料输入/确认、早期本地验证、提现加载/阶段/复查、欢迎、教学、玩法解锁、提现历史、早期现金奖励提示。

事件绑定由代码完成。统计组件不创建 UI、不改变界面布局或交互规则。只记录实际启用的实例，不因某个遗留脚本存在而把排队、付款完成等不可达页面算作已上线功能。短提示与奖励飞行动画不扩展成新业务事件。

## 原表删改原则的保留

45 个原事件中，本次保留 23 个并增加 resource_change。SDK 原有 login、MaxTrack 广告生命周期、ad_revenue、设备/归因属性和数数自动应用事件继续使用现有来源，不再另外创建同义广告、登录或设备事件。

app_state_change/session_end 合并到 session_summary；page_view/popup_trigger 缩小到真实 popup_show；popup_fail 合并到有来源的 runtime_exception；withdraw_stage_change 合并到资格变化。没有现有业务来源的 tutorial_skip、withdraw_status_change、user_segment_change、permission_result、recall_action、anr_report、register 本次不启用。

## 核心代码

- Runtime/Content/LocalAnalyticsService.cs：统一字段、会话、尝试、教学、资源流水、条件去重、异常退出。
- Runtime/Content/OriginalUserStore.cs：实际保存成功后唯一的资源差值发送边界。
- Runtime/Content/OriginalItemManager.cs：道具获得/消费、使用次数；只包装原有行为。
- Runtime/World/OriginalGameScene.cs：真实有效移动、首次胜负、重开、恢复标志。
- Runtime/UI/LocalGameplayController.Analytics.cs 及既有流程分部文件：奖励、提现和上下文关联。
- Runtime/UI/LocalAnalyticsPopup.cs：Prefab 可见生命周期及有效 Button 操作。
- Integration/LocalAnalyticsBridge.cs：唯一 SDKAdapter.Track 和 Bizza 新属性调用点。
- Editor/Validation/LocalAnalyticsPolicyValidation.cs、LocalAnalyticsPlayValidation.cs：策略边界和生产 Prefab/场景实际操作验证。

## 验证记录

机器可读报告存放在 Docs/AnalyticsValidation/20261014-implementation/。策略验证覆盖首次观测、会话切分、恢复去重、失败后重开、先赠后耗、多资源领取、教学步骤、条件重复刷新、两个提现事件、异常聚合、正常退出与未闭合运行。Play Mode 使用现有生产 Prefab、Button 指针事件和独立存档，检查重复广告回调、前两份记录、500 余额引导、奖励重试、实际资源差值及资料隐私。

本轮结果：策略检查 63 项通过；生产 UI Play Mode 检查 597 项通过。MuMu 独立测试包实际完成四阶段教学及首次结算，连续点击只产生一次现金流水和一次领奖结果；重启后首次观测、教学完成和已领取奖励未重复记录，未闭合运行事件保留 confirmed=false。数数返回 HTTP 200，Bizza 返回 HTTP 201；取证时 Bizza 无待重试批次、无丢弃，仍有一条按 SDK 批量策略等待的缓存。原安装包存档未清除。测试包和业务事件使用 is_test=true，工程已恢复正式包名及 is_test=false。设备的资料错误分支已验证；资料保存、最终两个提现事件、道具与重试覆盖来自生产 UI Play Mode/策略验证。HTTP 返回和客户端缓存证据已保存，后台报表尚未登录查询，不能把传输成功等同最终报表入库。
