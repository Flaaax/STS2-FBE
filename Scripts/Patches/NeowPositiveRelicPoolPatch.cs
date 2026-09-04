using System.Reflection;
using System.Reflection.Emit;
using FBE.Scripts.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace FBE.Scripts.Patches;

/// <summary>将指定遗物混入涅奥的正面遗物候选池。</summary>
[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
internal static class NeowPositiveRelicPoolPatch
{
	private const string PositiveDonePage = "NEOW.pages.DONE.POSITIVE.description";

	// 在这里添加需要混入涅奥正面选项池的遗物。
	private static readonly IReadOnlyList<Func<RelicModel>> NeowPositiveRelicPool =
	[
		static () => ModelDb.Relic<EdensBlessing>()
	];

	private static readonly MethodInfo? PositiveOptionsGetter = typeof(Neow).GetProperty(
		"PositiveOptions", BindingFlags.Instance | BindingFlags.NonPublic)?.GetMethod;

	private static readonly MethodInfo? RelicOptionMethod = typeof(AncientEventModel)
		.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
		.SingleOrDefault(method =>
		{
			if (method.Name != "RelicOption" || method.IsGenericMethodDefinition)
				return false;

			var parameters = method.GetParameters();
			return parameters.Length == 3 &&
			       parameters[0].ParameterType == typeof(RelicModel) &&
			       parameters[1].ParameterType == typeof(string) &&
			       parameters[2].ParameterType == typeof(string);
		});

	private static readonly MethodInfo AddPoolRelicsMethod = AccessTools.Method(
		typeof(NeowPositiveRelicPoolPatch), nameof(AddPoolRelicsToPositiveOptions))!;

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var patchedInstructions = instructions.ToList();
		if (PositiveOptionsGetter is null)
		{
			Entry.Log.Error("[Neow] Could not find the positive-option pool getter; FBE relics were not injected.");
			return patchedInstructions;
		}

		var positiveOptionsGetterIndex = patchedInstructions.FindIndex(instruction => instruction.Calls(PositiveOptionsGetter));
		if (positiveOptionsGetterIndex < 0)
		{
			Entry.Log.Error("[Neow] Could not find the positive-option pool in GenerateInitialOptions; FBE relics were not injected.");
			return patchedInstructions;
		}

		patchedInstructions.InsertRange(positiveOptionsGetterIndex + 1,
		[
			new CodeInstruction(OpCodes.Ldarg_0),
			new CodeInstruction(OpCodes.Call, AddPoolRelicsMethod)
		]);
		return patchedInstructions;
	}

	private static IEnumerable<EventOption> AddPoolRelicsToPositiveOptions(
		IEnumerable<EventOption> options, Neow neow)
	{
		if (RelicOptionMethod is null || neow.Owner is null)
			return options;

		var poolOptions = new List<EventOption>();
		foreach (var relicFactory in NeowPositiveRelicPool)
		{
			var relic = relicFactory().ToMutable();
			var option = (EventOption?)RelicOptionMethod.Invoke(neow, [relic, "INITIAL", PositiveDonePage]);
			if (option is not null)
				poolOptions.Add(option);
		}

		return options.Concat(poolOptions);
	}
}
