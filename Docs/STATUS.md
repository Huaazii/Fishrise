# 开发状态

更新：2026-10-21（Asia/Kuala_Lumpur）；功能验证截至 2026-10-21。需求基线：需求文档.md v1.1。

## 阶段映射

| 实施对话 | 第 9 章里程碑 | 状态 |
| --- | --- | --- |
| 01 工程准备、最小闭环与核心规则 | ①最小闭环＋②规则完善 | 最小闭环与首次 APK 构建已完成，保留真机/输入手感待验收项 |
| 02 鱼群、摄像机与界面 | ③内容与界面 | Editor 检查点完成：核心美术、场景/镜头、七鱼种、中文界面及三比例回归通过；设备验收留交付阶段 |
| 03 声呐与分散鱼钩 | ④创新玩法 | Editor 检查点完成：两能力、统一多钩裁决、分支 HUD 与真实来源结算；真机/操作平衡待交付阶段 |
| 04 表现打磨与交付 | ⑤表现打磨＋⑥交付验证 | Editor 功能检查点完成；Legacy 后端复验和 0.2.0 APK 已通过，设备/手感/性能待验收 |

## 当前检查点

- 2026-10-18：四份交接文档、Core/Game 基础、23 个纯规则测试。
- 2026-10-20：Menu/Main 保存场景、4 种鱼配置、鱼/鱼钩预制体、场景装配、固定步物理批次、最小 UGUI、基础跟随摄像机、暂停/结算/重开。
- 首阶段蓝纹鱼用内置 image_gen 生成；第二阶段使用用户已检查的 Arts 原图，没有补充生成插画。核心、环境、七鱼种与界面共复用 42/45 张原图；另有两张程序渐变底色与随工程打包的中文字体。详细来源在 ArtSources.md。
- 第二阶段首个检查点：四种鱼的尺寸、朝向、鱼头与鱼嘴挂点已校准；B06 裁为 183×184；主钩使用独立绿/黄/红灯、顶部鱼线挂点与 10 个挂鱼点。原鱼种 ID、收益、游速、数量和核心规则保持不变。
- 第二个检查点：Main/Menu 共用冰面、冰壁、渔夫两姿态、山云与道具；连续水色、背景鱼影视差、竿尖/冰洞鱼线、0.5 秒方向缓动与统一水面构图已接入。可视宽度为 12，可操作边界仍为 ±4.5。
- 2026-10-21 鱼群内容检查点：补齐黄蝴蝶鱼、鳗鱼和小银鱼，四段深度配置为 12/13/14/15 条（合计 54）；固定种子、鱼群游速与受限泳道保留可通行路线。七鱼种判定/挂点、分布、正常 100 米路线与原有玩法回归通过，正式中文界面留下一检查点。
- 2026-10-21 中文界面检查点：中文 HUD、起始页、三页教学、首次开始记忆、暂停与可滚动结算卡片完成；450×800/975/1050 真实 Game View 与模拟安全区回归通过。Core 23/23、PlayMode 21/21，正常游戏帧完成 100 米满载单局并重开。阶段 02 的 Editor 实施收尾，详见 Verification/ui-checkpoint-20261021.md。
- 2026-10-21 第三阶段：声呐、分散鱼钩、独立分支容量与 HUD、两按钮和统一批次分配已接入。Core 40/40、PlayMode 32/32；包含真实物理的主钩满载后额外捕获 6 条、原固定种子小鱼群高速捕获、跨端点扫掠和混合来源结算。正常帧实测、画面与限制见 Verification/abilities-checkpoint-20261021.md。
- 首次 APK 因输入后端切换后 Editor/Player 的 URP 类型布局不一致失败；保存并重启指定 Editor 后重试成功。构建耗时约 294 秒，0 errors / 0 warnings。失败与成功报告分别为 Verification/android-build-20261020-first-run.json、android-build-20261020.json。

## 需求覆盖

