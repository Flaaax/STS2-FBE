using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

namespace FBE.Scripts.Cards;

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

[Pool(typeof(NecrobinderCardPool))]
public class DeadTissue() : FBECardModel(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new OstyDamageVar(5m, ValueProp.Move),
		new SummonVar(5m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
		[HoverTipFactory.Static(StaticHoverTip.SummonDynamic, base.DynamicVars.Summon)];

	protected override HashSet<CardTag> CanonicalTags => [CardTag.OstyAttack];


	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);
		if (!Osty.CheckMissingWithAnim(Owner))
		{
			await DamageCmd.Attack(DynamicVars.OstyDamage.BaseValue)
#if STS2_Stable
				.FromOsty(Owner.Osty!, this)
#else
				.FromOsty(Owner.Osty!, this, cardPlay)
#endif
				.Targeting(cardPlay.Target)
				.WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
				.Execute(choiceContext);
		}

		await OstyCmd.Summon(choiceContext, Owner, DynamicVars.Summon.BaseValue, this);
	}

	protected override void OnUpgrade()
	{
		DynamicVars.OstyDamage.UpgradeValueBy(2m);
		DynamicVars.Summon.UpgradeValueBy(2m);
	}
}
