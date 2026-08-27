using System.Reflection;
using FBE.Scripts.Cards;
using FBE.Scripts.Config;
using FBE.Scripts.Relics;
using FBE.Scripts.Rewards;
using FBECore.Scripts.HoverTips;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Interop;

namespace FBE.Scripts;

// 必须要加的属性，用于注册Mod。字符串和初始化函数命名一致。
[ModInitializer("Init")]
public class Entry
{
	public const string ModId = "FBE";

	public static Logger Log { get; } = RitsuLibFramework.CreateLogger(ModId);
	public static bool EnableSyncDebugTracePatches { get; private set; }
	public static bool EnableInvestmentPromotionDynamicPreview { get; private set; }

	private static Harmony? _harmony;

	// 初始化函数
	public static void Init()
	{
		// harmony可用，但是最好用ritsu的封装patch，见补丁系统一章
		// var harmony = new Harmony("com.example.testmod");
		// harmony.PatchAll();


		//允许Debug日志（会造成日志膨胀）
		EnableSyncDebugTracePatches = false;
		// 动态刷新“投资推广”的卡牌预览。关闭时对应 Harmony 补丁不会安装。
		EnableInvestmentPromotionDynamicPreview = true;

		// 打patch（即修改游戏代码的功能）用
		// 传入参数随意，只要不和其他人撞车即可
		_harmony = new Harmony("STS2.FBE");
		_harmony.PatchAll();

		FBEConfig.RegisterSettingsPage();

		// RitsuLib 注册器
		var assembly = Assembly.GetExecutingAssembly();
		AssociateRuntimeAssemblyWithMod(assembly);
		StarterDeckBundleReward.Register();
		RegisterCardCarouselPreviews();

		RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Log);
		// 自动注册内容
		ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

#if STS2_Stable
		RegisterSavedPropertyModels();
#endif
		// 使得tscn可以加载自定义脚本
		//ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);
		Log.Info("Mod initialized!");
	}

	private static void RegisterCardCarouselPreviews()
	{
		CardCarouselPreview.RegisterForModel<PackagingBoxCard>(
			box => box.StoredCards,
			new CardCarouselPreviewOptions
			{
				ShowInCompendium = false
			});

		CardCarouselPreview.RegisterForModel<Clicker>(
			clicker => clicker.GetFormCardPreviews(),
			new CardCarouselPreviewOptions
			{
				ShowInCompendium = true
			});
	}

	private static void AssociateRuntimeAssemblyWithMod(Assembly assembly)
	{
		// 普通单 DLL 加载时，ModManager 会自行关联 FBE.dll。
		// 只有多版本分派动态加载的 FBE.Runtime.dll 需要额外关联。
		if (assembly.GetName().Name == ModId)
			return;

#if STS2_Stable
		// 0.107.1 会在初始化函数返回后，将 Bootstrap 主程序集重新写入 Mod.assembly。
		// 等 OnModDetected 触发后再替换为 Runtime，避免这次写回覆盖兼容处理。
		Action<Mod>? onModDetected = null;
		onModDetected = mod =>
		{
			if (mod.manifest?.id != ModId)
				return;

			mod.assembly = assembly;
			Traverse.Create(typeof(ReflectionHelper)).Field("_modTypes").SetValue(null);
			ModManager.OnModDetected -= onModDetected;
			Log.Info($"Associated runtime assembly {assembly} with mod {ModId} after detection (0.107.1 compatibility path).");
		};
		ModManager.OnModDetected += onModDetected;
#else
		ModManager.AssociateAssemblyWithMod(ModId, assembly);
#endif
	}

#if STS2_Stable
	private static void RegisterSavedPropertyModels()
	{
		const BindingFlags flags =
			BindingFlags.Instance |
			BindingFlags.Public |
			BindingFlags.NonPublic;

		foreach (var type in typeof(Entry).Assembly.GetTypes())
		{
			if (!type.IsClass || type.IsAbstract)
				continue;

			if (!typeof(MegaCrit.Sts2.Core.Models.AbstractModel).IsAssignableFrom(type))
				continue;

			var hasSavedProperty = type
				.GetProperties(flags)
				.Any(p => p.GetCustomAttribute<SavedPropertyAttribute>() != null);

			if (!hasSavedProperty)
				continue;

			SavedPropertiesTypeCache.InjectTypeIntoCache(type);
			Log.Info($"Registered SavedProperty model: {type.FullName}");
		}
	}
#endif
}

// [HarmonyPatch(typeof(NPlayerHand), "SelectCardInSimpleMode")]
// [HarmonyPatch([typeof(NHandCardHolder)])]
// static class PatchSelectCardInSimpleMode //单选时跳过确认
// {
// 	static void Postfix(NPlayerHand __instance)
// 	{
// 		var prefs = Traverse.Create(__instance).Field("_prefs").GetValue<CardSelectorPrefs>();
//
// 		if (prefs.MinSelect == 1 && prefs.MaxSelect == 1)
// 		{
// 			Traverse.Create(__instance).Method("CheckIfSelectionComplete").GetValue();
// 		}
// 	}
// }
//
// [HarmonyPatch(typeof(NPlayerHand), "SelectCardInUpgradeMode")]
// [HarmonyPatch([typeof(NHandCardHolder)])]
// static class PatchSelectCardInUpgradeMode //同上
// {
// 	static void Postfix(NPlayerHand __instance)
// 	{
// 		var prefs = Traverse.Create(__instance).Field("_prefs").GetValue<CardSelectorPrefs>();
//
// 		if (prefs.MinSelect == 1 && prefs.MaxSelect == 1)
// 		{
// 			Traverse.Create(__instance).Method("CheckIfSelectionComplete").GetValue();
// 		}
// 	}
// }