| 需求 | 本阶段实现和证据 | 尚待完成 |
| --- | --- | --- |
| FR-01 | 鼠标/触摸共用相对拖动映射，过滤 UI 起始触点、横移边界和速度上限 | 实体鼠标手感、Android 触摸一致性检查 |
| FR-02 | 真实场景下潜、底部停留、上收；Core 与 Game 测试通过 | 真机操作体验 |
| FR-03 | 保护、持续接触、分离再进入、无敌和底部失效；真实重叠与 Core 测试通过 | 气泡破裂动画/音效 |
| FR-04 | 全部钩的回调/扫掠/末端重叠统一批次；分支优先、容量回退、稳定排序与统一提交；七鱼种判定和来源钩嘴部挂点、主/分支跨端点扫掠通过 | 实体操作与设备验收 |
| FR-05 | 主容量 10、满载事件一次、5 倍上收/红灯；分支两钩各 3 条，主钩满载后仍可捕获；单鱼全钩唯一，真实物理 10＋6 不超限 | 设备与操作平衡检查 |
| FR-06 | 七鱼种、四段 54 条、固定种子、Queue 池与背景鱼影视差；初始普通泳道的完整泳动范围保留通道，实际路线到达 100 米；受惊小鱼保留位移后可改变原通道；20 局复用通过 | 实体体验、设备与性能；受惊后不承诺保持原泳道通道；池复用不代替性能测量 |
| FR-07 | 冰面与水下场景；宽度匹配、上下行 y=0.8/0.2、0.5 秒转向缓动、统一出水构图与重开重置；三比例真实 Game View 的界面与玩法回归通过 | 设备屏幕与实体操作验收 |
| FR-08 | 中文保护/主钩/深度/分支四行、数值弹跳与颜色、两能力次数/冷却/可用状态；模型事件驱动，暂停冻结；三比例安全区和上收钩前的按钮避让通过 | 真机可读性与操作检查 |
| FR-09 | 独立三色灯、主/分支来源挂点、嘴部固定的轻微摆动与捕获缩放；暂停/重开回归通过 | 设备观感 |
| FR-10 | 冰面渔夫场景的 3-2-1 倒计时；三页中文教学可翻页/关闭；首次开始先教学再倒计时，手动教学关闭回菜单；首次记录跨场景保留 | 设备操作验收；教学动画留表现打磨 |
| FR-11 | 2.3 秒出水演出、逐条金额、分支延后 0.3 秒；开始/中途/末尾跳过与自然结束收益一致，16 条完整播放通过 | 设备操作与观感 |
| FR-12 | 每鱼种合并/排序、图标/星级/快照单价/数量、来源明细/角标、滚动与两种总额；真实主/分支捕获到结算通过，不再仅依赖合成记录 | 设备检查 |
| FR-13 | 游戏时钟冻结物理、鱼/钩/能力/挂鱼/捕获反馈，音频显式 Pause/UnPause；设置仍可交互 | 手机后台恢复/音频检查 |
| FR-14 | 新局重建、回收鱼、重置全部钩/镜头/HUD/次数与受惊状态；两分支复用、20 局无旧挂鱼/重复捕获事件通过 | 真机长期稳定性 |
| FR-15 | 8 种事件短音、菜单/暂停共用音效和震动开关；静音不播放、保护/捕获事件去重；Android 20 ms 震动请求与权限已接入 | 手机听感和震动实测 |
| FR-16 | 未接入完整 Development 调试面板 | 固定种子界面、判定区、倍率、满载/深度跳转与状态显示 |
| FR-17 | 独立 Menu 场景与正式中文起始页；开始/教学/退出回调；首次教学记忆、返回菜单再开始无旧局残留 | Android 上退出应用与实体触摸验收 |
| FR-18 | 每局 2 次/4 秒冷却，非底部下潜可用，4 米小鱼查询、0.4 秒声波、2.5 秒受惊/3 倍逃离；判定豁免、大鱼照常、无跳位恢复与重开通过 | 设备/输入验收 |
| FR-19 | 每局 1 次，上收展开两钩，扇形/阻尼跟随、独立 3＋3 容量、分支优先、满载扫掠、来源结算、20 局复用；可选大鱼断钩默认关且已单独验证 | 设备/输入验收 |
| NFR-05 / T18 | Core 无 Unity/Game 引用，纯数据规则测试通过 | 后续迭代保持 |

上述证据限于对应层级；第四阶段 Editor 功能已有检查记录，设备验收、FR-16 调试界面与更完整的氛围仍待完成。

## 主要文件

