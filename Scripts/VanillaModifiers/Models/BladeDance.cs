using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;

namespace FBE.Scripts.VanillaModifiers.Models;

/// <summary>Changes Blade Dance's upgrade to grant Retain.</summary>
[HarmonyPatch(typeof(BladeDance), "OnUpgrade")]
internal static class PatchBladeDance
{
    private static bool Prefix(BladeDance __instance)
    {
        __instance.AddKeyword(CardKeyword.Retain);
        return false;
    }
}
