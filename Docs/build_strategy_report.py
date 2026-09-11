from pathlib import Path
import re

template = Path('C:/Users/pc/Downloads/framework1-commercialization-strategy.html').read_text(encoding='utf-8')
head = template.split('<body>')[0].replace('框架1 · 商业化策略报告', 'Nut Sort Relax · 原版商业化策略分析')
script = re.search(r'<script id="strategy-navigation">[\s\S]*?</script>', template).group()
body = '''
<body><main>
<header><div class="eyebrow">NUT SORT RELAX · 2026.09.10 <span class="tag">原版策略分析</span></div>
<h1>商业化策略报告</h1><p class="muted">双余额奖励 · 领奖广告分流 · 道具激励 · 提现进度与地区差异</p></header>
<nav role="tablist" aria-label="报告内容">
<button id="tab-business" role="tab" aria-selected="true" aria-controls="business" data-nav="business">商业化总览</button>
<button id="tab-users" role="tab" aria-selected="false" aria-controls="users" data-nav="users" tabindex="-1">用户分层</button>
<button id="tab-rules" role="tab" aria-selected="false" aria-controls="rules" data-nav="rules" tabindex="-1">奖励与提现</button></nav>

<section id="business" role="tabpanel" aria-labelledby="tab-business">
<div class="card intro"><b>以现金目标吸引玩家开始，以领奖和道具需求形成广告机会，再用双余额与提现进度承接持续游玩。</b><p>这是对已观察机制的策略归纳。原版同时存在普通领奖广告、主动加奖视频和道具视频入口；尚无收入数据，不能确定哪一类广告贡献最大。</p></div>
<div class="policy-metrics"><div><span>用户提供的投放地区占比</span><strong>94.28<small>% BR + ID</small></strong></div><div><span>普通领奖广告阈值配置</span><strong>3<small>次领取</small></strong></div><div><span>该领奖链路的插屏阶段</span><strong>前 5<small>次广告机会</small></strong></div><div><span>结算预览采样规模</span><strong>242<small>笔 / 4 个账号</small></strong></div></div>
<div class="card"><h2>玩家流程</h2><ol class="journey">
<li><span>首次进入</span><b>欢迎页展示现金目标</b><small>“Clear levels to earn cash”；1～5阶段目标与 START</small></li>
<li><span>正常游玩</span><b>螺母分类与道具需求</b><small>刷新、撤回、增加螺杆等入口，提供观看广告获取道具的机会</small></li>
<li><span>通关结算</span><b>现金 + 钻石双奖励</b><small>普通 Claim 与视频 Claim 10X；早期结算分支存在差异</small></li>
<li><span>查看目标</span><b>现金进度 / 钻石档位</b><small>关卡进度、兑换比例、剩余金额持续可见</small></li>
<li><span>申请提现</span><b>填写渠道与后续条件</b><small>客户端存在金额、登录天数、等级等阶段分支；非达标即证明到账</small></li>
</ol><p class="caption">依据：原版截图、领奖按钮及提现分支审计 [E1/E2/E4]。流程说明入口关系，不代表所有用户按同一时间表经历全部阶段。</p></div>

<div class="card"><h2>普通领取也会进入广告链路</h2><div class="rule-steps">
<div class="step"><span>01 · 累计</span><b>普通领取计数 +1</b><p>未达到配置阈值，直接走奖励处理。</p></div>
<div class="step"><span>02 · 达到 3 次</span><b>清零普通领取计数</b><p>广告机会计数 +1，决定本次广告类型。</p></div>
<div class="step"><span>03 · 广告机会 1～5</span><b>请求插屏</b><p>源码比较包含第5次，不是第5次提前切换。</p></div>
<div class="step"><span>04 · 第6次起</span><b>请求激励类型</b><p>仍来自普通领取按钮的分支。</p></div></div>
<div class="note"><b>“每3次”只描述这条计数链路。</b>主动加奖会重置普通领取计数，早期关卡还有结算分支；不能直接换算成每3关必播广告，更不能当成全游戏每小时展示量。广告请求也不等于实际展示成功。</div>
<p class="caption" style="margin-top:12px">采集配置 LSSLRFP=3、LSSLRFV=5，控制流来自原版按钮逻辑 [E2]。未发现足够证据支持模板中的150秒保护、90→30秒间隔或按登录天数切换概率。</p></div>

<div class="card"><h2>广告入口与玩家动机</h2><div class="table-scroll"><table class="wrap-table"><thead><tr><th>入口</th><th>已确认表现</th><th>商业作用（推断）</th><th>尚未确定</th></tr></thead><tbody>
<tr><td><b>普通 Claim</b></td><td>按累计次数选择直接领取、插屏或激励类型</td><td>覆盖不主动选择加奖的用户，增加领奖节点广告机会</td><td>全局频控、填充率、实际每用户展示量</td></tr>
<tr><td><b>Claim 10X</b></td><td>原版结算有视频图标和10倍文案；MoreGet请求激励并重置普通领取计数</td><td>用明确的加奖表达提高主动观看意愿</td><td>没有证明现金和钻石都按同样方式乘10</td></tr>
<tr><td><b>刷新道具</b></td><td>Refresh弹窗解释“Refresh nut arrangement”，Claim按钮带视频图标</td><td>将卡关时的即时需求转为激励观看</td><td>各道具在所有库存、关卡下的完整频控</td></tr>
<tr><td><b>其他道具 / ADS螺杆位</b></td><td>原版主界面存在撤回、增加螺杆和ADS入口</td><td>提供不同解决困局的方式，承接继续游玩需求</td><td>不能仅凭图标推定每次使用均强制播广告</td></tr>
</tbody></table></div></div>
<div class="grid2"><div class="card"><h2>奖励与提现形成连续目标</h2><p>顶部常驻现金、钻石、阶段进度及提现提示。通关结算把本次收益具象化，提现页再展示剩余条件，玩家能从单关奖励回到长期目标。</p><p class="caption" style="margin-top:12px">策略推断：提高目标显著性，为继续闯关与主动加奖提供理由；没有留存实验可证明实际提升幅度。</p></div>
<div class="card"><h2>信任表达集中在提现页</h2><p>原版页面展示支付品牌、当日通关人数、平均尝试次数、平均提现额和其他玩家成功提现播报。</p><p class="caption" style="margin-top:12px">这些是界面展示事实 [E1]，不是已核验的交易记录。不能据此推导真实兑付率或支付成本。</p></div></div>
<div class="card"><h2>评估商业结果还缺哪些数据</h2><div class="table-scroll"><table class="wrap-table"><thead><tr><th>需要回答</th><th>应记录的指标</th><th>目前结论</th></tr></thead><tbody>
<tr><td>广告是否有效变现</td><td>各入口请求→填充→展示→完成率，分国家eCPM、ARPDAU</td><td>能确认请求链路，无法计算收入贡献</td></tr>
<tr><td>目标是否带来持续游玩</td><td>奖励余额分位下的继续率、道具观看率、次日与七日留存</td><td>机制可解释动机，效果待行为数据验证</td></tr>
<tr><td>买量是否回本</td><td>分渠道CPI、归因人群LTV、实际兑付成本</td><td>地区投放占比不能替代ROI</td></tr></tbody></table></div></div>
</section>

<section id="users" role="tabpanel" aria-labelledby="tab-users" hidden>
<div class="card intro"><b>已确认的差异集中在地区配置、领奖次数、奖励余额和提现阶段。</b><p>目前不能把巴西/印尼参数测试直接称为真实买量人群实验，也没有证据把模板的连续登录广告分层套进原版。</p></div>
<div class="card"><h2>主要投放地区：巴西与印尼占绝大多数</h2><div class="ad-mix">
<div class="mix-tier"><h3>巴西</h3><strong class="mix-value">75.69%</strong><div class="mix-track"><i style="width:75.69%"></i></div></div>
<div class="mix-tier"><h3>印度尼西亚</h3><strong class="mix-value">18.59%</strong><div class="mix-track"><i style="width:18.59%"></i></div></div>
<div class="mix-tier"><h3>日本</h3><strong class="mix-value">2.73%</strong><div class="mix-track"><i style="width:2.73%"></i></div></div>
<div class="mix-tier"><h3>美国</h3><strong class="mix-value">1.34%</strong><div class="mix-track"><i style="width:1.34%"></i></div></div></div>
<p class="caption">来源：你提供的“投放国家/地区排名”截图，另列法国0.61%。时间范围与占比口径未提供；不把它当成安装、付费用户或收入占比。</p></div>
<div class="card"><h2>原版的分流维度</h2><div class="table-scroll"><table class="wrap-table"><thead><tr><th>维度</th><th>依据</th><th>产生的差异</th><th>证据范围</th></tr></thead><tbody>
<tr><td><b>国家 / 地区</b></td><td>BR、ID配置及原版界面</td><td>奖励量级、钻石兑换比例、提现档位与支付渠道不同</td><td>配置与界面已确认；完整归因分组未确认</td></tr>
<tr><td><b>普通领取次数</b></td><td>NormalGetTimes / InterAdTimes</td><td>进入广告请求及插屏→激励切换</td><td>原版控制流及采集阈值</td></tr>
<tr><td><b>钻石余额</b></td><td>不同余额下重复结算预览</td><td>奖励总体随接近门槛而缩小，同余额仍波动</td><td>4个账号242笔；分布与精确分界仍属模型</td></tr>
<tr><td><b>早期进度</b></td><td>四个账号前3次为空，第4次非零</td><td>样本中的钻石奖励存在早期开启差异</td><td>样本规律，未证明所有用户一致</td></tr>
<tr><td><b>提现阶段</b></td><td>提现页分支读取余额、登录天数、用户等级</td><td>当前展示目标及按钮去向改变</td><td>客户端分支已恢复；完整服务端推进条件不全</td></tr>
</tbody></table></div></div>
<div class="grid2"><div class="card"><h2>地区配置不同，不等于已经证明个性化定价</h2><p>两国的数据量级不同，兑换比例与门槛也不同。现有样本支持“按地区使用不同奖励参数”；不足以判断同一国家是否按广告渠道、设备质量、用户价值或A/B分组进一步调整。</p></div>
<div class="card"><h2>美国只能作为有限对照</h2><p>已留存美国原机通关样本：现金 $2.95、钻石90.38，响应与屏幕显示一致。没有与巴西、印尼同等规模的余额梯度样本，暂不能证明美国整条曲线相同。</p></div></div>
<div class="card"><h2>本项目的配置约定与原版分开</h2><div class="rule-steps economy-stages">
<div class="step"><span>印尼用户</span><b>奖励使用印尼数据</b><p>实际货币、语言、渠道沿用对应地区。</p></div>
<div class="step"><span>巴西用户</span><b>奖励使用巴西数据</b><p>保持巴西的显示和支付配置。</p></div>
<div class="step"><span>其他国家</span><b>仅奖励数值回退巴西</b><p>显示国家与渠道仍遵循原版对应国家。</p></div></div>
<p class="caption">以上是你的工程约定，不是“原版所有其他国家都采用巴西策略”的结论。比如美国支付渠道不能因为数值回退而换成巴西渠道。</p></div>
</section>

<section id="rules" role="tabpanel" aria-labelledby="tab-rules" hidden>
<div class="card intro"><b>现金以阶段目标承接进度，钻石以余额兑换档位承接长期累积；钻石收益在靠近首档门槛时明显放缓。</b><p>余额曲线证据支持总体趋势，但目前区间模型仍是黑盒近似，不能称为完整服务器算法。</p></div>
<div class="card"><h2>双余额的规则差异</h2><div class="table-scroll"><table class="wrap-table"><thead><tr><th>项目</th><th>现金</th><th>钻石</th></tr></thead><tbody>
<tr><td>主界面</td><td>现金余额、Clear this level / Withdraw all提示</td><td>钻石余额、距离提现档位还差多少</td></tr>
<tr><td>提现页</td><td>Level标题、金额、关卡进度与后续任务状态</td><td>兑换比例、可选择金额档、余额进度条</td></tr>
<tr><td>结算</td><td>原版响应下发现金，本次与钻石并排显示</td><td>原版响应下发钻石，不能只显示现金</td></tr>
<tr><td>数值证据</td><td>地区解密配置含逐档下降规则；不把配置直接等同于全部实时结算公式</td><td>242笔预览形成余额区间模型，未唯一确定随机算法</td></tr></tbody></table></div></div>
<div class="card"><h2>国家配置：兑换比例与提现档位</h2><div class="table-scroll"><table><thead><tr><th>项目</th><th>巴西</th><th>印尼</th></tr></thead><tbody>
<tr><td>钻石兑换比例</td><td>100钻石 = R$1</td><td>10钻石 = Rp1</td></tr>
<tr><td>首档所需钻石</td><td>50,000</td><td>10,000,000</td></tr>
<tr><td>四档提现金额</td><td>R$500 / 1,000 / 1,500 / 2,000</td><td>Rp1,000,000 / 2,000,000 / 4,000,000 / 8,000,000</td></tr>
<tr><td>对应钻石需求</td><td>5万 / 10万 / 15万 / 20万</td><td>1,000万 / 2,000万 / 4,000万 / 8,000万</td></tr>
</tbody></table></div><p class="caption" style="margin-top:12px">来源：采集的两国奖励配置 [E3]。这里比较游戏内部兑换关系，不是现实汇率。</p></div>
<div class="card"><h2>按首档门槛归一化：两国都有减速，曲线并不完全相同</h2><p class="caption">“单次推进”=候选奖励区间 ÷ 首档所需钻石。区间来自当前样本模型，便于看趋势，不是服务器公布的保证范围。</p>
<div class="table-scroll"><table><thead><tr><th>当前余额 / 首档门槛</th><th>巴西候选钻石奖励</th><th>单次推进</th><th>印尼候选钻石奖励</th><th>单次推进</th></tr></thead><tbody>
<tr><td>0%（已开启钻石结算）</td><td>80～120</td><td>0.16%～0.24%</td><td>12,000～18,000</td><td>0.12%～0.18%</td></tr>
<tr><td>80%</td><td>16～24</td><td>0.032%～0.048%</td><td>4,500～7,000</td><td>0.045%～0.070%</td></tr>
<tr><td>90%</td><td>3.2～4.8</td><td>0.0064%～0.0096%</td><td>2,500～3,000</td><td>0.025%～0.030%</td></tr>
<tr><td>96%</td><td>0.8～1.2</td><td>0.0016%～0.0024%</td><td>350～700</td><td>0.0035%～0.0070%</td></tr>
<tr><td>99.5%</td><td>0.8～1.2（末档延用）</td><td>0.0016%～0.0024%</td><td>40～60</td><td>0.0004%～0.0006%</td></tr>
</tbody></table></div>
<div class="note"><b>趋势结论：</b>两国都呈现接近门槛后单次贡献下降，但减速位置、幅度不同。以区间中点比较，巴西低余额100降至末档1，约降至1/100；印尼15,000降至50，约降至1/300。不能据此计算必需关数：这还受初始奖励、其他来源及规则变化影响。</div></div>
<div class="grid2"><div class="card"><h2>策略含义：靠近目标后的推进变慢</h2><p>在同样一次通关下，后期余额增加更少，达到下一门槛需要更多积累。配合始终可见的剩余金额与广告加奖入口，可能延长游玩和广告接触。</p><p class="caption" style="margin-top:12px">这是机制推断，不等于已证明留存或收入增长；加奖对钻石的实际作用仍需领取链路数据确认。</p></div>
<div class="card"><h2>样本能证明什么</h2><p>固定余额重复请求仍得到不同奖励，排除了固定样本轮播。冻结模型后先测30笔，覆盖29笔；保留反例并修正区间，再测8笔，全部覆盖。</p><p class="caption" style="margin-top:12px">全量230笔非零记录回查全覆盖包含训练数据。它不证明均匀分布、精确边界、所有用户分组或未来服务端规则一致 [E5]。</p></div></div>
<div class="card"><h2>现金提现：阶段展示不等于单一“五关到账”规则</h2><div class="rule-steps">
<div class="step"><span>前台目标</span><b>1～5阶段进度</b><p>原图显示Level 4、3/5与“Clear level 5, withdraw all”。</p></div>
<div class="step"><span>账户信息</span><b>选择渠道并填写信息</b><p>按钮按当前阶段和是否已有账户信息分流。</p></div>
<div class="step"><span>后续条件</span><b>金额 / 登录 / 等级</b><p>后段源码分支分别读取目标金额、登录天数、用户等级。</p></div>
<div class="step"><span>最终结果</span><b>需服务端及支付证据</b><p>面板成功状态不能替代真实到账核验。</p></div></div>
<p class="caption">依据 [E4]。客户端存在多个阶段，不代表每个人必经所有分支；当前证据不支持照搬模板的六天任务和168小时等待，也不能把顶部5个阶段当成工程只有5个棋盘关卡。</p></div>
<div class="card"><h2>提现渠道遵循原版地区</h2><div class="table-scroll"><table><thead><tr><th>地区</th><th>原版渠道</th><th>电话区号</th></tr></thead><tbody>
<tr><td>巴西</td><td>Pix / Mercado Pago</td><td>+55</td></tr><tr><td>印尼</td><td>OVO / DANA</td><td>+62</td></tr><tr><td>美国</td><td>Venmo / Zelle / PayPal</td><td>+1</td></tr></tbody></table></div><p class="caption" style="margin-top:12px">渠道名称与界面配置代表可选入口，不能据此认定支付已接通或已成功兑付。</p></div>
<div class="card"><h2>证据口径与复核入口</h2><p>报告只采用原版截图、已采集响应与原版控制流审计。附带HTML提供视觉样式，不作为本游戏参数来源。</p>
<details><summary>E1 · 原版界面与投放截图</summary><p>来自本次对话：欢迎页、双余额结算、现金/钻石提现页、提现账户表单、Refresh道具弹窗，以及投放国家排名。提现页中的人数、金额、播报仅按显示内容引用。</p></details>
<details><summary>E2 · 领奖与广告控制流</summary><p>Docs/RewardGetButtonFlowAudit.md；Docs/LocalInterstitialAds.md中的“原版依据”。审计确认计数与分流；本项目的模拟广告成功回调不作为原版实际广告展示证据。</p></details>
<details><summary>E3 · 国家奖励配置</summary><p>Docs/CountryRewardConfiguration.md；Assets/Resources/Configuration/CountryRewards/BR-reward-config.json、ID-reward-config.json。奖励国家回退巴西是项目约定，与实际显示国家分开。</p></details>
<details><summary>E4 · 提现分支</summary><p>Docs/WithdrawalClaimAudit.md；WithdrawalLaterProgressAudit.md；WithdrawalSuccessFlowAudit.md。分别支持按钮路由、后段条件与成功面板行为的边界；不证明实际付款。</p></details>
<details><summary>E5 · 钻石样本与模型</summary><p>Docs/DiamondAlgorithmAudit.md；Docs/RewardEvidence/DiamondAlgorithm/validation-report.json。共242笔成功预览，BR100、ID142，每国2个账号；230笔非零、12笔为空。美国单次对照：Docs/RewardEvidence/diamond-US-level5-server.json。测试地区参数不等于已经完成真实买量归因实验。</p></details>
<div class="note">尚不能确定：完整服务器奖励算法、所有国家/用户分组、全局广告频控、实际收入与留存、真实兑付率。当前本地工程使用数值模型和可注入广告边界，不能把工程验证通过写成原版服务器或支付系统已还原。</div></div>
</section><footer class="foot">Nut Sort Relax 原版商业化策略分析 · 依据截至2026-09-10的现有材料 · 原版事实、样本模型与策略推断已分开说明。</footer>
</main><noscript><style>[role="tabpanel"][hidden]{display:block!important}nav,[data-jump]{display:none!important}</style></noscript>
'''
output = Path(__file__).with_name('nut-sort-relax-commercialization-strategy.html')
output.write_text(head + body + script + '\n</body></html>', encoding='utf-8')
Path('C:/Users/pc/Downloads/nut-sort-relax-commercialization-strategy.html').write_bytes(output.read_bytes())
print(output)
