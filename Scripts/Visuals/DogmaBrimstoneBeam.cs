using System.Text.Json;
using FBE.Scripts.Monsters;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using FileAccess = Godot.FileAccess;

namespace FBE.Scripts.Visuals;

/// <summary>Dogma 一阶段向左硫磺火；本体动作之外独立维持并压缩。</summary>
public sealed partial class DogmaBrimstoneBeam : Node2D
{
	private const string TimelinePath = "res://FBE/animations/Dogma/dogma_brimstone_beam/timeline.json";
	private const string ShaderPath = "res://FBE/materials/dogma.gdshader";
	private const float Fps = 30f;
	private const int FireStartTick = 40;
	private const int CompressStartTick = 72;
	private const int CompressDurationTicks = 19;
	private const float LaserScale = 2.64f;
	private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private Texture2D[] _frames = [];
	private Sprite2D? _sprite;
	private ShaderMaterial? _material;
	private double _elapsed;
	private int _tick;
	private int _initialEffectFrame;
	private bool _started;
	private bool _hasTriggeredScreenShake;

	public Task Playback => _completion.Task;

	public override void _Ready()
	{
		LoadVisual();
	}

	public override void _ExitTree()
	{
		_completion.TrySetResult(false);
		base._ExitTree();
	}

	public void Start(int initialEffectFrame, Vector2 mouthAnchor)
	{
		_initialEffectFrame = initialEffectFrame;
		Position = mouthAnchor;
		_started = true;
		_hasTriggeredScreenShake = false;
		_tick = 0;
		_elapsed = 0;
		ApplyFrame();
	}

	public override void _Process(double delta)
	{
		if (!_started || _sprite == null)
			return;

		_elapsed += delta;
		while (_elapsed >= 1.0 / Fps)
		{
			_elapsed -= 1.0 / Fps;
			_tick++;
			if (_tick >= CompressStartTick + CompressDurationTicks)
			{
				_sprite.Visible = false;
				_started = false;
				_completion.TrySetResult(true);
				QueueFree();
				return;
			}
			ApplyFrame();
		}
	}

	private void LoadVisual()
	{
		var timeline = JsonSerializer.Deserialize<BeamTimeline>(FileAccess.GetFileAsString(TimelinePath),
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
		if (timeline is not { Schema: "FBE.BakedAnmTimeline/v1" } || timeline.Fps != Fps ||
			timeline.Layers.Length != 1 || timeline.Layers[0].Frames.Length != timeline.FrameCount)
		{
			GD.PushError($"[FBE][Dogma] Invalid Brimstone beam timeline: {TimelinePath}");
			_completion.TrySetResult(false);
			return;
		}

		var textures = timeline.Layers[0].Frames.Select(GD.Load<Texture2D>).ToArray();
		if (textures.Any(texture => texture == null))
		{
			GD.PushError($"[FBE][Dogma] Missing Brimstone beam texture: {TimelinePath}");
			_completion.TrySetResult(false);
			return;
		}
		_frames = textures.Select(texture => texture!).ToArray();

		_material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
		_material.SetShaderParameter("glitch_strength", 0f);
		_material.SetShaderParameter("eye_lit", 0f);
		_sprite = new Sprite2D
		{
			Centered = false,
			TextureFilter = TextureFilterEnum.Nearest,
			Texture = _frames[0],
			Position = new Vector2(-timeline.OriginX, -timeline.OriginY),
			Material = _material,
			Visible = false
		};
		Scale = Vector2.One * LaserScale;
		AddChild(_sprite);
	}

	private void ApplyFrame()
	{
		if (_sprite == null || _material == null || _frames.Length == 0)
			return;

		_sprite.Visible = _tick >= FireStartTick;
		// 蓄力期间固定首帧，避免发射前的负时间取余后成为负索引。
		var frameIndex = _tick < FireStartTick ? 0 : (_tick - FireStartTick) % _frames.Length;
		_sprite.Texture = _frames[frameIndex];
		if (_tick == FireStartTick && !_hasTriggeredScreenShake)
		{
			_hasTriggeredScreenShake = true;
			DogmaBattleAudio.PlayBrimstoneLaser();
			NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Normal);
		}
		var compressProgress = _tick < CompressStartTick
			? 1f
			: (CompressStartTick + CompressDurationTicks - _tick) / (float)CompressDurationTicks;
		Scale = new Vector2(LaserScale, LaserScale * MathF.Max(compressProgress, 0.01f));
		_material.SetShaderParameter("animation_frame", (_initialEffectFrame + _tick) % 256);
	}

	private sealed class BeamTimeline
	{
		public string Schema { get; init; } = string.Empty;
		public float Fps { get; init; }
		public int FrameCount { get; init; }
		public float OriginX { get; init; }
		public float OriginY { get; init; }
		public BeamLayer[] Layers { get; init; } = [];
	}

	private sealed class BeamLayer
	{
		public string[] Frames { get; init; } = [];
	}
}
