using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Monsters;

/// <summary>
/// 将 DoorMonster 加入所有第二幕的精英候选池。
/// </summary>
[RegisterGlobalEncounter]
public class DoorMonsterEncounter : ModEncounterTemplate
{
	public override RoomType RoomType => RoomType.Elite;

	public override bool IsValidForAct(ActModel act) => act.Index == 1;

	public override IEnumerable<MonsterModel> AllPossibleMonsters =>
		[ModelDb.Monster<DoorMonster>()];

	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		return [(ModelDb.Monster<DoorMonster>().ToMutable(), null)];
	}
}
