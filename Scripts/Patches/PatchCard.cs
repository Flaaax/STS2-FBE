using System.Reflection;
using FBE.Scripts.Powers;
using FBE.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using CoolantPower = FBE.Scripts.Powers.CoolantPower;

// ReSharper disable InconsistentNaming


namespace FBE.Scripts.Patches;

[HarmonyPatch(typeof(Acrobatics), MethodType.Constructor)]
[HarmonyPatch([])]
static class PatchAcrobaticsCtor //杂技变白卡
{
	private static readonly FieldInfo? RarityBackingField =
		AccessTools.Field(typeof(CardModel), "<Rarity>k__BackingField");

	static void Postfix(Acrobatics __instance)
	{
		RarityBackingField?.SetValue(__instance, CardRarity.Common);
	}
}

[HarmonyPatch(typeof(Snakebite), MethodType.Constructor)]
[HarmonyPatch([])]
static class PatchSnakebiteCtor //蛇咬加强数值
{
	static void Postfix(Snakebite __instance)
	{
		__instance.DynamicVars.Poison.BaseValue = 8m;
	}
}

[HarmonyPatch(typeof(EternalArmor), MethodType.Constructor)]
[HarmonyPatch([])]
static class PatchEternalArmorCtor //永恒铠甲加强数值,变成技能牌
{
	private static readonly FieldInfo? TypeBackingField =
		AccessTools.Field(typeof(CardModel), "<Type>k__BackingField");

	static void Postfix(EternalArmor __instance)
	{
		__instance.DynamicVars["PlatingPower"].BaseValue = 10m;
		TypeBackingField?.SetValue(__instance, CardType.Skill);
	}
}

[HarmonyPatch(typeof(Bolas), MethodType.Constructor)]
[HarmonyPatch([])]
static class PatchBolasCtor //流星锤加强数值
{
	static void Postfix(Bolas __instance)
	{
		__instance.DynamicVars.Damage.BaseValue = 7;
	}
}

[HarmonyPatch(typeof(Bolas), "OnUpgrade")]
[HarmonyPatch([])]
static class PatchBolasOnUpgrade //同上
{
	static void Postfix(Bolas __instance)
	{
		__instance.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}

[HarmonyPatch(typeof(RollingBoulder), MethodType.Constructor)]
[HarmonyPatch([])]
static class PatchRollingBoulder1
{
	private static readonly FieldInfo? CanonicalEnergyCostField =
		AccessTools.Field(typeof(CardModel), "<CanonicalEnergyCost>k__BackingField");

	static void Postfix(RollingBoulder __instance)
	{
		__instance.DynamicVars["RollingBoulderPower"].BaseValue = 10m;
		__instance.DynamicVars["IncrementAmount"].BaseValue = 10m;

		CanonicalEnergyCostField?.SetValue(__instance, 2);
	}
}

[HarmonyPatch(typeof(RollingBoulder), "OnUpgrade")]
[HarmonyPatch([])]
static class PatchRollingBoulder2
{
	static void Postfix(RollingBoulder __instance)
	{
		__instance.DynamicVars["RollingBoulderPower"].UpgradeValueBy(5m);
	}
}

[HarmonyPatch(typeof(RollingBoulderPower), MethodType.Constructor)]
[HarmonyPatch([])]
static class PatchRollingBoulderPower
{
	static void Postfix(RollingBoulderPower __instance)
	{
		__instance.DynamicVars.Damage.BaseValue =
			ModelDb.Card<RollingBoulder>().DynamicVars["IncrementAmount"].BaseValue;
	}
}

[HarmonyPatch(typeof(Mayhem), MethodType.Constructor)]
[HarmonyPatch([])]
static class PatchMayhemCtor
{
	private static readonly FieldInfo? CanonicalEnergyCostField =
		AccessTools.Field(typeof(CardModel), "<CanonicalEnergyCost>k__BackingField");

	static void Postfix(Mayhem __instance)
	{
		CanonicalEnergyCostField?.SetValue(__instance, 1);
		//__instance.RemoveKeyword(CardKeyword.Exhaust);
	}
}

[HarmonyPatch(typeof(BladeDance), "OnUpgrade")]
static class PatchBladeDanceOnUpgrade
{
	static bool Prefix(BladeDance __instance)
	{
		__instance.AddKeyword(CardKeyword.Retain);
		return false;
	}
}

//-----Rainbow-----

[HarmonyPatch(typeof(Rainbow), "get_ExtraHoverTips")]
static class PatchRainbowExtraHoverTips
{
	static bool Prefix(ref IEnumerable<IHoverTip> __result)
	{
		__result = new[] { HoverTipFactory.Static(StaticHoverTip.Channeling) };
		return false; // 阻止原 getter 执行
	}
}

[HarmonyPatch(typeof(Rainbow))]
static class PatchRainbowCanonicalKeywords
{
	static MethodBase TargetMethod()
	{
		return AccessTools.PropertyGetter(typeof(Rainbow), "CanonicalKeywords");
	}

	static bool Prefix(ref IEnumerable<CardKeyword> __result)
	{
		__result = Array.Empty<CardKeyword>();
		return false;
	}
}

[HarmonyPatch(typeof(Rainbow))]
static class PatchRainbowCanonicalVars
{
	static MethodBase TargetMethod()
	{
		return AccessTools.PropertyGetter(typeof(Rainbow), "CanonicalVars");
	}