- AGENTS.md、Docs/需求文档.md、Architecture.md、ArtSources.md、STATUS.md。
- Assets/_Game/Scripts/Core：RunModel、规则快照、捕获来源、状态机、接触批次与统一裁决。
- Assets/_Game/Scripts/Game：GameBootstrap、GameClock、GameUi、CameraRig、FishSpawner/FishActor、HookMotor、ContactDetector、DragInput 和配置类型。
- Assets/_Game/Scripts/Editor：FishingSceneBuilder（只创建缺失资产）、FishingPreview（真正竖屏 Game View）、FishingAndroidBuild。
- 核心美术检查点新增 FishingCoreArtImporter，Game 的 FishDefinition/FishActor/HookMotor/GameBootstrap 接入美术与挂点；Art/Fish、Art/Hook 保存 7 个裁边副本。既有 4 份鱼配置和 Hook.prefab 已更新，Fish.prefab 保持原绑定。
- 场景检查点新增 FishingSceneView、FishingEnvironmentBuilder、Environment.prefab 与 EnvironmentIntegrationTests；更新 CameraRig、HookMotor、GameBootstrap 及既有 Main/Menu 场景引用，旧占位环境停用但保留。Hook.prefab 仅追加鱼线宽度调整。
- 鱼群检查点新增 FishingPopulationBuilder、PopulationIntegrationTests、Fish_yellow/eel/silver 配置与 3 张鱼图副本；更新 FishDefinition、FishingConfig、FishSpawner、FishActor、GameBootstrap、既有 GameIntegrationTests 与两个场景的鱼种数组。FishingCoreArtImporter 补充缩图后 PPU 校正；既有场景和资产身份保留。
- 界面检查点新增 GameUiAssets、UiText、UiWidgets、TutorialView、ResultView、FishingUiBuilder 与 UiIntegrationTests；重写 GameUi 表现，GameBootstrap 接入共享 UI 资产，FishingPreview 支持真实三比例预览。Art/UI 保存 15 张现有素材副本，Art/Fonts 与 StreamingAssets/Licenses 保存字体及许可。
- 第三阶段新增 Core 的 AbilityRules、AbilityController/IAbility、SonarAbility、BranchHookController/Model，Game 的 AbilityDefinition、SonarConfig、BranchHookConfig、AbilityRig、AbilityHud 与 BranchHookView，以及 FishingAbilityBuilder、AbilityRulesTests、SonarIntegrationTests、BranchIntegrationTests。ContactDetector 复用于不同钩 ID，GameBootstrap 仍只提交一个统一物理批次。原图和玩法/美术资产身份保留。
- Assets/_Game/Scenes/Menu.unity、Main.unity；Configs/FishingConfig 与 Fish_*.asset；Prefabs/Fish.prefab、Hook.prefab；Art/FishBlue.png 和简单几何占位图。
- Tests/EditMode/CoreRulesTests、Tests/PlayMode/GameIntegrationTests。

## 实际验证

- Core EditMode：23/23 通过，Verification/core-editmode-20261020.xml；重启与后端生效后再次 23/23 通过，core-editmode-after-restart-20261020.json。
- Game PlayMode：7/7 通过，Verification/game-playmode-20261020.xml；重启后再次 7/7 通过，game-playmode-after-restart-20261020.json。使用已保存场景、资产、实际 Physics2D 和按钮回调；固定步由测试显式推进。
- 覆盖正常触底至结算、无保护高速扫掠提前收钩、主钩满载停止捕获及速度、三个状态暂停/继续、持续重叠/分离再进入、菜单开始/退出，以及场景连续重开 20 局。
- 重开检查：池分配数量不增长、场景鱼对象数量不增长、捕获表现事件每次只响应一次、每次复用有新鱼实例 ID、无旧挂鱼。此结果不是内存/帧率测量。
- 首轮 7 项中有 2 项在场景销毁时报 MissingReferenceException；已修复：活跃鱼归场景所有者管理，OnDestroy 不再回收正在销毁的组件。首轮失败报告保留为 game-playmode-20261020-first-run.xml；修复后全通过。
- 窗口恢复可见后，真实运行快照由 Ascending（depth 58.55、2 条）推进到 Result（depth 0、6 条），Time.frameCount 12186 → 31329；确认不是冻结截图。
- Game View 曾是横屏，450×800 截图发生拉伸；已设置真正 450×800。不能以最初拉伸图作为适配通过证据。

### 第二阶段核心美术检查点

