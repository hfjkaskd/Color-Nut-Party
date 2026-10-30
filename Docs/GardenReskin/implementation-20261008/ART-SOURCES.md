# 2026-10-09 实施素材来源

本批素材用内置 ImageGen 从已确认的 `../regenerated-20261008/*-effect.png` 提取、去除内容并生成无字资源。没有使用 API/CLI 图像生成，也没有将整页效果图作为游戏背景。支付品牌、银行、现金和道具语义图标优先保留原图。

生成文件位置记录在 `root-generations.json`、`main-art-generation.json`、`main-refinement-generation.json`，其他分组的源图与提示词保存在 `secondary-sources/`、`aux-sources/`。工程最终使用 `Assets/Resources/atlas/octobergarden/` 下的图片；源图从生成目录复制保留。

以下是根代理最终采用的提示要求（按资产记录的中文摘要，非逐字 API 日志）：

| 素材 | 输入参考 | 提示要求 |
| --- | --- | --- |
| panel | refresh-effect | 提取单个空白奶油弹窗底板；约2%浅蜂蜜木边、窄象牙色内高光、连续空白内区；去除标题条、按钮、图标、文字；透明外部，无叶子花朵或套框。 |
| primary-button | account-effect | 单个3.9:1绿色圆角矩形，薄深棕轮廓、微弱底部绿色斜面；去除Confirm文字和全部内容，禁止厚金边或椭圆形。 |
| ribbon | refresh-effect | 提取浅木折叠标题丝带，5.4:1，微小侧尾、薄棕边、浅木纹，去除Refresh和关闭按钮，无装饰和文字。 |
| input | account-effect | 提取Enter Name输入框底图，5:1，奶油色、细蜂蜜边、窄内高光；去除文字，不保留外层输入区底框。 |
| wood-card | diamond-effect | 空白浅木矩形底板，750:1200，保留细边和柔和圆角，去除所有内部内容，透明外部。 |
| title | account-effect | 空白Enter Information木标题牌，5:1，浅蜂蜜木纹和薄棕边，去除内容，不加装饰。 |
| amount-selected | diamond-effect | 提取选中金额格，2:1，浅开心果绿内区、鲜绿色细边、象牙色内高光；移除金额和全部内容。 |
| background | main-effect | 只保留浅奶油木纹底图，细水平木纹、低对比；移除游戏物体、UI、文字和物体阴影；无树叶、花朵、拼板缝或明显木结，9:16全幅。 |
| close | cash-effect | 象牙色圆盘、细木边、绿色X，1:1透明底；保持关闭含义，禁止厚金边与额外装饰。 |
| cream-ribbon | diamond-effect | 兑换比率的奶油色燕尾条，9:1；去掉钻石和文字，极细浅金边，无木质厚边。 |
| progress-track | diamond-effect | 提取无文字的浅焦糖棕色进度槽，16:1，圆端、极弱内阴影，无厚框。 |
| progress-fill | cash-effect | 提取1/1背后的绿色进度填充，12:1、极细边和窄顶部高光，去掉文字和周围内容。 |
| diamond-panel | diamond-effect | 单张1048:1801弹窗背景：浅木拱顶、两短木柱、顶部8%至14%的奶油余额条、16%至31%的奶油金额卡；其余连续浅木。去掉所有文字、图标、关闭、金额选项、说明和按钮。 |
| amount-normal | diamond-effect | 提取未选中金额格，365:221，奶油内区、约2%浅木边、窄象牙色内高光、轻微底部斜面。去掉文字，禁止简化成单像素平框。 |
| cash-card | cash-effect | 提取850:329提现金额卡：薄木边、奶油色；顶部中央55%宽、27%高的一体浅蜂蜜标题区域，下角圆，不带独立凸起标题牌。移除文字、金额、现金图标。 |

机械处理只包括透明边界裁切、缩放、九宫切片、留透明边及素材组合；没有手绘或修饰实际运行截图。实际截图仅由 Unity `ScreenCapture` 导出，三图对比仅做等比缩放和排版。

`cash-card` 的顶部透明留白与 `cash-heading` 两侧透明留白用于适配原 Image 矩形，不改变任何 RectTransform；余额、标题、金额、进度仍是原有动态 TMP 组件。新增图片不包含动态文字。
