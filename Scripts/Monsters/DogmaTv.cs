using FBE.Scripts.Afflictions;
using FBE.Scripts.MonsterMoves.Intents;
using FBE.Scripts.Powers;
using FBE.Scripts.Visuals;
using FBECore.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Monsters;

/// <summary>教条电视：维持教条存活，并在意图触发时侵蚀两张牌。</summary>
[RegisterMonster]
public sealed class DogmaTv : ModMonsterTemplate
{
	private const float DeathAnimationSeconds = 16f / 30f;
	private const string NoSignalSoundPath = "res://FBE/audio/Dogma/ClickerStatic.wav";
	public override int MinInitialHp => 250;
	public override int MaxInitialHp => 250;
	public override bool HasDeathSfx => false;
	public override bool ShouldFadeAfterDeath => false;
	public override float DeathAnimLengthOverride => DeathAnimationSeconds;

	protected override NCreatureVisuals TryCreateCreatureVisuals() => new DogmaTvVisuals();

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		var noSignal = new MoveState("NO_SIGNAL", NoSignalMove, new NoSignalIntent());
		var empowerDogma = new MoveState("EMPOWER_DOGMA", EmpowerDogmaMove, new BuffIntent());
		noSignal.FollowUpState = empowerDogma;
		empowerDogma.FollowUpState = noSignal;
		return new MonsterMoveStateMachine([noSignal, empowerDogma], noSignal);
	}

	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		await PowerCmd.Apply<SignalSourcePower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
	}

	public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature,
		bool wasRemovalPrevented, float deathAnimLength)
	{
		if (creature != Creature || wasRemovalPrevented)
			return;

		(NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals as DogmaTvVisuals)?.SpawnDeathRemnant();
		var dogma = CombatState.Enemies.FirstOrDefault(enemy => enemy.IsAlive && enemy.Monster is Dogma);
		if (dogma != null)
			await CreatureCmd.Kill(dogma, force: true);
	}

	private void AddRandomAfflictableCard(IEnumerable<CardModel> primaryPile, IEnumerable<CardModel> fallbackPile,
		ICollection<CardModel> targets)
	{
		var candidates = primaryPile.Where(card => card.Affliction == null && !targets.Contains(card)).ToList();
		if (candidates.Count == 0)
			candidates = fallbackPile.Where(card => card.Affliction == null && !targets.Contains(card)).ToList();
		if (candidates.Count > 0)
			targets.Add(candidates[Rng.NextInt(candidates.Count)]);
	}

	private async Task NoSignalMove(IReadOnlyList<Creature> targets)
	{
		NoSignal.CacheOverlayForCombat();
		FbeAudio.PlayOneShot(NoSignalSoundPath);
		foreach (var player in CombatState.Players)
		{
			var state = player.PlayerCombatState;
			if (state == null)
				continue;

			var pileTargets = new List<CardModel>(2);
			AddRandomAfflictableCard(state.DrawPile.Cards, state.DiscardPile.Cards, pileTargets);
			AddRandomAfflictableCard(state.DiscardPile.Cards, state.DrawPile.Cards, pileTargets);
			await CardCmd.AfflictAndPreview<NoSignal>(pileTargets, 1m);
		}
	}

	private Task EmpowerDogmaMove(IReadOnlyList<Creature> targets)
	{
		var dogma = CombatState.Enemies.FirstOrDefault(enemy => enemy.IsAlive && enemy.Monster is Dogma);
		return dogma == null
			? Task.CompletedTask
			: PowerCmd.Apply<DogmaStrengthPower>(new ThrowingPlayerChoiceContext(), dogma, 1m, Creature, null);
	}

	public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
		ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (target == Creature)
			(NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals as DogmaEffectVisuals)?.TriggerHitDistortion();
		return Task.CompletedTask;
	}
}
