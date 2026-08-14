using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace FBE.Scripts.Nodes;

public partial class NChooseBundlesSelectionScreen : Control, IOverlayScreen
{
	private const string ScenePath = "res://FBE/scenes/choose_bundles_selection_screen.tscn";
	private const string CommonBannerScenePath = "res://scenes/ui/common_banner.tscn";
	private const string PeekButtonScenePath = "res://scenes/combat/peek_button.tscn";
	private const string BackButtonScenePath = "res://scenes/ui/back_button.tscn";
	private const string ConfirmButtonScenePath = "res://scenes/ui/confirm_button.tscn";
	private const float CardXSpacing = 400f;
	private const float BundleXSpacing = 280f;

	private static readonly Color SelectedColor = new(0.7f, 0.7f, 0.7f);

	private readonly TaskCompletionSource<IReadOnlyList<int>> _completionSource = new();
	private readonly List<NCardBundle> _bundleNodes = [];
	private readonly List<int> _selectedIndexes = [];

	private IReadOnlyList<IReadOnlyList<CardModel>> _bundles = [];
	private int _requiredSelections;
	private Control _bundleGrid = null!;
	private Control _bundlePreviewContainer = null!;
	private Control _bundlePreviewCards = null!;
	private NBackButton _previewCancelButton = null!;
	private NConfirmButton _previewConfirmButton = null!;
	private NConfirmButton _finalConfirmButton = null!;
	private NCardBundle? _previewedBundle;
	private NCommonBanner _banner = null!;
	private NPeekButton _peekButton = null!;
	private Tween? _fadeTween;
	private Tween? _cardTween;

	public NetScreenType ScreenType => NetScreenType.CardSelection;
	public bool UseSharedBackstop => true;

	public Control DefaultFocusedControl => _bundlePreviewContainer.Visible
		? _bundlePreviewCards.GetChild<Control>(_bundlePreviewCards.GetChildCount() - 1)
		: _bundleNodes[0].Hitbox;

	public override void _Ready()
	{
		_bundleGrid = GetNode<Control>("%BundleGrid");
		_bundlePreviewContainer = GetNode<Control>("%BundlePreviewContainer");
		_bundlePreviewCards = GetNode<Control>("%Cards");
		CreateGameUiNodes();

		_previewCancelButton.Connect(
			NClickableControl.SignalName.Released,
			Callable.From<NButton>(CancelPreview));
		_previewConfirmButton.Connect(
			NClickableControl.SignalName.Released,
			Callable.From<NButton>(TogglePreviewedBundle));
		_finalConfirmButton.Connect(
			NClickableControl.SignalName.Released,
			Callable.From<NButton>(ConfirmFinalSelection));
		CreateBundleNodes();

		_bundlePreviewContainer.Visible = false;
		_bundlePreviewContainer.MouseFilter = MouseFilterEnum.Ignore;
		_previewCancelButton.Disable();
		_previewConfirmButton.Disable();
		_finalConfirmButton.Disable();

		_peekButton.AddTargets(_banner, _bundleGrid, _bundlePreviewContainer);

		RefreshSelectionVisuals();
		_banner.AnimateIn();
	}

	private void CreateGameUiNodes()
	{
		_banner = InstantiateGameScene<NCommonBanner>(CommonBannerScenePath);
		_banner.Name = "Banner";
		AddChild(_banner);

		_peekButton = InstantiateGameScene<NPeekButton>(PeekButtonScenePath);
		_peekButton.Name = "PeekButton";
		AddChild(_peekButton);

		_previewCancelButton = InstantiateGameScene<NBackButton>(BackButtonScenePath);
		_previewCancelButton.Name = "PreviewCancel";
		_bundlePreviewContainer.AddChild(_previewCancelButton);

		_previewConfirmButton = InstantiateGameScene<NConfirmButton>(ConfirmButtonScenePath);
		_previewConfirmButton.Name = "PreviewConfirm";
		_bundlePreviewContainer.AddChild(_previewConfirmButton);

		_finalConfirmButton = InstantiateGameScene<NConfirmButton>(ConfirmButtonScenePath);
		_finalConfirmButton.Name = "FinalConfirm";
		AddChild(_finalConfirmButton);
	}

	private static T InstantiateGameScene<T>(string path) where T : Node
	{
		return PreloadManager.Cache.GetScene(path).Instantiate<T>();
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (!_completionSource.Task.IsCompleted)
			_completionSource.SetCanceled();
	}

