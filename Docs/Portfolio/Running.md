# 打开、运行与构建

[返回项目首页](../../README.md) · [架构图谱](Architecture.md)

## 环境

| 项目 | 固定配置 |
| --- | --- |
| Unity | 2022.3.52f1 |
| 渲染 | URP 14.0.11，2D Renderer |
| 界面 | uGUI，Noto Sans SC；标题使用 Lilita One |
| 输入 | 游戏使用 Legacy Input API；项目 Active Input Handling 保持 Both |
| Android 构建 | IL2CPP、ARM64、竖屏，最低 API 23 / 目标 API 34 |
| 版本资源 | Git LFS 管理工程中的图片、音频和字体 |

版本以 [ProjectVersion.txt](../../ProjectSettings/ProjectVersion.txt)、[manifest.json](../../Packages/manifest.json) 和 [构建工具](../../Assets/_Game/Scripts/Editor/Build/FishingAndroidBuild.cs) 为准。

## 获取工程

先安装 Git LFS，再克隆。仓库为 private 时，账号需要具备访问权限。

```bash
git lfs install
git clone https://github.com/Huaazii/FishingCasualGame-2026-10-18_05-14-14.git
cd FishingCasualGame-2026-10-18_05-14-14
git lfs pull
```

1. 在 Unity Hub 中使用 **2022.3.52f1** 打开仓库根目录，等待依赖和资源导入。
2. 打开 `Assets/_Game/Scenes/Menu.unity`，进入 Play。
3. 选择菜单 `Fishing/Xiaomi 15 Portrait`，以 1200×2670 预览；`Fishing/Portrait Game View` 提供 450×800。
4. 首次开始显示教学；`Assets/_Game/Scenes/Main.unity` 也可以直接运行单局。

运行需要的已导入素材保存在工程内。部分原始美术的重新导入工具会读取工程外的 `../Arts/`；普通打开和 Play 不需要该源目录。无需重新执行场景或美术生成器。

## 操作

| 场景 | 操作 |
| --- | --- |
| 电脑 | 按住鼠标左右拖动选路；上收时点按/按住空格控制收线；Esc 暂停或返回 |
| 手机拖动模式 | 非 UI 区域拖动控制横移；上收时短点按产生收线脉冲，原地按住持续收线；拖动优先，也可另一根手指收线 |
| 手机倾斜模式 | 自然握姿校准后左右轻倾移动，回正停住；非 UI 区域点按/按住收线 |
| 设置 | 切换操控模式、倾斜倍率、校准及音效/震动；设备无传感器时回退拖动 |

默认拖动。设置保存在本机 PlayerPrefs；收益不跨局累计。

## Android 构建

通过 Unity Hub 为固定 Editor 安装 Android Build Support、SDK/NDK 和 OpenJDK，退出 Play 后选择 `Fishing/Build Android APK`；开发包入口为 `Fishing/Build Android APK (Development)`。

工具构建 Menu/Main 两个场景，输出到忽略版本管理的 `Builds/Android/`。本次作品集更新没有构建或发布新的 APK。

`com.akiojin.unity-cli-bridge` 0.18.1 依赖 Input System，因此保留桥接时 Active Input Handling 使用 **Both**。改为 Old 会在域重载时弹出启用新输入后端的提示，改为 New 则使游戏 Legacy API 不可用。Android 构建可能出现 Both 兼容提示，构建工具会将其原文保存在报告的 `suppressed` 字段，设备输入仍须单独验证；不要仅凭 `warnings = 0` 推断没有该提示。

## 测试

在 Unity Test Runner 中分别运行 `Fishing.Core.Tests`（EditMode）和 `Fishing.Game.Tests`（PlayMode）。桥接包自带测试不包含在游戏套件的统计中。当前结果与已知失败见 [验证说明](Verification.md)。

可选的 `unity-cli` / bridge 用于开发自动化，不是游戏规则层的依赖。Unity 2022 工程使用项目已有 bridge，不要改用需要 Unity 6 的 Pipeline 包。
