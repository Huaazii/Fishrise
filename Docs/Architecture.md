# 架构记录

需求基线：需求文档.md v1.1 第 3.3、3.4、5、6 章。

## 当前确定的边界

- Core 为纯 C#，Game → Core 单向依赖。Unity 资源和 Physics2D 不进入 Core。
- Game 将 ScriptableObject 复制为本局只读规则，显式传入 dt 与批次。
- RunModel 每局创建，捕获记录是数量、容量使用与收益的唯一来源。
- Main / Branch 来源及 HookId 从第一阶段保留；第三阶段已接入声呐和分支能力，仍由同一个捕获流程统一提交记录。
- 接触按物理步统一收集、稳定排序、裁决、提交，不能在回调中直接改规则。

## 已实现类型

| 类型 | 程序集 / 职责 |
| --- | --- |
| FishingRules / FishRules | Core：只读规则快照 |
| RunModel | Core：本局状态、时间、深度、保护、唯一捕获记录与派生统计 |
| CaptureRecord / CaptureSource | Core：鱼种、本局鱼实例、Main/Branch、HookId、深度、时间、两种货币价值快照 |
| Settlement / FishSummary | Core：每鱼种合并，保留主钩与分支数量，按金币单价降序排列 |
| FishingFlowController / IFlowState | Core：状态机、接触裁决、统一捕获提交 |
| ContactCandidate / ContactKey / ContactBatch | Core：候选、接触对、完整物理步数据 |
| IAbility / AbilitySlot / AbilityController | Core：两个能力策略、资格、次数、游戏时钟冷却与 0.2 秒防抖 |
| AbilityRules / SonarRules / BranchHookRules | Core：每局只读的能力限制和效果参数 |
| SonarAbility / SonarTarget / ScareCommand | Core：筛选小鱼、距离、身份和受惊接触豁免；不查询物理 |
| BranchHookController / BranchHookModel | Core：展开/退回状态，容量从 RunModel 的来源钩记录读取 |
| FishingConfig / FishDefinition | Game：Unity 配置资产类型；Snapshot 转换为纯规则 |
| FishSpawner / FishActor | Game：分段固定种子鱼群、受限泳道与通道、Queue 池、每次生成新的 ID、挂鱼与回收 |
| HookMotor / ContactDetector | Game：移动、回调收集、扫掠/末端重叠查询与批次构建 |
| DragInput | Game：鼠标与触摸共用相对位移映射，过滤 UI 起始触点 |
| GameUi / TutorialView / ResultView | Game：中文菜单/HUD、三页教学与唯一记录驱动的结算卡片 |
| GameUiAssets / UiWidgets / UiText | Game：共享字体/图像引用、UGUI 组件与中文文案，不持有玩法计数 |
| AbilityDefinition / SonarConfig / BranchHookConfig | Game：能力配置资产与图标，转换成 Core 快照 |
| AbilityRig / BranchHookView / AbilityHud | Game：声波、空间查询、两钩复用、扫掠批次合并、能力按钮与分支 HUD |

程序集：Fishing.Game → Fishing.Core；Fishing.Core.Tests 仅引用 Core 与测试框架。Core asmdef `noEngineReferences=true`，已有程序集依赖测试。

## 固定步契约

**已由 GameBootstrap 装配，并通过保存场景的 PlayMode 测试验证。**

1. 调用 `BeginStep(dt)`，推进游戏时间、状态与深度，同步两个能力槽的时钟显示，取得 StepSequence。
2. `ContactDetector.Begin()` 清空本步回调缓存；Game 依当前深度与输入移动主钩、推进鱼与受惊状态，并移动两侧分支/声波。各分支保留自己的步初位置。
3. 同步 Transform，并由 Game 在明确的固定步中运行物理模拟。若采用手动 `Physics2D.Simulate`，需保存并在退出时恢复全局 simulationMode。
4. 本步物理回调完成后调用各钩的 `Finish(run, step, previousPosition)`，合并回调、圆形扫掠和末端重叠；AbilityRig.Combine 将全部钩合成同一个局/物理步批次。
5. 仅一次 `ResolveContacts(batch)`，然后应用规则结果到 View。不得在模拟之前提前提交批次。

