using Godot;
using System.Reflection;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace FBE.Scripts.Visuals;

/// <summary>
/// 古烈的站姿循环形象。贴图已按素材导出规范镜像为左朝向。
/// </summary>
public partial class GuileVisuals : NCreatureVisuals
{
	public AnimatedSprite2D Sprite { get; } = new();
	public const float DeathPlaybackSeconds = 30f / 60f;
	private Creature? _observedCreature;
	private bool _isDying;
	private bool _victoryPoseForced;
	private const string DeathAnimationName = "death";
	private const string DeathTextureDirectory = "res://FBE/animations/Guile/Death";
	// Action5050：倒下过渡8 tick，倒地帧保留22 tick后交给原版消逝。
	private static readonly (int FrameIndex, float DurationTicks)[] DeathFrames = [(0, 8f), (1, 22f)];

	private const string IdleAnimationName = "idle";
	private const string SomersaultAnimationName = "somersault";
	private const string StandingHitAnimationName = "standing_hit";
	private const string StandingGuardAnimationName = "standing_guard";
	private const string StandingGuardTextureDirectory = "res://FBE/animations/Guile/StandingGuard";
	private const string CrouchIdleAnimationName = "crouch_idle";
	private const string CrouchGuardAnimationName = "crouch_guard";
	private const string CrouchHitAnimationName = "crouch_hit";
	private const string SonicCastAnimationName = "sonic_cast";
	private const string IdleTextureDirectory = "res://FBE/animations/Guile/Idle";
	private const string SomersaultTextureDirectory = "res://FBE/animations/Guile/Somersault";
	private const string StandingHitTextureDirectory = "res://FBE/animations/Guile/StandingHit";
	private const string CrouchIdleTextureDirectory = "res://FBE/animations/Guile/CrouchIdle";
	private const string CrouchGuardTextureDirectory = "res://FBE/animations/Guile/CrouchGuard";
	private const string CrouchHitTextureDirectory = "res://FBE/animations/Guile/CrouchHit";
	private const string SonicCastTextureDirectory = "res://FBE/animations/Guile/SonicCast";
	// 为消除原始各帧的尺寸和轴点差异，导出时已统一到此画布和脚底轴点。
	private const float IdleTextureWidth = 80f;
	private const float IdleTextureHeight = 96f;
	private const float IdleAxisX = 48f;
	private const float IdleAxisY = 91f;
	// 古烈本体全部动画共用的硬编码缩放倍率；后续新增动画不要再单独设置 Sprite.Scale。
	private const float OverallTextureScale = 3.6f;
	private const float IntentGap = 48f;
	// 受击框从可见本体边界向外延伸 10%，使宽高各增加 20%。
	private const float HitboxOutsetRatio = 0.1f;
	private const float CrouchHitboxHeightMultiplier = 0.75f;
	// 两个支持版本均通过此原版方法同步卡牌目标碰撞框、选择外框和血条布局。
	private static readonly MethodInfo RefreshCreatureBoundsMethod =
		typeof(NCreature).GetMethod("UpdateBounds", BindingFlags.Instance | BindingFlags.NonPublic,
			null, [typeof(Node)], null)
		?? throw new MissingMethodException(typeof(NCreature).FullName, "UpdateBounds(Node)");
	private bool _isPlayingSomersault;
	private double _somersaultElapsedSeconds;
	// Action 0: (0,0)x3 → (0,1)x4 → (0,2)x4 → (0,3)x4 → (0,4)x4 → (0,0)x1。
	private static readonly (int FrameIndex, float DurationTicks)[] IdleFrames =
	[
		(0, 3f),
		(1, 4f),
		(2, 4f),
		(3, 4f),
		(4, 4f),
		(0, 1f)
	];
	// Action 1100 的 21 tick 攻击段，接原始落地帧 (240,5) 停留 21 tick 后回站姿。
	private static readonly (int FrameIndex, float DurationTicks)[] SomersaultFrames =
	[
		(0, 1f),
		(1, 2f),
		(2, 2f),
		(3, 4f),
		(4, 2f),
		(5, 2f),
		(6, 4f),
		(7, 2f),
		(8, 2f),
		(9, 21f)
	];
	// 站姿受击、蹲姿待机、格挡与受击均使用单帧，并由状态机决定恢复到哪一种待机姿势。
	private static readonly (int FrameIndex, float DurationTicks)[] StandingHitFrames = [(0, 14f)];
	private static readonly (int FrameIndex, float DurationTicks)[] StandingGuardFrames = [(0, 14f)];
	private static readonly (int FrameIndex, float DurationTicks)[] CrouchIdleFrames = [(0, 1f)];
	private static readonly (int FrameIndex, float DurationTicks)[] CrouchGuardFrames = [(0, 14f)];
	private static readonly (int FrameIndex, float DurationTicks)[] CrouchHitFrames = [(0, 14f)];
	// Action 1000: (10,0)x2 → (1000,0)x3 → (1000,1)x3 → (1000,2)x3 → (10,0)x19。
	private static readonly (int FrameIndex, float DurationTicks)[] SonicCastFrames =
	[
		(0, 2f),
		(1, 3f),
		(2, 3f),
		(3, 3f),
		(0, 19f)
	];

