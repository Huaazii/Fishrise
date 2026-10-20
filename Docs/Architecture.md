# 架构记录

需求基线：需求文档.md v1.1 第 3.3、3.4、5、6 章。

## 当前确定的边界

- Core 为纯 C#，Game → Core 单向依赖。Unity 资源和 Physics2D 不进入 Core。
- Game 将 ScriptableObject 复制为本局只读规则，显式传入 dt 与批次。
- RunModel 每局创建，捕获记录是数量、容量使用与收益的唯一来源。
- Main / Branch 来源及 HookId 从第一阶段保留；分支和声呐能力在第三阶段实现。
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
| FishingConfig / FishDefinition | Game：Unity 配置资产类型；Snapshot 转换为纯规则 |
| FishSpawner / FishActor | Game：固定种子占位鱼、Queue 池、每次生成新的 ID、挂鱼与回收 |
| HookMotor / ContactDetector | Game：移动、回调收集、扫掠/末端重叠查询与批次构建 |
| DragInput | Game：鼠标与触摸共用相对位移映射，过滤 UI 起始触点 |

程序集：Fishing.Game → Fishing.Core；Fishing.Core.Tests 仅引用 Core 与测试框架。Core asmdef `noEngineReferences=true`，已有程序集依赖测试。

## 固定步契约

**已由 GameBootstrap 装配，并通过保存场景的 PlayMode 测试验证。**

1. 调用 `BeginStep(dt)`，推进游戏时间、状态与深度，取得 StepSequence。
2. `ContactDetector.Begin()` 清空本步回调缓存；Game 依当前深度与输入移动鱼钩、推进鱼。
3. 同步 Transform，并由 Game 在明确的固定步中运行物理模拟。若采用手动 `Physics2D.Simulate`，需保存并在退出时恢复全局 simulationMode。
4. 本步物理回调完成后调用 `Finish(run, step, previousPosition)`，合并回调、圆形扫掠和末端重叠。
5. 仅一次 `ResolveContacts(batch)`，然后应用规则结果到 View。不得在模拟之前提前提交批次。

ContactBatch 头部保存 RunId / Step，候选继承其局与物理步身份。候选为 HookId、FishId、SpeciesId、DistanceSquared。距离是本步钩路径到鱼头中心的最近距离平方，Game 计算后以纯数值传入。

当前 ContactBatch 复制候选与末端接触列表形成只读快照；尚未验证分配量或性能。性能收敛时可改为同步借用缓冲区，但不能让规则保留会被下步覆盖的数据。

## 接触生命周期与裁决

- ContactKey 是 (HookId, FishId)。EndContacts 只包括步末仍重叠的鱼；短暂扫掠可只有候选、没有 EndContact。
- 同步重复候选按接触对合并，保留更小距离，按距离 / FishId / HookId 排序。
- 保护区下潜只处理排序后的首个有效新接触。其余进入接触、以及无敌期内开始的接触均保留末端记录，无敌到期不补判。
- 完全分离后，下步末端接触记录不再含该接触对，再进入可重新判定。
- 进入底部区或转为上收清空保护无敌与阶段接触记录，使仍重叠的鱼在新阶段参与捕获；捕获去重集合不清空。
- 到最大深度时先按底部规则裁决，再进入 BottomHold。最后一段上收扫掠也先裁决，再进入 Surfacing。
- 暂停拒绝批次。局 ID 不符、过期/未来步、重复步批次均丢弃。重开必须创建新 controller / RunModel，并清空 Game 检测缓存。

## 事件与顺序

- `RunModel.OnDepthChanged(float)`：深度及最大实际到达深度更新后通知。
- `RunModel.OnProtectionChanged(int)`：扣保护并设置无敌截止时间后通知。
- `FishingFlowController.OnFishCaptured(CaptureRecord)`：记录和全局去重集合已提交；View 可关闭鱼头判定、移出活跃集合并挂鱼。
- `RunModel.OnBecameFull()`：同一捕获的 OnFishCaptured 之后通知，整局最多一次。IsFull 始终从主钩记录派生。
- `FishingFlowController.OnStateChanged(RunState)`：进入新状态、重置该状态计时后通知。无保护提前上收时顺序为捕获 →（若满）满载 → 状态变更。
- `FishingFlowController.OnRunFinished(RunModel)`：进入 Result 的状态事件之后通知一次；两种货币和最大到达深度已可读取。
- 暂停是 IsPaused 覆盖层，不切换流程状态；BeginStep 不推进时间与序号。

## 第三阶段接入位置

当前仅接受 Main 的 HookId=0；正数 HookId 暂无资格并被忽略，不能把当前行为当作分支优先分配已完成。

分支阶段应在 ResolveContacts 的主钩分配前增加完整批次分配：先对已展开、有余量分支的小鱼候选稳定排序并占用临时容量，再将未分配的主钩候选分给主钩，最后统一提交。所有钩仍共用 RunModel 的记录与捕获去重集合。暂停/重开/迟到批次策略保持一致。

来源聚合已有合成 Main/Branch 记录测试；这只证明数据结构与统计契约，不代表分支能力或分支运行验收已完成。声呐与能力事件载荷待第三阶段实施时确定，不预建空类。

## 已装配的 Unity 生命周期（2026-10-20）

- Menu 与 Main 均保存 GameBootstrap 的配置、4 种鱼、Fish/Hook 预制体及 Camera 引用；Main 可直接 Play。Menu 开始按钮加载 Main，结算/暂停退出加载 Menu。
- GameBootstrap.Start 保存原 Physics2D.simulationMode 并启用 Script；FixedUpdate 使用 GameClock 的显式时间，顺序与上述固定步契约一致。OnDestroy 解除订阅并恢复原模拟设置。
- GameClock 不改变全局 Time.timeScale。暂停冻结规则、鱼、钩与摄像机，UGUI 按钮仍可交互。倒计时不接收拖动输入。
- BeginRun 先解除旧订阅、回收全部本局鱼并清空检测缓存，再创建新 flow / RunModel、重新绑定事件并生成鱼。
- 鱼的活跃/缓存视图均保留在场景所有者子树；捕获后移到同一所有者下的 Hook。场景销毁时交由 Unity 销毁子树，不在 OnDestroy 中回收已经部分销毁的 Collider。此修复由场景切换与 20 局重开测试覆盖。
- GameUi 创建一套 UGUI 元素及按钮回调；HUD 数量来自模型事件，结算来自 Settlement。英文占位帮助面板不等同于需求的三页教学。
- CameraRig 按宽度匹配，暂停不推进；第二阶段已接入方向缓动与统一水面构图，完整界面比例验收仍待后续步骤。
- Sprite 资源留在 FishDefinition；规则快照不包含 Sprite。使用完整鱼图时隐藏几何尾巴/眼睛，头部 Collider 独立。
- FishingSceneBuilder 通过 Unity API 只创建缺失资产，不重写已有配置、场景或预制体；重运行用于补缺，已有资产的后续修改需单独编辑并验证序列化引用。
- FishingPreview 只修改 Editor 的 Game View 分辨率；Player 使用独立的竖屏设置。
- 首个 APK 由 FishingAndroidBuild 构建 Menu/Main，IL2CPP ARM64 Development，API 23/34，版本 0.1.0；已于 2026-10-20 成功构建，报告 android-build-20261020.json；APK 实际大小约 29.4 MiB，v1/v2 签名有效，未做真机安装验收。

## 后续仍待实现

三页教学/首次流程、七类鱼深度分布、正式 HUD 与结算卡片、全比例适配、音频与出水鱼演出、声呐/分散鱼钩、真机验收与性能测量。

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
