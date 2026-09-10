# Dogma 首领音乐冲突：诊断与修复建议

## 当前采用的规避方案

Dogma 仅在原版章节 `Overgrowth`、`Underdocks`、`Hive`、`Glory` 中执行 BGM 静音与专属主题曲播放。其他章节保留其自身背景音乐，不调用本次会话的 `SetBgmVol(0f)`，也不启动 Dogma 主题曲；Dogma 音效、战斗结束和退出清理仍正常启用。战后仅在本次会话确实静音过时恢复 BGM 音量。

该方案在 FBE 内使用原版章节类型白名单，不修改 FBECore，不反射适配第三方章节，也不限制 Dogma 的遭遇资格。已做静态核对，尚未进行游戏内验证。下文为原问题的诊断记录及未采用的独占兼容层建议。

## 结论

Dogma 被定义为 `RoomType.Boss`，而 AFTP（Acts from the Past）和 Ruina2 都会把**任意**首领房识别为自己的首领音乐场景，并启动各自的 `AudioStreamPlayer` / 淡入流程。Dogma 同时启动自己的背景音乐，并通过 `NAudioManager.SetBgmVol(0f)` 尝试静音原版 BGM。

该全局音量调用会进入 AFTP 与 Ruina2 对 `SetBgmVol` 的 Harmony 后置补丁。它们将 `0` 代入 `Mathf.LinearToDb(volume²)` 并写回自己的播放器。零线性音量会产生非有限 dB 值，Godot 拒绝该写入并持续记录：

```
Volume can't be set to NaN.
```

因此，Dogma 战中可能同时出现：

1. AFTP 或 Ruina2 的章节 Boss 曲；
2. Dogma 的 `Living in the Light`；
3. 大量 Godot 非有限音量错误。

这不是 FBECore 播放器参数或 `SettingsSave` 被写成 NaN。FBE/FBECore 的临时探针已完成诊断后删除。

## 为什么 Guile 不会触发

Guile 与 Dogma 都调用了 `NAudioManager.SetBgmVol(0f)`，但它们的房间类型不同：

| 遭遇 | 房间类型 | 外部章节模组的音乐分支 |
| --- | --- | --- |
| Dogma | `RoomType.Boss` | 启动外部 Boss BGM / Boss stinger / 淡入流程 |
| Guile | `RoomType.Monster` | 不进入外部 Boss 音乐分支 |

这解释了为何 AFTP 章节内的 Guile 战可正常静音，而同章节内的 Dogma 战会出现错误和音乐叠播。

## 已完成的复现与排除

- 原版游戏：无 NaN 音量错误。
- 仅 FBE + FBECore 的 Dogma 战：无 NaN 音量错误。
- 混合模组环境（AFTP、Ruina2 等）中的 Dogma 战：稳定出现错误。
- 禁用 SF6BossIntro 后：仍出现 98 次错误；SF6BossIntro 不是原因。
- AFTP 章节内 Guile 战：无此错误；确认首领房状态是关键差异。

归档日志位于工作区 `tmp_logs/`：

- `dogma_mixed_20260910_213520.log`
- `dogma_nan_probe_20260910_215231.log`
- `dogma_no_sf6_20260910_215809.log`
- `audio_control_tests_20260910_220856.log`

## 参考代码位置

### FBE

- `Scripts/Monsters/DogmaEncounter.cs`
  - `RoomType => RoomType.Boss`。
- `Scripts/Monsters/DogmaBattleAudio.cs`
  - `Start()` 中的 `NAudioManager.SetBgmVol(0f)`；
  - `StopMusic()` 中恢复原 BGM 音量。
- `Scripts/Monsters/GuileEncounter.cs`
  - `RoomType => RoomType.Monster`，用于对照。
- `Scripts/Monsters/GuileBattleAudio.cs`
  - 与 Dogma 相同的全局静音调用，证明调用本身并非充分条件。

### 原版游戏（`STS2 source` 的 `0.111.0` 分支）

