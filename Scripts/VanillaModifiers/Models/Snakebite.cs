using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Cards;

namespace FBE.Scripts.VanillaModifiers.Models;

/// <summary>Sets vanilla Snakebite's base Poison value to 8.</summary>
[HarmonyPatch(typeof(Snakebite), MethodType.Constructor)]
[HarmonyPatch([])]
internal static class PatchSnakebite
{
    private static void Postfix(Snakebite __instance)
    {
        __instance.DynamicVars.Poison.BaseValue = 8m;
    }
}