- Rider：6 个改动 C# 文件检查无错误，Verification/rider-core-art-20261020.json。连接 64482 的项目定向 MCP 请求可用；通用工具包装器的目标识别错误没有当作工程诊断结果。
- Unity：相关修改已编译成功；最终编译与控制台记录见 Verification/core-art-compilation-20261020.json、core-art-console-20261020.json。导入器曾因对 Unity 组件使用空合并而误判缺失组件，已改为 Unity 的显式空判断，重跑导入成功。
- Core EditMode **23/23** 通过，core-art-editmode-20261020.json；Game PlayMode **10/10** 通过，core-art-playmode-20261020.json。实际保存场景/预制体与 Physics2D 参与，固定步由测试显式推进。
- 原有 7 项回归继续通过：正常触底至结算、保护连续接触/分离再进入、无保护高速扫掠提前上收、满载、三个状态暂停、20 局重开、菜单导航。新增 3 项验证四鱼种双向鱼头/鱼尾接触及边界翻转、混合捕获的嘴部挂点与复用旋转、裁边气泡运动/暂停/底部隐藏。
- 真实逐帧运行（未由测试显式推进）从 Countdown → Diving → Ascending → Surfacing → Result，最深 **75.00006 米**、捕获 **4 条**、金币 **45**、贝壳 **40**。runId、帧号、游戏时间与阶段采样见 core-art-live-run-20261020.json；没有用此局替代测试中的正常 100 米触底路径。
- 此局结算后触发实际 Dive again 按钮回调：新局 ID、Countdown、保护 2、捕获 0、深度 0、挂鱼 0、活跃鱼 54，见 core-art-live-restart-20261020.json。此操作未用于声称实体输入手感通过。
- 已查看 450×800 实际帧截图：core-art-diving-20261020.png、core-art-ascent-20261020.png、core-art-result-20261020.png，包含 HUD、保护泡/灯色、挂鱼与结算。此结果只覆盖当前竖屏比例，不等同于三种比例验收。
- 既有配置、预制体、场景 .meta 的身份核对均相同，见 core-art-guids-20261020.json。纹理尺寸与配置绑定分别见 core-art-textures-20261020.json、core-art-bindings-20261020.json。
- 本轮没有改输入或做实体鼠标/手机触摸验收，没有重建 APK，没有测帧率、内存或 GC。阶段 02 尚未整体完成。
- 最终 Unity 编译错误/警告、控制台错误/警告均为 0；交接状态为 Main 场景、dirty=false、playing=false，见 core-art-final-scene-20261020.json。

### 第二阶段场景与镜头检查点

- Rider 7 文件诊断无错误，Verification/environment-rider-20261020.json。Unity 最终编译与控制台记录为 environment-compilation-20261020.json、environment-console-20261020.json。
- Core EditMode **23/23**，environment-editmode-20261020.json；Game PlayMode **12/12**，environment-playmode-20261020.json。此前 10 项继续通过，新增场景/镜头两项覆盖冰壁边界重叠与深度覆盖、单环境实例、共享脚底 pivot、鱼竿/洞口/钩顶连线、450×800/975/1050 镜头宽度、下潜/上收视口位置、0.5 秒缓动、暂停与无跳变出水。
- 初次带命名空间筛选的调用未执行任何测试，报告 environment-playmode-no-tests-20261020.json，未记为通过。随后完整 12 项首轮为 **11/12**：重复装配产生两个 Environment 实例，失败报告 environment-playmode-first-run-20261020.json。已改为按预制体来源识别/去重；重复执行前后 Main/Menu 均各 1 实例，见 environment-assembly-before/after-repeat-20261020.json。
- 450×800 实际画面已检查闲置/拉竿姿态、人物/道具落点、冰面洞口、两侧冰壁、水色连续性及背景鱼影。扩大边缘视野后原鱼线末端不足 1 像素；已改为 0.045→0.04 世界单位，增加最小像素宽检查，并在最终 12 项回归中通过。旧画面保留为 environment-rope-before-20261020.png。
- 真实逐帧完整流程与截图见 environment-live-run-20261020.json、environment-surface/diving/ascent/surfacing/result-20261020.png；重开按钮回调见 environment-live-restart-20261020.json。它们与显式步进的 PlayMode 检查分别记录，不代表实体输入或手机验收。
- 最终真实运行帧号 51024→75562，最深 75.00006 米、捕获 4 条、金币 45/贝壳 40，到达 Result；下潜/上收阶段的实际钩视口高度稳定为约 0.8/0.2，出水恢复约 0.7。鱼线修复后的同构图空水区 231 行均连续可见，之前为 0 行，见 environment-rope-visibility-20261020.json；此结果仅用于说明该处渲染修复。
- 本轮涉及的 20 张源 PNG 哈希未变；9 份既有配置/预制体/场景 .meta 未变，见 environment-preservation-20261020.json。19 张新增工程纹理的尺寸见 environment-textures-20261020.json；既有文件改动见 environment-existing-file-changes-20261020.json。
- 最终编译/控制台均为 0 错误、0 警告；Main 场景 dirty=false、playing=false，见 environment-final-scene-20261020.json。
- 场景与镜头检查点完成，阶段 02 未整体完成。当前仍为 4 种可捕获鱼、固定种子基础分布与英文占位 UI；相机三比例的数值验证不等于三比例完整界面验收。未重建 APK、未做设备/实体输入/性能验收。

