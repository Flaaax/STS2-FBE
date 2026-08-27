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
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace FBE.Scripts.Nodes;

// Mirrors the native Scroll Boxes presentation, with an explicit exit from the bundle choice.
public partial class NChooseOptionalBundleSelectionScreen : Control, IOverlayScreen, IScreenContext
{
	private const string ScenePath = "res://FBE/scenes/choose_optional_bundle_selection_screen.tscn";
	private const float CardXSpacing = 400f;

	private readonly TaskCompletionSource<int> _completionSource = new();
	private readonly List<NCardBundle> _bundleNodes = [];

	private IReadOnlyList<IReadOnlyList<CardModel>> _bundles = [];
	private Control _bundleRow = null!;
	private Control _bundlePreviewContainer = null!;
	private Control _bundlePreviewCards = null!;
	private NBackButton _previewCancelButton = null!;
	private NConfirmButton _previewConfirmButton = null!;
	private NBackButton _skipButton = null!;
	private NCardBundle? _selectedBundle;
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
		_bundleRow = GetNode<Control>("%BundleRow");
		_bundlePreviewContainer = GetNode<Control>("%BundlePreviewContainer");
		_bundlePreviewCards = GetNode<Control>("%Cards");
		_previewCancelButton = GetNode<NBackButton>("%Cancel");
		_previewConfirmButton = GetNode<NConfirmButton>("%Confirm");
		_skipButton = GetNode<NBackButton>("%Skip");
		_banner = GetNode<NCommonBanner>("Banner");
		_peekButton = GetNode<NPeekButton>("%PeekButton");

