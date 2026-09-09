using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
public sealed class PlanC : FBERelicModel
{
	private bool _usedThisCombat;
	private bool _pendingVakuuTakeover;

	public override RelicRarity Rarity => RelicRarity.Ancient;

	private bool UsedThisCombat
	{
		get => _usedThisCombat;
		set
		{
			AssertMutable();
			_usedThisCombat = value;
		}
	}

	private bool PendingVakuuTakeover
	{
		get => _pendingVakuuTakeover;
		set
		{
			AssertMutable();
			_pendingVakuuTakeover = value;
		}
	}

	public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
		ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (CombatManager.Instance.IsInProgress && target == Owner.Creature && result.UnblockedDamage > 0 &&
			!UsedThisCombat)
		{
			UsedThisCombat = true;
			PendingVakuuTakeover = true;
			Status = RelicStatus.Active;
			Flash();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterAutoPrePlayPhaseEnteredLate(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || !PendingVakuuTakeover)
		{
			return;
		}

		// 在 await 前消费标记，避免接管期间的嵌套伤害再次安排该效果。
		PendingVakuuTakeover = false;
		Status = RelicStatus.Normal;

		await VakuuTakeover.Run(choiceContext, player);
		if (CombatManager.Instance.IsInProgress && !player.Creature.IsDead)
		{
			await FakePersonalTurn.Run(choiceContext, player);
		}
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		UsedThisCombat = false;
		PendingVakuuTakeover = false;
		Status = RelicStatus.Normal;
		return Task.CompletedTask;
	}
}
