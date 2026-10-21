# 第三阶段：声呐与分散鱼钩

2026-10-21，Unity 2022.3.52f1 / URP 2D，需求基线 v1.1。FR-18、FR-19 和分支 HUD 已完成 Editor 检查，下一阶段为出水演出、反馈与交付验证。

## 实现

| 能力 | 当前行为 |
| --- | --- |
| 下潜声呐 | 非底部区 Diving 可用；每局 2 次、冷却 4 秒；释放瞬间查询 4 米内小鱼，声波在 0.4 秒展开；小鱼以 3 倍游速横向逃离 2.5 秒，期间关闭鱼头判定。恢复后继续当前位置游动，大鱼不受影响。 |
| 分散鱼钩 | Ascending 每局展开 1 次；两侧小钩按屏宽约 18% 扇形展开、阻尼跟随，各容纳 3 条小鱼；独立于主钩 10 条容量。主钩满载后仍可通过扫掠捕获。 |
| HUD 与按钮 | 增加分支 x/6；两侧独立次数/冷却按钮，0.2 秒防抖、阶段限制和暂停保护。按钮位于上收钩上方，保留中央拖动区域。 |
| 统一裁决 | 每步合并所有钩候选，先给分支分配小鱼，再给主钩分配剩余候选；距离/FishId/HookId 稳定排序、临时预留容量、统一提交。计数与收益均来自 RunModel.Captures。 |
| 复用与风险 | 两个场景拥有的 BranchHookView 反复复用，按记录来源钩挂鱼；重开回收旧鱼并重置钩/次数/冷却/受惊。可选大鱼退回默认关闭，开启时保留已抓鱼。 |

配置在 SonarConfig.asset、BranchHookConfig.asset；打开工程即可运行，无须重新执行装配。需要重新补齐绑定时使用 Fishing/Prepare stage 03 abilities。该工具保留现有场景与资源身份，只增加两能力引用和专用图层/分支碰撞矩阵。

没有新增美术。分支复用 B04 主钩图，声呐按钮复用 B05 气泡图，声波与细线使用已有材质和 LineRenderer。普通鱼群仍使用受限泳道；受惊小鱼恢复后保留位移，原行通道可能变化，不承诺声呐之后继续保持原通道。

## 验证层级

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Rider | 25 个本阶段 C# 文件无错误 | [诊断](abilities-rider-20261021.json) |
| Unity 编译 | 编译与控制台均 0 错误、0 警告 | [编译](abilities-compilation-20261021.json)、[控制台](abilities-console-20261021.json) |
| 纯规则 | Core 40/40（原 23＋能力 17） | [EditMode](abilities-editmode-20261021.json) |
| Unity 集成 | PlayMode 32/32（原 21＋能力 11） | [PlayMode](abilities-playmode-20261021.json) |
| 正常游戏帧 | 固定种子，100 米、主钩 10＋分支 3＋3，共 16 条；250 金币/190 贝壳 | [单局采样](abilities-live-run-20261021.json)、[真实记录与卡片](abilities-live-settlement-20261021.json) |
| 正常帧暂停 | 声波与受惊鱼活动中暂停约 1.25 秒，时钟/冷却/鱼/钩/声波冻结 | [暂停采样](abilities-live-pause-20261021.json) |
| 按钮重开 | 新局 ID，保护 2、54 条鱼、零捕获/受惊/挂鱼，声呐 2 次/分支 1 次、两钩隐藏复用 | [重开](abilities-live-restart-20261021.json) |
| 三比例 | 实际 450×800/975/1050、模拟安全区、按钮横向与纵向避让、中文字与按钮射线命中 | PlayMode 的 ThreePortraitRatiosKeepAbilityButtonsInSafeAreaAndOutOfTheCentralDragLane，另保留原三比例综合回归 |

正常帧单局仅通过脚本提供横向目标，并调用实际声呐、分支、暂停按钮回调；规则与物理由正常 FixedUpdate 推进，没有显式调用 AdvanceGameplay。它与显式步进测试分开记录，也不代表实体鼠标/手机输入验收。

## 对照能力验收

