using FBE.Scripts.Visuals;
using FBE.Scripts.VFX;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine.Backends;

namespace FBE.Scripts.Monsters;

/// <summary>
/// 门：用于验证精英遭遇、意图和非 Spine 形象的完整接入链路。
/// </summary>
[RegisterMonster]
public class DoorMonster : ModMonsterTemplate
{
	public const int AttackDamage = 25;
	private static readonly LocString PlaceholderStrikeBanter =
		new("monsters", "FBE_MONSTER_DOOR_MONSTER.moves.PLACEHOLDER_STRIKE.banter");

	public override int MinInitialHp => 120;

	public override int MaxInitialHp => 120;

	// 占位敌人没有专属 FMOD 死亡音效，避免默认路径指向不存在的事件。
	public override bool HasDeathSfx => false;

	protected override NCreatureVisuals TryCreateCreatureVisuals()
	{
		return new DoorMonsterVisuals();
	}

	protected override ModAnimStateMachine? SetupCustomCombatAnimationStateMachine(Node visualsRoot, MonsterModel monster)
	{
		if (visualsRoot is not DoorMonsterVisuals visuals)
			return null;

		// 待机序列帧同样接住游戏的战斗触发器；后续可为攻击等状态换用专属动画。
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
			"PLACEHOLDER_STRIKE",
			PerformPlaceholderStrike,
			new SingleAttackIntent(AttackDamage));
		attack.FollowUpState = attack;
		return new MonsterMoveStateMachine([attack], attack);
	}

	private async Task PerformPlaceholderStrike(IReadOnlyList<Creature> targets)
	{
		TalkCmd.Play(PlaceholderStrikeBanter, Creature, VfxColor.Purple, VfxDuration.Standard);
		await DamageCmd.Attack(AttackDamage).FromMonster(this)
			.BeforeDamage(async () =>
			{
				if (targets.Count == 0)
					return;

				var beam = DoorMonsterHyperbeamVfx.CreateBeam(Creature, targets[0]);
				if (beam != null)
				{
					NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(beam);
					TaskHelper.RunSafely(RestartIdleAfterHyperbeam());
					await Cmd.Wait(NHyperbeamVfx.hyperbeamAnticipationDuration);
				}

				foreach (Creature target in targets)
				{
					var impact = DoorMonsterHyperbeamVfx.CreateImpact(Creature, target);
					if (impact != null)
						NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(impact);
				}
			})
			.Execute(null);
	}

	private async Task RestartIdleAfterHyperbeam()
	{
		await Cmd.Wait(DoorMonsterHyperbeamVfx.Duration);
		if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals is DoorMonsterVisuals visuals)
			visuals.RestartIdleAnimation();
	}
}
