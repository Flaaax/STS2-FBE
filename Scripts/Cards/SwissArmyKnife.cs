using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Cards;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(SilentCardPool))]
public sealed class SwissArmyKnife() : FBECardModel(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar("Shivs", 1),
		new PowerVar<PoisonPower>(2),
		new CardsVar("Discard", 1),
		new BlockVar(1, ValueProp.Move),
		new PowerVar<DexterityPower>(1)
	];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
	[
		HoverTipFactory.FromCard<Shiv>(),
		HoverTipFactory.FromPower<PoisonPower>(),
		HoverTipFactory.FromPower<DexterityPower>()
	];

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);

		await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
		await Shiv.CreateInHand(Owner, DynamicVars["Shivs"].IntValue, CombatState!);
		await PowerCmd.Apply<PoisonPower>(choiceContext, cardPlay.Target, DynamicVars[nameof(PoisonPower)].BaseValue,
			Owner.Creature, this);
		await CardCmd.Discard(choiceContext, await CardSelectCmd.FromHandForDiscard(choiceContext, Owner,
			new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, DynamicVars["Discard"].IntValue), null, this));
		await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
		await PowerCmd.Apply<AnticipatePower>(choiceContext, Owner.Creature,
			DynamicVars[nameof(DexterityPower)].BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		EnergyCost.UpgradeBy(-1);
	}
}