	static bool Prefix(ref IEnumerable<DynamicVar> __result)
	{
		__result = new[] { new RepeatVar(4) };
		return false;
	}
}

[HarmonyPatch(typeof(Rainbow), MethodType.Constructor)]
static class PatchRainbowCtor
{
	static void Postfix(Rainbow __instance)
	{
		FieldPatcher.Set(__instance, "Type", CardType.Power);
	}
}

[HarmonyPatch(typeof(Rainbow), "OnPlay")]
static class PatchRainbowOnPlay
{
	static bool Prefix(Rainbow __instance, ref Task __result, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		var patchedRainbowOnPlay = async () =>
		{
			await CreatureCmd.TriggerAnim(__instance.Owner.Creature, "Cast", __instance.Owner.Character.CastAnimDelay);
			await OrbCmd.AddSlots(__instance.Owner, __instance.DynamicVars.Repeat.IntValue);
			await Cmd.Wait(0.25f);
			for (int i = 0; i < __instance.DynamicVars.Repeat.IntValue; i++)
			{
				await OrbCmd.Channel(choiceContext,
					OrbModel.GetRandomOrb(__instance.Owner.RunState.Rng.CombatOrbGeneration).ToMutable(),
					__instance.Owner);
			}

			await PowerCmd.Apply<RainbowPower>(choiceContext, __instance.Owner.Creature,
				__instance.DynamicVars.Repeat.BaseValue,
				__instance.Owner.Creature, __instance);
		};

		__result = PatchHelper.WrapAsync(patchedRainbowOnPlay);

		return false;
	}
}

[HarmonyPatch(typeof(Rainbow), "OnUpgrade")]
static class PatchRainbowOnUpgrade
{
	static bool Prefix(Rainbow __instance)
	{
		__instance.DynamicVars.Repeat.UpgradeValueBy(1m);
		return false;
	}
}

[HarmonyPatch(typeof(Coolant), "OnPlay")]
static class PatchCoolantOnPlay
{
	static bool Prefix(Coolant __instance, ref Task __result, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		var task = async () =>
		{
			await CreatureCmd.TriggerAnim(__instance.Owner.Creature, "Cast", __instance.Owner.Character.CastAnimDelay);
			await PowerCmd.Apply<CoolantPower>(choiceContext, __instance.Owner.Creature, 1, __instance.Owner.Creature,
				__instance);
		};
		__result = PatchHelper.WrapAsync(task);
		return false;
	}
}

[HarmonyPatch(typeof(Coolant), "get_ExtraHoverTips")]
static class PatchCoolantExtraHoverTips
{
	static bool Prefix(ref IEnumerable<IHoverTip> __result)
	{
		__result = new[]
		{
			HoverTipFactory.Static(StaticHoverTip.Channeling),
			HoverTipFactory.FromOrb<FrostOrb>()
		};
		return false; // 阻止原 getter 执行
	}
}

[HarmonyPatch(typeof(Coolant), MethodType.Constructor)]
[HarmonyPatch([])]
static class PatchCoolantCtor
{
	static void Postfix(Mayhem __instance)
	{
		FieldPatcher.Set(__instance, "CanonicalEnergyCost", 1);
	}
}

[HarmonyPatch(typeof(Coolant), "OnUpgrade")]
static class PatchCoolantOnUpgrade
{
	static bool Prefix(Coolant __instance)
	{
		__instance.AddKeyword(CardKeyword.Innate);
		return false;
	}
}

[HarmonyPatch(typeof(BouncingFlask))]
static class PatchBouncingFlaskCanonicalVars
{
	static MethodBase TargetMethod()
	{
		return AccessTools.PropertyGetter(typeof(BouncingFlask), "CanonicalVars");
	}

	static bool Prefix(ref IEnumerable<DynamicVar> __result)
	{
		__result =
		[
			new PowerVar<PoisonPower>(2m),
			new RepeatVar(4)
		];
		return false;
	}
}

[HarmonyPatch(typeof(BouncingFlask), "OnUpgrade")]
static class PatchBouncingFlaskOnUpgrade
{
	static bool Prefix(BouncingFlask __instance)
	{
		__instance.DynamicVars.Repeat.UpgradeValueBy(2m);
		return false;
	}
}

// [HarmonyPatch(typeof(SwordSage), "OnPlay")]
// static class PatchSwordSage
// {
//     static bool Prefix(StrikeDefect __instance, ref Task __result, PlayerChoiceContext choiceContext,
//         CardPlay cardPlay)
//     {
//         var task = async () =>
//         {
//             await CreatureCmd.TriggerAnim(__instance.Owner.Creature, "Cast", __instance.Owner.Character.CastAnimDelay);
//             await PowerCmd.Apply<SwordSagePower2>(choiceContext,
//                 __instance.Owner.Creature, __instance.DynamicVars["SwordSagePower"].BaseValue,
//                 __instance.Owner.Creature, __instance);
//         };
//
//         __result = PatchHelper.WrapAsync(task);
//         return false;
//     }
// }