### 第二阶段七鱼种与分段鱼群检查点

- Rider **9 个改动 C# 文件**诊断无错误，Verification/population-rider-20261021.json。Unity 最终编译/控制台状态见 population-compilation-20261021.json、population-console-20261021.json。
- 游戏 Core EditMode **23/23**，PlayMode **15/15**，报告 population-editmode-20261021.json、population-playmode-20261021.json。原有 12 项继续通过，鱼头与混合捕获检查已扩大到七鱼种；混合捕获的 7 条记录正确汇总为 163 金币、118 贝壳。
- 新增 3 项验证七鱼种数量/稀有度/价值/深度/游速、四段 12/13/14/15 条、固定种子与新实例 ID、60 秒显式鱼群运动仍有通道，以及实际 Physics2D 下不消耗下潜保护的 100 米路径并到达结算。连续 20 局池复用、保护、满载、暂停和导航回归保留。
- 首轮 PlayMode **14/15**：长条鳗鱼纹理缩小到 2048 上限后，宽高取整比例不同，实际 Sprite 宽 1.251106 超过原检查容差。已按实际导入尺寸校正 PPU，保持 2048 上限与 1.25 基准宽；修复后 15/15。首轮失败报告保留为 population-playmode-first-run-20261021.json。
- 未筛选的额外 EditMode 执行包含桥接包：共 77 项、76 通过、1 失败。失败是包内 UnityCliBridge.Tests.InputSystemHandlerTests.SimulateGamepadInput_ButtonHoldSeconds_Releases，见 population-editmode-with-bridge-20261021.json；未修改包代码，也未把这轮记为全通过。游戏 Core 定向检查为上述 23/23。
- 真实逐帧运行以脚本提供横向目标，仍由正常 FixedUpdate/Physics2D 推进，没有显式调用 AdvanceGameplay：帧号 **767→9912**，到达 **100 米**并结算，满载 **10 条**、金币 **228**、贝壳 **173**，包含鳗鱼和两条黄蝴蝶鱼。记录见 population-live-run-20261021.json，450×800 截图见 population-diving/deep/ascent/result-20261021.png。此结果不代表人工鼠标/手机输入验收。
- 结算界面的实际 Dive again 按钮回调产生新局 ID，Countdown、保护 2、深度/捕获/挂鱼均 0，54 条鱼且包含七类，环境实例 1；见 population-live-restart-20261021.json。
- 重复执行本轮装配工具后，34 份配置/鱼图/场景及身份文件的哈希全部相同，见 population-repeat-import-20261021.json。3 张源 PNG、既有 .meta 与本轮开始前已有的摄像机组件改动保留，见 population-preservation-20261021.json。
- 本检查点完成，阶段 02 仍待正式中文界面和完整适配；不重建 APK，不做设备、实体输入或性能达标声明。最终交接保持 Main 场景、未进入 Play、无未保存场景修改，见 population-final-scene-20261021.json。

### 第二阶段中文界面与三比例检查点