- `src/Core/Nodes/Audio/NRunMusicController.cs`
  - `UpdateMusic()`、`UpdateTrack()`、`UpdateAmbience()`：原版按房间状态更新音乐；
  - `StopMusic()`：停止原版 run music 与 ambience 的公开入口；
  - `NRunMusicController.Instance`：当前 run 的音乐控制器。
- `src/Core/Nodes/Audio/NAudioManager.cs`
  - `SetBgmVol(float)`：原版全局 BGM 音量入口。

### AFTP 反编译参考

- `Thirdparty Mods/ActsFromThePast_decompiled/ActsFromThePast.Patches.Audio/MusicPatches.cs`
  - `LegacyActMusicPatches.IsBossRoom()`：仅按 `RoomType.Boss` 判定；
  - `UpdateMusic_Prefix`、`UpdateTrack_Prefix`、`UpdateAmbience_Prefix`：接管章节音乐；
  - `StopMusic_Postfix`：停止 AFTP 自定义音乐；
  - `SetBgmVolPatch.Postfix(float)`：转发给 `AFTPModAudio.SetMusicVolume`。
- `Thirdparty Mods/ActsFromThePast_decompiled/ActsFromThePast/AFTPModAudio.cs`
  - `SetMusicVolume(float)`：`LinearToDb(Pow(volume, 2))` 未处理零值；
  - `FadeIn(...)`、`StopMusic()`：自定义 Boss BGM 生命周期。

### Ruina2 反编译参考

- `Thirdparty Mods/Ruina2_decompiled/Ruina2.Ruina2Code.Patches/MusicPatches.cs`
  - `IsBossRoom()`、`UpdateMusic_Prefix`、`UpdateTrack_Prefix`、`UpdateAmbience_Prefix`；
  - `SetBgmVolPatch.Postfix(float)`：转发给 `RuinaAudio.SetMusicVolume`。
- `Thirdparty Mods/Ruina2_decompiled/Ruina2.Ruina2Code.Audio/RuinaAudio.cs`
  - `SetMusicVolume(float)`：同样未处理零值；
  - `FadeIn(...)`、`StopMusic()`：自定义 Boss BGM 生命周期。

## 建议修复：FBE 首领音乐独占兼容层

目标是保留 Dogma 的 `RoomType.Boss`（Boss 奖励、地图与历史语义），并且不修改 AFTP 或 Ruina2 的文件。

1. 在 FBECore 提供通用的“独占背景音乐会话”API。会话启动时调用 `NRunMusicController.Instance?.StopMusic()`，而不是调用 `NAudioManager.SetBgmVol(0f)`；会话结束时释放控制权。
2. Dogma 使用该会话启动自身 BGM，删除现有的 `SetBgmVol(0f)` 与战后音量恢复调用。
3. 在 FBE 中实现 Dogma 专属、可选的运行时兼容适配器：检测 AFTP/Ruina2 的相关类型是否已加载；仅在 Dogma 会话期间阻止其 `UpdateMusic`、`UpdateTrack` 与 `UpdateAmbience` 的音乐接管逻辑重新创建 Boss 曲。
4. 会话结束后先停止 Dogma 曲目，再解除适配器；由原版/外部模组的正常后续音乐更新恢复 Boss 战后 stinger 或地图音乐。

该适配器使用反射定位已加载类型，不添加 AFTP 或 Ruina2 的编译时依赖；未安装时没有行为。现有日志显示 SF6BossIntro 的肖像捕获不是本问题的触发条件（禁用后仍复现），因此不纳入此兼容层。

## 不建议的方案

- 将 Dogma 改为 `RoomType.Monster`：能绕过外部 Boss 分支，但会破坏 Boss 房语义。
- 将 `SetBgmVol(0f)` 改成极小正数：可暂时规避非有限 dB，但外部 Boss 曲仍可能启动，不能解决曲目叠播。
- 继续保留临时探针：诊断已完成，探针已移除。
