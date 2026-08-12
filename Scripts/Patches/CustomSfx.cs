using FBE.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;

namespace FBE.Scripts.Patches;

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play))]
[HarmonyPatch([typeof(string), typeof(float)])]
public static class CustomSfxPatch1
{
    public static bool Prefix(string sfx, float volume)
    {
        if (NonInteractiveMode.IsActive || CombatManager.Instance.IsEnding)
            return true;
        if (!sfx.StartsWith("res://")) return true;
        AudioHelper.Play(sfx, volume);
        return false;
    }
}
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
public static class CustomSfxPatch2
{
    public static void Postfix(IRunState runState, ICombatState? combatState, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature.IsPlayer)
        {
            AudioHelper.PlayRandom("res://FBE/audio/isaac dies new0.wav");
        }
    }
}
