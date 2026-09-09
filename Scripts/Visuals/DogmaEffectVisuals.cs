using Godot;

namespace FBE.Scripts.Visuals;

/// <summary>Dogma 专用实时雪花、眼睛闪烁与横向失真控制器。</summary>
public abstract partial class DogmaEffectVisuals : BakedAnmVisuals
{
	private const string ShaderPath = "res://FBE/materials/dogma.gdshader";
	private readonly RandomNumberGenerator _random = new();
	private float _nextGlitch;
	private float _glitchRemaining;
	private double _eyeTickElapsed;
	private int _eyeTick;
	private int _effectFrame;
	private int _sourceEyeFrame;
	private int _consecutiveEyeOnTicks;
	private bool _sourceEyeState;
	private bool _eyeLit;
	private readonly Queue<(int ApplyAtTick, bool State)> _delayedEyeTransitions = [];

	protected abstract float GlitchIntervalMin { get; }
	protected abstract float GlitchIntervalMax { get; }
	protected abstract float GlitchDurationMin { get; }
	protected abstract float GlitchDurationMax { get; }

	/// <summary>对应原 shader 的 Colorize.r；电视与本体采用不同强度。</summary>
	protected abstract float ActiveGlitchStrength { get; }

	public override void _Ready()
	{
		_random.Randomize();
		// 预览 shader 的相位按 30 FPS 的 256 tick 循环。随机起点保留运行时随机，
		// 但雪花与眼睛共用同一帧号，避免脱离原始 shader 的时间关系。
		_effectFrame = _random.RandiRange(0, 255);
		_nextGlitch = _random.RandfRange(GlitchIntervalMin, GlitchIntervalMax);
		_sourceEyeFrame = _effectFrame;
		_sourceEyeState = EvaluateSourceEyeState(_sourceEyeFrame);
		_eyeLit = _sourceEyeState;
		base._Ready();
	}

	protected override ShaderMaterial? CreateLayerMaterial(int layerIndex)
	{
		var shader = GD.Load<Shader>(ShaderPath);
		if (shader == null)
		{
			GD.PushError($"[FBE][Dogma] Shader not found: {ShaderPath}");
			return null;
		}

		var material = new ShaderMaterial { Shader = shader };
		material.SetShaderParameter("animation_frame", _effectFrame);
		material.SetShaderParameter("glitch_strength", 0f);
		material.SetShaderParameter("eye_lit", 0f);
		return material;
	}

	protected override void UpdateLayerEffects(double delta)
	{
		var seconds = (float)delta;
		_nextGlitch -= seconds;
		if (_glitchRemaining > 0f)
		{
			_glitchRemaining -= seconds;
			if (_glitchRemaining <= 0f)
				_nextGlitch = _random.RandfRange(GlitchIntervalMin, GlitchIntervalMax);
		}
		else if (_nextGlitch <= 0f)
		{
			_glitchRemaining = _random.RandfRange(GlitchDurationMin, GlitchDurationMax);
		}


		// 严格复刻已确认预览脚本的眼睛逻辑：每个 30 FPS tick 先从 shader 的
		// simplex 状态取得开关，再将状态转换随机延后 0.05–0.15 秒，并限制亮眼不超过 0.1 秒。
		// 随机源只是不再固定为预览的 RNG_SEED。
		_eyeTickElapsed += delta;
		const double eyeTickDuration = 1.0 / 30.0;
		while (_eyeTickElapsed >= eyeTickDuration)
		{
			_eyeTickElapsed -= eyeTickDuration;
			AdvanceEyeTick();
		}

		var glitchStrength = _glitchRemaining > 0f ? ActiveGlitchStrength : 0f;
		var eyeLit = _eyeLit ? 1f : 0f;
		foreach (var material in LayerMaterials)
		{
			material.SetShaderParameter("animation_frame", _effectFrame);
			material.SetShaderParameter("glitch_strength", glitchStrength);
			material.SetShaderParameter("eye_lit", eyeLit);
		}
	}

	private void AdvanceEyeTick()
	{
		_eyeTick++;
		_effectFrame = (_effectFrame + 1) % 256;
		_sourceEyeFrame = (_sourceEyeFrame + 1) % 256;
		var nextSourceState = EvaluateSourceEyeState(_sourceEyeFrame);
		if (nextSourceState != _sourceEyeState)
		{
			// round(0.05–0.15 * 30) 在参考脚本中产生 2–4 tick 的延迟。
			_delayedEyeTransitions.Enqueue((_eyeTick + _random.RandiRange(2, 4), nextSourceState));
			_sourceEyeState = nextSourceState;
		}

		while (_delayedEyeTransitions.Count > 0 && _delayedEyeTransitions.Peek().ApplyAtTick <= _eyeTick)
			_eyeLit = _delayedEyeTransitions.Dequeue().State;

		if (_eyeLit)
		{
			_consecutiveEyeOnTicks++;
			if (_consecutiveEyeOnTicks > 3)
				_eyeLit = false;
		}
		else
		{
			_consecutiveEyeOnTicks = 0;
		}
	}

	private static bool EvaluateSourceEyeState(int frame)
	{
		return SimplexNoise((frame % 256) / 256f * 1000f, 0f) >= 0f;
	}

	/// <summary>与 coloroffset_dogma.fs 相同的二维 simplex noise 标量实现。</summary>
	private static float SimplexNoise(float x, float y)
	{
		const float c0 = 0.211324865405187f;
		const float c1 = 0.366025403784439f;
		const float c2 = -0.577350269189626f;
		const float c3 = 0.024390243902439f;
		var dot = (x + y) * c1;
		var ix = MathF.Floor(x + dot);
		var iy = MathF.Floor(y + dot);
		var dotI = (ix + iy) * c0;
		var x0 = x - ix + dotI;
		var y0 = y - iy + dotI;
		var i1x = x0 > y0 ? 1f : 0f;
		var i1y = 1f - i1x;
		var x1 = x0 + c0 - i1x;
		var y1 = y0 + c0 - i1y;
		var x2 = x0 + c2;
		var y2 = y0 + c2;
		ix = Mod289(ix);
		iy = Mod289(iy);
		var p0 = Permute(Permute(iy) + ix);
		var p1 = Permute(Permute(iy + i1y) + ix + i1x);
		var p2 = Permute(Permute(iy + 1f) + ix + 1f);
		return 130f * (Gradient(p0, x0, y0, c3) * Kernel(x0, y0, p0, c3) +
		               Gradient(p1, x1, y1, c3) * Kernel(x1, y1, p1, c3) +
		               Gradient(p2, x2, y2, c3) * Kernel(x2, y2, p2, c3));
	}

	private static float Kernel(float x, float y, float permutation, float inverse41)
	{
		var value = MathF.Max(0.5f - x * x - y * y, 0f);
		value *= value;
		value *= value;
		var gx = 2f * Fract(permutation * inverse41) - 1f;
		var h = MathF.Abs(gx) - 0.5f;
		var a0 = gx - MathF.Floor(gx + 0.5f);
		return value * (1.79284291400159f - 0.85373472095314f * (a0 * a0 + h * h));
	}

	private static float Gradient(float permutation, float x, float y, float inverse41)
	{
		var gx = 2f * Fract(permutation * inverse41) - 1f;
		var h = MathF.Abs(gx) - 0.5f;
		var a0 = gx - MathF.Floor(gx + 0.5f);
		return a0 * x + h * y;
	}

	private static float Mod289(float value) => value - MathF.Floor(value / 289f) * 289f;
	private static float Permute(float value) => Mod289((value * 34f + 1f) * value);
	private static float Fract(float value) => value - MathF.Floor(value);
}