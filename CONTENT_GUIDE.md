# FBE 内容开发指南

本文档供负责新增或修改 FBE 游戏内容的 Agent 使用。开始制作卡牌、遗物或 Power 前，应先阅读本指南；涉及多版本 ABI、构建或发布配置时，另行阅读 `DIST.md`。上层工作区规则仍然适用，禁止由 Agent 执行构建。

## 通用规则

- `Entry.Init()` 已调用 `ModTypeDiscoveryHub.RegisterModAssembly`，用于发现 RitsuLib 内容注册注解；新增内容仍必须在具体模型类上添加对应的注册注解。
- RitsuLib 默认把公开 Entry 规范化为 `MODID_CATEGORY_TYPENAME`。例如 FBE 的 `ExampleCard` 默认为 `FBE_CARD_EXAMPLE_CARD`，`ExampleRelic` 默认为 `FBE_RELIC_EXAMPLE_RELIC`；本地化键必须以实际公开 Entry 为词干。
- 已经发布的内容不要仅因重命名 C# 类型而改变 Entry。需要稳定命名时，在注册注解上使用 `StableEntryStem`；除非兼容既有完整 ID，不要使用 `FullPublicEntry`，两者不能同时设置。
- 游戏内容文本使用原生本地化表。FBE 当前的目录约定为 `FBE/localization/zhs/<table>.json` 与 `FBE/localization/eng/<table>.json`，新增内容应同时提供中英文键。不要删除本地化文件中的注释。
- 新增内容的英文名称应保持简短，优先使用能准确表达概念的短名称，不要把完整中文名逐词扩写成冗长英文。
- 自定义资源放在 `FBE/images/`、`FBE/audio/` 等 PCK 资源目录中，代码使用 `res://FBE/...` 路径。只填写实际需要覆盖的资源，未覆盖部分保留原版行为。
- 如果不确定某项 RitsuLib API 是否存在、在两个目标版本间是否兼容，先检查项目实际引用的包版本、本地 RitsuLib 源码及 `STS2 source/` 的 `0.107.1`、`0.111.0` 分支；无法确认时退回原版模型能力，并用 `STS2_Stable` / `STS2_Beta` 隔离 ABI 差异。
- RitsuLib 的本地源码位于 [`../Thirdparty Mods/STS2-RitsuLib-main/`](../Thirdparty%20Mods/STS2-RitsuLib-main/)；使用其 API 前优先查阅该目录下的 `src/` 与 `docs/`。常用入口包括 `ModRelicTemplate`、`ModCardTemplate` 与 `ModPowerTemplate`。
- FBE 自带音效优先使用现有的 `FBE.Scripts.Utils.AudioHelper`：短音效调用 `AudioHelper.Play("res://FBE/audio/example.ogg")`，循环音效调用 `AudioHelper.PlayLoop(...)`，结束循环时调用 `AudioHelper.StopLoop(...)`。
- 所有内容必须支持联机。涉及随机数、玩家选择、状态修改和异步命令时，应使用游戏提供的同步上下文、RNG 与命令系统，谨慎处理联机同步。
- 对于 `CanonicalVars`，一般整数变量优先使用 `IntVar`，不要无理由使用基础 `DynamicVar`。
- 尽量将数值放到CanonicalVars，例如造成`4`点伤害，抽`2`张牌
- 在继承原版、RitsuLib 或 FBE 类型时，不要无意间声明与基类成员同名的字段、常量、属性或方法。优先换成不会冲突的名称；确实需要隐藏基类成员时，必须显式添加 `new`，避免产生 CS0108 警告。

## 遗物

- FBE 的自定义遗物默认继承 `FBERelicModel`，不要直接继承 `ModRelicTemplate`，也不要为单个遗物再增加一层空基类。
- `FBERelicModel` 位于 `FBE.Scripts.Relics`，本身继承 RitsuLib 的 `STS2RitsuLib.Scaffolding.Content.ModRelicTemplate`，后者继承原版 `MegaCrit.Sts2.Core.Models.RelicModel`。
- 推荐的最小结构如下；遗物池类型应替换为实际使用的池：

```csharp
using FBE.Scripts.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class ExampleRelic : FBERelicModel
{
}
```

