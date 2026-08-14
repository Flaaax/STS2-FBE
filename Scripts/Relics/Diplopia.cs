using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
class Diplopia : FBERelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task AfterObtained()
    {
	    var originalDeckSize = Owner.Deck.Cards.Count;
	    for (var i = 0; i < originalDeckSize; i++)
	    {
		    var card = Owner.RunState.CloneCard(Owner.Deck.Cards[i]);
		    CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 1.2f, CardPreviewStyle.MessyLayout);
		    await Cmd.CustomScaledWait(0.1f, 0.2f);
	    }
	    await Cmd.CustomScaledWait(0.6f, 1.2f);
    }
}