ContactBatch 头部保存 RunId / Step，候选继承其局与物理步身份。候选为 HookId、FishId、SpeciesId、DistanceSquared。距离是本步钩路径到鱼头中心的最近距离平方，Game 计算后以纯数值传入。

当前 ContactBatch 复制候选与末端接触列表形成只读快照；尚未验证分配量或性能。性能收敛时可改为同步借用缓冲区，但不能让规则保留会被下步覆盖的数据。

## 接触生命周期与裁决

- ContactKey 是 (HookId, FishId)。EndContacts 只包括步末仍重叠的鱼；短暂扫掠可只有候选、没有 EndContact。
- 同步重复候选按接触对合并，保留更小距离，按距离 / FishId / HookId 排序。
- 受惊鱼在 Game 关闭头部 Collider，Core 也排除其候选和末端接触，恢复后能按新的有效接触判定。
- 保护区下潜只处理排序后的首个有效新接触。其余进入接触、以及无敌期内开始的接触均保留末端记录，无敌到期不补判。
- 完全分离后，下步末端接触记录不再含该接触对，再进入可重新判定。
- 进入底部区或转为上收清空保护无敌与阶段接触记录，使仍重叠的鱼在新阶段参与捕获；捕获去重集合不清空。
- 到最大深度时先按底部规则裁决，再进入 BottomHold。最后一段上收扫掠也先裁决，再进入 Surfacing。
- 上收先给有效分支分配小鱼，再给主钩分配剩余候选。每个阶段都按距离/FishId/HookId 排序，用临时容量与已分配鱼集合完成整个批次后再统一提交；分支满时可尝试另一分支或回退主钩。主钩满载不阻止分支继续捕获。
- 暂停拒绝批次。局 ID 不符、过期/未来步、重复步批次均丢弃。重开必须创建新 controller / RunModel，并清空 Game 检测缓存。

## 事件与顺序

- `RunModel.OnDepthChanged(float)`：深度及最大实际到达深度更新后通知。
- `RunModel.OnProtectionChanged(int)`：扣保护并设置无敌截止时间后通知。
- `FishingFlowController.OnFishCaptured(CaptureRecord)`：记录和全局去重集合已提交；View 可关闭鱼头判定、移出活跃集合并挂鱼。
- `RunModel.OnBecameFull()`：同一捕获的 OnFishCaptured 之后通知，整局最多一次。IsFull 始终从主钩记录派生。
- `FishingFlowController.OnStateChanged(RunState)`：进入新状态、重置该状态计时后通知。无保护提前上收时顺序为捕获 →（若满）满载 → 状态变更。
- `FishingFlowController.OnRunFinished(RunModel)`：进入 Result 的状态事件之后通知一次；两种货币和最大到达深度已可读取。
- 暂停是 IsPaused 覆盖层，不切换流程状态；BeginStep 不推进时间与序号。

## 第三阶段能力与统一分配

FishingFlowController 为每局建立 AbilityController，拥有 SonarAbility 和 BranchHookController。AbilitySlot 使用 RunModel.GameTime 的截止时刻计算冷却和防抖，暂停不另走计时。Activate 先提交次数与策略状态，再通知 Game 的空间查询或视图；Cancel 停止旧局的进一步命令，不退还次数、不移除捕获记录。

SonarAbility 在非底部区 Diving 激活，Game 以释放瞬间的钩位置查询 FishingSmallFish 层，将鱼 ID、鱼种、距离平方和横向方向提交 Core。Core 用本局 FishRules 快照裁决，一次激活只接收一次查询。Game 对接受的小鱼应用 Scare：关闭鱼头判定，以 3 倍游速逃离；恢复后继续在实际水域范围游动，不拉回原鱼群泳道。声波环的 0.4 秒展开是表现，命中在释放时统一判定。

分支 ID 为 1/2，主钩为 0。BranchHookModel 只存展开/退回状态；每钩已用量从 RunModel.CountForHook 派生。分配期间使用临时余量，提交后才发捕获事件，挂鱼按记录来源钩选择局部挂点。两个 BranchHookView 在场景中一次创建并复用；重开先回收鱼，再隐藏/重置钩，不遗留挂鱼或重复订阅。