- 继承 `FBERelicModel` 不等于自动进入遗物池。必须在具体遗物类上添加 `[RegisterRelic(typeof(...))]`；`Entry.Init()` 已调用 `ModTypeDiscoveryHub.RegisterModAssembly` 来发现这些类型。
- `FBERelicModel` 默认从 `res://FBE/images/relics/<C# 类型名>.png` 加载普通、轮廓和大图标。常规遗物只需放置与类型同名的 PNG；只有资源名或图标规格不同才覆盖 `CustomIconPath`、`CustomIconOutlinePath`、`CustomBigIconPath` 或 `AssetProfile`。
- 可以使用已经从 RitsuLib 源码确认的模板能力：`AssetProfile`、`RegisteredKeywordIds`、`AdditionalHoverTips` 和 `IncludeEnergyHoverTip`。不要为了使用模板而强行引入不需要的便利功能。
- 遗物的费用、稀有度、触发时机、战斗逻辑、存档状态等核心行为，优先按照对应游戏版本的原版 `RelicModel` API 实现。
- 只有多个 FBE 遗物确实出现稳定、项目专属的重复逻辑时，才考虑扩充 `FBERelicModel`；不要另建一次性封装。

## 卡牌

- FBE 的自定义卡牌默认继承 `FBECardModel`，不要直接继承 `ModCardTemplate`，也不要为单张卡牌再增加一层空基类。
- `FBECardModel` 位于 `FBE.Scripts.Cards`，本身继承 RitsuLib 的 `STS2RitsuLib.Scaffolding.Content.ModCardTemplate`，后者继承原版 `MegaCrit.Sts2.Core.Models.CardModel`。
- 两个目标游戏版本中，基础构造参数均为：基础费用、`CardType`、`CardRarity`、`TargetType`、是否显示在卡牌图鉴。推荐的最小结构如下：

```csharp
using FBE.Scripts.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;

[RegisterCard(typeof(ColorlessCardPool))]
public sealed class ExampleCard()
    : FBECardModel(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
}
```

- 继承 `FBECardModel` 不等于自动进入卡池。必须用 `[RegisterCard(typeof(...))]` 指定实际卡池；角色牌使用对应角色的 `CardPoolModel`，所有角色可用的无色牌使用 `ColorlessCardPool`。不要为了方便把角色牌错误注册进无色卡池。
- 默认公开 Entry 示例：`ExampleCard` 对应 `FBE_CARD_EXAMPLE_CARD`。本地化写入 `FBE/localization/<语言>/cards.json`，至少提供 `.title` 与 `.description`。
- `FBECardModel` 默认从 `res://FBE/images/cards/<C# 类型名>.png` 加载卡图。常规卡牌只需放置与类型同名的 PNG；需要复用另一类型的图片时可重写 `PortraitOverride`。只有确实存在独立 beta 图、边框、费用图标、材质或 overlay 时，才使用 `CardAssetProfile` 填写对应字段。
- 卡牌核心行为优先使用原版 `CardModel` API：
  - 用构造参数声明基础费用、类型、稀有度与目标类型。
  - 用 `CanonicalVars` 声明 `DamageVar`、`BlockVar`、`IntVar`、`EnergyVar` 等动态变量，并在本地化描述中引用同名占位符。
  - 只要 Canonical Var 是 `PowerVar<TPower>`，本地化占位符键就与 Power 的 C# 类名完全相同，即 `typeof(TPower).Name`，必须保留 `Power` 后缀。例如 `new PowerVar<DoomPower>(...)` 的文本占位符是 `{DoomPower}` / `{DoomPower:diff()}`，不是 `{Doom}`；即使代码中通过 `DynamicVars.Doom` 访问，也不能把代码访问名当成本地化键。
  - 用 `OnPlay(PlayerChoiceContext, CardPlay)` 实现打出效果，优先调用 `CardCmd`、`CreatureCmd`、`PowerCmd`、`PlayerCmd` 等原版命令，不直接绕过命令系统修改战斗状态。
  - 用 `OnUpgrade()` 实现升级；数值使用 `DynamicVars.<变量>.UpgradeValueBy(...)`，费用使用 `EnergyCost.UpgradeBy(...)`。
  - 原版关键词与标签分别重写 `CanonicalKeywords`、`CanonicalTags`。不要使用 RitsuLib 已标记过时的 `RegisteredKeywordIds`、`RegisteredCardTagIds`；自定义关键词或标签应先通过 RitsuLib 注册，再转换成 `CardKeyword` / `CardTag`。
