using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using FileAccess = Godot.FileAccess;

namespace FBE.Scripts.Visuals;

/// <summary>
/// 播放由 ANM2 离线转换器生成的透明序列帧与 timeline.json。
/// 运行时不读取原始 ANM2；所有插值、裁切和图层摆放均已在转换阶段解析。
/// </summary>
public abstract partial class BakedAnmVisuals : NCreatureVisuals
{
	private const float IntentGap = 40f;
	private BakedAnmTimeline? _timeline;
	private readonly List<Sprite2D> _layerSprites = [];
	private readonly List<Texture2D[]> _layerFrames = [];
	private readonly List<ShaderMaterial> _layerMaterials = [];
	private readonly Dictionary<string, (BakedAnmTimeline Timeline, List<Texture2D[]> Frames)> _timelineCache = [];
	private double _elapsed;
	private int _frameIndex;
	private bool _isPlayingOneShot;
	private TaskCompletionSource<bool>? _oneShotCompletion;

	protected abstract string TimelinePath { get; }
	protected virtual float VisualScale => 1f;
	/// <summary>只偏移动画贴图，不影响遭遇站位、碰撞范围或战斗 UI。</summary>
	protected virtual Vector2 SpriteOffset => Vector2.Zero;
	/// <summary>意图锚点的额外偏移，使用战斗场景坐标。</summary>
	protected virtual Vector2 IntentOffset => Vector2.Zero;
	protected IReadOnlyList<ShaderMaterial> LayerMaterials => _layerMaterials;

	public override void _Ready()
	{
		LoadAndCreateVisuals();
		base._Ready();
	}

	public override void _ExitTree()
	{
		// 战斗结束或怪物被移除时，不能让等待单次动作的战斗命令永久悬挂。
		_oneShotCompletion?.TrySetResult(false);
		_oneShotCompletion = null;
		base._ExitTree();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (_timeline == null)
			return;

		_elapsed += delta;
		var frameDuration = 1.0 / _timeline.Fps;
		while (_elapsed >= frameDuration)
		{
			_elapsed -= frameDuration;
			if (_isPlayingOneShot && _frameIndex >= _timeline.FrameCount - 1)
			{
				RestoreIdleTimeline();
				_oneShotCompletion?.TrySetResult(true);
				_oneShotCompletion = null;
				break;
			}

			_frameIndex = (_frameIndex + 1) % _timeline.FrameCount;
			ApplyFrame(_frameIndex);
		}

		UpdateLayerEffects(delta);
	}

	/// <summary>为每个 ANM 图层创建独立材质；需要运行时特效的子类可重写。</summary>
	protected virtual ShaderMaterial? CreateLayerMaterial(int layerIndex)
	{
		return null;
	}

	/// <summary>在每帧更新由子类管理的运行时材质参数。</summary>
	protected virtual void UpdateLayerEffects(double delta)
	{
	}

	/// <summary>播放同图层结构的单次时间线，结束后自动回到默认 Idle。</summary>
	protected Task PlayOneShotAndReturnAsync(string timelinePath)
	{
		if (!TryActivateTimeline(timelinePath))
			return Task.CompletedTask;

		_oneShotCompletion?.TrySetResult(false);
		_isPlayingOneShot = true;
		_oneShotCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		return _oneShotCompletion.Task;
	}

	private void LoadAndCreateVisuals()
	{
		if (!TryLoadTimeline(TimelinePath, out var timeline, out var frames))
			return;
		_timeline = timeline;
		_layerFrames.AddRange(frames);

		var visualRoot = new Node2D
		{
			Name = "Visuals",
			UniqueNameInOwner = true,
			TextureFilter = TextureFilterEnum.Nearest,
			Scale = Vector2.One * VisualScale
		};
		AddOwnedChild(visualRoot);

		for (var layerIndex = 0; layerIndex < _timeline.Layers.Length; layerIndex++)
		{
			var textures = _layerFrames[layerIndex];

			var sprite = new Sprite2D
			{
				Name = $"Layer{layerIndex:D2}",
				Centered = false,
				Position = new Vector2(-_timeline.OriginX, -_timeline.OriginY) + SpriteOffset,
				TextureFilter = TextureFilterEnum.Nearest,
				Texture = textures[0]
			};
			var material = CreateLayerMaterial(layerIndex);
			if (material != null)
			{
				sprite.Material = material;
				_layerMaterials.Add(material);
			}
			visualRoot.AddChild(sprite);
			sprite.Owner = this;
			_layerSprites.Add(sprite);
		}

		var bounds = _timeline.Bounds;
		var boundsPosition = new Vector2(bounds.X - _timeline.OriginX, bounds.Y - _timeline.OriginY) * VisualScale;
		var boundsSize = new Vector2(bounds.Width, bounds.Height) * VisualScale;
		AddOwnedChild(new Control
		{
			Name = "Bounds",
			UniqueNameInOwner = true,
			Position = boundsPosition,
			Size = boundsSize
		});
		AddOwnedChild(new Marker2D
		{
			Name = "IntentPos",
			UniqueNameInOwner = true,
			Position = new Vector2(boundsPosition.X + boundsSize.X / 2f, boundsPosition.Y - IntentGap) + IntentOffset
		});
		AddOwnedChild(new Marker2D
		{
			Name = "CenterPos",
			UniqueNameInOwner = true,
			Position = boundsPosition + boundsSize / 2f
		});
		AddOwnedChild(new Marker2D
		{
			Name = "TalkPos",
			UniqueNameInOwner = true,
			Position = boundsPosition + new Vector2(12f, 20f)
		});
	}

