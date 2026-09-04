using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace FBE.Scripts.Cards;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(RegentCardPool))]
public sealed class SwordOil() : FBECardModel(0, CardType.Skill, CardRarity.Common, TargetType.None)
{
	public override int CanonicalStarCost => 1;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new ForgeVar(6)
	];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => HoverTipFactory.FromForge();

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
		await ForgeCmd.Forge(DynamicVars.Forge.IntValue, Owner, this);
	}

	protected override void OnUpgrade()
	{
		DynamicVars.Forge.UpgradeValueBy(3m);
	}
}
