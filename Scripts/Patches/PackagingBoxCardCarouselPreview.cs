using FBE.Scripts.Cards;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace FBE.Scripts.Patches;

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.CreateAndShow),
	[typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment)])]
internal static class PackagingBoxCardCarouselPreviewPatch
{
	private static void Postfix(Control owner, NHoverTipSet? __result)
	{
		if (__result is null || owner is not NCardHolder holder || holder.CardModel is not PackagingBoxCard box)
			return;

		var storedCards = box.StoredCards.Where(card => card.Id is not null).ToList();
		if (storedCards.Count == 0)
			return;

		var cardContainer = __result.GetNodeOrNull<NHoverTipCardContainer>("cardHoverTipContainer");
		if (cardContainer is null)
			return;

		var firstCard = CardModel.FromSerializable(storedCards[0]);
		cardContainer.Add((CardHoverTip)HoverTipFactory.FromCard(firstCard));

		var previewTip = cardContainer.GetChild<Control>(cardContainer.GetChildCount() - 1);
		var previewCard = previewTip.GetNode<NCard>("%Card");
		__result.AddChild(new PackagingBoxCardCarouselPreview(storedCards, previewCard));
	}
}

internal sealed partial class PackagingBoxCardCarouselPreview : Node
{
	private const double SwitchInterval = 0.66;

	private readonly List<SerializableCard> _storedCards;
	private readonly NCard _previewCard;
	private double _elapsed;
	private int _currentIndex;

	public PackagingBoxCardCarouselPreview(IEnumerable<SerializableCard> storedCards, NCard previewCard)
	{
		_storedCards = storedCards.ToList();
		_previewCard = previewCard;
	}

	public override void _Process(double delta)
	{
		_elapsed += delta;
		if (_elapsed < SwitchInterval)
			return;

		_elapsed %= SwitchInterval;
		_currentIndex = (_currentIndex + 1) % _storedCards.Count;
		_previewCard.Model = CardModel.FromSerializable(_storedCards[_currentIndex]);
		_previewCard.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
	}
}