	public static NChooseBundlesSelectionScreen ShowScreen(
		IReadOnlyList<IReadOnlyList<CardModel>> bundles,
		int requiredSelections)
	{
		var scene = GD.Load<PackedScene>(ScenePath);
		var screen = scene.Instantiate<NChooseBundlesSelectionScreen>();
		screen.Name = nameof(NChooseBundlesSelectionScreen);
		screen._bundles = bundles;
		screen._requiredSelections = requiredSelections;
		NOverlayStack.Instance?.Push(screen);
		return screen;
	}

	private void CreateBundleNodes()
	{
		for (var i = 0; i < _bundles.Count; i++)
		{
			var bundleNode = NCardBundle.Create(_bundles[i])
			                 ?? throw new InvalidOperationException("Failed to create a card bundle node.");
			_bundleGrid.AddChildSafely(bundleNode);
			bundleNode.Connect(
				NCardBundle.SignalName.Clicked,
				Callable.From<NCardBundle>(OpenBundlePreview));
			bundleNode.Position = GetBundlePosition(i);
			_bundleNodes.Add(bundleNode);
		}

		RefreshFocusNeighbors();
	}

	private void RefreshFocusNeighbors()
	{
		for (var i = 0; i < _bundleNodes.Count; i++)
		{
			var left = (i + _bundleNodes.Count - 1) % _bundleNodes.Count;
			var right = (i + 1) % _bundleNodes.Count;

			var hitbox = _bundleNodes[i].Hitbox;
			hitbox.FocusNeighborLeft = _bundleNodes[left].Hitbox.GetPath();
			hitbox.FocusNeighborRight = _bundleNodes[right].Hitbox.GetPath();
			hitbox.FocusNeighborTop = hitbox.GetPath();
			hitbox.FocusNeighborBottom = hitbox.GetPath();
		}
	}

	private Vector2 GetBundlePosition(int index)
	{
		var centeredIndex = index - (_bundles.Count - 1) * 0.5f;
		return Vector2.Right * centeredIndex * BundleXSpacing + Vector2.Down * 60f;
	}

	private void OpenBundlePreview(NCardBundle bundleNode)
	{
		_previewedBundle = bundleNode;
		_bundleGrid.Visible = false;
		_bundlePreviewContainer.Visible = true;
		_bundlePreviewContainer.MouseFilter = MouseFilterEnum.Stop;
		_finalConfirmButton.Disable();
		_previewCancelButton.Enable();

		var bundleIndex = _bundleNodes.IndexOf(bundleNode);
		var isAlreadySelected = _selectedIndexes.Contains(bundleIndex);
		if (isAlreadySelected || _selectedIndexes.Count < _requiredSelections)
			_previewConfirmButton.Enable();
		else
			_previewConfirmButton.Disable();

		_banner.ChangeText(GetText(isAlreadySelected ? "previewRemovePrompt" : "previewAddPrompt"));

		var start = Vector2.Right * (bundleNode.Bundle.Count - 1) * CardXSpacing * 0.5f;
		var cardNodes = bundleNode.RemoveCardNodes();
		_cardTween?.Kill();
		_cardTween = CreateTween().SetParallel();

		for (var i = 0; i < cardNodes.Count; i++)
		{
			var globalPosition = cardNodes[i].GlobalPosition;
			var holder = NPreviewCardHolder.Create(cardNodes[i], showHoverTips: true, scaleOnHover: true)
			             ?? throw new InvalidOperationException("Failed to create a card preview holder.");
			_bundlePreviewCards.AddChildSafely(holder);
			holder.GlobalPosition = globalPosition;
			holder.Connect(NCardHolder.SignalName.Pressed, Callable.From<NCardHolder>(OpenCardInspection));
			cardNodes[i].UpdateVisuals(PileType.None, CardPreviewMode.Normal);
			_cardTween.TweenProperty(holder, "position", start + Vector2.Left * CardXSpacing * i, 0.5)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Expo);
		}