- 中文 HUD、起始页、三页教学、暂停和结算卡片完成；首次点开始先教学，读完或关闭后进入倒计时，手动教学关闭回菜单。只保存首次教学标记，不保存关卡收益。测试后已恢复原标记，交接时仍可看到首次教学。
- Rider **11 文件**无错误，Verification/ui-rider-20261021.json；Core EditMode **23/23**，ui-editmode-20261021.json；游戏 PlayMode **21/21**，ui-playmode-20261021.json。新增 6 项覆盖教学导航/记忆、记录快照、混合来源合并/角标、七鱼种滚动与空收获、HUD 弹跳暂停、三比例布局和玩法回归。
- 真实 Game View 分辨率为 450×800、450×975、450×1050；每种比例均检查菜单、三页教学、HUD、暂停/结算与模拟安全区，并回归保护、满载 10 条/5 倍上收、暂停及重开。安全区为编辑器注入的矩形，不是手机刘海/手势区实测。
- 首轮 21/21 虽通过，截图发现结算鱼名和数量未生成可见字符：文字框高 38，字体实际需要 38.4/40。现增至 48，收紧高度容差并检查真实生成字符，最终重新 21/21。旧报告/截图与前后文字数据保留为 ui-playmode-before-text-fix、ui-result-before-labels、ui-card-text-before/after-20261021。
- 正常游戏帧单局由脚本只提供横向目标，没有显式推进规则：帧号 **1337→10306**，最深 **100 米**、满载 **10 条**、金币 **240**/贝壳 **180**，见 ui-live-run-20261021.json。结算按钮重开后新局 ID、Countdown、保护 2、深度/捕获/挂鱼 0、54 条鱼/7 类、环境与 Canvas 各 1，见 ui-live-restart-20261021.json。
- 已检查实际菜单、三页教学、暂停、下潜/深水/上收/结算截图；另有正常帧暂停前后采样确认鱼/钩/游戏时钟冻结。七鱼种滚动与“含分支”画面使用 7 主钩＋1 蓝纹鱼分支的受控记录，8 条、173 金币/128 贝壳；真实已完成单局的 10 条记录未变。这项只验证结算界面，不代表分散鱼钩已实现。
- 15 张既有 UI/教学图已导入；捕获图标复用 B04，随工程引用 Noto Sans SC 字体并附 OFL。45 张源图哈希、既有 .meta 与原有摄像机改动均保留；重复导入的 42 份文件哈希完全一致，见 ui-preservation-20261021.json、ui-repeat-import-20261021.json。
- 编译后桥接曾因本机发现文件替换失败而拒绝连接；归档该进程的陈旧发现文件并通过 Unity 菜单重启桥接后恢复，未修改桥接包或权限设置。最终编译和控制台均为 **0 错误、0 警告**；Menu 场景、450×800、dirty=false、playing=false，见 ui-compilation、ui-console、ui-final-scene-20261021.json。
- 本检查点与阶段 02 的 Editor 实施完成。汇总、证据索引和限制见 [ui-checkpoint-20261021.md](Verification/ui-checkpoint-20261021.md)。本轮没有新 APK、实体鼠标/手机触摸或性能测量；声呐、分散鱼钩与分支 HUD 保留第三阶段。

### 第三阶段：声呐与分散鱼钩

- Core 新增两个 IAbility 策略和每局规则快照；Game 保留输入、图标、物理查询和视图。声呐仅查询小鱼，Core 再按快照、ID 和距离裁决；受惊头部同时在 Game 与 Core 豁免。分支候选先分配、再回退主钩，整批预留容量后统一提交唯一捕获记录。
- 声呐每局 2 次/冷却 4 秒，4 米释放瞬间查询、0.4 秒声波展开、小鱼 2.5 秒/3 倍逃离；非底部区下潜才可用。恢复后保留当前位移，不瞬移回原泳道；原通道可能因此变化。大鱼仍照常消耗保护或触发提前上收。
- 分支每局 1 次、仅上收展开，复用 B04 为两侧小钩；各 3 条、独立于主容量 10。两钩使用专用图层/查询和圆形扫掠，扇形展开后阻尼跟随并限制在水域；按来源钩挂鱼。可选大鱼退回默认关闭，开启后的新局查询切换、退回与保留旧挂鱼也已验证。
- Rider **25 文件**无错误，Verification/abilities-rider-20261021.json；游戏 Core **40/40**（原 23＋新 17），abilities-editmode-20261021.json；Game PlayMode **32/32**（原 21＋新 11），abilities-playmode-20261021.json。定向游戏程序集，不把此前包自带手柄释放失败算作通过。
- 显式步进的真实 Physics2D 检查覆盖五条声呐与六条分支验收：包括先主后分支、反序与混序一致，分支已满时回退另一分支/主钩；主钩 10/10 后再捕获 6 条；分支扫掠命中但前后端点均不重叠；真实混合来源卡片与两钩连续 20 局复用。另用原固定种子鱼群验证满载高速分支捕获：只调整 10 条大鱼建立满载前置，目标小鱼的泳道/游速与捕获前位置不作设置。
- 正常游戏帧固定种子单局也达到 **100 米、主钩 10＋分支 3＋3＝16 条，250 金币/190 贝壳**；使用两次声呐、一次展开，见 abilities-live-run-20261021.json。脚本只提供横向目标和实际按钮回调，未调用 AdvanceGameplay。声呐/受惊期间正常帧暂停约 1.25 秒，时钟、冷却、位置和声波全部冻结，见 abilities-live-pause-20261021.json。
- 七鱼种真实结算与来源细目保存于 abilities-live-settlement-20261021.json；9:21 滚动截图显示小银鱼主钩 2＋分支 3，合并为一张卡。结算按钮重开后 54 条、零捕获/受惊/挂鱼、两钩隐藏复用、声呐 2 次/分支 1 次，见 abilities-live-restart-20261021.json。
- 初版按钮在上收钩位置附近，会遮挡钩与挂鱼。已移到屏幕 20% 高度之上、左右两侧，并给三比例检查增加纵向避让断言；31/31 后再次完成正常帧 10＋6 单局及画面复核，补充固定种子高速检查后最终 32/32。旧截图与报告保留，详见 abilities-checkpoint-20261021.md。
- 45 张源图、40 份既有 UI 资产及原场景修改保留；Main/Menu 仅新增两能力引用，Physics2D 设置只更改分支碰撞矩阵。重复装配的 15 份配置/场景/设置哈希相同，见 abilities-preservation-20261021.json、abilities-repeat-assembly-20261021.json。
- 最终编译/控制台均 **0 错误、0 警告**；Menu、450×800、dirty=false、playing=false，首次教学标记恢复，见 abilities-compilation、abilities-console、abilities-final-scene-20261021.json。本轮没有新 APK、实体输入或设备/性能验收。

