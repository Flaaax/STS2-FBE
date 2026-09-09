using FBE.Scripts.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace FBE.Scripts.Powers;

/// <summary>电视持有时，令同场教条保持存活；本体上的副本仅作关联提示。</summary>
[RegisterPower]
public sealed class SignalSourcePower : FBEPowerModel
{
	public override PowerType Type => PowerType.Buff;
	public override PowerStackType StackType => PowerStackType.Single;

	public override bool ShouldDie(Creature creature) =>
		Owner.Monster is not DogmaTv || creature.Monster is not Dogma || Owner.IsDead;

	public override Task AfterPreventingDeath(Creature creature)
	{
		return Owner.Monster is DogmaTv
			? CreatureCmd.Heal(creature, 1m, playAnim: false)
			: Task.CompletedTask;
	}
}