- `AdditionalHoverTips`、`AssetProfile` 及手牌高亮/轮廓注册是可选便利能力。没有明确需求时不要引入材质、overlay、全局卡牌类型文本修改器或额外 UI 注册。
- 如果卡牌带有“不能被打出”的效果，通常不需要在本地化描述中重复写出，因为游戏会自动渲染“无法被打出”。
- 只有多个 FBE 卡牌确实出现稳定、项目专属的重复逻辑时，才考虑扩充 `FBECardModel`；不要另建一次性封装。

## Power

- FBE 的自定义 Power 默认继承 `FBEPowerModel`，不要直接继承 `ModPowerTemplate`，也不要为单个 Power 再增加一层空基类。
- `FBEPowerModel` 位于 `FBE.Scripts.Powers`，本身继承 RitsuLib 的 `STS2RitsuLib.Scaffolding.Content.ModPowerTemplate`，后者继承原版 `MegaCrit.Sts2.Core.Models.PowerModel`。
- Power 是独立模型，不属于池。必须在具体类型上添加 `[RegisterPower]`，然后由能力牌或其他内容通过 `PowerCmd.Apply<TPower>(choiceContext, target, amount, applier, cardSource)` 施加。
- 推荐的最小结构如下：

```csharp
using FBE.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

[RegisterPower]
public sealed class ExamplePower : FBEPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}
```

- `PowerType` 决定 Buff/Debuff 分类；`PowerStackType.Counter` 显示并叠加层数，`PowerStackType.Single` 表示不显示层数的单实例效果。只有确实需要按施加者分别存在或允许多个实例时，才重写 `InstanceType`。
- 默认公开 Entry 示例：`ExamplePower` 对应 `FBE_POWER_EXAMPLE_POWER`。本地化写入 `FBE/localization/<语言>/powers.json`，至少提供 `.title` 与 `.description`；原版描述管线自动提供 `{Amount}` 等 Power 参数。
- `FBEPowerModel` 默认从 `res://FBE/images/powers/<C# 类型名>.png` 加载普通和大图标。常规 Power 只需放置与类型同名的透明背景 PNG；只有资源名或规格不同才覆盖 `CustomIconPath`、`CustomBigIconPath` 或 `PowerAssetProfile`。
- 如果一个能力基本上是一张能力牌的实现细节或“转发 Power”，卡牌描述应直接写出最终效果，而不是只写“获得某某能力”。这种纯实现细节 Power 不应添加到卡牌 Hovertip；只有其名称或机制需要玩家单独理解时，才通过 `AdditionalHoverTips` 添加 `HoverTipFactory.FromPower<TPower>()`。
- 能力牌通常用 `PowerVar<TPower>` 保存施加量。其本地化占位符必须使用完整 C# 类型名并保留 `Power` 后缀。
- Power 的核心逻辑优先使用原版 `PowerModel` Hook。修改型 Hook 应只返回修改量，不在计算阶段播放动画或改变战斗状态；需要反馈或后续行为时，配套实现对应的 `AfterModifying...` Hook。
- `ModifyPowerAmountGivenAdditive` 的返回值是要加到当前施加量上的数值，而不是最终值。例如返回传入的 `amount` 会使该次 Power 附加量翻倍。只有该 Hook 实际返回非零修改时，该模型才会收到 `AfterModifyingPowerAmountGiven`，适合在其中调用 `Flash()`。
- 是否影响玩家、敌人或特定施加者，应通过 Hook 参数中的 `giver`、`target`、`power`、`cardSource` 明确过滤；需要全场生效时不要按 Power 自身的 `Owner` 过滤。
- `AssetProfile`、`AdditionalHoverTips`、`RegisteredKeywordIds` 与 `IncludeEnergyHoverTip` 只是可选便利能力。类型、层数、生命周期、战斗 Hook 和多人行为仍以原版 `PowerModel` 为准。
- 只有多个 FBE Power 确实出现稳定、项目专属的重复逻辑时，才考虑扩充 `FBEPowerModel`；不要另建一次性封装。