## 环境、已知限制与未验证项

- Unity 固定 2022.3.52f1 (1120fcb54228)，URP 2D；使用 unity-cli / bridge 0.18.1，明确传工程路径。原 SampleScene 保留。
- Git 纳入源工程、已导入素材与验证记录；图片等二进制资源沿用 Git LFS。Unity 缓存、构建产物及 .codex/.idea/.unity 本机配置保留在本地并忽略，项目入口与已完成功能见 README.md。
- 当前 Legacy Input Manager 后端用于鼠标/触摸和 StandaloneInputModule；避免 Android 使用 Both 后端。
- 首阶段 Rider 曾连接失败；核心美术检查点完成 6 文件诊断，场景检查点完成 7 文件诊断。旧 NuGet 缓存目录与 Roslyn 生成器环境警告未通过修改全局配置处理。
- 核心/环境美术、七鱼种、中文界面/教学、两能力、出水演出与最小事件反馈已接入；设备验收与最终性能证据仍待完成。
- 额外执行的桥接包手柄释放测试有一项失败，已保留报告；本轮未修改桥接包，也不声称全部包测试通过。
- 本阶段未测帧率、内存或 GC；ContactBatch 每步复制快照，HUD 文本更新也需后续测量收敛。不声称 NFR-01/03 性能达标。
- APK 构建成功、安装成功、真机运行通过必须分别记录。当前没有设备验收证据。
- 0.1.0 APK 是首阶段构建。第四阶段 0.2.0 已在 Legacy 后端重启后重建成功，0 errors/0 warnings；首包的 Both 警告与处理过程另存，设备运行待验收。

## 下一步

- 接手文档：[项目框架与优化指南](项目框架与优化指南.md)。已按当前代码、测试报告及 APK 哈希核对开发版 MVP；后续优先人工输入、设备体验和性能验收，再调整细节。

1. 连接手机，安装当前 0.2.0 APK 并检查持续左右拖动与完整单局。
2. 设备回归保护、满载、两能力、完整播放/跳过、暂停/重开/后台恢复，以及声音和震动。
3. 在设备测量帧率、内存和 GC；再按实测收敛性能及补齐调试工具、可选氛围。

## APK 与最终环境

- 首个 APK 已成功：Builds/Android/CasualFishing-0.1.0-arm64.apk，30,872,697 bytes（约 29.4 MiB），Development / IL2CPP / ARM64，API 23/34、竖屏。签名工具退出码 0，v1/v2 签名有效；工具另有两项 META-INF 条目提示，原文保留在 apk-artifact-20261020.json。包名 com.huaziii.casualfishing、版本 0.1.0。该记录仅对应首阶段，安装与手机运行未验证。
- SHA256：73c231ddc11a0406fe9bafac29831b1496dc7e0a9bc61a1689c1720cbec58029。
- 构建报告 totalSize 包含构建过程产物，并非 APK 文件大小；交付体积以 APK 实际文件的 30,872,697 bytes 为准。
- 构建完成后 Editor 切换到 Android 并出现新实例 PID 147696。用户确认 Main 场景可操作；带明确 host/port 的 get_editor_info 已核实工程路径与版本。CLI test 的本地进程识别仍失败，已改用同一桥接的 run_tests / get_test_status，未改用户工具配置。重启后 Core 23/23、Game 7/7 再次全部通过，证据为 core-editmode-after-restart-20261020.json 与 game-playmode-after-restart-20261020.json。

