# 提现界面接入

现金 Cash 打开 `TXPanel.prefab`；钻石 Cash 打开独立的 `TXCoinPanel.prefab`。静态布局复用恢复的原版 Prefab，交互使用 Unity Button 并由代码绑定。

现金显示当前余额、关卡标题、进度及目标关卡文案，第 4 关为 3/5、Clear level 5, withdraw all。补齐下方 PlayerInfo 统计卡和 PMDSimple 播报区，并修复原导出 Sprite 缺失渲染数据导致的白底。

钻石界面包含 My Diamonds、钻石数、兑换后金额、兑换比例、四档提现金额、当前档位说明和进度。选中项使用原版黄色背景和绿色边框，其余蓝色。金额及兑换率从国家配置 bear_zs_list、bear_rates 读取。

奖励数值对 ID/IDN 使用印尼配置，其他国家采用巴西数值；其他国家行为遵循实际国家的原版配置。实际余额来自用户存档，没有将参考截图余额写入游戏。

关卡统计和播报没有可用实时数据源：统计显示 --，播报显示 Withdrawal records unavailable。不会把参考截图的人数、平均金额和其他玩家提现记录当成实时数据。当前仍未连接实际提现服务，点击不扣款或生成成功记录。

验证使用隔离 Unity 工程的 LocalWithdrawalPlayValidation，覆盖 BR/ID 两个现金入口、独立钻石入口、目标金额、档位选择、重复打开防护及关闭后的输入恢复。截图在 RewardEvidence/local-withdrawal-*.png。

本轮验证通过：NUT_LOCAL_WITHDRAWAL_PASS。最终日志无编译错误、运行异常或资源加载错误，已人工检查现金统计背景及 BR/ID 钻石界面。

钻石 Withdraw 点击反馈修复：此前余额不足只重新刷新说明，视觉无变化；现在使用已有 PopTip Prefab 弹出所选档位的余额要求及差额。金额满足时弹出服务未连接提示。操作不修改余额。

现金 Withdraw 已连接 TXUserInfoPanel，再进入 TXUserInfoSurePanel：沿用原版渠道、电话区号、输入验证、信息确认和重新编辑流程。BR 使用 Pix/Mercado Pago、+55；ID 使用 OVO/DANA、+62。确认信息保存在本地用户存档；没有网络提交或实际付款，最终确认显示服务未连接提示并返回现金面板。

账户流程集成验证通过：现金按钮入口、空输入拒绝、信息确认、重新编辑保留数据、渠道切换及回到现金面板，BR/ID 均通过。

国家规则修正：只有奖励数值对非印尼用户回退巴西；实际国家独立保留，提现渠道、区号、币种格式和图标使用原版国家配置。US 使用 Venmo/Zelle/PayPal、+1、美元展示，同时数值配置使用 BR。NUT_COUNTRY_SEPARATION_PASS 已通过美国及印尼完整流程验证。
