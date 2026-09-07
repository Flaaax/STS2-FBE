using System.Reflection;
using FBECore.Scripts.ContentBlacklist;
using FBECore.Scripts.SettingsPreview;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Ui.Shell;
using STS2RitsuLib.Ui.Shell.Theme;
using STS2RitsuLib.Utils.Persistence;

namespace FBE.Scripts.Config;

public sealed class FBEConfigData
{
	/// <summary>
	/// 键为“内容类别:模型 Entry”，不存在的项默认启用，以兼容新增内容前保存的旧配置。
	/// </summary>
	public Dictionary<string, bool>? ContentEnabled { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// 管理 FBE 自然生成内容的本地开关，并将禁用状态提交给 FBECore。
/// </summary>
public static class FBEConfig
{
	private const string DataKey = "settings";
	private const string ConfigFileName = "config.json";
	private const string SettingsTextTable = "settings_ui";
	private const string BlacklistSourceKey = "settings";

	private static readonly ContentBlacklistSource BlacklistSource = new(Entry.ModId, BlacklistSourceKey);
	private static IReadOnlyList<ContentOption> _contentOptions = [];
	private static bool _dataRegistered;
	private static bool _contentBlacklistInitializationScheduled;
	private static bool _blacklistInitialized;
	private static bool _settingsPageRegistered;
	private static bool _entryActionsReflectionFailureLogged;
	private static long _blacklistRevision;
	private static MethodInfo? _registerRefreshMethod;

	/// <summary>
	/// 注册配置存储。必须早于读取配置或创建设置绑定。
	/// </summary>
	public static void RegisterDataStore()
	{
		if (_dataRegistered)
			return;

		ModDataStore.For(Entry.ModId).Register(
			key: DataKey,
			fileName: ConfigFileName,
			scope: SaveScope.Global,
			defaultFactory: static () => new FBEConfigData(),
			autoCreateIfMissing: true);

		_dataRegistered = true;
	}

	/// <summary>
	/// 在模型 ID 已分配后初始化内容黑名单。订阅可重放事件，兼容订阅发生在模型初始化之后的情况。
	/// </summary>
	public static void ScheduleContentBlacklistInitialization(Assembly contentAssembly)
	{
		ArgumentNullException.ThrowIfNull(contentAssembly);
		RegisterDataStore();
		if (_contentBlacklistInitializationScheduled)
			return;

		try
		{
			_contentBlacklistInitializationScheduled = true;
			RitsuLibFramework.SubscribeLifecycleOnce<ModelIdsInitializedEvent>(_ =>
			{
				if (InitializeContentBlacklist(contentAssembly))
					RegisterSettingsPage();
			});
		}
		catch (Exception exception)
		{
			_contentBlacklistInitializationScheduled = false;
			Entry.Log.Error($"[ContentBlacklist] Failed to schedule FBE content settings: {exception}");
		}
	}

	/// <summary>
	/// 扫描已完成 ID 分配的 FBE 模型、登记 FBECore 目录，并提交当前本地配置。
	/// </summary>
	private static bool InitializeContentBlacklist(Assembly contentAssembly)
	{
		if (_blacklistInitialized)
			return true;

		try
		{
			_contentOptions = DiscoverContent(contentAssembly);
			if (_contentOptions.Count == 0)
			{
				Entry.Log.Error("[ContentBlacklist] No FBE content was discovered after model initialization; settings page was not registered.");
				return false;
			}

			ContentBlacklistRegistry.RegisterCatalog(Entry.ModId,
				_contentOptions.Select(static option => option.Content));
			ContentBlacklistRegistry.EnableHostAuthoritativeSync(Entry.ModId);
			_blacklistInitialized = true;
			ApplyContentBlacklist(ReadConfig());

			Entry.Log.Info($"[ContentBlacklist] Registered {_contentOptions.Count} FBE content toggle(s).");
			return true;
		}
		catch (Exception exception)
		{
			Entry.Log.Error($"[ContentBlacklist] Failed to initialize FBE content settings: {exception}");
			return false;
		}
	}

	/// <summary>
	/// 在内容目录准备好后注册设置页。
	/// </summary>
	public static void RegisterSettingsPage()
	{
		RegisterDataStore();
		if (_settingsPageRegistered)
			return;
		if (!_blacklistInitialized || _contentOptions.Count == 0)
		{
			Entry.Log.Error("[ContentBlacklist] Refusing to register an empty FBE settings page.");
			return;
		}

		RitsuLibFramework.RegisterModSettings(Entry.ModId, page =>
		{
			page
				.WithTitle(Text("FBE_SETTINGS_PAGE.title", "FBE 设置"))
				.WithModDisplayName(Text("FBE_SETTINGS_MOD_DISPLAY_NAME.title", "FBE"))
				.WithVisibleOnHostSurfaces(ModSettingsHostSurface.MainMenu | ModSettingsHostSurface.RunPause);

			page.AddSection("content_actions", section => section
				.WithTitle(Text("FBE_SETTINGS_CONTENT_ACTIONS.title", "内容批量操作"))
				.WithDescription(Text("FBE_SETTINGS_CONTENT_ACTIONS.description",
					"这些操作会立即保存并更新 FBECore 黑名单。"))
				.AddButton(
					"enable_all_content",
					Text("FBE_SETTINGS_CONTENT_ACTIONS_ENABLE_ALL.title", "全部启用"),
					Text("FBE_SETTINGS_CONTENT_ACTIONS_ENABLE_ALL.button", "启用全部"),
					host =>
					{
						SetAllContentEnabled(true);
						host.RequestRefreshAfterDataModelBatchChange();
					})
				.AddButton(
					"disable_all_content",
					Text("FBE_SETTINGS_CONTENT_ACTIONS_DISABLE_ALL.title", "全部禁用"),
					Text("FBE_SETTINGS_CONTENT_ACTIONS_DISABLE_ALL.button", "禁用全部"),
					host =>
					{
						SetAllContentEnabled(false);
						host.RequestRefreshAfterDataModelBatchChange();
					},
					ModSettingsButtonTone.Danger)
				.AddButton(
					"restore_content_defaults",
					Text("FBE_SETTINGS_CONTENT_ACTIONS_RESTORE_DEFAULTS.title", "恢复默认"),
					Text("FBE_SETTINGS_CONTENT_ACTIONS_RESTORE_DEFAULTS.button", "恢复全部启用"),
					host =>
					{
						RestoreContentDefaults();
						host.RequestRefreshAfterDataModelBatchChange();
					}));

			foreach (var group in _contentOptions.GroupBy(static option => option.Kind)
				         .OrderBy(static group => group.Key))
			{
				var kind = group.Key;
				page.AddSection($"content_{kind.ToString().ToLowerInvariant()}", section =>
				{
					section
						.WithTitle(ContentKindTitle(kind))
						.WithDescription(Text("FBE_SETTINGS_CONTENT_SECTION.description",
							"关闭的内容不会自然出现；联机时以房主设置为准。"))
						.Collapsible();

					foreach (var option in group.OrderBy(static option => option.Content.ContentId, StringComparer.Ordinal))
					{
						var label = ModSettingsText.LocString(option.LocalizationTable, option.LocalizationKey,
							option.FallbackTitle);
						if (option.Kind is ContentBlacklistKind.Card or ContentBlacklistKind.Relic or ContentBlacklistKind.Potion)
						{
							section.AddCustom(option.SettingId, label,
								host => CreateContentPreviewToggleRow(option, label, host));
						}
						else
						{
							section.AddToggle(option.SettingId, label, CreateContentBinding(option));
						}
					}
				});
			}
		});

		_settingsPageRegistered = true;
	}

	private static IReadOnlyList<ContentOption> DiscoverContent(Assembly contentAssembly)
	{
		List<ContentOption> options = [];
		// 事件池内容由事件或专用逻辑显式发放，通用自然生成过滤不会可靠覆盖它们。
		AddModels(ModelDb.AllCards.Where(static card =>
			card.Pool is not EventCardPool and not TokenCardPool &&
			card.Type is not CardType.Status), ContentBlacklistKind.Card, contentAssembly, options,
			static model => ("cards", $"{model.Id.Entry}.title"));
		AddModels(ModelDb.AllRelics.Where(static relic => relic.Pool is not EventRelicPool), ContentBlacklistKind.Relic, contentAssembly, options,
			static model => ("relics", $"{model.Id.Entry}.title"));
		AddModels(ModelDb.AllPotions, ContentBlacklistKind.Potion, contentAssembly, options,
			static model => ("potions", $"{model.Id.Entry}.title"));
		AddModels(ModelDb.AllEvents, ContentBlacklistKind.Event, contentAssembly, options,
			static model => ("events", $"{model.Id.Entry}.title"));
		AddModels(ModelDb.AllAncients, ContentBlacklistKind.Ancient, contentAssembly, options,
			static model => ("ancients", $"{model.Id.Entry}.title"));
		AddModels(ModelDb.AllEncounters, ContentBlacklistKind.Encounter, contentAssembly, options,
			static model => GetEncounterLocalization(model));

		return options
			.DistinctBy(static option => option.Content)
			.OrderBy(static option => option.Kind)
			.ThenBy(static option => option.Content.ContentId, StringComparer.Ordinal)
			.ToArray();
	}

	private static void AddModels<TModel>(
		IEnumerable<TModel> models,
		ContentBlacklistKind kind,
		Assembly contentAssembly,
		List<ContentOption> destination,
		Func<TModel, (string Table, string Key)> localization)
		where TModel : AbstractModel
	{
		foreach (var model in models.Where(model => model.GetType().Assembly == contentAssembly))
		{
			var content = new ContentBlacklistContent(kind, model.Id.Entry);
			var (table, key) = localization(model);
			destination.Add(new ContentOption(content, table, key, model.Id.Entry, model));
		}
	}

	/// <summary>
	/// 创建与 RitsuLib 标准设置行一致的内容开关，并仅将右侧开关作为预览的悬浮目标。
	/// </summary>
	private static Control CreateContentPreviewToggleRow(
		ContentOption option,
		ModSettingsText labelText,
		IModSettingsUiActionHost host)
	{
		var binding = CreateContentBinding(option);
		var line = new MarginContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		line.AddThemeConstantOverride("margin_left", 8);
		line.AddThemeConstantOverride("margin_top", 4);
		line.AddThemeConstantOverride("margin_right", 8);
		line.AddThemeConstantOverride("margin_bottom", 4);

		var surface = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		surface.AddThemeStyleboxOverride("panel", RitsuShellChromeStyles.CreateSurfaceStyle());
		line.AddChild(surface);

		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 20);
		surface.AddChild(row);

		var label = new Label
		{
			Text = labelText.Resolve(),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Off,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontOverride("font", RitsuShellTheme.Current.Font.Body);
		label.AddThemeFontSizeOverride("font_size", RitsuShellTheme.Current.Metric.FontSize.SettingLineTitle);
		label.AddThemeColorOverride("font_color", RitsuShellTheme.Current.Text.LabelPrimary);
		row.AddChild(label);

		var toggle = new ModSettingsToggleControl(binding.Read(), value =>
		{
			binding.Write(value);
			host.MarkDirty(binding);
			host.RequestRefresh();
		});
		RegisterCustomToggleRefresh(toggle, binding, host);
		row.AddChild(toggle);
		var actions = CreateDefaultEntryActionsControl(host, binding);
		if (actions is not null)
			row.AddChild(actions);

		ContentSettingsPreview.Attach(
			toggle,
			() => option.Model,
			new ContentSettingsPreviewOptions
			{
				DiagnosticKey = $"{Entry.ModId}:{option.Content.ContentId}",
			});
		return line;
	}

	/// <summary>
	/// 为自定义预览开关注册 UI 刷新回调。批量操作通过 <see cref="IModSettingsUiActionHost.RequestRefreshAfterDataModelBatchChange"/>
	/// 触发完整刷新，但自定义行不会自动像标准开关那样重新读取绑定值，必须显式注册。
	/// </summary>
	private static void RegisterCustomToggleRefresh(
		ModSettingsToggleControl toggle,
		ModSettingsValueBinding<FBEConfigData, bool> binding,
		IModSettingsUiActionHost host)
	{
		try
		{
			_registerRefreshMethod ??= host.GetType().GetMethod(
				"RegisterRefresh",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				[typeof(Action)],
				null);

			_registerRefreshMethod?.Invoke(host, [() =>
			{
				if (GodotObject.IsInstanceValid(toggle) && toggle.IsInsideTree())
					toggle.SetValue(binding.Read());
			}]);
		}
		catch (Exception exception)
		{
			Entry.Log.Warn($"[ContentBlacklist] Could not register custom toggle refresh callback: {exception.Message}");
		}
	}

	/// <summary>
	/// 自定义行不能直接使用 RitsuLib 的内部标准行工厂；反射复用其操作按钮，以保持复制/粘贴菜单与普通开关一致。
	/// </summary>
	private static Control? CreateDefaultEntryActionsControl(
		IModSettingsUiActionHost host,
		ModSettingsValueBinding<FBEConfigData, bool> binding)
	{
		try
		{
			var method = typeof(ModSettingsUiFactory)
				.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
				.FirstOrDefault(candidate =>
					candidate.Name == "CreateEntryActionsButton" &&
					candidate.IsGenericMethodDefinition &&
					candidate.GetGenericArguments().Length == 1 &&
					candidate.GetParameters().Length == 3);
			if (method is null)
				throw new MissingMethodException(typeof(ModSettingsUiFactory).FullName, "CreateEntryActionsButton");

			return method.MakeGenericMethod(typeof(bool)).Invoke(null,
			[
				host,
				binding,
				ModSettingsMenuCapabilities.All,
			]) as Control;
		}
		catch (Exception exception)
		{
			if (!_entryActionsReflectionFailureLogged)
			{
				_entryActionsReflectionFailureLogged = true;
				Entry.Log.Warn($"[ContentBlacklist] Could not create the standard settings action button; preview toggle rows will omit it. {exception}");
			}

			return null;
		}
	}

	private static (string Table, string Key) GetEncounterLocalization(EncounterModel encounter)
	{
		var monster = encounter.AllPossibleMonsters.FirstOrDefault();
		return monster is null
			? ("encounters", $"{encounter.Id.Entry}.title")
			: ("monsters", $"{monster.Id.Entry}.name");
	}

	private static ModSettingsValueBinding<FBEConfigData, bool> CreateContentBinding(ContentOption option)
	{
		return new ModSettingsValueBinding<FBEConfigData, bool>(
			Entry.ModId,
			DataKey,
			SaveScope.Global,
			settings => IsContentEnabled(settings, option.StorageKey),
			(settings, value) =>
			{
				settings.ContentEnabled ??= new Dictionary<string, bool>(StringComparer.Ordinal);
				settings.ContentEnabled[option.StorageKey] = value;
				ApplyContentBlacklist(settings);
			});
	}

	private static bool IsContentEnabled(FBEConfigData settings, string storageKey)
	{
		return settings.ContentEnabled is null ||
		       !settings.ContentEnabled.TryGetValue(storageKey, out var enabled) ||
		       enabled;
	}

	private static void ApplyContentBlacklist(FBEConfigData settings)
	{
		if (!_blacklistInitialized)
			return;

		try
		{
			var disabled = _contentOptions
				.Where(option => !IsContentEnabled(settings, option.StorageKey))
				.Select(static option => option.Content)
				.ToArray();
			var revision = Interlocked.Increment(ref _blacklistRevision);
			ContentBlacklistRegistry.ReplaceContribution(new ContentBlacklistContribution(
				Entry.ModId,
				BlacklistSource,
				revision,
				disabled));
		}
		catch (Exception exception)
		{
			Entry.Log.Error($"[ContentBlacklist] Failed to apply FBE settings: {exception}");
		}
	}

	private static void SetAllContentEnabled(bool enabled)
	{
		ModifyContentSettings(settings =>
		{
			settings.ContentEnabled ??= new Dictionary<string, bool>(StringComparer.Ordinal);
			foreach (var option in _contentOptions)
				settings.ContentEnabled[option.StorageKey] = enabled;
		});
	}

	private static void RestoreContentDefaults()
	{
		ModifyContentSettings(settings =>
			settings.ContentEnabled = new Dictionary<string, bool>(StringComparer.Ordinal));
	}

	private static void ModifyContentSettings(Action<FBEConfigData> modify)
	{
		try
		{
			var store = RitsuLibFramework.GetDataStore(Entry.ModId);
			store.Modify<FBEConfigData>(DataKey, settings =>
			{
				modify(settings);
				ApplyContentBlacklist(settings);
			});
			store.Save(DataKey);
		}
		catch (Exception exception)
		{
			Entry.Log.Error($"[ContentBlacklist] Failed to update all FBE content settings: {exception}");
		}
	}

	private static FBEConfigData ReadConfig() =>
		RitsuLibFramework.GetDataStore(Entry.ModId).Get<FBEConfigData>(DataKey);

	private static ModSettingsText ContentKindTitle(ContentBlacklistKind kind)
	{
		var key = kind switch
		{
			ContentBlacklistKind.Card => "FBE_SETTINGS_CONTENT_KIND_CARDS.title",
			ContentBlacklistKind.Relic => "FBE_SETTINGS_CONTENT_KIND_RELICS.title",
			ContentBlacklistKind.Potion => "FBE_SETTINGS_CONTENT_KIND_POTIONS.title",
			ContentBlacklistKind.Event => "FBE_SETTINGS_CONTENT_KIND_EVENTS.title",
			ContentBlacklistKind.Ancient => "FBE_SETTINGS_CONTENT_KIND_ANCIENTS.title",
			ContentBlacklistKind.Encounter => "FBE_SETTINGS_CONTENT_KIND_ENCOUNTERS.title",
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
		};
		var fallback = kind switch
		{
			ContentBlacklistKind.Card => "卡牌",
			ContentBlacklistKind.Relic => "遗物",
			ContentBlacklistKind.Potion => "药水",
			ContentBlacklistKind.Event => "事件",
			ContentBlacklistKind.Ancient => "先古之民",
			ContentBlacklistKind.Encounter => "遭遇战",
			_ => "内容",
		};
		return Text(key, fallback);
	}

	private static ModSettingsText Text(string key, string fallback) =>
		ModSettingsText.LocString(SettingsTextTable, key, fallback);

	private sealed record ContentOption(
		ContentBlacklistContent Content,
		string LocalizationTable,
		string LocalizationKey,
		string FallbackTitle,
		AbstractModel Model)
	{
		public ContentBlacklistKind Kind => Content.Kind;
		public string StorageKey => $"{Kind}:{Content.ContentId}";
		public string SettingId => $"toggle_{Kind.ToString().ToLowerInvariant()}_{Content.ContentId.ToLowerInvariant()}";
	}
}
