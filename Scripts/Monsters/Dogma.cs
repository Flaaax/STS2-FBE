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
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Monsters;

/// <summary>教条的第一版本体：1 生命，每回合攻击 15 点。</summary>
[RegisterMonster]
public sealed class Dogma : ModMonsterTemplate
{
	private const int AttackDamage = 15;
	private const int GodheadAttackDamage = 6;
	private const int GodheadAttackRepeat = 3;
	private const float AnmFramesPerSecond = 30f;
	// GodheadShoot 的 EventId 0 分别位于拼接动作的第 30、47、64 个 30 FPS tick。
	private static readonly int[] GodheadImpactTicks = [30, 47, 64];
	public override int MinInitialHp => 1;
	public override int MaxInitialHp => 1;
	public override bool HasDeathSfx => false;

	protected override NCreatureVisuals TryCreateCreatureVisuals() => new DogmaVisuals();

	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		// 信号源副本只展示与电视的关系；实际保护由电视身上的同类能力执行。
		await PowerCmd.Apply<SignalSourcePower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
		await PowerCmd.Apply<DogmaPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
	}

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		var attack = new MoveState("ATTACK", Attack, new SingleAttackIntent(AttackDamage));
		var godheadAttack = new MoveState("GODHEAD_ATTACK", GodheadAttack,
			new MultiAttackIntent(GodheadAttackDamage, GodheadAttackRepeat), new NoSignalIntent());
		attack.FollowUpState = godheadAttack;
		godheadAttack.FollowUpState = attack;
		return new MonsterMoveStateMachine([attack, godheadAttack], attack);
	}

	private Task Attack(IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> targets)
	{
		return DamageCmd.Attack(AttackDamage).FromMonster(this).WithNoAttackerAnim().Execute(null);
	}

	private async Task GodheadAttack(IReadOnlyList<Creature> targets)
	{
		var playback = PlayGodheadAttackAnimation();
		var animationStartedMsec = Time.GetTicksMsec();
		foreach (var impactTick in GodheadImpactTicks)
		{
			await WaitUntilGodheadImpact(animationStartedMsec, impactTick);
			await DamageCmd.Attack(GodheadAttackDamage).FromMonster(this).WithNoAttackerAnim().Execute(null);
			await AfflictDiscardCards(targets);
		}
		await playback;
	}

	/// <summary>以动画启动时刻为基准等待，避免上一发的伤害结算时间累加到下一发。</summary>
	private static Task WaitUntilGodheadImpact(ulong animationStartedMsec, int impactTick)
	{
		var dueMsec = animationStartedMsec + (ulong)MathF.Round(impactTick * 1000f / AnmFramesPerSecond);
		var nowMsec = Time.GetTicksMsec();
		return nowMsec >= dueMsec ? Task.CompletedTask : Cmd.Wait((dueMsec - nowMsec) / 1000f);
	}

	private async Task AfflictDiscardCards(IEnumerable<Creature> targets)
	{
		NoSignal.CacheOverlayForCombat();
		foreach (var target in targets)
		{
			var player = target.Player ?? target.PetOwner;
			var state = player?.PlayerCombatState;
			if (state == null)
				continue;

			var candidates = state.DiscardPile.Cards.Where(card => card.Affliction == null).ToList();
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

	public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
		ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (target == Creature)
			(NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals as DogmaEffectVisuals)?.TriggerHitDistortion();
		return Task.CompletedTask;
	}
}
