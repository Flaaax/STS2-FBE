using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace FBE.Scripts.Visuals;

/// <summary>
/// 古烈音速波动的循环形象。出场和攻击仅移动精灵，保留待命点的碰撞框与布局。
/// </summary>
public partial class GuileSonicBoomVisuals : NCreatureVisuals
{
	public AnimatedSprite2D Sprite { get; } = new();
	// 标准速度下出场耗时；攻击复用出场距离算出的线速度。
	private const float SpawnTravelSeconds = 0.3f;
	private float _travelSpeed = 800f;

	public async Task PlaySpawnTravelAsync(NCreature summoner)
	{
		var end = Sprite.Position;
		var center = VisibleContentBounds.GetCenter();
		// 以可见波动的中心对齐古烈稍左侧，避免透明画布轴点造成出场位置偏差。
		var origin = ToLocal(summoner.Visuals.VfxSpawnPosition.GlobalPosition);
		origin.X -= 60f;
		origin.Y = center.Y;
		var start = end + origin - center;
		_travelSpeed = Math.Max(start.DistanceTo(end) / SpawnTravelSeconds, 1f);
		Sprite.Position = start;
		await MoveSpriteAsync(end, SpawnTravelSeconds);
	}

	public Task PlayImpactTravelAsync(NCreature target)
	{
		var targetRect = target.Hitbox.GetGlobalRect();
		var contact = ToLocal(new Vector2(targetRect.End.X, targetRect.GetCenter().Y));
		// 左侧前缘触及玩家右侧边界时命中，而不是让透明画布中心穿进玩家。
		contact.X += VisibleContentBounds.Size.X * 0.5f;
		var end = SpritePosition + contact - VisibleContentBounds.GetCenter();
		return MoveSpriteAsync(end, Sprite.Position.DistanceTo(end) / _travelSpeed);
	}

	private async Task MoveSpriteAsync(Vector2 end, float seconds)
	{
		var mode = SaveManager.Instance.PrefsSave.FastMode;
		if (mode == FastModeType.Instant || seconds <= 0f)
		{
			Sprite.Position = end;
			return;
		}

		var tween = Sprite.CreateTween();
		tween.TweenProperty(Sprite, "position", end,
			mode == FastModeType.Fast ? seconds * 0.5f : seconds).SetTrans(Tween.TransitionType.Linear);
		try
		{
			// 使用游戏等待接口，避免节点退出后等待永远不再发出的 Tween 信号。
			if (mode == FastModeType.None)
				await Cmd.Wait(seconds);
			else
				await Cmd.CustomScaledWait(seconds * 0.5f, seconds);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(tween))
				tween.Kill();
			if (GodotObject.IsInstanceValid(Sprite))
				Sprite.Position = end;
		}
	}

	private const string IdleAnimationName = "idle";
	private const string IdleTextureDirectory = "res://FBE/animations/Guile/SonicBoom";
	private const float TextureWidth = 128f;
	private const float TextureHeight = 80f;
	private const float AnchorX = 112f;
	private const float AnchorY = 80f;
	private const float TextureScale = 3f;
	private const float IntentGap = 36f;
	// 受击框从可见本体边界向外延伸 10%，使宽高各增加 20%。
	private const float HitboxOutsetRatio = 0.1f;
	// Action 1001 原始帧率为 60 FPS；测试版将波动循环整体放慢 20%。
	private const double IdleFramesPerSecond = 30.0;
	private static readonly Rect2 VisibleContentBounds = new(
		new Vector2(-111f, -60f) * TextureScale,
		new Vector2(81f, 25f) * TextureScale);
	private static Rect2 HitboxBounds => ExpandBounds(VisibleContentBounds);
	// 卡牌目标碰撞框纵向放大到原来的 2.25 倍（90 → 202.5），中心和宽度保持不变。
	private static Rect2 SelectionBounds => ExpandVerticallyFromCenter(HitboxBounds, 2.25f);

	private static Vector2 SpritePosition =>
		new Vector2(TextureWidth / 2f - AnchorX, TextureHeight / 2f - AnchorY) * TextureScale;

	public override void _Ready()
	{
		EnsureCreatureVisualNodes();
		base._Ready();
		Sprite.Play(IdleAnimationName);
	}

	private void EnsureCreatureVisualNodes()
	{
		var visuals = new Node2D
		{
			Name = "Visuals",
			UniqueNameInOwner = true,
			TextureFilter = TextureFilterEnum.Nearest
		};
		AddOwnedChild(visuals);

		Sprite.Name = "AnimatedSprite2D";
		Sprite.Centered = true;
		Sprite.Position = SpritePosition;
		Sprite.Scale = Vector2.One * TextureScale;
		Sprite.TextureFilter = TextureFilterEnum.Nearest;
		Sprite.SpriteFrames = BuildSpriteFrames();
		Sprite.Animation = IdleAnimationName;
		visuals.AddChild(Sprite);
		Sprite.Owner = this;

		AddOwnedChild(new Control
		{
			Name = "Bounds",
			UniqueNameInOwner = true,
			Position = SelectionBounds.Position,
			Size = SelectionBounds.Size
		});

		AddOwnedChild(new Marker2D
		{
			Name = "IntentPos",
			UniqueNameInOwner = true,
			Position = new Vector2(
				VisibleContentBounds.GetCenter().X,
				VisibleContentBounds.Position.Y - IntentGap)
		});

		AddOwnedChild(new Marker2D
		{
			Name = "CenterPos",
			UniqueNameInOwner = true,
			Position = VisibleContentBounds.GetCenter()
		});
	}

	private static SpriteFrames BuildSpriteFrames()
	{
		var frames = new SpriteFrames();
		frames.ClearAll();
		frames.AddAnimation(IdleAnimationName);
		frames.SetAnimationLoop(IdleAnimationName, true);
		frames.SetAnimationSpeed(IdleAnimationName, IdleFramesPerSecond);

		// 跳过 Action 1001 的空白占位帧，避免循环时闪烁。
		for (var frameIndex = 1; frameIndex < 5; frameIndex++)
		{
			var texturePath = $"{IdleTextureDirectory}/Guile_SonicBoom_{frameIndex:D3}.png";
			var texture = GD.Load<Texture2D>(texturePath);
			if (texture == null)
			{
				GD.PushError($"Guile Sonic Boom frame not found: {texturePath}");
				continue;
			}

			frames.AddFrame(IdleAnimationName, texture, 1.0f);
		}

		return frames;
	}

	private static Rect2 ExpandBounds(Rect2 bounds)
	{
		var outset = bounds.Size * HitboxOutsetRatio;
		return new Rect2(bounds.Position - outset, bounds.Size + outset * 2f);
	}

	private static Rect2 ExpandVerticallyFromCenter(Rect2 bounds, float multiplier)
	{
		var height = bounds.Size.Y * multiplier;
		return new Rect2(
			bounds.Position - Vector2.Down * ((height - bounds.Size.Y) / 2f),
			new Vector2(bounds.Size.X, height));
	}

	private void AddOwnedChild(Node node)
	{
		AddChild(node);
		node.Owner = this;
	}
}
