# Fishrise 音频来源

核对日期：2026-10-25。当前 BGM 使用下节两份用户导入素材，分别记录 Pixabay 和 Asset Store 授权；原首版资源表中的第三方音源页面标注 CC0。保留来源，不使用 demo 视频或 YouTube 音轨。

## 当前 BGM（本日后续修订）

- 菜单／教学／图鉴／出水后：`Music/magiksolo-aquarium-fish-132518.mp3`，Magiksolo / Aquarium Fish，用户导入原文件，实测 146.469 秒。[原曲页](https://pixabay.com/music/beats-aquarium-fish-132518/) 标注 Pixabay Content License；[授权摘要](https://pixabay.com/service/license-summary/)。原文件不改写，不标作 CC0；不是从 Motion Array 或 YouTube 下载的预览音。
- 下潜／停留／上收：`Music/2 - Phat Phrog Studios - Underwater Ambience - 3 Minute.wav`，Phat Phrog Studio，用户导入原文件，实测 192 秒。[Unity Asset Store 产品页](https://assetstore.unity.com/packages/audio/ambient/nature/ambient-video-game-music-underwater-worlds-220371) 标注 Standard Unity Asset Store EULA，非 CC0。商店说明不支持无缝循环，因此播放器在曲尾使用双音轨交叉淡化。
- 只调整这两份文件的导入设置：立体声、44100 Hz、Streaming／Vorbis 0.75、Android 对应覆盖。2026-10-25 资源清理后仅保留当前两首 BGM；另外九首 Underwater Worlds、宣传图与已被替代的 FrozenVeil 已随清理前工程备份归档。保留曲目的原始音频字节和 GUID 不变，没有生成额外长音乐副本。

## 首版音效与环境资源来源（FrozenVeil 已归档）

| 文件 | 原资源与作者 | 授权页面 |
| --- | --- | --- |
| FrozenVeil.wav（已归档） | Synth-thetic，Beyond the Frozen Veil (Loop).flac | https://opengameart.org/content/beyond-the-frozen-veil |
| IceWind.wav | Luke.RUSTLTD，wind1.wav；作者说明为程序合成风声 | https://opengameart.org/content/wind1 |
| Underwater.wav | gmason，underwater_or_space_engine_0.ogg；海浪录音经低通处理 | https://opengameart.org/content/underwater-or-space-engine-rumble |
| Dive / Surface / CaptureA / CaptureB / Protection / Escape.wav | rubberduck，40 CC0 water / splash / slime SFX：splash_01 / splash_06 / bubble_01 / bubble_02 / bubble_03 / splash_03.ogg | https://opengameart.org/content/40-cc0-water-splash-slime-sfx |
| Branches / FishLandA / FishLandB.wav | Kenney Impact Sounds：impactMetal_light_000 / impactWood_medium_000 / impactWood_medium_001.ogg | https://kenney.nl/assets/impact-sounds |
| Result.wav | Kenney Music Jingles：jingles_PIZZI00.ogg | https://kenney.nl/assets/music-jingles |
| Ui.wav | 工程已有 Kenney UI Pack 2.0，Sounds/click-a.ogg | 原授权：Assets/_Game/Art/kenney_ui-pack/License.txt |
| Sonar / Full / Treasure / Reward / FishWhoosh / Reel.wav | 本工程原创程序合成：柔和声呐、音阶、过滤噪声和卷线棘轮 | 无第三方采样 |

处理记录：短音效去首尾静音、极短淡入淡出、统一峰值；风声和水下底声交叉拼接循环；风声高通 160 Hz／低通 5500 Hz，水下底声高通 65 Hz／低通 1200 Hz。使用 OGG 源转成 WAV 便于统一导入，不代表恢复原先损失的细节。音乐由原 FLAC 转为立体声 WAV，不截取 demo。

音乐来源页所写的时长与下载文件不同：实际 Loop 文件用 ffprobe 测得 **142.220792 秒**；运行时直接循环所下载的 Loop 版本，不按网页所写的 153.6 秒硬裁。

Kenney 下载包随附原授权保留在 License-Kenney-Impact.txt / License-Kenney-Jingles.txt。其他 OGA 下载没有随附授权文本，以上原作者页面是所核对的来源记录。备选但未接入：CleytonKauffman 的 [Snow Theme / Through the Snow](https://opengameart.org/content/snow-theme)，页面标注 CC0，24 秒，适合更温暖的短菜单循环。

## 当前目录（2026-10-25）

- `Music/`：当前菜单与水下两首 BGM。
- `Ambience/`：IceWind 与 Underwater 两种环境循环。
- `SFX/`：卷线与其余事件音效。
- 本文件及 Kenney 授权原文继续保留；删除列表和还原备份见 `Docs/Verification/asset-cleanup-20261025.md`。
