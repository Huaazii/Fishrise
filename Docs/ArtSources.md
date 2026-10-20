# 2D 资源记录

更新：2026-10-20。首阶段蓝纹鱼由内置工具生成；第二阶段使用用户已检查齐全的 Arts 原图，没有补充生成插画。天空与水色使用程序渐变。

## 第二阶段：场景与镜头接入

本检查点将以下 17 张源图复制到 `Assets/_Game/Art/Environment/`，源文件不写回。已有 7 张核心素材继续使用原工程副本。

| 源图 | 用途与处理 |
| --- | --- |
| B01/B02 | 渔夫闲置/拉竿两姿态，保留相同的 1149×1369 完整画布与 PPU，脚底 pivot 均为 (0.5, 0.028)，不分别裁边 |
| B03 | 独立鱼竿，手握点为 Sprite pivot；两姿态分别校准手位/角度，竿尖连接鱼线 |
| B07/B08 | 左右冰面，各自裁边后宽度 5.9 世界单位；中部留出冰洞与鱼线导向点 |
| B09/B10 | 左右冰壁，每片高度 5，间距 4.9，交替上下翻转并重叠 0.1；外侧用底色填充，当前每侧 27 片覆盖到 100 米以下 |
| B11/B12 | 山景、阴云，置于冰面和人物后方 |
| B13/B14/B15 | 风向杆、风速杯、风向袋，分别按连接位置装配 |
| B16/B17/B18 | 提灯、雪桶、木台；按可见雪面与台面校准落点 |
| A09/A10 | 大鱼影与鱼群，8 个低透明度装饰节点；仅作视差，不附 Collider 或鱼实例身份 |

其他源图仍使用与核心检查点相同的 alpha≥16 外扩 4 像素裁边、无 Mipmap、双线性、Clamp、无压缩 Sprite 设置。B01/B02 例外保留全画布，以免切姿态时因裁边改变脚底和座椅基准。

`SkyGradient.png`、`WaterGradient.png` 为 Unity 确定性绘制的 **4×512** 竖向渐变纹理，只承担底色。水体由一个连续渐变矩形覆盖，不再使用 25 米一段的色块。它们不依赖 AI 出图，也不覆盖 Arts 原图。

共用 `Assets/_Game/Prefabs/Environment.prefab`，Menu/Main 各保存一个实例及 GameBootstrap.sceneView 引用。旧 Placeholder Environment 保留但停用。`Fishing/Prepare stage 02 environment` 会重设本检查点的命名节点和布局；只允许在非 Play 且开放场景已保存时运行，按预制体来源复用实例，清理同一预制体的多余实例。

镜头默认可视宽度为 12 单位，现有可操作边界仍为 ±4.5，边缘空位用于显示冰壁。下潜/上收分别保持约 80%/20% 的底部起算视口高度，转向在 0.5 秒内缓动；临近水面停在统一冰面构图，避免 Surfacing 状态切换时跳镜头。场景与镜头参数不进入 Core 规则快照。

宽镜头下原鱼线末端仅约 0.94 像素，曾在上收画面局部消失。Hook.prefab 的线宽已改为 0.045→0.04 世界单位，在 450 像素宽、12 单位视野中末端为 1.5 像素；核心导入菜单也保留这一设置。

来源完整性、导入参数和验证证据见 Verification/environment-*.json 与本检查点截图。Android 压缩、全界面适配和性能测量仍待后续步骤。

## 第二阶段：核心美术接入

源目录为工程上一级 `Arts/`，原始 45 张 PNG 保留。本检查点只接入以下 7 张，其他环境、教学及鱼种素材留后续步骤。

