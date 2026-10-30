# 花园木质换皮素材审计

工程：`C:\Projects\com.nut.sort.relax`。审计时间：2026-09-21T10:50:48.793991+00:00。仅新增本目录审计文件；未修改纹理、Prefab、场景、材质或代码。

共 **219** 张项目纹理；指定UI目录 **189** 张，其他玩法/效果/技术贴图 **30** 张。主题替换候选 **125** 张。

## 引用与实施约束

- 本清单为磁盘静态审计，不等同当前运行中可见性或功能验收。
- 默认入口 LuoSiSortGame.unity；OriginalMainLevel.asset 加载 MainPanel 与 LocalGameplay，后者加载 LocalMainPanelTop、LocalSubrounds 并嵌套 MainPanelBottom。MainPanelComplete 是恢复/验证组合，不能代替默认入口。
- 两类引用：Prefab Image.m_Sprite -> 独立 Sprite .asset GUID -> m_RD.texture -> PNG GUID；或直接指向 PNG.meta 导入Sprite GUID/fileID。逐项区分，不可只看同名PNG。
- 9-slice 以实际引用Sprite的 m_Border 为准；PNG.meta.spriteBorder 是导入Sprite的边框，两者可能不同。
- Resources 同路径可能同时有 .asset Sprite 与PNG导入Sprite。静态审计仅列候选，不断言运行时选择顺序；同尺寸替换原PNG可作用于两者，避免换GUID/删.asset。
- 保持PNG尺寸、GUID、alpha轮廓、Sprite rect/pivot/border/custom mesh、Prefab层级/组件/位置/按钮/交互。主题纹理建议原位替换PNG内容，不把动态TMP文字画入图。
- 现金/地区钞票、粉钻、支付品牌、广告符号、问号、工具含义保留；可复用语义素材。字体SDF/法线/遮罩和技术效果不做木纹重绘。
- 相同PNG哈希只证明当前像素相同；副本有各自GUID和用途，修改须覆盖各引用，不能删除或合并引用。
- 类别结合路径、原素材接触表和引用关系整理；review项须再看原图/节点。direct consumers记录Image/SpriteRenderer的sprite槽，材料/变体的间接关系在transitive字段。无静态Prefab引用不代表绝对不使用，还需看动态路径证据。
- Resources/game/spirte/Mask.png 是紫色可见玩法罩，Texture2D/mask.png 才属于技术遮罩；不得只按文件名Mask批量排除或木纹化。
- 性能建议：复用原分辨率、压缩和不可读设置，无需4K/新装饰节点。1024x2048 RGBA32约8MiB，原背景ASTC10x10约0.32MiB；保持现有压缩路线。

## 分类统计

| 类别 | 数量 |
|---|---:|
| 功能icon | 16 |
| 奖励/支付logo | 46 |
| 按钮 | 6 |
| 标题装饰 | 17 |
| 玩法纹理 | 27 |
| 背景 | 3 |
| 遮罩/字体/技术贴图 | 22 |
| 面板/框 | 82 |

独立Sprite关联纹理 188；非零9-slice 9；Resources同名Sprite候选 114；相同像素副本组 9。

## 全量纹理清单

动作：replace_theme=主题替换；restyle_preserve_semantics=保留功能语义调外观；restyle_preserve_gameplay=受玩法状态约束；preserve_semantic=可复用语义资产；preserve_technical/effect=不改；review=先人工确认。

