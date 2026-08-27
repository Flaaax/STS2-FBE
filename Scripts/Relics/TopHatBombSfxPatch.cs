using FBE.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;

namespace FBE.Scripts.Relics;

/// <summary>Plays FBE's explosion sound when a Top Hat-generated The Bomb detonates.</summary>
[HarmonyPatch(typeof(TheBombPower), nameof(TheBombPower.BeforeSideTurnEnd))]
internal static class PatchTheBombSound
{
    private static void Prefix(TheBombPower __instance, PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (__instance.Applier?.Player == null || !__instance.Applier.Player.Relics.Any(relic => relic is TopHat))
        {
            return;
        }

        if (!participants.Contains(__instance.Owner) || __instance.Amount > 1)
        {
            return;
        }

        TaskHelper.RunSafely(PlayDelayed());
    }

    private static async Task PlayDelayed()
    {
        await Cmd.CustomScaledWait(0.4f, 0.8f);
        AudioHelper.PlayRandom("res://FBE/audio/boss explosions 0.wav", 0.8f);
    }
}
