using FBE.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace FBE.Scripts.VanillaModifiers.Models;

/// <summary>Reduces Test Subject's Enrage trigger rate in multiplayer.</summary>
[HarmonyPatch(typeof(EnragePower), nameof(EnragePower.AfterCardPlayed))]
internal static class PatchTestSubjectEnrage
{
    private static bool Prefix(EnragePower __instance, ref Task __result, PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        __result = PatchHelper.WrapAsync(async () =>
        {
            if (cardPlay.Card.Type != CardType.Skill)
            {
                return;
            }

            await Cmd.Wait(0.5f);
            var playerCount = __instance.CombatState.PlayerCreatures.Count;
            if (__instance.Owner.Monster!.Rng.NextFloat() <= 1.0 / playerCount)
            {
                await PowerCmd.Apply<StrengthPower>(choiceContext, __instance.Owner, __instance.Amount,
                    __instance.Owner, null);
            }
        });
        return false;
    }
}
