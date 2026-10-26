# 中文界面字体

- 字体：Noto Sans SC Regular，原始静态 OpenType，未改字形。
- 版本：官方 Sans2.004 标签；获取日期 2026-10-21。
- 官方下载：https://raw.githubusercontent.com/notofonts/noto-cjk/Sans2.004/Sans/SubsetOTF/SC/NotoSansSC-Regular.otf
- 官方说明：https://github.com/notofonts/noto-cjk/blob/main/Sans/README.md
- 授权：SIL Open Font License 1.1，全文保留在同目录 OFL.txt；Player 构建另随 StreamingAssets/Licenses/NotoSansSC-OFL.txt 打包。
- 字体原始版权信息保留在字体文件元数据中。
- SHA256：faa6c9df652116dde789d351359f3d7e5d2285a2b2a1f04a2d7244df706d5ea9。
- 字体由 GameUiAssets 引用，中文显示不依赖目标机器安装系统字体；使用 Git LFS 管理。

## Fishrise 标题字体（2026-10-25）

- Lilita One，Juan Montoreano；原始静态 TrueType，字形文件未修改。仅 GameUiAssets.titleFont 与菜单 Fishrise 标题使用，中文及 HUD 继续绑定原 Noto Sans SC。
- 官方来源：https://github.com/google/fonts/tree/main/ofl/lilitaone
- 固定下载提交：90abd17b4f97671435798b6147b698aa9087612f。
- SHA256：f5b641c45c69d772ee4eda687bc9fda411d5cad6b0b45371491da4580cbc8d59。
- SIL Open Font License 1.1 原文：LilitaOne-OFL.txt；构建随 Assets/StreamingAssets/Licenses/LilitaOne-OFL.txt 打包。字体保留原版权与保留字体名。
- 显示字号 82；IceTitleGradient 在字形实际高度范围添加白→冰蓝顶点色，标准 UGUI Shadow／Outline 提供下沿厚度与细边。效果未烘焙到字体，也未新增 PNG 标题图。
