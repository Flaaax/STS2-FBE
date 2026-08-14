using System.Reflection;
using FBE.Scripts.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace FBE.Scripts.VanillaModifiers.Models;

/// <summary>Adds Diplopia to Vakuu's first relic pool and removes Lord's Parasol from the third.</summary>
[HarmonyPatch(typeof(Vakuu))]
internal static class PatchVakuuPoolOne
{
    private static MethodBase TargetMethod() => AccessTools.PropertyGetter(typeof(Vakuu), "Pool1");

    private static void Postfix(Vakuu __instance, ref IEnumerable<EventOption> __result)
    {
        __result = __result.Append(VakuuRelicOption.Create<Diplopia>(__instance));
    }
}

[HarmonyPatch(typeof(Vakuu))]
internal static class PatchVakuuPoolThree
{
    private static MethodBase TargetMethod() => AccessTools.PropertyGetter(typeof(Vakuu), "Pool3");

    private static void Postfix(ref IEnumerable<EventOption> __result)
    {
        var lordsParasolId = ModelDb.Relic<LordsParasol>().Id;
        __result = __result.Where(option => option.Relic?.Id != lordsParasolId);
    }
}

internal static class VakuuRelicOption
{
    private static readonly MethodInfo RelicOptionMethod =
        AccessTools.Method(typeof(AncientEventModel), "RelicOption",
            [typeof(RelicModel), typeof(string), typeof(string)])
        ?? throw new MissingMethodException(typeof(AncientEventModel).FullName, "RelicOption");

    internal static EventOption Create<T>(AncientEventModel ancient) where T : RelicModel
    {
        var relic = ModelDb.Relic<T>().ToMutable();
        var option = RelicOptionMethod.Invoke(ancient, [relic, "INITIAL", null]);
        return (EventOption?)option
               ?? throw new MissingMethodException(typeof(AncientEventModel).FullName, "RelicOption");
    }
}
