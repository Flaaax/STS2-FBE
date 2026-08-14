using FBE.Scripts.Powers;
using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using FileAccess = Godot.FileAccess;

namespace FBE.Scripts.Cards;

[Pool(typeof(SilentCardPool))]
public class Industry() : FBECardModel(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DamageVar(14m, ValueProp.Move),
		new CardsVar(10)
	];


	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
#if STS2_Stable
			.FromCard(this)
#else
			.FromCard(this, cardPlay)
#endif
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash", null, "heavy_attack.mp3")
			.Execute(choiceContext);

		var maxDraw = 10 - base.Owner.PlayerCombatState!.Hand.Cards.Count;
		var draw = Math.Min(maxDraw, DynamicVars.Cards.IntValue);

		var cards = await CardPileCmd.Draw(choiceContext, draw, Owner);
		await CardCmd.Discard(choiceContext, cards);
	}

	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(5);
	}
}
