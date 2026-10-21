# 第四阶段：出水演出、最小反馈与 Android 交付检查

日期：2026-10-21，Unity 2022.3.52f1 / URP 2D。当前 Editor 功能检查点与 0.2.0 APK 构建已完成，Android 输入后端修正后的复验和构建结果见下方报告。手机安装、触摸、声音/震动和性能尚未验收。

## 已接入

- FR-09：鱼嘴固定在来源挂点，捕获时轻微缩放，随后摆动；整图鱼游动时仅作小幅视觉摆动，独立鱼尾素材动画仍未实现。
- FR-11：2.3 秒出水演出，主钩鱼依次跃出并显示记录中的金币金额；分支序列延后 0.3 秒。出水隐藏玩法 HUD，金额牌有深色底，底部可点击跳过。结算卡片交错展开。
- 最小反馈：16 个复用捕获飘字、8 个保护破裂气泡、8 条满载速度线；满载时与红灯共同提示高速返航。
- FR-15：入水、保护、捕获、满载、出水、结算、声呐和分支共 8 种程序合成短音；事件发生一次才播放一次。菜单与暂停共用音效/震动设置，偏好跨场景保存；Android 捕获/保护请求 20 ms 短震动。

## 数据与生命周期

Core 仍只通过唯一 CaptureRecord 维护收获。SkipSurfacing 只能从 Surfacing 转入 Result，不重新捕获或计价；重复命令不再发布结算事件。表现层仅读取快照，演出完成或跳过均清除全部活动/待播视图。

鱼、气泡、速度线、飘字和卡片动画使用同一显式游戏时钟；暂停不会推进。音效显式 Pause/UnPause，静音立即停止在播声音。重开清除调度、事件计数和通道，再复用视图。Core/Game 依赖方向不变。

主要文件：Core/FishingFlowController；Game/GameBootstrap、GameUi、FishActor、SurfacePresentation、GameplayFeedbackView、GameFeedback、FeedbackSettingsView、ResultView；Editor/FishingAndroidBuild、FishingAndroidPermissions。路径均在 Assets/_Game/Scripts 下。

## 自动与正常帧证据

- [Core 45/45](presentation-editmode-20261021.json)：新增 5 项检查跳过开始/中途/末尾、重复请求、非演出阶段拒绝、自然超时、空收获与主钩 10＋分支 6 的记录/收益一致。
- [Game 38/38](presentation-playmode-20261021.json)：保留前 32 项，新增 6 项，使用保存场景、真实 Physics2D 和按钮回调，检查延迟、全部鱼播放、跳过清理、挂点、暂停、静音、出水时后台冻结/恢复、20 次重开及 450×800/975/1050 与模拟安全区。
- [Rider](presentation-rider-20261021.json)：16 个相关 C# 文件诊断；最终两个布局文件与构建守卫也重新诊断。
- 首轮已通过测试，但画面发现 HUD/金额拥挤，随后隐藏出水 HUD、缩短单鱼飞行、增大横向分散并添加金额底牌。修改后重新通过 37 项；旧版 [报告](presentation-playmode-before-layout-20261021.json) 与 [画面](presentation-surface-before-layout-20261021.png) 保留，避免只依赖测试结论。
- [正常帧单局](presentation-live-run-20261021.json) 与 [捕获快照](presentation-live-settlement-20261021.json)：脚本仅提供横向目标和实际按钮回调，由正常 FixedUpdate/Physics2D 推进；不是显式 AdvanceGameplay，也不是真人操作验收。重启后最终局 ID 70195509-e036-4cc3-bf9a-37901350f2ff，已完成 100 米、主钩 10＋分支 3＋3、16 条、250 金币/190 贝壳；全部 16 条完成演出，计时 2.3199985 秒，结束活动跳跃数 0，收益未变。
- [正常帧暂停](presentation-live-pause-20261021.json)：声呐及受惊鱼活动时暂停，时钟、位置、冷却和声波冻结。
- [原生窗口输入](presentation-native-input-20261021.json)：通过 computer-use 技能自动点击结算重开、暂停、设置和静音。一次注入拖动没有产生横移，未记为通过，原因未定位；需要真人持续拖动检查。这些自动注入均不替代设备验收。

![出水演出](presentation-surface-peak-20261021.png)

![结算](presentation-result-20261021.png)

## 构建与输入设置

0.2.0 首次构建成功，但有 1 条 Both 输入后端警告。核查发现 Git HEAD 实际保存 activeInputHandler=2，旧文档标记为 Legacy 并不准确。本轮只把该项改为 0，保留其余设置，并在构建工具加入守卫。处理过程中发现文件对象与实际 PlayerSettings 状态需要一致，Input System 包启动提示也可能重新启用新后端；最终通过实际设置对象修改、原生重启并选择 No 后，新进程 195972 读取为 0，见重启报告。重启前后测试分开保留。

首次构建 [报告](android-build-before-input-fix-20261021.json)、[APK 信息](apk-artifact-before-input-fix-20261021.json) 和 [警告原文](android-input-before-fix-20261021.json) 均保留。最终记录：[重启/输入后端](presentation-restart-input-20261021.json)、[构建](android-build-20261021.json)、[APK 版本/权限/签名](apk-artifact-20261021.json)。BuildReport.totalSize 是构建过程体积，APK 交付大小取实际文件长度。

最终重建 **Succeeded，0 错误、0 警告，110.2002241 秒**。APK 为 `Builds/Android/CasualFishing-0.2.0-arm64.apk`，实际 **125,678,847 bytes（约 119.9 MiB）**，Development / IL2CPP / ARM64，API 23/34、竖屏，包名 com.huaziii.casualfishing、versionCode 2。VIBRATE 权限已包含；v1/v2 签名有效，工具的两项 META-INF 提示仍保留在原文。SHA256：`517ce9d23b15917c066276ac0c330a3edacd27884afa7c361e91ecedcaf00873`。

## 保留与限制

- [保留检查](presentation-preservation-20261021.json)：45 张源 PNG 哈希未变，既有 .meta 没有修改；前阶段 14 份资产/场景/设置哈希相同，Main/Menu 保留原摄像机组件与全部绑定。本轮无新增插画。
- 最终 [编译](presentation-compilation-20261021.json) / [控制台](presentation-console-20261021.json) 为 0 错误/0 警告；[交接](presentation-final-scene-20261021.json) 为 Menu、450×800、dirty=false、playing=false、绑定完整、实际输入后端 0，首次教学和新设置没有遗留偏好键。
- [设备检查](presentation-device-20261021.json)：没有连接的 Android 设备，未安装。未测设备帧率、内存或 GC，不声称达到性能指标；现有 ContactBatch/HUD 分配仍需测量。
- 手机触摸、声音听感、震动强弱和后台恢复需实际检查。FR-16 Development 调试界面、独立尾部动画、镜头拉伸和更完整氛围仍未接入；满载当前以红灯和速度线表示。

最小手机验收：安装当前 APK；首次教学与菜单设置；持续左右拖动；用两能力完成一局并分别完整播放/跳过；暂停、重开、切后台恢复；开关声音/震动；在目标设备记录帧率、内存与 GC，再决定需要的优化。
