using System.Diagnostics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;

namespace FBE.Scripts.Events;

public sealed class StrangeRoadSign : FBEEventModel
{
	private const int CardsToRemove = 2;
	private const int RewardChoices = 3;
	private const string CursesVar = "Curses";
	private const int CursesToAdd = 2;
	private const string FbeCurseEntryPrefix = "FBE-";
	private const string TiebaDiyCurseEntryPrefix = "TIEBA_DIY_CARD_";
	private static readonly string BlockHoverTipId = HoverTipFactory.Static(StaticHoverTip.Block).Id;

	// 背景图位置
	public override string CustomInitialPortraitPath => "res://FBE/images/events/WierdGuidepost.png";

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new GoldVar(0),
		new IntVar(CursesVar, CursesToAdd)
	];

	public override void CalculateVars()
	{
		DynamicVars.Gold.BaseValue = Rng.NextInt(370, 431);
	}

	public override bool IsAllowed(IRunState runState)
	{
		return runState.Players.All(player =>
			player.Deck.Cards.Count(card => card.IsRemovable && IsBlockCard(card)) >= CardsToRemove &&
			player.Deck.Cards.Count(card => card.IsRemovable && card.Type == CardType.Attack) >= CardsToRemove);
	}

	protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
	[
		Option(GoAttack),
		Option(GoDefend),
		Option(RemoveSign)
	];

	private async Task GoAttack()
	{
		Debug.Assert(Owner != null, nameof(Owner) + " != null");
		var cardsToRemove = (await CardSelectCmd.FromDeckForRemoval(
			prefs: new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, CardsToRemove), player: Owner,
			filter: IsBlockCard)).ToList();

		var pools = ModelDb.AllCharacterCardPools.ToList();
		pools.Add(ModelDb.CardPool<ColorlessCardPool>());

		var options = CardCreationOptions.ForNonCombatWithUniformOdds(pools, c => c.Tags.Contains(CardTag.Strike));
		var cards = CardFactory.CreateForReward(Owner, RewardChoices, options).ToList();
		foreach (var item in await CardSelectCmd.FromSimpleGridForRewards(
			         prefs: new CardSelectorPrefs(
				         L10NLookup("FBE-STRANGE_ROAD_SIGN.pages.GO_ATTACK.selectionScreenPrompt"),
				         1), context: new BlockingPlayerChoiceContext(), cards: cards, player: Owner))
		{
			CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(item, PileType.Deck));
		}

		await CardPileCmd.RemoveFromDeck(cardsToRemove);

		// var options =
		//     CardCreationOptions.ForNonCombatWithDefaultOdds(pools, c => c.Type == CardType.Attack);
		// var cardModel = CardFactory.CreateForReward(Owner, 1, options).FirstOrDefault()?.Card;
		// if (cardModel != null)
		// {
		//     CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(cardModel, PileType.Deck), 1.2f,
		//         CardPreviewStyle.EventLayout);
		// }

		SetEventFinished(PageDescription("GO_ATTACK_CHOSEN"));
	}

	private async Task GoDefend()
	{
		Debug.Assert(Owner != null, nameof(Owner) + " != null");
		var cardsToRemove = (await CardSelectCmd.FromDeckForRemoval(
			prefs: new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, CardsToRemove), player: Owner,
			filter: static card => card.Type == CardType.Attack)).ToList();

		var cards = CreateBlockRewardChoices();
		foreach (var item in await CardSelectCmd.FromSimpleGridForRewards(
			         prefs: new CardSelectorPrefs(
				         L10NLookup("FBE-STRANGE_ROAD_SIGN.pages.GO_DEFEND.selectionScreenPrompt"),
				         1), context: new BlockingPlayerChoiceContext(), cards: cards, player: Owner))
		{
			CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(item, PileType.Deck));
		}

		await CardPileCmd.RemoveFromDeck(cardsToRemove);

		SetEventFinished(PageDescription("GO_DEFEND_CHOSEN"));
	}

	private async Task RemoveSign()
	{
		await PlayerCmd.GainGold(DynamicVars.Gold.IntValue, Owner!);
		await CardPileCmd.AddCursesToDeck(SelectCurses(), Owner!);

		SetEventFinished(PageDescription("REMOVE_SIGN_CHOSEN"));
	}

	private List<CardModel> SelectCurses()
	{
		var remainingCurses = ModelDb.CardPool<CurseCardPool>()
			.GetUnlockedCards(Owner!.UnlockState, Owner.RunState.CardMultiplayerConstraint)
			.Where(static card => card.CanBeGeneratedByModifiers)
			.OrderBy(static card => card.Id)
			.ToList();
		var remainingSpecialCurses = remainingCurses
			.Where(IsFbeOrTiebaDiyCurse)
			.ToList();

		List<CardModel> selectedCurses = [];
		for (var i = 0; i < DynamicVars[CursesVar].IntValue && remainingCurses.Count > 0; i++)
		{
			var choseSpecialPool = Rng.NextInt(2) == 0;
			var candidates = choseSpecialPool && remainingSpecialCurses.Count > 0
				? remainingSpecialCurses
				: remainingCurses;
			var curse = Rng.NextItem(candidates);
			if (curse is null)
				break;

			selectedCurses.Add(curse);
			remainingCurses.Remove(curse);
			remainingSpecialCurses.Remove(curse);
		}

		return selectedCurses;
	}

	private static bool IsFbeOrTiebaDiyCurse(CardModel card)
	{
		var entry = card.Id.Entry;
		return entry.StartsWith(FbeCurseEntryPrefix, StringComparison.Ordinal) ||
		       entry.StartsWith(TiebaDiyCurseEntryPrefix, StringComparison.Ordinal);
	}

	private List<CardCreationResult> CreateBlockRewardChoices()
	{
		var owner = Owner!;
		var pools = ModelDb.AllCharacterCardPools.ToList();
		pools.Add(ModelDb.CardPool<ColorlessCardPool>());

		CardModel[] manuallyIncludedCards =
		[
			ModelDb.Card<BodySlam>(),
			ModelDb.Card<Entrench>(),
			ModelDb.Card<Barricade>()
		];

		var remainingCandidates = pools
			.SelectMany(pool => pool.GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint))
			.Where(IsBlockCard)
			.Concat(manuallyIncludedCards)
			.DistinctBy(card => card.Id)
			.OrderBy(card => card.Id)
			.ToList();

		List<CardCreationResult> choices = [];
		for (var i = 0; i < RewardChoices && remainingCandidates.Count > 0; i++)
		{
			var canonicalCard = Rng.NextItem(remainingCandidates);
			if (canonicalCard is null)
			{
				return choices;
			}

			remainingCandidates.Remove(canonicalCard);
			choices.Add(new CardCreationResult(owner.RunState.CreateCard(canonicalCard, owner)));
		}

		return choices;
	}

	private static bool IsBlockCard(CardModel card)
	{
		return card.Tags.Contains(CardTag.Defend) ||
		       card.HoverTips.Any(tip => tip.Id == BlockHoverTipId) ||
		       card.DynamicVars.Values.Any(variable => variable is BlockVar);
	}
}