using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Monsters;

/// <summary>教条首领遭遇。章节归属暂不限制，之后可按设计调整。</summary>
[RegisterGlobalEncounter]
public sealed class DogmaEncounter : ModEncounterTemplate
{
	private const string TvSlot = "dogmaTv";
	private const string DogmaSlot = "dogma";

	public override RoomType RoomType => RoomType.Boss;

	public override IEnumerable<MonsterModel> AllPossibleMonsters =>
		[ModelDb.Monster<DogmaTv>(), ModelDb.Monster<Dogma>()];

	public override IReadOnlyList<string> Slots => [TvSlot, DogmaSlot];
	public override string? CustomEncounterScenePath => "res://FBE/scenes/encounters/dogma_encounter.tscn";
	// 使用原版 Boss 地图节点的 PNG 兜底分支，而非不存在的 Spine .tres。
	public override string? CustomBossNodePath => "res://FBE/images/map/placeholder/fbe_encounter_dogma_encounter_icon";
	public override string? CustomRunHistoryIconPath => "res://FBE/images/ui/run_history/fbe_encounter_dogma_encounter.png";
	public override string? CustomRunHistoryIconOutlinePath =>
		"res://FBE/images/ui/run_history/fbe_encounter_dogma_encounter_outline.png";

	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		return [(ModelDb.Monster<DogmaTv>().ToMutable(), TvSlot), (ModelDb.Monster<Dogma>().ToMutable(), DogmaSlot)];
	}
}
