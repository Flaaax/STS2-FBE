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

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(NecrobinderCardPool))]
public class BloodFeud() : FBECardModel(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
	protected override bool ShouldGlowRedInternal => Owner.IsOstyMissing;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new OstyDamageVar(6m, ValueProp.Move),
		new RepeatVar(2)
	];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<DoomPower>()];

	protected override HashSet<CardTag> CanonicalTags => [CardTag.OstyAttack];


	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);
		if (!Osty.CheckMissingWithAnim(Owner))
		{
			var hitCount = !cardPlay.Target.HasPower<DoomPower>() ? 1 : (DynamicVars.Repeat.IntValue + 1);
			await DamageCmd.Attack(DynamicVars.OstyDamage.BaseValue)
#if STS2_Stable
				.FromOsty(Owner.Osty!, this)
#else
				.FromOsty(Owner.Osty!, this, cardPlay)
#endif
				.WithHitCount(hitCount)
				.Targeting(cardPlay.Target)
				.WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
				.Execute(choiceContext);
		}
	}

	protected override void OnUpgrade()
	{
		DynamicVars.Repeat.UpgradeValueBy(1m);
	}
}
