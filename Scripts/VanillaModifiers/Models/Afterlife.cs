using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;

namespace FBE.Scripts.VanillaModifiers.Models;

/// <summary>Removes vanilla Afterlife from the Necrobinder card pool.</summary>
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
internal static class LockVanillaAfterlife
{
    private static void Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        if (__instance is NecrobinderCardPool)
        {
            __result = __result.Where(card => card.GetType() != typeof(Afterlife)).ToList();
        }
    }
}
