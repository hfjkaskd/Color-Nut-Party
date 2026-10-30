# Approved Garden 替换：独立结构审查

最终修订审查结论：**通过，未发现越出授权白名单的变化，也未发现本轮新皮肤遗漏动态路径。** 已包含第二轮素材、额外 TMP padding/颜色，以及一处基线既存的错误重复组件引用修复。本审查仅读取 Assets 和脚本，未运行 Unity、未修改 Assets。详细逐组件证据及最终资源 SHA-256 见 `independent-structure-review.json`。

## 基线与范围

- 基线：`baseline.json`，创建于 `2026-09-22T07:57:56.946153+00:00`；独立计算 3,859 个基线文件的 SHA-256，没有缺失文件。
- 仅四页 `TXCoinPanel`、`TXPanel`、`TXUserInfoPanel`、`ItemGetPanel` 和嵌套 `PlayerInfo`、`PMDSimple` 六个 Prefab 有变化。
- 基线内 489 个 C#、全部经济/文案/配置文件、220 张既有纹理、2,713 个 metadata、57 个 material 均保持原 SHA-256。`Assets/NutSort/Runtime` 没有新增文件。
- `prefabs-all-before.zip` 的全部 48 个 Prefab 都先对齐基线 SHA-256，再与当前文件逐 Unity 对象块比较。共 3,394 个对象块；831 个 Transform/RectTransform、对象 fileID、层级、激活状态、Button、事件绑定、文案均保持。831 个 GameObject 中，830 个完全保持，账户页 Get 仅删去一条错误重复组件引用；没有删除实际组件定义。
- 比较统一了 CRLF/LF 后，仅移除允许字段的整行，再要求其余内容完全一致；没有通过重新序列化来掩盖其他字段变化。剩余 42 个 Prefab 保持原文件 SHA-256。
- 新 `Editor/GardenReskin` 测试工具不作为生产改动审查；本报告没有评价其运行结果。

## 实际变化

| Prefab | 变化组件数 | 字段变化 |
|---|---:|---|
| ItemGetPanel | 5 | 4 个 Sprite，1 个装饰 Image alpha |
| TXCoinPanel | 19 | 10 个 Sprite，5 个装饰 Image alpha，4 个 Material，2 个动态路径，Title enabled/raycast，3 个 TMP padding |
| TXPanel | 9 | 7 个 Sprite，1 个装饰 Image alpha，1 个 TMP padding/深棕字体颜色 |
| TXUserInfoPanel | 8 + 1 个 GameObject | 7 个 Sprite，1 个装饰 Image alpha；Get 删除错误重复组件引用 |
| PlayerInfo | 7 | 4 个 Sprite，3 个 TMP padding |
| PMDSimple | 1 | 1 个 Sprite；左徽章已恢复基线 |

共 49 个变化组件和 1 个修复重复引用的 GameObject，即 50 个变化对象块。Image 的 `m_Type`、填充配置、RectTransform、Button targetGraphic 均未改变。所有新 Sprite 引用均为现存新 PNG 的 `fileID: 21300000, type: 3`，不依赖旧 `.asset` Sprite 的 rect/UV。

TXCoinPanel 的额外允许项精确匹配：

- Title Image `114397768327470545`：`m_Enabled 0→1`、`m_RaycastTarget 1→0`，接入 `title.png`。
- Controller `114217368724061974`：仅 `selectedSpritePath` 和 `normalSpritePath` 从旧 `Atlas/TXCoinPanel/item_bg1` / `item_bg` 指到 `Atlas/approvedgarden/amount-selected` / `amount-normal`。
- TMP `114561126880652180`：仅 margin 改为 `{x: 20, y: 0, z: 20, w: 0}`。
- TMP `114695600491528212`：仅 margin 改为 `{x: 20, y: 12, z: 20, w: 0}`。

第二轮追加允许项也精确匹配：TXCoinPanel TMP `114132862189046193` margin 为 `{x: 24, y: 6, z: 24, w: 6}`；TXPanel TMP `114704330047573733` margin 为 `{x: 18, y: 4, z: 18, w: 4}`、`m_fontColor` 为 `{r: 0.3, g: 0.14, b: 0.06, a: 1}`；PlayerInfo 的 TMP `114634108722742392`、`114371939378208658`、`114060945828524476` margin 均为 `{x: 12, y: 10, z: 12, w: 10}`。没有其他文字属性变化。

