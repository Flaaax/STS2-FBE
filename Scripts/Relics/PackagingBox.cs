using FBE.Scripts.Cards;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
public sealed class PackagingBox : FBERelicModel
{
	public override RelicRarity Rarity => RelicRarity.Ancient;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(5)
	];

	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
	[
		HoverTipFactory.FromCard<PackagingBoxCard>(upgrade: true)
	];

	public override async Task AfterObtained()
	{
		var maxStoredCards = DynamicVars["Cards"].IntValue;
		var selectedCards = (await CardSelectCmd.FromDeckForRemoval(
			Owner,
			new CardSelectorPrefs(L10NLookup("FBE_RELIC_PACKAGING_BOX.selectionScreenPrompt"), 0, maxStoredCards),
			card => card is not PackagingBoxCard)).ToList();

		// The selector filter prevents this in normal play. Keep this check so an invalid
		// selection can never put a Packaging Box inside another Packaging Box.
		var cardsToStore = selectedCards
			.Where(card => card is not PackagingBoxCard)
			.ToList();

		var packagingBox = Owner.RunState.CreateCard<PackagingBoxCard>(Owner);
		CardCmd.Upgrade(packagingBox, CardPreviewStyle.None);
		foreach (var card in cardsToStore)
		{
			packagingBox.AddStoredCard(card);
		}

		// Deck selection is synchronized by CardSelectCmd. Snapshot the selected cards
		// before removing them, then use the normal deck command without its preview.
		await CardPileCmd.RemoveFromDeck(cardsToStore, showPreview: false);
		CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(packagingBox, PileType.Deck));
	}
}
