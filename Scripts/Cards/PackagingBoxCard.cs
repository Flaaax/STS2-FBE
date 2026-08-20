using FBECore.Scripts.Keywords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace FBE.Scripts.Cards;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(EventCardPool))]
public sealed class PackagingBoxCard() : FBECardModel(1, CardType.Skill, CardRarity.Ancient, TargetType.None)
{
	private const bool RetainContentsOnEtherealExhaust = true;

	private List<SerializableCard> _storedCards = [];

	public override IEnumerable<CardKeyword> CanonicalKeywords =>
	[
		FBECoreKeywords.Afterlife,
		FBECoreKeywords.Fleeting,
		CardKeyword.Exhaust,
		CardKeyword.Ethereal,
		CardKeyword.Eternal
	];

	[SavedProperty]
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

	/// <summary>
	/// Stores a card snapshot. The caller owns selecting and removing the source card from the deck.
	/// </summary>
	public void AddStoredCard(CardModel card)
	{
		AssertMutable();
		_storedCards.Add(card.ToSerializable());
	}

	public override async Task AfterCardExhausted(
		PlayerChoiceContext choiceContext,
		CardModel card,
		bool causedByEthereal)
	{
		if (card != this)
			return;

		var storedCards = _storedCards.Where(card => card.Id is not null).ToList();
		_storedCards.Clear();

		var generatedCards = storedCards.Select(CardModel.FromSerializable).ToList();
		foreach (var generatedCard in generatedCards)
		{
			CombatState!.AddCard(generatedCard, Owner);
		}

		if (RetainContentsOnEtherealExhaust && causedByEthereal)
		{
			foreach (var generatedCard in generatedCards)
			{
				generatedCard.GiveSingleTurnRetain();
			}
		}

		await CardPileCmd.AddGeneratedCardsToCombat(generatedCards, PileType.Hand, Owner);
	}

	protected override void OnUpgrade()
	{
		EnergyCost.UpgradeBy(-1);
	}

	protected override void DeepCloneFields()
	{
		base.DeepCloneFields();
		_storedCards = _storedCards.ToList();
	}

	protected override void AddExtraArgsToDescription(LocString description)
	{
		var cardTitleSeparator = new LocString("cards", "FBE_CARD_PACKAGING_BOX_CARD.separator")
			.GetFormattedText();
		var storedCardTitles = _storedCards
			.Select(card => card.Id is { } id ? "[gold]" + SaveUtil.CardOrDeprecated(id).Title + "[/gold]" : null)
			.OfType<string>();
		description.Add("StoredCardTitles", string.Join(cardTitleSeparator, storedCardTitles));
		description.Add("HasStoredCards", _storedCards.Any(card => card.Id is not null) ? 1m : 0m);
		description.Add("InRun", RunState is not null);
	}
}
