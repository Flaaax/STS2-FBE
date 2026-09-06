using FBE.Scripts.Visuals;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
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
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine.Backends;

namespace FBE.Scripts.Monsters;

/// <summary>
/// 古烈：召唤防御、强化召唤、强化防御、空翻攻击，随后重新循环。
/// </summary>
[RegisterMonster]
public sealed class Guile : ModMonsterTemplate
{
	private const int SummonBlockAmount = 12;
	private const int StrengthAmount = 2;
	private const int EmpowerBlockAmount = 8;

	private const int SomersaultDamage = 17;

	// 前4个动画元素合计9 tick；第5个元素进入踢击阶段时结算伤害。
	private const float SomersaultImpactDelay = 9f / 60f;
	private const float CrouchGuardAnimationDuration = 14f / 60f;
	private const float HitAnimationDuration = 14f / 60f;
	private const float SonicCastAnimationDuration = 30f / 60f;
	private bool _isCrouching;

	public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 90, 80);

	public override int MaxInitialHp => MinInitialHp;

	// 目前没有导入专属死亡音效，避免回退到不存在的 FMOD 事件。
	public override bool HasDeathSfx => false;
	// 原版非Spine死亡流程先等待此时长，再从倒地画面生成消逝特效。
	public override float DeathAnimLengthOverride => GuileVisuals.DeathPlaybackSeconds + 0.05f;

	// 显式提供场景，避免战斗资源预加载回退到不存在的默认怪物形象。
	public override string? CustomVisualsPath => "res://FBE/scenes/creature_visuals/guile_visuals.tscn";

	protected override NCreatureVisuals TryCreateCreatureVisuals()
	{
		return new GuileVisuals();
	}

	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		GuileBattleAudio.Start(this);
	}

	/// <summary>玩家战败时强制结束当前动作并固定为站姿。</summary>
	internal void ForceStandingVictoryPose()
	{
		_isCrouching = false;
		GetGuileVisuals()?.ForceStandingVictoryPose();
	}

	protected override ModAnimStateMachine? SetupCustomCombatAnimationStateMachine(Node visualsRoot,
		MonsterModel monster)
	{
		if (visualsRoot is not GuileVisuals visuals)
			return null;

		// 受击根据当前姿势回到对应待机；完全格挡由 AfterDamageReceived 显式触发蹲姿防御帧。
		return ModAnimStateMachineBuilder.Create()
			.AddState("idle", loop: true).AsInitial().Done()
			.AddState("standing_hit").WithNext("idle").Done()
			.AddState("standing_guard").WithNext("idle").Done()
			.AddState("crouch_idle", loop: true).Done()
			.AddState("crouch_guard").WithNext("crouch_idle").Done()
			.AddState("crouch_hit").WithNext("crouch_idle").Done()
			.AddState("sonic_cast").WithNext("crouch_idle").Done()
			.AddState("somersault").WithNext("idle").Done()
			.AddState("death").Done()
			.AddAnyState("Idle", "crouch_idle", () => _isCrouching)
			.AddAnyState("Idle", "idle")
			.AddAnyState("Hit", "crouch_hit", () => _isCrouching)
			.AddAnyState("Hit", "standing_hit")
			.AddAnyState("Dead", "death")
			.AddAnyState("Attack", "idle")
			.AddAnyState("Somersault", "somersault")
			.AddAnyState("SonicCast", "sonic_cast")
			.AddAnyState("CrouchGuard", "crouch_guard")
			.AddAnyState("StandingGuard", "standing_guard")
			.AddAnyState("CrouchIdle", "crouch_idle")
			.AddAnyState("Cast", "sonic_cast")
			.Build(new AnimatedSprite2DBackend(visuals.Sprite));
	}

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		var summonAndGuard = new MoveState(
			"SUMMON_AND_GUARD",
			SummonAndGuardMove,
			new SummonIntent(),
			new DefendIntent());
		var empower = new MoveState("EMPOWER_AND_SUMMON", EmpowerAndSummonMove, new BuffIntent(), new SummonIntent());
		var empowerAgain = new MoveState("EMPOWER_AND_GUARD_AGAIN", EmpowerAndGuardMove, new BuffIntent(),
			new DefendIntent());
		var somersault = new MoveState("SOMERSAULT", SomersaultMove, new SingleAttackIntent(SomersaultDamage));
		summonAndGuard.FollowUpState = empower;
		empower.FollowUpState = empowerAgain;
		empowerAgain.FollowUpState = somersault;
		somersault.FollowUpState = summonAndGuard;

		return new MonsterMoveStateMachine([summonAndGuard, empower, empowerAgain, somersault], summonAndGuard);
	}

	public override Task AfterDamageReceived(
		PlayerChoiceContext choiceContext,
		Creature target,
		DamageResult result,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
	{
		if (target != Creature || Creature.IsDead)
			return Task.CompletedTask;

		if (result.WasFullyBlocked)
		{
			return _isCrouching
				? PlayGuileOneShotAsync("crouch_guard", "crouch_idle", "CrouchGuard", CrouchGuardAnimationDuration)
				: PlayGuileOneShotAsync("standing_guard", "idle", "StandingGuard", CrouchGuardAnimationDuration);
		}

		// 原版伤害流程会先以零等待时间触发 Hit；这里直接驱动古烈的精灵，避免触发器转发失败时静默停在待机帧。
		return result.UnblockedDamage > 0 || result.OverkillDamage > 0
			? PlayGuileOneShotAsync(
				_isCrouching ? "crouch_hit" : "standing_hit",
				_isCrouching ? "crouch_idle" : "idle",
				"Hit",
				HitAnimationDuration)
			: Task.CompletedTask;
	}

	public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
	{
		// 完美 KO 只要求玩家未实际失去生命；完全格挡和回血都不影响判定。
		GuileBattleAudio.MarkPlayerHurt(creature, delta);
		return Task.CompletedTask;
	}

	public override Task AfterDeath(
		PlayerChoiceContext choiceContext,
		Creature creature,
		bool wasRemovalPrevented,
		float deathAnimLength)
	{
		if (creature == Creature && !wasRemovalPrevented)
			GuileBattleAudio.PlayGuileDefeat();

		return Task.CompletedTask;
	}

	private Task SummonAndGuardMove(IReadOnlyList<Creature> targets)
	{
		return SummonWaveAsync(SummonBlockAmount);
	}

	private async Task EmpowerAndSummonMove(IReadOnlyList<Creature> targets)
	{
		await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, StrengthAmount, Creature,
			null);
		await SummonWaveAsync(0);
	}

	private async Task SummonWaveAsync(int blockAmount)
	{
		_isCrouching = true;
		GuileBattleAudio.PlayShooting();
		// 先启动音速手刀，但不等待它结束：波动应在手刀播放期间出现，且一定先于格挡生成。
		var sonicCastPlayback = PlayGuileOneShotAsync("sonic_cast", "crouch_idle", "Cast", SonicCastAnimationDuration);
		if (CombatState.IsLiveCombat())
		{
			await CreatureCmd.Add<GuileSonicBoom>(
				CombatState,
				CombatState.Encounter!.GetNextSlot(CombatState));
		}

		if (blockAmount > 0)
			await CreatureCmd.GainBlock(Creature, blockAmount, ValueProp.Move, null);
		await sonicCastPlayback;
	}

	private async Task EmpowerAndGuardMove(IReadOnlyList<Creature> targets)
	{
		GuileBattleAudio.PlaySecondEmpower();
		await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, StrengthAmount, Creature,
			null);
		await CreatureCmd.GainBlock(Creature, EmpowerBlockAmount, ValueProp.Move, null);
	}

	private async Task SomersaultMove(IReadOnlyList<Creature> targets)
	{
		GuileBattleAudio.PlaySomersault();
		Task playback = Task.CompletedTask;
		try
		{
			await DamageCmd.Attack(SomersaultDamage).FromMonster(this).WithNoAttackerAnim()
				.AfterAttackerAnim(async () =>
				{
					playback = PlayGuileOneShotAsync("somersault", "idle", "Somersault", 42f / 60f);
					// 精灵按60 tick/s播放；只等待命中点，余下动画与伤害结算并行。
					await Cmd.Wait(SomersaultImpactDelay);
				})
				.Execute(null);
		}
		finally
		{
			await playback;
			_isCrouching = false;
		}
	}

	private Task PlayGuileOneShotAsync(
		string animationName,
		string returnAnimationName,
		string fallbackTrigger,
		float fallbackDuration)
	{
		var visuals = GetGuileVisuals();
		if (visuals != null)
			return visuals.PlayOneShotAndReturnAsync(animationName, returnAnimationName);

		var actualVisualType = NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals?.GetType().FullName ?? "<none>";
		GD.PushError($"[FBE][Guile] Expected GuileVisuals, got {actualVisualType}; using trigger fallback.");
		return CreatureCmd.TriggerAnim(Creature, fallbackTrigger, fallbackDuration);
	}

	private GuileVisuals? GetGuileVisuals()
	{
		return NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals as GuileVisuals;
	}
}
