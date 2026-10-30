# 批准效果图替换：三页 Prefab 只读映射

检查对象：`C:/Projects/com.nut.sort.relax`。对照 `Docs/GardenReskin/previews-clean-boundaries/` 的 Level Withdrawal、Enter Information、Refresh 三张批准图。仅生成本说明，未修改 Assets、未操作 Unity。

## 结论与优先实施项

1. 视觉移除三个独立装饰 Image：TXPanel 的 `main/Image (1)`，TXUserInfoPanel 的 `main/Image (1)`，ItemGetPanel 的 `main/BG (1)`。只抑制各自 Image 渲染，保留节点、RectTransform、父子关系；它们都不是 Button。前两张 PNG 还被其他 Prefab 共用，不应全局改成透明。
2. 核心主背景、顶部标题、按钮、进度轨道/填充均应分件换图，运行时动态文字、图标、按钮绑定仍由原组件承担。不要将预览中的文字、金额、进度值、支付商标或选中勾烘焙进背景。
3. ItemGetPanel 已有独立 `Resources/atlas/tooloffer/` PNG；另两页多为 `Assets/Sprite/*.asset` 引用 `Assets/Texture2D/*.png`。覆盖 PNG 会影响表内其他共享 Prefab，局部美术可考虑新增同尺寸 Sprite 并仅改目标 Image 的视觉资源引用。

## 多余底板的精确目标

| Prefab / node | Image fileID | 原资源 | RectTransform size / anchoredPosition | 原因 |
|---|---:|---|---|---|
| TXPanel / main/Image (1) | 114344419225485095 | `S/bg1_2.asset → T/bg1_2.png` | 881×734 / (0,355) | 金额、进度、说明背后的二次大内框；批准图该区域使用主背景奶油底，只有金额卡与进度条独立木框 |
| TXUserInfoPanel / main/Image (1) | 114291844617409189 | `S/bg3_0.asset → T/bg3_0.png` | 925×443 / (0,-85.99997) | 输入区的大底板，当前两端凸出；批准图保留独立输入框并露出主面板背景 |
| ItemGetPanel / main/BG (1) | 114302127217294661 | `A/tooloffer/bg1_5.png` | 811×658.5 / (0,129.24997) | 图标卡与说明外侧额外大框；批准图只保留 BG 主背景和 BG (2) 图标卡 |

不要误移除：TXPanel `main/Image (2)` 是金额深木卡；ItemGetPanel `main/BG (2)` 是 Refresh 图标卡。根 Image 是全屏遮罩，不是上述多余底板。输入框本体、支付选择、关闭、确认、领取按钮均为交互图像，应保留可见表现。

三处待移除 Image fileID 在各自 Prefab 只出现于 GameObject component 列表与组件定义，没有任何脚本字段、Button targetGraphic 或其他序列化绑定引用。

## 映射表说明

- `S/` = `Assets/Sprite/`，`T/` = `Assets/Texture2D/`，`A/` = `Assets/Resources/atlas/`。
- node 路径省略 Prefab 根；“size @ pos”均为现有 RectTransform 逻辑单位，相对直接父节点，坐标向上为正。未写 scale 的节点为 (1,1,1)。
- m_Type：0 Simple、1 Sliced、3 Filled。Border 顺序为 (left,bottom,right,top)，来自实际 Sprite .asset 的 `m_Border`；直接 PNG 使用 importer 的 `spriteBorder`。0 表示四边全为0。
- 共用列表是全部序列化 Prefab 引用（包含同 PNG 的 Sprite 别名）；代码 Resources.Load 的动态用途另列，不能仅依赖该列表判断影响范围。

## TXPanel.prefab

