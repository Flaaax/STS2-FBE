using FBE.Scripts.Relics;
using Godot;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Ancients;

[RegisterActAncient(typeof(Hive))]
public class TaintedCain : ModAncientEventTemplate
{
	// 选项按钮颜色
	public override Color ButtonColor => new(0.12f, 0.2f, 0.8f, 0.5f);

	// 对话框颜色
	public override Color DialogueColor => new(0.12f, 0.2f, 0.8f);

	// 自定义场景的路径
	public override EventAssetProfile AssetProfile => new(
		BackgroundScenePath: "res://FBE/scenes/TaintedCain.tscn"
	);

	private const string MapPortrait = "res://FBE/images/ancients/TaintedCainHead.png";

	// 对于图片，只要是godot支持的格式都可以，例如png,jpg,svg等等
	// 自定义地图图标和轮廓的路径
	public override AncientEventPresentationAssetProfile AncientPresentationAssetProfile => new(
		MapIconPath: MapPortrait,
		MapIconOutlinePath: MapPortrait,
		RunHistoryIconPath: MapPortrait,
		RunHistoryIconOutlinePath: MapPortrait
	);

	// 固定池一和二
	private IReadOnlyList<EventOption> Pool1 =>
	[
		CreateModRelicOption<DarkBum>(),
		CreateModRelicOption<AnarchistCookbook>(),
		CreateModRelicOption<Clicker>(),
	];

	private IReadOnlyList<EventOption> Pool2 =>
	[
		CreateModRelicOption<StarterDeck>(),
	];

	// 带权重池三。权重越大越有机会生成。当然你也可以写自定义的列表生成函数
	private IReadOnlyList<EventOption> Pool3 =>
	[
		CreateModRelicOption<LordsParasol>(),
		CreateModRelicOption<PackagingBox>(),
	];

	// 所有可能的选项
	public override IEnumerable<EventOption> AllPossibleOptions => [.. Pool1, .. Pool2, .. Pool3];

	// 生成选项
	protected override IReadOnlyList<EventOption> GenerateInitialOptions()
	{
		return
		[
			Rng.NextItem(Pool1)!,
			Rng.NextItem(Pool2)!,
			Rng.NextItem(Pool3)!,
			// Pool3.GetRandom(Rng),
		];
	}

	// 出现条件
	public override bool IsValidForAct(ActModel act) => act.Index == 1;
}
