using FBE.Scripts.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
public sealed class DiceBag : FBERelicModel
{
	public override RelicRarity Rarity => RelicRarity.Ancient;

	protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
	[
		HoverTipFactory.FromCard<TheD20>()
	];

	public override async Task AfterObtained()
	{
		var d20 = Owner.RunState.CreateCard<TheD20>(Owner);
		CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(d20, PileType.Deck));
	}
}
