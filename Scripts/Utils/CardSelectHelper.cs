using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace FBE.Scripts.Utils;

internal static class CardSelectHelper
{
	public static async Task<IReadOnlyList<CardModel>?> FromChooseOptionalBundleScreen(
		Player player,
		IReadOnlyList<IReadOnlyList<CardModel>> bundles)
	{
		if (CombatManager.Instance.IsEnding || bundles.Count == 0)
			return null;

		var choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(player);
		int index;

		if (TestMode.IsOn)
		{
			index = 0;
		}
		else if (ShouldSelectLocally(player))
		{
			var screen = NChooseABundleSelectionScreen.ShowScreen(bundles);
			var wasSkipped = false;
			AddSkipButton(screen, () =>
			{
				wasSkipped = true;
				NOverlayStack.Instance?.Remove(screen);
			});

			try
			{
				var selectedBundle = (await screen.CardsSelected()).Single();
				index = Enumerable.Range(0, bundles.Count)
					.Single(i => ReferenceEquals(bundles[i], selectedBundle));
			}
			catch (TaskCanceledException) when (wasSkipped)
			{
				index = -1;
			}

			RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
				player,
				choiceId,
				PlayerChoiceResult.FromIndex(index));
		}
		else
		{
			index = (await RunManager.Instance.PlayerChoiceSynchronizer
				.WaitForRemoteChoice(player, choiceId))
				.AsIndex();
		}

		if (index == -1)
			return null;

		if (index < 0 || index >= bundles.Count)
			throw new InvalidOperationException("Received an invalid card bundle selection.");

		return bundles[index];
	}

	private static bool ShouldSelectLocally(Player player)
	{
		return LocalContext.IsMe(player) &&
		       RunManager.Instance.NetService.Type != NetGameType.Replay;
	}

	private static void AddSkipButton(NChooseABundleSelectionScreen screen, Action onSkipped)
	{
		var skipButton = SceneHelper.Instantiate<NBackButton>("/ui/back_button");
		screen.AddChildSafely(skipButton);
		skipButton.Connect(
			NClickableControl.SignalName.Released,
			Callable.From((Action<NButton>)(_ => ReturnFromBundlePreviewOrSkip(screen, onSkipped))));
		skipButton.Enable();
	}

	private static void ReturnFromBundlePreviewOrSkip(NChooseABundleSelectionScreen screen, Action onSkipped)
	{
		var previewContainer = screen.GetNode<Control>("%BundlePreviewContainer");
		if (previewContainer.Visible)
		{
			var previewCancelButton = screen.GetNode<NBackButton>("%Cancel");
			previewCancelButton.EmitSignal(
				NClickableControl.SignalName.Released,
				previewCancelButton);
			return;
		}

		onSkipped();
	}

}
