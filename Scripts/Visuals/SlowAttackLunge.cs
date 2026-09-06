using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace FBE.Scripts.Visuals;

/// <summary>
/// 可复用的慢攻击横向突进。
/// 依次执行小后撤、前冲和回位，并在前冲顶点结算攻击行为。
/// </summary>
public static class SlowAttackLunge
{
	public const float DefaultRetreatDistance = 18f;
	public const float DefaultLungeDistance = 60f;

	private const float RetreatControlMultiplier = 1.7f;
	private const float NormalApproachDuration = 0.5f;
	private const float NormalReturnDuration = 0.5f;

	/// <summary>
	/// 对视觉根节点播放一次慢攻击位移，并在前冲顶点结算传入的攻击行为。
	/// </summary>
	public static async Task PlayAsync(Node2D visualRoot, Vector2 retreatOffset, Vector2 lungeOffset,
		Func<Task> onImpact)
	{
		ArgumentNullException.ThrowIfNull(visualRoot);
		ArgumentNullException.ThrowIfNull(onImpact);

		if (!GodotObject.IsInstanceValid(visualRoot))
		{
			await onImpact();
			return;
		}

		var fastMode = SaveManager.Instance.PrefsSave.FastMode;
		if (fastMode == FastModeType.Instant)
		{
			await onImpact();
			return;
		}

		var startPosition = visualRoot.Position;
		var tween = visualRoot.CreateTween();
		tween.TweenMethod(
			Callable.From(delegate(float progress)
			{
				if (GodotObject.IsInstanceValid(visualRoot))
				{
					// 两个控制点都位于后撤方向，使后撤自然衔接为前冲而非在端点停顿。
					var retreatControl = retreatOffset * RetreatControlMultiplier;
					visualRoot.Position = startPosition + CubicBezier(
						Vector2.Zero,
						retreatControl,
						retreatControl,
						lungeOffset,
						progress);
				}
			}),
			0f,
			1f,
			GetScaledDuration(fastMode, NormalApproachDuration));
		tween.TweenMethod(
			Callable.From(delegate(float progress)
			{
				if (GodotObject.IsInstanceValid(visualRoot))
					visualRoot.Position = startPosition + lungeOffset * (1f - SmoothStep(progress));
			}),
			0f,
			1f,
			GetScaledDuration(fastMode, NormalReturnDuration));

		try
		{
			await WaitForDuration(fastMode, NormalApproachDuration);
			await onImpact();
			await WaitForDuration(fastMode, NormalReturnDuration);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(tween))
				tween.Kill();
			if (GodotObject.IsInstanceValid(visualRoot))
				visualRoot.Position = startPosition;
		}
	}

	private static Task WaitForDuration(FastModeType fastMode, float normalDuration)
	{
		// None 不会出现在正常战斗设置中；按标准速度处理可避免 CustomScaledWait 抛出异常。
		return fastMode == FastModeType.None
			? Cmd.Wait(normalDuration)
			: Cmd.CustomScaledWait(
				normalDuration * 0.5f,
				normalDuration);
	}

	private static float GetScaledDuration(FastModeType fastMode, float normalDuration)
	{
		return fastMode == FastModeType.Fast ? normalDuration * 0.5f : normalDuration;
	}

	private static Vector2 CubicBezier(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end,
		float progress)
	{
		progress = Mathf.Clamp(progress, 0f, 1f);
		var inverse = 1f - progress;
		return inverse * inverse * inverse * start +
			3f * inverse * inverse * progress * control1 +
			3f * inverse * progress * progress * control2 +
			progress * progress * progress * end;
	}

	private static float SmoothStep(float progress)
	{
		progress = Mathf.Clamp(progress, 0f, 1f);
		return progress * progress * (3f - 2f * progress);
	}
}
