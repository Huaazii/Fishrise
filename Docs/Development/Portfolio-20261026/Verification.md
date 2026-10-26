# 验证结果与已知限制

[返回项目首页](../../../README.md) · [架构图谱](../../Portfolio/Architecture.md) · [演示取证](Demo.md)

检查日期：2026-10-26，Asia/Kuala_Lumpur。目标工程 Unity 2022.3.52f1 / URP 2D。以下重新执行的游戏测试与历史记录分开统计，不包含 bridge 等第三方包自带测试。

## 本次结果

| 类型 | 检查 | 结果与证据 |
| --- | --- | --- |
| 静态 | Core / Game 程序集方向 | `Fishing.Core` 禁用 Unity 引用；`Fishing.Game` 单向引用 Core；[架构图谱](../../Portfolio/Architecture.md) |
| 静态 | 源工程保留与发布快照 | 取证前后 Assets / Packages / ProjectSettings 共 668 个文件 SHA256 一致；发布副本逐文件核对，不重写场景、Prefab、配置或 GUID |
| 编译状态 | 当前 Editor 已编译脚本状态 | 0 错误 / 0 警告，非编译、非导入状态；[状态 JSON](Evidence/compilation-state.json)。本次没有改 C#，没有触发新的脚本编译 |
| 规则测试 | Core EditMode | **100/100**，失败 / 跳过均 0，约 1.38 秒；[完整报告](Evidence/core-editmode.json) |
| 场景测试 | Game PlayMode | **173/177**，4 失败，0 跳过，约 146.97 秒；[完整报告](Evidence/game-playmode.json) |
| 实际运行 | 从菜单开始到结算 | 正常 Editor 游戏帧中完成 100 米下潜、三跳声呐、分钩、满载、出水与结算；[完整视频](../../Portfolio/Media/full-run.mp4) / [逐帧记录](recording.json) |

录屏脚本只提供横移目标与按住/松开收线意图，没有生成额外鱼、写入鱼获或收益、修改深度与放宽规则。该局留存 15 条鱼，主钩 10 / 分钩 5，结算 110 金币 / 105 贝壳。

## 当前 4 项失败

| 测试 | 本次断言结果 |
| --- | --- |
| `ButtonStateIntegrationTests.EveryButtonRemainsReadableAcrossPointerSelectedDisabledAndReopenStates` | 按钮覆盖计数预期 23，实际访问 22 |
| `GameIntegrationTests.ImportedFishOnlyContactAtTheirHeadsAndMirrorWithFacing` | 鱼头位置预期 -0.442000061，实际 -0.675999999 |
| `SpeciesPrefabIntegrationTests.SevenIndependentPrefabsHaveSerializedArtHeadAndMouth` | 缩放预期 (1.2, 1.2, 1)，实际 (1, 1, 1) |
| `SpeciesPrefabIntegrationTests.SpeciesPoolsKeepTheirPrefabAndResetCapturedOrScaredStateForTwentyRuns` | 位置预期 -0.451499969，实际 -0.494499952 |

后三项与本地此前记录的断言及数值一致。按钮检查在当前设置页会按操控模式及传感器能力隐藏控件的设计下出现计数不一致，需要继续核对覆盖路径。仅凭这次检查不能把全部失败都归为“测试过时”，也不能认定为玩法已修复。

这次作品集工作没有修改玩法、素材绑定或测试断言来消除失败，也没有用旧版本全通过报告替代上述结果。报告保留失败项与堆栈，便于后续单独处理。

## 未验证范围

- **APK 与设备**：本次没有重新构建 APK、安装手机或验收当前提交的设备运行。此前的构建记录属于对应历史版本。
- **输入**：受控 Editor 演示不等于实体触屏、手机重力传感器、人工握持手感或触控延迟测试。
- **性能**：没有当前版本的手机 FPS、GPU、内存、GC 或功耗测量。目标 120 FPS 只是代码设置，当前直接写 Transform 的物理路径也不能据此声称动作已在 120 Hz 平滑呈现。
- **声音**：GIF 和本次无声 MP4 不证明 BGM、音效或震动体验；真机听感与强弱需另验收。
- **跨机器导入**：已核对发布快照与本机源工程，未在另一台设备或全新 Unity 缓存中执行导入与构建。

## 材料范围

仓库收录本次测试导出、编译状态、录制数值、压缩 GIF 与完整 MP4。约 635 MiB 的本地历史原始取证没有整体复制到本次作品集更新中。既有历史文件不作为当前版本全部通过的证明。
