using FBE.Scripts.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Monsters;

/// <summary>教条的第一版占位本体：100 生命，每回合攻击 15 点。</summary>
[RegisterMonster]
public sealed class Dogma : ModMonsterTemplate
{
	private const int AttackDamage = 15;
	public override int MinInitialHp => 100;
	public override int MaxInitialHp => 100;
	public override bool HasDeathSfx => false;

	protected override NCreatureVisuals TryCreateCreatureVisuals() => new DogmaVisuals();

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		var attack = new MoveState("ATTACK", Attack, new SingleAttackIntent(AttackDamage));
		attack.FollowUpState = attack;
		return new MonsterMoveStateMachine([attack], attack);
	}

	private Task Attack(IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> targets)
	{
		return DamageCmd.Attack(AttackDamage).FromMonster(this).WithNoAttackerAnim().Execute(null);
	}
}
