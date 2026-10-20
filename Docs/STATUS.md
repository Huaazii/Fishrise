# 开发状态

更新：2026-10-21（Asia/Kuala_Lumpur）；功能验证截至 2026-10-20。需求基线：需求文档.md v1.1。

## 阶段映射

| 实施对话 | 第 9 章里程碑 | 状态 |
| --- | --- | --- |
| 01 工程准备、最小闭环与核心规则 | ①最小闭环＋②规则完善 | 最小闭环与首次 APK 构建已完成，保留真机/输入手感待验收项 |
| 02 鱼群、摄像机与界面 | ③内容与界面 | 进行中：核心美术与场景/镜头检查点通过，七类鱼深度分布/正式界面待续 |
| 03 声呐与分散鱼钩 | ④创新玩法 | 待开始 |
| 04 表现打磨与交付 | ⑤表现打磨＋⑥交付验证 | 待开始 |

## 当前检查点

- 2026-10-18：四份交接文档、Core/Game 基础、23 个纯规则测试。
- 2026-10-20：Menu/Main 保存场景、4 种鱼配置、鱼/鱼钩预制体、场景装配、固定步物理批次、最小 UGUI、基础跟随摄像机、暂停/结算/重开。
- 首阶段蓝纹鱼用内置 image_gen 生成；第二阶段使用用户已检查的 Arts 原图，没有补充生成插画。核心 7 张工程副本已接入，第二检查点再接入 17 张环境/人物/背景鱼素材及两张程序渐变底色。正式界面仍待续，详细来源在 ArtSources.md。
- 第二阶段首个检查点：四种鱼的尺寸、朝向、鱼头与鱼嘴挂点已校准；B06 裁为 183×184；主钩使用独立绿/黄/红灯、顶部鱼线挂点与 10 个挂鱼点。原鱼种 ID、收益、游速、数量和核心规则保持不变。
- 第二个检查点：Main/Menu 共用冰面、冰壁、渔夫两姿态、山云与道具；连续水色、背景鱼影视差、竿尖/冰洞鱼线、0.5 秒方向缓动与统一水面构图已接入。可视宽度为 12，可操作边界仍为 ±4.5。
- 首次 APK 因输入后端切换后 Editor/Player 的 URP 类型布局不一致失败；保存并重启指定 Editor 后重试成功。构建耗时约 294 秒，0 errors / 0 warnings。失败与成功报告分别为 Verification/android-build-20261020-first-run.json、android-build-20261020.json。

## 需求覆盖

| 需求 | 本阶段实现和证据 | 尚待完成 |
| --- | --- | --- |
| FR-01 | 鼠标/触摸共用相对拖动映射，过滤 UI 起始触点、横移边界和速度上限 | 实体鼠标手感、Android 触摸一致性检查 |
| FR-02 | 真实场景下潜、底部停留、上收；Core 与 Game 测试通过 | 真机操作体验 |
| FR-03 | 保护、持续接触、分离再进入、无敌和底部失效；真实重叠与 Core 测试通过 | 气泡破裂动画/音效 |
| FR-04 | 回调/扫掠/末端重叠统一批次；四种正式鱼图独立鱼头判定与转向镜像，捕获后关判定、停游、鱼嘴贴挂点；跨端点扫掠测试通过 | 能力多钩第三阶段 |
| FR-05 | 主容量 10、满载事件一次、上收加速/红灯；Core 和实际物理场景测试通过 | 分支容量第三阶段 |
| FR-06 | 4 种鱼正式 Sprite、固定种子基础布局、Queue 复用池；8 个无碰撞背景鱼影/鱼群带视差 | 七类鱼、按深度分段、稀有鱼、可通行性和内容完整验收 |
| FR-07 | 冰面与水下场景；宽度匹配、上下行 y=0.8/0.2、0.5 秒转向缓动、统一出水构图与重开重置；三比例镜头数值检查通过 | 正式界面与全部屏幕比例综合验收 |
| FR-08 | 保护/主容量/深度事件驱动 HUD、暂停按钮状态限制 | 分支 HUD、数值动画与中文正式界面 |
| FR-09 | B04 主钩＋独立绿/黄/红灯，10 个嘴部挂点；鱼线经竿尖/冰洞/钩顶连接，回收后旋转重置 | 摆动与最终表现 |
| FR-10 | 冰面渔夫场景的 3-2-1 倒计时、简易帮助面板 | 三页教学与首次自动打开流程 |
| FR-11 | 简化 Surfacing 等待后结算 | 出水鱼、飘字、跳过 |
| FR-12 | 实际最深深度、鱼种合并数量、两种货币、再开/退出 | 图标/星级卡片、来源明细/角标和滚动 |
| FR-13 | 自定义时钟与暂停界面；三个可暂停状态下实际鱼、钩、游戏时间冻结测试通过 | 后续动画/音频接入后回归 |
| FR-14 | 新局重建、回收鱼、重置钩/镜头/HUD、菜单导航；场景连续重开 20 局通过 | 真机长期稳定性 |
| FR-17 | 独立 Menu 场景、开始/帮助/退出骨架；菜单与 Main 导航测试通过 | 教学首次流程和正式起始页表现 |
| FR-18/19 | CaptureRecord 来源、HookId 与统一批次扩展位置已固定 | 声呐/分散鱼钩第三阶段实施 |
| NFR-05 / T18 | Core 无 Unity/Game 引用，纯数据规则测试通过 | 后续迭代保持 |

