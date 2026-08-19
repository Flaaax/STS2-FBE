using FBECore.Scripts.Keywords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace FBE.Scripts.Cards;

/// <summary>
/// A status that drains energy whenever its owner plays another card while it is held.
/// </summary>
[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(StatusCardPool))]
public sealed class Grasp() : FBECardModel(1, CardType.Status, CardRarity.Status, TargetType.None)
{
	public override int MaxUpgradeLevel => 0;

	public override bool CanBeGeneratedInCombat => false;
	public override bool CanBeGeneratedByModifiers => false;

	public override IEnumerable<CardKeyword> CanonicalKeywords =>
	[
		CardKeyword.Ethereal,
		FBECoreKeywords.WhileInHand
	];

	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (Pile?.Type != PileType.Hand || cardPlay.Card.Owner != Owner)
		{
			return;
		}

		await PlayerCmd.LoseEnergy(1, Owner);
	}
}