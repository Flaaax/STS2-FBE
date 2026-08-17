using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Monsters;

/// <summary>
/// 将 DoorMonster 加入每个章节的精英候选池。
/// </summary>
[RegisterGlobalEncounter]
public class DoorMonsterEncounter : ModEncounterTemplate
{
	// 改为 false 后，RitsuLib 会在生成章节遭遇池时排除此精英。
	public const bool CanAppear = false;

	public override RoomType RoomType => RoomType.Elite;

	public override IEnumerable<MonsterModel> AllPossibleMonsters =>
		[ModelDb.Monster<DoorMonster>()];

	public override bool IsValidForAct(ActModel act)
	{
		return CanAppear;
	}

	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		return [(ModelDb.Monster<DoorMonster>().ToMutable(), null)];
	}
}
