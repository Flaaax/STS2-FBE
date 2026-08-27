using FBE.Scripts.Afflictions;
using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;

namespace FBE.Scripts.Relics;

[RegisterRelic(typeof(EventRelicPool))]
public sealed class BrokenModem : FBERelicModel
{
	public override RelicRarity Rarity => RelicRarity.Ancient;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new EnergyVar(1),
	];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
		HoverTipFactory.FromAffliction<NoSignal>().Concat([HoverTipFactory.ForEnergy(this)]);

	public override async Task BeforeCombatStart()
	{
		NoSignal.CacheOverlayForCombat();
		Flash();
		foreach (var card in Owner.PlayerCombatState!.DrawPile.Cards.Take(10).ToArray())
		{
			await AfflictWithNoSignal(card);
		}
	}

	public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		if (card.Owner == Owner && Owner.PlayerCombatState!.TurnNumber == 1)
		{
			await AfflictWithNoSignal(card);
		}
	}

	public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == Owner && Owner.PlayerCombatState!.TurnNumber == 1)
		{
			AudioHelper.Play("res://FBE/audio/broken_modem_static.wav");

			// Only the cards that did not enter the first hand were marked ahead of time. Clear those temporary
			// markers now; cards drawn later are handled by AfterCardDrawn as usual.
			foreach (var card in Owner.PlayerCombatState.DrawPile.Cards.Where(card => card.Affliction is NoSignal)
				.ToArray())
			{
				CardCmd.ClearAffliction(card);
			}
		}

		return Task.CompletedTask;
	}

	public override decimal ModifyMaxEnergy(Player player, decimal amount)
	{
		if (player != Owner)
		{
			return amount;
		}
		return amount + DynamicVars.Energy.BaseValue;
	}

	public override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
		IEnumerable<Creature> participants)
	{
		if (participants.Contains(Owner.Creature) && Owner.PlayerCombatState!.TurnNumber == 1)
		{
			foreach (var card in Owner.PlayerCombatState.AllCards)
			{
				if (card.Affliction is NoSignal)
				{
					CardCmd.ClearAffliction(card);
				}
			}
		}

		return Task.CompletedTask;
	}

	private static Task AfflictWithNoSignal(CardModel card)
	{
		return card.Affliction == null
			? CardCmd.Afflict<NoSignal>(card, 1m)
			: Task.CompletedTask;
	}
}