	private static Vector2 SpritePosition =>
		new Vector2(IdleTextureWidth / 2f - IdleAxisX, IdleTextureHeight / 2f - IdleAxisY) * OverallTextureScale;

	private static Rect2 SpriteBounds => new(
		SpritePosition - new Vector2(IdleTextureWidth, IdleTextureHeight) * OverallTextureScale / 2f,
		new Vector2(IdleTextureWidth, IdleTextureHeight) * OverallTextureScale);

	private static Rect2 HitboxBounds => ExpandBounds(SpriteBounds);

	public override void _Ready()
	{
		EnsureCreatureVisualNodes();
		base._Ready();
		Sprite.AnimationChanged += UpdatePoseHitbox;
		PlayLoop(IdleAnimationName);
		if (GetParent() is NCreature creatureNode)
		{
			_observedCreature = creatureNode.Entity;
			_observedCreature.Died += OnCreatureDied;
			if (_observedCreature.IsDead)
				OnCreatureDied(_observedCreature);
		}
	}

	public override void _ExitTree()
	{
		if (_observedCreature != null)
			_observedCreature.Died -= OnCreatureDied;
		_observedCreature = null;
		base._ExitTree();
	}

	private void OnCreatureDied(Creature creature)
	{
		if (_isDying)
			return;
		_isDying = true;
		Sprite.Position = SpritePosition;
		Sprite.Stop();
		Sprite.Play(DeathAnimationName);
	}

	private void UpdatePoseHitbox()
	{
		// 阻止受击动作先前排队的待机或其他触发器覆盖死亡终态。
		if (_isDying && Sprite.Animation != DeathAnimationName)
		{
			Sprite.Play(DeathAnimationName);
			return;
		}
		// 音速手刀属于当前蹲姿动作，受击和格挡也保留蹲姿范围。
		var crouching = Sprite.Animation == CrouchIdleAnimationName ||
			Sprite.Animation == CrouchHitAnimationName ||
			Sprite.Animation == CrouchGuardAnimationName ||
			Sprite.Animation == SonicCastAnimationName;
		// 倒地身体比站姿更宽；消逝特效使用碰撞框截图，需要包含完整尸体。
		var fullBounds = _isDying
			? ExpandBounds(new Rect2(SpritePosition - new Vector2(160f, 96f) * OverallTextureScale / 2f,
				new Vector2(160f, 96f) * OverallTextureScale))
			: HitboxBounds;
		var height = fullBounds.Size.Y * (crouching ? CrouchHitboxHeightMultiplier : 1f);
		var size = new Vector2(fullBounds.Size.X, height);
		// Godot 的 Position 是左上角；顶部下移减少的高度，左下角才能保持不动。
		var position = fullBounds.Position + Vector2.Down * (fullBounds.Size.Y - height);
		if (Bounds.Size == size && Bounds.Position == position)
			return;
		Bounds.Size = size;
		Bounds.Position = position;

		// 初次入树时父节点的 _Ready 会读取 Bounds；后续姿势变化才需请求刷新。
		if (GetParent() is NCreature creature && creature.IsNodeReady())
			RefreshCreatureBoundsMethod.Invoke(creature, [this]);
	}