	private bool TryActivateTimeline(string timelinePath)
	{
		if (!TryLoadTimeline(timelinePath, out var timeline, out var frames) || frames.Count != _layerSprites.Count)
		{
			GD.PushError($"[FBE][BakedAnm] Timeline layer structure does not match Idle: {timelinePath}");
			return false;
		}

		_timeline = timeline;
		_layerFrames.Clear();
		_layerFrames.AddRange(frames);
		_frameIndex = 0;
		_elapsed = 0;
		ApplyFrame(0);
		return true;
	}

	private void RestoreIdleTimeline()
	{
		_isPlayingOneShot = false;
		if (!TryActivateTimeline(TimelinePath))
			return;
	}

	private bool TryLoadTimeline(string timelinePath, out BakedAnmTimeline timeline, out List<Texture2D[]> frames)
	{
		if (_timelineCache.TryGetValue(timelinePath, out var cached))
		{
			timeline = cached.Timeline;
			frames = cached.Frames;
			return true;
		}

		var json = FileAccess.GetFileAsString(timelinePath);
		var loadedTimeline = JsonSerializer.Deserialize<BakedAnmTimeline>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
		if (loadedTimeline is not { Schema: BakedAnmTimeline.SchemaName } ||
			loadedTimeline.Fps <= 0 || loadedTimeline.FrameCount <= 0 || loadedTimeline.Layers.Length == 0)
		{
			GD.PushError($"[FBE][BakedAnm] Invalid timeline: {timelinePath}");
			timeline = null!;
			frames = null!;
			return false;
		}

		var loadedFrames = new List<Texture2D[]>(loadedTimeline.Layers.Length);
		for (var layerIndex = 0; layerIndex < loadedTimeline.Layers.Length; layerIndex++)
		{
			var layer = loadedTimeline.Layers[layerIndex];
			if (layer.Frames.Length != loadedTimeline.FrameCount)
			{
				GD.PushError($"[FBE][BakedAnm] Layer {layerIndex} has an invalid frame count: {timelinePath}");
				timeline = null!;
				frames = null!;
				return false;
			}

			var textures = layer.Frames.Select(path => GD.Load<Texture2D>(path)).ToArray();
			if (textures.Any(texture => texture == null))
			{
				GD.PushError($"[FBE][BakedAnm] Missing frame texture in: {timelinePath}");
				timeline = null!;
				frames = null!;
				return false;
			}
			loadedFrames.Add(textures.Select(texture => texture!).ToArray());
		}

		timeline = loadedTimeline;
		frames = loadedFrames;
		_timelineCache.Add(timelinePath, (timeline, frames));
		return true;
	}

	private void ApplyFrame(int frameIndex)
	{
		for (var layerIndex = 0; layerIndex < _layerSprites.Count; layerIndex++)
			_layerSprites[layerIndex].Texture = _layerFrames[layerIndex][frameIndex];
	}

	private void AddOwnedChild(Node node)
	{
		AddChild(node);
		node.Owner = this;
	}

	private sealed class BakedAnmTimeline
	{
		public const string SchemaName = "FBE.BakedAnmTimeline/v1";
		public string Schema { get; init; } = string.Empty;
		public int Fps { get; init; }
		public int FrameCount { get; init; }
		public int CanvasWidth { get; init; }
		public int CanvasHeight { get; init; }
		public float OriginX { get; init; }
		public float OriginY { get; init; }
		public BakedAnmBounds Bounds { get; init; } = new();
		public BakedAnmLayer[] Layers { get; init; } = [];
	}

	private sealed class BakedAnmBounds
	{
		public float X { get; init; }
		public float Y { get; init; }
		public float Width { get; init; }
		public float Height { get; init; }
	}

	private sealed class BakedAnmLayer
	{
		public int LayerId { get; init; }
		public string[] Frames { get; init; } = [];
	}
}