| node [Image fileID] | Sprite → PNG | PNG px | Type / Border | size @ pos | 共享 Prefab |
|---|---|---:|---|---|---|
| (root遮罩) [114946936116752871] | `无` | — | 1 /  | 0 @ 0 | — |
| main/1/Title [114694121525821480] | `S/title1.asset` → `T/title1.png` | 979×167 | 0 / 0 | 975,163 @ 0,860 | TXPanel |
| main/2/Title [114808113985169905] | `S/title1.asset` → `T/title1.png` | 979×167 | 0 / 0 | 975,163 @ 0,726 | TXPanel |
| main/Image [114841364053301648] | `S/bg_11.asset` → `T/bg_11.png` | 983×1034 | 0 / 0,149.924,0,124.924 | 983,1034 @ 0,231 | TXLevelPanel, TXPanel |
| main/Image (1) [114344419225485095] | `S/bg1_2.asset` → `T/bg1_2.png` | 881×734 | 0 / 0,87,0,106 | 881,734 @ 0,355 | TXLevelPanel, TXPanel |
| main/Image (2) [114833703512093738] | `S/bg2_0.asset` → `T/bg2_0.png` | 850×376 | 0 / 0 | 850,369 @ 0,525 | TXLevelPanel, TXPanel |
| main/Title1 [114726790013014859] | `S/title_9.asset` → `T/title_9.png` | 975×163 | 0 / 0 | 975,163 @ 0,757 | TXPanel |
| main/Close [114571718729274231] | `S/close.asset` → `T/close.png` | 118×120 | 0 / 0 | 116,117 @ 437,740 | TXChannelPanel, TXCoinPanel, TXLevelPanel, TXPanel, TXUserInfoPanel, TXUserInfoSurePanel |
| main/ItemIcon [114722951815217645] | `A/Golds/US1.asset` → `A/Golds/US1.png` | 111×95 | 0 / 0 | 111,95 @ -285,458 | Gold, GoldItem, LocalMainPanelTop, MainPanelComplete, MainPanelTop, TXPanel |
| main/TXBtn [114572839542275309] | `S/btn_0.asset` → `T/btn_0.png` | 764×192 | 0 / 0 | 668.9,192 @ 0,-134 | TXLevelPanel, TXPanel |
| main/Progress [114370222726513266] | `S/bar_bg_1.asset` → `T/bar_bg_1.png` | 840×73 | 0 / 0 | 840,73 @ 0,210 | TXPanel |
| main/Progress/Bar [114393105877652414] | `S/bar_1.asset` → `T/bar_1.png` | 836×65 | 3 / 0 | 836,66.11 @ 0,3.19 | TXPanel |
| main/Progress/Tip [114534704304255049] | `S/tip_bg_0.asset` → `T/tip_bg_0.png` | 255×93 | 0 / 0 | 255,93 @ 0,70，scale=0,1,1 | TXPanel |
| main/Image [114703931505913403] | `S/title3.asset` → `T/title3.png` | 457×84 | 0 / 0 | 457,84 @ 0,616 | TXLevelPanel, TXPanel |

## TXUserInfoPanel.prefab