| 路径 | 尺寸 | alpha透明/半透明% | 9-slice | 分类 / 动作 | Sprite资产数 | 关联Prefab |
|---|---:|---:|---|---|---:|---|
| `Assets/Cubemap/Cubemap.png` | 256×1536 | 0.0/0.0 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 0 | —（见动态/材质引用） |
| `Assets/Resources/atlas/appratingpanel/bg.png` | 895×941 | 2.8/0.3 | 无 | 面板/框<br>`replace_theme` | 1 | ReplayPanel |
| `Assets/Resources/atlas/appratingpanel/title.png` | 944×485 | 40.3/2.2 | 无 | 标题装饰<br>`replace_theme` | 1 | ReplayPanel |
| `Assets/Resources/atlas/Golds/AR1.png` | 111×95 | 43.1/3.9 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/Golds/BR1.png` | 111×95 | 42.5/3.9 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/Golds/BR3.png` | 426×258 | 42.9/1.8 | 无 | 奖励/支付logo<br>`preserve_semantic` | 0 | —（见动态/材质引用） |
| `Assets/Resources/atlas/Golds/DE1.png` | 111×95 | 41.4/4.6 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/Golds/GB1.png` | 111×95 | 40.5/4.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/Golds/ID1.png` | 111×95 | 39.2/5.0 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/Golds/ID3.png` | 426×258 | 44.8/2.0 | 无 | 奖励/支付logo<br>`preserve_semantic` | 0 | —（见动态/材质引用） |
| `Assets/Resources/atlas/Golds/JP1.png` | 111×95 | 40.4/4.3 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/Golds/MX1.png` | 111×95 | 42.2/3.5 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/Golds/RU1.png` | 111×95 | 40.3/4.4 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | TXRecordGuidePanel |
| `Assets/Resources/atlas/Golds/US1.png` | 111×95 | 42.0/4.4 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | Gold, GoldItem, LocalMainPanelTop, MainPanelComplete, MainPanelTop, TXPanel |
| `Assets/Resources/atlas/Golds/US3.png` | 426×258 | 42.9/2.2 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | Item_Big, TXGuideTargetCompletePanel |
| `Assets/Resources/atlas/items/AddScrew.png` | 138×127 | 32.3/4.5 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | AddScrewItem, LocalGameplay, MainPanelBottom, MainPanelComplete |
| `Assets/Resources/atlas/items/Coin.png` | 104×95 | 40.9/4.5 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | Coin, CoinItem, LocalMainPanelTop, MainPanelComplete, MainPanelTop, TXCoinPanel |
| `Assets/Resources/atlas/items/Coin2.png` | 360×231 | 34.6/4.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/items/Exchange.png` | 120×126 | 25.4/38.3 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | Item, ExchangeItem, LocalGameplay, MainPanelBottom, MainPanelComplete |
| `Assets/Resources/atlas/items/Revoke.png` | 119×129 | 27.4/39.5 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | ItemGetPanel, LocalGameplay, MainPanelBottom, MainPanelComplete, RevokeItem |
| `Assets/Resources/atlas/localstats/bg2_3.png` | 256×117 | 0.2/0.3 | 无 | 面板/框<br>`replace_theme` | 0 | TXPanel, PlayerInfo |
| `Assets/Resources/atlas/localstats/bg_3.png` | 968×334 | 1.2/1.8 | 无 | 面板/框<br>`replace_theme` | 0 | TXPanel, PlayerInfo |
| `Assets/Resources/atlas/mainpanel/coin_tip_bg.png` | 527×99 | 25.4/2.5 | 无 | 面板/框<br>`replace_theme` | 1 | CoinItem, LocalMainPanelTop, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/gold_tip_bg.png` | 527×99 | 25.4/2.5 | 无 | 面板/框<br>`replace_theme` | 1 | GoldItem, LocalMainPanelTop, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/goldget.png` | 193×117 | 5.4/6.9 | 无 | 按钮<br>`replace_theme` | 1 | CoinItem, GoldItem, LocalMainPanelTop, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/hintlevel_bg1.png` | 115×118 | 16.5/7.7 | 无 | 面板/框<br>`replace_theme` | 1 | HiddenLevel, LocalMainPanelTop, LocalSubrounds, MainPanelComplete, MainPanelTop, TXRecordGuidePanel |
| `Assets/Resources/atlas/mainpanel/hintlevel_bg2.png` | 115×118 | 16.5/7.7 | 无 | 面板/框<br>`replace_theme` | 1 | HiddenLevel, LocalMainPanelTop, LocalSubrounds, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/hintlevel_bg3.png` | 115×118 | 16.5/7.7 | 无 | 面板/框<br>`replace_theme` | 1 | HiddenLevel, LocalMainPanelTop, LocalSubrounds, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/hintlevel_bg4.png` | 115×137 | 22.7/6.5 | 无 | 面板/框<br>`replace_theme` | 1 | HiddenLevel, LocalMainPanelTop, LocalSubrounds, MainPanelComplete, MainPanelTop, TXRecordGuidePanel |
| `Assets/Resources/atlas/mainpanel/hintlevel_tx_bg.png` | 206×77 | 25.1/4.9 | 无 | 面板/框<br>`replace_theme` | 1 | HiddenLevel, LocalMainPanelTop, LocalSubrounds, MainPanelComplete, MainPanelTop, NewbieGuidePanel, TXRecordGuidePanel |
| `Assets/Resources/atlas/mainpanel/house.png` | 92×92 | 24.9/8.3 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | LocalMainPanelTop, MainPanel, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/item_add.png` | 80×80 | 27.4/6.0 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/mainpanel/item_bg.png` | 243×192 | 7.7/3.1 | 无 | 面板/框<br>`replace_theme` | 1 | AddScrewItem, ExchangeItem, LocalGameplay, MainPanelBottom, MainPanelComplete, RevokeItem |
| `Assets/Resources/atlas/mainpanel/item_bg1.png` | 243×192 | 7.7/3.3 | 无 | 面板/框<br>`replace_theme` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/mainpanel/item_count_bg.png` | 80×80 | 27.4/6.0 | 无 | 面板/框<br>`replace_theme` | 1 | AddScrewItem, ExchangeItem, LocalGameplay, MainPanelBottom, MainPanelComplete, RevokeItem |
| `Assets/Resources/atlas/mainpanel/level_bg.png` | 202×72 | 7.4/90.4 | 无 | 面板/框<br>`replace_theme` | 1 | LocalMainPanelTop, MainPanel, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/PlayerGoldGetHint_bg.png` | 1040×287 | 6.5/0.5 | 无 | 标题装饰<br>`replace_theme` | 1 | LocalMainPanelTop, MainPanelComplete, MainPanelTop, PlayerGoldGetHint |
| `Assets/Resources/atlas/mainpanel/replay.png` | 59×65 | 43.5/27.9 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | LocalGameplay, MainPanel, MainPanelBottom, MainPanelComplete |
| `Assets/Resources/atlas/mainpanel/setting.png` | 68×65 | 31.4/20.1 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | LocalGameplay, MainPanelBottom, MainPanelComplete |
| `Assets/Resources/atlas/mainpanel/target_tip_bg.png` | 337×105 | 29.1/4.1 | 无 | 面板/框<br>`replace_theme` | 1 | MainPanel, MainPanelComplete |
| `Assets/Resources/atlas/mainpanel/TargetReward_bg.png` | 1080×324 | 0.0/3.6 | 无 | 标题装饰<br>`replace_theme` | 1 | MainPanel, MainPanelComplete |
| `Assets/Resources/atlas/mainpanel/tx_bar.png` | 660×44 | 13.8/13.4 | 无 | 面板/框<br>`replace_theme` | 1 | LocalMainPanelTop, MainPanel, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/tx_bar_bg.png` | 659×44 | 10.8/4.8 | 无 | 面板/框<br>`replace_theme` | 1 | LocalMainPanelTop, MainPanel, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/tx_bg.png` | 477×126 | 2.7/10.6 | 无 | 面板/框<br>`replace_theme` | 1 | CoinItem, GoldItem, LocalMainPanelTop, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/tx_bg1.png` | 110×38 | 18.4/7.2 | 无 | 面板/框<br>`replace_theme` | 1 | HiddenLevel, LocalMainPanelTop, LocalSubrounds, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/tx_dj_bg.png` | 737×128 | 4.6/1.8 | 无 | 面板/框<br>`replace_theme` | 1 | LocalMainPanelTop, MainPanel, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/tx_glow.png` | 134×134 | 25.5/71.1 | 无 | 遮罩/字体/技术贴图<br>`preserve_effect` | 1 | LocalMainPanelTop, MainPanel, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/mainpanel/tx_lv_bar.png` | 831×44 | 13.4/13.5 | 无 | 面板/框<br>`replace_theme` | 1 | HiddenLevel, LocalMainPanelTop, LocalSubrounds, MainPanelComplete, MainPanelTop, TXGuideLoadingPanel, TXRecordGuidePanel |
| `Assets/Resources/atlas/mainpanel/tx_lv_bar_bg.png` | 829×44 | 10.4/4.8 | 无 | 面板/框<br>`replace_theme` | 1 | HiddenLevel, LocalMainPanelTop, LocalSubrounds, MainPanelComplete, MainPanelTop, TXGuideLoadingPanel, TXRecordGuidePanel |
| `Assets/Resources/atlas/newbieguidepanel/bg.png` | 809×200 | 4.9/1.7 | 无 | 面板/框<br>`replace_theme` | 1 | NewbieGuidePanel |
| `Assets/Resources/atlas/newbieguidepanel/bg1.png` | 115×118 | 16.3/7.6 | 无 | 面板/框<br>`replace_theme` | 1 | NewbieGuidePanel |
| `Assets/Resources/atlas/newbieguidepanel/dian.png` | 34×34 | 16.9/12.5 | 无 | 面板/框<br>`replace_theme` | 1 | HiddenLevel, LocalMainPanelTop, LocalSubrounds, MainPanelComplete, MainPanelTop, NewbieGuidePanel |
| `Assets/Resources/atlas/newbieguidepanel/dian1.png` | 34×34 | 16.7/9.8 | 无 | 面板/框<br>`replace_theme` | 1 | HiddenLevel, LocalMainPanelTop, LocalSubrounds, MainPanelComplete, MainPanelTop |
| `Assets/Resources/atlas/newbieguidepanel/hand.png` | 192×195 | 36.0/2.1 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | GuideHand, NewbieGuidePanel, TXGuideTargetCompletePanel |
| `Assets/Resources/atlas/newbieguidepanel/line.png` | 131×5 | 0.0/1.8 | 无 | 面板/框<br>`replace_theme` | 1 | HiddenLevel, LocalMainPanelTop, LocalSubrounds, MainPanelComplete, MainPanelTop, NewbieGuidePanel |
| `Assets/Resources/atlas/pay/DANA.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/LINE Pay.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | TXUserInfoPanel |
| `Assets/Resources/atlas/pay/MercadoPago.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/Naranja X.png` | 511×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/Other.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/OVO.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/paypal.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | TXUserInfoPanel, TXUserInfoSurePanel |
| `Assets/Resources/atlas/pay/paypay.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/PIX.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/QIWI Wallet.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/Revolut.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/Ualá.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/Venmo.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/YooMoney.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/pay/Zelle.png` | 513×303 | 2.3/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/DANA.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/LINE Pay.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/MercadoPago.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/Naranja X.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/OVO.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/PayPal.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | LocalMainPanelTop, MainPanelComplete, MainPanelTop, PMDBullet, PMDItem, PMDSimple |
| `Assets/Resources/atlas/paysimple/PayPay.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/PIX.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/QIWI Wallet.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/Revolut.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/Ualá.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/Venmo.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/paysimple/YooMoney.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | LocalMainPanelTop, MainPanelComplete, MainPanelTop, PlayerGoldGetHint |
| `Assets/Resources/atlas/paysimple/Zelle.png` | 324×324 | 2.5/0.1 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/tooloffer/ad.png` | 113×101 | 17.4/4.3 | 无 | 功能icon<br>`restyle_preserve_semantics` | 0 | ItemGetPanel |
| `Assets/Resources/atlas/tooloffer/bg1_5.png` | 811×627 | 0.5/0.1 | 无 | 面板/框<br>`replace_theme` | 0 | ItemGetPanel |
| `Assets/Resources/atlas/tooloffer/bg2_1.png` | 625×396 | 13.1/1.5 | 无 | 面板/框<br>`replace_theme` | 0 | ItemGetPanel |
| `Assets/Resources/atlas/tooloffer/bg_0.png` | 907×953 | 3.4/0.5 | 无 | 面板/框<br>`replace_theme` | 0 | ItemGetPanel |
| `Assets/Resources/atlas/tooloffer/btn1.png` | 764×192 | 3.1/2.6 | 无 | 按钮<br>`replace_theme` | 0 | ItemGetPanel |
| `Assets/Resources/atlas/tooloffer/close.png` | 118×120 | 19.2/4.3 | 无 | 功能icon<br>`restyle_preserve_semantics` | 0 | ItemGetPanel |
| `Assets/Resources/atlas/tooloffer/coin_max.png` | 59×60 | 10.4/89.6 | 无 | 奖励/支付logo<br>`preserve_semantic` | 0 | ItemGetPanel |
| `Assets/Resources/atlas/tooloffer/goldget_bg.png` | 352×117 | 23.4/3.7 | 无 | 面板/框<br>`replace_theme` | 0 | ItemGetPanel |
| `Assets/Resources/atlas/tooloffer/title_0.png` | 961×186 | 16.8/2.3 | 无 | 标题装饰<br>`replace_theme` | 0 | ItemGetPanel |
| `Assets/Resources/atlas/txcoinpanel/bar.png` | 870×60 | 13.6/6.8 | 无 | 面板/框<br>`replace_theme` | 1 | TXCoinPanel |
| `Assets/Resources/atlas/txcoinpanel/bar_bg.png` | 860×51 | 1.0/0.5 | 无 | 面板/框<br>`replace_theme` | 1 | TXCoinPanel |
| `Assets/Resources/atlas/txcoinpanel/bg.png` | 1048×1801 | 5.9/1.0 | 无 | 面板/框<br>`replace_theme` | 1 | TXCoinPanel |
| `Assets/Resources/atlas/txcoinpanel/bg2.png` | 710×76 | 4.5/4.6 | 无 | 面板/框<br>`replace_theme` | 1 | TXCoinPanel |
| `Assets/Resources/atlas/txcoinpanel/bg3.png` | 944×587 | 2.4/1.4 | 无 | 面板/框<br>`replace_theme` | 1 | TXCoinPanel |
| `Assets/Resources/atlas/txcoinpanel/bg4.png` | 562×61 | 1.0/0.5 | 无 | 面板/框<br>`replace_theme` | 1 | TXCoinPanel |
| `Assets/Resources/atlas/txcoinpanel/btn.png` | 227×92 | 6.6/2.6 | 无 | 按钮<br>`replace_theme` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/txcoinpanel/item_bg.png` | 366×221 | 3.5/10.3 | 无 | 面板/框<br>`replace_theme` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/txcoinpanel/item_bg1.png` | 365×221 | 3.3/10.9 | 无 | 面板/框<br>`replace_theme` | 1 | TXCoinPanel |
| `Assets/Resources/atlas/txcoinpanel/item_bg1_1.png` | 370×210 | 88.4/6.3 | 无 | 面板/框<br>`replace_theme` | 1 | TXCoinPanel |
| `Assets/Resources/atlas/txcoinpanel/item_bg2.png` | 422×254 | 3.2/9.9 | 无 | 面板/框<br>`replace_theme` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/txcoinpanel/tip_bg.png` | 944×281 | 4.1/1.8 | 无 | 面板/框<br>`replace_theme` | 1 | TXCoinPanel |
| `Assets/Resources/atlas/txcoinpanel/title1.png` | 562×61 | 1.9/0.9 | 无 | 标题装饰<br>`replace_theme` | 1 | —（见动态/材质引用） |
| `Assets/Resources/atlas/txrecordpanel/bg1.png` | 123×123 | 28.4/36.4 | 无 | 面板/框<br>`replace_theme` | 1 | TXCoinPanel, TXLevelPanel |
| `Assets/Resources/fonts & materials/LiberationSans SDF Atlas.png` | 1024×1024 | 27.2/72.8 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 0 | TXChannelPanel, TXUserInfoPanel |
| `Assets/Resources/game/spirte/1.png` | 86×146 | 45.4/25.0 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | Screw |
| `Assets/Resources/game/spirte/10.png` | 85×146 | 45.1/24.8 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/11.png` | 85×146 | 45.0/25.1 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/12.png` | 83×146 | 45.2/22.9 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/2.png` | 86×146 | 45.3/24.9 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/3.png` | 85×146 | 44.9/25.2 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/4.png` | 85×146 | 44.7/25.4 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/5.png` | 80×146 | 42.0/25.6 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/6.png` | 85×146 | 44.9/25.3 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/7.png` | 85×146 | 44.8/25.3 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/8.png` | 85×146 | 44.7/25.4 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/9.png` | 85×146 | 44.8/25.3 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/ALCI.png` | 149×404 | 12.0/3.3 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 1 | Screw |
| `Assets/Resources/game/spirte/bg.png` | 1024×2048 | 0.0/0.0 | 无 | 背景<br>`replace_theme` | 0 | —（见动态/材质引用） |
| `Assets/Resources/game/spirte/guide_correct.png` | 138×101 | 66.2/28.8 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | GuideCorrect |
| `Assets/Resources/game/spirte/guide_error.png` | 84×85 | 37.0/46.3 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | GuideError |
| `Assets/Resources/game/spirte/Mask.png` | 157×272 | 16.7/5.8 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 1 | Screw |
| `Assets/Resources/game/spirte/Mask1.png` | 109×46 | 50.7/24.8 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 1 | Screw |
| `Assets/Resources/game/spirte/Mask_done.png` | 87×77 | 64.5/12.6 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 1 | Screw |
| `Assets/Resources/game/spirte/miaozi.png` | 90×119 | 5.8/89.7 | 无 | 玩法纹理<br>`preserve_gameplay_semantic` | 1 | ScrewTile |
| `Assets/Resources/game/spirte/Stone.png` | 145×93 | 27.8/15.8 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 1 | Screw |
| `Assets/Resources/prefabs/effects/doneeffect/magic_line.png` | 512×256 | 26.1/71.9 | 无 | 遮罩/字体/技术贴图<br>`preserve_effect` | 0 | DoneEffect |
| `Assets/Resources/prefabs/effects/doneeffect/magic_orb2.png` | 512×512 | 25.9/63.0 | 无 | 遮罩/字体/技术贴图<br>`preserve_effect` | 0 | DoneEffect |
| `Assets/Resources/prefabs/effects/fireworkseffect/confetti.png` | 512×512 | 80.3/2.5 | 无 | 遮罩/字体/技术贴图<br>`preserve_effect` | 0 | SuccessEffect, LocalMainPanelTop, MainPanelComplete, MainPanelTop, PlayerGoldGetHint, TXGuideTargetCompletePanel |
| `Assets/Resources/prefabs/effects/sparkeffect/circlestretchy.png` | 256×256 | 26.3/73.5 | 无 | 遮罩/字体/技术贴图<br>`preserve_effect` | 0 | SparkEffect |
| `Assets/Resources/prefabs/effects/sparkeffect/glow1.png` | 512×512 | 27.7/72.2 | 无 | 遮罩/字体/技术贴图<br>`preserve_effect` | 0 | DoneEffect, SparkEffect, UnlockEffect |
| `Assets/Resources/prefabs/effects/stareffect/sparkle.png` | 256×256 | 37.3/48.5 | 无 | 遮罩/字体/技术贴图<br>`preserve_effect` | 0 | Screw, StarEffect, UnlockEffect, Item_Big, TXGuideTargetCompletePanel, TXRecordGuidePanel |
| `Assets/TextMesh Pro/Sprites/EmojiOne.png` | 512×512 | 28.4/2.4 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 0 | —（见动态/材质引用） |
| `Assets/Texture2D/ad.png` | 113×101 | 17.4/4.3 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | FailPanel, LocalFailure, LocalSuccess, SuccessPanel |
| `Assets/Texture2D/ads.png` | 169×159 | 21.6/8.6 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | Screw |
| `Assets/Texture2D/bar.png` | 750×83 | 11.8/5.1 | 无 | 面板/框<br>`replace_theme` | 1 | BootLoadingPanel, OriginalBootstrapHost |
| `Assets/Texture2D/bar_1.png` | 836×65 | 11.5/0.6 | 无 | 面板/框<br>`replace_theme` | 1 | TXPanel |
| `Assets/Texture2D/bar_2.png` | 835×69 | 13.8/6.0 | 无 | 面板/框<br>`replace_theme` | 1 | TXTooFastHintPanel |
| `Assets/Texture2D/bar_bg.png` | 754×87 | 3.9/4.9 | 无 | 面板/框<br>`replace_theme` | 1 | BootLoadingPanel, OriginalBootstrapHost |
| `Assets/Texture2D/bar_bg_1.png` | 840×73 | 10.3/0.6 | 无 | 面板/框<br>`replace_theme` | 1 | TXPanel |
| `Assets/Texture2D/bar_bg_2.png` | 840×73 | 10.3/0.6 | 无 | 面板/框<br>`replace_theme` | 1 | TXTooFastHintPanel |
| `Assets/Texture2D/bg.png` | 853×2048 | 0.0/0.6 | 无 | 背景<br>`replace_theme` | 1 | BootLoadingPanel, OriginalBootstrapHost |
| `Assets/Texture2D/bg0.png` | 819×841 | 3.6/0.3 | 有 | 面板/框<br>`replace_theme` | 1 | MessagePanel |
| `Assets/Texture2D/bg0_0.png` | 956×1199 | 0.4/1.3 | 无 | 面板/框<br>`replace_theme` | 1 | TXTooFastHintPanel |
| `Assets/Texture2D/bg1.png` | 476×84 | 3.2/1.0 | 无 | 面板/框<br>`replace_theme` | 1 | TXChannelPanel, TXProgress0Panel, TXUserInfoPanel, TXUserInfoSurePanel |
| `Assets/Texture2D/bg1_1.png` | 734×290 | 5.3/1.4 | 无 | 面板/框<br>`replace_theme` | 1 | TXProgress0Panel |
| `Assets/Texture2D/bg1_2.png` | 881×734 | 0.1/0.1 | 有 | 面板/框<br>`replace_theme` | 1 | TXLevelPanel, TXPanel |
| `Assets/Texture2D/bg1_4.png` | 256×117 | 0.2/0.3 | 无 | 面板/框<br>`replace_theme` | 1 | TXPanel, PlayerInfo |
| `Assets/Texture2D/bg1_5.png` | 811×627 | 0.5/0.1 | 有 | 面板/框<br>`replace_theme` | 1 | —（见动态/材质引用） |
| `Assets/Texture2D/bg1_7.png` | 896×213 | 0.3/1.0 | 无 | 面板/框<br>`replace_theme` | 1 | TXTooFastHintPanel |
| `Assets/Texture2D/bg1_8.png` | 753×556 | 0.7/0.2 | 无 | 面板/框<br>`replace_theme` | 1 | TXGuideTargetCompletePanel |
| `Assets/Texture2D/bg2_0.png` | 850×376 | 10.2/3.6 | 无 | 面板/框<br>`replace_theme` | 1 | TXLevelPanel, TXPanel |
| `Assets/Texture2D/bg2_1.png` | 625×396 | 13.1/1.5 | 无 | 面板/框<br>`replace_theme` | 1 | —（见动态/材质引用） |
| `Assets/Texture2D/bg2_2.png` | 1080×505 | 22.9/4.1 | 无 | 标题装饰<br>`replace_theme` | 1 | TXGuideLoadingPanel |
| `Assets/Texture2D/bg2_4.png` | 552×157 | 9.1/1.9 | 有 | 面板/框<br>`replace_theme` | 1 | TXChannelPanel, TXProgress0Panel, TXUserInfoPanel |
| `Assets/Texture2D/bg2_5.png` | 896×246 | 0.3/0.1 | 无 | 面板/框<br>`replace_theme` | 1 | TXTooFastHintPanel |
| `Assets/Texture2D/bg3_0.png` | 925×443 | 1.6/0.7 | 有 | 面板/框<br>`replace_theme` | 1 | TXUserInfoPanel, TXUserInfoSurePanel |
| `Assets/Texture2D/bg3_1.png` | 256×117 | 0.2/0.3 | 无 | 面板/框<br>`replace_theme` | 1 | TXPanel, PlayerInfo |
| `Assets/Texture2D/bg_0.png` | 907×953 | 3.4/0.5 | 有 | 面板/框<br>`replace_theme` | 1 | —（见动态/材质引用） |
| `Assets/Texture2D/bg_10.png` | 956×1199 | 0.7/2.1 | 有 | 面板/框<br>`replace_theme` | 1 | TXChannelPanel |
| `Assets/Texture2D/bg_11.png` | 983×1034 | 2.8/0.4 | 有 | 面板/框<br>`replace_theme` | 1 | TXLevelPanel, TXPanel |
| `Assets/Texture2D/bg_12.png` | 988×1421 | 6.5/1.4 | 无 | 面板/框<br>`replace_theme` | 1 | TXUserInfoPanel, TXUserInfoSurePanel |
| `Assets/Texture2D/bg_14.png` | 895×1076 | 3.4/0.5 | 无 | 面板/框<br>`replace_theme` | 1 | TXGuideTargetCompletePanel |
| `Assets/Texture2D/bg_2.png` | 853×2048 | 0.0/1.6 | 无 | 背景<br>`replace_theme` | 1 | LocalSuccess, SuccessPanel |
| `Assets/Texture2D/bg_4.png` | 1080×324 | 0.0/3.8 | 无 | 面板/框<br>`replace_theme` | 1 | RewardPanel |
| `Assets/Texture2D/bg_5.png` | 998×1550 | 11.1/2.0 | 无 | 面板/框<br>`replace_theme` | 1 | TXProgress0Panel |
| `Assets/Texture2D/bg_7.png` | 1080×440 | 0.0/2.9 | 无 | 面板/框<br>`replace_theme` | 1 | TXRecordGuidePanel |
| `Assets/Texture2D/btn.png` | 510×192 | 4.2/2.8 | 无 | 按钮<br>`replace_theme` | 1 | FailPanel, LocalFailure, MessagePanel, ReplayPanel, TXRecordGuidePanel, UnlockGameplayPanel |
| `Assets/Texture2D/btn1.png` | 764×192 | 3.1/2.6 | 无 | 按钮<br>`replace_theme` | 1 | LocalSuccess, SuccessPanel, TXChannelPanel, TXCoinPanel, TXGuideTargetCompletePanel, TXProgress0Panel, TXTooFastHintPanel, TXUserInfoPanel, TXUserInfoSurePanel |
| `Assets/Texture2D/btn_0.png` | 764×192 | 3.1/2.7 | 无 | 按钮<br>`replace_theme` | 1 | TXLevelPanel, TXPanel |
| `Assets/Texture2D/close.png` | 118×120 | 19.2/4.3 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | TXChannelPanel, TXCoinPanel, TXLevelPanel, TXPanel, TXUserInfoPanel, TXUserInfoSurePanel |
| `Assets/Texture2D/coin_max.png` | 59×60 | 10.4/89.6 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | FailPanel, FailureCoinTip, LocalFailure |
| `Assets/Texture2D/done.png` | 73×73 | 17.4/13.4 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 1 | TXLevelPanel, TXProgress0Panel |
| `Assets/Texture2D/doneMove.png` | 523×546 | 33.3/66.3 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 1 | UnlockGameplayPanel |
| `Assets/Texture2D/duigou.png` | 139×105 | 63.6/4.2 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | TXUserInfoPanel |
| `Assets/Texture2D/glow1.png` | 428×425 | 25.1/71.5 | 无 | 遮罩/字体/技术贴图<br>`preserve_effect` | 1 | Item, Item_Big, TXGuideTargetCompletePanel, UnlockGameplayPanel |
| `Assets/Texture2D/goldget_bg.png` | 352×117 | 23.4/3.7 | 无 | 面板/框<br>`replace_theme` | 1 | FailPanel, FailureCoinTip, LocalFailure |
| `Assets/Texture2D/Hide.png` | 256×256 | 0.0/0.0 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 0 | Nut_Hide |
| `Assets/Texture2D/hide_0.png` | 433×547 | 26.9/22.5 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 1 | UnlockGameplayPanel |
| `Assets/Texture2D/kuang.png` | 85×85 | 20.7/7.0 | 无 | 面板/框<br>`replace_theme` | 1 | LocalGameplay, MainPanel, MainPanelBottom, MainPanelComplete, PMDSimple |
| `Assets/Texture2D/LexendDeca-Bold SDF Atlas.png` | 256×256 | 36.3/60.8 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 0 | Tip, Item, Item_Big, AddScrewItem, BootLoadingPanel, CoinItem, ExchangeItem, FailPanel, FailureCoinTip, GMButton, GoldItem, HiddenLevel, ItemGetPanel, LocalFailure, LocalGameplay, LocalMainPanelTop, LocalSubrounds, LocalSuccess, MainPanel, MainPanelBottom, MainPanelComplete, MainPanelTop, MessagePanel, NewbieGuidePanel, OriginalBootstrapHost, PMDBullet, PMDItem, PlayerGoldGetHint, ReplayPanel, RevokeItem, SuccessPanel, TXChannelPanel, TXCoinPanel, TXGuideLoadingPanel, TXGuideTargetCompletePanel, TXLevelPanel, TXPanel, TXProgress0Panel, TXRecordGuidePanel, TXTooFastHintPanel, TXUserInfoPanel, TXUserInfoSurePanel, UnlockGameplayPanel, PMDSimple, PlayerInfo |
| `Assets/Texture2D/line.png` | 5×36 | 0.0/20.0 | 无 | 面板/框<br>`replace_theme` | 1 | TXLevelPanel, TXProgress0Panel |
| `Assets/Texture2D/line2.png` | 6×150 | 30.8/53.4 | 无 | 面板/框<br>`replace_theme` | 1 | TXProgress0Panel |
| `Assets/Texture2D/loading.png` | 71×71 | 77.8/13.0 | 无 | 面板/框<br>`replace_theme` | 1 | TXLevelPanel, TXProgress0Panel |
| `Assets/Texture2D/loading1.png` | 29×42 | 43.0/25.6 | 无 | 面板/框<br>`replace_theme` | 1 | TXLevelPanel, TXProgress0Panel |
| `Assets/Texture2D/logo_android.png` | 818×413 | 10.8/9.2 | 无 | 标题装饰<br>`restyle_preserve_semantics` | 1 | BootLoadingPanel, OriginalBootstrapHost |
| `Assets/Texture2D/ls.png` | 115×119 | 27.1/8.8 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 1 | BootLoadingPanel, OriginalBootstrapHost |
| `Assets/Texture2D/mask.png` | 558×641 | 33.9/19.7 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 1 | UnlockGameplayPanel |
| `Assets/Texture2D/max.png` | 103×62 | 26.0/74.0 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | Item_Big |
| `Assets/Texture2D/NotoSansJP-Bold SDF Atlas.png` | 1024×1024 | 27.8/72.1 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 0 | Tip, Item, Item_Big, AddScrewItem, BootLoadingPanel, CoinItem, ExchangeItem, FailPanel, FailureCoinTip, GMButton, GoldItem, HiddenLevel, ItemGetPanel, LocalFailure, LocalGameplay, LocalMainPanelTop, LocalSubrounds, LocalSuccess, MainPanel, MainPanelBottom, MainPanelComplete, MainPanelTop, MessagePanel, NewbieGuidePanel, OriginalBootstrapHost, PMDBullet, PMDItem, PlayerGoldGetHint, ReplayPanel, RevokeItem, SuccessPanel, TXChannelPanel, TXCoinPanel, TXGuideLoadingPanel, TXGuideTargetCompletePanel, TXLevelPanel, TXPanel, TXProgress0Panel, TXRecordGuidePanel, TXTooFastHintPanel, TXUserInfoPanel, TXUserInfoSurePanel, UnlockGameplayPanel, PMDSimple, PlayerInfo |
| `Assets/Texture2D/Nut_Stand_Color.png` | 1024×512 | 0.0/0.0 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 0 | Screw, Nut_1 |
| `Assets/Texture2D/Nut_Stand_Normal_03.png` | 1024×512 | 0.0/0.0 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 0 | Screw, ScrewTile, Nut_Hide |
| `Assets/Texture2D/people.png` | 533×588 | 42.1/4.3 | 无 | 奖励/支付logo<br>`preserve_semantic` | 1 | FailPanel, LocalFailure |
| `Assets/Texture2D/pmd_bg.png` | 986×106 | 8.1/0.4 | 无 | 面板/框<br>`replace_theme` | 1 | PMDSimple |
| `Assets/Texture2D/pmd_dm_bg.png` | 676×78 | 10.2/2.9 | 有 | 面板/框<br>`replace_theme` | 1 | LocalMainPanelTop, MainPanelComplete, MainPanelTop, PMDBullet, PMDItem |
| `Assets/Texture2D/Rubik-Bold SDF Atlas.png` | 256×256 | 31.0/68.9 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 0 | Tip, Item, Item_Big, AddScrewItem, BootLoadingPanel, CoinItem, ExchangeItem, FailPanel, FailureCoinTip, GMButton, GoldItem, HiddenLevel, ItemGetPanel, LocalFailure, LocalGameplay, LocalMainPanelTop, LocalSubrounds, LocalSuccess, MainPanel, MainPanelBottom, MainPanelComplete, MainPanelTop, MessagePanel, NewbieGuidePanel, OriginalBootstrapHost, PMDBullet, PMDItem, PlayerGoldGetHint, ReplayPanel, RevokeItem, SuccessPanel, TXChannelPanel, TXCoinPanel, TXGuideLoadingPanel, TXGuideTargetCompletePanel, TXLevelPanel, TXPanel, TXProgress0Panel, TXRecordGuidePanel, TXTooFastHintPanel, TXUserInfoPanel, TXUserInfoSurePanel, UnlockGameplayPanel, PMDSimple, PlayerInfo |
| `Assets/Texture2D/screwHide.png` | 353×422 | 32.2/1.6 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 1 | UnlockGameplayPanel |
| `Assets/Texture2D/star.png` | 118×119 | 36.3/60.4 | 无 | 遮罩/字体/技术贴图<br>`preserve_effect` | 1 | FailPanel, FailureSparkle1, FailureSparkle2, FailureSparkle3, FailureSparkle4, FailureSparkle5, LocalFailure |
| `Assets/Texture2D/tip_bg.png` | 717×127 | 0.5/99.5 | 无 | 面板/框<br>`replace_theme` | 1 | Tip |
| `Assets/Texture2D/tip_bg_0.png` | 255×93 | 25.0/6.2 | 无 | 面板/框<br>`replace_theme` | 1 | TXPanel |
| `Assets/Texture2D/title.png` | 961×308 | 35.3/3.0 | 无 | 标题装饰<br>`replace_theme` | 1 | MessagePanel |
| `Assets/Texture2D/title1.png` | 979×167 | 13.6/6.5 | 无 | 标题装饰<br>`replace_theme` | 1 | TXPanel |
| `Assets/Texture2D/title2.png` | 1080×234 | 3.3/2.9 | 无 | 标题装饰<br>`replace_theme` | 1 | TXLevelPanel |
| `Assets/Texture2D/title3.png` | 457×84 | 3.8/96.2 | 无 | 标题装饰<br>`replace_theme` | 1 | TXLevelPanel, TXPanel |
| `Assets/Texture2D/title_0.png` | 961×186 | 16.8/2.3 | 无 | 标题装饰<br>`replace_theme` | 1 | —（见动态/材质引用） |
| `Assets/Texture2D/title_1.png` | 896×828 | 36.9/27.7 | 无 | 标题装饰<br>`replace_theme` | 1 | TXRecordGuidePanel |
| `Assets/Texture2D/title_2.png` | 819×395 | 32.7/67.3 | 无 | 遮罩/字体/技术贴图<br>`preserve_effect` | 1 | UnlockGameplayPanel |
| `Assets/Texture2D/title_3.png` | 896×362 | 39.8/5.0 | 无 | 标题装饰<br>`replace_theme` | 1 | TXGuideTargetCompletePanel |
| `Assets/Texture2D/title_5.png` | 615×84 | 2.3/1.2 | 无 | 标题装饰<br>`replace_theme` | 1 | TXTooFastHintPanel |
| `Assets/Texture2D/title_7.png` | 896×362 | 40.1/4.7 | 无 | 标题装饰<br>`replace_theme` | 1 | LocalSuccess, SuccessPanel |
| `Assets/Texture2D/title_9.png` | 975×163 | 14.9/3.4 | 无 | 标题装饰<br>`replace_theme` | 1 | TXPanel |
| `Assets/Texture2D/undone.png` | 74×75 | 19.4/5.5 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 1 | TXProgress0Panel |
| `Assets/Texture2D/Vagron SDF Atlas.png` | 512×512 | 34.5/56.5 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 0 | Tip, Item, Item_Big, AddScrewItem, BootLoadingPanel, CoinItem, ExchangeItem, FailPanel, FailureCoinTip, GMButton, GoldItem, HiddenLevel, ItemGetPanel, LocalFailure, LocalGameplay, LocalMainPanelTop, LocalSubrounds, LocalSuccess, MainPanel, MainPanelBottom, MainPanelComplete, MainPanelTop, MessagePanel, NewbieGuidePanel, OriginalBootstrapHost, PMDBullet, PMDItem, PlayerGoldGetHint, ReplayPanel, RevokeItem, SuccessPanel, TXChannelPanel, TXCoinPanel, TXGuideLoadingPanel, TXGuideTargetCompletePanel, TXLevelPanel, TXPanel, TXProgress0Panel, TXRecordGuidePanel, TXTooFastHintPanel, TXUserInfoPanel, TXUserInfoSurePanel, UnlockGameplayPanel, PMDSimple, PlayerInfo |
| `Assets/Texture2D/wenhao.png` | 26×39 | 36.5/33.4 | 无 | 玩法纹理<br>`restyle_preserve_gameplay` | 1 | Nut_Hide |
| `Assets/Texture2D/zh_Custom SDF Atlas.png` | 512×256 | 28.6/70.8 | 无 | 遮罩/字体/技术贴图<br>`preserve_technical` | 0 | Tip, Item, Item_Big, AddScrewItem, BootLoadingPanel, CoinItem, ExchangeItem, FailPanel, FailureCoinTip, GMButton, GoldItem, HiddenLevel, ItemGetPanel, LocalFailure, LocalGameplay, LocalMainPanelTop, LocalSubrounds, LocalSuccess, MainPanel, MainPanelBottom, MainPanelComplete, MainPanelTop, MessagePanel, NewbieGuidePanel, OriginalBootstrapHost, PMDBullet, PMDItem, PlayerGoldGetHint, ReplayPanel, RevokeItem, SuccessPanel, TXChannelPanel, TXCoinPanel, TXGuideLoadingPanel, TXGuideTargetCompletePanel, TXLevelPanel, TXPanel, TXProgress0Panel, TXRecordGuidePanel, TXTooFastHintPanel, TXUserInfoPanel, TXUserInfoSurePanel, UnlockGameplayPanel, PMDSimple, PlayerInfo |
| `Assets/Texture2D/？.png` | 69×71 | 25.9/11.1 | 无 | 功能icon<br>`restyle_preserve_semantics` | 1 | TXUserInfoPanel |

