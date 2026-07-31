using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace FBE.Scripts.Visuals;

public partial class DarkBumVisuals : NCreatureVisuals
{
	public AnimatedSprite2D Sprite { get; } = new();

	private const string IdleAnimationName = "idle";

	// [新增字段] DarkBum 遗物效果触发时播放的一次性动画名。
	private const string RelicTriggerAnimationName = "relic_trigger";

	// 静态贴图路径
	private const string IdleTexturePath = "res://FBE/animations/DarkBum/Idle.png";

	// [新增字段] DarkBum 遗物触发动画序列帧所在目录。
	// 默认读取：
	// res://FBE/animations/DarkBum/RelicTrigger/RelicTrigger_000.png
	// res://FBE/animations/DarkBum/RelicTrigger/RelicTrigger_001.png
	// ...
	private const string RelicTriggerFrameDirectory = "res://FBE/animations/DarkBum/Spawn";

	// [新增字段] DarkBum 遗物触发动画序列帧文件名前缀。
	private const string RelicTriggerFramePrefix = "Spawn_";

	// [新增字段] DarkBum 遗物触发动画序列帧数量。
	private const int RelicTriggerFrameCount = 14;

	// [新增字段] DarkBum 遗物触发动画播放速度。
	private const double RelicTriggerAnimationSpeed = 30.0;

	// 贴图缩放。像素风建议用整数：1、2、3...
	private const float SpriteScale = 4.0f;

	// 贴图基础偏移。x 正数向右，y 正数向下。
	private static readonly Vector2 BaseSpritePosition = new(65.0f, -65.0f);

	// idle 浮动幅度，单位：像素
	private const float BobAmplitude = 5.0f;

	// idle 浮动速度
	private const float BobSpeed = 3.0f;

	private double _time;

	public override void _Ready()
	{
		EnsureCreatureVisualNodes();

		// 必须在节点结构创建之后再调用 base._Ready()
		base._Ready();

		// [新增逻辑] 监听一次性动画播放完成。
		Sprite.AnimationFinished += OnSpriteAnimationFinished;

		Sprite.Play(IdleAnimationName);
	}

	public override void _ExitTree()
	{
		// [新增逻辑] 退出节点树时取消事件订阅，避免节点重建后重复回调。
		Sprite.AnimationFinished -= OnSpriteAnimationFinished;

		base._ExitTree();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		_time += delta;

		if (_time >= 2 * double.Pi)
		{
			_time -= 2 * double.Pi;
		}

		// 只在 idle 状态上下浮动。
		// 后面你加 attack / hit / die 等动画时，它们不会被这个浮动影响。
		// [新增说明] relic_trigger 是 DarkBum 的特殊 idle 派生视觉状态，所以也使用同一套浮动。
		if (ShouldApplyBob())
		{
			var bobOffset = new Vector2(
				0f,
				(float)Math.Sin(_time * BobSpeed) * BobAmplitude
			);

			Sprite.Position = BaseSpritePosition + bobOffset;
		}
		else
		{
			Sprite.Position = BaseSpritePosition;
		}
	}

	// [新增方法] DarkBum 遗物效果触发时调用。
	// 这个方法会从第 0 帧开始播放 relic_trigger。
	// 播放结束后会由 OnSpriteAnimationFinished 自动恢复 idle。
	public void PlayRelicTrigger()
	{
		if (Sprite.SpriteFrames == null)
		{
			GD.PushError("DarkBum SpriteFrames is null.");
			return;
		}

		if (!Sprite.SpriteFrames.HasAnimation(RelicTriggerAnimationName))
		{
			GD.PushError($"DarkBum animation not found: {RelicTriggerAnimationName}");
			return;
		}

		if (Sprite.SpriteFrames.GetFrameCount(RelicTriggerAnimationName) <= 0)
		{
			GD.PushError($"DarkBum animation has no frames: {RelicTriggerAnimationName}");
			return;
		}

		Sprite.Stop();
		Sprite.Animation = RelicTriggerAnimationName;
		Sprite.SetFrameAndProgress(0, 0.0f);
		Sprite.Play(RelicTriggerAnimationName);
	}

	// [新增方法] 判断当前动画是否应该参与正弦悬浮。
	private bool ShouldApplyBob()
	{
		return Sprite.Animation == IdleAnimationName
		       || Sprite.Animation == RelicTriggerAnimationName;
	}

	// [新增方法] relic_trigger 播放完成后自动恢复 idle。
	private void OnSpriteAnimationFinished()
	{
		if (Sprite.Animation != RelicTriggerAnimationName)
		{
			return;
		}

		Sprite.Animation = IdleAnimationName;
		Sprite.SetFrameAndProgress(0, 0.0f);
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
		Sprite.Position = BaseSpritePosition;
		Sprite.Scale = new Vector2(SpriteScale, SpriteScale);
		Sprite.TextureFilter = TextureFilterEnum.Nearest;
		Sprite.SpriteFrames = BuildSpriteFrames();
		Sprite.Animation = IdleAnimationName;

		visuals.AddChild(Sprite);
		Sprite.Owner = this;

		AddOwnedChild(new Control
		{
			Name = "Bounds",
			UniqueNameInOwner = true,
			Position = Sprite.Position,
			Size = new Vector2(128f, 128f)
		});

		AddOwnedChild(new Marker2D
		{
			Name = "IntentPos",
			UniqueNameInOwner = true,
			Position = new Vector2(0f, -88f)
		});

		AddOwnedChild(new Marker2D
		{
			Name = "CenterPos",
			UniqueNameInOwner = true,
			Position = BaseSpritePosition
		});
	}

	private void AddOwnedChild(Node node)
	{
		AddChild(node);
		node.Owner = this;
	}

	private static SpriteFrames BuildSpriteFrames()
	{
		var frames = new SpriteFrames();
		frames.ClearAll();

		// idle 是单帧动画：静态图 + 代码控制上下浮动
		frames.AddAnimation(IdleAnimationName);
		frames.SetAnimationLoop(IdleAnimationName, true);
		frames.SetAnimationSpeed(IdleAnimationName, 1.0);

		var idleTexture = GD.Load<Texture2D>(IdleTexturePath);

		if (idleTexture == null)
		{
			GD.PushError($"DarkBum idle texture not found: {IdleTexturePath}");
		}
		else
		{
			frames.AddFrame(IdleAnimationName, idleTexture);
		}

		// [新增逻辑] 添加 DarkBum 遗物触发时播放的一次性序列帧动画。
		frames.AddAnimation(RelicTriggerAnimationName);
		frames.SetAnimationLoop(RelicTriggerAnimationName, false);
		frames.SetAnimationSpeed(RelicTriggerAnimationName, RelicTriggerAnimationSpeed);

		for (var i = 0; i < RelicTriggerFrameCount; i++)
		{
			var path = $"{RelicTriggerFrameDirectory}/{RelicTriggerFramePrefix}{i:000}.png";
			var texture = GD.Load<Texture2D>(path);

			if (texture == null)
			{
				GD.PushError($"DarkBum relic trigger frame not found: {path}");
				continue;
			}

			frames.AddFrame(RelicTriggerAnimationName, texture);
		}

		return frames;
	}
}