		_banner.label.SetTextAutoSize(new LocString("gameplay_ui", "CHOOSE_A_PACK").GetRawText());
		_banner.AnimateIn();
		_previewCancelButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(CancelPreview));
		_previewConfirmButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(ConfirmSelection));
		_skipButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(SkipSelection));
		_previewCancelButton.Disable();
		_previewConfirmButton.Disable();

		CreateBundleNodes();
		_bundlePreviewContainer.Visible = false;
		_bundlePreviewContainer.MouseFilter = MouseFilterEnum.Ignore;
		_peekButton.AddTargets(_banner, _bundleRow, _bundlePreviewContainer, _skipButton);
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (!_completionSource.Task.IsCompleted)
			_completionSource.SetCanceled();
	}

	public static NChooseOptionalBundleSelectionScreen ShowScreen(IReadOnlyList<IReadOnlyList<CardModel>> bundles)
	{
		var screen = GD.Load<PackedScene>(ScenePath).Instantiate<NChooseOptionalBundleSelectionScreen>();
		screen.Name = nameof(NChooseOptionalBundleSelectionScreen);
		screen._bundles = bundles;
		NOverlayStack.Instance?.Push(screen);
		return screen;
	}

	public async Task<int> SelectedIndex()
	{
		var index = await _completionSource.Task;
		NOverlayStack.Instance?.Remove(this);
		return index;
	}

	private void CreateBundleNodes()
	{
		var startPosition = Vector2.Left * (_bundles.Count - 1) * CardXSpacing * 0.5f;
		for (var i = 0; i < _bundles.Count; i++)
		{
			var bundleNode = NCardBundle.Create(_bundles[i])
			                 ?? throw new InvalidOperationException("Failed to create a card bundle node.");
			_bundleRow.AddChildSafely(bundleNode);
			bundleNode.Connect(NCardBundle.SignalName.Clicked, Callable.From<NCardBundle>(OpenBundlePreview));
			bundleNode.Scale = bundleNode.smallScale;
			bundleNode.Position += startPosition + Vector2.Right * CardXSpacing * i;
			_bundleNodes.Add(bundleNode);
		}

		for (var i = 0; i < _bundleNodes.Count; i++)
		{
			var previous = (i + _bundleNodes.Count - 1) % _bundleNodes.Count;
			var next = (i + 1) % _bundleNodes.Count;
			var hitbox = _bundleNodes[i].Hitbox;
			hitbox.FocusNeighborLeft = _bundleNodes[previous].Hitbox.GetPath();
			hitbox.FocusNeighborRight = _bundleNodes[next].Hitbox.GetPath();
			hitbox.FocusNeighborTop = hitbox.GetPath();
			hitbox.FocusNeighborBottom = _skipButton.GetPath();
		}

		_skipButton.FocusNeighborTop = _bundleNodes[0].Hitbox.GetPath();
	}

	private void OpenBundlePreview(NCardBundle bundleNode)
	{
		_banner.AnimateOut();
		_selectedBundle = bundleNode;
		_bundlePreviewContainer.Visible = true;
		_bundlePreviewContainer.MouseFilter = MouseFilterEnum.Stop;
		_bundleRow.Visible = false;
		_previewCancelButton.Enable();
		_previewConfirmButton.Enable();

		var startPosition = Vector2.Right * (bundleNode.Bundle.Count - 1) * CardXSpacing * 0.5f;
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
			_cardTween.TweenProperty(holder, "position", startPosition + Vector2.Left * CardXSpacing * i, 0.5f)
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
			var previous = (i + _bundlePreviewCards.GetChildCount() - 1) % _bundlePreviewCards.GetChildCount();
			var next = (i + 1) % _bundlePreviewCards.GetChildCount();
			holder.FocusNeighborLeft = _bundlePreviewCards.GetChild(next).GetPath();
			holder.FocusNeighborRight = _bundlePreviewCards.GetChild(previous).GetPath();
			holder.FocusNeighborTop = holder.Hitbox.GetPath();
			holder.FocusNeighborBottom = _skipButton.GetPath();
		}
	}

	private static void OpenCardInspection(NCardHolder cardHolder)
	{
		NGame.Instance!.GetInspectCardScreen().Open([cardHolder.CardNode!.Model!], 0);
	}

	private void CancelPreview(NButton _)
	{
		_banner.AnimateIn();
		_bundlePreviewContainer.Visible = false;
		_bundlePreviewContainer.MouseFilter = MouseFilterEnum.Ignore;
		_cardTween?.Kill();
		_selectedBundle?.ReAddCardNodes();
		_selectedBundle?.Hitbox.TryGrabFocus();
		_previewCancelButton.Disable();
		_previewConfirmButton.Disable();
		_selectedBundle = null;
		_bundleRow.Visible = true;
	}

	private void ConfirmSelection(NButton _)
	{
		if (_selectedBundle is null)
			return;

		foreach (var cardNode in _selectedBundle.CardNodes)
		{
			NRun.Instance!.GlobalUi.ReparentCard(cardNode);
			var vfx = NCardFlyVfx.Create(
				cardNode,
				PileType.Deck,
				isAddingToPile: true,
				cardNode.Model!.Owner.Character.TrailPath);
			NRun.Instance.GlobalUi.TopBar.TrailContainer.AddChildSafely(vfx);
		}

		_completionSource.TrySetResult(_bundleNodes.IndexOf(_selectedBundle));
	}

	private void SkipSelection(NButton _)
	{
		_completionSource.TrySetResult(-1);
	}

	public void AfterOverlayOpened()
	{
		Modulate = Colors.Transparent;
		_fadeTween?.Kill();
		_fadeTween = CreateTween();
		_fadeTween.TweenProperty(this, "modulate:a", 1f, 0.4f);
	}

	public void AfterOverlayClosed()
	{
		_fadeTween?.Kill();
		this.QueueFreeSafely();
	}

	public void AfterOverlayShown()
	{
		Visible = true;
		_skipButton.Enable();
		if (_bundlePreviewContainer.Visible)
		{
			_previewCancelButton.Enable();
			_previewConfirmButton.Enable();
		}
	}

	public void AfterOverlayHidden()
	{
		Visible = false;
		_skipButton.Disable();
		_previewCancelButton.Disable();
		_previewConfirmButton.Disable();
	}
}