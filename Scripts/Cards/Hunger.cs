using FBECore.Scripts.Keywords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace FBE.Scripts.Cards;

/// <summary>
/// A status that makes its owner's cards Exhaust for as long as it remains in their hand.
/// </summary>
[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(StatusCardPool))]
public sealed class Hunger() : FBECardModel(1, CardType.Status, CardRarity.Status, TargetType.None)
{
	public override int MaxUpgradeLevel => 0;

	// Keep this card out of random in-combat creation and modifier pools.
	public override bool CanBeGeneratedInCombat => false;
	public override bool CanBeGeneratedByModifiers => false;

	public override IEnumerable<CardKeyword> CanonicalKeywords =>
	[
		CardKeyword.Ethereal, FBECoreKeywords.WhileInHand,
	];

	public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
	{
		if (Pile?.Type != PileType.Hand || Owner != card.Owner)
		{
			return false;
		}

		return keywords.Add(CardKeyword.Exhaust);
	}
}