	/// <summary>
	/// 直接播放一个非循环动作，并在其结束后恢复指定待机姿势。
	/// 这条路径由古烈的战斗逻辑直接调用，不依赖战斗触发器的转发链。
	/// </summary>
	public async Task PlayOneShotAndReturnAsync(string animationName, string returnAnimationName)
	{
		if (_isDying || _victoryPoseForced)
			return;
		if (Sprite.SpriteFrames == null || !Sprite.SpriteFrames.HasAnimation(animationName))
		{
			GD.PushError($"[FBE][Guile] Missing animation '{animationName}'.");
			return;
		}

		Sprite.Stop();
		Sprite.Animation = animationName;
		Sprite.Frame = 0;
		Sprite.FrameProgress = 0f;
		Sprite.Play(animationName);
		// 命中后的反伤可能中断动作或移除古烈，不能只等待 AnimationFinished。
		var completion = new TaskCompletionSource<bool>();
		void OnFinished() => completion.TrySetResult(true);
		void OnChanged()
		{
			if (Sprite.Animation != animationName)
				completion.TrySetResult(false);
		}
		void OnExiting() => completion.TrySetResult(false);
		Sprite.AnimationFinished += OnFinished;
		Sprite.AnimationChanged += OnChanged;
		TreeExiting += OnExiting;
		bool finished;
		try
		{
			finished = await completion.Task;
		}
		finally
		{
			if (GodotObject.IsInstanceValid(Sprite))
			{
				Sprite.AnimationFinished -= OnFinished;
				Sprite.AnimationChanged -= OnChanged;
			}
			if (GodotObject.IsInstanceValid(this))
				TreeExiting -= OnExiting;
		}

		// 只有该动作自然结束时才复位，避免覆盖中途开始的其他动作。
		if (!_victoryPoseForced && finished && GodotObject.IsInstanceValid(Sprite) && Sprite.Animation == animationName)
			PlayLoop(returnAnimationName);
	}

	/// <summary>玩家战败后立即切至站姿，并阻止未完成动作恢复到原姿势。</summary>
	public void ForceStandingVictoryPose()
	{
		if (_isDying)
			return;

		_victoryPoseForced = true;
		Sprite.Position = SpritePosition;
		PlayLoop(IdleAnimationName);
	}

	/// <summary>切换到指定循环待机动作并从第一帧开始播放。</summary>
	public void PlayLoop(string animationName)
	{
		if (_isDying)
			return;
		if (Sprite.SpriteFrames == null || !Sprite.SpriteFrames.HasAnimation(animationName))
		{
			GD.PushError($"[FBE][Guile] Missing animation '{animationName}'.");
			return;
		}

		Sprite.Stop();
		Sprite.Animation = animationName;
		Sprite.Frame = 0;
		Sprite.FrameProgress = 0f;
		Sprite.Play(animationName);
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		// 防止状态机或已排队动作在胜利结算后重新覆盖站姿。
		if (_victoryPoseForced && Sprite.Animation != IdleAnimationName)
		{
			Sprite.Position = SpritePosition;
			PlayLoop(IdleAnimationName);
			return;
		}

		if (Sprite.Animation != SomersaultAnimationName)
		{
			_isPlayingSomersault = false;
			_somersaultElapsedSeconds = 0.0;
			Sprite.Position = SpritePosition;
			return;
		}

		if (!_isPlayingSomersault)
		{
			_isPlayingSomersault = true;
			_somersaultElapsedSeconds = 0.0;
		}

		_somersaultElapsedSeconds += delta;
		// Action 1100: posset 0,-6，随后只施加 Y 方向的 0.3 加速度；X 永远保持不动。
		Sprite.Position = SpritePosition + Vector2.Down * CalculateSomersaultVerticalOffset(_somersaultElapsedSeconds);
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
		Sprite.Scale = Vector2.One * OverallTextureScale;
		Sprite.TextureFilter = TextureFilterEnum.Nearest;
		Sprite.SpriteFrames = BuildSpriteFrames();
		Sprite.Animation = IdleAnimationName;
		visuals.AddChild(Sprite);
		Sprite.Owner = this;

		AddOwnedChild(new Control
		{
			Name = "Bounds",
			UniqueNameInOwner = true,
			Position = HitboxBounds.Position,
			Size = HitboxBounds.Size
		});

		AddOwnedChild(new Marker2D
		{
			Name = "IntentPos",
			UniqueNameInOwner = true,
			Position = new Vector2(SpriteBounds.GetCenter().X, SpriteBounds.Position.Y - IntentGap)
		});

		AddOwnedChild(new Marker2D
		{
			Name = "CenterPos",
			UniqueNameInOwner = true,
			Position = SpriteBounds.GetCenter()
		});

		AddOwnedChild(new Marker2D
		{
			Name = "TalkPos",
			UniqueNameInOwner = true,
			Position = SpriteBounds.Position + new Vector2(24f, 32f)
		});
	}

