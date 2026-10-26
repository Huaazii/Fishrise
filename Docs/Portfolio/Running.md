# 源码入口

[项目首页](../../README.md) / [技术架构](Architecture.md) / [玩法与画面](Demo.md)

## 工程环境

Unity 2022.3.52f1 / C# / URP 14.0.11 / uGUI / Android 竖屏。

## 打开工程

```bash
git lfs install
git clone https://github.com/Huaazii/Fishrise.git
cd Fishrise
git lfs pull
```

使用 Unity 2022.3.52f1 打开仓库根目录，运行 `Assets/_Game/Scenes/Menu.unity`。`Main.unity` 可以直接进入单局。

`Fishing/Xiaomi 15 Portrait` 提供 1200×2670 竖屏预览，`Fishing/Portrait Game View` 提供 450×800 预览。

## 目录

| 目录 | 内容 |
| --- | --- |
| `Assets/_Game/Scripts/Core` | 状态机、捕获、鱼获模型、能力和收线规则 |
| `Assets/_Game/Scripts/Game` | 输入、物理、鱼群、镜头、界面和表现 |
| `Assets/_Game/Scripts/Editor` | 场景装配、资源导入、预览与 Android 构建工具 |
| `Assets/_Game/Configs` | 玩法、鱼种、能力、UI 和音频配置 |
| `Assets/_Game/Prefabs` | 主钩、七鱼种和冰湖环境 |
| `Assets/_Game/Scenes` | Menu / Main |