部分完成的 FR-06/07/08/09/10/11/12/17 不应记为完整验收。

## 主要文件

- AGENTS.md、Docs/需求文档.md、Architecture.md、ArtSources.md、STATUS.md。
- Assets/_Game/Scripts/Core：RunModel、规则快照、捕获来源、状态机、接触批次与统一裁决。
- Assets/_Game/Scripts/Game：GameBootstrap、GameClock、GameUi、CameraRig、FishSpawner/FishActor、HookMotor、ContactDetector、DragInput 和配置类型。
- Assets/_Game/Scripts/Editor：FishingSceneBuilder（只创建缺失资产）、FishingPreview（真正竖屏 Game View）、FishingAndroidBuild。
- 核心美术检查点新增 FishingCoreArtImporter，Game 的 FishDefinition/FishActor/HookMotor/GameBootstrap 接入美术与挂点；Art/Fish、Art/Hook 保存 7 个裁边副本。既有 4 份鱼配置和 Hook.prefab 已更新，Fish.prefab 保持原绑定。
- 场景检查点新增 FishingSceneView、FishingEnvironmentBuilder、Environment.prefab 与 EnvironmentIntegrationTests；更新 CameraRig、HookMotor、GameBootstrap 及既有 Main/Menu 场景引用，旧占位环境停用但保留。Hook.prefab 仅追加鱼线宽度调整。
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

## 环境、已知限制与未验证项

- Unity 固定 2022.3.52f1 (1120fcb54228)，URP 2D；使用 unity-cli / bridge 0.18.1，明确传工程路径。原 SampleScene 保留。
- Git 纳入源工程、已导入素材与验证记录；图片等二进制资源沿用 Git LFS。Unity 缓存、构建产物及 .codex/.idea/.unity 本机配置保留在本地并忽略，项目入口与已完成功能见 README.md。
- 当前 Legacy Input Manager 后端用于鼠标/触摸和 StandaloneInputModule；避免 Android 使用 Both 后端。
- 首阶段 Rider 曾连接失败；核心美术检查点完成 6 文件诊断，场景检查点完成 7 文件诊断。旧 NuGet 缓存目录与 Roslyn 生成器环境警告未通过修改全局配置处理。
- 核心与环境美术已接入；当前界面仍为英文占位，七类鱼内容、三页教学、正式 HUD/结算卡片、音效和能力尚未完成。
- 本阶段未测帧率、内存或 GC；ContactBatch 每步复制快照，HUD 文本更新也需后续测量收敛。不声称 NFR-01/03 性能达标。
- APK 构建成功、安装成功、真机运行通过必须分别记录。当前没有设备验收证据。
- 现有 0.1.0 APK 是首阶段构建，不含本轮核心/环境美术；本轮验收针对 Unity 源工程，未重建 APK。

## 下一步

1. 首个 APK 已成功：Builds/Android/CasualFishing-0.1.0-arm64.apk，30,872,697 bytes（约 29.4 MiB），Development / IL2CPP / ARM64，API 23/34、竖屏。签名工具退出码 0，v1/v2 签名有效；工具另有两项 META-INF 条目提示，原文保留在 apk-artifact-20261020.json。包名 com.huaziii.casualfishing、版本 0.1.0。ADB 当前无设备，安装与手机运行未验证。
2. 继续 02 的内容与界面检查点：七类鱼的分段深度配置、稀有鱼与可通行性、中文 HUD/起始页/三页教学、正式结算卡片；随后做 9:16/9:19.5/9:21 的综合适配与保护/满载/暂停/重开回归。声呐与分散鱼钩继续留第三阶段。
3. 第三阶段接入声呐与分支优先批次分配；第四阶段表现与真机交付。

## APK 与最终环境

- SHA256：73c231ddc11a0406fe9bafac29831b1496dc7e0a9bc61a1689c1720cbec58029。
- 构建报告 totalSize 包含构建过程产物，并非 APK 文件大小；交付体积以 APK 实际文件的 30,872,697 bytes 为准。
- 构建完成后 Editor 切换到 Android 并出现新实例 PID 147696。用户确认 Main 场景可操作；带明确 host/port 的 get_editor_info 已核实工程路径与版本。CLI test 的本地进程识别仍失败，已改用同一桥接的 run_tests / get_test_status，未改用户工具配置。重启后 Core 23/23、Game 7/7 再次全部通过，证据为 core-editmode-after-restart-20261020.json 与 game-playmode-after-restart-20261020.json。

- 重启后 Unity 编译/控制台：errorCount=0、warningCount=0、consoleErrorCount=0、consoleWarningCount=0（2026-10-20T07:23:47.8258089Z 后检查）。正式中文界面、美术完善和手动输入验收仍在待办中。

- 首阶段自动截取 UGUI 曾因 Game View 焦点条件失败。第二阶段已通过实际 ScreenCapture 获取上述 450×800 运行帧；首阶段暂停图仍保留。交接时退出 Play，留在 Main。
