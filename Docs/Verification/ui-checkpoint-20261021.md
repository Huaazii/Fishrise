# 第二阶段：中文界面与三比例检查点

日期：2026-10-21（Asia/Kuala_Lumpur）。Unity 2022.3.52f1，URP 2D。需求基线为 Docs/需求文档.md v1.1。本检查点完成阶段 02 的 Editor 实施，设备与交付验收仍待后续阶段。

## 已完成

- 中文 HUD 显示保护、主钩容量、深度与阶段；模型事件驱动数值变化、轻微弹跳、保护失效灰显和满载红显，暂停时弹跳冻结。
- 正式起始页和三页教学。首次点开始先进入教学，关闭或读完后才创建游戏局并倒计时；之后开始直接倒计时。手动教学关闭回菜单，首次标记由单独的 PlayerPrefs 键保存。
- 中文暂停面板与结算卡片。结算按捕获记录合并/排序，显示图标、星级、记录快照单价、总数量、主钩/分支明细及“含分支”角标；七鱼种可滚动，空收获清除旧卡。
- 接入 15 张已有界面/教学图，捕获图标复用主钩 B04；中文字体随工程引用并附许可。累计复用 42/45 张源 PNG，没有生成新插画。
- 按宽度缩放与安全区域布局；真实 450×800、450×975、450×1050 Game View 下完成界面和关键玩法检查。

主要代码：GameUi、GameUiAssets、UiText、UiWidgets、TutorialView、ResultView、GameBootstrap、FishingUiBuilder、FishingPreview，以及 UiIntegrationTests。Core 规则不因界面接入而改变。Main/Menu 仅追加 UI 资产绑定，保留鱼群、环境与既有摄像机修改。

## 分层验证

| 层级 | 结果 | 证据 |
| --- | --- | --- |
| C# 诊断 | Rider 11 文件无错误 | [ui-rider](ui-rider-20261021.json) |
| Unity 编译 | 最终编译/控制台均 0 错误、0 警告 | [编译](ui-compilation-20261021.json)、[控制台](ui-console-20261021.json) |
| 纯规则 | Core EditMode 23/23 | [ui-editmode](ui-editmode-20261021.json) |
| Unity 集成 | 游戏 PlayMode 21/21（原有 15 项＋界面 6 项） | [ui-playmode](ui-playmode-20261021.json) |
| 三比例 | 每个比例均检查菜单、教学、HUD、暂停/结算、模拟安全区，并回归保护、满载、暂停、重开及镜头宽度 | 上述 PlayMode 中两个 ThreePortrait/ThreeRealPortrait 测试 |
| 正常帧单局 | 100 米、10 条、240 金币/180 贝壳，Countdown 到 Result，未超时 | [逐帧采样](ui-live-run-20261021.json) |
| 正常帧暂停 | 暂停期间游戏时间、钩和鱼位置不变；恢复后继续 | [暂停前](ui-live-pause-before-20261021.json)、[暂停后](ui-live-pause-after-20261021.json) |
| 结算按钮重开 | 新局 ID，保护 2，深度/捕获/挂鱼 0，54 条/7 类，环境和 Canvas 各 1 | [ui-live-restart](ui-live-restart-20261021.json) |
| 受控结算展示 | 7 主钩＋1 蓝纹鱼分支，7 卡/8 条，173 金币/128 贝壳；合并来源与拖动滚动正确 | [受控数据](ui-controlled-settlement-20261021.json)、[滚动](ui-result-scroll-20261021.json) |

PlayMode 使用保存场景、真实 Physics2D 与按钮回调，部分玩法步骤由测试显式推进。另行记录的正常帧单局只由脚本设置横向目标，规则和物理由正常 FixedUpdate 推进；帧号 1337→10306、游戏时间 0→32.5084 秒。两者都不代表实体鼠标或手机输入验收。

受控混合来源展示没有改变真实已完成单局的 10 条记录；它验证界面兼容 Main/Branch，不代表分散鱼钩已实现。模拟安全区为编辑器注入矩形，不代表设备刘海/手势区域实测。

## 实际画面

- [9:16 起始页](ui-menu-916-20261021.png)
- [教学第一页](ui-tutorial-1-916-20261021.png)、[第二页](ui-tutorial-2-916-20261021.png)、[第三页](ui-tutorial-3-916-20261021.png)、[9:19.5 教学](ui-tutorial-9195-20261021.png)
- [暂停](ui-pause-916-20261021.png)、[下潜](ui-diving-20261021.png)、[深水](ui-deep-20261021.png)、[上收](ui-ascent-20261021.png)
- [真实单局结算](ui-result-20261021.png)
- [9:21 七鱼种受控列表顶部](ui-result-seven-921-20261021.png)、[拖动后底部](ui-result-seven-bottom-921-20261021.png)

## 发现并修复的问题

首轮集成 21/21 通过后，画面复核发现结算鱼名和数量被截断。原文字框高 38，Noto 字体实际所需高度分别约 38.4/40，可见字符未生成。已将两处文字框增至 48，把高度容差从 2 收紧到 0.1，并验证蓝纹鱼名称和数量确实生成可见字符。修复后全量集成再次 21/21，实际截图也已复核。

保留[修复前报告](ui-playmode-before-text-fix-20261021.json)、[修复前截图](ui-result-before-labels-20261021.png)及[文字数据前](ui-card-text-before-20261021.json)/[后](ui-card-text-after-20261021.json)，避免把首轮测试通过等同于画面通过。

编译后桥接的本机发现文件曾替换失败，导致认证信息陈旧。归档该进程的旧发现文件并通过 Unity 的 Restart Bridge 恢复后，才完成最终回归；没有修改桥接包、认证配置或 Windows 权限。详见[恢复记录](ui-bridge-recovery-20261021.json)。此前桥接包额外 EditMode 测试的手柄释放失败仍保留在 population-editmode-with-bridge-20261021.json，本报告的 23/23 专指游戏 Core。

## 素材与交接

- [完整性记录](ui-preservation-20261021.json)：45 张源图哈希全部不变；既有 .meta 无改动；本轮开始前的摄像机组件改动保留。
- [重复导入记录](ui-repeat-import-20261021.json)：42 份界面图/字体/配置/场景及身份文件再次导入后哈希相同。
- [教学标记恢复](ui-preference-restored-20261021.json)：恢复到检查前未看首次教学的状态，只处理该键。
- [最终状态](ui-final-scene-20261021.json)：Menu、450×800、未进入 Play、无未保存场景修改、UI/字体/主钩绑定完整。

本轮未构建新 APK，现有 0.1.0 包仍为第一阶段版本；未做真机、实体输入、帧率/内存/GC 验收。FR-08 分支 HUD 留第三阶段；出水鱼/飘字/跳过、音效与最终表现留第四阶段。

下一部分进入第三阶段：先实现下潜声呐的次数、冷却、受惊小鱼与接触豁免，再接入分散鱼钩、分支容量/HUD、统一批次分配与真实多钩结算回归。
