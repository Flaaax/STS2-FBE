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
	private double _elapsed;
	private int _frameIndex;

	protected abstract string TimelinePath { get; }
	protected virtual float VisualScale => 1f;
	/// <summary>只偏移动画贴图，不影响遭遇站位、碰撞范围或战斗 UI。</summary>
	protected virtual Vector2 SpriteOffset => Vector2.Zero;
	protected IReadOnlyList<ShaderMaterial> LayerMaterials => _layerMaterials;

	public override void _Ready()
	{
		LoadAndCreateVisuals();
		base._Ready();
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

	private void LoadAndCreateVisuals()
	{
		var json = FileAccess.GetFileAsString(TimelinePath);
		_timeline = JsonSerializer.Deserialize<BakedAnmTimeline>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
		if (_timeline is not { Schema: BakedAnmTimeline.SchemaName } ||
			_timeline.Fps <= 0 || _timeline.FrameCount <= 0 || _timeline.Layers.Length == 0)
		{
			GD.PushError($"[FBE][BakedAnm] Invalid timeline: {TimelinePath}");
			return;
		}

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
			var layer = _timeline.Layers[layerIndex];
			if (layer.Frames.Length != _timeline.FrameCount)
			{
				GD.PushError($"[FBE][BakedAnm] Layer {layerIndex} has an invalid frame count: {TimelinePath}");
				continue;
			}

			var textures = layer.Frames.Select(path => GD.Load<Texture2D>(path)).ToArray();
			if (textures.Any(texture => texture == null))
			{
				GD.PushError($"[FBE][BakedAnm] Missing frame texture in: {TimelinePath}");
				continue;
			}

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
			_layerFrames.Add(textures.Select(texture => texture!).ToArray());
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
			Position = new Vector2(boundsPosition.X + boundsSize.X / 2f, boundsPosition.Y - IntentGap)
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
