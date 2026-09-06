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
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine.Backends;

namespace FBE.Scripts.Monsters;

/// <summary>
/// 古烈的音速波动测试爪牙：出场飞到待命点，下一回合飞向玩家并自杀攻击。
/// </summary>
[RegisterMonster]
public sealed class GuileSonicBoom : ModMonsterTemplate
{
	private int AttackDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 7);
	private const int WeakAmount = 1;

	// 原版创建怪物时使用战斗 RNG，在包含两端点的范围内抽取生命值。
	public override int MinInitialHp => 10;

	public override int MaxInitialHp => 13;

	public override bool HasDeathSfx => false;

	// 显式提供场景，确保波动的 Bounds 节点成为战斗实际使用的选择框。
	public override string? CustomVisualsPath => "res://FBE/scenes/creature_visuals/guile_sonic_boom_visuals.tscn";

	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		ApplyExpandedSelectionBounds();
		var room = NCombatRoom.Instance;
		var summoner = CombatState.Enemies.FirstOrDefault(enemy => enemy.Monster is Guile && enemy.IsAlive);
		if (room?.GetCreatureNode(Creature)?.Visuals is GuileSonicBoomVisuals visuals &&
			summoner != null && room.GetCreatureNode(summoner) is { } summonerNode)
			await visuals.PlaySpawnTravelAsync(summonerNode);
		// 次级敌人不会阻止古烈死亡后的战斗结算。
		await PowerCmd.Apply<MinionPower>(
			new ThrowingPlayerChoiceContext(),
			Creature,
			1m,
			Creature,
			null);
	}

	private void ApplyExpandedSelectionBounds()
	{
		var creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
		if (creatureNode?.Visuals is not GuileSonicBoomVisuals visuals)
		{
			var actualVisualType = creatureNode?.Visuals?.GetType().FullName ?? "<none>";
			GD.PushError($"[FBE][GuileSonicBoom] Expected GuileSonicBoomVisuals, got {actualVisualType}.");
			return;
		}

		// NCreature 只会在创建时把 Visuals.Bounds 复制到 Hitbox/SelectionReticle。
		// 波动为运行时追加的爪牙，在此再复制一次，使用视觉节点中配置的目标碰撞范围。
		var bounds = visuals.Bounds;
		var size = bounds.Size * visuals.Scale;
		var globalPosition = creatureNode.GlobalPosition + (bounds.GlobalPosition - creatureNode.GlobalPosition);
		creatureNode.Hitbox.Size = size;
		creatureNode.Hitbox.GlobalPosition = globalPosition;

		var selectionReticle = creatureNode.GetNodeOrNull<NSelectionReticle>("%SelectionReticle");
		if (selectionReticle == null)
		{
			GD.PushError("[FBE][GuileSonicBoom] Creature selection reticle was not found.");
			return;
		}

		selectionReticle.Size = size;
		selectionReticle.GlobalPosition = globalPosition;
		selectionReticle.PivotOffset = size * 0.5f;
	}

	protected override NCreatureVisuals TryCreateCreatureVisuals()
	{
		return new GuileSonicBoomVisuals();
	}

	protected override ModAnimStateMachine? SetupCustomCombatAnimationStateMachine(Node visualsRoot, MonsterModel monster)
	{
		if (visualsRoot is not GuileSonicBoomVisuals visuals)
			return null;

		// 目前只有循环飞行帧，所有战斗触发器均维持该动画。
		return ModAnimStateMachineBuilder.Create()
			.AddState("idle", loop: true).AsInitial().Done()
			.AddAnyState("Idle", "idle")
			.AddAnyState("Hit", "idle")
			.AddAnyState("Dead", "idle")
			.AddAnyState("Attack", "idle")
			.AddAnyState("Cast", "idle")
			.Build(new AnimatedSprite2DBackend(visuals.Sprite));
	}

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		var attack = new MoveState(
			"SONIC_BOOM_ATTACK",
			SonicBoomAttackMove,
			new DeathBlowIntent(() => AttackDamage),
			new DebuffIntent());
		attack.FollowUpState = attack;
		return new MonsterMoveStateMachine([attack], attack);
	}

	private async Task SonicBoomAttackMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(AttackDamage).FromMonster(this).WithNoAttackerAnim()
			.AfterAttackerAnim(async () =>
			{
				var room = NCombatRoom.Instance;
				var node = room?.GetCreatureNode(Creature);
				var target = targets.FirstOrDefault(candidate => candidate.IsAlive);
				if (node?.Visuals is GuileSonicBoomVisuals visuals && target != null &&
					room?.GetCreatureNode(target) is { } targetNode)
					await visuals.PlayImpactTravelAsync(targetNode);
				// 命中瞬间隐藏贴图和战斗 UI；随后仍由正常死亡流程清理敌人。
				if (GodotObject.IsInstanceValid(node))
					node!.Hide();
			})
			.Execute(null);
		await PowerCmd.Apply<WeakPower>(
			new ThrowingPlayerChoiceContext(),
			targets,
			WeakAmount,
			Creature,
			null);
		if (Creature.IsAlive)
			await CreatureCmd.Kill(Creature);
	}
}
