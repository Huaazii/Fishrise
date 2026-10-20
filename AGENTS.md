# FishingCasualGame

- 需求唯一基线：`Docs/需求文档.md`（v1.1）。先读 `Docs/STATUS.md`，再按任务读取需求与 `Docs/Architecture.md`。
- Unity 固定为 **2022.3.52f1**，URP 2D；Android 竖屏，占位画面先完成规则。
- 目录：`Assets/_Game/Scripts/Core` 为纯 C#；`Game` 为 Unity 适配；`Configs`、`Prefabs`、`Scenes` 保存资产。依赖只能 Game → Core。Core asmdef 禁用 Unity 引用。
- Core 接收只读规则、显式 dt、完整接触批次；配置资源、物理、输入、场景对象留在 Game。
- 语义分析与 C# 诊断使用 Rider MCP；Unity 状态、资产、编译、运行使用 Unity CLI。本版本已安装 `unity-cli` / bridge 0.18.1，所有调用显式传工程路径。官方 `unity` 的 Pipeline 要求 Unity 6，不能直接替换本工程桥接。
- 修改前确认目录、差异、目标 Editor 与 Play 状态。保留既有未跟踪文件、GUID、共享引用及无关修改。
- 相关代码修改后集中编译和诊断；Core 编辑器测试验证规则，PlayMode 验证实际适配与流程。构建、安装、真机验收分别记录；无测量不声称性能达标。
- 公开类与方法写 XML summary，规则注释解释原因。避免无关重构与提前实现后续阶段能力。
- 当前输入是 Legacy Input Manager（activeInputHandler=0），Game View 可用 Fishing/Portrait Game View 设置 450×800。修改输入后端需要重启 Editor 后再构建，避免 URP Editor/Player 条件编译的类型布局不同。
- 重启后的进程识别可能失效。当前已验证本地端口 6400；必要时显式 `--host 127.0.0.1 --port 6400 --project-path <工程路径>`，先调用 get_editor_info 确认目标。CLI test 包装器失败时可使用 tool call run_tests / get_test_status；该接口导出 JSON，不是 NUnit XML。
- 每个检查点更新 STATUS：需求编号、主要文件、实测结果、问题/未验证项、下一步。失败与未完成检查不能记为通过。
- 简洁中文沟通。按当前实施阶段统一修改公共接口、场景和项目设置。