	private static SpriteFrames BuildSpriteFrames()
	{
		var frames = new SpriteFrames();
		frames.ClearAll();
		frames.AddAnimation(IdleAnimationName);
		frames.SetAnimationLoop(IdleAnimationName, true);
		frames.SetAnimationSpeed(IdleAnimationName, 60.0);

		foreach (var (frameIndex, durationTicks) in IdleFrames)
		{
			var texturePath = $"{IdleTextureDirectory}/Guile_Idle_{frameIndex:D3}.png";
			var texture = GD.Load<Texture2D>(texturePath);
			if (texture == null)
			{
				GD.PushError($"Guile idle frame not found: {texturePath}");
				continue;
			}

			frames.AddFrame(IdleAnimationName, texture, durationTicks);
		}

		frames.AddAnimation(SomersaultAnimationName);
		frames.SetAnimationLoop(SomersaultAnimationName, false);
		frames.SetAnimationSpeed(SomersaultAnimationName, 60.0);

		foreach (var (frameIndex, durationTicks) in SomersaultFrames)
		{
			var texturePath = $"{SomersaultTextureDirectory}/Guile_Somersault_{frameIndex:D3}.png";
			var texture = GD.Load<Texture2D>(texturePath);
			if (texture == null)
			{
				GD.PushError($"Guile Somersault frame not found: {texturePath}");
				continue;
			}

			frames.AddFrame(SomersaultAnimationName, texture, durationTicks);
		}

		AddAnimationFrames(frames, StandingHitAnimationName, StandingHitTextureDirectory, StandingHitFrames, loop: false);
		AddAnimationFrames(frames, StandingGuardAnimationName, StandingGuardTextureDirectory, StandingGuardFrames, loop: false);
		AddAnimationFrames(frames, DeathAnimationName, DeathTextureDirectory, DeathFrames, loop: false);
		AddAnimationFrames(frames, CrouchIdleAnimationName, CrouchIdleTextureDirectory, CrouchIdleFrames, loop: true);
		AddAnimationFrames(frames, CrouchGuardAnimationName, CrouchGuardTextureDirectory, CrouchGuardFrames, loop: false);
		AddAnimationFrames(frames, CrouchHitAnimationName, CrouchHitTextureDirectory, CrouchHitFrames, loop: false);
		AddAnimationFrames(frames, SonicCastAnimationName, SonicCastTextureDirectory, SonicCastFrames, loop: false);

		return frames;
	}

	private static void AddAnimationFrames(
		SpriteFrames frames,
		string animationName,
		string textureDirectory,
		IEnumerable<(int FrameIndex, float DurationTicks)> frameDefinitions,
		bool loop)
	{
		frames.AddAnimation(animationName);
		frames.SetAnimationLoop(animationName, loop);
		frames.SetAnimationSpeed(animationName, 60.0);

		foreach (var (frameIndex, durationTicks) in frameDefinitions)
		{
			var texturePath = $"{textureDirectory}/Guile_{ToPascalCase(animationName)}_{frameIndex:D3}.png";
			var texture = GD.Load<Texture2D>(texturePath);
			if (texture == null)
			{
				GD.PushError($"Guile animation frame not found: {texturePath}");
				continue;
			}

			frames.AddFrame(animationName, texture, durationTicks);
		}
	}

	private static string ToPascalCase(string animationName)
	{
		return string.Concat(animationName.Split('_').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
	}

	private static Rect2 ExpandBounds(Rect2 bounds)
	{
		var outset = bounds.Size * HitboxOutsetRatio;
		return new Rect2(bounds.Position - outset, bounds.Size + outset * 2f);
	}

	private static float CalculateSomersaultVerticalOffset(double elapsedSeconds)
	{
		const double ticksPerSecond = 60.0;
		const double finalTick = 41.0;
		var elapsedTicks = Math.Min(elapsedSeconds * ticksPerSecond, finalTick);
		var sourcePixels = -6.0 * elapsedTicks + 0.15 * elapsedTicks * (elapsedTicks - 1.0);
		return (float)Math.Min(0.0, sourcePixels) * OverallTextureScale;
	}

	private void AddOwnedChild(Node node)
	{
		AddChild(node);
		node.Owner = this;
	}
}