FishingSmallFish 与 FishingBranchHook 为专用图层，基础分支碰撞矩阵仅允许小鱼；分支查询默认也只查小鱼。可选大鱼断钩默认关闭，打开后额外查询大鱼并退回对应钩，已有挂鱼保留。新局会按该局规则重新设置复用钩的查询范围。

## 已装配的 Unity 生命周期（截至 2026-10-21）

- Menu 与 Main 均保存 GameBootstrap 的配置、7 种鱼、Fish/Hook 预制体、Camera 与 GameUiAssets 引用；Main 可直接 Play。Menu 首次开始先教学，关闭后加载 Main 倒计时；结算/暂停退出加载 Menu。
- GameBootstrap.Start 保存原 Physics2D.simulationMode 并启用 Script；FixedUpdate 使用 GameClock 的显式时间，顺序与上述固定步契约一致。OnDestroy 解除订阅并恢复原模拟设置。
- GameClock 不改变全局 Time.timeScale。暂停冻结规则、鱼、钩与摄像机，UGUI 按钮仍可交互。倒计时不接收拖动输入。
- BeginRun 先解除旧订阅、回收全部本局鱼并清空检测缓存，再创建新 flow / RunModel、重新绑定事件并生成鱼。
- 鱼的活跃/缓存视图均保留在场景所有者子树；捕获后移到同一所有者下的 Hook。场景销毁时交由 Unity 销毁子树，不在 OnDestroy 中回收已经部分销毁的 Collider。此修复由场景切换与 20 局重开测试覆盖。
- GameUi 只创建一套 UGUI 与按钮回调；HUD 数量来自模型事件，结算来自 Settlement。TutorialView 和 ResultView 分别负责三页教学及可滚动卡片，共用 GameUiAssets 的中文字体和现有图像。
- CameraRig 按宽度匹配，暂停不推进；第二阶段已接入方向缓动与统一水面构图，真实三比例 Game View 的界面与玩法回归通过；设备检查留交付阶段。
- Sprite 资源留在 FishDefinition；规则快照不包含 Sprite。使用完整鱼图时隐藏几何尾巴/眼睛，头部 Collider 独立。
- FishingSceneBuilder 通过 Unity API 只创建缺失资产，不重写已有配置、场景或预制体；重运行用于补缺，已有资产的后续修改需单独编辑并验证序列化引用。
- FishingPreview 只修改 Editor 的 Game View 分辨率；Player 使用独立的竖屏设置。
- 首个 APK 由 FishingAndroidBuild 构建 Menu/Main，IL2CPP ARM64 Development，API 23/34，版本 0.1.0；已于 2026-10-20 成功构建，报告 android-build-20261020.json；APK 实际大小约 29.4 MiB，v1/v2 签名有效，未做真机安装验收。

## 后续仍待实现

音频、出水鱼演出与最终反馈、实体输入/设备验收和性能测量。声呐/分散鱼钩及 HUD 已实现并完成 Editor 检查；三比例及模拟安全区域检查不能替代真实设备。

## 第二阶段核心美术检查点（2026-10-20）

- FishDefinition 新增 Game 层的 headPosition / headRadius / mouthPosition，分别校准四种鱼的鱼头与挂鱼位置。Core 快照、捕获批次与记录没有增加美术字段。
- Sprite 副本按有效区域裁边并用 PPU 统一到 1.25 单位基准宽；FishActor 不再额外放大 Sprite。左右游动用同一根变换镜像外观与头部，边界转向同样生效。
- Hook.prefab 的 Head 使用 B04，本体保持白色；Lamp 仍是独立节点。Rope Anchor、10 个 Hang Points 和 3 个 B06 小气泡均序列化在同一预制体。保护泡只影响显示，不扩大玩法碰撞。
- Captured 仍从 CaptureRecord 取唯一捕获身份和序号，FishActor.Attach 将鱼嘴对齐给定的钩内局部挂点；没有增加独立的捕获计数。重开回收时还原鱼的旋转，避免对象池继承悬挂姿态。
- 小气泡的位移在 HookMotor.Move 的显式 dt 中推进，BeginRun 重置时间；暂停不会调用 Move。此检查点没有新增 Update 动画或全局 Time.timeScale 操作。
- FishingCoreArtImporter 只更新四份既有鱼配置、Hook 预制体与 7 个工程内图像副本；Main/Menu 场景与所有既有配置/预制体/场景 GUID 保留。重新导入会重设这些指定的美术参数，不用于保护人工修改过的同一美术参数。