| 需求验收 | 对应证据 |
| --- | --- |
| S1：范围内小鱼受惊，重叠不扣保护/不收钩 | SonarScaresOnlyInRangeSmallHeadsWhileLargeFishStillSpendsProtection；Core 也验证旧接触候选豁免和无保护场景 |
| S2：大鱼照常触发 | 同上；范围外小鱼仍可触发提前上收的纯规则检查 |
| S3：次数/冷却/阶段限制与暂停 | SonarUsesResetWithPooledFishAndButtonLocksAtTheBottomAndOnAscent；PausingFreezesCooldownEscapeAndWaveAndRejectsActivation；正常帧暂停采样 |
| S4：恢复不跳位，重新参与捕获 | RecoveryKeepsDisplacedPositionRestoresHeadAndAllowsAscentCapture；Core 验证恢复后重叠重新判定 |
| S5：重开清理 | SonarUsesResetWithPooledFishAndButtonLocksAtTheBottomAndOnAscent；真实结算按钮重开 |
| B1：主钩满载后仍捕获小鱼 | FullMainSweepsSixSmallFishIntoBranchesAndBuildsRealMixedSourceCards：主钩先 10/10，再由分支各抓 3 条 |
| B2：默认大鱼穿过 | SimultaneousMainAndBranchHeadContactGoesToBranchAndLargeFishPassesThrough；默认专用图层/小鱼查询 |
| B3：容量/去重/顺序与回退 | Core 三种报告顺序一致；一分支满回退另一分支，全部分支满回退主钩；真实物理 10＋6 不超限 |
| B4：真实来源合并与收益 | 上述 FullMain 测试从真实物理记录渲染混合卡片；正常帧结算小银鱼主钩 2＋分支 3、共 5 条一张卡 |
| B5：暂停/20 局重开无残留 | TwentyRestartsReuseTwoBranchViewsAndPauseFreezesHangingFishAndTethers；正常帧重开 |
| B6：满载高速不漏判 | FullMain 测试在 5 倍上收步长 0.65 下命中小鱼，步初/步末均不重叠，依靠 CircleCast 扫掠捕获；另有 FullMainBranchSweepsTheSavedSeedPopulationWithoutMovingItsSmallFish 使用原固定种子小鱼群，目标不挪位、不改泳道或游速，只将 10 条大鱼对齐主钩建立满载前置，仍高速捕获为 Branch。正常固定种子单局另记录完整 16 条。 |

表中 S/B 编号指 8.5.1/8.4.4 的验收序号，并非规则表的全部 S1–S7/B1–B8。可选大鱼退回另由 OptionalLargeFishRiskRetractsOnlyThatBranchAndKeepsItsCapturedFish 验证，默认仍关闭。

## 画面与修正

- [声呐](abilities-sonar-20261021.png)、[暂停](abilities-pause-20261021.png)、[下潜](abilities-diving-20261021.png)
- [分支捕获](abilities-branches-20261021.png)、[10＋6 满载上收](abilities-full-20261021.png)
- [9:16 真实结算](abilities-result-20261021.png)、[9:21 七卡顶部](abilities-result-seven-921-20261021.png)、[拖动到底部](abilities-result-bottom-921-20261021.png)、[滚动数值](abilities-result-scroll-20261021.json)

首轮 30/30 后复核了可选风险的新局规则切换，补齐复用检测器的图层查询更新并增加实测，达到 31/31。画面复核又发现按钮与上收钩重叠；按钮现按屏幕相对高度放在钩上方两侧，补充纵向避让断言后再次 31/31，再次完成正常帧 10＋6 单局。最后补齐原固定种子小鱼群的满载高速检查，全量最终 32/32。

保留[按钮移动前上收图](abilities-branches-before-buttons-20261021.png)、[移动前满载图](abilities-full-before-buttons-20261021.png)、[移动前测试](abilities-playmode-before-buttons-20261021.json)与[新增风险检查前测试](abilities-playmode-before-risk-filter-20261021.json)。编译重载期间一次工具连接中断及尚未开始的测试调用没有计入通过；最终确认最新代码已编译后才执行上述最终检查。

## 保留与交接

- [完整性](abilities-preservation-20261021.json)：45 张源 PNG 哈希不变，40 份既有 UI 文件不变，既有跟踪 .meta 未改；Main/Menu 只有两能力绑定新增，包含原摄像机在内的旧场景内容保持；Physics2DSettings 仅更改碰撞矩阵。
- [重复装配](abilities-repeat-assembly-20261021.json)：15 份配置、场景与设置重复执行后哈希相同；既有鱼数量、价值、图像和环境未由本轮装配重写。
- [最终状态](abilities-final-scene-20261021.json)：Menu、450×800、未运行、无未保存场景、绑定完整，首次教学标记恢复为未看。

未生成新图片，未构建新 APK；现有 0.1.0 仍是第一阶段版本。未做实体输入、设备屏幕、帧率/内存/GC 测量；20 局对象复用检查不代替性能或内存验收。此前桥接包自带手柄释放测试失败仍在 population-editmode-with-bridge-20261021.json，本轮只对游戏程序集报告通过。

下一步进入第四阶段：出水鱼与金币飘字、可跳过演出（分支延后 0.3 秒），验证跳过与完整演出的结算一致；随后接入最小反馈/音效，进行性能测量、当前版本构建和设备验收。