- 首阶段重启后的 Unity 编译/控制台均为 0 错误/警告（2026-10-20T07:23:47.8258089Z 后检查）。后续美术与中文界面已完成上述 Editor 检查，手动输入与当前版本设备验收仍待完成。

- 首阶段自动截取 UGUI 曾因 Game View 焦点条件失败。第二阶段已通过实际 ScreenCapture 获取真实竖屏运行帧；首阶段暂停图仍保留。最新交接退出 Play，留在 Menu，保留首次教学状态。


### 第四阶段：出水演出与最小反馈

- FR-09/11/15：挂鱼绕嘴摆动、捕获缩放与飘字、保护气泡、满载速度线、逐条出水鱼/金额、0.3 秒分支延迟、跳过、卡片交错展开，以及 8 种程序短音和共享设置已接入。当前单局 2.3 秒演出，不增加或重新计算奖励。
- Core 新增 5 项，45/45；Game 新增 6 项，38/38。包括主钩 10＋分支 6、不同时间跳过/重复命令、唯一结算事件、自然结束/空收获、挂点不漂移、暂停冻结、静音/震动开关、20 次重开复用、三比例安全区。同名最终报告对应输入后端修正并重启后的结果。
- 首轮规则/场景已通过，但画面检查发现出水 HUD 与金额拥挤，随后隐藏玩法 HUD、缩短单鱼飞行并分散金额底牌；调整后再次 37/37。前版报告与画面保留为 presentation-playmode-before-layout、presentation-surface-before-layout-20261021。
- 最终 Legacy 后端正常帧固定种子单局 ID 70195509-e036-4cc3-bf9a-37901350f2ff：100 米、主钩 10＋分支 3＋3、250 金币/190 贝壳；16 次出水、演出计时 2.3199985 秒、结束活动跳跃数 0、捕获音事件 16/结算音事件 1。脚本只给横向目标和按钮回调，仍由正常 FixedUpdate/Physics2D 推进，见 presentation-live-run/live-settlement-20261021.json。
- 原生 Unity 窗口自动点击验证再来一局、暂停、设置和静音；一次注入拖动没有产生横移，未记为通过，原因未定位，真人持续拖动/手感仍待检查。相关记录见 presentation-native-input-20261021.json；不把自动注入当真人输入或手机验收。
- 本轮未补图；45 源 PNG、既有 .meta、前阶段 14 份配置/场景/设置哈希保留，见 presentation-preservation-20261021.json。Main/Menu 没有本轮序列化改动；全局配置仅演出等待从 0.6 改为 2.3 秒。
- 0.2.0 首次 APK 成功，但 BuildReport 有 1 条 Both 输入后端警告。核实 Git HEAD 本来为 activeInputHandler=2，与旧文档标记不符；已只改为 0，使用实际设置对象和原生重启，并在新进程验证；构建工具增加前置检查。首包与警告证据另存 before-input-fix，不冒充零警告最终包。
- 当前未检测到 Android 设备，未安装、未测设备帧率/内存/GC、未验证声音听感或震动强弱；FR-16 调试界面、独立尾部动画、镜头拉伸和更完整的氛围未实现，满载以红灯＋速度线呈现。

- 输入后端最终通过实际 PlayerSettings 设置与原生重启校正，启动提示选择 No；新进程 195972 读取 activeInputHandler=0。重启后 Core 再次 45/45，Game **38/38**（额外加入出水后台冻结/恢复），见最终 presentation-editmode/playmode/restart-input-20261021.json。
- 最终 0.2.0 构建 **Succeeded，0 errors / 0 warnings，110.2002241 秒**；APK 实际 125,678,847 bytes（119.9 MiB），Development / IL2CPP / ARM64、API 23/34、竖屏，包名 com.huaziii.casualfishing、versionCode 2。v1/v2 签名有效；签名工具的两项 META-INF 未受保护提示仍保留，构建警告与签名提示分别记录。
- SHA256：517ce9d23b15917c066276ac0c330a3edacd27884afa7c361e91ecedcaf00873。详见 android-build、android-build-messages、apk-artifact-20261021.json。BuildReport.totalSize 不等于 APK 大小。
- 最终编译/控制台 0 错误/0 警告；Menu、450×800、dirty=false、playing=false、绑定完整、实际输入后端 0，首次教学和新反馈开关均未遗留偏好键，见 presentation-compilation/console/final-scene-20261021.json。ADB 仍无设备；未做安装、手机触摸或性能验收。
