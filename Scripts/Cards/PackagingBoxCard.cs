using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace FBE.Scripts.Cards;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(EventCardPool))]
public sealed class PackagingBoxCard() : FBECardModel(1, CardType.Skill, CardRarity.Ancient, TargetType.None)
{
	private List<SerializableCard> _storedCards = [];	// 用于动态卡牌预览
	private List<CardModel> _combatStoredCards = [];

	public override IEnumerable<CardKeyword> CanonicalKeywords =>
	[
		CardKeyword.Retain
	];

	public List<SerializableCard> StoredCards
	{
		get => _storedCards;
		private set
		{
			AssertMutable();
			_storedCards.Clear();
			_storedCards.AddRange(value);
		}
	}

	public override Task BeforeCombatStart()
	{
		// Contents are combat-only. This also discards any contents left by an older save.
		_combatStoredCards.Clear();
		StoredCards = [];
		return Task.CompletedTask;
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		var handCards = PileType.Hand.GetPile(Owner).Cards.ToList();
		var cardsToUnpack = _combatStoredCards.ToList();

		// Stored cards deliberately have no game pile. Remove their hand nodes as well as
		// their models from the Hand pile; RemoveFromCurrentPile alone leaves orphaned
		// NHandCardHolder nodes and creates visible gaps in the local hand.
		var localHand = NCombatRoom.Instance?.Ui.Hand;
		foreach (var card in handCards)
		{
			var cardNode = localHand?.GetCard(card);
			if (cardNode is not null)
			{
				localHand!.Remove(card);
				cardNode.QueueFreeSafely();
			}

			card.RemoveFromCurrentPile();
		}

		// Use the ordinary pile command for cards that were already in combat. In particular,
		// do not deserialize or use AddGeneratedCardsToCombat here: neither operation should
		// count as generating a card or trigger AfterCardGeneratedForCombat.
		await CardPileCmd.Add(cardsToUnpack, PileType.Hand);

		_combatStoredCards.Clear();
		_combatStoredCards.AddRange(handCards);
		StoredCards = handCards.Select(card => card.ToSerializable()).ToList();
	}

#if STS2_Stable
	protected override PileType GetResultPileTypeForCardPlay()
	{
		var result = base.GetResultPileTypeForCardPlay();
		return result == PileType.Discard ? PileType.Hand : result;
	}
#elif STS2_Beta
	protected override CardLocation GetResultLocationForCardPlay()
	{
		var result = base.GetResultLocationForCardPlay();
		if (result.pileType == PileType.Discard)
		{
			result.pileType = PileType.Hand;
			result.position = CardPilePosition.Top;
		}

		return result;
	}
#endif

	protected override void OnUpgrade()
	{
		EnergyCost.UpgradeBy(-1);
		AddKeyword(CardKeyword.Innate);
	}

	protected override void DeepCloneFields()
	{
		base.DeepCloneFields();
		_storedCards = _storedCards.ToList();
		// A clone must never share the original box's live card objects.
		_combatStoredCards = [];
	}
}