## 非零9-slice边框

| PNG | 导入Sprite边框 | 独立Sprite边框 |
|---|---|---|
| `Assets/Texture2D/bg0.png` | `{x: 141.92387, y: 153.92387, z: 141.9239, w: 594.9239}` | `Assets/Sprite/bg0.asset`: {'x': 141.92387, 'y': 153.92387, 'z': 141.9239, 'w': 594.9239} |
| `Assets/Texture2D/bg1_2.png` | `{x: 0, y: 87, z: 0, w: 106}` | `Assets/Sprite/bg1_2.asset`: {'x': 0.0, 'y': 87.0, 'z': 0.0, 'w': 106.0} |
| `Assets/Texture2D/bg1_5.png` | `{x: 101, y: 101, z: 101, w: 101}` | `Assets/Sprite/bg1_5.asset`: {'x': 101.0, 'y': 101.0, 'z': 101.0, 'w': 101.0} |
| `Assets/Texture2D/bg2_4.png` | `{x: 66.92388, y: 66.92388, z: 65.9239, w: 64.97324}` | `Assets/Sprite/bg2_4.asset`: {'x': 66.92388, 'y': 66.92388, 'z': 65.9239, 'w': 64.97324} |
| `Assets/Texture2D/bg3_0.png` | `{x: 59.923878, y: 59.923878, z: 59.9239, w: 59.9239}` | `Assets/Sprite/bg3_0.asset`: {'x': 59.923878, 'y': 59.923878, 'z': 59.9239, 'w': 59.9239} |
| `Assets/Texture2D/bg_0.png` | `{x: 0, y: 185.92387, z: 0, w: 180.9239}` | `Assets/Sprite/bg_0.asset`: {'x': 0.0, 'y': 185.92387, 'z': 0.0, 'w': 180.9239} |
| `Assets/Texture2D/bg_10.png` | `{x: 0, y: 444, z: 0, w: 279}` | `Assets/Sprite/bg_10.asset`: {'x': 0.0, 'y': 444.0, 'z': 0.0, 'w': 279.0} |
| `Assets/Texture2D/bg_11.png` | `{x: 0, y: 149.92387, z: 0, w: 124.923775}` | `Assets/Sprite/bg_11.asset`: {'x': 0.0, 'y': 149.92387, 'z': 0.0, 'w': 124.923775} |
| `Assets/Texture2D/pmd_dm_bg.png` | `{x: 75.92388, y: 28.97325, z: 75.9239, w: 28.973253}` | `Assets/Sprite/pmd_dm_bg.asset`: {'x': 75.92388, 'y': 28.97325, 'z': 75.9239, 'w': 28.973253} |