| node [Image fileID] | Sprite → PNG | PNG px | Type / Border | size @ pos | 共享 Prefab |
|---|---|---:|---|---|---|
| (root遮罩) [114808113985169905] | `无` | — | 0 /  | 0 @ 0 | — |
| main/Image [114220653531496868] | `S/bg_12.asset` → `T/bg_12.png` | 988×1421 | 0 / 0 | 988,1421 @ 0,68 | TXUserInfoPanel, TXUserInfoSurePanel |
| main/Image (1) [114291844617409189] | `S/bg3_0.asset` → `T/bg3_0.png` | 925×443 | 0 / 59.9239,59.9239,59.9239,59.9239 | 925,443 @ 0,-86 | TXUserInfoPanel, TXUserInfoSurePanel |
| main/Title [114693329660128220] | `S/bg1.asset` → `T/bg1.png` | 476×84 | 0 / 0 | 476,84 @ 9,716 | TXChannelPanel, TXProgress0Panel, TXUserInfoPanel, TXUserInfoSurePanel |
| main/Close [114477046003934300] | `S/close.asset` → `T/close.png` | 118×120 | 0 / 0 | 116,117 @ -20,-751，scale=1,1,1.00009 | TXChannelPanel, TXCoinPanel, TXLevelPanel, TXPanel, TXUserInfoPanel, TXUserInfoSurePanel |
| main/Get [114689364072382846] | `S/btn1.asset` → `T/btn1.png` | 764×192 | 0 / 0 | 764,192 @ 0,-459，scale=1,1,1.00009 | SuccessPanel, TXChannelPanel, TXCoinPanel, TXGuideTargetCompletePanel, TXProgress0Panel, TXTooFastHintPanel, TXUserInfoPanel, TXUserInfoSurePanel |
| main/InputName [114384403120170545] | `S/bg2_4.asset` → `T/bg2_4.png` | 552×157 | 1 / 66.9239,66.9239,65.9239,64.9732 | 822,156 @ 0,2.00003 | TXChannelPanel, TXProgress0Panel, TXUserInfoPanel |
| main/InputAreaNumber [114571878917323452] | `S/bg2_4.asset` → `T/bg2_4.png` | 552×157 | 1 / 66.9239,66.9239,65.9239,64.9732 | 253.865,156 @ -284.068,-175.7 | TXChannelPanel, TXProgress0Panel, TXUserInfoPanel |
| main/InputNumber [114121005239445321] | `S/bg2_4.asset` → `T/bg2_4.png` | 552×157 | 1 / 66.9239,66.9239,65.9239,64.9732 | 552,156 @ 133.71,-175.7 | TXChannelPanel, TXProgress0Panel, TXUserInfoPanel |
| main/ToggleGroup/Toggle/Background [114007513189690300] | `A/pay/LINE Pay.asset` → `A/pay/LINE Pay.png` | 513×303 | 0 / 0 | 256.7,166.8 @ 0 | TXUserInfoPanel |
| main/ToggleGroup/Toggle/Background/Image [114190181876552715] | `无` | — | 0 /  | 252.45,161 @ 0，初始禁用 | — |
| main/ToggleGroup/Toggle/Background/Checkmark [114998874197659892] | `S/duigou.asset` → `T/duigou.png` | 139×105 | 0 / 0 | 139,105 @ 0,-57 | TXUserInfoPanel |
| main/ToggleGroup/Toggle1/Background [114132862189046193] | `A/pay/paypal.asset` → `A/pay/paypal.png` | 513×303 | 0 / 0 | 256.7,166.8 @ 0 | TXUserInfoPanel, TXUserInfoSurePanel |
| main/ToggleGroup/Toggle1/Background/Image [114386943702330414] | `无` | — | 0 /  | 252.45,161 @ 0，初始禁用，scale=1.00003,1.00003,1.00003 | — |
| main/ToggleGroup/Toggle1/Background/Checkmark [114561126880652180] | `S/duigou.asset` → `T/duigou.png` | 139×105 | 0 / 0 | 139,105 @ 0,-57 | TXUserInfoPanel |
| main/ToggleGroup/Toggle2/Background [114232094423628703] | `A/pay/paypal.asset` → `A/pay/paypal.png` | 513×303 | 0 / 0 | 256.7,166.8 @ 0 | TXUserInfoPanel, TXUserInfoSurePanel |
| main/ToggleGroup/Toggle2/Background/Image [114570723408815475] | `无` | — | 0 /  | 252.45,161 @ 0，初始禁用，scale=1.00003,1.00003,1.00003 | — |
| main/ToggleGroup/Toggle2/Background/Checkmark [114992816746925483] | `S/duigou.asset` → `T/duigou.png` | 139×105 | 0 / 0 | 139,105 @ 0,-57 | TXUserInfoPanel |
| main/InputEmail [114291632710222552] | `S/bg2_4.asset` → `T/bg2_4.png` | 552×157 | 1 / 66.9239,66.9239,65.9239,64.9732 | 822,156 @ 0,-175.7，初始禁用 | TXChannelPanel, TXProgress0Panel, TXUserInfoPanel |
| main/ChannelInfo [114589652054545887] | `S/？.asset` → `T/？.png` | 69×71 | 0 / 0 | 69,71 @ 414.5,716.4，scale=1,1,1.00009 | TXUserInfoPanel |

## ItemGetPanel.prefab

| node [Image fileID] | Sprite → PNG | PNG px | Type / Border | size @ pos | 共享 Prefab |
|---|---|---:|---|---|---|
| (root遮罩) [114256069497284392] | `无` | — | 1 /  | 0 @ 0 | — |
| main/BG [114621510681728303] | `A/tooloffer/bg_0.png` | 907×953 | 1 / 0 | 907,964.79 @ 0,-5.9 | ItemGetPanel |
| main/BG (1) [114302127217294661] | `A/tooloffer/bg1_5.png` | 811×627 | 0 / 0 | 811,658.5 @ 0,129.25 | ItemGetPanel |
| main/BG (2) [114946936116752871] | `A/tooloffer/bg2_1.png` | 625×396 | 0 / 0 | 625,396 @ 0,145 | ItemGetPanel |
| main/Title [114587397909062814] | `A/tooloffer/title_0.png` | 961×186 | 0 / 0 | 961,186 @ 0,475 | ItemGetPanel |
| main/GetBtn [114733206282342372] | `A/tooloffer/btn1.png` | 764×192 | 0 / 0 | 670.7,192 @ 0,-335 | ItemGetPanel |
| main/GetBtn/ad [114472185465350827] | `A/tooloffer/ad.png` | 113×101 | 0 / 0 | 113,101 @ -223,1.3 | ItemGetPanel |
| main/Close [114841364053301648] | `A/tooloffer/close.png` | 118×120 | 0 / 0 | 116,117 @ 416,467 | ItemGetPanel |
| main/ItemIcon [114919942235722503] | `A/items/Revoke.asset` → `A/items/Revoke.png` | 119×129 | 0 / 0 | 119,129 @ 0,144，scale=2,2,1 | ItemGetPanel, MainPanelBottom, MainPanelComplete, RevokeItem |
| main/CoinTip [114632746491512912] | `A/tooloffer/goldget_bg.png` | 352×117 | 0 / 0 | 352,117 @ 219,-218，初始禁用 | ItemGetPanel |
| main/CoinTip/Image [114693329660128220] | `A/tooloffer/coin_max.png` | 59×60 | 0 / 0 | 59,60 @ -104.4,12.1 | ItemGetPanel |

