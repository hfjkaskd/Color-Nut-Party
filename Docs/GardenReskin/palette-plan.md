# 非纹理配色适配

已对照原运行截图、原素材 contact 和用户确认的主界面 AI 图。执行范围仅包含现有 Prefab 的颜色字段，以及现有材质的颜色和底座 metallic；不改布局、节点、组件、字体、字号、文本、绑定或运行代码。

| 对象 | 原值/问题 | 适配 |
| --- | --- | --- |
| Scene.mat | 白色 tint，使用原背景贴图 | 保留白色，直接呈现替换后背景原色 |
| Scene Camera | 清屏黑/蓝灰，正常由现有背景覆盖 | 保留场景，不为换皮改变清屏流程 |
| DiZuoMaterial | 灰色 RGB 0.843，metallic 0.722 | 暖木 albedo (0.20, 0.17, 0.13)，metallic 0；保留 shader 和透明设置 |
| DiZuoLockedMaterial | 冷灰蓝，metallic 0.8 | 较暗灰褐 albedo (0.12, 0.105, 0.082)，metallic 0；保留锁定状态区分 |
| 主界面双货币金额、弹窗普通正文 | 深蓝/黑/灰、蓝色正文 | 深棕 #4D2F1C；同步 TMP 的 m_fontColor 与 m_fontColor32 |
| 主界面 Cash 下 GoldItem / CoinItem 的 Hint/Text (TMP) | 深棕与已确认效果图的深蓝提示不一致 | 深靛蓝 #171339；仅同步普通 TMP 颜色与 Color32，红色富文本片段保持原机制。三份主界面 Prefab 及两份独立货币 Prefab 一致 |
| 现有白色标题/按钮文字 | 蓝/粉描边与花园风格冲突 | 保留白字、描边宽度与字体，描边改深棕/深绿/焦橙；钻石描边保留紫色色相并压暗 |
| SuccessPanel/main/label_88 | 浅背景下无描边白色 Claim 可读性不足 | 深棕，保持原文本与下划线机制 |
| NewbieGuidePanel/Pos/Tip/Value | 深木教学横幅与普通深棕正文对比不足 | 奶油白 #FFF5D9；仅同步 TMP 颜色与 Color32，保持字号、位置与本地化 |
| LocalGameplay/Result/Card | 冷蓝黑色纯色面板 | 奶油色；现有 Message 深棕、Dismiss 草绿 |

红色奖励金额、钻石数据的紫色、PlayerInfo 分类数据配色、白色按钮/标题、黑色遮罩 alpha、螺母 12 色、隐藏问号、螺杆金属材质均保留。

首轮 Unity 实际截图显示最初直接采用浅奶油 albedo 会在现有 Gamma 色彩空间、环境光强度 5、主灯强度 1.2 下过曝为纯白。最终降低两份底座 albedo，仅改变这两份材质的 _BaseColor / _Color，保持非金属与原灯光设置。表中的数值是材质输入值，不代表截图上的最终受光颜色。apply_palette.py 已同步最终数值，脚本可从原 baseline 复现最终材质；此次同步未重跑脚本或写入 Assets。

运行代码的 OriginalTargetRewardBanner 会产生黄色富文本，OriginalWithdrawalClaimFlow.WhiteTip 会产生白色富文本，因此相关 TargetReward_bg / PlayerGoldGetHint 背景及提现进度小提示须保留深棕/绿承载区。不能只通过 Prefab 字色解决这些运行时富文本，也不应在换皮中改变原有文本机制。

完整对象路径、当前字段与材质引用见 palette-inventory.json；执行记录及结构不变验证见 palette-validation.json。该文件不代表 Unity 运行验收结果；运行画面与交互由主任务统一验证。
