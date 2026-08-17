using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace FBE.Scripts.Visuals;

/// <summary>
/// 门的待机序列帧形象。节点命名与原版 NCreatureVisuals 的战斗 UI 约定保持一致。
/// </summary>
public partial class DoorMonsterVisuals : NCreatureVisuals
{
	public AnimatedSprite2D Sprite { get; } = new();

	private const string IdleAnimationName = "idle";
	private const string IdleTextureDirectory = "res://FBE/animations/DoorMonster/Idle";
	private const int IdleFrameCount = 76;
	private const double IdleFramesPerSecond = 10.0;

	private const float IdleFrameWidth = 720f;
	private const float IdleFrameHeight = 960f;
	// 序列帧非透明内容的 Alpha 边界底部（右下边界为排他值）。
	private static readonly Vector2 IdleContentTopLeft = new(118f, 14f);
	private static readonly Vector2 IdleContentSize = new(490f, 934f);
	private const float IdleContentBottom = 948f;

	// 调整此值时，Bounds、意图、气泡和特效中心会同步匹配缩放后的贴图。
	private const float SpriteScale = 0.50f;

	// 将帧的可见底部对齐怪物本地原点（血条的当前对齐基准）。
	private static readonly Vector2 SpriteBottomAlignmentOffset =
		new(0f, (IdleFrameHeight / 2f - IdleContentBottom) * SpriteScale);

	// 在底部对齐基础上的手动微调；X 正向右，Y 正向下。
	private static readonly Vector2 SpriteOffset = new(0.0f, 0.0f);
	// 碰撞范围在可见贴图四周额外保留的源像素边距。
	private const float BoundsPaddingInSourcePixels = 24f;
	private const float IntentGapInSourcePixels = 72f;
	private static readonly Vector2 TalkOffsetInSourcePixels = new(-52f, 8f);

	private static Vector2 SpritePosition => SpriteBottomAlignmentOffset + SpriteOffset;

	private static Rect2 VisibleSpriteBounds
	{
		get
		{
			var frameCenter = new Vector2(IdleFrameWidth / 2f, IdleFrameHeight / 2f);
			var position = SpritePosition + (IdleContentTopLeft - frameCenter) * SpriteScale;
			var padding = Vector2.One * BoundsPaddingInSourcePixels * SpriteScale;
			return new Rect2(position - padding, IdleContentSize * SpriteScale + padding * 2f);
		}
	}

	private static Vector2 VisibleSpriteCenter =>
		SpritePosition + (IdleContentTopLeft + IdleContentSize / 2f -
			new Vector2(IdleFrameWidth / 2f, IdleFrameHeight / 2f)) * SpriteScale;

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
		Sprite.Scale = Vector2.One * SpriteScale;
		Sprite.TextureFilter = TextureFilterEnum.Nearest;
		Sprite.SpriteFrames = BuildSpriteFrames();
		Sprite.Animation = IdleAnimationName;
		visuals.AddChild(Sprite);
		Sprite.Owner = this;

		var bounds = VisibleSpriteBounds;
		AddOwnedChild(new Control
		{
			Name = "Bounds",
			UniqueNameInOwner = true,
			Position = bounds.Position,
			Size = bounds.Size
		});

		AddOwnedChild(new Marker2D
		{
			Name = "IntentPos",
			UniqueNameInOwner = true,
			Position = new Vector2(
				bounds.GetCenter().X,
				bounds.Position.Y - IntentGapInSourcePixels * SpriteScale)
		});

		AddOwnedChild(new Marker2D
		{
			Name = "CenterPos",
			UniqueNameInOwner = true,
			Position = VisibleSpriteCenter
		});

		AddOwnedChild(new Marker2D
		{
			Name = "TalkPos",
			UniqueNameInOwner = true,
			Position = bounds.Position + TalkOffsetInSourcePixels * SpriteScale
		});
	}

	private static SpriteFrames BuildSpriteFrames()
	{
		var frames = new SpriteFrames();
		frames.ClearAll();
		frames.AddAnimation(IdleAnimationName);
		frames.SetAnimationLoop(IdleAnimationName, true);
		frames.SetAnimationSpeed(IdleAnimationName, IdleFramesPerSecond);

		for (var frameIndex = 0; frameIndex < IdleFrameCount; frameIndex++)
		{
			var texturePath = $"{IdleTextureDirectory}/DoorMonster_Idle_{frameIndex:D3}.png";
			var texture = GD.Load<Texture2D>(texturePath);
			if (texture == null)
			{
				GD.PushError($"DoorMonster idle frame not found: {texturePath}");
				continue;
			}

			frames.AddFrame(IdleAnimationName, texture);
		}

		return frames;
	}

	private void AddOwnedChild(Node node)
	{
		AddChild(node);
		node.Owner = this;
	}
}
