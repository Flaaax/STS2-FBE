using FBE.Scripts.Powers;
using FBE.Scripts.Utils;
using FBE.Scripts.VFX;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Runs;

namespace FBE.Scripts.Cards;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(CurseCardPool))]
public class Years() : FBECardModel(-1, CardType.Curse, CardRarity.Curse, TargetType.Self)
{
	private const bool UseFakeExtraTurnInSingleplayer = true;

	//public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];
	public override int MaxUpgradeLevel => 0;

	protected override bool IsPlayable => _enabled;
	private bool UseFakeExtraTurn =>
		UseFakeExtraTurnInSingleplayer || !RunManager.Instance.IsSingleplayerOrFakeMultiplayer;

	private bool ShouldTrigger
	{
		get
		{
			if (CombatState == null)
				return false;

			if (UseFakeExtraTurn)
			{
				// CardPlaysStarted already contains the Years whose OnPlay is currently refreshing the hand. This
				// also makes another copy drawn by the nested turn-start hooks observe the same per-turn limit.
				return !CombatManager.Instance.History.CardPlaysStarted.Any(e =>
					e.CardPlay.Card is Years &&
					e.CardPlay.Card.Owner == Owner &&
					e.HappenedThisTurn(CombatState));
			}

			return !CombatManager.Instance.History.CardPlaysFinished.Any(e =>
				e.CardPlay.Card is Years &&
				e.CardPlay.Card.Owner == Owner &&
				(e.HappenedThisTurn(CombatState) || e.HappenedLastPlayerTurn(Owner)));
		}
	}

	private bool _enabled;

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		TimeWarpTurnEndVfx.Play();
		
		AudioHelper.Play("res://FBE/audio/SOTE_SFX_EndTurn_v2.ogg");
		AudioHelper.Play("res://FBE/audio/STS_SFX_TimeWarp_v2.ogg");
		
		if (!UseFakeExtraTurn)
		{
			await PowerCmd.Apply<ExtraTurnPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
			PlayerCmd.EndTurn(Owner, canBackOut: false);
		}
		else
		{
			await Cmd.Wait(0.25f);
			await MakeFakeExtraTurn(choiceContext);
		}
		
		await Cmd.Wait(0.25f);
	}

	/// <summary>
	/// Simulates the owner ending and starting a new personal turn without ending the current side turn.
	/// Side-turn hooks are intentionally excluded: in multiplayer, replaying them would also affect the other players
	/// and would no longer be equivalent to giving only this card's owner another chance to act.
	/// </summary>
	private async Task MakeFakeExtraTurn(PlayerChoiceContext choiceContext)
	{
		var combatState = Owner.Creature.CombatState!;
		var playerState = Owner.PlayerCombatState;

		await playerState!.OrbQueue.BeforeTurnEnd(choiceContext);
		if (CombatManager.Instance.IsOverOrEnding)
			return;

		var hand = PileType.Hand.GetPile(Owner);
		var turnEndCards = hand.Cards.Where(card => card.HasTurnEndInHandEffect).ToList();
		var etherealCards = hand.Cards
			.Where(card => !card.HasTurnEndInHandEffect &&
			               card.Keywords.Contains(CardKeyword.Ethereal) &&
			               Hook.ShouldEtherealTrigger(combatState, card))
			.ToList();

		foreach (var card in etherealCards)
			await CardCmd.Exhaust(choiceContext, card, causedByEthereal: true);

		foreach (var card in turnEndCards)
			await card.OnTurnEndInHandWrapper(choiceContext);

		if (CombatManager.Instance.IsOverOrEnding)
			return;

		await Hook.BeforeFlush(combatState, Owner);

		var cardsToFlush = new List<CardModel>();
		var cardsToRetain = new List<CardModel>();
		var shouldFlush = Hook.ShouldFlush(combatState, Owner);
		foreach (var card in hand.Cards)
		{
			if (!shouldFlush || card.ShouldRetainThisTurn)
				cardsToRetain.Add(card);
			else
				cardsToFlush.Add(card);
		}

		if (cardsToFlush.Count > 0)
			await CardPileCmd.Add(cardsToFlush, PileType.Discard);

		await Hook.AfterFlush(combatState, Owner, choiceContext, cardsToFlush, cardsToRetain);
		playerState.EndOfTurnCleanup();

		Owner.Creature.BeforeTurnStart(CombatSide.Player);
		await Owner.Creature.AfterTurnStart(CombatSide.Player);
		await Hook.AfterBlockCleared(combatState, Owner.Creature);

		if (Hook.ShouldPlayerResetEnergy(combatState, Owner))
		{
			SfxCmd.Play("event:/sfx/ui/gain_energy");
			playerState.ResetEnergy();
		}
		else
		{
			playerState.AddMaxEnergyToCurrent();
		}

		await Hook.AfterEnergyReset(combatState, Owner);
		await Hook.BeforeHandDraw(combatState, Owner, choiceContext);
		var handDraw = Hook.ModifyHandDraw(combatState, Owner, 5m, out var modifiers);
		await Hook.AfterModifyingHandDraw(combatState, modifiers);
		await CardPileCmd.Draw(choiceContext, handDraw, Owner, fromHandDraw: true);
		await Hook.AfterPlayerTurnStart(combatState, choiceContext, Owner);
		if (!CombatManager.Instance.IsOverOrEnding)
			await playerState.OrbQueue.AfterTurnStart(choiceContext);
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner)
			return;

		var c = CardPile.GetCards(player, PileType.Hand).FirstOrDefault(c => c == this);
		if (c == null || !ShouldTrigger)
		{
			_enabled = false;
			return;
		}

		await Cmd.Wait(0.25f);
		_enabled = true;
		await CardCmd.AutoPlay(choiceContext, this, null);
		//await CardPileHelper.AutoPlayFromHand(choiceContext, this);
		_enabled = false;
	}

	public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
	{
		return card != this || _enabled;
	}
}
