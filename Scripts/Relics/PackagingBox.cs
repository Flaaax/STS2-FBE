using FBE.Scripts.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
public sealed class PackagingBox : FBERelicModel
{
	public override RelicRarity Rarity => RelicRarity.Ancient;

	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
	[
		HoverTipFactory.FromCard<PackagingBoxCard>(upgrade: true)
	];

	public override async Task AfterObtained()
	{
		var packagingBox = Owner.RunState.CreateCard<PackagingBoxCard>(Owner);
		CardCmd.Upgrade(packagingBox, CardPreviewStyle.None);
		CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(packagingBox, PileType.Deck));
	}
}
