using FBE.Scripts.Afflictions;
using FBE.Scripts.MonsterMoves.Intents;
using FBE.Scripts.Powers;
using FBE.Scripts.Visuals;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

/// <summary>教条一阶段本体：Godhead、硫磺火与哭喊三段循环。</summary>
[RegisterMonster]
public sealed class Dogma : ModMonsterTemplate
{
	private const int BrimstoneAttackDamage = 2;
	private const int BrimstoneAttackRepeat = 5;
	private const int GodheadAttackDamage = 6;
	private const int GodheadAttackRepeat = 3;
	private const int ScreamFrailAmount = 1;
	private const int ScreamWeakAmount = 1;
	private const float AnmFramesPerSecond = 30f;
	// Brimstone 于第 40 tick 射出、第 72 tick 进入压缩；5 发均匀落在完整光束时段内。
	private static readonly int[] BrimstoneImpactTicks = [40, 46, 53, 59, 66];
	// GodheadShoot 的 EventId 0 分别位于拼接动作的第 30、47、64 个 30 FPS tick。
	private static readonly int[] GodheadImpactTicks = [30, 47, 64];
	// Scream 的 Shoot Trigger 位于原始 ANM2 的第 13 tick，代表哭喊发生。
	private const int ScreamCryTick = 13;
	public override int MinInitialHp => 1;
	public override int MaxInitialHp => 1;
	public override bool HasDeathSfx => false;

	protected override NCreatureVisuals TryCreateCreatureVisuals() => new DogmaVisuals();

	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		DogmaBattleAudio.Start(CombatState.RunState.Act);
		// 信号源副本只展示与电视的关系；实际保护由电视身上的同类能力执行。
		await PowerCmd.Apply<SignalSourcePower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
		await PowerCmd.Apply<DogmaPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
	}

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		var brimstoneAttack = new MoveState("BRIMSTONE_ATTACK", BrimstoneAttack,
			new MultiAttackIntent(BrimstoneAttackDamage, BrimstoneAttackRepeat));
		var godheadAttack = new MoveState("GODHEAD_ATTACK", GodheadAttack,
			new MultiAttackIntent(GodheadAttackDamage, GodheadAttackRepeat), new NoSignalIntent());
		var scream = new MoveState("SCREAM", Scream, new DebuffIntent());
		godheadAttack.FollowUpState = brimstoneAttack;
		brimstoneAttack.FollowUpState = scream;
		scream.FollowUpState = godheadAttack;
		return new MonsterMoveStateMachine([godheadAttack, brimstoneAttack, scream], godheadAttack);
	}

	private async Task BrimstoneAttack(IReadOnlyList<Creature> targets)
	{
		var playback = PlayBrimstoneAttackAnimation();
		var animationStartedMsec = Time.GetTicksMsec();
		foreach (var impactTick in BrimstoneImpactTicks)
		{
			await WaitUntilAnimationImpact(animationStartedMsec, impactTick);
			await DamageCmd.Attack(BrimstoneAttackDamage).FromMonster(this).WithNoAttackerAnim().Execute(null);
		}
		await playback;
	}

	private async Task Scream(IReadOnlyList<Creature> targets)
	{
		var playback = PlayScreamAnimation();
		var animationStartedMsec = Time.GetTicksMsec();
		await WaitUntilAnimationImpact(animationStartedMsec, ScreamCryTick);
		DogmaBattleAudio.PlayScream();
		await PowerCmd.Apply<FrailPower>(
			new ThrowingPlayerChoiceContext(),
			targets,
			ScreamFrailAmount,
			Creature,
			null);
		await PowerCmd.Apply<WeakPower>(
			new ThrowingPlayerChoiceContext(),
			targets,
			ScreamWeakAmount,
			Creature,
			null);
		await playback;
	}

	private async Task GodheadAttack(IReadOnlyList<Creature> targets)
	{
		var playback = PlayGodheadAttackAnimation();
		var animationStartedMsec = Time.GetTicksMsec();
		foreach (var impactTick in GodheadImpactTicks)
		{
			await WaitUntilAnimationImpact(animationStartedMsec, impactTick);
			DogmaBattleAudio.PlayGodheadTear();
			await DamageCmd.Attack(GodheadAttackDamage).FromMonster(this).WithNoAttackerAnim().Execute(null);
			await AfflictDrawPileCards(targets);
		}
		await playback;
	}

	/// <summary>以动画启动时刻为基准等待，避免上一发的伤害结算时间累加到下一发。</summary>
	private static Task WaitUntilAnimationImpact(ulong animationStartedMsec, int impactTick)
	{
		var dueMsec = animationStartedMsec + (ulong)MathF.Round(impactTick * 1000f / AnmFramesPerSecond);
		var nowMsec = Time.GetTicksMsec();
		return nowMsec >= dueMsec ? Task.CompletedTask : Cmd.Wait((dueMsec - nowMsec) / 1000f);
	}

	private async Task AfflictDrawPileCards(IEnumerable<Creature> targets)
	{
		NoSignal.CacheOverlayForCombat();
		foreach (var target in targets)
		{
			var player = target.Player ?? target.PetOwner;
			var state = player?.PlayerCombatState;
			if (state == null)
				continue;

			// Godhead 优先侵蚀抽牌堆；仅在抽牌堆完全为空时才回退到弃牌堆。
			var sourcePile = state.DrawPile.Cards.Count == 0
				? state.DiscardPile.Cards
				: state.DrawPile.Cards;
			var candidates = sourcePile.Where(card => card.Affliction == null).ToList();
			if (candidates.Count > 0)
			{
				var card = candidates[Rng.NextInt(candidates.Count)];
				if (await CardCmd.Afflict<NoSignal>(card, 1m) != null)
					// 与门怪物的卡牌展示路径相同：预览仅负责视觉反馈，不阻塞下一发 Godhead。
					CardCmd.Preview(card);
			}
		}
	}

	private Task PlayGodheadAttackAnimation()
	{
		if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals is DogmaVisuals visuals)
			return visuals.PlayGodheadAttackAsync();

		return CreatureCmd.TriggerAnim(Creature, "GodheadAttack", 95f / AnmFramesPerSecond);
	}

	private Task PlayBrimstoneAttackAnimation()
	{
		if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals is DogmaVisuals visuals)
			return visuals.PlayBrimstoneAttackAsync();

		return CreatureCmd.TriggerAnim(Creature, "BrimstoneAttack", 91f / AnmFramesPerSecond);
	}

	private Task PlayScreamAnimation()
	{
		if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals is DogmaVisuals visuals)
			return visuals.PlayScreamAsync();

		return CreatureCmd.TriggerAnim(Creature, "Scream", 57f / AnmFramesPerSecond);
	}

	public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
		ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (target == Creature)
			(NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals as DogmaEffectVisuals)?.TriggerHitDistortion();
		return Task.CompletedTask;
	}
}