被视觉隐藏的 8 个 Image 均不是 Button targetGraphic；原按钮图像保持可见。三个多余底板与 My Diamonds 的旧金额底板/四个 Kuang 仅 alpha 改为 0，未删除对象、组件或运行时绑定。账户页原 Close 和 ChannelInfo 保持原结构与交互。

## 账户页既存导入警告与最小修复

独立扫描全部 48 个 Prefab 的 `m_Component` 引用和实际组件 `m_GameObject` 归属，基线只发现这一处错误：`TXUserInfoPanel` 的 `OriginalButtonFeedback` 组件 `114990000000000169` 被 Get（GameObject `1587765826080250`）和 ChannelInfo（`1217368724061974`）同时列入组件表。该组件声明 owner 为 ChannelInfo，target=`224092105871539913` 也指向 ChannelInfo RectTransform，故 Get 的引用不合法；首轮替换没有引入该错误。

最终修复已精确验证：仅从 Get 的 `m_Component` 列表移除 `114990000000000169` 这一行；ChannelInfo 的正确引用、组件定义、owner、target、settings 保持基线。当前 48 个 Prefab 无重复组件引用、缺失组件或 owner 不匹配。详见 `component-ownership-audit.json`。不需要复制/移动反馈组件，也没有新增绑定。

## 19 张新资源

19 张 PNG 均可解码，RGBA，独立 Sprite 导入（textureType=8、spriteMode=1），GUID 在当前 Assets metadata 中唯一。最大像素边不超过 importer 的 2048，不会因该上限缩小。全部 m_Border 为 0，唯 `input.png` 为四边各 40；四个输入 Image 仍为 Sliced。既有共享素材的 PNG、meta、material 均未被覆盖。

## 动态路径复核

| 运行时写入位置 | 当前资源路径 | 结论 |
|---|---|---|
| `LocalDiamondWithdrawalPanel.Init/Select` 金额按钮 | `Atlas/approvedgarden/amount-selected` / `amount-normal` | 两个路径各对应唯一新 PNG；初始化和切换选项都继续用新皮肤，不会恢复旧金额按钮 |
| `OriginalWithdrawalPanel.InitLocal` 统计与记录 | `playerInfoPrefab` 原引用 + `Prefabs/PlayerInfo/PMDSimple` | 两个实际实例化的 Prefab 都已替换；引用/路径保持 |
| `OriginalWithdrawalUserInfoPanel.SetChannelIcon` | `Atlas/Pay/{0}` | 保留渠道 Logo；US 的 Venmo/Zelle/paypal 资源存在，相关 PNG/.asset/meta 未改变 |
| `LocalToolOfferPanel.Init` | `Atlas/Items/Revoke`、`Exchange`、`AddScrew` | 三项均存在且保持；Refresh 的实际动态图标是 Exchange，未误将初始 Revoke 当作所有工具 |
| `OriginalGoldImage.Apply` | `Atlas/Golds/{0}{1}` | 保留国家/币种图标及 SetNativeSize 行为；US1 路径存在，资源未改变 |
| `OriginalMarqueeItemView.Init` | `Atlas/PaySimple/{0}` | 保留动态付款头像；本地 InitUnavailable 会隐藏头像 Image，背景仍显示新皮肤 |

既有图标路径下存在 `.asset` Sprite 与源 PNG 配对，是基线既有结构；本轮没有新增该歧义。新 approvedgarden 两条动态路径不存在同名 `.asset`。未发现其他会将本轮已换背景、标题或按钮恢复成旧 Sprite 的运行时赋值。选中框 SetActive、支付 Checkmark CrossFadeAlpha、遮罩 alpha、进度 Filled、金额/统计/文案刷新均保持。

## 需要实际画面验证的边界

静态结构通过不等于最终画面已验收；Unity 运行与截图由独立验证流程负责。PMDSimple 左徽章已恢复为基线圆框，第一轮所列的长方卡压成方形风险已消除。最终仍需看既有尺寸下的视觉适配：

1. 主按钮在 TXPanel/ItemGet 中依旧显示约 669×192，而 PNG 为 764×192；账户标题也在原 476×84 Rect 显示 613×99 PNG。第二轮素材更圆，实际压缩后的边框/花叶仍须以画面确认。
2. 新输入框四边 40 的九宫格需同时检查 822、552、253.865 三种宽度；TMP 新 padding 需看双行文本与本地化是否仍完整。

这些均为视觉验证项，未发现结构或经济逻辑回归。