		RefreshPreviewFocusNeighbors();
		_bundlePreviewCards.GetChild<Control>(_bundlePreviewCards.GetChildCount() - 1).TryGrabFocus();
	}

	private void RefreshPreviewFocusNeighbors()
	{
		for (var i = 0; i < _bundlePreviewCards.GetChildCount(); i++)
		{
			var holder = _bundlePreviewCards.GetChild<NPreviewCardHolder>(i);
			var left = (i + 1) % _bundlePreviewCards.GetChildCount();
			var right = (i + _bundlePreviewCards.GetChildCount() - 1) % _bundlePreviewCards.GetChildCount();
			holder.FocusNeighborLeft = _bundlePreviewCards.GetChild(left).GetPath();
			holder.FocusNeighborRight = _bundlePreviewCards.GetChild(right).GetPath();
			holder.FocusNeighborTop = holder.Hitbox.GetPath();
			holder.FocusNeighborBottom = holder.Hitbox.GetPath();
		}
	}

	private static void OpenCardInspection(NCardHolder cardHolder)
	{
		NGame.Instance!.GetInspectCardScreen().Open([cardHolder.CardNode!.Model!], 0);
	}

	private void CancelPreview(NButton _)
	{
		ClosePreview();
	}

	private void TogglePreviewedBundle(NButton _)
	{
		if (_previewedBundle == null)
			return;

		var index = _bundleNodes.IndexOf(_previewedBundle);
		if (_selectedIndexes.Contains(index))
			_selectedIndexes.Remove(index);
		else if (_selectedIndexes.Count < _requiredSelections)
			_selectedIndexes.Add(index);

		ClosePreview();
	}

	private void ClosePreview()
	{
		_cardTween?.Kill();
		var focusTarget = _previewedBundle;
		_previewedBundle?.ReAddCardNodes();

		foreach (var holder in _bundlePreviewCards.GetChildren().OfType<NPreviewCardHolder>())
		{
			_bundlePreviewCards.RemoveChildSafely(holder);
			holder.QueueFreeSafely();
		}

		_previewedBundle = null;
		_bundlePreviewContainer.Visible = false;
		_bundlePreviewContainer.MouseFilter = MouseFilterEnum.Ignore;
		_bundleGrid.Visible = true;
		_previewCancelButton.Disable();
		_previewConfirmButton.Disable();
		RefreshSelectionVisuals();
		focusTarget?.Hitbox.TryGrabFocus();
	}

	private void RefreshSelectionVisuals()
	{
		for (var i = 0; i < _bundleNodes.Count; i++)
		{
			var selected = _selectedIndexes.Contains(i);
			_bundleNodes[i].Modulate = selected ? SelectedColor : Colors.White;
			_bundleNodes[i].Position = GetBundlePosition(i);
		}

		var prompt = GetText("selectionScreenPrompt")
			.Replace("{Required}", _requiredSelections.ToString());
		_banner.ChangeText(prompt);

		if (_selectedIndexes.Count == _requiredSelections)
			_finalConfirmButton.Enable();
		else
			_finalConfirmButton.Disable();
	}

	private void ConfirmFinalSelection(NButton _)
	{
		if (_selectedIndexes.Count != _requiredSelections)
			return;

		foreach (var index in _selectedIndexes)
		{
			foreach (var cardNode in _bundleNodes[index].CardNodes)
			{
				NRun.Instance!.GlobalUi.ReparentCard(cardNode);
				var vfx = NCardFlyVfx.Create(
					cardNode,
					PileType.Deck,
					isAddingToPile: true,
					cardNode.Model!.Owner.Character.TrailPath);
				NRun.Instance.GlobalUi.TopBar.TrailContainer.AddChildSafely(vfx);
			}
		}

		_finalConfirmButton.Disable();
		_completionSource.SetResult(_selectedIndexes.ToList());
	}

	private static string GetText(string key)
	{
		return new LocString("relics", $"FBE_RELIC_STARTER_DECK.{key}").GetRawText();
	}

	public async Task<IReadOnlyList<int>> BundlesSelected()
	{
		var result = await _completionSource.Task;
		NOverlayStack.Instance!.Remove(this);
		return result;
	}

	public void AfterOverlayOpened()
	{
		Modulate = Colors.Transparent;
		_fadeTween?.Kill();
		_fadeTween = CreateTween();
		_fadeTween.TweenProperty(this, "modulate:a", 1f, 0.4);
	}

	public void AfterOverlayClosed()
	{
		_fadeTween?.Kill();
		this.QueueFreeSafely();
	}

	public void AfterOverlayShown()
	{
		Visible = true;
		if (_bundlePreviewContainer.Visible)
		{
			_previewCancelButton.Enable();
			var index = _previewedBundle == null ? -1 : _bundleNodes.IndexOf(_previewedBundle);
			if (index >= 0 && (_selectedIndexes.Contains(index) || _selectedIndexes.Count < _requiredSelections))
				_previewConfirmButton.Enable();
		}
		else if (_selectedIndexes.Count == _requiredSelections)
		{
			_finalConfirmButton.Enable();
		}
	}

	public void AfterOverlayHidden()
	{
		Visible = false;
		_previewCancelButton.Disable();
		_previewConfirmButton.Disable();
		_finalConfirmButton.Disable();
	}
}
