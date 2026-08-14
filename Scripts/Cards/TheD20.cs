using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace FBE.Scripts.Cards;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(ColorlessCardPool))]
public class TheD20() : FBECardModel(1, CardType.Skill, CardRarity.Rare, TargetType.None)
{
	private readonly TheD6Base _myBase = new();

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		//new IntVar("Selections", 2),
		new IntVar("MinRange", 1),
		new IntVar("MaxRange", 20)
	];

	// name, value, modifier

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await _myBase.OnPlay(choiceContext, cardPlay, this);
	}

	protected override void OnUpgrade()
	{
		EnergyCost.UpgradeBy(-1);
	}
}
