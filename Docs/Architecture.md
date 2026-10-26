# 架构说明

当前源工程的架构图、职责、数据流、固定步契约、单局状态机与设计取舍见 [Fishrise 架构图谱](Portfolio/Architecture.md)。

运行时项目程序集依赖为 `Fishing.Game → Fishing.Core`；Core 的 `noEngineReferences=true`。配置、输入、Physics2D 和场景资源留在 Game，规则与鱼获记录留在 Core。

[作品集首页](../README.md) / [玩法与画面](Portfolio/Demo.md)
