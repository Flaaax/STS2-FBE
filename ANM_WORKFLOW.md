# FBE ANM 动画导入标准

本文件规定 FBE 新增自定义怪物动画的唯一资源契约。无论来源是外部 `.anm2` 还是手工序列帧，
都必须使用“透明序列帧 + `timeline.json`”；不得为新内容直接在 C# 中硬编码帧路径或使用没有时间线的 GIF。
ANM2 导入时由转换器生成此契约；手工资源也必须按同一 JSON 格式编写时间线。现有古烈、门等本来就以普通序列帧制作的资源不追溯迁移。

## 运行时文件契约

每一个导入动作必须位于独立目录，并且至少包含：

```text
FBE/animations/<敌人>/<动作>/
  timeline.json
  layer_<图层编号>_<30FPS帧编号>.png
```

- PNG 必须是透明背景、统一画布尺寸、nearest 采样的 RGBA 图像。
- `timeline.json` 必须遵循 `FBE.BakedAnmTimeline/v1`，包含 FPS、画布、原点、可见范围、图层顺序及每一 tick 引用的 PNG 路径。
- 单次战斗动作若需在特定帧结算效果，可额外写入 `events`；每项记录原始 ANM2 事件名和其在导出时间线中的零基 tick。事件位置必须由 ANM2 的 `Trigger` 推导，不得按预览视频估计。
- 所有动画固定按其 ANM2 的原生 FPS 运行；Isaac 资源通常为 30 FPS。
- 原始 `.anm2`、原始 spritesheet 和 Python 转换脚本不放进 FBE 发布包；它们保留在 `Project_Isaac` 作为可复现的制作来源。

运行时由 `BakedAnmVisuals` 只读取 PNG 与 `timeline.json`。它绝不解析 `.anm2`。ANM2 的 `Delay`、`Interpolated`、裁切、pivot、缩放、旋转、颜色和图层摆放必须由转换器在离线阶段解决。唯一例外是已有、经用户确认的预览脚本已经定义了不同的行为：转换器必须逐项复刻该脚本，并在转换脚本中用中文注释记录该例外；不得自行选择“更正确”的 ANM 语义。

## 转换与审阅流程

1. 在 `E:\Projects\Python\PythonWorkspace\Project_Isaac` 中选定原始 ANM2、动画名及需要保留的图层。
2. 使用 `export_anm2_to_fbe.py` 输出透明帧与 `timeline.json` 到 FBE 对应动作目录。
3. 审阅导出的首帧、循环衔接和 alpha 边界；不要从 GIF、MP4 或录屏反向提取帧。
4. 若资源具有运行时 shader，PNG 只保存原始颜色，shader 写入 FBE 材质并由视觉节点为每个怪物实例创建独立材质。
5. 新动作接入战斗前，先确认其名称、循环/单次性质、事件点和所需的额外 ANM2 特效。

## Dogma 特例

Dogma 的蓝色雪花、绿色眼睛闪烁与横向失真必须由 `FBE/materials/dogma.gdshader` 在运行时执行。每个实例生成独立随机相位及闪烁/失真定时；不得把这些动态效果烘焙进 PNG，也不得复用预览脚本的固定 RNG 序列。

阶段一电视的 `Idle` 只导出本体图层 0。`grid/tv_light.png` 的图层 1 是大光柱，当前不属于电视精灵。

## 格式示例

```json
{
  "schema": "FBE.BakedAnmTimeline/v1",
  "fps": 30,
  "frameCount": 16,
  "canvasWidth": 160,
  "canvasHeight": 160,
  "originX": 80,
  "originY": 88,
  "bounds": { "x": 20, "y": 10, "width": 110, "height": 130 },
  "layers": [
    {
      "layerId": 0,
      "frames": ["res://FBE/animations/Dogma/dogma_idle/layer_00_000.png"]
    }
  ]
}
```

`frameCount` 与每个图层的 `frames` 长度必须一致。图层数组的顺序就是绘制顺序，后面的图层覆盖前面的图层。
