using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Cards;

namespace FBE.Scripts.VanillaModifiers.Models;

/// <summary>Adds one more card option to Seeker Strike when upgraded.</summary>
[HarmonyPatch(typeof(SeekerStrike), "OnUpgrade")]
internal static class PatchSeekerStrikeUpgrade
{
	private const string CardsVarKey = "Cards";

	private static void Postfix(SeekerStrike __instance)
	{
		if (!__instance.DynamicVars.TryGetValue(CardsVarKey, out var cardsVar) || cardsVar is null)
		{
			Entry.Log.Warn($"Skipping Seeker Strike upgrade modifier: DynamicVar '{CardsVarKey}' was not found.");
			return;
		}

		cardsVar.UpgradeValueBy(1m);
	}
}