| 源图 | 工程内副本 | 用途 |
| --- | --- | --- |
| A01_Fish_Puffer.png | Art/Fish/A01_Fish_Puffer.png | 现有 puffer，河豚 |
| A02_Fish_Striped.png | Art/Fish/A02_Fish_Striped.png | 现有 blue，蓝纹鱼 |
| A03_Fish_Red.png | Art/Fish/A03_Fish_Red.png | 现有 red，红鱼 |
| A04_Fish_SmallSilver.png | Art/Fish/A04_Fish_SmallSilver.png | 现有 small，小红鱼；按红尾小鱼外观复用，文件名不作为鱼种 ID |
| B04_Hook_Main.png | Art/Hook/B04_Hook_Main.png | 主钩本体，独立 Lamp 节点负责状态灯 |
| B05_Bubble_Shield.png | Art/Hook/B05_Bubble_Shield.png | 保护泡 |
| B06_Bubble_Small.png | Art/Hook/B06_Bubble_Small.png | 3 个复用同一 Sprite 的小气泡 |

工程副本路径均相对于 `Assets/_Game/`。`Fishing/Import stage 02 core art` 通过 FishingCoreArtImporter 读取原图，将 alpha≥16 的有效范围外扩 4 像素后输出 RGBA 副本；B06 由 1254×1254 裁为 **183×184**。所有源图不写回。后续可在同一工程重新运行该菜单；使用既有 .meta 保留导入 Sprite 的 GUID，不调用第一阶段的全场景创建器。

导入为 Single Sprite、FullRect、双线性、Clamp、无 Mipmap、非 Read/Write、最高 2048、无压缩。此设置服务当前画面校准，尚未做 Android 压缩与性能验收。四种鱼的 Sprite 基准宽均为 1.25 世界单位，再乘原 size；不改变现有鱼种 ID、数值、游速、数量或场景引用。

每个 FishDefinition 记录朝右时的鱼头位置、半径和鱼嘴挂点；鱼向左游时由根缩放同时镜像 Sprite 与鱼头。捕获后头部判定关闭，鱼嘴贴合主钩的 10 个预设挂点，回收时清除挂鱼旋转。主钩保持原圆形检测中心/半径；鱼线连接到图中顶部吊环，绿/黄/红灯由原规则状态决定。小气泡仅在保护有效时可见，沿显式游戏时间上浮，暂停停止，重开重置。

C12 独立银鱼留作后续小银鱼；黄蝴蝶鱼、鳗鱼与七类鱼深度分布未在本检查点提前实现。捕获 HUD 图标留后续界面步骤复用 B04，不接入造型不同的 C05。

## 蓝纹鱼首版

- 工程文件：`Assets/_Game/Art/FishBlue.png`。
- 方式：内置 image_gen，transparent_background=true；保留原始 RGBA，1774×887，alpha 0..255。
- 原始产物：`C:/Users/Huaziii/.codex/generated_images/01a14e41-c699-7273-bca6-40764a142d00/exec-ffb797c9-7e6f-4d8c-b2b0-f23235641cfe.png`。工程引用使用工程内副本。
- 用途：侧视向右的蓝纹鱼 Sprite，首版风格起点；当前已由上述 A02 副本替换引用，旧文件和 GUID 保留。
- 物理：鱼头小触发区独立于外观，不使用图像轮廓替代玩法判定。

### 实际生成提示词

Use case: stylized-concept. Asset type: one production-ready 2D fish sprite for a portrait casual Unity fishing game. Primary request: a single friendly blue striped fish in exact side view, facing RIGHT, whole body including tail and fins, centered with 8 percent empty padding, horizontal silhouette about 2:1 width to height. Style: polished casual mobile game illustration, clean rounded shapes, hand-painted highlights, restrained dark blue outlines, cyan and turquoise body with three darker blue stripes, large readable eye near the rightmost head, orange small fins. Mouth/head on the right, tail on the left. Readable at small screen size. Background: genuinely transparent alpha. Constraints: exactly one fish; no water, no bubbles, no ground, no scenery, no shadow outside the body, no text, no border, no UI, no watermark. This is a sprite asset, not a concept sheet or a screenshot.

## 占位资源

PlaceholderDisk / Square / Ring 由 Unity Editor 确定性绘制，分别服务于鱼身、环境矩形和保护圈等简单几何，不是最终美术。
