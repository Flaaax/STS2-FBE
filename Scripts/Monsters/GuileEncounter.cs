using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Monsters;

/// <summary>
/// 将古烈加入所有第一幕变种的强敌候选池。
/// </summary>
[RegisterGlobalEncounter]
public sealed class GuileEncounter : ModEncounterTemplate
{
	private const string SonicBoomSlot = "sonicBoom";
	private const string GuileSlot = "guile";

	// 普通战斗且不标记 IsWeak 时，原版会将遭遇归入强敌池。
	public override RoomType RoomType => RoomType.Monster;

	public override bool IsValidForAct(ActModel act) => act.Index == 0;

	public override IEnumerable<MonsterModel> AllPossibleMonsters =>
		[ModelDb.Monster<Guile>(), ModelDb.Monster<GuileSonicBoom>()];

	// 第2回合召唤时旧波动可能尚未自杀，需要容纳短暂共存的两个波动。
	public override IReadOnlyList<string> Slots => [SonicBoomSlot, "sonicBoomReserve", GuileSlot];

	public override string? CustomEncounterScenePath => "res://FBE/scenes/encounters/guile_encounter.tscn";

	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		// 波动由古烈首回合召唤；预留 sonicBoom 槽位供战斗中加入时定位。
		return [(ModelDb.Monster<Guile>().ToMutable(), GuileSlot)];
	}
}
