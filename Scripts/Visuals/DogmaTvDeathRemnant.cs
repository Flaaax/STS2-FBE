using System.Text.Json;
using Godot;
using FileAccess = Godot.FileAccess;

namespace FBE.Scripts.Visuals;

/// <summary>从电视实体中脱离的死亡贴图；战斗场景销毁前始终保留。</summary>
public sealed partial class DogmaTvDeathRemnant : Node2D
{
	private const string TimelinePath = "res://FBE/animations/Dogma/dogma_tv_destroyed/timeline.json";
	private const string ShaderPath = "res://FBE/materials/dogma.gdshader";
	private const float VisualScale = 4f;
	private readonly List<Sprite2D> _sprites = [];
	private readonly List<Texture2D[]> _frames = [];
	private readonly List<ShaderMaterial> _materials = [];
	private DeathTimeline? _timeline;
	private double _elapsed;
	private int _frameIndex;
	private int _effectFrame;

	public override void _Ready()
	{
		var json = FileAccess.GetFileAsString(TimelinePath);
		_timeline = JsonSerializer.Deserialize<DeathTimeline>(json,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
		if (_timeline is not { Schema: "FBE.BakedAnmTimeline/v1" } || _timeline.Fps <= 0 ||
			_timeline.FrameCount <= 0 || _timeline.Layers.Length == 0)
		{
			GD.PushError($"[FBE][Dogma] Invalid television death timeline: {TimelinePath}");
			QueueFree();
			return;
		}

		var visualRoot = new Node2D { TextureFilter = TextureFilterEnum.Nearest, Scale = Vector2.One * VisualScale };
		AddChild(visualRoot);
		foreach (var layer in _timeline.Layers)
		{
			if (layer.Frames.Length != _timeline.FrameCount)
				continue;
			var textures = layer.Frames.Select(GD.Load<Texture2D>).ToArray();
			if (textures.Any(texture => texture == null))
				continue;
			var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
			material.SetShaderParameter("animation_frame", 0);
			material.SetShaderParameter("glitch_strength", 0f);
			material.SetShaderParameter("eye_lit", 0f);
			var sprite = new Sprite2D
			{
				Centered = false,
				Position = new Vector2(-_timeline.OriginX, -_timeline.OriginY),
				TextureFilter = TextureFilterEnum.Nearest,
				Texture = textures[0],
				Material = material
			};
			visualRoot.AddChild(sprite);
			_sprites.Add(sprite);
			_frames.Add(textures.Select(texture => texture!).ToArray());
			_materials.Add(material);
		}
	}

	public override void _Process(double delta)
	{
		if (_timeline == null || _frameIndex >= _timeline.FrameCount - 1)
			return;
		_elapsed += delta;
		var duration = 1.0 / _timeline.Fps;
		while (_elapsed >= duration && _frameIndex < _timeline.FrameCount - 1)
		{
			_elapsed -= duration;
			_frameIndex++;
			_effectFrame = (_effectFrame + 1) % 256;
			for (var index = 0; index < _sprites.Count; index++)
				_sprites[index].Texture = _frames[index][_frameIndex];
			foreach (var material in _materials)
				material.SetShaderParameter("animation_frame", _effectFrame);
		}
	}

	private sealed class DeathTimeline
	{
		public string Schema { get; init; } = string.Empty;
		public int Fps { get; init; }
		public int FrameCount { get; init; }
		public float OriginX { get; init; }
		public float OriginY { get; init; }
		public DeathLayer[] Layers { get; init; } = [];
	}

	private sealed class DeathLayer
	{
		public string[] Frames { get; init; } = [];
	}
}