## Level Stats 与记录条：运行时嵌套 Prefab

`TXPanel` 运行时实例化 `Resources/prefabs/playerinfo/PlayerInfo.prefab` 到 `main/PlayerInfoPos`，再将 `PMDSimple.prefab` 挂到 `player.MarqueeParent`。批准图下面两块不是 TXPanel 背景的一部分，必须分别换其资源。

### PlayerInfo.prefab

| node [Image fileID] | Sprite → PNG | PNG px | Type / Border | size @ pos | 共享 Prefab |
|---|---|---:|---|---|---|
| Image [114977153420339851] | `A/localstats/bg_3.png` | 968×334 | 0 / 0 | 968,334 @ 0 | PlayerInfo |
| 2 [114313026266067498] | `A/localstats/bg2_3.png` | 256×117 | 0 / 0 | 256,117 @ 0,20 | PlayerInfo |
| 1 [114918264173551851] | `S/bg3_1.asset` → `T/bg3_1.png` | 256×117 | 0 / 0 | 256,117 @ -300,20 | PlayerInfo |
| 3 [114089843903986396] | `S/bg1_4.asset` → `T/bg1_4.png` | 256×117 | 0 / 0 | 256,117 @ 300,20 | PlayerInfo |

### PMDSimple.prefab

| node [Image fileID] | Sprite → PNG | PNG px | Type / Border | size @ pos | 共享 Prefab |
|---|---|---:|---|---|---|
| Scroll View [114589667821646929] | `S/pmd_bg.asset` → `T/pmd_bg.png` | 986×106 | 0 / 0 | 1013,120 @ 0,-0.00018501 | PMDSimple |
| Scroll View/Viewport [114705390221481576] | `无` | — | 1 /  | 0 @ 0 | — |
| Scroll View/Viewport/Content/PMDItem [114002669891404536] | `无` | — | 0 /  | 1023,106 @ 0 | — |
| Scroll View/Viewport/Content/PMDItem/Bg [114632457200066912] | `S/kuang.asset` → `T/kuang.png` | 85×85 | 0 / 0 | 85,85 @ -431,0，scale=1.2,1.2,1 | MainPanelBottom, MainPanelComplete, PMDSimple |
| Scroll View/Viewport/Content/PMDItem/Image [114969106005120503] | `A/paysimple/PayPal.asset` → `A/paysimple/PayPal.png` | 324×324 | 0 / 0 | 324,324 @ -431,0，scale=0.28,0.28,1 | LocalMainPanelTop, MainPanelComplete, MainPanelTop, PMDBullet, PMDItem, PMDSimple |

## 动态图像与适配风险

| 运行时资源 | Sprite → PNG | PNG px | Border | 使用方式 |
|---|---|---:|---|---|
| US 渠道 Venmo | `A/pay/Venmo.asset → A/pay/Venmo.png` | 513×303 | 0 | 替换任一渠道 Background Image，Type0，显示256.7×166.8 |
| US 渠道 Zelle | `A/pay/Zelle.asset → A/pay/Zelle.png` | 513×303 | 0 | 同上 |
| US 渠道 PayPal | `A/pay/paypal.asset → A/pay/paypal.png` | 513×303 | 0 | 同上；TXUserInfoSurePanel也使用 |
| Revoke | `A/items/Revoke.asset → A/items/Revoke.png` | 119×129 | 0 | 工具领取图标Type0；底栏、RevokeItem共享 |
| Refresh | `A/items/Exchange.asset → A/items/Exchange.png` | 120×126 | 0 | 工具领取图标Type0；底栏、ExchangeItem、奖励Item共享 |
| Add Screw | `A/items/AddScrew.asset → A/items/AddScrew.png` | 138×127 | 0 | 工具领取图标Type0；底栏、AddScrewItem共享 |

