using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
public sealed class EdensBlessing : FBERelicModel
{
	public override RelicRarity Rarity => RelicRarity.Ancient;

	public override bool HasUponPickupEffect => true;

	public override async Task AfterObtained()
	{
		var options = CardCreationOptions.ForNonCombatWithUniformOdds(
			[ModelDb.CardPool<ColorlessCardPool>()]);
		var card = CardFactory.CreateForReward(Owner, 1, options).First().Card;
		CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck));
	}
}