## 第二阶段场景与镜头检查点（2026-10-20）

- FishingEnvironmentBuilder 导入 17 个现有素材与两张程序渐变底色，保存 Environment.prefab，并给既有 Main/Menu 各装配一个实例。以预制体来源识别实例，不依赖 Unity 可能改变的对象名。旧占位环境停用但保留；不改鱼种或玩法参数。
- GameBootstrap.sceneView 保存场景级 FishingSceneView；它只切换渔夫 Sprite、鱼竿手位/角度与背景鱼影视差。渔夫两图保留同画布与 pivot，根位置不随姿态改变；场景装饰没有物理判定和捕获身份。
- HookMotor 的运行时 SetRopeOrigin 绑定场景竿尖与冰洞导向点，Hook.prefab 无跨场景引用。入水后鱼线依次连接竿尖、洞内导向点和钩顶吊环；钩在洞口以上时省略导向点。姿态/钩位置变化后统一刷新鱼线。
- CameraRig 默认水平半宽为 halfWidth + 1.5（当前 6），左右操作边界仍由原 halfWidth 控制。深水下潜/上收的钩视口位置分别为 y=0.8/0.2；状态转向从当前偏移开始以 SmoothStep 在 0.5 秒内到目标。水面段限制镜头上界，Countdown/Surfacing/Result 共用同一构图。
- GameBootstrap.LateUpdate 使用暂停感知的游戏更新条件推进镜头，并让 8 个背景鱼影按相机纵坐标的 0.12 比例偏移。BeginRun 重置镜头与视差基准；暂停不推进这些表现。
- 连续水色覆盖全海域；两侧冰壁交替 flipY，5 单位高、4.9 单位间距并有底色兜底。铺设与可视边界只是表现，不新增墙体玩法或改变接触批次。

## 第二阶段七鱼种与分段鱼群检查点（2026-10-21）

- FishDefinition 在 Game 层保存稀有度、出现深度和游速区间；原四种鱼 ID/收益/体型保留，补充 yellow、eel、silver。Core 规则快照与唯一捕获记录的边界保持不变。
- FishingConfig.depthBands 是数量的唯一配置源，FishCount 从表派生。FishDepthBand 保存深度范围、行数（纵向密度）和 FishPopulationEntry 数量/鱼群大小；四段为 0–25、25–50、50–75、75–100 米，共 12/13/14/15 条。
- FishSpawner 先给可用深度较少的鱼种分配行，再把普通鱼群填入剩余位置；每行至多左右两个鱼群。红鱼从 20 米、黄蝴蝶鱼从 60 米、鳗鱼从 85 米后分配，固定种子决定选行、左右位置、初始朝向与速度。
- 每行保留略有左右变化的通道。以鱼身与鱼头判定的较大横向包围范围计算泳道，通道至少为实际主钩判定直径的 2.5 倍。FishActor.SwimRange 在每次生成时重置，普通鱼群成员共享速度、朝向与相位，在各自偏移后的范围内往返。第三阶段的受惊小鱼会离开原泳道并保留位移，恢复后不保证原通道仍完整；其受惊豁免、边界与恢复单独验证。
- FishingPopulationBuilder 复用现有素材导入器，只添加三份工程图副本、三种鱼配置，更新分段表和两个既有场景的 species 数组；不重建场景。导入器对纹理缩放取整后的实际 Sprite 尺寸校正 PPU，保留 2048 纹理上限与 1.25 单位基准宽。
- PlayMode 使用保存场景验证分段数量与价值、七种鱼双向判定/混合捕获、固定种子复现、60 秒显式游动的通道，以及通过真实 Physics2D、不消耗下潜保护的 100 米路线。显式步进、实际帧运行和实体输入验收分别记录。

## 第二阶段中文界面与适配检查点（2026-10-21）