## 相同像素副本

文件内容相同但GUID不同，保留各自引用路径。

- `Assets/Resources/atlas/tooloffer/ad.png`；`Assets/Texture2D/ad.png`
- `Assets/Resources/atlas/tooloffer/bg1_5.png`；`Assets/Texture2D/bg1_5.png`
- `Assets/Resources/atlas/tooloffer/bg2_1.png`；`Assets/Texture2D/bg2_1.png`
- `Assets/Resources/atlas/tooloffer/bg_0.png`；`Assets/Texture2D/bg_0.png`
- `Assets/Resources/atlas/tooloffer/btn1.png`；`Assets/Texture2D/btn1.png`
- `Assets/Resources/atlas/tooloffer/close.png`；`Assets/Texture2D/close.png`
- `Assets/Resources/atlas/tooloffer/coin_max.png`；`Assets/Texture2D/coin_max.png`
- `Assets/Resources/atlas/tooloffer/goldget_bg.png`；`Assets/Texture2D/goldget_bg.png`
- `Assets/Resources/atlas/tooloffer/title_0.png`；`Assets/Texture2D/title_0.png`

## 机器可读明细

`asset-inventory.json`逐张记录尺寸/哈希、alpha范围/包围盒、PNG.meta参数/平台压缩、纹理GUID、独立Sprite GUID/fileID/rect/border/pivot、直接Image节点、传递Prefab/Scene/Material关联、Resources同名候选和动态路径证据。
