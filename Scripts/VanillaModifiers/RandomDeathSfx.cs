using FBE.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;

namespace FBE.Scripts.VanillaModifiers;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
public static class RandomDeathSfx
{
	public static void Postfix(IRunState runState, ICombatState? combatState, Creature creature,
		bool wasRemovalPrevented, float deathAnimLength)
	{
		if (creature.IsPlayer)
		{
			DeathSfxHelper.PlayRandom();
		}
	}
}