- **Enter Information 的付款图标不是固定三张。** `OriginalWithdrawalUserInfoPanel.SetChannelIcon` 从 `Atlas/Pay/{0}` 加载，当前 US 样本会出现 Venmo / Zelle / paypal；Prefab 的 LINE Pay / paypal 只是初值。各 PNG 位于 `A/pay/`，图标是独立支付 Logo，不建议为了木风格重新绘制商标。Checkmark 是 `S/duigou.asset → T/duigou.png`，运行时 alpha 动画显示选中状态。`Background/Image` 三个白板当前均禁用，不是凸出底板来源。
- **工具图标动态变化。** ItemGetPanel `iconPaths` = `Atlas/Items/Revoke` / `Exchange` / `AddScrew`；Refresh 实际用 Exchange。图标 Rect=119×129、scale=(2,2,1)，即显示238×258逻辑单位；不同源图会拉伸进同一框。覆盖 `A/items/Exchange.png` 等会同时影响底栏、奖励展示/飞行动画等动态引用。不要只换 Prefab 的初始 Revoke sprite。
- **金额图标动态改变原生尺寸。** TXPanel ItemIcon 的 OriginalGoldImage 会按国家资源格式加载并调用 `Image.SetNativeSize()`；Prefab 的 US1 111×95 只是初值。国家图标、货币格式仍应动态保留，不烘焙进金额卡。
- **当前 Level 标题只用 Title1。** `OriginalWithdrawalPanel.InitLocal()` 会隐藏 `main/1`、`main/2`，因此 `S/title1.asset` 的两个备用标题当前不显示；可见的绿色横幅为 `main/Title1 → S/title_9.asset → T/title_9.png`。金额图标资源格式为 `Atlas/Golds/{0}{1}`，goldType=1。
- **输入框是九宫格。** `S/bg2_4.asset → T/bg2_4.png` 552×157，Border≈(66.924,66.924,65.924,64.973)，同时用于宽度822、552、253.865及邮件输入。上下边总高约131.9，中央可拉伸带仅约25px；新边框/木纹需落在原切片区，细花叶不可跨接缝，否则不同宽度下断裂。该资源还影响 TXChannelPanel、TXProgress0Panel。
- **Progress/Bar 为 Filled。** TXPanel 填充必须保持绿色纯填充内容，宽836，高65 PNG；边框只在 Progress 轨道上。若把完整封口/花叶边框画进填充，低进度会切断装饰。`Progress/Tip` 初始 scale.x=0，代码仍维护位置，不能当静态卡片合并。
- **Sliced 不一定有有效边框。** ItemGetPanel BG m_Type=1 但当前 border=0，实际几乎整图缩放；PNG907×953显示907×964.79。TXPanel bg_11/bg1_2 有非零 Sprite border，但 Image m_Type=0，所以实际仍整图拉伸，不应按九宫格预期制作。
- **按钮横纵比不全与PNG一致。** TXPanel TXBtn PNG764×192显示668.9×192，ItemGet GetBtn PNG764×192显示670.7×192。新资源需按最终Rect纵横比预览，避免木边、花朵被横向压窄；不得为改比例改动现有RectTransform。
- **序列化 Sprite 有独立 rect/UV。** title1 PNG979×167，Sprite rect约978.947×166.848，实际UI显示975×163；close PNG/Sprite rect118×120，实际UI显示116×117。只替换PNG时保持原PNG像素尺寸、留白与Sprite所用边界；不能把整张AI预览直接缩到纹理大小后期待元素位置吻合。
- **批准图未画出 Enter Information 的原 Close Button。** 真实Prefab存在 `main/Close`，pos=(-20,-751)，116×117；当前游戏截图在面板下方显示圆×。本次限定不改结构/绑定，应保留且换一致皮肤，不能把按钮图像透明后留下隐形点击区域。右上角`ChannelInfo`是问号帮助按钮，不能将其冒充Close。
- **遮罩/背景不能整图覆盖。** 根 Image 无sprite，为黑色modal遮罩；TXPanel、TXUserInfoPanel 初始化时会按settings重写alpha。批准图里的全屏花园/奶油色超出本次面板Sprite覆盖范围，不能将预览整屏贴图塞入面板背景。
- **Level Stats 的标题与卡片独立。** PlayerInfo 使用已隔离的 `A/localstats/bg_3.png` / `bg2_3.png` 加另外两个 `T/bg3_1.png` / `bg1_4.png`；不应把统计标题、三个标签和数值烘焙进总背景。PMDSimple `InitUnavailable()` 会隐藏付款头像 Image，但保留其 Bg 占位圆框，正对应批准图最下方左边的浅色圆位。

## 建议核验

保持所有对象/RectTransform/Button事件引用稳定；完成后核对三处底板不再出现；US电话与email两类账号输入各看一次；Refresh、Revoke、AddScrew三个图标各看一次；金额条0%、部分、100%各看一次；共享Close/按钮/输入框涉及的其他弹窗检查一次。字体、数值、支付渠道仍保持运行时组件。