- GameUi 是场景级界面组合入口，保留稳定的对象标识供导航与验收使用，玩家看到的文字统一为中文。UiWidgets 只构建常用 UGUI 元素，UiText 保存教学及鱼种中文名；单个脚本保持小于 300 行。
- 首次开始标记为 PlayerPrefs 的 CasualFishing.TutorialSeen.v1，首次点开始先打开教学，关闭或读完后记为已看并进入 Main。手动教学只返回 Menu，不替代首次开始流程。测试与画面检查会保存并恢复原标记，不清空其他偏好。
- 保护/主钩/深度在模型事件触发时刷新；轻微数值弹跳由 GameUi.Tick 接收同一显式游戏 dt，暂停时不推进。保护在水底/上收时保留数值并弱化颜色，主钩满载以红色显示。此界面检查点当时未接入分支 HUD，第三阶段已补齐。
- ResultView 直接调用 Settlement.Summarize(run.Captures)，每鱼种一张卡，按单条金币快照降序显示。单价取记录聚合，不回读可变配置中的当前收益；图像和稀有度来自共享 FishDefinition 引用。混合来源保留 Main/Branch 明细及角标，不另记捕获数量。
- ScrollRect/RectMask2D 负责七种鱼时的滚动和裁切，空收获清除旧卡片并显示零总额。UI 的混合来源检查使用合成记录，只验证展示，不代表分支玩法已实现。
- CanvasScaler 按宽度匹配；安全区域同时检测尺寸和 Screen.safeArea 的变化。面板高度有上限，页内采用相对锚点，HUD/按钮保留边距。FishingPreview.Resolution 提供真正的 450×800/975/1050 编辑器窗口，分别检查布局与保护/满载/暂停/重开。
- FishingUiBuilder 仅导入 15 张既有 UI/教学图，复用 B04 主钩、B05 保护泡和既有 C12 银鱼；向 Main/Menu 追加 UI 资产引用，不重建场景。Noto Sans SC 静态字体随工程引用，OFL 授权文本同时放入 StreamingAssets。

## 第四阶段：出水演出与事件反馈（2026-10-21）

- 捕获仍只由 Core 提交 CaptureRecord。Surfacing 开始前收益已确定；FishingFlowController.SkipSurfacing 只接受 Surfacing，转到 Result，重复命令不再发布 OnRunFinished。不暂停或重算捕获，也不由动画完成回调增加奖励。
- 当前 FishingConfig 的演出时长为 2.3 秒。SurfacePresentation 读取捕获快照，预建 16 个鱼/金币视图；主钩按序、分支序列延后 0.3 秒，自动压缩间隔适配总时长。结束/跳过立即隐藏所有活动与待播视图，重开重置调度；UI 演出不操作玩法鱼判定。
- GameUi 在 Surfacing 隐藏玩法 HUD，以单独标题、鱼跃、金额牌和跳过按钮显示收获。ResultView 卡片交错展开，结果阶段继续由 GameClock 推进界面。鱼嘴挂点摆动及捕获缩放由 FishActor.Tick 更新，FishSpawner 统一推进游动和挂鱼；整图鱼仅作轻微视觉摆动，不拆分新鱼尾素材、不移动头部碰撞区。
- GameplayFeedbackView 预建 16 条捕获飘字、8 个保护气泡和 8 条满载速度线，全部复用。满载以既有红灯与速度线共同表示；镜头宽度与跟随基准保持原适配结果。BeginRun 清空旧效果，暂停跳过游戏时钟与音效推进。
- GameFeedback 只接收已提交事件：入水、保护消耗、捕获、主钩满载、出水、结算，以及成功的声呐/分支命令。8 个短音由数学波形生成，8 个声音通道有上限，随场景释放。音效/震动偏好通过 PlayerPrefs 保存，菜单和暂停共用设置；不保存收获。Android 捕获/保护请求 20 ms 震动，API 26+ 幅度 60；旧系统使用时长接口。构建后处理只向生成的 manifest 补充 VIBRATE 普通权限。
- Core/Game 测试分别验证跳过边界与真实物理/场景；正常帧演出、原生窗口自动点击、APK 构建、安装、手机触摸、声音听感和性能验收分别记录。Editor 或自动注入输入不能代替真人/手机验收。

第四阶段补充：应用后台状态单独进入 GameClock，覆盖不接受玩家暂停的 Countdown/Surfacing/Result。后台时 AdvanceGameplay/界面/音频均停止；返回后普通游戏仍停留在暂停面板，出水演出则从同一进度继续。场景测试通过显式触发 Unity 应用回调验证该路径，不等于 Android 后台实测。
