using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Cards;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(ColorlessCardPool))]
public class GiantKiller() : FBECardModel(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DamageVar(4m, ValueProp.Move),
		new IntVar("Multiplier", 10m)
	];

	protected override bool ShouldGlowGoldInternal =>
		CombatState?.HittableEnemies.Any(IsCriticalTarget) ?? false;

	// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
	private int ModifiedHealth => HasExactHealth
		? Owner.Creature.CurrentHp * Owner.RunState.Players.Count
		: 0;

	// Card-library previews are mutable clones, but they do not have an owner.
	// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
	private bool HasExactHealth => IsMutable && Owner is not null;

	private bool IsCriticalTarget(Creature? target) =>
		target is not null && target.CurrentHp > ModifiedHealth;

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);
		AudioHelper.Play("res://FBE/audio/giant_killer.ogg");
		var sfx = IsCriticalTarget(cardPlay.Target)
			? "res://FBE/audio/hit_crit.ogg"
			: null;
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
#if STS2_Stable
	        .FromCard(this)
#else
			.FromCard(this, cardPlay)
#endif
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt", sfx, "blunt_attack.mp3")
			.Execute(choiceContext);
	}

#if STS2_Stable
	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
		Creature? dealer, CardModel? cardSource)
#else
	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
		Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
#endif
	{
		if (cardSource == this && IsCriticalTarget(target))
			return DynamicVars["Multiplier"].BaseValue;

		return 1m;
	}

	public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
	{
		if (creature != Owner.Creature) return Task.CompletedTask;
		var cardNode = NCard.FindOnTable(this);
		if (Pile != null)
		{
			cardNode?.UpdateVisuals(Pile.Type, CardPreviewMode.Normal);
		}
		else
		{
			Entry.Log.Warn("This is unexpected...");
		}

		return Task.CompletedTask;
	}

	protected override void OnUpgrade()
	{
		DynamicVars["Multiplier"].UpgradeValueBy(4m);
	}

	protected override void AddExtraArgsToDescription(LocString description)
	{
		description.Add("HasExactHealth", HasExactHealth);
		description.Add("ModifiedHealth", ModifiedHealth);
	}
}
