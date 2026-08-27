using FBE.Scripts.Monsters;
using FBE.Scripts.Utils;
using FBE.Scripts.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
class DarkBum : FBERelicModel
{
	public override RelicRarity Rarity => RelicRarity.Ancient;

	public override bool AddsPet => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new BlockVar(8m, ValueProp.Unpowered),
		new EnergyVar(1),
		new CardsVar(1),
		new PowerVar<StrengthPower>(2),
		new PowerVar<DexterityPower>(2),
		new PowerVar<DrawCardsNextTurnPower>(2),
		new PowerVar<EnergyNextTurnPower>(2),
		new PowerVar<ThornsPower>(6),
		new HealVar(6),
		new GoldVar(35),
		new PowerVar<FocusPower>(2),
		new SummonVar(8m),
	];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Exhaust)];

	private bool _usedThisTurn;

	private bool UsedThisTurn
	{
		get => _usedThisTurn;
		set
		{
			AssertMutable();
			_usedThisTurn = value;
		}
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
		IReadOnlyList<Creature> participants, ICombatState combatState)
	{
		if (!participants.Contains(Owner.Creature))
		{
			return Task.CompletedTask;
		}

		UsedThisTurn = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom _)
	{
		UsedThisTurn = false;
		return Task.CompletedTask;
	}

	// 保留随机奖励池，方便以后恢复 Dark Bum 的随机奖励效果。
	private List<(Func<PlayerChoiceContext, Task> Effect, decimal Weight)> Effects =>
	[
		(_ => PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner), 15m),
		(_ => PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue + 1, Owner), 1m),

		(_ => CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null), 2m),
		(
			_ => PotionCmd.TryToProcure(
				PotionFactory.CreateRandomPotionInCombat(Owner, Owner.RunState.Rng.CombatPotionGeneration)
					.ToMutable(), Owner),
			Owner.PotionSlots.Any(p => p is null) ? 1m : 0m
		),

		(context => CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner), 2m),
		(
			context => PowerCmd.Apply<StrengthPower>(context, Owner.Creature,
				DynamicVars.Strength.BaseValue, Owner.Creature, null),
			2m
		),

		(
			context => PowerCmd.Apply<DrawCardsNextTurnPower>(context, Owner.Creature,
				DynamicVars[nameof(DrawCardsNextTurnPower)].BaseValue, Owner.Creature, null),
			2m
		),

		(
			context => PowerCmd.Apply<EnergyNextTurnPower>(context, Owner.Creature,
				DynamicVars[nameof(EnergyNextTurnPower)].BaseValue, Owner.Creature, null),
			2m
		),


		(
			context => PowerCmd.Apply<ThornsPower>(context, Owner.Creature,
				DynamicVars[nameof(ThornsPower)].BaseValue, Owner.Creature, null),
			2m
		),

		(
			context => PowerCmd.Apply<FocusPower>(context, Owner.Creature,
				DynamicVars[nameof(FocusPower)].BaseValue, Owner.Creature, null),
			Owner.Character is Defect ? 2m : 0m
		),

		(
			context => OstyCmd.Summon(context, Owner, DynamicVars.Summon.BaseValue, null),
			Owner.Character is Necrobinder ? 2m : 0m
		),


		(
			context => PowerCmd.Apply<DexterityPower>(context, Owner.Creature,
				DynamicVars.Dexterity.BaseValue, Owner.Creature, null),
			2m
		),

		(
			_ => CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue),
			Owner.Creature.CurrentHp == Owner.Creature.MaxHp ? 0m : 1m
		),

		(_ => PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner), 1m)
	];

	public override async Task AfterObtained()
	{
		if (CombatManager.Instance.IsInProgress)
		{
			await SummonPet();
		}
	}

	public override async Task BeforeCombatStart()
	{
		await SummonPet();
	}

	private async Task SummonPet()
	{
		await PlayerCmd.AddPet<DarkBumMonster>(Owner);
	}

#if STS2_Beta
	public override CardLocation ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay, ResourceInfo resources,
		CardLocation cardLocation)
	{
		if (card.Owner != Owner || UsedThisTurn)
		{
			return cardLocation;
		}

		cardLocation.pileType = PileType.Exhaust;

		return cardLocation;
	}
	
	public override Task AfterModifyingCardPlayResultLocation(CardModel card, CardLocation cardLocation)
	{
		Flash();
		return Task.CompletedTask;
	}
#else
	public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card,
		bool isAutoPlay,
		ResourceInfo resources, PileType pileType, CardPilePosition position)
	{
		if (card.Owner != Owner || UsedThisTurn)
		{
			return (pileType, position);
		}

		return (PileType.Exhaust, position);
	}
#endif


	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (cardPlay.Card.Owner != Owner || UsedThisTurn)
		{
			return;
		}

		UsedThisTurn = true;

		Flash();

		await Cmd.Wait(0.25f);

		PlayPetRelicTriggerFx();

		await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
	}

	private void PlayPetRelicTriggerFx()
	{
		if (!CombatManager.Instance.IsInProgress)
		{
			return;
		}

		var pet = Owner.PlayerCombatState?.GetPet<DarkBumMonster>();
		var petNode = pet?.GetCreatureNode();

		if (petNode?.Visuals is DarkBumVisuals visuals)
		{
			visuals.PlayRelicTrigger();
		}

		AudioHelper.Play("res://FBE/audio/thumbs up.wav", 0.8f);
	}
}
